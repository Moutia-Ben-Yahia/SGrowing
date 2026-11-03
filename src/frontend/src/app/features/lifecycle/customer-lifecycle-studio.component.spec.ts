// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { CustomerLifecycleStudioComponent } from './customer-lifecycle-studio.component';

describe('CustomerLifecycleStudioComponent (DAG Journey Canvas & Multi-Channel Failover)', () => {
  let component: CustomerLifecycleStudioComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/active-contacts')) {
          return of([
            {
              id: 'ptr-test-1',
              journeyId: '11111111-2222-3333-4444-555555555551',
              contactId: 'contact_lead_1001',
              currentNodeId: 'saas_act_welcome',
              status: 'InProgress',
              enteredAtUtc: new Date().toISOString(),
              lastStepAtUtc: new Date().toISOString(),
              contactProperties: {},
              breadcrumbs: []
            }
          ]);
        }
        return of([
          {
            id: '11111111-2222-3333-4444-555555555551',
            tenantId: '00000000-0000-0000-0000-000000000001',
            name: 'SaaS Onboarding & Activation',
            description: 'Automated onboarding flow with activation condition checks, temporal delays, and cross-channel failover.',
            status: 'Active',
            createdAtUtc: new Date().toISOString(),
            updatedAtUtc: new Date().toISOString(),
            totalEnrolled: 142,
            totalCompleted: 98,
            totalExited: 4,
            activeCount: 40,
            fallbackRecoveryCount: 16,
            conversionRate: 69.0,
            nodes: [
              {
                id: 'saas_trg_1',
                name: 'User Sign-up Event',
                type: 'Trigger',
                positionX: 100,
                positionY: 150,
                nextNodeIds: ['saas_act_welcome'],
                triggerConfig: { type: 'Event', eventName: 'user_signed_up', segmentName: '' }
              },
              {
                id: 'saas_act_welcome',
                name: 'Welcome Email & Checklist',
                type: 'Action',
                positionX: 350,
                positionY: 150,
                nextNodeIds: [],
                actionConfig: {
                  channel: 'Email',
                  fallbackChannel: 'WhatsApp',
                  subject: 'Welcome to SGrowing',
                  messageBody: 'Getting started guide',
                  simulateChannelFailure: false
                }
              }
            ],
            edges: []
          }
        ]);
      }),
      post: vi.fn().mockImplementation((url: string, body: any) => {
        if (url.includes('/trigger-contact')) {
          return of({
            pointer: {
              contactId: body?.contactId || 'contact_lead_9999',
              journeyId: '11111111-2222-3333-4444-555555555551',
              currentNodeId: 'saas_act_welcome',
              status: 'InProgress',
              enteredAtUtc: new Date().toISOString(),
              lastStepAtUtc: new Date().toISOString(),
              contactProperties: {},
              breadcrumbs: []
            },
            executedSteps: []
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

    component = runInInjectionContext(injector, () => new CustomerLifecycleStudioComponent());
    component.ngOnInit();
  });

  it('should initialize with default journeys and select active onboarding journey', () => {
    expect(component).toBeTruthy();
    expect(component.journeys().length).toBeGreaterThan(0);
    expect(component.activeJourney()).not.toBeNull();
    expect(component.activeJourney()?.name).toContain('Onboarding');
  });

  it('should select node and find available target nodes for DAG traversal', () => {
    const firstNode = component.activeJourney()!.nodes[0];
    component.selectNode(firstNode);

    expect(component.selectedNode()).not.toBeNull();
    expect(component.selectedNode()?.id).toBe(firstNode.id);

    const availableTargets = component.getAvailableTargetNodes();
    expect(availableTargets.every(n => n.id !== firstNode.id)).toBe(true);
  });

  it('should calculate active contact count at a specific DAG node', () => {
    const count = component.getActiveContactsCountAtNode('saas_act_welcome');
    expect(count).toBeGreaterThanOrEqual(1);
  });

  it('should open trigger modal and execute contact enrollment in journey', () => {
    component.openTriggerModal();
    expect(component.showTriggerModal()).toBe(true);
    expect(component.triggerForm.contactId).toContain('contact_lead_');

    component.executeTriggerContact();
    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.showTriggerModal()).toBe(false);
  });
});
