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
});
