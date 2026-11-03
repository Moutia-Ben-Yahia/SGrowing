// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CtvAdInsertionCockpitComponent } from './ctv-ad-insertion-cockpit.component';

describe('CtvAdInsertionCockpitComponent', () => {
  it('should initialize with correct default metrics and pods', () => {
    const comp = new CtvAdInsertionCockpitComponent();
    expect(comp.activePods()).toBe(4);
    expect(comp.averageCpm()).toBe(24.00);
    expect(comp.pods().length).toBe(4);
    expect(comp.attributions().length).toBe(3);
  });

  it('should update impressions and CPM on bid simulation', () => {
    const comp = new CtvAdInsertionCockpitComponent();
    comp.simulateBidClearing();
    expect(comp.averageCpm()).toBe(27.50);
    expect(comp.totalImpressions()).toBeGreaterThan(425000);
  });

  it('should prepend new attribution on mobile scan simulation', () => {
    const comp = new CtvAdInsertionCockpitComponent();
    const initialCount = comp.attributions().length;
    comp.simulateQrScan();
    expect(comp.attributions().length).toBe(initialCount + 1);
  });
});
