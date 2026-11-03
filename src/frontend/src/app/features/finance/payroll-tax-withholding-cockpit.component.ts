import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TaxJurisdictionProfile {
  jurisdictionCode: string;
  countryName: string;
  currency: string;
  incomeTaxRatePct: number;
  socialSecurityRatePct: number;
  pensionEmployerRatePct: number;
  regulatoryAuthority: string;
}

export interface StatutoryFilingRecord {
  filingId: string;
  jurisdictionCode: string;
  taxPeriod: string;
  totalTaxRemitted: number;
  currency: string;
  authorityConfirmationCode: string;
  status: string;
}

@Component({
  selector: 'app-payroll-tax-withholding-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="payroll-tax-withholding-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
            Global Multi-Jurisdiction Payroll Tax & Sovereign Compliance Ledger
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Statutory Income Tax, Social Security & Pension Withholding Mesh with Cryptographic Sovereign Remittance
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="generateFiling()" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-medium rounded-lg shadow-sm transition">
            Generate Sovereign Tax Filing
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Jurisdictions</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ jurisdictions().length }}</p>
          <span class="text-xs text-emerald-400">US, UK, DE, SG Compliant</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Monthly Withheld</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">$1.28M</p>
          <span class="text-xs text-slate-400">Statutory Tax & Pension</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Remitted QTD</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">$3.84M</p>
          <span class="text-xs text-slate-400">100% On-Time Remittances</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Recent Filings</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">{{ filings().length }}</p>
          <span class="text-xs text-emerald-400">Merkle Audit Verified</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Jurisdictions Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Statutory Tax Jurisdictions</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Jurisdiction</th>
                  <th class="pb-3 font-medium">Income Tax</th>
                  <th class="pb-3 font-medium">Social Security</th>
                  <th class="pb-3 font-medium">Pension Employer</th>
                  <th class="pb-3 font-medium">Regulatory Authority</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let jur of jurisdictions()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3">
                    <span class="font-medium text-slate-200 block">{{ jur.countryName }}</span>
                    <span class="font-mono text-xs text-indigo-400">{{ jur.jurisdictionCode }} ({{ jur.currency }})</span>
                  </td>
                  <td class="py-3 font-mono text-emerald-400">{{ jur.incomeTaxRatePct }}%</td>
                  <td class="py-3 font-mono text-indigo-300">{{ jur.socialSecurityRatePct }}%</td>
                  <td class="py-3 font-mono text-amber-300">{{ jur.pensionEmployerRatePct }}%</td>
                  <td class="py-3 text-slate-400 text-xs">{{ jur.regulatoryAuthority }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Filings Ledger -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Cryptographic Filings Ledger</h2>
          <div class="space-y-3">
            <div *ngFor="let f of filings()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-mono text-xs text-indigo-300 font-bold">{{ f.filingId }}</span>
                <span class="px-2 py-0.5 text-xs bg-emerald-500/20 text-emerald-300 rounded font-semibold">{{ f.status }}</span>
              </div>
              <p class="text-xs text-slate-300">Period: {{ f.taxPeriod }} | Remitted: {{ f.totalTaxRemitted.toLocaleString() }} {{ f.currency }}</p>
              <div class="mt-2 text-[10px] font-mono text-slate-400 bg-slate-950 p-1.5 rounded">
                Ref: {{ f.authorityConfirmationCode }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class PayrollTaxWithholdingCockpitComponent {
  jurisdictions = signal<TaxJurisdictionProfile[]>([
    { jurisdictionCode: 'US-FED-CA', countryName: 'United States (California)', currency: 'USD', incomeTaxRatePct: 24.0, socialSecurityRatePct: 7.65, pensionEmployerRatePct: 4.0, regulatoryAuthority: 'Internal Revenue Service / EDD' },
    { jurisdictionCode: 'GB-PAYE', countryName: 'United Kingdom', currency: 'GBP', incomeTaxRatePct: 20.0, socialSecurityRatePct: 8.0, pensionEmployerRatePct: 3.0, regulatoryAuthority: 'HM Revenue & Customs (HMRC)' },
    { jurisdictionCode: 'DE-LOHN', countryName: 'Germany', currency: 'EUR', incomeTaxRatePct: 28.5, socialSecurityRatePct: 9.3, pensionEmployerRatePct: 9.3, regulatoryAuthority: 'Bundeszentralamt für Steuern' },
    { jurisdictionCode: 'SG-CPF', countryName: 'Singapore', currency: 'SGD', incomeTaxRatePct: 15.0, socialSecurityRatePct: 20.0, pensionEmployerRatePct: 17.0, regulatoryAuthority: 'Inland Revenue Authority of Singapore / CPF' }
  ]);

  filings = signal<StatutoryFilingRecord[]>([
    { filingId: 'FIL-US-2026-M10', jurisdictionCode: 'US-FED-CA', taxPeriod: '2026-10', totalTaxRemitted: 345600, currency: 'USD', authorityConfirmationCode: 'IRS-EFTPS-998822', status: 'CONFIRMED_REMITTED' },
    { filingId: 'FIL-GB-2026-M10', jurisdictionCode: 'GB-PAYE', taxPeriod: '2026-10', totalTaxRemitted: 189200, currency: 'GBP', authorityConfirmationCode: 'HMRC-FPS-445511', status: 'CONFIRMED_REMITTED' }
  ]);

  generateFiling() {
    const newFiling: StatutoryFilingRecord = {
      filingId: `FIL-DE-${Date.now().toString().slice(-4)}`,
      jurisdictionCode: 'DE-LOHN',
      taxPeriod: '2026-11',
      totalTaxRemitted: 420000,
      currency: 'EUR',
      authorityConfirmationCode: 'BZST-ELSTER-778899',
      status: 'CONFIRMED_REMITTED'
    };
    this.filings.update(list => [newFiling, ...list]);
  }
}
