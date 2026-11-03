// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { AgencySnapshotCockpitComponent } from './agency-snapshot-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('AgencySnapshotCockpitComponent', () => {
  let component: AgencySnapshotCockpitComponent;

  beforeEach(() => {
    const injector = createEnvironmentInjector([TranslationService]);

    injector.runInContext(() => {
      component = new AgencySnapshotCockpitComponent();
    });
  });

  it('should initialize with turn-key blueprint snapshots and resale multipliers', () => {
    expect(component).toBeTruthy();
    expect(component.snapshots().length).toBe(3);
    expect(component.smsMultiplier()).toBe(1.4);
    expect(component.voiceMultiplier()).toBe(1.25);
    expect(component.aiMultiplier()).toBe(1.5);
    expect(component.waterfallSteps().length).toBeGreaterThan(0);
  });

  it('should deploy snapshot into client account with confirmation toast', () => {
    const snap = component.snapshots()[0];
    component.deploySnapshot(snap);

    expect(component.toastMessage()).toContain(`Successfully deployed '${snap.name}'`);
  });

  it('should calculate overall resale margin and net profit dynamically', () => {
    const margin = component.calculateOverallMargin();
    expect(margin).toBeGreaterThan(0);

    const netProfit = component.calculateNetProfit();
    expect(parseFloat(netProfit)).toBeGreaterThan(0);
  });

  it('should run multi-provider waterfall enrichment cascade simulation', () => {
    component.prospectQueryInput = 'Kais Saidi (biat.com.tn)';
    component.simulateWaterfallEnrichment();

    expect(component.waterfallSteps().some(s => s.status === 'Matched')).toBe(true);
    expect(component.enrichedOutput()).toBeTruthy();
    expect(component.enrichedOutput()?.confidence).toBe(96);
    expect(component.toastMessage()).toContain('Waterfall cascade completed');
  });
});
