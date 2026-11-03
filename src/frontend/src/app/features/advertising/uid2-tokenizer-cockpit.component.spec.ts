// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { Uid2TokenizerCockpitComponent } from './uid2-tokenizer-cockpit.component';

describe('Uid2TokenizerCockpitComponent', () => {
  it('should initialize with correct default metrics and identities', () => {
    const comp = new Uid2TokenizerCockpitComponent();
    expect(comp.totalIdentities()).toBe(3);
    expect(comp.activeTokens()).toBe(3);
    expect(comp.tokenComplianceRate()).toBe(99.8);
    expect(comp.dailyTouchpoints()).toBe(8450);
    expect(comp.identities().length).toBe(3);
  });

  it('should increment active tokens on generation', () => {
    const comp = new Uid2TokenizerCockpitComponent();
    comp.generateAdvertisingToken();
    expect(comp.activeTokens()).toBe(4);
  });

  it('should update daily touchpoints and stitched count on session stitch', () => {
    const comp = new Uid2TokenizerCockpitComponent();
    comp.stitchAnonymousSession();
    expect(comp.dailyTouchpoints()).toBe(8950);
    const id = comp.identities().find(i => i.identityGraphId === 'GRAPH-ID-01');
    expect(id?.totalStitchedSessionsCount).toBe(15);
  });
});
