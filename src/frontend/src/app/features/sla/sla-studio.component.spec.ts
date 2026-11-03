// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of, throwError } from 'rxjs';
import { SlaStudioComponent } from './sla-studio.component';
import { ApiService } from '../../core/services/api.service';
import { SlaPolicyDto } from '../../core/models/platform.models';

describe('SlaStudioComponent', () => {
  let component: SlaStudioComponent;
  let apiServiceMock: {
    getSlaPolicies: ReturnType<typeof vi.fn>;
    createSlaPolicy: ReturnType<typeof vi.fn>;
    evaluateSla: ReturnType<typeof vi.fn>;
  };

  const samplePolicies: SlaPolicyDto[] = [
    {
      id: 'sla-tier1',
      tenantId: '00000000-0000-0000-0000-000000000001',
      name: 'Tier 1 Enterprise SLA',
      priority: 4,
      targetFirstResponseMinutes: 15,
      targetResolutionMinutes: 60,
      isActive: true
    },
    {
      id: 'sla-tier2',
      tenantId: '00000000-0000-0000-0000-000000000001',
      name: 'General Inquiries SLA',
      priority: 2,
      targetFirstResponseMinutes: 120,
      targetResolutionMinutes: 720,
      isActive: true
    }
  ];

  beforeEach(() => {
    apiServiceMock = {
      getSlaPolicies: vi.fn().mockReturnValue(of(samplePolicies)),
      createSlaPolicy: vi.fn(),
      evaluateSla: vi.fn().mockReturnValue(of({ breachedCount: 3 }))
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: apiServiceMock }
      ]
    });

    component = runInInjectionContext(injector, () => new SlaStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load SLA policies from ApiService', () => {
    expect(component).toBeTruthy();
    expect(apiServiceMock.getSlaPolicies).toHaveBeenCalledWith(component.tenantId);
    expect(component.policies().length).toBe(2);
    expect(component.policies()[0].name).toBe('Tier 1 Enterprise SLA');
  });

  it('should fallback to default demo policies if getSlaPolicies throws error', () => {
    apiServiceMock.getSlaPolicies.mockReturnValue(throwError(() => new Error('Network error')));
    component.loadPolicies();
    expect(component.policies().length).toBe(2);
    expect(component.policies()[0].name).toBe('Urgent Support Escalation SLA');
  });

  it('should create new policy via ApiService and reset input fields', () => {
    const created: SlaPolicyDto = {
      id: 'sla-new-99',
      tenantId: component.tenantId,
      name: 'VIP Urgent SLA',
      priority: 4,
      targetFirstResponseMinutes: 10,
      targetResolutionMinutes: 60,
      isActive: true
    };
    apiServiceMock.createSlaPolicy.mockReturnValue(of(created));

    component.newPolicyName = 'VIP Urgent SLA';
    component.newPolicyPriority = 4;
    component.newTargetFirstResponse = 10;
    component.newTargetResolution = 60;

    component.createPolicy();

    expect(apiServiceMock.createSlaPolicy).toHaveBeenCalledWith(expect.objectContaining({
      name: 'VIP Urgent SLA',
      priority: 4,
      targetFirstResponseMinutes: 10,
      targetResolutionMinutes: 60
    }));
    expect(component.policies()).toContainEqual(created);
    expect(component.newPolicyName).toBe('');
  });

  it('should evaluate SLA breaches and update evaluatedBreaches count', () => {
    component.evaluateSla();
    expect(apiServiceMock.evaluateSla).toHaveBeenCalledWith(component.tenantId);
    expect(component.evaluatedBreaches()).toBe(3);
    expect(component.isEvaluating()).toBe(false);
  });

  it('should map priority integers to readable labels correctly', () => {
    expect(component.getPriorityLabel(4)).toBe('Urgent');
    expect(component.getPriorityLabel(3)).toBe('High');
    expect(component.getPriorityLabel(2)).toBe('Normal');
    expect(component.getPriorityLabel(1)).toBe('Low');
    expect(component.getPriorityLabel(0)).toBe('Normal');
  });
});
