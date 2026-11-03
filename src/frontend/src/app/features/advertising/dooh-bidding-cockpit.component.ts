import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DoohBillboardPlacement {
  placementId: string;
  venueName: string;
  geoLocationDma: string;
  screenResolution: string;
  loopDurationSeconds: number;
  pedestrianHourlyFlow: number;
  dynamicAudienceMultiplier: number;
  clearingCpmUsd: number;
  status: string;
}

export interface MobileFootTrafficAttribution {
  attributionId: string;
  placementId: string;
  hashedMaidDeviceGroup: string;
  physicalStoreLocation: string;
  attributedStoreVisits: number;
  liftOverBaselinePct: number;
}

@Component({
  selector: 'app-dooh-bidding-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="dooh-bidding-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 147 · Digital Out-of-Home & Programmatic Media</div>
        <h1>DOOH Real-Time Programmatic Bidding & Mobile Foot-Traffic Mesh</h1>
        <p class="subtitle">Millisecond-scale OpenRTB billboard auctions, dynamic sensor audience multipliers, and privacy-safe mobile device graph conversion attribution</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-screens">
          <div class="metric-label">Active Digital Screens</div>
          <div class="metric-value">{{ totalScreens() }}</div>
          <div class="metric-sub">Connected 4K/8K airport & urban displays</div>
        </div>
        <div class="metric-card" data-testid="metric-audience-multiplier">
          <div class="metric-label">Avg Audience Multiplier</div>
          <div class="metric-value positive">{{ avgMultiplier() }}x</div>
          <div class="metric-sub">Pedestrian & vehicular flow sensor lift</div>
        </div>
        <div class="metric-card" data-testid="metric-foot-traffic">
          <div class="metric-label">Attributed Store Visits</div>
          <div class="metric-value positive">{{ totalVisits().toLocaleString() }}</div>
          <div class="metric-sub">Geofenced mobile device graph matches</div>
        </div>
        <div class="metric-card" data-testid="metric-dooh-spend">
          <div class="metric-label">Total DOOH Spend</div>
          <div class="metric-value">\${{ totalSpend().toLocaleString() }}</div>
          <div class="metric-sub">Dynamic clearing CPM programmatic volume</div>
        </div>
      </section>

      <section class="screens-section">
        <div class="section-header">
          <h2>Active DOOH Programmatic Placements</h2>
          <button class="bid-btn" (click)="submitNewBid()" data-testid="submit-bid-btn">
            + Submit Programmatic Bid
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="screens-table">
            <thead>
              <tr>
                <th>Venue & Screen</th>
                <th>DMA Location</th>
                <th>Resolution</th>
                <th>Loop</th>
                <th>Hourly Flow</th>
                <th>Audience Multiplier</th>
                <th>Clearing CPM</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (screen of placements(); track screen.placementId) {
                <tr [attr.data-testid]="'screen-row-' + screen.placementId">
                  <td class="primary-cell">
                    <strong>{{ screen.venueName }}</strong>
                    <span class="sub-text">{{ screen.placementId }}</span>
                  </td>
                  <td><span class="dma-badge">{{ screen.geoLocationDma }}</span></td>
                  <td>{{ screen.screenResolution }}</td>
                  <td>{{ screen.loopDurationSeconds }}s</td>
                  <td>{{ screen.pedestrianHourlyFlow.toLocaleString() }}</td>
                  <td><span class="mult-badge">{{ screen.dynamicAudienceMultiplier }}x</span></td>
                  <td>\${{ screen.clearingCpmUsd.toFixed(2) }}</td>
                  <td><span class="status-pill status-serving">{{ screen.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="attribution-section">
        <h2>Recent Geofenced Mobile Foot-Traffic Attributions</h2>
        <div class="attr-grid" data-testid="attribution-grid">
          @for (attr of attributions(); track attr.attributionId) {
            <div class="attr-card" [attr.data-testid]="'attr-card-' + attr.attributionId">
              <div class="attr-header">
                <strong>{{ attr.physicalStoreLocation }}</strong>
                <span class="lift-badge">+{{ attr.liftOverBaselinePct }}% Lift</span>
              </div>
              <div class="attr-body">
                <p><strong>Visits Attributed:</strong> {{ attr.attributedStoreVisits }}</p>
                <p class="maid-ref"><code>{{ attr.hashedMaidDeviceGroup }}</code></p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #fef3c7; color: #92400e; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .bid-btn { background: #d97706; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; }
    .dma-badge { background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 11px; font-weight: 600; color: #334155; }
    .mult-badge { background: #dcfce7; color: #15803d; font-weight: 700; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-serving { background: #dcfce7; color: #15803d; }
    .attr-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 16px; margin-top: 14px; }
    .attr-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .attr-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .lift-badge { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .attr-body p { margin: 4px 0; font-size: 12px; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
  `]
})
export class DoohBiddingCockpitComponent {
  totalScreens = signal(2);
  avgMultiplier = signal(2.40);
  totalVisits = signal(2100);
  totalSpend = signal(28375);

  placements = signal<DoohBillboardPlacement[]>([
    {
      placementId: 'DOOH-NYC-001',
      venueName: 'Times Square Mega-Tower Spectacular',
      geoLocationDma: 'NYC_TIMES_SQUARE',
      screenResolution: '8K_SURROUND',
      loopDurationSeconds: 15,
      pedestrianHourlyFlow: 42000,
      dynamicAudienceMultiplier: 2.85,
      clearingCpmUsd: 68.50,
      status: 'SERVING'
    },
    {
      placementId: 'DOOH-SFO-002',
      venueName: 'SFO International Terminal 2 Executive Concourse',
      geoLocationDma: 'SFO_AIRPORT_T2',
      screenResolution: '4K_UHD',
      loopDurationSeconds: 10,
      pedestrianHourlyFlow: 12500,
      dynamicAudienceMultiplier: 1.95,
      clearingCpmUsd: 45.00,
      status: 'SERVING'
    }
  ]);

  attributions = signal<MobileFootTrafficAttribution[]>([
    {
      attributionId: 'ATTR-FT-001',
      placementId: 'DOOH-NYC-001',
      hashedMaidDeviceGroup: 'MAID-GRP-89B10',
      physicalStoreLocation: 'Manhattan Flagship Store (5th Ave)',
      attributedStoreVisits: 1420,
      liftOverBaselinePct: 24.5
    },
    {
      attributionId: 'ATTR-FT-002',
      placementId: 'DOOH-SFO-002',
      hashedMaidDeviceGroup: 'MAID-GRP-44A90',
      physicalStoreLocation: 'San Francisco Experience Center (Market St)',
      attributedStoreVisits: 680,
      liftOverBaselinePct: 18.2
    }
  ]);

  submitNewBid(): void {
    const newScreen: DoohBillboardPlacement = {
      placementId: `DOOH-LON-${Math.floor(Math.random() * 900 + 100)}`,
      venueName: 'Piccadilly Circus High-Impact Screen',
      geoLocationDma: 'LON_PICCADILLY',
      screenResolution: '8K_SURROUND',
      loopDurationSeconds: 12,
      pedestrianHourlyFlow: 35000,
      dynamicAudienceMultiplier: 2.65,
      clearingCpmUsd: 58.00,
      status: 'BID_WON'
    };
    this.placements.update(list => [...list, newScreen]);
    this.totalScreens.update(v => v + 1);
  }
}
