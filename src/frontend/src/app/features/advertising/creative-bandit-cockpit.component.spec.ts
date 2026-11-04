// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CreativeBanditCockpitComponent } from './creative-bandit-cockpit.component';

describe('CreativeBanditCockpitComponent', () => {
  it('initializes with active creative variants and metrics', () => {
    const comp = new CreativeBanditCockpitComponent();
    expect(comp.variants().length).toBe(3);
    expect(comp.totalConversions()).toBeGreaterThan(500);
    expect(comp.fatiguedCount()).toBe(1);
    expect(comp.topAllocatedShare()).toBe(45.0);
  });

  it('synthesizes mutant creative and adds to active variants', () => {
    const comp = new CreativeBanditCockpitComponent();
    comp.mutateWinningCreative();
    expect(comp.variants().length).toBe(4);
    expect(comp.variants()[0].headline).toContain('[AI-Evolved]');
    expect(comp.variants()[0].isFatigued).toBe(false);
  });

  it('computes blended conversion rate accurately and handles empty state', () => {
    const comp = new CreativeBanditCockpitComponent();
    expect(comp.blendedCvr()).toBe('2.38');

    comp.mutateWinningCreative();
    expect(comp.blendedCvr()).toBe('2.42');

    comp.variants.set([]);
    expect(comp.totalConversions()).toBe(0);
    expect(comp.fatiguedCount()).toBe(0);
    expect(comp.topAllocatedShare()).toBe(0);
    expect(comp.blendedCvr()).toBe('0.00');
  });
});
