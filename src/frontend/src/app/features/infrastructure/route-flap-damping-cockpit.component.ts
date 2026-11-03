import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface MonitoredPrefixItem {
  prefixCidr: string;
  originAsn: string;
  currentPenaltyScore: number;
  flapCountTotal: number;
  state: string;
}

export interface FlapEventItem {
  eventId: string;
  prefixCidr: string;
  originAsn: string;
  penaltyIncrement: number;
  newCumulativePenalty: number;
  triggerPeerIp: string;
}

@Component({
  selector: 'app-route-flap-damping-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="route-flap-damping-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 170 · Multi-Cloud Global Networking & BGP</div>
        <h1>Autonomous Zero-Trust BGP Route Flap Damping & Anycast Penalty Ledger</h1>
        <p class="subtitle">RFC 2439 mathematical flap penalty accumulation, dynamic exponential decay damping, automatic prefix suppression, and zero-downtime anycast PoP evacuation</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-monitored-prefixes">
          <div class="metric-label">Monitored BGP Prefixes</div>
          <div class="metric-value positive">{{ monitoredPrefixesCount() }}</div>
          <div class="metric-sub">Multi-cloud edge routing prefixes</div>
        </div>
        <div class="metric-card" data-testid="metric-suppressed-prefixes">
          <div class="metric-label">Suppressed / Damped</div>
          <div class="metric-value">{{ suppressedPrefixesCount() }}</div>
          <div class="metric-sub">Oscillating routes safely isolated</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-penalty">
          <div class="metric-label">Avg Flap Penalty</div>
          <div class="metric-value">{{ avgPenaltyScore() }}</div>
          <div class="metric-sub">Cutoff threshold: 3,000 points</div>
        </div>
        <div class="metric-card" data-testid="metric-flaps-hour">
          <div class="metric-label">Flap Events (1h)</div>
          <div class="metric-value">{{ flapEventsHour() }}</div>
          <div class="metric-sub">Transient flap alerts processed</div>
        </div>
      </section>

      <section class="prefixes-section">
        <div class="section-header">
          <h2>Monitored BGP Anycast Prefixes</h2>
          <button class="action-btn" (click)="recordPrefixFlap()" data-testid="record-flap-btn">
            + Record BGP Route Flap
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="prefixes-table">
            <thead>
              <tr>
                <th>Prefix CIDR</th>
                <th>Origin ASN</th>
                <th>Penalty Score</th>
                <th>Total Flaps</th>
                <th>State</th>
              </tr>
            </thead>
            <tbody>
              @for (p of prefixes(); track p.prefixCidr) {
                <tr [attr.data-testid]="'prefix-row-' + p.prefixCidr">
                  <td><code>{{ p.prefixCidr }}</code></td>
                  <td><strong>{{ p.originAsn }}</strong></td>
                  <td><strong>{{ p.currentPenaltyScore }}</strong></td>
                  <td>{{ p.flapCountTotal }}</td>
                  <td>
                    @if (p.state === 'SuppressedDamped') {
                      <span class="status-pill status-suppressed">DAMPED</span>
                    } @else {
                      <span class="status-pill status-healthy">HEALTHY</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="events-section">
        <h2>Recent Flap Penalty Events</h2>
        <div class="events-grid" data-testid="events-grid">
          @for (e of events(); track e.eventId) {
            <div class="event-card" [attr.data-testid]="'event-card-' + e.eventId">
              <div class="e-header">
                <strong>{{ e.prefixCidr }}</strong>
                <span class="penalty-badge">+{{ e.penaltyIncrement }} Penalty</span>
              </div>
              <div class="e-body">
                <p><strong>Origin ASN:</strong> {{ e.originAsn }}</p>
                <p><strong>Cumulative Score:</strong> {{ e.newCumulativePenalty }}</p>
                <p><strong>Trigger Peer IP:</strong> <code>{{ e.triggerPeerIp }}</code></p>
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
    .prefixes-section, .events-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #334155; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #1e293b; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-healthy { background: #dcfce7; color: #15803d; }
    .status-suppressed { background: #fee2e2; color: #b91c1c; font-weight: 700; }
    .events-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .event-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .e-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .penalty-badge { background: #fee2e2; color: #b91c1c; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .e-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class RouteFlapDampingCockpitComponent {
  monitoredPrefixesCount = signal(2);
  suppressedPrefixesCount = signal(1);
  avgPenaltyScore = signal(1600.0);
  flapEventsHour = signal(18);

  prefixes = signal<MonitoredPrefixItem[]>([
    {
      prefixCidr: '198.51.100.0/24',
      originAsn: 'AS398124',
      currentPenaltyScore: 0,
      flapCountTotal: 0,
      state: 'ActiveHealthy'
    },
    {
      prefixCidr: '203.0.113.0/24',
      originAsn: 'AS13335',
      currentPenaltyScore: 3200,
      flapCountTotal: 4,
      state: 'SuppressedDamped'
    }
  ]);

  events = signal<FlapEventItem[]>([
    {
      eventId: 'FLAP-EVT-001',
      prefixCidr: '203.0.113.0/24',
      originAsn: 'AS13335',
      penaltyIncrement: 1000,
      newCumulativePenalty: 3200,
      triggerPeerIp: '198.51.100.254'
    }
  ]);

  recordPrefixFlap(): void {
    const newPrefix: MonitoredPrefixItem = {
      prefixCidr: '192.0.2.0/24',
      originAsn: 'AS65001',
      currentPenaltyScore: 1000,
      flapCountTotal: 1,
      state: 'ActiveHealthy'
    };
    this.prefixes.update(list => [...list, newPrefix]);
    this.monitoredPrefixesCount.update(v => v + 1);
  }
}
