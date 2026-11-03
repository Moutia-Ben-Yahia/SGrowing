// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { LocalizationCockpitComponent } from './localization-cockpit.component';

describe('LocalizationCockpitComponent', () => {
  it('initializes with German formal executive translation and preserved brand terms', () => {
    const comp = new LocalizationCockpitComponent();
    expect(comp.currentMessage().targetLang).toBe('de');
    expect(comp.currentMessage().politenessScore).toBeGreaterThan(95);
    expect(comp.currentMessage().preservedTerms).toContain('SGrowing');
    expect(comp.activeLatency()).toBeLessThan(200);
  });

  it('translates to Japanese Keigo and updates score and output', () => {
    const comp = new LocalizationCockpitComponent();
    comp.translateToJapaneseKeigo();
    expect(comp.currentMessage().targetLang).toBe('ja');
    expect(comp.currentMessage().localizedText).toContain('誠にありがとうございます');
    expect(comp.currentMessage().politenessScore).toBeGreaterThan(98);
  });
});
