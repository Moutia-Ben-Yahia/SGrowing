import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface AutopsyItem {
  autopsyId: string;
  opportunityId: string;
  accountName: string;
  dealValueUsd: number;
  salesRepName: string;
  primaryLossCategory: string;
  confidenceScore: number;
}

export interface PlaybookItem {
  playbookId: string;
  targetOpportunityId: string;
  recoveryStrategy: string;
  cooldownDays: number;
  revivalProbability: number;
}

@Component({
  selector: 'app-deal-autopsy-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="deal-autopsy-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 176 · Sales Forensic Intelligence</div>
        <h1>Autonomous B2B Win-Rate Deal Autopsy & Lost Opportunity Signal Forensic Mesh</h1>
        <p class="subtitle">Transcript signal forensics on closed-lost opportunities, friction clustering, and automated pipeline recovery playbooks</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-autopsies-count">
          <div class="metric-label">Autopsies Completed</div>
          <div class="metric-value positive">{{ autopsiesCount() }}</div>
          <div class="metric-sub">Analyzed closed-lost deals</div>
        </div>
        <div class="metric-card" data-testid="metric-lost-pipeline">
          <div class="metric-label">Lost Pipeline Analyzed</div>
          <div class="metric-value">\${{ (lostPipelineUsd() / 1000000).toFixed(1) }}M</div>
          <div class="metric-sub">Forensically classified deal volume</div>
        </div>
        <div class="metric-card" data-testid="metric-recovery-potential">
          <div class="metric-label">Pipeline Revival Potential</div>
          <div class="metric-value positive">+{{ recoveryPotentialPct() }}%</div>
          <div class="metric-sub">Addressable via recovery playbooks</div>
        </div>
        <div class="metric-card" data-testid="metric-active-playbooks">
          <div class="metric-label">Active Playbooks</div>
          <div class="metric-value">{{ activePlaybooksCount() }}</div>
          <div class="metric-sub">Automated re-engagement sequences</div>
        </div>
      </section>

      <section class="autopsies-section">
        <div class="section-header">
          <h2>Closed-Lost Deal Autopsies</h2>
          <button class="action-btn" (click)="analyzeNewLostDeal()" data-testid="analyze-deal-btn">
            + Run Deal Autopsy
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="autopsies-table">
            <thead>
              <tr>
                <th>Autopsy ID</th>
                <th>Opportunity</th>
                <th>Account Name</th>
                <th>Deal Value</th>
                <th>Sales Rep</th>
                <th>Primary Loss Category</th>
                <th>Confidence</th>
              </tr>
            </thead>
            <tbody>
              @for (a of autopsies(); track a.autopsyId) {
                <tr [attr.data-testid]="'autopsy-row-' + a.autopsyId">
                  <td><code>{{ a.autopsyId }}</code></td>
                  <td>{{ a.opportunityId }}</td>
                  <td class="primary-cell">
                    <strong>{{ a.accountName }}</strong>
                  </td>
                  <td>\${{ a.dealValueUsd.toLocaleString() }}</td>
                  <td>{{ a.salesRepName }}</td>
                  <td><span class="loss-badge">{{ a.primaryLossCategory }}</span></td>
                  <td><span class="conf-pill">{{ (a.confidenceScore * 100).toFixed(0) }}%</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="playbooks-section">
        <h2>Win-Rate Recovery Playbooks</h2>
        <div class="playbooks-grid" data-testid="playbooks-grid">
          @for (p of playbooks(); track p.playbookId) {
            <div class="playbook-card" [attr.data-testid]="'playbook-card-' + p.playbookId">
              <div class="p-header">
                <strong>{{ p.targetOpportunityId }}</strong>
                <span class="revival-badge">{{ (p.revivalProbability * 100).toFixed(0) }}% Revival Prob</span>
              </div>
              <div class="p-body">
                <p><strong>Strategy:</strong> {{ p.recoveryStrategy }}</p>
                <p><strong>Cooldown Window:</strong> {{ p.cooldownDays }} days</p>
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
    .badge { display: inline-block; padding: 4px 10px; background: #fee2e2; color: #b91c1c; border-radius: 9999px; font-size: 12px; font-weight: 600; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 8px 0; color: #881337; }
    .subtitle { color: #64748b; font-size: 14px; margin: 0; max-width: 900px; line-height: 1.5; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 10px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 12px; font-weight: 600; text-transform: uppercase; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 800; color: #0f172a; margin-bottom: 4px; }
    .metric-value.positive { color: #059669; }
    .metric-sub { font-size: 12px; color: #94a3b8; }
    .autopsies-section, .playbooks-section { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; padding: 20px; margin-bottom: 24px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    h2 { font-size: 18px; font-weight: 700; margin: 0; color: #1e293b; }
    .action-btn { background: #be123c; color: #ffffff; border: none; border-radius: 6px; padding: 8px 14px; font-size: 13px; font-weight: 600; cursor: pointer; transition: background 0.15s; }
    .action-btn:hover { background: #9f1239; }
    .table-wrapper { overflow-x: auto; }
    .data-table { width: 100%; border-collapse: collapse; font-size: 13px; text-align: left; }
    .data-table th { background: #f8fafc; padding: 10px 14px; font-weight: 600; color: #475569; border-bottom: 1px solid #e2e8f0; }
    .data-table td { padding: 12px 14px; border-bottom: 1px solid #f1f5f9; vertical-align: middle; }
    .primary-cell strong { color: #0f172a; }
    .loss-badge { background: #fee2e2; color: #991b1b; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .conf-pill { color: #059669; font-weight: 700; }
    .playbooks-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin-top: 14px; }
    .playbook-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .p-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .revival-badge { background: #ecfdf5; color: #047857; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .p-body p { margin: 4px 0; font-size: 12px; }
  `]
})
export class DealAutopsyCockpitComponent {
  autopsiesCount = signal(48);
  lostPipelineUsd = signal(6240000);
  recoveryPotentialPct = signal(34.2);
  activePlaybooksCount = signal(1);

  autopsies = signal<AutopsyItem[]>([
    {
      autopsyId: 'AUTO-001',
      opportunityId: 'OPP-FIN-8812',
      accountName: 'Metropolitan Capital Union',
      dealValueUsd: 145000,
      salesRepName: 'Alexandra Hayes',
      primaryLossCategory: 'PricingAndTcoMisalignment',
      confidenceScore: 0.94
    }
  ]);

  playbooks = signal<PlaybookItem[]>([
    {
      playbookId: 'PLAY-001',
      targetOpportunityId: 'OPP-FIN-8812',
      recoveryStrategy: 'Milestone-Gated Payback Guarantee & Executive Re-briefing',
      cooldownDays: 45,
      revivalProbability: 0.68
    }
  ]);

  analyzeNewLostDeal(): void {
    const newAutopsy: AutopsyItem = {
      autopsyId: `AUTO-00${this.autopsies().length + 1}`,
      opportunityId: 'OPP-HLTH-902',
      accountName: 'OmniHealth BioSystems',
      dealValueUsd: 280000,
      salesRepName: 'Marcus Chen',
      primaryLossCategory: 'SecurityAndComplianceBlocker',
      confidenceScore: 0.96
    };
    this.autopsies.update(list => [...list, newAutopsy]);
    this.autopsiesCount.update(v => v + 1);
  }
}
