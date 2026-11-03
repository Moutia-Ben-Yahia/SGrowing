import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface QualifyingRdProject {
  projectId: string;
  projectName: string;
  technicalHypothesis: string;
  meetsPermissiblePurpose: boolean;
  meetsTechnologicalInNature: boolean;
  meetsEliminationOfUncertainty: boolean;
  meetsProcessOfExperimentation: boolean;
  qualifiedWagesUsd: number;
  qualifiedCloudComputeUsd: number;
  totalQreUsd: number;
  fourPartTestStatus: string;
}

export interface Form6765TaxCreditClaim {
  claimId: string;
  taxYear: string;
  totalQreUsd: number;
  baseAmountUsd: number;
  ascCreditCalculatedUsd: number;
  payrollTaxOffsetEligibleUsd: number;
  auditBinderHash: string;
}

@Component({
  selector: 'app-tax-rd-credit-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="tax-rd-credit-cockpit">
      <header class="cockpit-header">
        <div class="badge">Horizon 149 · Sovereign Corporate Tax Strategy</div>
        <h1>Corporate R&D Tax Credit (Form 6765) & Patent Box Ledger</h1>
        <p class="subtitle">Automated Section 41 four-part test qualification, sprint engineering wage and cloud compute QRE ingestion, and audit-ready tax binder generation</p>
      </header>

      <section class="metrics-grid">
        <div class="metric-card" data-testid="metric-qualified-projects">
          <div class="metric-label">Qualified R&D Projects</div>
          <div class="metric-value positive">{{ qualifiedProjectsCount() }}</div>
          <div class="metric-sub">100% compliant with 4-part statutory test</div>
        </div>
        <div class="metric-card" data-testid="metric-company-qre">
          <div class="metric-label">Total Company QRE</div>
          <div class="metric-value">\${{ (totalQre() / 1000000).toFixed(2) }}M</div>
          <div class="metric-sub">Engineering wages + Cloud GPU compute</div>
        </div>
        <div class="metric-card" data-testid="metric-estimated-credit">
          <div class="metric-label">Estimated Tax Credit</div>
          <div class="metric-value positive">\${{ totalTaxCredit().toLocaleString() }}</div>
          <div class="metric-sub">Section 41 ASC 14% credit calculation</div>
        </div>
        <div class="metric-card" data-testid="metric-payroll-offset">
          <div class="metric-label">Payroll Tax Offset</div>
          <div class="metric-value">\${{ payrollOffset().toLocaleString() }}</div>
          <div class="metric-sub">Claimable against employer FICA tax</div>
        </div>
      </section>

      <section class="projects-section">
        <div class="section-header">
          <h2>Qualified Research Activities & Four-Part Test</h2>
          <button class="qualify-btn" (click)="qualifyNewProject()" data-testid="qualify-project-btn">
            + Qualify Project
          </button>
        </div>
        <div class="table-wrapper">
          <table class="data-table" data-testid="projects-table">
            <thead>
              <tr>
                <th>Project & Technical Uncertainty</th>
                <th>Wages QRE</th>
                <th>Cloud QRE</th>
                <th>Total QRE</th>
                <th>4-Part Status</th>
              </tr>
            </thead>
            <tbody>
              @for (proj of projects(); track proj.projectId) {
                <tr [attr.data-testid]="'project-row-' + proj.projectId">
                  <td class="primary-cell">
                    <strong>{{ proj.projectName }}</strong>
                    <span class="sub-text">{{ proj.technicalHypothesis }}</span>
                  </td>
                  <td>\${{ proj.qualifiedWagesUsd.toLocaleString() }}</td>
                  <td>\${{ proj.qualifiedCloudComputeUsd.toLocaleString() }}</td>
                  <td><strong>\${{ proj.totalQreUsd.toLocaleString() }}</strong></td>
                  <td>
                    <span class="status-pill status-qualified">{{ proj.fourPartTestStatus }}</span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <section class="claims-section">
        <h2>Executed Form 6765 Statutory Claims</h2>
        <div class="claims-grid" data-testid="claims-grid">
          @for (claim of claims(); track claim.claimId) {
            <div class="claim-card" [attr.data-testid]="'claim-card-' + claim.claimId">
              <div class="claim-header">
                <strong>Tax Year {{ claim.taxYear }}</strong>
                <code>{{ claim.auditBinderHash }}</code>
              </div>
              <div class="claim-body">
                <p><strong>Total QRE:</strong> \${{ claim.totalQreUsd.toLocaleString() }}</p>
                <p class="credit-amount"><strong>Calculated ASC Credit:</strong> \${{ claim.ascCreditCalculatedUsd.toLocaleString() }}</p>
                <p><strong>Payroll Offset:</strong> \${{ claim.payrollTaxOffsetEligibleUsd.toLocaleString() }}</p>
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
    .badge { display: inline-block; padding: 4px 10px; border-radius: 9999px; background: #ecfdf5; color: #047857; font-size: 12px; font-weight: 600; text-transform: uppercase; margin-bottom: 8px; }
    h1 { font-size: 26px; font-weight: 700; margin: 0 0 6px 0; color: #0f172a; }
    .subtitle { font-size: 14px; color: #64748b; margin: 0; }
    .metrics-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 16px; margin-bottom: 28px; }
    .metric-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 18px; box-shadow: 0 1px 3px rgba(0,0,0,0.05); }
    .metric-label { font-size: 13px; font-weight: 500; color: #64748b; margin-bottom: 6px; }
    .metric-value { font-size: 28px; font-weight: 700; color: #0f172a; }
    .metric-value.positive { color: #16a34a; }
    .metric-sub { font-size: 12px; color: #94a3b8; margin-top: 4px; }
    .section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 14px; }
    .qualify-btn { background: #059669; color: #ffffff; border: none; padding: 8px 14px; border-radius: 4px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .table-wrapper { overflow-x: auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; }
    .data-table { width: 100%; border-collapse: collapse; text-align: left; font-size: 13px; }
    .data-table th, .data-table td { padding: 12px 16px; border-bottom: 1px solid #e2e8f0; }
    .data-table th { background: #f8fafc; color: #475569; font-weight: 600; }
    .primary-cell strong { display: block; color: #0f172a; }
    .sub-text { font-size: 11px; color: #64748b; }
    .status-pill { padding: 2px 8px; border-radius: 9999px; font-size: 11px; font-weight: 600; }
    .status-qualified { background: #dcfce7; color: #15803d; }
    .claims-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-top: 14px; }
    .claim-card { background: #ffffff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; }
    .claim-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }
    code { background: #f1f5f9; padding: 2px 6px; border-radius: 4px; font-size: 11px; color: #0f172a; }
    .claim-body p { margin: 4px 0; font-size: 12px; }
    .credit-amount { color: #16a34a; font-weight: 700; font-size: 13px; }
  `]
})
export class TaxRdCreditCockpitComponent {
  qualifiedProjectsCount = signal(2);
  totalQre = signal(3050000);
  totalTaxCredit = signal(305000);
  payrollOffset = signal(305000);

  projects = signal<QualifyingRdProject[]>([
    {
      projectId: 'PRJ-RD-001',
      projectName: 'Real-Time Telephony Dual-Channel Voice Beamformer',
      technicalHypothesis: 'Synthesizing sub-10ms acoustic filter removing non-stationary noise',
      meetsPermissiblePurpose: true,
      meetsTechnologicalInNature: true,
      meetsEliminationOfUncertainty: true,
      meetsProcessOfExperimentation: true,
      qualifiedWagesUsd: 1450000,
      qualifiedCloudComputeUsd: 380000,
      totalQreUsd: 1830000,
      fourPartTestStatus: 'FULLY_QUALIFIED'
    },
    {
      projectId: 'PRJ-RD-002',
      projectName: 'Zero-Trust eBPF Kernel Packet Inspection & Micro-Segmenter',
      technicalHypothesis: 'Eliminating socket lookup contention under 100Gbps line rates',
      meetsPermissiblePurpose: true,
      meetsTechnologicalInNature: true,
      meetsEliminationOfUncertainty: true,
      meetsProcessOfExperimentation: true,
      qualifiedWagesUsd: 980000,
      qualifiedCloudComputeUsd: 240000,
      totalQreUsd: 1220000,
      fourPartTestStatus: 'FULLY_QUALIFIED'
    }
  ]);

  claims = signal<Form6765TaxCreditClaim[]>([
    {
      claimId: 'CLM-6765-2025',
      taxYear: '2025',
      totalQreUsd: 3050000,
      baseAmountUsd: 1200000,
      ascCreditCalculatedUsd: 259000,
      payrollTaxOffsetEligibleUsd: 250000,
      auditBinderHash: 'BINDER-SHA256-48BA001C92D'
    }
  ]);

  qualifyNewProject(): void {
    const newProj: QualifyingRdProject = {
      projectId: `PRJ-RD-${Math.floor(Math.random() * 900 + 100)}`,
      projectName: 'WebAssembly Edge CDN Sub-5ms Snapshot Restore',
      technicalHypothesis: 'Memory page mapping for instantaneous warm cold-starts',
      meetsPermissiblePurpose: true,
      meetsTechnologicalInNature: true,
      meetsEliminationOfUncertainty: true,
      meetsProcessOfExperimentation: true,
      qualifiedWagesUsd: 750000,
      qualifiedCloudComputeUsd: 180000,
      totalQreUsd: 930000,
      fourPartTestStatus: 'FULLY_QUALIFIED'
    };
    this.projects.update(list => [...list, newProj]);
    this.qualifiedProjectsCount.update(v => v + 1);
    this.totalQre.update(v => v + 930000);
  }
}
