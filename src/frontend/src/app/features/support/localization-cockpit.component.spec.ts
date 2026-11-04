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

  it('should maintain glossary term preservation across Japanese translation', () => {
    const comp = new LocalizationCockpitComponent();
    comp.translateToJapaneseKeigo();
    expect(comp.currentMessage().preservedTerms).toEqual(['SGrowing', 'SOC2 Type II', 'CAPI']);
    expect(comp.activeLatency()).toBe(168);
  });

  it('should allow updating activeLatency and currentMessage custom values', () => {
    const comp = new LocalizationCockpitComponent();
    comp.activeLatency.set(95);
    expect(comp.activeLatency()).toBe(95);

    comp.currentMessage.update(m => ({ ...m, formality: 'Casual', politenessScore: 80.0 }));
    expect(comp.currentMessage().formality).toBe('Casual');
    expect(comp.currentMessage().politenessScore).toBe(80.0);
  });
});
