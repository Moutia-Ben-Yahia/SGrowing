// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { WaterfallEnrichmentCockpitComponent } from './waterfall-enrichment-cockpit.component';

describe('WaterfallEnrichmentCockpitComponent (Horizon 61)', () => {
  let component: WaterfallEnrichmentCockpitComponent;

  beforeEach(() => {
    localStorage.clear();
    component = new WaterfallEnrichmentCockpitComponent();
  });

  it('should initialize with waterfall provider metrics and enriched leads', () => {
    expect(component).toBeTruthy();
    expect(component.providers().length).toBeGreaterThanOrEqual(4);
    expect(component.overallMatchRate()).toBe(94);
    expect(component.leads().length).toBeGreaterThanOrEqual(2);
  });

  it('should trigger waterfall enrichment and add new verified lead', () => {
    const initialCount = component.leads().length;
    component.triggerWaterfallEnrichment();
    expect(component.leads().length).toBe(initialCount + 1);
    expect(component.leads()[0].domain).toBe('databricks.com');
  });

  it('should re-verify deliverability for an existing lead', () => {
    const lead = component.leads()[0];
    component.verifyMx(lead);
    expect(component.leads()[0].deliverability).toBe('DeliverableWithMx');
  });

  it('should return 0 avgLatency and 0 verifiedCount when arrays are empty', () => {
    expect(component.avgLatency()).toBeGreaterThan(0);
    expect(component.verifiedCount()).toBe(2);
    component.providers.set([]);
    component.leads.set([]);
    expect(component.avgLatency()).toBe(0);
    expect(component.verifiedCount()).toBe(0);
  });

  it('should list all providers and compute avgLatency across provider latency entries', () => {
    const providerNames = component.providers().map(p => p.name);
    expect(providerNames).toContain('Clearbit Enterprise API');
    expect(providerNames).toContain('Apollo.io B2B Graph');
    expect(providerNames).toContain('ZoomInfo Enterprise Data');
    expect(providerNames).toContain('People Data Labs (PDL)');

    const expectedAvg = Math.round(component.providers().reduce((acc, p) => acc + p.latencyMs, 0) / component.providers().length);
    expect(component.avgLatency()).toBe(expectedAvg);
  });
});
