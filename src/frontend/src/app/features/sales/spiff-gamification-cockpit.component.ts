import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CampaignItem {
  campaignId: string;
  campaignName: string;
  rewardType: string;
  rewardAmountUsd: number;
  totalBudgetUsd: number;
  remainingBudgetUsd: number;
  isActive: boolean;
}

export interface EscrowItem {
  escrowId: string;
  campaignId: string;
  dealId: string;
  salesRepName: string;
  amountUsd: number;
  status: string;
}

export interface LeaderboardRep {
  repId: string;
  salesRepName: string;
  dealsClosed: number;
  totalEarnedUsd: number;
  tierBadge: string;
  rank: number;
}

@Component({
  selector: 'app-spiff-gamification-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="spiff-gamification-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 171 · Sales Incentives & Real-Time Gamification</div>
        <h1>Autonomous Sales Performance SPIFF & Real-Time Deal Gamification Escrow</h1>
        <p class="subtitle">Sub-second deal closing bounty escrow allocation, real-time leaderboard acceleration, and transparent milestone rewards</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-active-campaigns">
          <div class="metric-label">Active SPIFF Campaigns</div>
          <div class="metric-value positive">{{ activeCampaignsCount() }}</div>
          <div class="metric-sub">Targeted enterprise revenue sprint bounties</div>
        </div>
        <div class="metric-card" data-testid="metric-escrow-funded">
          <div class="metric-label">Total Escrow Funded</div>
          <div class="metric-value positive">\${{ (totalEscrowFunded() / 1000).toFixed(0) }}k</div>
          <div class="metric-sub">Dedicated prize pool reserves</div>
        </div>
        <div class="metric-card" data-testid="metric-spiffs-paid">
          <div class="metric-label">Bounties Paid Today</div>
          <div class="metric-value">\${{ (spiffsPaidToday() / 1000).toFixed(1) }}k</div>
          <div class="metric-sub">Instant conditional rep disbursements</div>
        </div>
        <div class="metric-card" data-testid="metric-active-reps">
          <div class="metric-label">Ranked Closers</div>
          <div class="metric-value">{{ rankedRepsCount() }}</div>
          <div class="metric-sub">Active leaderboard competitors</div>
        </div>
      </section>

      <section class="campaigns-section">
        <div class="section-header">
          <h2>Live SPIFF Incentive Campaigns</h2>
          <button class="action-btn" (click)="launchNewSpiffCampaign()" data-testid="launch-campaign-btn">
            + Launch SPIFF Campaign
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="campaigns-table">
            <thead>
              <tr>
                <th>Campaign ID</th>
                <th>Campaign Name</th>
                <th>Reward Type</th>
                <th>Bounty / Deal</th>
                <th>Total Escrow</th>
                <th>Remaining</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (c of campaigns(); track c.campaignId) {
                <tr [attr.data-testid]="'campaign-row-' + c.campaignId">
                  <td><code>{{ c.campaignId }}</code></td>
                  <td class="primary-cell">
                    <strong>{{ c.campaignName }}</strong>
                  </td>
                  <td><span class="type-badge">{{ c.rewardType }}</span></td>
                  <td><strong>\${{ c.rewardAmountUsd.toLocaleString() }}</strong></td>
                  <td>\${{ (c.totalBudgetUsd / 1000).toFixed(0) }}k</td>
                  <td><span class="rem-pill">\${{ (c.remainingBudgetUsd / 1000).toFixed(0) }}k</span></td>
                  <td><span class="status-pill status-active">ACTIVE</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="leaderboard-section">
        <h2>Live Sales Closer Leaderboard</h2>
        <div class="leaderboard-grid" data-testid="leaderboard-grid">
          @for (r of leaderboard(); track r.repId) {
            <div class="rep-card" [attr.data-testid]="'rep-card-' + r.repId">
              <div class="r-header">
                <span class="rank-badge">#{{ r.rank }}</span>
                <strong>{{ r.salesRepName }}</strong>
                <span class="tier-pill">{{ r.tierBadge }}</span>
              </div>
              <div class="r-body">
                <p><strong>Deals Closed:</strong> {{ r.dealsClosed }} enterprise accounts</p>
                <p><strong>Total SPIFF Earned:</strong> <span class="cash-val">+\${{ r.totalEarnedUsd.toLocaleString() }}</span></p>
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
    .badge { display: inline-block; padding: 4px 10px; background: #fef3c7; color: #b45309; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #78350f; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .campaigns-section, .leaderboard-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #d97706; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #b45309; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .type-badge { background: #fef9c3; color: #854d0e; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .rem-pill { color: #059669; font-weight: 700; }
    .status-pill { padding: 3px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-active { background: #dcfce7; color: #15803d; }
    .leaderboard-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-top: 14px; }
    .rep-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .r-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; }
    .rank-badge { background: #f59e0b; color: #ffffff; font-weight: 800; font-size: 12px; padding: 2px 8px; border-radius: 9999px; }
    .tier-pill { background: #f3e8ff; color: #6b21a8; font-size: 10px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .r-body p { margin: 4px 0; font-size: 12px; }
    .cash-val { color: #059669; font-weight: 800; }
  `]
})
export class SpiffGamificationCockpitComponent {
  activeCampaignsCount = signal(1);
  totalEscrowFunded = signal(100000);
  spiffsPaidToday = signal(12500);
  rankedRepsCount = signal(3);

  campaigns = signal<CampaignItem[]>([
    {
      campaignId: 'SPIFF-CAMP-001',
      campaignName: 'Q4 Strategic Enterprise Acceleration Rush',
      rewardType: 'CashBonus',
      rewardAmountUsd: 2500,
      totalBudgetUsd: 100000,
      remainingBudgetUsd: 87500,
      isActive: true
    }
  ]);

  leaderboard = signal<LeaderboardRep[]>([
    {
      repId: 'REP-01',
      salesRepName: 'Alexandra Hayes',
      dealsClosed: 8,
      totalEarnedUsd: 20000,
      tierBadge: 'DIAMOND_CHAMPION',
      rank: 1
    },
    {
      repId: 'REP-02',
      salesRepName: 'Marcus Chen',
      dealsClosed: 6,
      totalEarnedUsd: 15000,
      tierBadge: 'PLATINUM_CLOSER',
      rank: 2
    }
  ]);

  launchNewSpiffCampaign(): void {
    const newCamp: CampaignItem = {
      campaignId: `SPIFF-CAMP-00${this.campaigns().length + 1}`,
      campaignName: 'Q1 AI Agent Enterprise Tier Rush',
      rewardType: 'CashBonus',
      rewardAmountUsd: 3500,
      totalBudgetUsd: 75000,
      remainingBudgetUsd: 75000,
      isActive: true
    };
    this.campaigns.update(list => [...list, newCamp]);
    this.activeCampaignsCount.update(v => v + 1);
  }
}
