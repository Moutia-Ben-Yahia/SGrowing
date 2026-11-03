import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CrossDeviceSyncCockpitComponent } from './cross-device-sync-cockpit.component';

describe('CrossDeviceSyncCockpitComponent', () => {
  it('should initialize with active identity clusters and nodes', () => {
    const comp = new CrossDeviceSyncCockpitComponent();
    expect(comp.clusters().length).toBeGreaterThan(0);
    expect(comp.selectedCluster()).not.toBeNull();
    expect(comp.selectedCluster()?.nodes.length).toBeGreaterThan(0);
  });

  it('should match offline conversion and set last match event', () => {
    const comp = new CrossDeviceSyncCockpitComponent();
    comp.posAmount = 500.00;
    comp.matchOfflineConversion();

    expect(comp.lastMatch()).not.toBeNull();
    expect(comp.lastMatch()?.amount).toBe(500.00);
    expect(comp.lastMatch()?.forwardedToCapi).toBe(true);
  });
});
