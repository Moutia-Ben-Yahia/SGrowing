// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CallTransferCockpitComponent } from './call-transfer-cockpit.component';

describe('CallTransferCockpitComponent', () => {
  it('should initialize with active calls and high initial deflection rate', () => {
    const comp = new CallTransferCockpitComponent();
    expect(comp.calls().length).toBe(3);
    expect(comp.deflectionRate()).toBe(100);
    expect(comp.warmTransfersCount()).toBe(0);
  });

  it('should select a call', () => {
    const comp = new CallTransferCockpitComponent();
    const second = comp.calls()[1];
    comp.selectCall(second);
    expect(comp.selectedCall()?.customerName).toContain('Elena');
  });

  it('should initiate warm transfer and update bridge status', () => {
    const comp = new CallTransferCockpitComponent();
    comp.initiateWarmTransfer('call_voice_01', 'Lead Support Specialist');
    expect(comp.warmTransfersCount()).toBe(1);

    const call = comp.calls().find(c => c.callId === 'call_voice_01');
    expect(call?.state).toBe('ThreeWayBridge');
    expect(call?.targetSpecialist).toBe('Lead Support Specialist');
  });

  it('should complete transfer and boost sentiment score', () => {
    const comp = new CallTransferCockpitComponent();
    comp.initiateWarmTransfer('call_voice_01', 'Lead Support Specialist');
    comp.completeTransfer('call_voice_01');

    const call = comp.calls().find(c => c.callId === 'call_voice_01');
    expect(call?.state).toBe('TransferCompleted');
    expect(call?.callerSentimentScore).toBeGreaterThan(0.7);
  });

  it('should recalculate deflectionRate when a call is transferred or when calls is empty', () => {
    const comp = new CallTransferCockpitComponent();
    expect(comp.deflectionRate()).toBe(100);

    comp.initiateWarmTransfer('call_voice_01', 'Support Agent');
    expect(comp.deflectionRate()).toBeCloseTo(66.67, 1);

    comp.calls.set([]);
    expect(comp.deflectionRate()).toBe(100);
  });
});
