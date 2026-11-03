import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface FixedAssetRecord {
  assetTag: string;
  assetName: string;
  assetClass: string;
  costBasisUsd: number;
  salvageValueUsd: number;
  usefulLifeYears: number;
  depreciationConvention: string;
}

export interface DepreciationScheduleEntry {
  scheduleId: string;
  assetTag: string;
  fiscalYear: number;
  depreciationAmountUsd: number;
  remainingBookValueUsd: number;
  standardApplied: string;
}

@Component({
  selector: 'app-asset-depreciation-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="asset-depreciation-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-blue-500 animate-pulse"></span>
            Dynamic CapEx Depreciation & Asset Lifecycle Ledger
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            US MACRS 200% Declining Balance, IFRS Straight-Line Dual-Book Engine & Impairment Write-Downs
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="calculateDepreciationSchedule()" class="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white font-medium rounded-lg shadow-sm transition">
            Calculate Next Period Depreciation
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Fixed Assets</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ assets().length }}</p>
          <span class="text-xs text-blue-400">GPU Clusters & POPs</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Total Cost Basis</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">$3.35M</p>
          <span class="text-xs text-slate-400">Capitalized Equipment</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Net Book Value</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">$2.61M</p>
          <span class="text-xs text-slate-400">Post-Depreciation Carrying Value</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Monthly Expense</span>
          <p class="text-2xl font-extrabold text-amber-400 mt-1">$54.4K</p>
          <span class="text-xs text-slate-400">Dual-Book Automated Accrual</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Assets Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Capital Asset Register</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Asset Tag</th>
                  <th class="pb-3 font-medium">Asset Name</th>
                  <th class="pb-3 font-medium">Cost Basis</th>
                  <th class="pb-3 font-medium">Useful Life</th>
                  <th class="pb-3 font-medium">Depreciation Standard</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let a of assets()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-mono text-xs text-blue-400 font-semibold">{{ a.assetTag }}</td>
                  <td class="py-3 font-medium text-slate-200">{{ a.assetName }}</td>
                  <td class="py-3 font-mono text-emerald-400 font-bold">\${{ a.costBasisUsd.toLocaleString() }}</td>
                  <td class="py-3 text-slate-400">{{ a.usefulLifeYears }} Years</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-xs rounded font-mono bg-slate-700 text-slate-300">
                      {{ a.depreciationConvention }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Depreciation Schedules -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Statutory Accrual Ledger</h2>
          <div class="space-y-3">
            <div *ngFor="let s of schedules()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-mono text-xs text-blue-300 font-bold">{{ s.scheduleId }}</span>
                <span class="px-2 py-0.5 text-xs bg-emerald-500/20 text-emerald-300 rounded font-semibold font-mono">
                  \${{ s.depreciationAmountUsd.toLocaleString() }}
                </span>
              </div>
              <p class="text-xs text-slate-300">Asset: {{ s.assetTag }} | FY: {{ s.fiscalYear }}</p>
              <div class="mt-2 text-[10px] font-mono text-slate-400 bg-slate-950 p-1.5 rounded">
                Remaining Book Value: \${{ s.remainingBookValueUsd.toLocaleString() }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class AssetDepreciationCockpitComponent {
  assets = signal<FixedAssetRecord[]>([
    { assetTag: 'AST-GPU-H100-01', assetName: 'NVIDIA H100 GPU AI Cluster (64 Nodes)', assetClass: 'GPU Server Fleet', costBasisUsd: 2500000, salvageValueUsd: 250000, usefulLifeYears: 3, depreciationConvention: 'US_MACRS_5YR' },
    { assetTag: 'AST-EDGE-POP-02', assetName: 'Anycast Edge Server Rack Deployments (12 POPs)', assetClass: 'Edge POP Hardware', costBasisUsd: 850000, salvageValueUsd: 85000, usefulLifeYears: 5, depreciationConvention: 'IFRS_STRAIGHT_LINE' }
  ]);

  schedules = signal<DepreciationScheduleEntry[]>([
    { scheduleId: 'SCH-AST-01-2026', assetTag: 'AST-GPU-H100-01', fiscalYear: 2026, depreciationAmountUsd: 41666.67, remainingBookValueUsd: 1875000, standardApplied: 'US_MACRS_5YR_HALF_YEAR' }
  ]);

  calculateDepreciationSchedule() {
    const newEntry: DepreciationScheduleEntry = {
      scheduleId: `SCH-AST-02-${Date.now().toString().slice(-4)}`,
      assetTag: 'AST-EDGE-POP-02',
      fiscalYear: 2026,
      depreciationAmountUsd: 12750.00,
      remainingBookValueUsd: 697000.00,
      standardApplied: 'IFRS_STRAIGHT_LINE'
    };
    this.schedules.update(list => [newEntry, ...list]);
  }
}
