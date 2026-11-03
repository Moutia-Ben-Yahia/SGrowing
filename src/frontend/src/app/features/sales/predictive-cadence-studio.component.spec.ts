// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import {
  PredictiveCadenceStudioComponent,
  CadenceChannelUi,
  ContactTimeBandUi
} from './predictive-cadence-studio.component';

describe('PredictiveCadenceStudioComponent (Predictive Sales Cadence & Heatmap)', () => {
  let component: PredictiveCadenceStudioComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue(of({ slots: [] })),
      post: vi.fn().mockReturnValue(of({}))
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new PredictiveCadenceStudioComponent());
    component.ngOnInit();
  });

  it('should initialize with cadences, heatmap slots, and enrolled leads', () => {
    expect(component).toBeTruthy();
    expect(component.cadences().length).toBe(3);
    expect(component.enrollments().length).toBe(6);
    expect(component.heatmapSlots().length).toBe(168); // 7 days * 24 hours
    expect(component.selectedCadenceId()).toBe('c1111111-0000-0000-0000-000000000001');
    expect(component.dialerQueue().length).toBeGreaterThan(0);
  });

  it('should regenerate heatmap and refresh advisor recommendations on timezone change', () => {
    component.onTimezoneChange('UTC+4');
    expect(component.selectedTimezone()).toBe('UTC+4');
    expect(component.heatmapSlots().length).toBe(168);
    expect(component.recommendedSlots().length).toBeGreaterThan(0);
  });

  it('should pause, resume, and advance lead enrollments', () => {
    const activeLead = component.enrollments().find(e => e.status === 'Active')!;
    expect(activeLead).toBeTruthy();

    component.pauseEnrollment(activeLead);
    let updated = component.enrollments().find(e => e.id === activeLead.id)!;
    expect(updated.status).toBe('Paused');
    expect(updated.pauseReason).toContain('Manually paused');

    component.resumeEnrollment(activeLead);
    updated = component.enrollments().find(e => e.id === activeLead.id)!;
    expect(updated.status).toBe('Active');
    expect(updated.pauseReason).toBeUndefined();

    const initialStep = updated.currentStepNumber;
    component.advanceEnrollment(activeLead);
    updated = component.enrollments().find(e => e.id === activeLead.id)!;
    expect(updated.currentStepNumber).toBe(initialStep + 1);
  });

  it('should simulate incoming reply and auto-pause sequence', () => {
    const telemetryBefore = component.telemetryCount();
    component.simulateIncomingReply();

    expect(component.telemetryCount()).toBe(telemetryBefore + 1);
    const replied = component.enrollments().find(e => e.status === 'Replied');
    expect(replied).toBeTruthy();
    expect(replied?.pauseReason).toContain('Auto-paused: Prospect replied via WhatsApp');
  });

  it('should simulate call outcome Answered and update enrollee status', () => {
    const dialerItem = component.dialerQueue()[0];
    component.simulateCall(dialerItem, 'Answered');

    const updated = component.enrollments().find(e => e.id === dialerItem.enrollmentId)!;
    expect(updated.status).toBe('Replied');
    expect(updated.pauseReason).toContain('answered softphone call');
  });

  it('should return appropriate CSS classes for slots, bands, and channels', () => {
    expect(component.getBandBadgeClass('Peak')).toContain('bg-emerald-500/20');
    expect(component.getBandBadgeClass('Low')).toContain('bg-slate-800');
    expect(component.getChannelBadgeClass('WhatsApp')).toContain('text-emerald-400');
    expect(component.getChannelBadgeClass('Phone')).toContain('text-blue-400');
    expect(component.getStatusBadgeClass('Active')).toContain('text-emerald-400');
    expect(component.getStatusBadgeClass('Paused')).toContain('text-amber-400');
  });
});
