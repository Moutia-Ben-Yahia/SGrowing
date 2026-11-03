import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface SttrPayment {
  paymentId: string;
  source: string;
  recipient: string;
  category: string;
  amountEur: number;
  recipientRate: number;
  subjectToSttr: boolean;
  withholdingTaxEur: number;
}

export interface SttrEvaluation {
  paymentId: string;
  recipientRate: number;
  topUpPct: number;
  withholdingTaxEur: number;
  netRemittanceEur: number;
  status: string;
  certificateIssued: boolean;
}

@Component({
  selector: 'app-sttr-treaty-tax-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="sttr-treaty-tax-cockpit">
      <header class="cockpit-header">
        <div class="header-titles">
          <h1>Horizon 189: Sovereign Pillar Two Subject to Tax Rule (STTR) & Treaty Ledger</h1>
          <p class="subtitle">OECD 9.0% Nominal Rate Treaty Clearance, Intragroup Royalty/Interest Withholding & MLI Certificates</p>
        </div>
        <div class="header-badges">
          <span class="badge badge-accent" data-testid="payments-count">Audited Payments: {{ payments().length }}</span>
          <span class="badge badge-success">OECD STTR Rate: 9.0%</span>
        </div>
      </header>

      <div class="cockpit-grid">
        <!-- Payments List -->
        <section class="card payments-card">
          <h2>Intragroup Cross-Border Payments</h2>
          <div class="payments-list">
            @for (p of payments(); track p.paymentId) {
              <div 
                class="pay-card" 
                [class.selected]="selectedPayment()?.paymentId === p.paymentId"
                (click)="selectPayment(p)"
                [attr.data-testid]="'payment-row-' + p.paymentId">
                <div class="pay-top">
                  <h3>{{ p.source }} &rarr; {{ p.recipient }} ({{ p.category }})</h3>
                  <span class="status-pill" [class.subject]="p.subjectToSttr" [class.exempt]="!p.subjectToSttr">
                    {{ p.subjectToSttr ? 'STTR Withholding Required' : 'Exempt (Rate &ge; 9%)' }}
                  </span>
                </div>
                <div class="pay-meta">
                  <span>Gross: <strong>€{{ (p.amountEur / 1000000).toFixed(2) }}M</strong></span>
                  <span>Recipient Tax: <strong>{{ p.recipientRate }}%</strong></span>
                  <span>Withholding: <strong>€{{ (p.withholdingTaxEur / 1000).toFixed(0) }}k</strong></span>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- STTR Payment Assessor -->
        <section class="card assessor-card">
          <h2>STTR Treaty Rule Assessor</h2>
          @if (selectedPayment(); as p) {
            <div class="assess-panel">
              <div class="selected-header">
                <h3>Evaluating: {{ p.paymentId }}</h3>
                <p>{{ p.category }} from {{ p.source }} to {{ p.recipient }}</p>
              </div>

              <div class="input-group">
                <label for="rec-rate">Recipient Nominal Corporate Tax Rate (%):</label>
                <input 
                  id="rec-rate" 
                  type="number" 
                  step="0.5" 
                  [(ngModel)]="recipientRate" 
                  data-testid="recipient-rate-input" />
              </div>

              <button 
                type="button" 
                class="btn-primary" 
                (click)="assessPayment()"
                data-testid="assess-payment-btn">
                Run STTR Assessment & Top-Up Deduction
              </button>

              @if (activeEvaluation(); as ev) {
                <div class="eval-result" data-testid="sttr-evaluation-box">
                  <div class="eval-header">
                    <span>Evaluation Status: {{ ev.status }}</span>
                    <span class="rate-diff">Top-Up Rate: {{ ev.topUpPct }}%</span>
                  </div>
                  <div class="metric-row" data-testid="top-up-tax-amount">
                    <span>Top-Up Withholding Tax (Payable to Source):</span>
                    <strong class="text-danger">€{{ (ev.withholdingTaxEur / 1000).toFixed(0) }}k</strong>
                  </div>
                  <div class="metric-row">
                    <span>Net Cash Remittance to Recipient:</span>
                    <strong class="text-success">€{{ (ev.netRemittanceEur / 1000000).toFixed(2) }}M</strong>
                  </div>

                  <button 
                    type="button" 
                    class="btn-secondary" 
                    (click)="issueCertificate()"
                    data-testid="issue-cert-btn">
                    {{ ev.certificateIssued ? 'Cryptographic Certificate Issued (SHA-256)' : 'Issue OECD Treaty Clearance Certificate' }}
                  </button>
                </div>
              }
            </div>
          }
        </section>

        <!-- OECD Treaty Rules -->
        <section class="card treaty-card">
          <h2>STTR Article Reference</h2>
          <div class="treaty-specs">
            <div class="spec-item">
              <h4>Article 1 (Scope of Covered Payments)</h4>
              <p>Applies to royalties, interest, and intra-group services between connected persons.</p>
            </div>
            <div class="spec-item">
              <h4>Article 2 (Minimum 9.0% Threshold)</h4>
              <p>Source jurisdiction retains top-up taxing right up to 9% if nominal rate is below 9%.</p>
            </div>
          </div>
        </section>
      </div>
    </div>
  `,
  styles: [`
    .cockpit-container {
      padding: 1.5rem;
      background: #0f172a;
      color: #f8fafc;
      min-height: 100vh;
      font-family: system-ui, -apple-system, sans-serif;
    }
    .cockpit-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 2rem;
      border-bottom: 1px solid #334155;
      padding-bottom: 1rem;
    }
    .cockpit-header h1 { margin: 0; font-size: 1.5rem; color: #38bdf8; }
    .subtitle { margin: 0.25rem 0 0 0; color: #94a3b8; font-size: 0.9rem; }
    .header-badges { display: flex; gap: 0.75rem; }
    .badge {
      padding: 0.35rem 0.75rem;
      border-radius: 9999px;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .badge-accent { background: #0369a1; color: #e0f2fe; }
    .badge-success { background: #15803d; color: #dcfce7; }
    .cockpit-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(340px, 1fr));
      gap: 1.5rem;
    }
    .card {
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 0.75rem;
      padding: 1.25rem;
    }
    .card h2 {
      margin-top: 0;
      font-size: 1.15rem;
      color: #e2e8f0;
      border-bottom: 1px solid #334155;
      padding-bottom: 0.5rem;
    }
    .payments-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .pay-card {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .pay-card:hover, .pay-card.selected {
      border-color: #38bdf8;
      background: #1e293b;
    }
    .pay-top { display: flex; justify-content: space-between; align-items: center; }
    .pay-top h3 { margin: 0; font-size: 0.95rem; color: #f8fafc; }
    .status-pill { font-size: 0.75rem; padding: 0.2rem 0.4rem; border-radius: 0.25rem; }
    .status-pill.subject { background: #b91c1c; color: #fee2e2; }
    .status-pill.exempt { background: #15803d; color: #dcfce7; }
    .pay-meta { display: flex; gap: 1rem; font-size: 0.8rem; color: #94a3b8; margin-top: 0.35rem; }
    .assess-panel { display: flex; flex-direction: column; gap: 0.75rem; }
    .input-group { display: flex; flex-direction: column; gap: 0.25rem; }
    input {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.375rem;
      color: #f8fafc;
      padding: 0.45rem;
      font-family: inherit;
    }
    .btn-primary {
      background: #2563eb;
      color: #fff;
      border: none;
      border-radius: 0.375rem;
      padding: 0.5rem 1rem;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn-primary:hover { background: #1d4ed8; }
    .btn-secondary {
      background: #0284c7;
      color: #fff;
      border: none;
      border-radius: 0.375rem;
      padding: 0.45rem 0.85rem;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
      margin-top: 0.5rem;
    }
    .btn-secondary:hover { background: #0369a1; }
    .eval-result {
      margin-top: 1rem;
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      font-size: 0.85rem;
    }
    .eval-header { display: flex; justify-content: space-between; margin-bottom: 0.5rem; }
    .rate-diff { color: #f59e0b; font-weight: bold; }
    .metric-row { display: flex; justify-content: space-between; margin-bottom: 0.4rem; }
    .text-danger { color: #ef4444; }
    .text-success { color: #4ade80; }
    .treaty-specs { display: flex; flex-direction: column; gap: 0.75rem; font-size: 0.8rem; }
    .spec-item h4 { margin: 0 0 0.25rem 0; color: #38bdf8; }
    .spec-item p { margin: 0; color: #94a3b8; }
  `]
})
export class SttrTreatyTaxCockpitComponent {
  payments = signal<SttrPayment[]>([
    {
      paymentId: 'PAY-STTR-101',
      source: 'DE',
      recipient: 'BM',
      category: 'Royalties',
      amountEur: 5000000,
      recipientRate: 0.0,
      subjectToSttr: true,
      withholdingTaxEur: 450000
    },
    {
      paymentId: 'PAY-STTR-102',
      source: 'FR',
      recipient: 'IE',
      category: 'Technical Services',
      amountEur: 3200000,
      recipientRate: 12.5,
      subjectToSttr: false,
      withholdingTaxEur: 0
    }
  ]);

  selectedPayment = signal<SttrPayment | null>(this.payments()[0]);
  recipientRate = 0.0;
  activeEvaluation = signal<SttrEvaluation | null>(null);

  selectPayment(p: SttrPayment) {
    this.selectedPayment.set(p);
    this.recipientRate = p.recipientRate;
    this.activeEvaluation.set(null);
  }

  assessPayment() {
    const p = this.selectedPayment();
    if (!p) return;

    const isSubject = this.recipientRate < 9.0;
    const topUpPct = isSubject ? +(9.0 - this.recipientRate).toFixed(1) : 0;
    const withholdingTax = Math.round(p.amountEur * (topUpPct / 100));
    const netRemittance = p.amountEur - withholdingTax;

    this.activeEvaluation.set({
      paymentId: p.paymentId,
      recipientRate: this.recipientRate,
      topUpPct,
      withholdingTaxEur: withholdingTax,
      netRemittanceEur: netRemittance,
      status: isSubject ? 'WithholdingDeductedAndCleared' : 'ExemptNominalRateAboveThreshold',
      certificateIssued: false
    });
  }

  issueCertificate() {
    if (this.activeEvaluation()) {
      this.activeEvaluation.update(ev => ev ? { ...ev, certificateIssued: true } : null);
    }
  }
}
