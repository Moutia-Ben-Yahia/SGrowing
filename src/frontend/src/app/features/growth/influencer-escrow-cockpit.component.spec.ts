// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { InfluencerEscrowCockpitComponent } from './influencer-escrow-cockpit.component';

describe('InfluencerEscrowCockpitComponent', () => {
  let component: InfluencerEscrowCockpitComponent;

  beforeEach(() => {
    component = new InfluencerEscrowCockpitComponent();
  });

  it('should create component and render initial escrow contracts', () => {
    expect(component).toBeTruthy();
    expect(component.contracts().length).toBeGreaterThan(0);
    expect(component.totalEscrowCapital()).toBeGreaterThan(0);
  });

  it('should fund a new creator escrow contract', () => {
    const initialCount = component.contracts().length;
    component.simulateNewContract();

    expect(component.contracts().length).toBe(initialCount + 1);
    expect(component.selectedContract()?.status).toBe('Funded');
    expect(component.selectedContract()?.remainingAmount).toBe(7500);
  });

  it('should verify and release milestone payout to creator', () => {
    const contract = component.contracts()[0];
    const milestone = contract.milestones[1]; // unreleased milestone

    expect(milestone.isReleased).toBe(false);
    component.releaseMilestone(contract, milestone);

    const updated = component.contracts().find(c => c.contractId === contract.contractId);
    expect(updated?.releasedAmount).toBe(5000);
    expect(updated?.remainingAmount).toBe(0);
    expect(updated?.status).toBe('Completed');
  });
});
