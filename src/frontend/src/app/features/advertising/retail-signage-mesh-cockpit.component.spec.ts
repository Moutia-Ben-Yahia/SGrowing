// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { RetailSignageMeshCockpitComponent } from './retail-signage-mesh-cockpit.component';

describe('RetailSignageMeshCockpitComponent', () => {
  it('should initialize with screens and shelves', () => {
    const comp = new RetailSignageMeshCockpitComponent();
    expect(comp.screens().length).toBe(2);
    expect(comp.shelves().length).toBe(2);
    expect(comp.screenAdFillRatePct()).toBeGreaterThan(90);
  });

  it('should restock shelf and restore active ad state', () => {
    const comp = new RetailSignageMeshCockpitComponent();
    comp.restockShelf();
    const shelf = comp.shelves().find(s => s.shelfId === 'SHLF-SNK-02');
    expect(shelf?.currentUnitsInStock).toBe(25);
    expect(shelf?.isAdActive).toBe(true);
  });
});
