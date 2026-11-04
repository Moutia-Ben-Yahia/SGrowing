// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { DbQueryRouterCockpitComponent } from './db-query-router-cockpit.component';

describe('DbQueryRouterCockpitComponent', () => {
  it('should initialize with correct default metrics and database nodes', () => {
    const comp = new DbQueryRouterCockpitComponent();
    expect(comp.totalReplicas()).toBe(4);
    expect(comp.healthyNodes()).toBe(3);
    expect(comp.readTrafficPct()).toBe(89.0);
    expect(comp.routingDecisionUs()).toBe(45);
    expect(comp.replicas().length).toBe(4);
  });

  it('should optimize routing decision latency on lag probe', () => {
    const comp = new DbQueryRouterCockpitComponent();
    comp.probeReplicaLag();
    expect(comp.routingDecisionUs()).toBe(38);
  });

  it('should drain lagging replica on circuit breaker trip', () => {
    const comp = new DbQueryRouterCockpitComponent();
    comp.tripCircuitBreaker();
    const node = comp.replicas().find(r => r.replicaId === 'DB-REPLICA-GRU');
    expect(node?.nodeStatus).toBe('Draining');
    expect(node?.activeConnectionsCount).toBe(0);
  });

  it('should verify primary node properties and initial lagging replica', () => {
    const comp = new DbQueryRouterCockpitComponent();
    const primary = comp.replicas().find(r => r.replicaId === 'DB-PRIMARY-IAD');
    expect(primary?.replicationLagMs).toBe(0);
    expect(primary?.nodeStatus).toBe('Healthy_InPool');

    const gru = comp.replicas().find(r => r.replicaId === 'DB-REPLICA-GRU');
    expect(gru?.replicationLagMs).toBe(125);
    expect(gru?.nodeStatus).toBe('Lagging_Degraded');
  });
});
