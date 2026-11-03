import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DisplayNodeItem {
  nodeId: string;
  venueStoreName: string;
  locationType: string;
  ambientDensityMultiplier: number;
  baseFloorCpmUsd: number;
  isOnline: boolean;
  hardwareThumbprint: string;
}

export interface SlotAuctionItem {
  auctionId: string;
  nodeId: string;
  durationSeconds: number;
  winningCampaignId: string;
  winningBidCpmUsd: number;
  effectiveSlotRevenueUsd: number;
  creativeAssetUri: string;
}

@Component({
  selector: 'app-signage-ssp-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="signage-ssp-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 167 · Retail Media & Digital Signage</div>
        <h1>Autonomous In-Store Digital Signage SSP Dynamic Slot Auction & Verification Mesh</h1>
        <p class="subtitle">OpenRTB 3.0 dynamic slot auctions for physical in-store displays, footfall density bid multipliers, and cryptographically verified proof-of-play hardware attestations</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-nodes">
          <div class="metric-label">Active Display Nodes</div>
          <div class="metric-value positive">{{ activeNodesCount() }}</div>
          <div class="metric-sub">4K in-store smart displays connected</div>
        </div>
        <div class="metric-card" data-testid="metric-auctions-today">
          <div class="metric-label">Slot Auctions Today</div>
          <div class="metric-value">{{ totalAuctionsToday() }}</div>
          <div class="metric-sub">Real-time programmatic impressions cleared</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-cpm">
          <div class="metric-label">Avg Winning CPM</div>
          <div class="metric-value positive">\${{ avgWinningCpmUsd() }}</div>
          <div class="metric-sub">Dynamic footfall-weighted yield</div>
        </div>
        <div class="metric-card" data-testid="metric-daily-revenue">
          <div class="metric-label">Daily Programmatic Revenue</div>
          <div class="metric-value positive">\${{ (dailyGrossRevenueUsd() / 1000).toFixed(1) }}k</div>
          <div class="metric-sub">Verified proof-of-play impressions</div>
        </div>
      </section>

      <section class="nodes-section">
        <div class="section-header">
          <h2>Active Retail Display Nodes</h2>
          <button class="action-btn" (click)="registerNewDisplayNode()" data-testid="register-node-btn">
            + Register Display Node
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="nodes-table">
            <thead>
              <tr>
                <th>Node ID</th>
                <th>Venue & Store</th>
                <th>Location</th>
                <th>Footfall Multiplier</th>
                <th>Floor CPM</th>
                <th>TPM Attestation</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (n of nodes(); track n.nodeId) {
                <tr [attr.data-testid]="'node-row-' + n.nodeId">
                  <td><code>{{ n.nodeId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ n.venueStoreName }}</strong>
                  </td>
                  <td><span class="loc-badge">{{ n.locationType }}</span></td>
                  <td><strong>{{ n.ambientDensityMultiplier }}x</strong></td>
                  <td>\${{ n.baseFloorCpmUsd.toFixed(2) }}</td>
                  <td><code>{{ n.hardwareThumbprint }}</code></td>
                  <td><span class="status-pill status-online">ONLINE</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="auctions-section">
        <h2>Recent Programmatic Slot Auctions & Attestations</h2>
        <div class="auctions-grid" data-testid="auctions-grid">
          @for (a of auctions(); track a.auctionId) {
            <div class="auction-card" [attr.data-testid]="'auction-card-' + a.auctionId">
              <div class="a-header">
                <strong>{{ a.winningCampaignId }}</strong>
                <span class="cpm-badge">\${{ a.winningBidCpmUsd }} CPM</span>
              </div>
              <div class="a-body">
                <p><strong>Display Node:</strong> <code>{{ a.nodeId }}</code></p>
                <p><strong>Slot Duration:</strong> {{ a.durationSeconds }}s</p>
                <p><strong>Effective Slot Revenue:</strong> \${{ a.effectiveSlotRevenueUsd.toFixed(4) }}</p>
                <p><strong>Creative Asset:</strong> <a [href]="a.creativeAssetUri" target="_blank">{{ a.creativeAssetUri }}</a></p>
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
    .badge { display: inline-block; padding: 4px 10px; background: #fae8ff; color: #86198f; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #701a75; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .nodes-section, .auctions-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #a21caf; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #86198f; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .loc-badge { background: #f3e8ff; color: #6b21a8; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-online { background: #dcfce7; color: #15803d; }
    .auctions-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .auction-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .a-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .cpm-badge { background: #dbeafe; color: #1d4ed8; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .a-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class SignageSspCockpitComponent {
  activeNodesCount = signal(2);
  totalAuctionsToday = signal(1845);
  avgWinningCpmUsd = signal(21.80);
  dailyGrossRevenueUsd = signal(4892.40);

  nodes = signal<DisplayNodeItem[]>([
    {
      nodeId: 'SIGN-NODE-001',
      venueStoreName: 'Target Supercenter Flagship - Downtown Chicago',
      locationType: 'SmartCheckoutKiosk',
      ambientDensityMultiplier: 1.85,
      baseFloorCpmUsd: 12.50,
      isOnline: true,
      hardwareThumbprint: 'THUMB-TPM2-99B4F1A0'
    },
    {
      nodeId: 'SIGN-NODE-002',
      venueStoreName: 'Nordstrom Plaza - Seattle WA',
      locationType: 'StoreEntrance',
      ambientDensityMultiplier: 2.10,
      baseFloorCpmUsd: 18.00,
      isOnline: true,
      hardwareThumbprint: 'THUMB-TPM2-88C3E201'
    }
  ]);

  auctions = signal<SlotAuctionItem[]>([
    {
      auctionId: 'AUC-SIGN-001',
      nodeId: 'SIGN-NODE-001',
      durationSeconds: 15,
      winningCampaignId: 'CAMP-BEVERAGE-ESPRESSO',
      winningBidCpmUsd: 24.50,
      effectiveSlotRevenueUsd: 0.3675,
      creativeAssetUri: 'https://cdn.sgrowing.io/creatives/espresso-boost-4k.mp4'
    }
  ]);

  registerNewDisplayNode(): void {
    const newNode: DisplayNodeItem = {
      nodeId: `SIGN-NODE-00${this.nodes().length + 1}`,
      venueStoreName: 'Whole Foods Market - Union Square NYC',
      locationType: 'AisleEndcap',
      ambientDensityMultiplier: 1.95,
      baseFloorCpmUsd: 16.00,
      isOnline: true,
      hardwareThumbprint: 'THUMB-TPM2-55D4A302'
    };
    this.nodes.update(list => [...list, newNode]);
    this.activeNodesCount.update(v => v + 1);
  }
}
