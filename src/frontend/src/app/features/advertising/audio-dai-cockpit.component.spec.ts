// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { AudioDaiCockpitComponent } from './audio-dai-cockpit.component';

describe('AudioDaiCockpitComponent', () => {
  it('should initialize with audio DAI metrics and slots', () => {
    const comp = new AudioDaiCockpitComponent();
    expect(comp.completionRate()).toBe(96.8);
    expect(comp.roas()).toBe(9.55);
    expect(comp.slots().length).toBe(2);
    expect(comp.attributions().length).toBe(2);
  });

  it('should insert dynamic audio ad slot and append to list', () => {
    const comp = new AudioDaiCockpitComponent();
    const initialCount = comp.slots().length;
    comp.insertNewSlot();

    expect(comp.slots().length).toBe(initialCount + 1);
    expect(comp.slots()[initialCount].status).toBe('INSERTED');
    expect(comp.slots()[initialCount].acousticWatermarkHash).toBe('AWT-66A1F9B310');
  });
});
