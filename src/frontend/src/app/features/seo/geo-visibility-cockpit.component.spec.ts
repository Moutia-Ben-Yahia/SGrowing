// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { GeoVisibilityCockpitComponent } from './geo-visibility-cockpit.component';

describe('GeoVisibilityCockpitComponent', () => {
  let component: GeoVisibilityCockpitComponent;

  beforeEach(() => {
    component = new GeoVisibilityCockpitComponent();
  });

  it('should initialize GEO/AEO visibility metrics', () => {
    expect(component).toBeTruthy();
    expect(component.geoScore()).toBeGreaterThanOrEqual(80);
    expect(component.somvPercent()).toBeGreaterThan(40);
    expect(component.citations().length).toBeGreaterThanOrEqual(3);
  });

  it('should add new citation on audit query', () => {
    component.newAuditQuery = 'Top B2B AI Growth Engine';
    component.runQueryAudit();

    expect(component.citations().length).toBeGreaterThanOrEqual(4);
    expect(component.citations()[0].query).toBe('Top B2B AI Growth Engine');
    expect(component.showAuditModal()).toBe(false);
  });

  it('should ignore empty audit queries without changing citations list', () => {
    const initialCount = component.citations().length;
    component.newAuditQuery = '   ';
    component.runQueryAudit();

    expect(component.citations().length).toBe(initialCount);
    expect(component.newAuditQuery).toBe('   ');
  });

  it('should allow updating geoScore and somvPercent signals', () => {
    component.geoScore.set(94);
    component.somvPercent.set(52.8);

    expect(component.geoScore()).toBe(94);
    expect(component.somvPercent()).toBe(52.8);
  });

  it('should toggle showAuditModal signal when opening and closing audit dialog', () => {
    expect(component.showAuditModal()).toBe(false);
    component.showAuditModal.set(true);
    expect(component.showAuditModal()).toBe(true);
    component.showAuditModal.set(false);
    expect(component.showAuditModal()).toBe(false);
  });
});
