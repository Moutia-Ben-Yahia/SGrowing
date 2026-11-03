import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CbamDeclarationRecord {
  declarationId: string;
  importerTaxId: string;
  countryOfOrigin: string;
  goodsCategory: string;
  netMassMetricTons: number;
  embeddedDirectEmissionsTco2e: number;
  statutoryCarbonTaxLiabilityEur: number;
  status: string;
}

export interface EmissionsFactorRecord {
  factorId: string;
  countryIsoCode: string;
  goodsCategory: string;
  benchmarkFactorTco2ePerTon: number;
  sovereignCarbonPriceEurPerTco2e: number;
}

@Component({
  selector: 'app-sovereign-cbam-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="sovereign-cbam-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-teal-500 animate-pulse"></span>
            Sovereign Carbon Border Adjustment (CBAM) & Scope 1/2/3 Ledger
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Statutory EU CBAM Embedded Emissions Accounting, Cross-Border Carbon Certificate Liability & Offset Settlement
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="generateCbamFiling()" class="px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white font-medium rounded-lg shadow-sm transition">
            Generate EU CBAM Filing
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Declarations</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ declarations().length }}</p>
          <span class="text-xs text-teal-400">EU Statutory Registry</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Embedded Emissions</span>
          <p class="text-2xl font-extrabold text-white mt-1">1,815 tCO2e</p>
          <span class="text-xs text-cyan-400">Direct & Indirect Scope 3</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Tax Liability</span>
          <p class="text-2xl font-extrabold text-white mt-1">€153.5K</p>
          <span class="text-xs text-amber-400">€85.50 / tCO2e Benchmark</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Offset Clearance</span>
          <p class="text-2xl font-extrabold text-white mt-1">100.0%</p>
          <span class="text-xs text-emerald-400">Zero Custom Penalty Audit</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- CBAM Declarations Table -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Statutory Import Declarations</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="text-slate-400 border-b border-slate-700/60 pb-2">
                <tr>
                  <th class="py-2">Declaration ID</th>
                  <th>Origin / Goods</th>
                  <th>Mass</th>
                  <th>Embedded tCO2e</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800 text-slate-200">
                <tr *ngFor="let d of declarations()">
                  <td class="py-2 font-mono text-teal-400">{{ d.declarationId }}</td>
                  <td>{{ d.countryOfOrigin }} - {{ d.goodsCategory }}</td>
                  <td>{{ d.netMassMetricTons }} T</td>
                  <td class="font-bold">{{ d.embeddedDirectEmissionsTco2e }} tCO2e</td>
                  <td>
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-teal-950 text-teal-300 border border-teal-800">
                      {{ d.status }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Sovereign Benchmark Factors -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Sovereign Benchmark Factors</h2>
          <div class="space-y-4">
            <div *ngFor="let f of factors()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-white">{{ f.countryIsoCode }} — {{ f.goodsCategory }}</span>
                <span class="text-xs text-teal-400 font-mono font-bold">€{{ f.sovereignCarbonPriceEurPerTco2e }}/tCO2e</span>
              </div>
              <p class="text-xs text-slate-400">Benchmark Intensity Factor: {{ f.benchmarkFactorTco2ePerTon }} tCO2e per metric ton</p>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class SovereignCbamCockpitComponent {
  declarations = signal<CbamDeclarationRecord[]>([
    { declarationId: 'CBAM-DEC-2026-001', importerTaxId: 'NL849302199B01', countryOfOrigin: 'CN', goodsCategory: 'STEEL', netMassMetricTons: 500, embeddedDirectEmissionsTco2e: 950, statutoryCarbonTaxLiabilityEur: 91912.5, status: 'CLEARED_BY_CUSTOMS' },
    { declarationId: 'CBAM-DEC-2026-002', importerTaxId: 'FR39849201928', countryOfOrigin: 'IN', goodsCategory: 'ALUMINIUM', netMassMetricTons: 150, embeddedDirectEmissionsTco2e: 640, statutoryCarbonTaxLiabilityEur: 61560, status: 'OFFSET_SETTLED' }
  ]);

  factors = signal<EmissionsFactorRecord[]>([
    { factorId: 'FCT-STL-CN', countryIsoCode: 'CN', goodsCategory: 'STEEL', benchmarkFactorTco2ePerTon: 2.15, sovereignCarbonPriceEurPerTco2e: 85.5 },
    { factorId: 'FCT-ALU-IN', countryIsoCode: 'IN', goodsCategory: 'ALUMINIUM', benchmarkFactorTco2ePerTon: 4.8, sovereignCarbonPriceEurPerTco2e: 85.5 }
  ]);

  generateCbamFiling() {
    const newDec: CbamDeclarationRecord = {
      declarationId: `CBAM-DEC-${Date.now().toString().slice(-4)}`,
      importerTaxId: 'DE819283741',
      countryOfOrigin: 'TR',
      goodsCategory: 'FERTILIZERS',
      netMassMetricTons: 300,
      embeddedDirectEmissionsTco2e: 520,
      statutoryCarbonTaxLiabilityEur: 44460,
      status: 'CLEARED_BY_CUSTOMS'
    };
    this.declarations.update(list => [newDec, ...list]);
  }
}
