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

  it('should verify participant cryptographic salts, cohort schemes, and total identities sum', () => {
    expect(component.totalIdentities()).toBe(404000); // 84000 + 320000

    const advertiser = component.participants().find(p => p.role === 'Advertiser');
    const publisher = component.participants().find(p => p.role === 'Publisher');

    expect(advertiser).toBeDefined();
    expect(advertiser?.cryptographicSaltId).toBe('salt_acme_sha256_v4');

    expect(publisher).toBeDefined();
    expect(publisher?.cryptographicSaltId).toBe('salt_pub_sha256_v4');

    component.datasets.set([]);
    expect(component.totalIdentities()).toBe(0);
  });

  it('should initialize with default query result containing positive attributed revenue and ROAS', () => {
    const result = component.queryResult();
    expect(result).not.toBeNull();
    expect(result?.attributedRevenue).toBe(922768);
    expect(result?.privacyPreservingRoas).toBe(6.15);
    expect(result?.queryId).toBe('query_zk_784a9c');
  });

  it('should allow dynamically adding participants and updating status', () => {
    component.participants.update(list => [
      ...list,
      {
        participantId: 'part_retail_partner',
        organizationName: 'OmniRetail Alliance',
        role: 'DataPartner',
        cryptographicSaltId: 'salt_retail_sha256',
        status: 'Active'
      }
    ]);
    expect(component.participants().length).toBe(3);
    expect(component.participants()[2].participantId).toBe('part_retail_partner');
  });
});
