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

  it('validates multichannel reading duration and carousel slide progression', () => {
    const comp = new ContentRepurposingCockpitComponent();
    const totalReadSeconds = comp.derivatives().reduce((acc, d) => acc + d.readSeconds, 0);
    expect(totalReadSeconds).toBe(165); // 45 + 30 + 90

    expect(comp.slides().every((s, idx) => s.slideNum === idx + 1)).toBe(true);
    expect(comp.slides()[4].header).toBe('Takeaway & Action');
  });

  it('should verify individual hashtags and channels across initial derivatives', () => {
    const comp = new ContentRepurposingCockpitComponent();
    const linkedin = comp.derivatives().find(d => d.channel === 'LinkedIn');
    expect(linkedin?.hashtags).toContain('#B2BGrowth');
    const twitter = comp.derivatives().find(d => d.channel === 'XTwitter');
    expect(twitter?.hashtags).toContain('#SalesTech');
    const newsletter = comp.derivatives().find(d => d.channel === 'Newsletter');
    expect(newsletter?.hashtags).toContain('#ExecutiveBriefing');
  });
});
