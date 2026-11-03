import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ClauseRedlineRecord {
  clauseId: string;
  contractReference: string;
  clauseCategory: string;
  proposedRedlineText: string;
  playbookStandardText: string;
  recommendedCompromiseText: string;
  deviationRiskScore: number;
  riskClassification: string;
  status: string;
}

export interface ContractDeviationApproval {
  approvalId: string;
  clauseId: string;
  approvedByLegalCounsel: string;
  businessJustification: string;
  cryptographicAuditSeal: string;
}

@Component({
  selector: 'app-contract-redline-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="contract-redline-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 146 · Enterprise Legal Ops</div>
        <h1>B2B Contract Redline Auto-Negotiator & Deviation Ledger</h1>
        <p class="subtitle">Autonomous semantic vector redline comparison, clause deviation risk scoring, fallback compromise generation, and audit-sealed legal approvals</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-clauses-reviewed">
          <div class="metric-label">Clauses Reviewed</div>
          <div class="metric-value">{{ totalReviewed() }}</div>
          <div class="metric-sub">Active contract redline clauses analyzed</div>
        </div>
        <div class="metric-card" data-testid="metric-pending-redlines">
          <div class="metric-label">Pending Redlines</div>
          <div class="metric-value warning">{{ pendingCount() }}</div>
          <div class="metric-sub">Requires compromise or legal sign-off</div>
        </div>
        <div class="metric-card" data-testid="metric-compromises-offered">
          <div class="metric-label">Acceptable Compromises</div>
          <div class="metric-value positive">{{ compromisesCount() }}</div>
          <div class="metric-sub">Balanced fallback positions accepted</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-risk">
          <div class="metric-label">Average Deviation Risk</div>
          <div class="metric-value">{{ avgRiskScore() }} / 100</div>
          <div class="metric-sub">Portfolio contract deviation exposure</div>
        </div>
      </section>

      <section class="redlines-section">
        <h2>Active Contract Redlines & Auto-Negotiation</h2>
        <div class="table-wrapper">
          <table class="data-table" data-testid="redlines-table">
            <thead>
              <tr>
                <th>Category & Reference</th>
                <th>Proposed Customer Redline</th>
                <th>Recommended Compromise</th>
                <th>Risk Score</th>
                <th>Status</th>
                <th>Action</th>
              </tr>
            </thead>
            <tbody>
              @for (clause of redlines(); track clause.clauseId) {
                <tr [attr.data-testid]="'clause-row-' + clause.clauseId">
                  <td class="primary-cell">
                    <strong>{{ clause.clauseCategory }}</strong>
                    <span class="sub-text">{{ clause.contractReference }}</span>
                  </td>
                  <td class="clause-text redline-diff">{{ clause.proposedRedlineText }}</td>
                  <td class="clause-text compromise-diff">{{ clause.recommendedCompromiseText }}</td>
                  <td>
                    <span class="risk-badge" [class.risk-acceptable]="clause.riskClassification === 'ACCEPTABLE_COMPROMISE'"
                          [class.risk-escalate]="clause.riskClassification === 'UNACCEPTABLE_ESCALATE'">
                      {{ clause.deviationRiskScore }} ({{ clause.riskClassification }})
                    </span>
                  </td>
                  <td>
                    <span class="status-pill" [class.status-approved]="clause.status === 'APPROVED'"
                          [class.status-pending]="clause.status === 'PENDING_REVIEW' || clause.status === 'COMPROMISE_OFFERED'">
                      {{ clause.status }}
                    </span>
                  </td>
                  <td>
                    @if (clause.status !== 'APPROVED') {
                      <button class="approve-btn" (click)="approveClause(clause)" data-testid="approve-clause-btn">
                        Approve
                      </button>
                    } @else {
                      <span class="sealed-tag">Sealed</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="approvals-section">
        <h2>Executed Legal Deviation Approvals</h2>
        <div class="approvals-grid" data-testid="approvals-grid">
          @for (app of approvals(); track app.approvalId) {
            <div class="approval-card" [attr.data-testid]="'approval-card-' + app.approvalId">
              <div class="app-header">
                <strong>{{ app.approvedByLegalCounsel }}</strong>
                <code>{{ app.cryptographicAuditSeal }}</code>
              </div>
              <div class="app-body">
                <p><strong>Clause Ref:</strong> {{ app.clauseId }}</p>
                <p class="justification"><strong>Justification:</strong> {{ app.businessJustification }}</p>
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
    .metric-value.warning { color: #d97706; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; }
    .clause-text { font-size: 12px; max-width: 280px; }
    .redline-diff { color: #b91c1c; font-style: italic; }
    .compromise-diff { color: #15803d; }
    .risk-badge { padding: 2px 6px; border-radius: 4px; font-size: 11px; font-weight: 600; }
    .risk-acceptable { background: #fef3c7; color: #92400e; }
    .risk-escalate { background: #fee2e2; color: #991b1b; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-approved { background: #dcfce7; color: #15803d; }
    .status-pending { background: #fef3c7; color: #92400e; }
    .approve-btn { background: #16a34a; color: #ffffff; border: none; padding: 6px 12px; border-radius: 4px; font-size: 12px; font-weight: 500; cursor: pointer; }
    .sealed-tag { color: #64748b; font-size: 11px; font-weight: 600; }
    .approvals-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .approval-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .app-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
    code { background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 11px; color: #0f172a; }
    .app-body p { margin: 4px 0; font-size: 12px; }
    .justification { color: #64748b; font-style: italic; }
  `]
})
export class ContractRedlineCockpitComponent {
  totalReviewed = signal(2);
  pendingCount = signal(1);
  compromisesCount = signal(2);
  avgRiskScore = signal(40.3);

  redlines = signal<ClauseRedlineRecord[]>([
    {
      clauseId: 'RED-LOL-001',
      contractReference: 'MSA-2026-NORTHSTAR',
      clauseCategory: 'LIMITATION_OF_LIABILITY',
      proposedRedlineText: 'Vendor liability shall be unlimited for gross negligence or data breach.',
      playbookStandardText: 'Vendor liability strictly capped at preceding 12 months fees.',
      recommendedCompromiseText: 'Vendor liability capped at 2x annual contract value, excluding IP infringement.',
      deviationRiskScore: 48.5,
      riskClassification: 'ACCEPTABLE_COMPROMISE',
      status: 'COMPROMISE_OFFERED'
    },
    {
      clauseId: 'RED-IND-002',
      contractReference: 'MSA-2026-NORTHSTAR',
      clauseCategory: 'INDEMNIFICATION',
      proposedRedlineText: 'Customer shall receive full uncapped indemnity for third-party claims without reciprocal defense.',
      playbookStandardText: 'Each party mutually defends and indemnifies against IP infringement.',
      recommendedCompromiseText: 'Reciprocal indemnification for third-party direct patent claims with 30-day notice.',
      deviationRiskScore: 32.0,
      riskClassification: 'ACCEPTABLE_COMPROMISE',
      status: 'PENDING_REVIEW'
    }
  ]);

  approvals = signal<ContractDeviationApproval[]>([
    {
      approvalId: 'APP-001',
      clauseId: 'RED-LOL-001',
      approvedByLegalCounsel: 'Sarah Jenkins (Associate General Counsel)',
      businessJustification: 'Approved 2x ACV super-cap in exchange for 3-year upfront commitment',
      cryptographicAuditSeal: 'SEAL-SHA256-48C9A0F112B'
    }
  ]);

  approveClause(clause: ClauseRedlineRecord): void {
    this.redlines.update(list =>
      list.map(r => r.clauseId === clause.clauseId ? { ...r, status: 'APPROVED' } : r)
    );
    const newApproval: ContractDeviationApproval = {
      approvalId: `APP-${Math.floor(Math.random() * 900 + 100)}`,
      clauseId: clause.clauseId,
      approvedByLegalCounsel: 'Michael Chang (Chief Legal Officer)',
      businessJustification: `Executive approval granted for ${clause.clauseCategory}`,
      cryptographicAuditSeal: `SEAL-SHA256-${Math.floor(Math.random() * 900000000 + 100000000).toString(16).toUpperCase()}`
    };
    this.approvals.update(list => [...list, newApproval]);
    this.pendingCount.update(v => Math.max(0, v - 1));
  }
}
