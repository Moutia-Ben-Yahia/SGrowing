import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface VehicleBidItem {
  bidId: string;
  vehicleMakeModel: string;
  contextType: string;
  speedKmh: number;
  matchedCampaignId: string;
  winningBidCpmUsd: number;
  audioAssetUri: string;
}

export interface PlayoutItem {
  attestationId: string;
  bidId: string;
  vehicleHardwareId: string;
  durationSeconds: number;
  volumeDb: number;
  isVerified: boolean;
}

@Component({
  selector: 'app-vehicle-audio-dsp-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="vehicle-audio-dsp-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 172 · Connected Vehicle In-Cabin Audio DSP</div>
        <h1>Autonomous Connected Vehicle Contextual Audio DSP Ads & In-Cabin Commute Attribution</h1>
        <p class="subtitle">OpenRTB in-cabin audio ad insertion, speed and commute context awareness, EBU R128 volume normalization, and drive-by footfall attribution</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-vehicles">
          <div class="metric-label">Connected Vehicles Active</div>
          <div class="metric-value positive">{{ activeVehiclesCount() }}</div>
          <div class="metric-sub">Infotainment-enabled OEM cabins</div>
        </div>
        <div class="metric-card" data-testid="metric-impressions-today">
          <div class="metric-label">Audio Impressions Today</div>
          <div class="metric-value">{{ impressionsToday() }}</div>
          <div class="metric-sub">Contextually delivered 15s spots</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-cpm">
          <div class="metric-label">Avg In-Cabin CPM</div>
          <div class="metric-value positive">\${{ avgCpmUsd().toFixed(2) }}</div>
          <div class="metric-sub">High-attention commute premium</div>
        </div>
        <div class="metric-card" data-testid="metric-commute-lift">
          <div class="metric-label">Commute Conversion Lift</div>
          <div class="metric-value positive">+{{ commuteLiftPct() }}%</div>
          <div class="metric-sub">Attributed physical drive-thru visits</div>
        </div>
      </section>

      <section class="bids-section">
        <div class="section-header">
          <h2>Real-Time In-Cabin Audio Ad Bids</h2>
          <button class="action-btn" (click)="bidNewVehicleSlot()" data-testid="bid-slot-btn">
            + Bid In-Cabin Audio Slot
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="bids-table">
            <thead>
              <tr>
                <th>Bid ID</th>
                <th>Vehicle Model</th>
                <th>Commute Context</th>
                <th>Speed</th>
                <th>Campaign</th>
                <th>Winning CPM</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (b of bids(); track b.bidId) {
                <tr [attr.data-testid]="'bid-row-' + b.bidId">
                  <td><code>{{ b.bidId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ b.vehicleMakeModel }}</strong>
                  </td>
                  <td><span class="context-badge">{{ b.contextType }}</span></td>
                  <td>{{ b.speedKmh }} km/h</td>
                  <td><strong>{{ b.matchedCampaignId }}</strong></td>
                  <td><span class="cpm-pill">\${{ b.winningBidCpmUsd.toFixed(2) }}</span></td>
                  <td><span class="status-pill status-won">WON</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="playouts-section">
        <h2>Playout Attestations & In-Cabin Hardware Verification</h2>
        <div class="playouts-grid" data-testid="playouts-grid">
          @for (p of playouts(); track p.attestationId) {
            <div class="playout-card" [attr.data-testid]="'playout-card-' + p.attestationId">
              <div class="p-header">
                <strong>{{ p.vehicleHardwareId }}</strong>
                <span class="norm-badge">{{ p.volumeDb }} dB EBU R128</span>
              </div>
              <div class="p-body">
                <p><strong>Associated Bid:</strong> <code>{{ p.bidId }}</code></p>
                <p><strong>Duration:</strong> {{ p.durationSeconds }} seconds</p>
                <p><strong>Verification:</strong> <span class="verified-pill">HARDWARE_ATTESTED</span></p>
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
    .badge { display: inline-block; padding: 4px 10px; background: #e0f2fe; color: #0284c7; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #0369a1; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .bids-section, .playouts-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #0284c7; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #0369a1; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .context-badge { background: #f0fdf4; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .cpm-pill { color: #059669; font-weight: 700; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-won { background: #dcfce7; color: #15803d; }
    .playouts-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-top: 14px; }
    .playout-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .p-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .norm-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .p-body p { margin: 4px 0; font-size: 12px; }
    .verified-pill { color: #059669; font-weight: 700; font-size: 11px; }
  `]
})
export class VehicleAudioDspCockpitComponent {
  activeVehiclesCount = signal(4210);
  impressionsToday = signal(18920);
  avgCpmUsd = signal(24.80);
  commuteLiftPct = signal(38.2);

  bids = signal<VehicleBidItem[]>([
    {
      bidId: 'BID-VEH-001',
      vehicleMakeModel: 'Tesla Model Y (Infotainment 2026.4)',
      contextType: 'MorningRushCongestion',
      speedKmh: 18.5,
      matchedCampaignId: 'CAMP-COFFEE-DRIVETHRU',
      winningBidCpmUsd: 26.50,
      audioAssetUri: 'https://cdn.sgrowing.io/audio/peets-coffee-drive-thru-15s.aac'
    }
  ]);

  playouts = signal<PlayoutItem[]>([
    {
      attestationId: 'ATT-VEH-001',
      bidId: 'BID-VEH-001',
      vehicleHardwareId: 'VIN-HARDWARE-99214A',
      durationSeconds: 15,
      volumeDb: -14.0,
      isVerified: true
    }
  ]);

  bidNewVehicleSlot(): void {
    const newBid: VehicleBidItem = {
      bidId: `BID-VEH-00${this.bids().length + 1}`,
      vehicleMakeModel: 'Porsche Taycan (PCM 6.0)',
      contextType: 'HighwayCruising',
      speedKmh: 110.0,
      matchedCampaignId: 'CAMP-AUTOMOTIVE-PREMIUM',
      winningBidCpmUsd: 28.00,
      audioAssetUri: 'https://cdn.sgrowing.io/audio/ambient-highway-15s.aac'
    };
    this.bids.update(list => [...list, newBid]);
  }
}
