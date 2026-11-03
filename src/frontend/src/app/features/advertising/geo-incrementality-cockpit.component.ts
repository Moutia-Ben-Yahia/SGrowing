import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface GeoTestCellRecord {
  cellId: string;
  dmaRegionName: string;
  marketRole: string;
  baselineWeeklyRevenueUsd: number;
  syntheticWeightCoefficient: number;
}

export interface GeoIncrementalityExperimentRecord {
  experimentId: string;
  campaignName: string;
  totalAdSpendUsd: number;
  measuredIncrementalRevenueUsd: number;
  trueIncrementalRoasMultiplier: number;
  status: string;
}

@Component({
  selector: 'app-geo-incrementality-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="geo-incrementality-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-cyan-500 animate-pulse"></span>
            Synthetic Control & Geo Incrementality Testing Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Algorithmic Matched-Market Geo Holdouts, Ridge Regression Synthetic Controls & True Incremental ROAS
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="launchNewGeoExperiment()" class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white font-medium rounded-lg shadow-sm transition">
            Deploy Geo Matched-Market Flight
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active Experiments</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ experiments().length }}</p>
          <span class="text-xs text-cyan-400">DMA Holdout Tests</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Calibrated DMAs</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ cells().length }}</p>
          <span class="text-xs text-emerald-400">Synthetic Control Weights</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">True iROAS</span>
          <p class="text-2xl font-extrabold text-white mt-1">3.86x</p>
          <span class="text-xs text-amber-400">Unbiased Causal Lift</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Incremental Lift</span>
          <p class="text-2xl font-extrabold text-white mt-1">$580K</p>
          <span class="text-xs text-indigo-400">Verified Revenue</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Calibrated DMA Market Cells -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Calibrated DMA Market Clusters</h2>
          <div class="space-y-4">
            <div *ngFor="let c of cells()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-white">{{ c.dmaRegionName }}</span>
                <span class="text-xs px-2 py-0.5 rounded font-mono"
                      [ngClass]="c.marketRole === 'TEST_TREATMENT' ? 'bg-cyan-950 text-cyan-300 border border-cyan-800' : 'bg-slate-800 text-slate-300'">
                  {{ c.marketRole }}
                </span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Baseline Weekly Rev: {{ c.baselineWeeklyRevenueUsd | currency }}</p>
              <div class="text-xs font-mono text-emerald-400">
                Synthetic Control Weight: {{ c.syntheticWeightCoefficient }}
              </div>
            </div>
          </div>
        </section>

        <!-- Experiments Table -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Active Geo Holdout Experiments</h2>
          <div class="space-y-4">
            <div *ngFor="let e of experiments()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-cyan-300">{{ e.campaignName }}</span>
                <span class="text-xs font-mono font-bold text-emerald-400">{{ e.trueIncrementalRoasMultiplier }}x iROAS</span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Spend: {{ e.totalAdSpendUsd | currency }} • Status: {{ e.status }}</p>
              <div class="bg-slate-950 p-2.5 rounded text-xs font-mono text-slate-200">
                Incremental Revenue Captured: {{ e.measuredIncrementalRevenueUsd | currency }}
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class GeoIncrementalityCockpitComponent {
  cells = signal<GeoTestCellRecord[]>([
    { cellId: 'CELL-DMA-DFW', dmaRegionName: 'Dallas-Fort Worth DMA #5', marketRole: 'TEST_TREATMENT', baselineWeeklyRevenueUsd: 420000, syntheticWeightCoefficient: 1.0 },
    { cellId: 'CELL-DMA-CHI', dmaRegionName: 'Chicago Metro DMA #3', marketRole: 'SYNTHETIC_CONTROL', baselineWeeklyRevenueUsd: 418000, syntheticWeightCoefficient: 0.642 }
  ]);

  experiments = signal<GeoIncrementalityExperimentRecord[]>([
    { experimentId: 'GEO-EXP-2026-01', campaignName: 'Q4 High-Yield CTV & Social Incrementality Test', totalAdSpendUsd: 150000, measuredIncrementalRevenueUsd: 580000, trueIncrementalRoasMultiplier: 3.86, status: 'RUNNING_HOLD_OUT' }
  ]);

  launchNewGeoExperiment() {
    const newExp: GeoIncrementalityExperimentRecord = {
      experimentId: `GEO-EXP-${Date.now().toString().slice(-4)}`,
      campaignName: 'Midwest Metro Omni-Channel Holdout',
      totalAdSpendUsd: 95000,
      measuredIncrementalRevenueUsd: 361000,
      trueIncrementalRoasMultiplier: 3.80,
      status: 'RUNNING_HOLD_OUT'
    };
    this.experiments.update(list => [newExp, ...list]);
  }
}
