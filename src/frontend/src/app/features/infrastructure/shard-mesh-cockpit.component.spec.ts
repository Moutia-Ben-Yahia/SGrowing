// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { ShardMeshCockpitComponent } from './shard-mesh-cockpit.component';

describe('ShardMeshCockpitComponent', () => {
  it('initializes with 3 active-active shards and detects imbalanced storage', () => {
    const comp = new ShardMeshCockpitComponent();
    expect(comp.shards().length).toBe(3);
    expect(comp.totalQps()).toBe(9100);
    // 84.5 - 48.0 = 36.5% variance (> 20%), so isMeshBalanced is false
    expect(comp.maxStorageVariance()).toBe(36.5);
    expect(comp.isMeshBalanced()).toBe(false);
  });

  it('routes tenant deterministically to bucket and shard', () => {
    const comp = new ShardMeshCockpitComponent();
    comp.testTenantId = 'tenant_nordic_fintech_secure';
    comp.routeTestTenant();

    const res = comp.routingResult();
    expect(res).not.toBeNull();
    expect(res?.tenantId).toBe('tenant_nordic_fintech_secure');
    expect(res?.virtualHashBucket).toBeGreaterThanOrEqual(0);
    expect(res?.virtualHashBucket).toBeLessThan(1024);
    expect(['shard_us_east', 'shard_eu_west', 'shard_apac_south']).toContain(res?.assignedShardId);
  });

  it('rebalances shard mesh to bring variance under 20% limit', () => {
    const comp = new ShardMeshCockpitComponent();
    expect(comp.isMeshBalanced()).toBe(false);

    comp.rebalanceMesh();
    // After rebalancing: 65.5 - 55.5 = 10% variance (< 20%)
    expect(comp.maxStorageVariance()).toBe(10);
    expect(comp.isMeshBalanced()).toBe(true);
    expect(comp.shards().find(s => s.shardId === 'shard_us_east')?.storageUtilizationPercentage).toBe(65.5);
  });
});
