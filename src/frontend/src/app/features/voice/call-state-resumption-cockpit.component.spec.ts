// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CallStateResumptionCockpitComponent } from './call-state-resumption-cockpit.component';

describe('CallStateResumptionCockpitComponent', () => {
  it('should initialize with snapshots and resumption events', () => {
    const comp = new CallStateResumptionCockpitComponent();
    expect(comp.snapshots().length).toBe(2);
    expect(comp.resumptions().length).toBe(1);
    expect(comp.autoCallbackSuccessRatePct()).toBeGreaterThan(90);
  });

  it('should simulate auto callback and prepend event to list', () => {
    const comp = new CallStateResumptionCockpitComponent();
    comp.simulateAutoCallback();
    expect(comp.resumptions().length).toBe(2);
    expect(comp.resumptions()[0].disconnectReason).toContain('SIP_BYE_UNEXPECTED');
    expect(comp.resumptions()[0].generatedAcousticGreeting).toContain('Helena');
  });

  it('should enforce sub-5s reconnect latency and retain customer dialog context', () => {
    const comp = new CallStateResumptionCockpitComponent();
    expect(comp.resumptions().every(r => r.reconnectLatencySeconds < 5.0)).toBe(true);

    const firstSnapshot = comp.snapshots()[0];
    expect(firstSnapshot.turnCounter).toBe(14);
    expect(firstSnapshot.currentContractPhase).toBe('COMMERCIAL_PROPOSAL_REVIEW');
    expect(firstSnapshot.dialogGoalTopic).toBe('Security Architecture');
  });

  it('should allow updating autoCallbackSuccessRatePct and adding custom snapshot', () => {
    const comp = new CallStateResumptionCockpitComponent();
    comp.autoCallbackSuccessRatePct.set(99.4);
    expect(comp.autoCallbackSuccessRatePct()).toBe(99.4);

    comp.snapshots.update(s => [...s, {
      sessionId: 'CALL-SES-999',
      callerPhoneNumber: '+1-555-0100',
      calleePhoneNumber: '+1-555-0200',
      customerName: 'Alice Morgan',
      lastAcousticUtterance: 'Looking forward to the rollout.',
      dialogGoalTopic: 'Deployment Plan',
      currentContractPhase: 'FINAL_SIGN_OFF',
      turnCounter: 5
    }]);
    expect(comp.snapshots().length).toBe(3);
  });
});
