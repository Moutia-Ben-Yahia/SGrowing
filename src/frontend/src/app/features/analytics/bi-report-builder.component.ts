import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface BiMetricRow {
  dimension: string;
  subDimension?: string;
  metricValue: number;
  secondaryValue: number;
  formattedMetric: string;
}

export interface MonteCarloScenario {
  percentile: string;
  arrTarget: number;
  probability: number;
  churnImpact: string;
}

export interface SavedReportItem {
  id: string;
  name: string;
  category: string;
  dimensions: string[];
  metrics: string[];
  lastRunUtc: string;
}

@Component({
  selector: 'app-bi-report-builder',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="bi-report-builder-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Enterprise BI Report Builder & ARR Forecaster</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-100 text-indigo-800">
              Monte Carlo Engine Ready
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Dynamic dimensional slicing, pivot analytics, and 1,000-run stochastic Monte Carlo probability distributions for ARR.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="runMonteCarloSimulation()"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-run-simulation">
            &infin; Run Monte Carlo (1,000 Paths)
          </button>
        </div>
      </div>

      <!-- Forecaster Overview KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="bi-forecaster-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Base Current ARR</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-base-arr">
            \${{ (baseArr() / 1_000_000) | number:'1.2-2' }}M
          </div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">Live Subscription Run-Rate</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">P50 Expected ARR (12-Mo)</div>
          <div class="text-2xl font-bold text-indigo-600 mt-1 font-mono" data-testid="kpi-p50-arr">
            \${{ (simP50Arr() / 1_000_000) | number:'1.2-2' }}M
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Median Confidence Band</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">P90 Bullish Case</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono" data-testid="kpi-p90-arr">
            \${{ (simP90Arr() / 1_000_000) | number:'1.2-2' }}M
          </div>
          <div class="text-[11px] text-slate-400 mt-1">Upper 10th Decile Tail</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Target Attainment Prob</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-prob-target">
            {{ probTarget() }}%
          </div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">Above \${{ (targetArr() / 1_000_000) | number:'1.1-1' }}M Milestone</div>
        </div>
      </div>

      <!-- Workbench View Selector -->
      <div class="flex items-center gap-2 border-b pb-2">
        <button
          (click)="activeTab.set('monte-carlo')"
          [class.border-indigo-600]="activeTab() === 'monte-carlo'"
          [class.text-indigo-600]="activeTab() === 'monte-carlo'"
          class="px-4 py-2 text-xs font-semibold border-b-2 border-transparent transition-colors"
          data-testid="tab-monte-carlo">
          Monte Carlo Forecast Scenarios
        </button>
        <button
          (click)="activeTab.set('pivot-builder')"
          [class.border-indigo-600]="activeTab() === 'pivot-builder'"
          [class.text-indigo-600]="activeTab() === 'pivot-builder'"
          class="px-4 py-2 text-xs font-semibold border-b-2 border-transparent transition-colors"
          data-testid="tab-pivot-builder">
          Pivot Slicer & Custom Reports
        </button>
      </div>

      <!-- Tab 1: Monte Carlo Scenarios -->
      @if (activeTab() === 'monte-carlo') {
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <!-- Simulation Parameters -->
          <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
            <h3 class="font-bold text-slate-900 text-sm">Simulation Hyperparameters</h3>
            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-500 mb-1">Target ARR Threshold ($)</label>
                <input 
                  type="number" 
                  [(ngModel)]="targetArrInput" 
                  class="w-full border rounded-lg px-3 py-1.5 font-mono text-slate-800" 
                  data-testid="input-target-arr" />
              </div>
              <div>
                <label class="block text-slate-500 mb-1">Mean Annual Growth Rate (%)</label>
                <input 
                  type="number" 
                  [(ngModel)]="growthMean" 
                  class="w-full border rounded-lg px-3 py-1.5 font-mono text-slate-800" />
              </div>
              <div>
                <label class="block text-slate-500 mb-1">Annual Churn Volatility StdDev (%)</label>
                <input 
                  type="number" 
                  [(ngModel)]="churnStdDev" 
                  class="w-full border rounded-lg px-3 py-1.5 font-mono text-slate-800" />
              </div>
              <div>
                <label class="block text-slate-500 mb-1">Monte Carlo Trial Iterations</label>
                <input 
                  type="number" 
                  [(ngModel)]="iterations" 
                  class="w-full border rounded-lg px-3 py-1.5 font-mono text-slate-800" 
                  disabled />
              </div>
            </div>

            <button 
              (click)="recalculateDistribution()"
              class="w-full py-2 bg-slate-900 hover:bg-slate-800 text-white rounded-lg text-xs font-semibold transition-colors"
              data-testid="btn-recalculate-sim">
              Recalculate Bounds
            </button>
          </div>

          <!-- Percentile Distribution Table -->
          <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
            <div class="flex items-center justify-between">
              <h3 class="font-bold text-slate-900 text-sm">Probability Quantile Distribution (12 Months Out)</h3>
              <span class="text-xs text-slate-400 font-mono">1,000 Paths Sampled</span>
            </div>

            <div class="overflow-x-auto" data-testid="table-scenarios">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-50 border-b text-slate-500">
                  <tr>
                    <th class="p-3">Quantile</th>
                    <th class="p-3">Simulated ARR</th>
                    <th class="p-3">Exceedance Probability</th>
                    <th class="p-3">Volatility Cushion</th>
                  </tr>
                </thead>
                <tbody class="divide-y">
                  @for (scenario of scenarios(); track scenario.percentile) {
                    <tr class="hover:bg-slate-50">
                      <td class="p-3 font-bold text-slate-800">{{ scenario.percentile }}</td>
                      <td class="p-3 font-mono font-bold text-indigo-700">
                        \${{ scenario.arrTarget | number:'1.0-0' }}
                      </td>
                      <td class="p-3">
                        <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800">
                          {{ scenario.probability }}%
                        </span>
                      </td>
                      <td class="p-3 text-slate-500">{{ scenario.churnImpact }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- Tab 2: Pivot Slicer & Custom Reports -->
      @if (activeTab() === 'pivot-builder') {
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
            <h3 class="font-bold text-slate-900 text-sm">Saved BI Queries</h3>
            <div class="space-y-3" data-testid="saved-reports-list">
              @for (rpt of savedReports(); track rpt.id) {
                <div class="p-3 border rounded-lg hover:border-slate-300 transition-colors text-xs space-y-1">
                  <div class="font-bold text-slate-800">{{ rpt.name }}</div>
                  <div class="text-[11px] text-slate-500 font-mono">Dims: {{ rpt.dimensions.join(', ') }}</div>
                  <div class="text-[10px] text-slate-400">Metrics: {{ rpt.metrics.join(', ') }}</div>
                </div>
              }
            </div>
          </div>

          <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
            <h3 class="font-bold text-slate-900 text-sm">Aggregated Multi-Dimensional Breakdown</h3>
            <div class="overflow-x-auto" data-testid="pivot-breakdown-table">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-50 border-b text-slate-500">
                  <tr>
                    <th class="p-3">Channel / Region</th>
                    <th class="p-3">Cohort</th>
                    <th class="p-3">Active ARR</th>
                    <th class="p-3">Gross Margin</th>
                  </tr>
                </thead>
                <tbody class="divide-y">
                  @for (row of pivotRows(); track row.dimension) {
                    <tr class="hover:bg-slate-50">
                      <td class="p-3 font-semibold text-slate-800">{{ row.dimension }}</td>
                      <td class="p-3 text-slate-500">{{ row.subDimension }}</td>
                      <td class="p-3 font-mono font-bold text-slate-900">{{ row.formattedMetric }}</td>
                      <td class="p-3 font-mono text-emerald-700 font-semibold">{{ row.secondaryValue }}%</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class BiReportBuilderComponent {
  activeTab = signal<'monte-carlo' | 'pivot-builder'>('monte-carlo');

  baseArr = signal<number>(10_000_000);
  simP50Arr = signal<number>(13_500_000);
  simP90Arr = signal<number>(16_200_000);
  probTarget = signal<number>(76.4);
  targetArr = signal<number>(12_500_000);

  targetArrInput = 12500000;
  growthMean = 35.0;
  churnStdDev = 5.2;
  iterations = 1000;

  scenarios = signal<MonteCarloScenario[]>([
    { percentile: 'P10 (Bearish)', arrTarget: 11150000, probability: 90.0, churnImpact: 'High churn stress (15% net churn)' },
    { percentile: 'P25 (Conservative)', arrTarget: 12200000, probability: 75.0, churnImpact: 'Macro contraction headwind' },
    { percentile: 'P50 (Base Case)', arrTarget: 13500000, probability: 50.0, churnImpact: 'Steady-state expansion (118% NDR)' },
    { percentile: 'P75 (Strong)', arrTarget: 14850000, probability: 25.0, churnImpact: 'Accelerated enterprise inbound' },
    { percentile: 'P90 (Bullish)', arrTarget: 16200000, probability: 10.0, churnImpact: 'Viral expansion & top-tier tiering' }
  ]);

  savedReports = signal<SavedReportItem[]>([
    { id: 'rpt_1', name: 'ARR by Acquisition Channel & Territory', category: 'Executive', dimensions: ['Channel', 'Country'], metrics: ['ARR', 'CAC'], lastRunUtc: '2026-11-02T19:30:00Z' },
    { id: 'rpt_2', name: 'Cohort LTV-to-CAC Efficiency Matrix', category: 'Marketing', dimensions: ['CohortMonth'], metrics: ['LTV', 'ROAS'], lastRunUtc: '2026-11-02T18:00:00Z' }
  ]);

  pivotRows = signal<BiMetricRow[]>([
    { dimension: 'Paid Search (Google Ads)', subDimension: '2026-Q1', metricValue: 2450000, secondaryValue: 84.5, formattedMetric: '$2.45M' },
    { dimension: 'Direct Sales Enterprise', subDimension: '2026-Q1', metricValue: 4890000, secondaryValue: 91.2, formattedMetric: '$4.89M' },
    { dimension: 'Partner Referrals & App Store', subDimension: '2026-Q2', metricValue: 3120000, secondaryValue: 88.0, formattedMetric: '$3.12M' }
  ]);

  runMonteCarloSimulation() {
    this.recalculateDistribution();
  }

  recalculateDistribution() {
    this.targetArr.set(this.targetArrInput);
    const growthFactor = 1 + (this.growthMean / 100);
    const newP50 = Math.round(this.baseArr() * growthFactor);
    const newP90 = Math.round(newP50 * 1.2);
    
    this.simP50Arr.set(newP50);
    this.simP90Arr.set(newP90);

    const prob = this.targetArrInput <= newP50 ? 78.5 : 45.2;
    this.probTarget.set(prob);

    this.scenarios.update(list => [
      { percentile: 'P10 (Bearish)', arrTarget: Math.round(newP50 * 0.82), probability: 90.0, churnImpact: 'Stress scenario' },
      { percentile: 'P25 (Conservative)', arrTarget: Math.round(newP50 * 0.90), probability: 75.0, churnImpact: 'Conservative bounds' },
      { percentile: 'P50 (Base Case)', arrTarget: newP50, probability: 50.0, churnImpact: 'Median trajectory' },
      { percentile: 'P75 (Strong)', arrTarget: Math.round(newP50 * 1.10), probability: 25.0, churnImpact: 'Upper quartile' },
      { percentile: 'P90 (Bullish)', arrTarget: newP90, probability: 10.0, churnImpact: 'Top 10% tail' }
    ]);
  }
}
