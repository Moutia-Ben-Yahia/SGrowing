import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { NeuralPlcCockpitComponent } from './neural-plc-cockpit.component';

describe('NeuralPlcCockpitComponent', () => {
  it('should initialize with active audio streams and reconstruction metrics', () => {
    const comp = new NeuralPlcCockpitComponent();
    expect(comp.activeStreamsCount()).toBe(68);
    expect(comp.lossConcealedPct()).toBe(98.6);
    expect(comp.recons().length).toBeGreaterThan(0);
    expect(comp.buffers().length).toBeGreaterThan(0);
  });

  it('should synthesize a lost audio frame', () => {
    const comp = new NeuralPlcCockpitComponent();
    const initialCount = comp.recons().length;
    comp.synthesizeLostFrame();
    expect(comp.recons().length).toBe(initialCount + 1);
    expect(comp.recons()[comp.recons().length - 1].callSessionId).toBe('CALL-SIP-9044');
  });
});
