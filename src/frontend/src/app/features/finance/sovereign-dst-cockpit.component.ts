import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface DstRecordView {
  dstRecordId: string;
  jurisdictionIsoCode: string;
  entityId: string;
  fiscalQuarter: string;
  taxableInScopeRevenueUsd: number;
  applicableDstRate: number;
  dstLiabilityUsd: number;
  status: string;
}

export interface GlobeAssessmentRecord {
  assessmentId: string;
  jurisdictionIsoCode: string;
  mneGroupId: string;
  effectiveTaxRatePercentage: number;
  topUpTaxPercentage: number;
  topUpTaxLiabilityUsd: number;
  iirStatus: string;
}

@Component({
  selector: 'app-sovereign-dst-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="sovereign-dst-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 154 · Global Tax Compliance</div>
        <h1>Autonomous Sovereign DST & OECD Pillar 1/2 Minimum Tax Ledger</h1>
        <p class="subtitle">Multi-jurisdiction Digital Services Tax accruals, OECD Pillar 2 15% GloBE minimum effective tax calculation, QDMTT top-up liabilities, and statutory reporting</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-covered-jurisdictions">
          <div class="metric-label">Covered Jurisdictions</div>
          <div class="metric-value">{{ coveredJurisdictionsCount() }}</div>
          <div class="metric-sub">FR, UK, IT, ES, IN, IE statutory rules</div>
        </div>
        <div class="metric-card" data-testid="metric-total-dst-accrued">
          <div class="metric-label">Total DST Accrued</div>
          <div class="metric-value positive">\${{ (totalDstAccrued() / 1000).toFixed(0) }}k</div>
          <div class="metric-sub">Multi-state digital advertising & marketplace tax</div>
        </div>
        <div class="metric-card" data-testid="metric-total-top-up-tax">
          <div class="metric-label">Pillar 2 Top-Up Tax</div>
          <div class="metric-value positive">\${{ (totalTopUpTax() / 1000).toFixed(0) }}k</div>
          <div class="metric-sub">OECD 15% GloBE minimum tax top-up</div>
        </div>
        <div class="metric-card" data-testid="metric-avg-etr">
          <div class="metric-label">Avg Effective Tax Rate</div>
          <div class="metric-value">{{ avgEtr() }}%</div>
          <div class="metric-sub">Global blended operational tax rate</div>
        </div>
      </section>

      <section class="records-section">
        <div class="section-header">
          <h2>Digital Services Tax (DST) Accrual Records</h2>
          <button class="action-btn" (click)="computeNewDstAccrual()" data-testid="compute-dst-btn">
            + Compute DST Liability
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="dst-records-table">
            <thead>
              <tr>
                <th>Record ID</th>
                <th>Jurisdiction</th>
                <th>Entity / Quarter</th>
                <th>In-Scope Revenue</th>
                <th>DST Rate</th>
                <th>Liability</th>
                <th>Status</th>
              </tr>
            </thead>
            <tbody>
              @for (rec of records(); track rec.dstRecordId) {
                <tr [attr.data-testid]="'dst-record-row-' + rec.dstRecordId">
                  <td><code>{{ rec.dstRecordId }}</code></td>
                  <td><span class="country-pill">{{ rec.jurisdictionIsoCode }}</span></td>
                  <td class="primary-cell">
                    <strong>{{ rec.entityId }}</strong>
                    <span class="sub-text">{{ rec.fiscalQuarter }}</span>
                  </td>
                  <td>\${{ (rec.taxableInScopeRevenueUsd / 1000).toFixed(0) }}k</td>
                  <td><strong>{{ (rec.applicableDstRate * 100).toFixed(1) }}%</strong></td>
                  <td><strong>\${{ (rec.dstLiabilityUsd / 1000).toFixed(1) }}k</strong></td>
                  <td><span class="status-pill status-accrued">{{ rec.status }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="globe-section">
        <h2>OECD Pillar 2 (GloBE 15%) Assessments</h2>
        <div class="globe-grid" data-testid="assessments-grid">
          @for (a of assessments(); track a.assessmentId) {
            <div class="globe-card" [attr.data-testid]="'assessment-card-' + a.assessmentId">
              <div class="globe-header">
                <strong>{{ a.jurisdictionIsoCode }} — {{ a.mneGroupId }}</strong>
                <span class="etr-badge">{{ a.effectiveTaxRatePercentage }}% ETR</span>
              </div>
              <div class="globe-body">
                <p><strong>Top-Up Rate:</strong> {{ a.topUpTaxPercentage }}%</p>
                <p><strong>Top-Up Tax Liability:</strong> \${{ (a.topUpTaxLiabilityUsd / 1000).toFixed(1) }}k</p>
                <p><strong>Status:</strong> <span class="qdmtt-badge">{{ a.iirStatus }}</span></p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #fef3c7; color: #b45309; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .action-btn { background: #059669; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; display: block; margin-top: 2px; }
    code { background: #f8fafc; padding: 2px 6px; border-radius: 4px; font-size: 11px; }
    .country-pill { background: #e0f2fe; color: #0369a1; font-size: 11px; font-weight: 700; padding: 2px 6px; border-radius: 4px; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-accrued { background: #dcfce7; color: #15803d; }
    .globe-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .globe-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .globe-header { display: flex; justify-content: space-between; margin-bottom: 8px; }
    .etr-badge { background: #fee2e2; color: #991b1b; font-size: 11px; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .globe-body p { margin: 4px 0; font-size: 12px; }
    .qdmtt-badge { background: #e0e7ff; color: #3730a3; font-size: 11px; font-weight: 600; padding: 2px 6px; border-radius: 4px; }
  `]
})
export class SovereignDstCockpitComponent {
  coveredJurisdictionsCount = signal(3);
  totalDstAccrued = signal(1245000);
  totalTopUpTax = signal(425000);
  avgEtr = signal(14.2);

  records = signal<DstRecordView[]>([
    {
      dstRecordId: 'DST-001',
      jurisdictionIsoCode: 'FR',
      entityId: 'MNE-EU-HOLDCO',
      fiscalQuarter: '2026-Q2',
      taxableInScopeRevenueUsd: 15000000,
      applicableDstRate: 0.03,
      dstLiabilityUsd: 450000,
      status: 'ACCRUED'
    },
    {
      dstRecordId: 'DST-002',
      jurisdictionIsoCode: 'UK',
      entityId: 'MNE-UK-OPERATIONS',
      fiscalQuarter: '2026-Q2',
      taxableInScopeRevenueUsd: 22000000,
      applicableDstRate: 0.02,
      dstLiabilityUsd: 440000,
      status: 'ACCRUED'
    }
  ]);

  assessments = signal<GlobeAssessmentRecord[]>([
    {
      assessmentId: 'GLOBE-001',
      jurisdictionIsoCode: 'IE',
      mneGroupId: 'MNE-GLOBAL-CORP',
      effectiveTaxRatePercentage: 11.5,
      topUpTaxPercentage: 3.5,
      topUpTaxLiabilityUsd: 425000,
      iirStatus: 'QDMTT_PAYABLE'
    }
  ]);

  computeNewDstAccrual(): void {
    const newRecord: DstRecordView = {
      dstRecordId: `DST-00${this.records().length + 1}`,
      jurisdictionIsoCode: 'IT',
      entityId: 'MNE-IT-DIGITAL',
      fiscalQuarter: '2026-Q3',
      taxableInScopeRevenueUsd: 12000000,
      applicableDstRate: 0.03,
      dstLiabilityUsd: 360000,
      status: 'ACCRUED'
    };
    this.records.update(list => [...list, newRecord]);
    this.totalDstAccrued.update(v => v + 360000);
  }
}
