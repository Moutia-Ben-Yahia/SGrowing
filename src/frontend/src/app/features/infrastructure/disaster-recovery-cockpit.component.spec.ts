// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { DisasterRecoveryCockpitComponent } from './disaster-recovery-cockpit.component';

describe('DisasterRecoveryCockpitComponent', () => {
  it('initializes with AWS primary and healthy quorum', () => {
    const comp = new DisasterRecoveryCockpitComponent();
    expect(comp.activePrimary()).toBe('AWS us-east-1');
    expect(comp.quorumHealthy()).toBe(true);
    expect(comp.nodes().length).toBe(4);
    expect(comp.nodes().find(n => n.role === 'PrimaryActive')?.nodeId).toBe('node_aws_primary');
  });

  it('executes failover to GCP and updates primary cluster', () => {
    const comp = new DisasterRecoveryCockpitComponent();
    comp.executeFailoverToGcp();
    expect(comp.activePrimary()).toBe('GCP Europe West');
    expect(comp.nodes().find(n => n.role === 'PrimaryActive')?.nodeId).toBe('node_gcp_standby');
    expect(comp.nodes().find(n => n.nodeId === 'node_aws_primary')?.role).toBe('HotStandbySynchronous');
  });
});
