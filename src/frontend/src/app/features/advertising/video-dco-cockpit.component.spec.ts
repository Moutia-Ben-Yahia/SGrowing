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
});
