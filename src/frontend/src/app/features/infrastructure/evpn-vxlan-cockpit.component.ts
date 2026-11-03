import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface VtepItem {
  vtepId: string;
  nodeName: string;
  cloudRegion: string;
  loopbackIp: string;
  vxlanPort: number;
  isBgpPeered: boolean;
}

export interface TenantVniItem {
  vniId: string;
  vni: number;
  tenantId: string;
  subnetCidr: string;
  anycastGatewayIp: string;
  vtepCount: number;
}

@Component({
  selector: 'app-evpn-vxlan-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="evpn-vxlan-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 175 · Multi-Cloud Bare-Metal & K8s Networking</div>
        <h1>Autonomous Zero-Trust Multi-Cloud eBGP EVPN VXLAN Overlay Mesh</h1>
        <p class="subtitle">BGP EVPN Type-2 (MAC/IP) and Type-5 (IP Prefix) route distribution across bare-metal GPU and multi-cloud Kubernetes clusters with distributed anycast gateways</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-vteps">
          <div class="metric-label">Active VTEP Endpoints</div>
          <div class="metric-value positive">{{ activeVtepsCount() }}</div>
          <div class="metric-sub">Multi-cloud tunnel termination nodes</div>
        </div>
        <div class="metric-card" data-testid="metric-tenant-vnis">
          <div class="metric-label">Configured Tenant VNIs</div>
          <div class="metric-value">{{ totalTenantVnis() }}</div>
          <div class="metric-sub">Isolated Layer-2 broadcast domains</div>
        </div>
        <div class="metric-card" data-testid="metric-evpn-routes">
          <div class="metric-label">EVPN Routes Advertised</div>
          <div class="metric-value positive">{{ advertisedRoutesCount() }}</div>
          <div class="metric-sub">Type-2 & Type-5 BGP NLRI entries</div>
        </div>
        <div class="metric-card" data-testid="metric-mesh-throughput">
          <div class="metric-label">Mesh Line-Rate Throughput</div>
          <div class="metric-value positive">{{ meshThroughputGbps() }} Gbps</div>
          <div class="metric-sub">Wire-speed overlay forwarding</div>
        </div>
      </section>

      <section class="vteps-section">
        <div class="section-header">
          <h2>Active VXLAN Tunnel Endpoints (VTEPs)</h2>
          <button class="action-btn" (click)="registerNewVtep()" data-testid="register-vtep-btn">
            + Register VTEP Node
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="vteps-table">
            <thead>
              <tr>
                <th>VTEP ID</th>
                <th>Node Hostname</th>
                <th>Cloud & Region</th>
                <th>Underlay Loopback</th>
                <th>VXLAN Port</th>
                <th>BGP Peered</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (v of vteps(); track v.vtepId) {
                <tr [attr.data-testid]="'vtep-row-' + v.vtepId">
                  <td><code>{{ v.vtepId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ v.nodeName }}</strong>
                  </td>
                  <td><span class="region-badge">{{ v.cloudRegion }}</span></td>
                  <td><code>{{ v.loopbackIp }}</code></td>
                  <td>UDP {{ v.vxlanPort }}</td>
                  <td><span class="peered-pill">PEERED_ESTABLISHED</span></td>
                  <td><span class="status-pill status-up">FORWARDING</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="vnis-section">
        <h2>Tenant Virtual Network Identifiers (VNIs)</h2>
        <div class="vnis-grid" data-testid="vnis-grid">
          @for (n of vnis(); track n.vniId) {
            <div class="vni-card" [attr.data-testid]="'vni-card-' + n.vniId">
              <div class="v-header">
                <strong>VNI {{ n.vni }}</strong>
                <span class="tenant-badge">{{ n.tenantId }}</span>
              </div>
              <div class="v-body">
                <p><strong>Subnet CIDR:</strong> <code>{{ n.subnetCidr }}</code></p>
                <p><strong>Anycast Gateway IP:</strong> <code>{{ n.anycastGatewayIp }}</code></p>
                <p><strong>Associated VTEP Mesh:</strong> {{ n.vtepCount }} endpoints</p>
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
    .vteps-section, .vnis-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #0f172a; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #1e293b; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .region-badge { background: #e2e8f0; color: #334155; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .peered-pill { color: #059669; font-weight: 700; font-size: 11px; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-up { background: #dcfce7; color: #15803d; }
    .vnis-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-top: 14px; }
    .vni-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .v-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .tenant-badge { background: #e0e7ff; color: #3730a3; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .v-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class EvpnVxlanCockpitComponent {
  activeVtepsCount = signal(2);
  totalTenantVnis = signal(1);
  advertisedRoutesCount = signal(4280);
  meshThroughputGbps = signal(184.5);

  vteps = signal<VtepItem[]>([
    {
      vtepId: 'VTEP-AWS-US-EAST',
      nodeName: 'aws-us-east-vtep-01',
      cloudRegion: 'AWS_US_EAST_1',
      loopbackIp: '10.250.0.1',
      vxlanPort: 4789,
      isBgpPeered: true
    },
    {
      vtepId: 'VTEP-EQUINIX-METAL',
      nodeName: 'equinix-sv10-metal-01',
      cloudRegion: 'EQUINIX_SV10',
      loopbackIp: '10.250.0.2',
      vxlanPort: 4789,
      isBgpPeered: true
    }
  ]);

  vnis = signal<TenantVniItem[]>([
    {
      vniId: 'VNI-CONF-1001',
      vni: 10010,
      tenantId: 'TENANT-STRATEGIC-GLOBAL',
      subnetCidr: '172.24.10.0/24',
      anycastGatewayIp: '172.24.10.1',
      vtepCount: 2
    }
  ]);

  registerNewVtep(): void {
    const newVtep: VtepItem = {
      vtepId: `VTEP-00${this.vteps().length + 1}`,
      nodeName: 'coreweave-ord1-gpu-01',
      cloudRegion: 'COREWEAVE_ORD1',
      loopbackIp: '10.250.0.4',
      vxlanPort: 4789,
      isBgpPeered: true
    };
    this.vteps.update(list => [...list, newVtep]);
    this.activeVtepsCount.update(v => v + 1);
  }
}
