// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { VideoDcoCockpitComponent } from './video-dco-cockpit.component';

describe('VideoDcoCockpitComponent', () => {
  it('should initialize with templates and rendered variants', () => {
    const comp = new VideoDcoCockpitComponent();
    expect(comp.templates().length).toBe(3);
    expect(comp.variants().length).toBe(2);
    expect(comp.cdnHitRatio()).toBeGreaterThan(90);
  });

  it('should dispatch render and add a new variant', () => {
    const comp = new VideoDcoCockpitComponent();
    comp.renderNewVariant();
    expect(comp.variants().length).toBe(3);
    expect(comp.variants()[0].localizedCurrency).toBe('SGD');
  });

  it('should verify composition templates and multi-aspect ratio specs', () => {
    const comp = new VideoDcoCockpitComponent();
    const aspectRatios = comp.templates().map(t => t.aspectRatio);
    expect(aspectRatios).toContain('16:9');
    expect(aspectRatios).toContain('9:16');
    expect(aspectRatios).toContain('1:1');

    const landscapeTpl = comp.templates().find(t => t.aspectRatio === '16:9');
    expect(landscapeTpl?.dynamicLayers).toContain('DynamicPriceBadge');
    expect(landscapeTpl?.durationSeconds).toBe(30);

    const warmedVariants = comp.variants().filter(v => v.cdnStatus === 'WARMED_EDGE');
    expect(warmedVariants.length).toBe(2);
  });

  it('should verify initial rendered variants target audiences and currencies', () => {
    const comp = new VideoDcoCockpitComponent();
    const euVariant = comp.variants().find(v => v.variantId === 'VAR-EU-DE-01');
    const usVariant = comp.variants().find(v => v.variantId === 'VAR-US-NY-02');

    expect(euVariant).toBeDefined();
    expect(euVariant?.localizedCurrency).toBe('EUR');
    expect(euVariant?.displayedPrice).toBe(4990);

    expect(usVariant).toBeDefined();
    expect(usVariant?.localizedCurrency).toBe('USD');
    expect(usVariant?.displayedPrice).toBe(299);
  });

  it('should allow modifying cdnHitRatio and clearing variants signal', () => {
    const comp = new VideoDcoCockpitComponent();
    comp.cdnHitRatio.set(99.4);
    comp.variants.set([]);
    expect(comp.cdnHitRatio()).toBe(99.4);
    expect(comp.variants().length).toBe(0);
  });
});
