// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of } from 'rxjs';
import { CustomerJourneyRuntimeCockpitComponent } from './customer-journey-runtime-cockpit.component';
import { ApiService } from '../../core/services/api.service';
import {
  JourneyNodeType,
  JourneyExecutionStatus,
  CustomerJourneyProgress,
  ActiveJourneySummary
} from '../../core/models/customer-journey-runtime.models';

describe('CustomerJourneyRuntimeCockpitComponent', () => {
  let component: CustomerJourneyRuntimeCockpitComponent;
  let mockApi: any;

  const mockActiveJourneys: ActiveJourneySummary[] = [
    {
      journeyId: 'journey-ramadan-cart-recovery',
      title: 'Ramadan COD Abandoned Cart Recovery & Retention Journey',
      description: 'Production-grade Ramadan COD cart recovery',
      triggerEvent: 'cart.abandoned',
      nodeCount: 5,
      enrolledContacts: 1420,
      inDelayNodes: 380,
      convertedContacts: 940,
      conversionRatePercentage: 66.2
    }
  ];

  const mockContacts: CustomerJourneyProgress[] = [
    {
      progressId: 'p-1',
      tenantId: '00000000-0000-0000-0000-000000000001',
      journeyId: 'journey-ramadan-cart-recovery',
      contactId: 'c-1',
      contactEmail: 'sultan.aldosari@growth.sa',
      currentNodeId: 'node-4a-action-whatsapp-promo',
      status: JourneyExecutionStatus.Active,
      executionHistory: [
        {
          stepId: 's-1',
          nodeId: 'node-1-trigger-cart-abandoned',
          nodeType: JourneyNodeType.TriggerNode,
          executedAtUtc: '2026-11-07T12:00:00Z',
          outcome: 'TriggerEnrolled',
          details: 'Cart abandoned event received (450 SAR).'
        },
        {
          stepId: 's-2',
          nodeId: 'node-4a-action-whatsapp-promo',
          nodeType: JourneyNodeType.ActionNode,
          executedAtUtc: '2026-11-07T12:15:00Z',
          outcome: 'WhatsAppPromoDispatched',
          details: 'WhatsApp 1-Click COD Discount Promo delivered.'
        }
      ],
      contextData: { cart_amount: '450 SAR', contact_name: 'Sultan Al-Dosari' }
    },
    {
      progressId: 'p-2',
      tenantId: '00000000-0000-0000-0000-000000000001',
      journeyId: 'journey-ramadan-cart-recovery',
      contactId: 'c-2',
      contactEmail: 'fatima.zahrani@growth.sa',
      currentNodeId: 'node-2-delay-15m',
      status: JourneyExecutionStatus.PausedWaitingDelay,
      executionHistory: [
        {
          stepId: 's-3',
          nodeId: 'node-2-delay-15m',
          nodeType: JourneyNodeType.DelayNode,
          executedAtUtc: '2026-11-07T12:10:00Z',
          outcome: 'PausedWaitingDelay',
          details: 'Waiting 15m delay simulation.'
        }
      ],
      contextData: { cart_amount: '180 SAR', contact_name: 'Fatima Al-Zahrani' },
      resumeAtUtc: '2026-11-07T12:25:00Z'
    }
  ];

  beforeEach(() => {
    mockApi = {
      getActiveJourneyRuntimeSummaries: vi.fn().mockReturnValue(of(mockActiveJourneys)),
      getJourneyRuntimeContacts: vi.fn().mockReturnValue(of(mockContacts)),
      triggerJourneyRuntime: vi.fn(),
      advanceJourneyRuntime: vi.fn(),
      getJourneyRuntimeProgress: vi.fn()
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi }
      ]
    });

    component = runInInjectionContext(injector, () => new CustomerJourneyRuntimeCockpitComponent());
    component.ngOnInit();
  });

  it('should initialize component and load active journeys and initial contacts', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getActiveJourneyRuntimeSummaries).toHaveBeenCalledWith(component.tenantId);
    expect(mockApi.getJourneyRuntimeContacts).toHaveBeenCalledWith('journey-ramadan-cart-recovery', component.tenantId);
    expect(component.activeJourneys().length).toBe(1);
    expect(component.contacts().length).toBe(2);
    expect(component.recentTimeline().length).toBeGreaterThan(0);
  });

  it('should compute top stats correctly (1,420 enrolled, 380 delay, 940 converted, 66.2% rate)', () => {
    expect(component.enrolledCount()).toBe(1420);
    expect(component.delayCount()).toBe(380);
    expect(component.convertedCount()).toBe(940);
    expect(component.conversionRate()).toBe(66.2);
  });

  it('should change selected journey and reload contacts when selectJourney is invoked', () => {
    component.selectJourney('journey-test-another');
    expect(component.selectedJourneyId()).toBe('journey-test-another');
    expect(mockApi.getJourneyRuntimeContacts).toHaveBeenCalledWith('journey-test-another', component.tenantId);
  });

  it('should open and close simulation drawer and set cart amount presets', () => {
    expect(component.isSimulateDrawerOpen()).toBe(false);
    component.openSimulateDrawer();
    expect(component.isSimulateDrawerOpen()).toBe(true);

    component.setCartPreset(150);
    expect(component.simCartAmount()).toBe(150);

    component.setCartPreset(450);
    expect(component.simCartAmount()).toBe(450);

    component.closeSimulateDrawer();
    expect(component.isSimulateDrawerOpen()).toBe(false);
  });

  it('should simulate inbound trigger event and pause at 15m delay node', () => {
    const mockTriggerResult: CustomerJourneyProgress = {
      progressId: 'sim-new-1',
      tenantId: component.tenantId,
      journeyId: 'journey-ramadan-cart-recovery',
      contactId: 'c-sim-1',
      contactEmail: 'nasser.otai@saudi.sa',
      currentNodeId: 'node-2-delay-15m',
      status: JourneyExecutionStatus.PausedWaitingDelay,
      executionHistory: [
        {
          stepId: 'st-1',
          nodeId: 'node-1-trigger-cart-abandoned',
          nodeType: JourneyNodeType.TriggerNode,
          executedAtUtc: '2026-11-07T13:00:00Z',
          outcome: 'TriggerEnrolled',
          details: 'Cart abandoned correlated.'
        },
        {
          stepId: 'st-2',
          nodeId: 'node-2-delay-15m',
          nodeType: JourneyNodeType.DelayNode,
          executedAtUtc: '2026-11-07T13:00:01Z',
          outcome: 'PausedWaitingDelay',
          details: '15-minute Ramadan COD recovery delay scheduled.'
        }
      ],
      contextData: { cart_amount: '450 SAR' },
      resumeAtUtc: '2026-11-07T13:15:01Z'
    };

    mockApi.triggerJourneyRuntime.mockReturnValue(of(mockTriggerResult));

    component.simContactEmail.set('nasser.otai@saudi.sa');
    component.simCartAmount.set(450);
    component.runTriggerSimulation();

    expect(mockApi.triggerJourneyRuntime).toHaveBeenCalledWith({
      tenantId: component.tenantId,
      eventName: 'cart.abandoned',
      contactId: expect.any(String),
      contactEmail: 'nasser.otai@saudi.sa',
      payloadData: expect.objectContaining({
        cart_amount: '450 SAR',
        currency: 'SAR'
      })
    });

    expect(component.isSimulating()).toBe(false);
    expect(component.currentSimulationProgress()).toEqual(mockTriggerResult);
    expect(component.simulationLogs().length).toBe(2);
    expect(component.simulationBanner()).toContain('15m delay simulation');
  });

  it('should advance simulation with High Cart value (>= 300 SAR) and route to VIP WhatsApp Promo', () => {
    const mockTrigger: CustomerJourneyProgress = {
      progressId: 'sim-vip-1',
      tenantId: component.tenantId,
      journeyId: 'journey-ramadan-cart-recovery',
      contactId: 'c-vip',
      contactEmail: 'vip.customer@saudi.sa',
      currentNodeId: 'node-2-delay-15m',
      status: JourneyExecutionStatus.PausedWaitingDelay,
      executionHistory: [
        { stepId: '1', nodeId: 'node-1-trigger-cart-abandoned', nodeType: JourneyNodeType.TriggerNode, executedAtUtc: '2026-11-07T13:00:00Z', outcome: 'TriggerEnrolled', details: 'Cart 450 SAR' },
        { stepId: '2', nodeId: 'node-2-delay-15m', nodeType: JourneyNodeType.DelayNode, executedAtUtc: '2026-11-07T13:00:01Z', outcome: 'PausedWaitingDelay', details: 'Waiting 15m' }
      ],
      contextData: { cart_amount: '450 SAR' }
    };

    const mockAdvanced: CustomerJourneyProgress = {
      ...mockTrigger,
      currentNodeId: 'node-5-action-conversion-checkpoint',
      status: JourneyExecutionStatus.Completed,
      executionHistory: [
        ...mockTrigger.executionHistory,
        { stepId: '3', nodeId: 'node-2-delay-15m', nodeType: JourneyNodeType.DelayNode, executedAtUtc: '2026-11-07T13:15:00Z', outcome: 'DelayCompleted', details: 'Delay elapsed' },
        { stepId: '4', nodeId: 'node-3-condition-cart-threshold', nodeType: JourneyNodeType.ConditionNode, executedAtUtc: '2026-11-07T13:15:01Z', outcome: 'ConditionTrue: HighCartValue', details: '450 SAR >= 300 SAR' },
        { stepId: '5', nodeId: 'node-4a-action-whatsapp-promo', nodeType: JourneyNodeType.ActionNode, executedAtUtc: '2026-11-07T13:15:02Z', outcome: 'WhatsAppPromoDispatched', details: 'Dispatched VIP WhatsApp Promo' },
        { stepId: '6', nodeId: 'node-5-action-conversion-checkpoint', nodeType: JourneyNodeType.ActionNode, executedAtUtc: '2026-11-07T13:15:03Z', outcome: 'ConversionCheckpointReached', details: 'Conversion completed' }
      ],
      completedAtUtc: '2026-11-07T13:15:03Z'
    };

    component.currentSimulationProgress.set(mockTrigger);
    component.simCartAmount.set(450);
    mockApi.advanceJourneyRuntime.mockReturnValue(of(mockAdvanced));

    component.advanceSimulation();

    expect(mockApi.advanceJourneyRuntime).toHaveBeenCalledWith({
      tenantId: component.tenantId,
      progressId: 'sim-vip-1'
    });

    expect(component.currentSimulationProgress()?.currentNodeId).toBe('node-5-action-conversion-checkpoint');
    expect(component.currentSimulationProgress()?.status).toBe(JourneyExecutionStatus.Completed);
    expect(component.simulationBanner()).toContain('VIP WhatsApp 1-Click COD Promo');
  });

  it('should advance simulation with Low Cart value (< 300 SAR) and route to Standard Email Voucher', () => {
    const mockTrigger: CustomerJourneyProgress = {
      progressId: 'sim-low-1',
      tenantId: component.tenantId,
      journeyId: 'journey-ramadan-cart-recovery',
      contactId: 'c-low',
      contactEmail: 'low.customer@saudi.sa',
      currentNodeId: 'node-2-delay-15m',
      status: JourneyExecutionStatus.PausedWaitingDelay,
      executionHistory: [
        { stepId: '1', nodeId: 'node-1-trigger-cart-abandoned', nodeType: JourneyNodeType.TriggerNode, executedAtUtc: '2026-11-07T13:00:00Z', outcome: 'TriggerEnrolled', details: 'Cart 150 SAR' },
        { stepId: '2', nodeId: 'node-2-delay-15m', nodeType: JourneyNodeType.DelayNode, executedAtUtc: '2026-11-07T13:00:01Z', outcome: 'PausedWaitingDelay', details: 'Waiting 15m' }
      ],
      contextData: { cart_amount: '150 SAR' }
    };

    const mockAdvanced: CustomerJourneyProgress = {
      ...mockTrigger,
      currentNodeId: 'node-5-action-conversion-checkpoint',
      status: JourneyExecutionStatus.Completed,
      executionHistory: [
        ...mockTrigger.executionHistory,
        { stepId: '3', nodeId: 'node-2-delay-15m', nodeType: JourneyNodeType.DelayNode, executedAtUtc: '2026-11-07T13:15:00Z', outcome: 'DelayCompleted', details: 'Delay elapsed' },
        { stepId: '4', nodeId: 'node-3-condition-cart-threshold', nodeType: JourneyNodeType.ConditionNode, executedAtUtc: '2026-11-07T13:15:01Z', outcome: 'ConditionFalse: StandardCartValue', details: '150 SAR < 300 SAR' },
        { stepId: '5', nodeId: 'node-4b-action-email-voucher', nodeType: JourneyNodeType.ActionNode, executedAtUtc: '2026-11-07T13:15:02Z', outcome: 'EmailVoucherDispatched', details: 'Dispatched Standard Email Voucher' },
        { stepId: '6', nodeId: 'node-5-action-conversion-checkpoint', nodeType: JourneyNodeType.ActionNode, executedAtUtc: '2026-11-07T13:15:03Z', outcome: 'ConversionCheckpointReached', details: 'Conversion completed' }
      ],
      completedAtUtc: '2026-11-07T13:15:03Z'
    };

    component.currentSimulationProgress.set(mockTrigger);
    component.simCartAmount.set(150);
    mockApi.advanceJourneyRuntime.mockReturnValue(of(mockAdvanced));

    component.advanceSimulation();

    expect(component.simulationBanner()).toContain('Standard Email Recovery Voucher');
  });

  it('should map node icons and status badge styles properly', () => {
    expect(component.getNodeIcon(JourneyNodeType.TriggerNode)).toBe('⚡');
    expect(component.getNodeIcon(JourneyNodeType.DelayNode)).toBe('⏳');
    expect(component.getNodeIcon(JourneyNodeType.ConditionNode)).toBe('🔀');
    expect(component.getNodeIcon(JourneyNodeType.ActionNode)).toBe('🚀');

    expect(component.getStatusBadgeClass(JourneyExecutionStatus.Active)).toContain('text-cyan-300');
    expect(component.getStatusBadgeClass(JourneyExecutionStatus.PausedWaitingDelay)).toContain('text-amber-300');
    expect(component.getStatusBadgeClass(JourneyExecutionStatus.Completed)).toContain('text-emerald-300');
  });

  it('should filter contacts at specific pipeline nodes', () => {
    const atWhatsApp = component.getContactsAtNode('node-4a-action-whatsapp-promo');
    expect(atWhatsApp.length).toBe(1);
    expect(atWhatsApp[0].contactEmail).toBe('sultan.aldosari@growth.sa');

    const atDelay = component.getContactsAtNode('node-2-delay-15m');
    expect(atDelay.length).toBe(1);
    expect(atDelay[0].contactEmail).toBe('fatima.zahrani@growth.sa');
  });
});
