// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { DirectMailMeshCockpitComponent } from './direct-mail-mesh-cockpit.component';

describe('DirectMailMeshCockpitComponent', () => {
  it('should initialize with campaigns and dispatches', () => {
    const comp = new DirectMailMeshCockpitComponent();
    expect(comp.campaigns().length).toBe(2);
    expect(comp.dispatches().length).toBe(2);
    expect(comp.deliverySuccessRatePct()).toBeGreaterThan(90);
    expect(comp.pipelineInfluenceRatePct()).toBeGreaterThan(30);
  });

  it('should dispatch gift box and prepend to dispatches ledger', () => {
    const comp = new DirectMailMeshCockpitComponent();
    comp.dispatchGift();
    expect(comp.dispatches().length).toBe(3);
    expect(comp.dispatches()[0].recipientExecutiveName).toContain('Elena Rostova');
    expect(comp.dispatches()[0].deliveryStatus).toBe('LABEL_CREATED_SCHEDULED_PICKUP');
  });

  it('should verify campaign budget tiers and carrier allocation', () => {
    const comp = new DirectMailMeshCockpitComponent();
    const enterpriseCamp = comp.campaigns().find(c => c.targetTier === 'ENTERPRISE_TIER_1');
    expect(enterpriseCamp).toBeDefined();
    expect(enterpriseCamp?.unitCostUsd).toBe(145.00);

    const hasFedEx = comp.dispatches().some(d => d.carrier.includes('FedEx'));
    expect(hasFedEx).toBe(true);
  });

  it('should allow updating deliverySuccessRatePct and pipelineInfluenceRatePct signals', () => {
    const comp = new DirectMailMeshCockpitComponent();
    comp.deliverySuccessRatePct.set(99.4);
    comp.pipelineInfluenceRatePct.set(48.2);

    expect(comp.deliverySuccessRatePct()).toBe(99.4);
    expect(comp.pipelineInfluenceRatePct()).toBe(48.2);
  });
});
