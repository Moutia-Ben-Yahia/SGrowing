import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface VehicleTelemetryRecord {
  vehicleId: string;
  oemBrand: string;
  vehicleModel: string;
  currentGeoCoordinates: string;
  speedKmh: number;
  drivingEnvironment: string;
  driverAttentionScore: number;
}

export interface InCarSessionRecord {
  sessionId: string;
  vehicleId: string;
  winningAdvertiserId: string;
  clearingCpm: number;
  audioDurationSec: number;
  contextualIntentTag: string;
  safetyScreenPassed: boolean;
  auctionResult: string;
}

@Component({
  selector: 'app-in-car-ad-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="in-car-ad-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 152 · Connected Vehicle Ad Tech</div>
        <h1>Autonomous In-Car Infotainment Voice Ad Bidding Mesh</h1>
        <p class="subtitle">Contextual sub-second voice RTB auctions, ADAS safety screens, driver attention attestation, and hyper-local geo navigation triggers</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-vehicles">
          <div class="metric-label">Connected Vehicles</div>
          <div class="metric-value positive">{{ activeVehiclesCount() }}</div>
          <div class="metric-sub">Active infotainment telemetry streams</div>
        </div>
        <div class="metric-card" data-testid="metric-bidding-sessions">
          <div class="metric-label">RTB Bidding Sessions</div>
          <div class="metric-value">{{ biddingSessionsCount() }}</div>
          <div class="metric-sub">Real-time voice ad auctions</div>
        </div>
        <div class="metric-card" data-testid="metric-average-cpm">
          <div class="metric-label">Average CPM Bid</div>
          <div class="metric-value positive">\${{ averageCpm() }}</div>
          <div class="metric-sub">Driver-intent premium pricing</div>
        </div>
        <div class="metric-card" data-testid="metric-fill-rate">
          <div class="metric-label">Auction Fill Rate</div>
          <div class="metric-value">{{ fillRate() }}%</div>
          <div class="metric-sub">ADAS safety compliance filtered</div>
        </div>
      </section>

      <section class="sessions-section">
        <div class="section-header">
          <h2>In-Car Voice Ad Auctions</h2>
          <button class="action-btn" (click)="submitInCarBid()" data-testid="submit-bid-btn">
            + Submit Ad Bid
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="sessions-table">
            <thead>
              <tr>
                <th>Session ID</th>
                <th>Vehicle / OEM</th>
                <th>Winning Advertiser</th>
                <th>Clearing CPM</th>
                <th>Context Intent</th>
                <th>Safety Screen</th>
                <th>Result</th>
              </tr>
            </thead>
            <tbody>
              @for (session of sessions(); track session.sessionId) {
                <tr [attr.data-testid]="'session-row-' + session.sessionId">
                  <td><code>{{ session.sessionId }}</code></td>
                  <td><strong>{{ session.vehicleId }}</strong></td>
                  <td class="primary-cell">
                    <strong>{{ session.winningAdvertiserId }}</strong>
                    <span class="sub-text">{{ session.audioDurationSec }}s Voice Audio</span>
                  </td>
                  <td><strong>\${{ session.clearingCpm.toFixed(2) }}</strong></td>
                  <td><span class="tag-badge">{{ session.contextualIntentTag }}</span></td>
                  <td>
                    @if (session.safetyScreenPassed) {
                      <span class="safety-pass">ADAS OK</span>
                    } @else {
                      <span class="safety-fail">BLOCKED</span>
                    }
                  </td>
                  <td><span class="status-pill status-won">{{ session.auctionResult }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="vehicles-section">
        <h2>Connected Fleet Telemetry Mesh</h2>
        <div class="fleet-grid" data-testid="vehicles-grid">
          @for (veh of vehicles(); track veh.vehicleId) {
            <div class="vehicle-card" [attr.data-testid]="'vehicle-card-' + veh.vehicleId">
              <div class="veh-header">
                <strong>{{ veh.vehicleModel }} ({{ veh.oemBrand }})</strong>
                <span class="speed-badge">{{ veh.speedKmh }} km/h</span>
              </div>
              <div class="veh-body">
                <p><strong>ID:</strong> {{ veh.vehicleId }}</p>
                <p><strong>Geo:</strong> {{ veh.currentGeoCoordinates }}</p>
                <p><strong>Environment:</strong> {{ veh.drivingEnvironment }}</p>
                <p><strong>Driver Attention:</strong> {{ (veh.driverAttentionScore * 100).toFixed(0) }}%</p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #e0f2fe; color: #0369a1; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .action-btn { background: #0284c7; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; display: block; margin-top: 2px; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .tag-badge { background: #f1f5f9; color: #334155; font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 4px; }
    .safety-pass { background: #dcfce7; color: #15803d; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .safety-fail { background: #fee2e2; color: #991b1b; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-won { background: #dcfce7; color: #15803d; }
    .fleet-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .vehicle-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .veh-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .speed-badge { background: #e2e8f0; color: #1e293b; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .veh-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class InCarAdCockpitComponent {
  activeVehiclesCount = signal(2);
  biddingSessionsCount = signal(2);
  averageCpm = signal(34.50);
  fillRate = signal(91.5);

  sessions = signal<InCarSessionRecord[]>([
    {
      sessionId: 'CAR-BID-001',
      vehicleId: 'VEH-TESLA-8821',
      winningAdvertiserId: 'ADV-STARBUCKS-GO',
      clearingCpm: 32.50,
      audioDurationSec: 10,
      contextualIntentTag: 'MORNING_COMMUTE_COFFEE',
      safetyScreenPassed: true,
      auctionResult: 'WON'
    },
    {
      sessionId: 'CAR-BID-002',
      vehicleId: 'VEH-MERC-4419',
      winningAdvertiserId: 'ADV-SHELL-RECHARGE',
      clearingCpm: 36.50,
      audioDurationSec: 15,
      contextualIntentTag: 'HIGHWAY_EV_CHARGING',
      safetyScreenPassed: true,
      auctionResult: 'WON'
    }
  ]);

  vehicles = signal<VehicleTelemetryRecord[]>([
    {
      vehicleId: 'VEH-TESLA-8821',
      oemBrand: 'Tesla',
      vehicleModel: 'Model Y',
      currentGeoCoordinates: '37.7749,-122.4194',
      speedKmh: 48.0,
      drivingEnvironment: 'URBAN_HIGHWAY',
      driverAttentionScore: 0.95
    },
    {
      vehicleId: 'VEH-MERC-4419',
      oemBrand: 'Mercedes-Benz',
      vehicleModel: 'EQS Sedan',
      currentGeoCoordinates: '52.5200,13.4050',
      speedKmh: 110.0,
      drivingEnvironment: 'AUTOBAHN',
      driverAttentionScore: 0.92
    }
  ]);

  submitInCarBid(): void {
    const newSession: InCarSessionRecord = {
      sessionId: `CAR-BID-00${this.biddingSessionsCount() + 1}`,
      vehicleId: 'VEH-TESLA-8821',
      winningAdvertiserId: 'ADV-BP-PULSE',
      clearingCpm: 38.00,
      audioDurationSec: 12,
      contextualIntentTag: 'FAST_CHARGER_OFF_RAMP',
      safetyScreenPassed: true,
      auctionResult: 'WON'
    };
    this.sessions.update(list => [...list, newSession]);
    this.biddingSessionsCount.update(v => v + 1);
  }
}
