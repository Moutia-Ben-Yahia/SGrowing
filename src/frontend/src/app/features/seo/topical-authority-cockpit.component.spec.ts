// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { TopicalAuthorityCockpitComponent } from './topical-authority-cockpit.component';

describe('TopicalAuthorityCockpitComponent (Horizon 59)', () => {
  let component: TopicalAuthorityCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    component = new TopicalAuthorityCockpitComponent();
  });

  it('should initialize with topic clusters and authority metrics', () => {
    expect(component).toBeTruthy();
    expect(component.clusters().length).toBeGreaterThanOrEqual(2);
    expect(component.overallAuthority()).toBeGreaterThan(70);
    expect(component.linkOpportunities().length).toBeGreaterThanOrEqual(2);
  });

  it('should audit and add new topical cluster', () => {
    const initialCount = component.clusters().length;
    component.auditTopicalClusters();
    expect(component.clusters().length).toBe(initialCount + 1);
  });

  it('should inject internal link anchor tag', () => {
    const opp = component.linkOpportunities()[0];
    expect(opp.isInjected).toBe(false);

    component.injectInternalLink(opp);
    expect(component.linkOpportunities()[0].isInjected).toBe(true);
  });
});
