import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface PeerItem {
  peerId: string;
  nodeName: string;
  cloudProvider: string;
  publicEndpointIpPort: string;
  internalMeshIp: string;
  natTraversalMethod: string;
  rttLatencyMs: number;
  status: string;
}

export interface IpsecTunnelItem {
  tunnelId: string;
  primaryPeerId: string;
  remoteGatewayIp: string;
  encryptionCipher: string;
  espPort: number;
  fallbackTriggerReason: string;
}

@Component({
  selector: 'app-wireguard-discovery-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="wireguard-discovery-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 165 · Multi-Cloud Peer Mesh & NAT Traversal</div>
        <h1>Autonomous Zero-Trust WireGuard Peer Discovery & IPsec Fallback Mesh</h1>
        <p class="subtitle">STUN/ICE dynamic peer discovery across clouds, sub-15ms WireGuard direct UDP hole punching, and automatic kernel IPsec ESP fallback on firewall blocks</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-discovered-peers">
          <div class="metric-label">Discovered Peers</div>
          <div class="metric-value positive">{{ discoveredPeersCount() }}</div>
          <div class="metric-sub">Multi-cloud mesh endpoint nodes</div>
        </div>
        <div class="metric-card" data-testid="metric-direct-punch">
          <div class="metric-label">Direct P2P Punch Rate</div>
          <div class="metric-value positive">{{ directPunchSuccessPct() }}%</div>
          <div class="metric-sub">STUN UDP 51820 hole punching</div>
        </div>
        <div class="metric-card" data-testid="metric-mesh-rtt">
          <div class="metric-label">Avg Mesh RTT</div>
          <div class="metric-value positive">{{ avgMeshRttMs() }}ms</div>
          <div class="metric-sub">WireGuard ChaCha20 transit latency</div>
        </div>
        <div class="metric-card" data-testid="metric-ipsec-tunnels">
          <div class="metric-label">IPsec Fallback Tunnels</div>
          <div class="metric-value">{{ ipsecFallbackTunnelsCount() }}</div>
          <div class="metric-sub">ESP UDP 4500 firewall bypass transits</div>
        </div>
      </section>

      <section class="peers-section">
        <div class="section-header">
          <h2>Discovered Multi-Cloud WireGuard Peers</h2>
          <button class="action-btn" (click)="discoverNewPeer()" data-testid="discover-peer-btn">
            + Discover Node Peer
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="peers-table">
            <thead>
              <tr>
                <th>Peer ID</th>
                <th>Node Name</th>
                <th>Cloud Provider</th>
                <th>Public Endpoint</th>
                <th>Mesh IP</th>
                <th>NAT Method</th>
                <th>RTT</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (p of peers(); track p.peerId) {
                <tr [attr.data-testid]="'peer-row-' + p.peerId">
                  <td><code>{{ p.peerId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ p.nodeName }}</strong>
                  </td>
                  <td><span class="cloud-badge">{{ p.cloudProvider }}</span></td>
                  <td><code>{{ p.publicEndpointIpPort }}</code></td>
                  <td><code>{{ p.internalMeshIp }}</code></td>
                  <td><span class="nat-badge">{{ p.natTraversalMethod }}</span></td>
                  <td><strong>{{ p.rttLatencyMs }}ms</strong></td>
                  <td>
                    @if (p.status === 'CONNECTED_DIRECT') {
                      <span class="status-pill status-direct">DIRECT</span>
                    } @else {
                      <span class="status-pill status-fallback">IPSEC_FALLBACK</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="ipsec-section">
        <h2>Active Kernel IPsec Fallback Encrypted Tunnels</h2>
        <div class="tunnels-grid" data-testid="tunnels-grid">
          @for (t of tunnels(); track t.tunnelId) {
            <div class="tunnel-card" [attr.data-testid]="'tunnel-card-' + t.tunnelId">
              <div class="t-header">
                <strong>{{ t.tunnelId }}</strong>
                <span class="esp-badge">PORT {{ t.espPort }} (ESP)</span>
              </div>
              <div class="t-body">
                <p><strong>Primary Peer:</strong> {{ t.primaryPeerId }}</p>
                <p><strong>Gateway:</strong> <code>{{ t.remoteGatewayIp }}</code></p>
                <p><strong>Cipher:</strong> {{ t.encryptionCipher }}</p>
                <p><strong>Trigger:</strong> <span class="reason-badge">{{ t.fallbackTriggerReason }}</span></p>
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
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .cloud-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .nat-badge { background: #f1f5f9; color: #334155; font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-direct { background: #dcfce7; color: #15803d; }
    .status-fallback { background: #fee2e2; color: #991b1b; }
    .tunnels-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .tunnel-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .t-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .esp-badge { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .t-body p { margin: 4px 0; font-size: 12px; }
    .reason-badge { background: #fee2e2; color: #991b1b; font-size: 10px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
  `]
})
export class WireGuardDiscoveryCockpitComponent {
  discoveredPeersCount = signal(2);
  directPunchSuccessPct = signal(92);
  avgMeshRttMs = signal(18.6);
  ipsecFallbackTunnelsCount = signal(1);

  peers = signal<PeerItem[]>([
    {
      peerId: 'PEER-AWS-001',
      nodeName: 'aws-us-east-worker-01',
      cloudProvider: 'AWS_US_EAST',
      publicEndpointIpPort: '54.210.12.99:51820',
      internalMeshIp: '10.244.1.10',
      natTraversalMethod: 'DIRECT_PUNCH_STUN',
      rttLatencyMs: 12.4,
      status: 'CONNECTED_DIRECT'
    },
    {
      peerId: 'PEER-ONPREM-002',
      nodeName: 'equinix-chicago-core-01',
      cloudProvider: 'ON_PREM_EQUINIX',
      publicEndpointIpPort: '198.51.100.42:4500',
      internalMeshIp: '10.244.2.20',
      natTraversalMethod: 'ICE_TURN_RELAY',
      rttLatencyMs: 24.8,
      status: 'FALLBACK_IPSEC'
    }
  ]);

  tunnels = signal<IpsecTunnelItem[]>([
    {
      tunnelId: 'IPSEC-TUN-001',
      primaryPeerId: 'PEER-ONPREM-002',
      remoteGatewayIp: '198.51.100.42',
      encryptionCipher: 'AES_GCM_256',
      espPort: 4500,
      fallbackTriggerReason: 'UDP_51820_DROPPED_BY_FIREWALL'
    }
  ]);

  discoverNewPeer(): void {
    const newPeer: PeerItem = {
      peerId: `PEER-00${this.peers().length + 1}`,
      nodeName: 'gcp-europe-west-01',
      cloudProvider: 'GCP_EU_WEST',
      publicEndpointIpPort: '34.76.12.88:51820',
      internalMeshIp: '10.244.4.40',
      natTraversalMethod: 'DIRECT_PUNCH_STUN',
      rttLatencyMs: 15.2,
      status: 'CONNECTED_DIRECT'
    };
    this.peers.update(list => [...list, newPeer]);
    this.discoveredPeersCount.update(v => v + 1);
  }
}
