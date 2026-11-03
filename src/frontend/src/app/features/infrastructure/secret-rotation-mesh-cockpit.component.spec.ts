// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { SecretRotationMeshCockpitComponent } from './secret-rotation-mesh-cockpit.component';

describe('SecretRotationMeshCockpitComponent', () => {
  it('should initialize with secrets and certs', () => {
    const comp = new SecretRotationMeshCockpitComponent();
    expect(comp.secrets().length).toBe(2);
    expect(comp.certs().length).toBe(1);
    expect(comp.secrets()[0].secretId).toBe('SEC-DB-PG-MASTER');
  });

  it('should issue ephemeral cert and prepend to cert list', () => {
    const comp = new SecretRotationMeshCockpitComponent();
    comp.issueEphemeralCert();
    expect(comp.certs().length).toBe(2);
    expect(comp.certs()[0].targetEdgeNode).toBe('EDGE-POP-IAD-02');
    expect(comp.certs()[0].status).toBe('CERT_ACTIVE_VALID');
  });
});
