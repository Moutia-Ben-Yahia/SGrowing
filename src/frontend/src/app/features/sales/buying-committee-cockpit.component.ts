import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface StakeholderMember {
  stakeholderId: string;
  fullName: string;
  roleTitle: string;
  buyingRole: string;
  influenceWeight: number;
  sentimentScore: number;
  department: string;
  currentStatus: string;
}

export interface CommitteeInterventionRecord {
  interventionId: string;
  targetStakeholderId: string;
  playbookType: string;
  assignedExecutive: string;
  expectedOutcome: string;
  status: string;
}

@Component({
  selector: 'app-buying-committee-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="buying-committee-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 141 · Enterprise B2B Sales</div>
        <h1>Buying Committee Influence Graph & Sentiment Journey</h1>
        <p class="subtitle">Multi-stakeholder coalition alignment scoring, internal graph influence mapping, and automated blocker intervention playbooks</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-consensus-score">
          <div class="metric-label">Coalition Consensus Score</div>
          <div class="metric-value">{{ consensusScore() }}%</div>
          <div class="metric-sub">Weighted sentiment across decision makers</div>
        </div>
        <div class="metric-card" data-testid="metric-total-stakeholders">
          <div class="metric-label">Total Committee Members</div>
          <div class="metric-value">{{ totalStakeholders() }}</div>
          <div class="metric-sub">Economic buyers, champions, legal & security</div>
        </div>
        <div class="metric-card" data-testid="metric-aligned-champions">
          <div class="metric-label">Aligned Champions</div>
          <div class="metric-value positive">{{ alignedChampions() }}</div>
          <div class="metric-sub">Strong internal advocates (>0.3 sentiment)</div>
        </div>
        <div class="metric-card" data-testid="metric-active-blockers">
          <div class="metric-label">Active Blockers</div>
          <div class="metric-value warning">{{ activeBlockers() }}</div>
          <div class="metric-sub">Skeptical or opposing members requiring intervention</div>
        </div>
      </section>

      <section class="stakeholder-section">
        <h2>Buying Committee Influence Graph</h2>
        <div class="table-wrapper">
          <table class="data-table" data-testid="stakeholder-table">
            <thead>
              <tr>
                <th>Stakeholder</th>
                <th>Department</th>
                <th>Buying Role</th>
                <th>Influence Weight</th>
                <th>Sentiment Score</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              @for (member of stakeholders(); track member.stakeholderId) {
                <tr [attr.data-testid]="'stakeholder-row-' + member.stakeholderId">
                  <td class="primary-cell">
                    <strong>{{ member.fullName }}</strong>
                    <span class="sub-text">{{ member.roleTitle }}</span>
                  </td>
                  <td>{{ member.department }}</td>
                  <td><span class="role-badge">{{ member.buyingRole }}</span></td>
                  <td>
                    <div class="progress-bar-container">
                      <div class="progress-bar" [style.width.%]="member.influenceWeight * 100"></div>
                      <span>{{ (member.influenceWeight * 100).toFixed(0) }}%</span>
                    </div>
                  </td>
                  <td>
                    <span [class.positive-sentiment]="member.sentimentScore > 0"
                          [class.negative-sentiment]="member.sentimentScore < 0">
                      {{ member.sentimentScore > 0 ? '+' : '' }}{{ member.sentimentScore.toFixed(2) }}
                    </span>
                  </td>
                  <td>
                    <span class="status-pill" [class.status-aligned]="member.currentStatus === 'ALIGNED'"
                          [class.status-blocked]="member.currentStatus === 'BLOCKED'">
                      {{ member.currentStatus }}
                    </span>
                  </td>
                  <td>
                    <button class="action-btn" (click)="triggerIntervention(member)" data-testid="intervention-btn">
                      Intervene
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="interventions-section">
        <h2>Active Blocker Intervention Playbooks</h2>
        <div class="interventions-grid" data-testid="interventions-grid">
          @for (item of interventions(); track item.interventionId) {
            <div class="intervention-card" [attr.data-testid]="'intervention-card-' + item.interventionId">
              <div class="int-header">
                <span class="playbook-badge">{{ item.playbookType }}</span>
                <span class="status-badge">{{ item.status }}</span>
              </div>
              <div class="int-body">
                <p><strong>Assigned Executive:</strong> {{ item.assignedExecutive }}</p>
                <p><strong>Target:</strong> Stakeholder #{{ item.targetStakeholderId }}</p>
                <p class="outcome"><strong>Expected Outcome:</strong> {{ item.expectedOutcome }}</p>
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
    .metric-value.warning { color: #dc2626; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; }
    .role-badge { display: inline-block; padding: 2px 8px; background: #f1f5f9; border-radius: 4px; font-size: 11px; font-weight: 600; color: #334155; }
    .progress-bar-container { display: flex; align-items: center; gap: 8px; }
    .progress-bar { height: 6px; background: #3b82f6; border-radius: 3px; max-width: 80px; }
    .positive-sentiment { color: #16a34a; font-weight: 600; }
    .negative-sentiment { color: #dc2626; font-weight: 600; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-aligned { background: #dcfce7; color: #15803d; }
    .status-blocked { background: #fee2e2; color: #b91c1c; }
    .action-btn { background: #2563eb; color: #ffffff; border: none; padding: 6px 12px; border-radius: 4px; font-size: 12px; font-weight: 500; cursor: pointer; }
    .interventions-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 16px; margin-top: 14px; }
    .intervention-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .int-header { display: flex; justify-content: space-between; margin-bottom: 10px; }
    .playbook-badge { background: #ede9fe; color: #5b21b6; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 4px; }
    .status-badge { background: #fef3c7; color: #92400e; font-size: 11px; font-weight: 600; padding: 2px 8px; border-radius: 4px; }
    .int-body p { margin: 4px 0; font-size: 12px; color: #334155; }
    .int-body .outcome { color: #64748b; font-style: italic; }
  `]
})
export class BuyingCommitteeCockpitComponent {
  consensusScore = signal(74.5);
  totalStakeholders = signal(4);
  alignedChampions = signal(3);
  activeBlockers = signal(1);

  stakeholders = signal<StakeholderMember[]>([
    {
      stakeholderId: 'STK-001',
      fullName: 'Dr. Marcus Vance',
      roleTitle: 'Chief Information Security Officer',
      buyingRole: 'TECHNICAL_EVALUATOR',
      influenceWeight: 0.85,
      sentimentScore: 0.65,
      department: 'InfoSec & Compliance',
      currentStatus: 'ALIGNED'
    },
    {
      stakeholderId: 'STK-002',
      fullName: 'Alicia Thorne',
      roleTitle: 'VP Procurement & Strategic Sourcing',
      buyingRole: 'PROCUREMENT_BLOCKER',
      influenceWeight: 0.90,
      sentimentScore: -0.45,
      department: 'Global Sourcing',
      currentStatus: 'BLOCKED'
    },
    {
      stakeholderId: 'STK-003',
      fullName: 'Jonathan Chen',
      roleTitle: 'Chief Revenue Officer',
      buyingRole: 'ECONOMIC_BUYER',
      influenceWeight: 0.95,
      sentimentScore: 0.88,
      department: 'Executive Leadership',
      currentStatus: 'ALIGNED'
    },
    {
      stakeholderId: 'STK-004',
      fullName: 'Sarah Jenkins',
      roleTitle: 'Director of Revenue Operations',
      buyingRole: 'CHAMPION',
      influenceWeight: 0.75,
      sentimentScore: 0.92,
      department: 'RevOps',
      currentStatus: 'ALIGNED'
    }
  ]);

  interventions = signal<CommitteeInterventionRecord[]>([
    {
      interventionId: 'INT-001',
      targetStakeholderId: 'STK-002',
      playbookType: 'EXEC_SPONSOR_ALIGNMENT',
      assignedExecutive: 'Jonathan Chen (CRO)',
      expectedOutcome: 'Unblock net-60 payment terms exception and deliver ROI impact case study',
      status: 'PENDING'
    }
  ]);

  triggerIntervention(member: StakeholderMember): void {
    const newInt: CommitteeInterventionRecord = {
      interventionId: `INT-${Math.floor(Math.random() * 900 + 100)}`,
      targetStakeholderId: member.stakeholderId,
      playbookType: 'ROI_CALCULATOR_SESSION',
      assignedExecutive: 'Jonathan Chen (CRO)',
      expectedOutcome: `Custom ROI business case review for ${member.fullName}`,
      status: 'SCHEDULED'
    };
    this.interventions.update(list => [...list, newInt]);
  }
}
