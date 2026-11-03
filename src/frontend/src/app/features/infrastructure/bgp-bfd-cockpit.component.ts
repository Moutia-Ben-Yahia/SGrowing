import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface BgpBfdSession {
  sessionId: string;
  edgePopLocation: string;
  peerTransitProvider: string;
  localAsn: number;
  peerAsn: number;
  bfdIntervalMs: number;
  packetLossPct: number;
  jitterMs: number;
  sessionState: string;
}

export interface AnycastRouteEvent {
  eventId: string;
  edgePopLocation: string;
  actionTaken: string;
  triggerLatencyMs: number;
  triggerReason: string;
}

@Component({
  selector: 'app-bgp-bfd-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="bgp-bfd-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 145 · Zero-Trust Global Edge Infrastructure</div>
        <h1>Multi-Cloud BGP Anycast BFD Fast-Failover Mesh</h1>
        <p class="subtitle">Microsecond-precision link heartbeats, sub-800ms Anycast route withdrawal, and automated tier-1 carrier failover mesh</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-bfd-sessions">
          <div class="metric-label">Active BFD Sessions</div>
          <div class="metric-value">{{ totalSessions() }}</div>
          <div class="metric-sub">Multi-cloud transit peer sessions</div>
        </div>
        <div class="metric-card" data-testid="metric-healthy-sessions">
          <div class="metric-label">Healthy Sessions</div>
          <div class="metric-value positive">{{ healthySessions() }}</div>
          <div class="metric-sub">Zero-loss sub-second heartbeats</div>
        </div>
        <div class="metric-card" data-testid="metric-edge-health">
          <div class="metric-label">Global Edge Health Index</div>
          <div class="metric-value positive">{{ healthIndex() }}%</div>
          <div class="metric-sub">Anycast route propagation health</div>
        </div>
        <div class="metric-card" data-testid="metric-failover-target">
          <div class="metric-label">Failover Latency Target</div>
          <div class="metric-value">{{ failoverTarget() }}ms</div>
          <div class="metric-sub">Sub-second Anycast BFD SLA</div>
        </div>
      </section>

      <section class="sessions-section">
        <h2>Global Anycast PoP BFD Peers</h2>
        <div class="table-wrapper">
          <table class="data-table" data-testid="bfd-sessions-table">
            <thead>
              <tr>
                <th>PoP & Session ID</th>
                <th>Transit Peer</th>
                <th>ASNs (Local / Peer)</th>
                <th>Interval</th>
                <th>Loss & Jitter</th>
                <th>State</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (session of sessions(); track session.sessionId) {
                <tr [attr.data-testid]="'session-row-' + session.sessionId">
                  <td class="primary-cell">
                    <strong>{{ session.edgePopLocation }}</strong>
                    <span class="sub-text">{{ session.sessionId }}</span>
                  </td>
                  <td><span class="provider-badge">{{ session.peerTransitProvider }}</span></td>
                  <td>AS{{ session.localAsn }} / AS{{ session.peerAsn }}</td>
                  <td>{{ session.bfdIntervalMs }}ms</td>
                  <td>
                    <span>Loss: {{ (session.packetLossPct * 100).toFixed(1) }}% | {{ session.jitterMs }}ms</span>
                  </td>
                  <td>
                    <span class="status-pill" [class.status-up]="session.sessionState === 'UP'"
                          [class.status-poisoned]="session.sessionState === 'POISONED'">
                      {{ session.sessionState }}
                    </span>
                  </td>
                  <td class="actions-cell">
                    @if (session.sessionState === 'UP') {
                      <button class="failover-btn" (click)="simulateFailover(session)" data-testid="simulate-failover-btn">
                        Simulate Drop
                      </button>
                    } @else {
                      <button class="restore-btn" (click)="restoreSession(session)" data-testid="restore-session-btn">
                        Restore
                      </button>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="events-section">
        <h2>Recent Anycast Route Events</h2>
        <div class="events-list" data-testid="events-list">
          @for (evt of events(); track evt.eventId) {
            <div class="event-card" [attr.data-testid]="'event-card-' + evt.eventId">
              <div class="evt-header">
                <span class="action-badge" [class.action-withdrawn]="evt.actionTaken === 'ROUTE_WITHDRAWN'">
                  {{ evt.actionTaken }}
                </span>
                <span class="latency-badge">{{ evt.triggerLatencyMs }}ms</span>
              </div>
              <div class="evt-body">
                <p><strong>PoP:</strong> {{ evt.edgePopLocation }}</p>
                <p class="reason">{{ evt.triggerReason }}</p>
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
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; }
    .provider-badge { background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 11px; font-weight: 600; color: #334155; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-up { background: #dcfce7; color: #15803d; }
    .status-poisoned { background: #fee2e2; color: #b91c1c; }
    .failover-btn { background: #dc2626; color: #ffffff; border: none; padding: 6px 10px; border-radius: 4px; font-size: 12px; font-weight: 500; cursor: pointer; }
    .restore-btn { background: #16a34a; color: #ffffff; border: none; padding: 6px 10px; border-radius: 4px; font-size: 12px; font-weight: 500; cursor: pointer; }
    .events-list { display: flex; flex-direction: column; gap: 10px; margin-top: 14px; }
    .event-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 14px; }
    .evt-header { display: flex; justify-content: space-between; margin-bottom: 6px; }
    .action-badge { padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 700; background: #e0e7ff; color: #3730a3; }
    .action-withdrawn { background: #fee2e2; color: #b91c1c; }
    .latency-badge { background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 11px; font-weight: 600; color: #475569; }
    .evt-body p { margin: 4px 0; font-size: 12px; }
    .reason { color: #64748b; font-style: italic; }
  `]
})
export class BgpBfdCockpitComponent {
  totalSessions = signal(3);
  healthySessions = signal(3);
  healthIndex = signal(100.0);
  failoverTarget = signal(800.0);

  sessions = signal<BgpBfdSession[]>([
    {
      sessionId: 'BFD-FRA-01',
      edgePopLocation: 'FRA-01 (Frankfurt)',
      peerTransitProvider: 'TELIA_ARELION',
      localAsn: 64512,
      peerAsn: 1299,
      bfdIntervalMs: 300.0,
      packetLossPct: 0.02,
      jitterMs: 1.2,
      sessionState: 'UP'
    },
    {
      sessionId: 'BFD-IAD-02',
      edgePopLocation: 'IAD-02 (Ashburn)',
      peerTransitProvider: 'LEVEL3_LUMEN',
      localAsn: 64512,
      peerAsn: 3356,
      bfdIntervalMs: 300.0,
      packetLossPct: 0.01,
      jitterMs: 0.9,
      sessionState: 'UP'
    },
    {
      sessionId: 'BFD-NRT-01',
      edgePopLocation: 'NRT-01 (Tokyo)',
      peerTransitProvider: 'AWS_DIRECT_CONNECT',
      localAsn: 64512,
      peerAsn: 16509,
      bfdIntervalMs: 300.0,
      packetLossPct: 0.04,
      jitterMs: 2.1,
      sessionState: 'UP'
    }
  ]);

  events = signal<AnycastRouteEvent[]>([
    {
      eventId: 'EVT-001',
      edgePopLocation: 'FRA-01',
      actionTaken: 'ROUTE_ANNOUNCED',
      triggerLatencyMs: 12.4,
      triggerReason: 'BGP Anycast prefix /24 announced globally across tier-1 carriers'
    }
  ]);

  simulateFailover(session: BgpBfdSession): void {
    this.sessions.update(list =>
      list.map(s => s.sessionId === session.sessionId ? { ...s, sessionState: 'POISONED' } : s)
    );
    this.healthySessions.update(v => v - 1);
    this.healthIndex.set(66.7);

    const newEvt: AnycastRouteEvent = {
      eventId: `EVT-${Math.floor(Math.random() * 900 + 100)}`,
      edgePopLocation: session.edgePopLocation,
      actionTaken: 'ROUTE_WITHDRAWN',
      triggerLatencyMs: 420.0,
      triggerReason: 'Simulated fiber link degradation; Anycast route withdrawn in 420ms'
    };
    this.events.update(list => [newEvt, ...list]);
  }

  restoreSession(session: BgpBfdSession): void {
    this.sessions.update(list =>
      list.map(s => s.sessionId === session.sessionId ? { ...s, sessionState: 'UP' } : s)
    );
    this.healthySessions.update(v => v + 1);
    this.healthIndex.set(100.0);

    const restoreEvt: AnycastRouteEvent = {
      eventId: `EVT-${Math.floor(Math.random() * 900 + 100)}`,
      edgePopLocation: session.edgePopLocation,
      actionTaken: 'ROUTE_ANNOUNCED',
      triggerLatencyMs: 180.0,
      triggerReason: 'BFD link stabilized; Anycast route re-announced'
    };
    this.events.update(list => [restoreEvt, ...list]);
  }
}
