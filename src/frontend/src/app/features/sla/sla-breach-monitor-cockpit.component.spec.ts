// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import {
  SlaBreachMonitorCockpitComponent,
  SlaEscalationStage,
  SlaRiskLevel,
  TicketSlaProjectionDto
} from './sla-breach-monitor-cockpit.component';

describe('SlaBreachMonitorCockpitComponent (Predictive SLA Breach Monitor)', () => {
  let component: SlaBreachMonitorCockpitComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/tickets')) {
          return of([]);
        }
        if (url.includes('/metrics')) {
          return of({
            firstResponseCompliancePercentage: 99.0,
            resolutionCompliancePercentage: 95.0,
            totalMonitoredTickets: 6,
            imminentBreachesCount: 2,
            criticalCount: 1,
            warningCount: 1,
            safeCount: 3,
            breachedCount: 1,
            activeQueueVelocity: 0.05,
            lastSweepAtUtc: new Date().toISOString()
          });
        }
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string, body: any) => {
        if (url.includes('/evaluate')) {
          return of({
            ticketId: body.ticketId,
            ticketNumber: 'TICK-4091',
            queueAhead: body.queueAhead,
            bufferMinutes: -40,
            riskLevel: SlaRiskLevel.ImminentBreach
          });
        }
        if (url.includes('/reassign')) {
          return of({
            ticketId: body.ticketId,
            ticketNumber: 'TICK-4091',
            assignedAgentId: body.targetAgentId,
            assignedAgentName: body.targetAgentName,
            assignedAgentTier: 'Supervisor Reassigned (Tier-2)',
            autoReassigned: true
          });
        }
        if (url.includes('/run-sweep')) {
          return of({
            sweepTimeUtc: new Date().toISOString(),
            evaluatedCount: 6,
            warningEscalationsCount: 1,
            criticalEscalationsCount: 1,
            autoReassignedCount: 1,
            breachedCount: 0,
            tickets: []
          });
        }
        return of({});
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new SlaBreachMonitorCockpitComponent());
    component.ngOnInit();
  });

  it('should initialize and load seeded tickets and metrics', () => {
    expect(component).toBeTruthy();
    expect(component.tickets().length).toBeGreaterThan(0);
    expect(component.metrics()?.totalMonitoredTickets).toBe(6);
    expect(mockHttp.get).toHaveBeenCalledWith('/api/v1/support/sla-monitor/tickets');
    expect(mockHttp.get).toHaveBeenCalledWith('/api/v1/support/sla-monitor/metrics');
  });

  it('should filter tickets by risk level and text query', () => {
    component.selectedRiskFilter.set('ImminentBreach');
    const filteredImminent = component.filteredTickets();
    expect(filteredImminent.every(t => t.riskLevel === SlaRiskLevel.ImminentBreach)).toBe(true);

    component.selectedRiskFilter.set('All');
    component.searchQuery.set('SAML');
    const filteredSearch = component.filteredTickets();
    expect(filteredSearch.length).toBe(1);
    expect(filteredSearch[0].ticketNumber).toBe('TICK-4091');
  });

  it('should open evaluate modal and execute ticket evaluation via API', () => {
    const target = component.tickets()[0];
    component.openEvaluateModal(target);
    expect(component.evaluateModalTicket()).toBe(target);
    expect(component.evalQueueAhead).toBe(target.queueAhead);

    component.evalQueueAhead = 4;
    component.executeEvaluateTicket();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/support/sla-monitor/evaluate',
      expect.objectContaining({
        ticketId: target.ticketId,
        queueAhead: 4
      })
    );
    expect(component.isEvaluating()).toBe(false);
    expect(component.evaluateModalTicket()).toBeNull();
    expect(component.notificationBanner()?.type).toBe('success');
  });

  it('should execute manual reassign and update assignee state', () => {
    const target = component.tickets()[1];
    component.openReassignModal(target);
    expect(component.reassignModalTicket()).toBe(target);

    component.executeManualReassign();

    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/support/sla-monitor/reassign',
      expect.objectContaining({
        ticketId: target.ticketId
      })
    );
    expect(component.isReassigning()).toBe(false);
    expect(component.reassignModalTicket()).toBeNull();
    expect(component.notificationBanner()?.message).toContain('manually reassigned');
  });

  it('should run escalation sweep and handle sweep results', () => {
    component.runEscalationSweep();
    expect(mockHttp.post).toHaveBeenCalledWith('/api/v1/support/sla-monitor/run-sweep', {});
    expect(component.isSweeping()).toBe(false);
    expect(component.notificationBanner()?.message).toContain('Escalation sweep executed successfully');
  });

  it('should return visual badges and icons according to risk and stage', () => {
    expect(component.getStageBadgeClass(SlaEscalationStage.Critical)).toContain('bg-amber-950');
    expect(component.getStageBadgeClass(SlaEscalationStage.Nominal)).toContain('bg-emerald-950');
    expect(component.getProgressBarColor(SlaEscalationStage.Breached)).toBe('bg-rose-600');
    expect(component.getBufferIcon(SlaRiskLevel.ImminentBreach)).toBe('🔥');
    expect(component.getBufferIcon(SlaRiskLevel.Safe)).toBe('🛡️');
    expect(component.getBufferBadgeClass(-20, SlaRiskLevel.ImminentBreach)).toContain('animate-pulse');
  });
});
