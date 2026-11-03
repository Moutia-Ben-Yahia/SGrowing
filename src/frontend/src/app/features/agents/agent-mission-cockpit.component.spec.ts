// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { AgentMissionCockpitComponent } from './agent-mission-cockpit.component';

describe('AgentMissionCockpitComponent', () => {
  let component: AgentMissionCockpitComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue(of({})),
      post: vi.fn().mockReturnValue(of({ success: true }))
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new AgentMissionCockpitComponent());
    component.ngOnInit();
  });

  it('should initialize with default pre-configured RevOps agent mission and steps', () => {
    expect(component).toBeTruthy();
    expect(component.currentMission()).not.toBeNull();
    expect(component.currentMission()?.title).toContain('Q4 Maghreb Retail Outreach');
    expect(component.currentMission()?.steps.length).toBe(5);
    expect(component.budgetUsagePercent()).toBeGreaterThan(0);
  });

  it('should approve sensitive step in human-in-the-loop mode and advance mission', () => {
    component.approveStep('step-3');

    const mission = component.currentMission();
    const approvedStep = mission?.steps.find(s => s.id === 'step-3');
    expect(approvedStep?.status).toBe('Completed');
    expect(approvedStep?.approvedBy).toBe('Operations Lead');
    expect(mission?.status).toBe('Running');
    expect(mission?.currentStepIndex).toBe(3);
  });

  it('should pause and resume mission execution loop', () => {
    component.pauseMission();
    expect(component.currentMission()?.status).toBe('PausedForApproval');

    component.resumeMission();
    expect(component.currentMission()?.status).toBe('Running');
  });

  it('should map tool system names to friendly user-facing labels', () => {
    expect(component.getToolDisplayName('crm_search_leads')).toBe('Search CRM Prospects');
    expect(component.getToolDisplayName('outbound_send_spintax_campaign')).toBe('Dispatch Spintax Campaign');
    expect(component.getToolDisplayName('courier_generate_waybill')).toBe('Generate COD Waybill');
  });
});
