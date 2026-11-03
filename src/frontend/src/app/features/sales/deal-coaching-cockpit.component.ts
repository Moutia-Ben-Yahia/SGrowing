import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CoachingSessionItem {
  sessionId: string;
  opportunityId: string;
  accountName: string;
  salesRepName: string;
  currentAdherenceScore: number;
  objectionsHandledCount: number;
  status: string;
}

export interface ObjectionItem {
  eventId: string;
  sessionId: string;
  transcribedSnippet: string;
  category: string;
  confidenceScore: number;
  recommendedCounterTactic: string;
  evidenceProofPoint: string;
}

@Component({
  selector: 'app-deal-coaching-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="deal-coaching-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 166 · Real-Time Sales AI</div>
        <h1>Autonomous B2B Live Deal Coaching & Buyer Objections Counter-Tactics Mesh</h1>
        <p class="subtitle">Sub-200ms acoustic objection recognition, live battlecard counter-tactic recommendations, and rep compliance scoring during active executive negotiations</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-sessions">
          <div class="metric-label">Active Coached Calls</div>
          <div class="metric-value positive">{{ activeSessionsCount() }}</div>
          <div class="metric-sub">Live rep conversations in progress</div>
        </div>
        <div class="metric-card" data-testid="metric-objections-quarter">
          <div class="metric-label">Objections Handled</div>
          <div class="metric-value">{{ totalObjectionsQuarter() }}</div>
          <div class="metric-sub">Classified and successfully resolved</div>
        </div>
        <div class="metric-card" data-testid="metric-rep-adherence">
          <div class="metric-label">Rep Adherence Score</div>
          <div class="metric-value positive">{{ repAdherenceScore() }}%</div>
          <div class="metric-sub">Adoption of recommended talk tracks</div>
        </div>
        <div class="metric-card" data-testid="metric-win-rate-lift">
          <div class="metric-label">Win Rate Acceleration</div>
          <div class="metric-value positive">+{{ winRateLiftPct() }}%</div>
          <div class="metric-sub">Attributed conversion lift across pipeline</div>
        </div>
      </section>

      <section class="sessions-section">
        <div class="section-header">
          <h2>Active Live Coaching Sessions</h2>
          <button class="action-btn" (click)="startNewCoachingSession()" data-testid="start-coaching-btn">
            + Start Live Coaching Session
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="sessions-table">
            <thead>
              <tr>
                <th>Session ID</th>
                <th>Opportunity</th>
                <th>Account Name</th>
                <th>Sales Rep</th>
                <th>Adherence</th>
                <th>Objections</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (s of sessions(); track s.sessionId) {
                <tr [attr.data-testid]="'session-row-' + s.sessionId">
                  <td><code>{{ s.sessionId }}</code></td>
                  <td>{{ s.opportunityId }}</td>
                  <td class="primary-cell">
                    <strong>{{ s.accountName }}</strong>
                  </td>
                  <td>{{ s.salesRepName }}</td>
                  <td><span class="score-pill">{{ s.currentAdherenceScore }}%</span></td>
                  <td><strong>{{ s.objectionsHandledCount }}</strong></td>
                  <td><span class="status-pill status-active">{{ s.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="objections-section">
        <h2>Live Detected Objections & Recommended Counter-Tactics</h2>
        <div class="objections-grid" data-testid="objections-grid">
          @for (o of objections(); track o.eventId) {
            <div class="objection-card" [attr.data-testid]="'objection-card-' + o.eventId">
              <div class="obj-header">
                <span class="cat-badge">{{ o.category }}</span>
                <span class="conf-badge">{{ (o.confidenceScore * 100).toFixed(0) }}% Match</span>
              </div>
              <p class="transcript-quote">"{{ o.transcribedSnippet }}"</p>
              <div class="tactic-box">
                <div class="tactic-title">Recommended Counter-Tactic:</div>
                <div class="tactic-text">{{ o.recommendedCounterTactic }}</div>
                <div class="proof-text"><strong>Proof Point:</strong> {{ o.evidenceProofPoint }}</div>
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
    .badge { display: inline-block; padding: 4px 10px; background: #e0e7ff; color: #3730a3; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #1e1b4b; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .sessions-section, .objections-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #4f46e5; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #4338ca; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .score-pill { color: #059669; font-weight: 700; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-active { background: #dcfce7; color: #15803d; }
    .objections-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .objection-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .obj-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .cat-badge { background: #fef3c7; color: #92400e; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .conf-badge { background: #e0e7ff; color: #3730a3; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .transcript-quote { font-style: italic; color: #334155; font-size: 13px; margin: 8px 0; background: #f8fafc; padding: 8px; border-left: 3px solid #6366f1; border-radius: 0 4px 4px 0; }
    .tactic-box { margin-top: 10px; background: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 6px; padding: 10px; }
    .tactic-title { font-size: 11px; font-weight: 700; text-transform: uppercase; color: #166534; margin-bottom: 4px; }
    .tactic-text { font-size: 12px; color: #15803d; font-weight: 600; margin-bottom: 4px; }
    .proof-text { font-size: 11px; color: #475569; }
  `]
})
export class DealCoachingCockpitComponent {
  activeSessionsCount = signal(3);
  totalObjectionsQuarter = signal(142);
  repAdherenceScore = signal(92.8);
  winRateLiftPct = signal(31.5);

  sessions = signal<CoachingSessionItem[]>([
    {
      sessionId: 'COACH-SESS-001',
      opportunityId: 'OPP-GLOBAL-890',
      accountName: 'Vanguard Logistics International',
      salesRepName: 'Alexandra Hayes',
      currentAdherenceScore: 94.5,
      objectionsHandledCount: 3,
      status: 'IN_PROGRESS'
    }
  ]);

  objections = signal<ObjectionItem[]>([
    {
      eventId: 'OBJ-EVT-001',
      sessionId: 'COACH-SESS-001',
      transcribedSnippet: 'We love the platform but leadership has frozen all non-critical SaaS budgets for Q4.',
      category: 'PricingAndBudget',
      confidenceScore: 0.96,
      recommendedCounterTactic: 'Pivot to milestone-gated deployment with net-60 deferred cash terms.',
      evidenceProofPoint: 'Customer case study: Apex Freight saved $280k in first 90 days.'
    }
  ]);

  startNewCoachingSession(): void {
    const newSession: CoachingSessionItem = {
      sessionId: `COACH-SESS-00${this.sessions().length + 1}`,
      opportunityId: 'OPP-RETAIL-402',
      accountName: 'Starlight Financial Systems',
      salesRepName: 'Marcus Reynolds',
      currentAdherenceScore: 98.0,
      objectionsHandledCount: 1,
      status: 'IN_PROGRESS'
    };
    this.sessions.update(list => [...list, newSession]);
    this.activeSessionsCount.update(v => v + 1);
  }
}
