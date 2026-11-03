// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ContentRepurposingCockpitComponent } from './content-repurposing-cockpit.component';

describe('ContentRepurposingCockpitComponent', () => {
  it('initializes with multichannel derivatives and slide carousel preview', () => {
    const comp = new ContentRepurposingCockpitComponent();
    expect(comp.derivatives().length).toBe(3);
    expect(comp.slides().length).toBe(5);
    expect(comp.slides()[0].header).toBe('The Outbound Dilemma');
  });

  it('synthesizes new short video script and appends to derivatives', () => {
    const comp = new ContentRepurposingCockpitComponent();
    comp.generateShortVideoScript();
    expect(comp.derivatives().length).toBe(4);
    expect(comp.derivatives().some(d => d.title.includes('TikTok / Shorts'))).toBe(true);
  });
});
