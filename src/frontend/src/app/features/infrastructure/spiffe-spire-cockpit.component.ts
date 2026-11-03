import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface WorkloadSvidRecord {
  svidId: string;
  spiffeId: string;
  attestedNamespace: string;
  serviceAccount: string;
  nodeAttestationType: string;
  x509CertificateSerialNumber: string;
  status: string;
}

export interface TrustDomainFederationRecord {
  federationId: string;
  localTrustDomain: string;
  remoteTrustDomain: string;
  bundleEndpointUrl: string;
  status: string;
}

@Component({
  selector: 'app-spiffe-spire-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="spiffe-spire-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 150 · Zero-Trust SPIFFE/SPIRE Identity Mesh</div>
        <h1>Multi-Cloud SPIFFE/SPIRE Ephemeral Workload Attestation Mesh</h1>
        <p class="subtitle">Cryptographic X.509 SVID issuance, 1-hour short-lived rotation, multi-cloud trust domain bundle federation, and zero-trust service identity</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-svids">
          <div class="metric-label">Active Workload SVIDs</div>
          <div class="metric-value positive">{{ activeSvidsCount() }}</div>
          <div class="metric-sub">Attested ephemeral service identities</div>
        </div>
        <div class="metric-card" data-testid="metric-federated-domains">
          <div class="metric-label">Federated Trust Domains</div>
          <div class="metric-value">{{ federatedDomainsCount() }}</div>
          <div class="metric-sub">Multi-cloud AWS, GCP, Azure roots</div>
        </div>
        <div class="metric-card" data-testid="metric-attestation-latency">
          <div class="metric-label">Avg Attestation Latency</div>
          <div class="metric-value positive">{{ attestationLatency() }}ms</div>
          <div class="metric-sub">Kernel cgroup & k8s PSAT attestation</div>
        </div>
        <div class="metric-card" data-testid="metric-svid-rotations">
          <div class="metric-label">24h SVID Rotations</div>
          <div class="metric-value">{{ svidRotations() }}</div>
          <div class="metric-sub">Zero-downtime 1-hour automated renewals</div>
        </div>
      </section>

      <section class="svids-section">
        <div class="section-header">
          <h2>Attested Workload SPIFFE Identities (SVIDs)</h2>
          <button class="attest-btn" (click)="attestNewWorkload()" data-testid="attest-workload-btn">
            + Attest Workload
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="svids-table">
            <thead>
              <tr>
                <th>SPIFFE ID</th>
                <th>Namespace / SA</th>
                <th>Node Attestation</th>
                <th>Cert Serial Number</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (svid of svids(); track svid.svidId) {
                <tr [attr.data-testid]="'svid-row-' + svid.svidId">
                  <td class="primary-cell">
                    <code>{{ svid.spiffeId }}</code>
                    <span class="sub-text">{{ svid.svidId }}</span>
                  </td>
                  <td>{{ svid.attestedNamespace }} / {{ svid.serviceAccount }}</td>
                  <td><span class="attest-badge">{{ svid.nodeAttestationType }}</span></td>
                  <td><code>{{ svid.x509CertificateSerialNumber }}</code></td>
                  <td><span class="status-pill status-valid">{{ svid.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="federations-section">
        <h2>Multi-Cloud Federated Trust Domains</h2>
        <div class="fed-grid" data-testid="federation-grid">
          @for (fed of federations(); track fed.federationId) {
            <div class="fed-card" [attr.data-testid]="'fed-card-' + fed.federationId">
              <div class="fed-header">
                <strong>{{ fed.remoteTrustDomain }}</strong>
                <span class="status-badge">{{ fed.status }}</span>
              </div>
              <div class="fed-body">
                <p><strong>Local:</strong> {{ fed.localTrustDomain }}</p>
                <p class="endpoint"><code>{{ fed.bundleEndpointUrl }}</code></p>
              </div>
            </div>
          }
        </div>
      </section>
    </div>
  `,
  styles: [`
    .cockpit-container { padding: 24px; max-width: 1200px; margin: 0 auto; font-family: system-ui, -apple-system, sans-serif; color: #1e293b; }
    .cockpit-header { margin-bottom: 24px; }
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #fee2e2; color: #991b1b; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .attest-btn { background: #4f46e5; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; display: block; margin-top: 2px; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .attest-badge { background: #ede9fe; color: #5b21b6; font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-valid { background: #dcfce7; color: #15803d; }
    .fed-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .fed-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .fed-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .status-badge { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .fed-body p { margin: 4px 0; font-size: 12px; }
    .endpoint code { font-size: 10px; color: #475569; }
  `]
})
export class SpiffeSpireCockpitComponent {
  activeSvidsCount = signal(2);
  federatedDomainsCount = signal(2);
  attestationLatency = signal(8.4);
  svidRotations = signal(142);

  svids = signal<WorkloadSvidRecord[]>([
    {
      svidId: 'SVID-001',
      spiffeId: 'spiffe://sgrowing.enterprise/ns/growth-mesh/sa/telephony-whisper-engine',
      attestedNamespace: 'growth-mesh',
      serviceAccount: 'telephony-whisper-engine',
      nodeAttestationType: 'K8S_PSAT',
      x509CertificateSerialNumber: 'SN-77A91F2B04E',
      status: 'ACTIVE_VALID'
    },
    {
      svidId: 'SVID-002',
      spiffeId: 'spiffe://sgrowing.enterprise/ns/finance-core/sa/sovereign-tax-ledger',
      attestedNamespace: 'finance-core',
      serviceAccount: 'sovereign-tax-ledger',
      nodeAttestationType: 'AWS_IID',
      x509CertificateSerialNumber: 'SN-12C94F88A10',
      status: 'ACTIVE_VALID'
    }
  ]);

  federations = signal<TrustDomainFederationRecord[]>([
    {
      federationId: 'FED-001',
      localTrustDomain: 'sgrowing.enterprise',
      remoteTrustDomain: 'aws.eu-central-1.corp',
      bundleEndpointUrl: 'https://spire.aws.eu-central-1.corp:8443/bundle',
      status: 'FEDERATED_HEALTHY'
    },
    {
      federationId: 'FED-002',
      localTrustDomain: 'sgrowing.enterprise',
      remoteTrustDomain: 'gcp.europe-west1.corp',
      bundleEndpointUrl: 'https://spire.gcp.europe-west1.corp:8443/bundle',
      status: 'FEDERATED_HEALTHY'
    }
  ]);

  attestNewWorkload(): void {
    const newSvid: WorkloadSvidRecord = {
      svidId: `SVID-${Math.floor(Math.random() * 900 + 100)}`,
      spiffeId: 'spiffe://sgrowing.enterprise/ns/edge-compute/sa/wasm-router',
      attestedNamespace: 'edge-compute',
      serviceAccount: 'wasm-router',
      nodeAttestationType: 'GCP_IIT',
      x509CertificateSerialNumber: 'SN-88B12C44F01',
      status: 'ACTIVE_VALID'
    };
    this.svids.update(list => [...list, newSvid]);
    this.activeSvidsCount.update(v => v + 1);
  }
}
