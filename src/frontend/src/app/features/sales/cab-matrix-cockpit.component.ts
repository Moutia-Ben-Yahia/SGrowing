import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CabMemberRecord {
  memberId: string;
  executiveName: string;
  title: string;
  enterpriseAccount: string;
  arrContribution: number;
}

export interface SponsorPairingRecord {
  pairingId: string;
  memberId: string;
  memberName: string;
  enterpriseAccount: string;
  executiveSponsorId: string;
  sponsorName: string;
  strategicTheme: string;
  alignmentScore: number;
  status: string;
}

export interface StrategicThemeRecord {
  themeId: string;
  title: string;
  category: string;
  votesCount: number;
  totalScore: number;
}

@Component({
  selector: 'app-cab-matrix-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="cab-matrix-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 151 · Sales & Strategic Alliances</div>
        <h1>Autonomous B2B Customer Advisory Board & Sponsor Matrix</h1>
        <p class="subtitle">Executive sponsor pairing, strategic roadmap co-innovation voting, account retention influence, and reciprocal value scorecards</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-members">
          <div class="metric-label">Active CAB Members</div>
          <div class="metric-value positive">{{ activeMembersCount() }}</div>
          <div class="metric-sub">Tier-1 C-suite executive advisors</div>
        </div>
        <div class="metric-card" data-testid="metric-exec-sponsors">
          <div class="metric-label">Executive Sponsors</div>
          <div class="metric-value">{{ execSponsorsCount() }}</div>
          <div class="metric-sub">Internal executive pairings</div>
        </div>
        <div class="metric-card" data-testid="metric-active-pairings">
          <div class="metric-label">Active Pairings</div>
          <div class="metric-value">{{ activePairingsCount() }}</div>
          <div class="metric-sub">Bi-weekly cadence engagements</div>
        </div>
        <div class="metric-card" data-testid="metric-alignment-score">
          <div class="metric-label">Avg Alignment Score</div>
          <div class="metric-value positive">{{ alignmentScore() }}%</div>
          <div class="metric-sub">Reciprocal roadmap & account NPS</div>
        </div>
      </section>

      <section class="pairings-section">
        <div class="section-header">
          <h2>Executive Sponsor Alignments</h2>
          <button class="action-btn" (click)="alignNewSponsor()" data-testid="align-sponsor-btn">
            + Align Sponsor
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="pairings-table">
            <thead>
              <tr>
                <th>Pairing ID</th>
                <th>CAB Advisor / Account</th>
                <th>Executive Sponsor</th>
                <th>Strategic Theme</th>
                <th>Score</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (pair of pairings(); track pair.pairingId) {
                <tr [attr.data-testid]="'pairing-row-' + pair.pairingId">
                  <td><code>{{ pair.pairingId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ pair.memberName }}</strong>
                    <span class="sub-text">{{ pair.enterpriseAccount }} ({{ pair.memberId }})</span>
                  </td>
                  <td><strong>{{ pair.sponsorName }}</strong></td>
                  <td><span class="theme-badge">{{ pair.strategicTheme }}</span></td>
                  <td><span class="score-pill">{{ pair.alignmentScore }}%</span></td>
                  <td><span class="status-pill status-aligned">{{ pair.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="themes-section">
        <h2>Strategic Roadmap Co-Innovation Themes</h2>
        <div class="themes-grid" data-testid="themes-grid">
          @for (theme of themes(); track theme.themeId) {
            <div class="theme-card" [attr.data-testid]="'theme-card-' + theme.themeId">
              <div class="theme-header">
                <strong>{{ theme.title }}</strong>
                <span class="votes-badge">{{ theme.votesCount }} Votes</span>
              </div>
              <div class="theme-body">
                <p><strong>Category:</strong> {{ theme.category }}</p>
                <p><strong>Total Priority Score:</strong> {{ theme.totalScore }}</p>
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
    .sub-text { font-size: 11px; color: #64748b; display: block; margin-top: 2px; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .theme-badge { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 4px; }
    .score-pill { font-weight: 700; color: #047857; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-aligned { background: #dcfce7; color: #15803d; }
    .themes-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .theme-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .theme-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .votes-badge { background: #fef3c7; color: #b45309; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .theme-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class CabMatrixCockpitComponent {
  activeMembersCount = signal(3);
  execSponsorsCount = signal(3);
  activePairingsCount = signal(2);
  alignmentScore = signal(94.2);

  pairings = signal<SponsorPairingRecord[]>([
    {
      pairingId: 'PAIR-001',
      memberId: 'CAB-MBR-001',
      memberName: 'Sarah Jenkins (CIO)',
      enterpriseAccount: 'Global Logistics Corp',
      executiveSponsorId: 'EXEC-SPON-001',
      sponsorName: 'Elena Rostova (CTO)',
      strategicTheme: 'Autonomous AI Orchestration',
      alignmentScore: 96.5,
      status: 'ACTIVE_ALIGNED'
    },
    {
      pairingId: 'PAIR-002',
      memberId: 'CAB-MBR-002',
      memberName: 'Marcus Vance (VP Tech)',
      enterpriseAccount: 'Starlight Financial',
      executiveSponsorId: 'EXEC-SPON-002',
      sponsorName: 'David Chen (CEO)',
      strategicTheme: 'Multi-Cloud Confidential Computing',
      alignmentScore: 91.8,
      status: 'ACTIVE_ALIGNED'
    }
  ]);

  themes = signal<StrategicThemeRecord[]>([
    {
      themeId: 'THEME-001',
      title: 'Autonomous AI Multi-Agent Orchestration',
      category: 'Product Core',
      votesCount: 8,
      totalScore: 76.5
    },
    {
      themeId: 'THEME-002',
      title: 'Sovereign Digital Services & OECD Pillar 2 Tax Compliance',
      category: 'Enterprise Compliance',
      votesCount: 6,
      totalScore: 54.0
    }
  ]);

  alignNewSponsor(): void {
    const newPairing: SponsorPairingRecord = {
      pairingId: `PAIR-00${this.activePairingsCount() + 1}`,
      memberId: 'CAB-MBR-003',
      memberName: 'Carlos Mendez (VP Digital)',
      enterpriseAccount: 'Nordic Retail Group',
      executiveSponsorId: 'EXEC-SPON-003',
      sponsorName: 'Amara Okafor (VP Product)',
      strategicTheme: 'Cross-Border Real-Time Settlement',
      alignmentScore: 93.0,
      status: 'ACTIVE_ALIGNED'
    };
    this.pairings.update(list => [...list, newPairing]);
    this.activePairingsCount.update(v => v + 1);
  }
}
