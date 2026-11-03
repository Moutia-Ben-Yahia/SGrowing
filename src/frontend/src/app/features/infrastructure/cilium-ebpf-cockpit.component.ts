import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CiliumPolicyRecord {
  policyId: string;
  namespace: string;
  policyName: string;
  direction: string;
  action: string;
  targetPort: number;
  protocol: string;
  ebpfProgramHash: string;
  status: string;
}

export interface WireGuardTunnelRecord {
  tunnelId: string;
  sourceNodeId: string;
  destinationNodeId: string;
  meshTransitVnet: string;
  bandwidthMbps: number;
  lossRatePercentage: number;
  tunnelStatus: string;
}

@Component({
  selector: 'app-cilium-ebpf-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="cilium-ebpf-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 155 · High-Performance Cloud Networking</div>
        <h1>Autonomous Cilium eBPF Service Mesh & WireGuard Transit Mesh</h1>
        <p class="subtitle">Kernel-level BPF bypass, sub-15µs packet forwarding, automated WireGuard cryptographic key rotation, and zero-trust L3/L4/L7 policy enforcement</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-mesh-nodes">
          <div class="metric-label">eBPF Mesh Nodes</div>
          <div class="metric-value positive">{{ meshNodesCount() }}</div>
          <div class="metric-sub">Kernel eBPF XDP & TC attached hosts</div>
        </div>
        <div class="metric-card" data-testid="metric-network-policies">
          <div class="metric-label">Active Policies</div>
          <div class="metric-value">{{ networkPoliciesCount() }}</div>
          <div class="metric-sub">Zero-trust CiliumNetworkPolicy filters</div>
        </div>
        <div class="metric-card" data-testid="metric-wireguard-tunnels">
          <div class="metric-label">WireGuard Tunnels</div>
          <div class="metric-value">{{ wireGuardTunnelsCount() }}</div>
          <div class="metric-sub">ChaCha20-Poly1305 cross-cloud transits</div>
        </div>
        <div class="metric-card" data-testid="metric-ebpf-latency">
          <div class="metric-label">Avg eBPF Latency</div>
          <div class="metric-value positive">{{ ebpfLatency() }}µs</div>
          <div class="metric-sub">XDP socket direct kernel forwarding</div>
        </div>
      </section>

      <section class="policies-section">
        <div class="section-header">
          <h2>Enforced eBPF Network Policies</h2>
          <button class="action-btn" (click)="applyNewPolicy()" data-testid="apply-policy-btn">
            + Apply eBPF Policy
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="policies-table">
            <thead>
              <tr>
                <th>Policy ID</th>
                <th>Namespace / Policy</th>
                <th>Direction</th>
                <th>Action</th>
                <th>Port / Protocol</th>
                <th>eBPF Program</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (policy of policies(); track policy.policyId) {
                <tr [attr.data-testid]="'policy-row-' + policy.policyId">
                  <td><code>{{ policy.policyId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ policy.policyName }}</strong>
                    <span class="sub-text">{{ policy.namespace }}</span>
                  </td>
                  <td><span class="dir-badge">{{ policy.direction }}</span></td>
                  <td>
                    @if (policy.action === 'ALLOW') {
                      <span class="action-allow">ALLOW</span>
                    } @else {
                      <span class="action-drop">DROP</span>
                    }
                  </td>
                  <td>{{ policy.targetPort }} / {{ policy.protocol }}</td>
                  <td><code>{{ policy.ebpfProgramHash }}</code></td>
                  <td><span class="status-pill status-enforced">{{ policy.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="tunnels-section">
        <h2>Cross-Cloud WireGuard Encrypted Transits</h2>
        <div class="tunnels-grid" data-testid="tunnels-grid">
          @for (tun of tunnels(); track tun.tunnelId) {
            <div class="tunnel-card" [attr.data-testid]="'tunnel-card-' + tun.tunnelId">
              <div class="tun-header">
                <strong>{{ tun.tunnelId }}</strong>
                <span class="tun-badge">{{ tun.tunnelStatus }}</span>
              </div>
              <div class="tun-body">
                <p><strong>Route:</strong> {{ tun.sourceNodeId }} &rarr; {{ tun.destinationNodeId }}</p>
                <p><strong>Transit:</strong> {{ tun.meshTransitVnet }}</p>
                <p><strong>Bandwidth:</strong> {{ tun.bandwidthMbps }} Mbps (Loss: {{ tun.lossRatePercentage }}%)</p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #e0e7ff; color: #3730a3; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .action-btn { background: #4f46e5; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; display: block; margin-top: 2px; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .dir-badge { background: #f1f5f9; color: #475569; font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 4px; }
    .action-allow { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .action-drop { background: #fee2e2; color: #991b1b; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-enforced { background: #dcfce7; color: #15803d; }
    .tunnels-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .tunnel-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .tun-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .tun-badge { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .tun-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class CiliumEbpfCockpitComponent {
  meshNodesCount = signal(2);
  networkPoliciesCount = signal(2);
  wireGuardTunnelsCount = signal(2);
  ebpfLatency = signal(12.8);

  policies = signal<CiliumPolicyRecord[]>([
    {
      policyId: 'POLICY-001',
      namespace: 'growth-mesh',
      policyName: 'allow-mutual-tls-in-cluster',
      direction: 'INGRESS',
      action: 'ALLOW',
      targetPort: 443,
      protocol: 'TCP',
      ebpfProgramHash: 'EBPF_PROG_4B8A29F',
      status: 'ACTIVE_ENFORCED'
    },
    {
      policyId: 'POLICY-002',
      namespace: 'finance-core',
      policyName: 'deny-unauthorized-external-egress',
      direction: 'EGRESS',
      action: 'DROP',
      targetPort: 80,
      protocol: 'TCP',
      ebpfProgramHash: 'EBPF_PROG_9F12C0A',
      status: 'ACTIVE_ENFORCED'
    }
  ]);

  tunnels = signal<WireGuardTunnelRecord[]>([
    {
      tunnelId: 'WG-TUN-001',
      sourceNodeId: 'NODE-AWS-EUC1-01',
      destinationNodeId: 'NODE-GCP-EUW1-01',
      meshTransitVnet: 'vnet-transit-crosscloud-prod',
      bandwidthMbps: 9400,
      lossRatePercentage: 0.001,
      tunnelStatus: 'TUNNEL_ESTABLISHED'
    },
    {
      tunnelId: 'WG-TUN-002',
      sourceNodeId: 'NODE-AZURE-WEU-01',
      destinationNodeId: 'NODE-AWS-EUC1-01',
      meshTransitVnet: 'vnet-transit-azure-aws-prod',
      bandwidthMbps: 9200,
      lossRatePercentage: 0.002,
      tunnelStatus: 'TUNNEL_ESTABLISHED'
    }
  ]);

  applyNewPolicy(): void {
    const newPolicy: CiliumPolicyRecord = {
      policyId: `POLICY-00${this.policies().length + 1}`,
      namespace: 'telephony-voice',
      policyName: 'fastpath-udp-kernel-bypass',
      direction: 'INGRESS',
      action: 'ALLOW',
      targetPort: 5060,
      protocol: 'UDP',
      ebpfProgramHash: 'EBPF_PROG_5C33A11',
      status: 'ACTIVE_ENFORCED'
    };
    this.policies.update(list => [...list, newPolicy]);
    this.networkPoliciesCount.update(v => v + 1);
  }
}
