// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { DataCleanRoomCockpitComponent } from './data-clean-room-cockpit.component';

describe('DataCleanRoomCockpitComponent', () => {
  let component: DataCleanRoomCockpitComponent;

  beforeEach(() => {
    component = new DataCleanRoomCockpitComponent();
  });

  it('should create component and render initial participants and datasets', () => {
    expect(component).toBeTruthy();
    expect(component.participants().length).toBeGreaterThanOrEqual(2);
    expect(component.datasets().length).toBeGreaterThanOrEqual(2);
    expect(component.totalIdentities()).toBeGreaterThan(0);
  });

  it('should execute zero-knowledge overlap query with differential privacy noise', () => {
    component.runZeroKnowledgeOverlapQuery();
    const result = component.queryResult();

    expect(result).toBeTruthy();
    expect(result?.rawOverlapMatchCount).toBeGreaterThan(0);
    expect(result?.privacyNoisedOverlapCount).toBeGreaterThan(0);
    expect(result?.matchRatePercent).toBeGreaterThan(20);
    expect(result?.epsilonCost).toBe(0.50);
  });
});
