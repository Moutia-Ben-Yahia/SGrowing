// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { VoiceDialectCockpitComponent } from './voice-dialect-cockpit.component';

describe('VoiceDialectCockpitComponent', () => {
  it('should initialize with dialect profiles and adaptations', () => {
    const comp = new VoiceDialectCockpitComponent();
    expect(comp.profiles().length).toBe(2);
    expect(comp.adaptations().length).toBe(1);
    expect(comp.adaptations()[0].dialectCode).toBe('en-US-TEXAS');
  });

  it('should adapt cultural phrase and prepend to list', () => {
    const comp = new VoiceDialectCockpitComponent();
    comp.adaptTestPhrase();
    expect(comp.adaptations().length).toBe(2);
    expect(comp.adaptations()[0].dialectCode).toBe('en-GB-SCOTTISH');
    expect(comp.adaptations()[0].culturallyAdaptedPhrase).toContain('sorted');
  });
});
