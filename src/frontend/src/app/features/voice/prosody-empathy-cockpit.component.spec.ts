import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ProsodyEmpathyCockpitComponent } from './prosody-empathy-cockpit.component';

describe('ProsodyEmpathyCockpitComponent', () => {
  it('should initialize with active tone shifts and empathy profiles', () => {
    const comp = new ProsodyEmpathyCockpitComponent();
    expect(comp.activeCallsCount()).toBe(28);
    expect(comp.deescalationRatePct()).toBe(88.4);
    expect(comp.events().length).toBeGreaterThan(0);
    expect(comp.profiles().length).toBeGreaterThan(0);
  });

  it('should trigger an empathetic tone shift event', () => {
    const comp = new ProsodyEmpathyCockpitComponent();
    const initialCount = comp.events().length;
    comp.triggerToneShift();
    expect(comp.events().length).toBe(initialCount + 1);
    expect(comp.events()[comp.events().length - 1].detectedEmotion).toBe('AnxiousDistressed');
  });
});
