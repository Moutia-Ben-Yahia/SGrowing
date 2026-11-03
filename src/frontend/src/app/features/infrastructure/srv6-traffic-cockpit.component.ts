import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface PolicyPathItem {
  policyId: string;
  policyName: string;
  ingressHeadend: string;
  egressTailend: string;
  profile: string;
  pathLatencyMs: number;
  isActive: boolean;
}

export interface TelemetryItem {
  telemetryId: string;
  policyId: string;
  pps: number;
  throughputGbps: number;
  lossRatio: number;
}

@Component({
  selector: 'app-srv6-traffic-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="srv6-traffic-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 180 · IPv6 Segment Routing & Multi-Cloud Backbones</div>
        <h1>Autonomous Multi-Cloud Segment Routing over IPv6 (SRv6) Traffic Engineering Mesh</h1>
        <p class="subtitle">IPv6 Segment Routing Header (SRH) path computation, latency-optimized policy steering across AWS, GCP, Azure, and Equinix, and sub-10ms TI-LFA fast reroute</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-policies-count">
          <div class="metric-label">Configured SRv6 Policies</div>
          <div class="metric-value positive">{{ policiesCount() }}</div>
          <div class="metric-sub">Multi-cloud traffic engineering paths</div>
        </div>
        <div class="metric-card" data-testid="metric-active-segments">
          <div class="metric-label">Active IPv6 SIDs</div>
          <div class="metric-value">{{ activeSegmentsCount() }}</div>
          <div class="metric-sub">End / End.X segment functions</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-latency">
          <div class="metric-label">Avg Multi-Cloud RTT</div>
          <div class="metric-value positive">{{ avgLatencyMs() }}ms</div>
          <div class="metric-sub">Transatlantic SRv6 steering latency</div>
        </div>
        <div class="metric-card" data-testid="metric-mesh-throughput">
          <div class="metric-label">Overlay Mesh Line-Rate</div>
          <div class="metric-value positive">{{ meshThroughputGbps() }} Gbps</div>
          <div class="metric-sub">Aggregated backplane throughput</div>
        </div>
      </section>

      <section class="policies-section">
        <div class="section-header">
          <h2>Configured SRv6 Policy Paths</h2>
          <button class="action-btn" (click)="createPolicyPath()" data-testid="create-policy-btn">
            + Create SRv6 Policy
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="policies-table">
            <thead>
              <tr>
                <th>Policy ID</th>
                <th>Policy Name</th>
                <th>Ingress Headend</th>
                <th>Egress Tailend</th>
                <th>Profile</th>
                <th>Path RTT</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (p of policies(); track p.policyId) {
                <tr [attr.data-testid]="'policy-row-' + p.policyId">
                  <td><code>{{ p.policyId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ p.policyName }}</strong>
                  </td>
                  <td><code>{{ p.ingressHeadend }}</code></td>
                  <td><code>{{ p.egressTailend }}</code></td>
                  <td><span class="prof-badge">{{ p.profile }}</span></td>
                  <td><strong>{{ p.pathLatencyMs }}ms</strong></td>
                  <td><span class="status-pill status-active">ACTIVE</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="telemetry-section">
        <h2>Live SRv6 Forwarding Telemetry</h2>
        <div class="telemetry-grid" data-testid="telemetry-grid">
          @for (t of telemetry(); track t.telemetryId) {
            <div class="t-card" [attr.data-testid]="'t-card-' + t.telemetryId">
              <div class="t-header">
                <strong>{{ t.policyId }}</strong>
                <span class="rate-badge">{{ t.throughputGbps }} Gbps</span>
              </div>
              <div class="t-body">
                <p><strong>PPS:</strong> {{ (t.pps / 1000000).toFixed(2) }} Mpps</p>
                <p><strong>Packet Loss Ratio:</strong> {{ (t.lossRatio * 100).toFixed(5) }}%</p>
              </div>
            </div>
          }
        </div>
      </section>
    </div>
  `,
  styles: [`
    .cockpit-container { padding: 24px; font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; background: #f8fafc; min-height: 100vh; color: #0f172a; }
    .cockpit-header { margin-bottom: 24px; }
    .badge { display: inline-block; padding: 4px 10px; background: #f1f5f9; color: #334155; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #0f172a; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .policies-section, .telemetry-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #1e293b; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #0f172a; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .prof-badge { background: #ecfdf5; color: #047857; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-active { background: #dcfce7; color: #15803d; }
    .telemetry-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-top: 14px; }
    .t-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .t-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .rate-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .t-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class Srv6TrafficCockpitComponent {
  policiesCount = signal(1);
  activeSegmentsCount = signal(24);
  avgLatencyMs = signal(68.4);
  meshThroughputGbps = signal(125.6);

  policies = signal<PolicyPathItem[]>([
    {
      policyId: 'SRV6-POL-001',
      policyName: 'Transatlantic Low-Latency Edge Steering',
      ingressHeadend: 'headend-aws-us-east-1',
      egressTailend: 'tailend-gcp-europe-west-3',
      profile: 'ULTRA_LOW_LATENCY',
      pathLatencyMs: 68.4,
      isActive: true
    }
  ]);

  telemetry = signal<TelemetryItem[]>([
    {
      telemetryId: 'SRV6-TEL-001',
      policyId: 'SRV6-POL-001',
      pps: 1420000,
      throughputGbps: 18.4,
      lossRatio: 0.00001
    }
  ]);

  createPolicyPath(): void {
    const newPolicy: PolicyPathItem = {
      policyId: `SRV6-POL-00${this.policies().length + 1}`,
      policyName: 'Asia-Pacific Core Transit Path',
      ingressHeadend: 'headend-equinix-ty2',
      egressTailend: 'tailend-aws-ap-southeast-1',
      profile: 'LEAST_COST',
      pathLatencyMs: 74.2,
      isActive: true
    };
    this.policies.update(list => [...list, newPolicy]);
    this.policiesCount.update(v => v + 1);
  }
}
