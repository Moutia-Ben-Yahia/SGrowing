// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { AudioNoiseCancellationCockpitComponent } from './audio-noise-cancellation-cockpit.component';

describe('AudioNoiseCancellationCockpitComponent', () => {
  it('initializes with audio channels and computes average MOS score', () => {
    const comp = new AudioNoiseCancellationCockpitComponent();
    expect(comp.channels().length).toBe(3);
    expect(comp.avgMos()).toBeGreaterThan(4.0);
  });

  it('toggles noise cancellation filter on channel and improves MOS', () => {
    const comp = new AudioNoiseCancellationCockpitComponent();
    comp.toggle('VOX-DSP-02');

    const updated = comp.channels().find(c => c.channelId === 'VOX-DSP-02');
    expect(updated?.isFilterActive).toBe(true);
    expect(updated?.meanOpinionScoreMos).toBe(4.7);
    expect(comp.lastDspNotice()).toContain('Engaged (-24dB spectral suppression)');
  });
});
