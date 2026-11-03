// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SovereignCbamCockpitComponent } from './sovereign-cbam-cockpit.component';

describe('SovereignCbamCockpitComponent', () => {
  it('should initialize with declarations and benchmark factors', () => {
    const comp = new SovereignCbamCockpitComponent();
    expect(comp.declarations().length).toBe(2);
    expect(comp.factors().length).toBe(2);
    expect(comp.declarations()[0].countryOfOrigin).toBe('CN');
  });

  it('should generate CBAM filing and prepend declaration to list', () => {
    const comp = new SovereignCbamCockpitComponent();
    comp.generateCbamFiling();
    expect(comp.declarations().length).toBe(3);
    expect(comp.declarations()[0].countryOfOrigin).toBe('TR');
    expect(comp.declarations()[0].goodsCategory).toBe('FERTILIZERS');
  });
});
