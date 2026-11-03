import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { DereverberationCockpitComponent } from './dereverberation-cockpit.component';

describe('DereverberationCockpitComponent', () => {
  it('should initialize with active audio streams and room profiles', () => {
    const comp = new DereverberationCockpitComponent();
    expect(comp.activeStreamsCount()).toBe(112);
    expect(comp.snrGainDb()).toBe(12.4);
    expect(comp.profiles().length).toBeGreaterThan(0);
    expect(comp.filters().length).toBeGreaterThan(0);
  });

  it('should calibrate and add a new acoustic room profile', () => {
    const comp = new DereverberationCockpitComponent();
    const initialCount = comp.profiles().length;
    comp.profileNewRoom();
    expect(comp.profiles().length).toBe(initialCount + 1);
    expect(comp.profiles()[comp.profiles().length - 1].roomType).toBe('SmallTiledRoomOrKitchen');
  });
});
