import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { VehicleAudioDspCockpitComponent } from './vehicle-audio-dsp-cockpit.component';

describe('VehicleAudioDspCockpitComponent', () => {
  it('should initialize with connected vehicles and audio bids', () => {
    const comp = new VehicleAudioDspCockpitComponent();
    expect(comp.activeVehiclesCount()).toBe(4210);
    expect(comp.commuteLiftPct()).toBe(38.2);
    expect(comp.bids().length).toBeGreaterThan(0);
    expect(comp.playouts().length).toBeGreaterThan(0);
  });

  it('should place a new in-cabin vehicle audio bid', () => {
    const comp = new VehicleAudioDspCockpitComponent();
    const initialCount = comp.bids().length;
    comp.bidNewVehicleSlot();
    expect(comp.bids().length).toBe(initialCount + 1);
    expect(comp.bids()[comp.bids().length - 1].vehicleMakeModel).toBe('Porsche Taycan (PCM 6.0)');
  });
});
