import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface JurisdictionProfile {
  code: string;
  name: string;
  revenueEur: number;
  netIncomeEur: number;
  coveredTaxesEur: number;
  statutoryRate: number;
  safeHarborPass: boolean;
  safeHarborType: string;
}

export interface TopUpCalculation {
  jurisdictionCode: string;
  etr: number;
  minimumRate: number;
  topUpPct: number;
  sbieCarveoutEur: number;
  excessProfitEur: number;
  topUpTaxEur: number;
}

@Component({
  selector: 'app-globe-top-up-tax-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cockpit-container" dir="ltr" data-testid="globe-top-up-tax-cockpit">
      <header class="cockpit-header">
        <div class="header-titles">
          <h1>Horizon 184: OECD Pillar Two GloBE Top-Up Tax & Safe Harbor Ledger</h1>
          <p class="subtitle">Global Anti-Base Erosion 15% Minimum Tax, CbCR Safe Harbors & SBIE Substance Carveouts</p>
        </div>
        <div class="header-badges">
          <span class="badge badge-accent" data-testid="jurisdictions-count">Jurisdictions: {{ jurisdictions().length }}</span>
          <span class="badge badge-success">OECD Minimum Rate: 15.0%</span>
        </div>
      </header>

      <div class="cockpit-grid">
        <!-- Jurisdictions List -->
        <section class="card jurisdictions-card">
          <h2>Evaluated Tax Jurisdictions</h2>
          <div class="jurisdictions-list">
            @for (jur of jurisdictions(); track jur.code) {
              <div 
                class="jur-card" 
                [class.selected]="selectedJurisdiction()?.code === jur.code"
                (click)="selectJurisdiction(jur)"
                [attr.data-testid]="'jurisdiction-card-' + jur.code">
                <div class="jur-top">
                  <h3>{{ jur.name }} ({{ jur.code }})</h3>
                  <span class="status-pill" [class.pass]="jur.safeHarborPass" [class.fail]="!jur.safeHarborPass">
                    {{ jur.safeHarborPass ? 'Safe Harbor Pass' : 'Subject to Top-Up' }}
                  </span>
                </div>
                <div class="jur-meta">
                  <span>Net Income: <strong>€{{ (jur.netIncomeEur / 1000000).toFixed(1) }}M</strong></span>
                  <span>Covered Taxes: <strong>€{{ (jur.coveredTaxesEur / 1000000).toFixed(2) }}M</strong></span>
                </div>
                <div class="safe-harbor-type" data-testid="safe-harbor-status">
                  Test: {{ jur.safeHarborType }}
                </div>
              </div>
            }
          </div>
        </section>

        <!-- Top-Up Tax Calculator -->
        <section class="card calculator-card">
          <h2>Top-Up Tax & SBIE Substance Carveout Calculator</h2>
          @if (selectedJurisdiction(); as jur) {
            <div class="calc-panel">
              <div class="selected-info">
                <h3>Selected: {{ jur.name }} ({{ jur.code }})</h3>
                <p>Statutory Rate: {{ jur.statutoryRate }}%</p>
              </div>

              <button 
                type="button" 
                class="btn-primary" 
                (click)="calculateTopUpTax()"
                data-testid="calculate-top-up-btn">
                Calculate Pillar Two Top-Up Liability
              </button>

              @if (activeCalculation(); as calc) {
                <div class="calc-result" data-testid="top-up-result-box">
                  <div class="result-row">
                    <span>Effective Tax Rate (ETR):</span>
                    <strong>{{ calc.etr }}%</strong>
                  </div>
                  <div class="result-row">
                    <span>Top-Up Percentage (15% - ETR):</span>
                    <strong class="text-warning">{{ calc.topUpPct }}%</strong>
                  </div>
                  <div class="result-row" data-testid="sbie-carveout-val">
                    <span>Substance-Based Carveout (SBIE):</span>
                    <strong>€{{ (calc.sbieCarveoutEur / 1000000).toFixed(2) }}M</strong>
                  </div>
                  <div class="result-row">
                    <span>Excess Net GloBE Profit:</span>
                    <strong>€{{ (calc.excessProfitEur / 1000000).toFixed(2) }}M</strong>
                  </div>
                  <div class="result-row total-liability">
                    <span>Jurisdictional Top-Up Tax Liability:</span>
                    <strong class="text-danger">€{{ (calc.topUpTaxEur / 1000000).toFixed(2) }}M</strong>
                  </div>
                </div>
              }
            </div>
          }
        </section>

        <!-- Safe Harbor Matrix Rules -->
        <section class="card rules-card">
          <h2>Transitional CbCR Safe Harbor Framework</h2>
          <div class="rules-list">
            <div class="rule-box">
              <h4>1. De Minimis Test</h4>
              <p>Consolidated revenue &lt; €10M AND profit before tax &lt; €1M.</p>
            </div>
            <div class="rule-box">
              <h4>2. Simplified ETR Test</h4>
              <p>Simplified covered taxes / profit before tax &gt;= 15.0%.</p>
            </div>
            <div class="rule-box">
              <h4>3. Routine Profits Test</h4>
              <p>Profit before tax &lt;= Substance-Based Income Exclusion (SBIE).</p>
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
    .jurisdictions-list {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .jur-card {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.75rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .jur-card:hover, .jur-card.selected {
      border-color: #38bdf8;
      background: #1e293b;
    }
    .jur-top {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .jur-top h3 { margin: 0; font-size: 1rem; color: #f8fafc; }
    .status-pill {
      font-size: 0.75rem;
      padding: 0.2rem 0.4rem;
      border-radius: 0.25rem;
    }
    .status-pill.pass { background: #15803d; color: #dcfce7; }
    .status-pill.fail { background: #b91c1c; color: #fee2e2; }
    .jur-meta { display: flex; gap: 1rem; font-size: 0.8rem; color: #94a3b8; margin: 0.35rem 0; }
    .safe-harbor-type { font-size: 0.75rem; color: #38bdf8; }
    .btn-primary {
      background: #2563eb;
      color: #fff;
      border: none;
      border-radius: 0.375rem;
      padding: 0.5rem 1rem;
      font-weight: 600;
      cursor: pointer;
      margin-top: 0.5rem;
      transition: background 0.2s;
    }
    .btn-primary:hover { background: #1d4ed8; }
    .calc-result {
      margin-top: 1rem;
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.5rem;
      padding: 0.85rem;
      font-size: 0.85rem;
    }
    .result-row {
      display: flex;
      justify-content: space-between;
      margin-bottom: 0.45rem;
    }
    .total-liability {
      border-top: 1px solid #334155;
      padding-top: 0.5rem;
      font-size: 0.95rem;
    }
    .text-warning { color: #f59e0b; }
    .text-danger { color: #ef4444; }
    .rules-list { display: flex; flex-direction: column; gap: 0.75rem; }
    .rule-box {
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 0.375rem;
      padding: 0.65rem;
      font-size: 0.8rem;
    }
    .rule-box h4 { margin: 0 0 0.25rem 0; color: #38bdf8; }
    .rule-box p { margin: 0; color: #94a3b8; }
  `]
})
export class GlobeTopUpTaxCockpitComponent {
  jurisdictions = signal<JurisdictionProfile[]>([
    {
      code: 'BM',
      name: 'Bermuda',
      revenueEur: 45000000,
      netIncomeEur: 22000000,
      coveredTaxesEur: 440000,
      statutoryRate: 0.0,
      safeHarborPass: false,
      safeHarborType: 'Fails All Tests'
    },
    {
      code: 'IE',
      name: 'Ireland',
      revenueEur: 320000000,
      netIncomeEur: 98000000,
      coveredTaxesEur: 12250000,
      statutoryRate: 12.5,
      safeHarborPass: false,
      safeHarborType: 'Subject to QDMTT'
    },
    {
      code: 'DE',
      name: 'Germany',
      revenueEur: 180000000,
      netIncomeEur: 35000000,
      coveredTaxesEur: 10500000,
      statutoryRate: 30.0,
      safeHarborPass: true,
      safeHarborType: 'Simplified ETR >= 15%'
    }
  ]);

  selectedJurisdiction = signal<JurisdictionProfile | null>(this.jurisdictions()[0]);
  activeCalculation = signal<TopUpCalculation | null>(null);

  selectJurisdiction(jur: JurisdictionProfile) {
    this.selectedJurisdiction.set(jur);
    this.activeCalculation.set(null);
  }

  calculateTopUpTax() {
    const jur = this.selectedJurisdiction();
    if (!jur) return;

    const etr = +(jur.coveredTaxesEur / jur.netIncomeEur * 100).toFixed(2);
    const topUpPct = Math.max(0, +(15.0 - etr).toFixed(2));
    const sbie = 2100000; // 2.1M SBIE carveout
    const excess = Math.max(0, jur.netIncomeEur - sbie);
    const topUpEur = Math.round(excess * (topUpPct / 100));

    this.activeCalculation.set({
      jurisdictionCode: jur.code,
      etr,
      minimumRate: 15.0,
      topUpPct,
      sbieCarveoutEur: sbie,
      excessProfitEur: excess,
      topUpTaxEur: topUpEur
    });
  }
}
