// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { RetailCoopDspCockpitComponent } from './retail-coop-dsp-cockpit.component';

describe('RetailCoopDspCockpitComponent', () => {
  it('should initialize with clean rooms and co-op campaigns', () => {
    const comp = new RetailCoopDspCockpitComponent();
    expect(comp.cleanRooms().length).toBe(2);
    expect(comp.campaigns().length).toBe(2);
    expect(comp.blendedRoas()).toBeGreaterThan(3.0);
  });

  it('should launch off-site co-op flight and prepend to list', () => {
    const comp = new RetailCoopDspCockpitComponent();
    comp.launchCoopCampaign();
    expect(comp.campaigns().length).toBe(3);
    expect(comp.campaigns()[0].brandName).toBe('Quantum Power Snacks');
    expect(comp.campaigns()[0].totalCoopBudgetUsd).toBe(90000);
  });
});
