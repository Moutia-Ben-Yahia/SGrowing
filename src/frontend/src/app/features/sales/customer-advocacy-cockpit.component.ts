import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AdvocateItem {
  advocateId: string;
  executiveName: string;
  companyName: string;
  industryVertical: string;
  referenceCallsThisQuarter: number;
  maxCallsQuarterlyQuota: number;
  advocateSatisfactionScore: number;
  rewardPointsBalance: number;
  status: string;
}

export interface MatchItem {
  matchId: string;
  prospectCompanyName: string;
  prospectDealSizeCategory: string;
  matchedAdvocateCompany: string;
  matchCompatibilityScore: number;
  referenceCallStatus: string;
}

@Component({
  selector: 'app-customer-advocacy-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="customer-advocacy-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 161 · Customer Advocacy & Deal Acceleration</div>
        <h1>Autonomous B2B Customer Advocacy & Reference Matchmaking Mesh</h1>
        <p class="subtitle">Double-blind reference champion matchmaking, advocate fatigue prevention, tokenized reward grants, and deal win-rate attribution</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-advocates">
          <div class="metric-label">Active Advocates</div>
          <div class="metric-value positive">{{ activeAdvocatesCount() }}</div>
          <div class="metric-sub">Verified enterprise reference leaders</div>
        </div>
        <div class="metric-card" data-testid="metric-calls-completed">
          <div class="metric-label">Calls Completed (Quarter)</div>
          <div class="metric-value">{{ callsCompletedQuarter() }}</div>
          <div class="metric-sub">Double-blind buyer-to-peer dialogues</div>
        </div>
        <div class="metric-card" data-testid="metric-win-rate-lift">
          <div class="metric-label">Win Rate Lift</div>
          <div class="metric-value positive">+{{ winRateLiftPct() }}%</div>
          <div class="metric-sub">Attributed conversion acceleration</div>
        </div>
        <div class="metric-card" data-testid="metric-reward-points">
          <div class="metric-label">Reward Points Distributed</div>
          <div class="metric-value">{{ rewardPointsTotal() }}</div>
          <div class="metric-sub">Tokenized customer champion equity</div>
        </div>
      </section>

      <section class="matches-section">
        <div class="section-header">
          <h2>Active Reference Call Matchmaking Pipeline</h2>
          <button class="action-btn" (click)="matchNewReference()" data-testid="match-reference-btn">
            + Match Reference Champion
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="matches-table">
            <thead>
              <tr>
                <th>Match ID</th>
                <th>Prospect Company</th>
                <th>Deal Size</th>
                <th>Matched Reference</th>
                <th>Match Score</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (m of matches(); track m.matchId) {
                <tr [attr.data-testid]="'match-row-' + m.matchId">
                  <td><code>{{ m.matchId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ m.prospectCompanyName }}</strong>
                  </td>
                  <td><span class="tier-pill">{{ m.prospectDealSizeCategory }}</span></td>
                  <td><strong>{{ m.matchedAdvocateCompany }}</strong></td>
                  <td><span class="score-pill">{{ m.matchCompatibilityScore }}%</span></td>
                  <td><span class="status-pill status-scheduled">{{ m.referenceCallStatus }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="advocates-section">
        <h2>Advocate Champions & Fatigue Guardrails</h2>
        <div class="advocates-grid" data-testid="advocates-grid">
          @for (a of advocates(); track a.advocateId) {
            <div class="advocate-card" [attr.data-testid]="'advocate-card-' + a.advocateId">
              <div class="adv-header">
                <strong>{{ a.executiveName }}</strong>
                <span class="quota-badge">{{ a.referenceCallsThisQuarter }}/{{ a.maxCallsQuarterlyQuota }} Quota</span>
              </div>
              <div class="adv-body">
                <p><strong>Company:</strong> {{ a.companyName }} ({{ a.industryVertical }})</p>
                <p><strong>Satisfaction:</strong> {{ a.advocateSatisfactionScore }}%</p>
                <p><strong>Rewards:</strong> {{ a.rewardPointsBalance }} Points</p>
                <p><strong>Status:</strong> <span class="avail-badge">{{ a.status }}</span></p>
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
    .tier-pill { background: #f1f5f9; color: #334155; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .score-pill { color: #047857; font-weight: 700; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-scheduled { background: #dcfce7; color: #15803d; }
    .advocates-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .advocate-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .adv-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .quota-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .adv-body p { margin: 4px 0; font-size: 12px; }
    .avail-badge { background: #dcfce7; color: #15803d; font-size: 10px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
  `]
})
export class CustomerAdvocacyCockpitComponent {
  activeAdvocatesCount = signal(47);
  callsCompletedQuarter = signal(29);
  winRateLiftPct = signal(34.5);
  rewardPointsTotal = signal(17750);

  matches = signal<MatchItem[]>([
    {
      matchId: 'REF-MATCH-001',
      prospectCompanyName: 'Horizon Wealth Management',
      prospectDealSizeCategory: 'ENTERPRISE',
      matchedAdvocateCompany: 'CloudScale Financial Corp',
      matchCompatibilityScore: 96.4,
      referenceCallStatus: 'COMPLETED'
    }
  ]);

  advocates = signal<AdvocateItem[]>([
    {
      advocateId: 'ADV-001',
      executiveName: 'Katherine Rivera',
      companyName: 'CloudScale Financial Corp',
      industryVertical: 'FINTECH',
      referenceCallsThisQuarter: 1,
      maxCallsQuarterlyQuota: 2,
      advocateSatisfactionScore: 98.5,
      rewardPointsBalance: 1250,
      status: 'AVAILABLE'
    },
    {
      advocateId: 'ADV-002',
      executiveName: 'Marcus Chen',
      companyName: 'Apex Freight Global',
      industryVertical: 'LOGISTICS',
      referenceCallsThisQuarter: 2,
      maxCallsQuarterlyQuota: 2,
      advocateSatisfactionScore: 96.0,
      rewardPointsBalance: 2500,
      status: 'QUOTA_REACHED'
    }
  ]);

  matchNewReference(): void {
    const newMatch: MatchItem = {
      matchId: `REF-MATCH-00${this.matches().length + 1}`,
      prospectCompanyName: 'Starlight Media Network',
      prospectDealSizeCategory: 'STRATEGIC_GLOBAL',
      matchedAdvocateCompany: 'CloudScale Financial Corp',
      matchCompatibilityScore: 95.2,
      referenceCallStatus: 'SCHEDULED'
    };
    this.matches.update(list => [...list, newMatch]);
  }
}
