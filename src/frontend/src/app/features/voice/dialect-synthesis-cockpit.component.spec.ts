import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { DialectSynthesisCockpitComponent } from './dialect-synthesis-cockpit.component';

describe('DialectSynthesisCockpitComponent', () => {
  it('should initialize with dialect profiles and idioms', () => {
    const comp = new DialectSynthesisCockpitComponent();
    expect(comp.profiles().length).toBeGreaterThan(0);
    expect(comp.selectedProfile()).not.toBeNull();
    expect(comp.selectedProfile()?.idioms.length).toBeGreaterThan(0);
  });

  it('should synthesize dialect-adapted utterance with sub-45ms latency', () => {
    const comp = new DialectSynthesisCockpitComponent();
    comp.selectProfile(comp.profiles()[1]); // Scottish English
    comp.callerTranscript = 'There is a small delay, but yes we will solve it with no problem.';
    comp.detectAndSynthesize();

    expect(comp.activeUtterance()).not.toBeNull();
    expect(comp.activeUtterance()?.adaptedText).toContain('wee');
    expect(comp.activeUtterance()?.adaptedText).toContain('nae bother');
    expect(comp.activeUtterance()?.latencyMs).toBeLessThanOrEqual(45);
  });

  it('should adapt to Singapore and US Southern vernacular with correct cultural idioms', () => {
    const comp = new DialectSynthesisCockpitComponent();
    const singlish = comp.profiles().find(p => p.code === 'EN-SG-SINGLISH');
    expect(singlish).toBeDefined();

    comp.selectProfile(singlish!);
    comp.detectAndSynthesize();
    expect(comp.activeUtterance()?.adaptedText).toContain('can lah');
    expect(comp.activeUtterance()?.adaptedText).toContain('steady no issue lah');

    const southern = comp.profiles().find(p => p.code === 'EN-US-SOUTH');
    comp.selectProfile(southern!);
    comp.detectAndSynthesize();
    expect(comp.activeUtterance()?.adaptedText).toContain("y'all");
  });

  it('should keep original transcript if selected profile has no dialect override and allow changing selectedProfile', () => {
    const comp = new DialectSynthesisCockpitComponent();
    comp.selectedProfile.set({
      code: 'EN-US-STANDARD',
      name: 'General American',
      idioms: [],
      pitchShift: 0,
      cadenceMultiplier: 1.0
    });
    comp.callerTranscript = 'Standard statement without alterations.';
    comp.detectAndSynthesize();

    expect(comp.activeUtterance()?.adaptedText).toBe('Standard statement without alterations.');
    expect(comp.activeUtterance()?.dialectCode).toBe('EN-US-STANDARD');
  });

  it('should fallback to default dialect code when selectedProfile is null', () => {
    const comp = new DialectSynthesisCockpitComponent();
    comp.selectedProfile.set(null);
    comp.callerTranscript = 'Unassigned profile test.';
    comp.detectAndSynthesize();

    expect(comp.activeUtterance()?.dialectCode).toBe('EN-GB-SCOTTISH');
    expect(comp.activeUtterance()?.adaptedText).toBe('Unassigned profile test.');
  });
});
