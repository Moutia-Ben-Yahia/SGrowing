import { Component, ChangeDetectionStrategy, signal, computed, Optional } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type PipelineStage = 'Discovery' | 'Qualification' | 'ProposalPresented' | 'ContractNegotiation' | 'ClosedWon' | 'ClosedLost';
export type ForecastCategory = 'Pipeline' | 'BestCase' | 'Commit' | 'Closed';
export type SimulationDistributionModel = 'Normal' | 'LogNormal' | 'Triangular';

export interface OpportunitySimulationInput {
  id: string;
  dealName: string;
  accountName: string;
  amount: number;
  stage: PipelineStage;
  category: ForecastCategory;
  winProbability: number;
  targetCloseDate: string;
  dealAgeDays: number;
  slippageRiskScore: number;
  historicalStageConversionRate: number;
  minEstimatedValue: number;
  mostLikelyEstimatedValue: number;
  maxEstimatedValue: number;
  distribution: SimulationDistributionModel;
}

export interface SimulationHistogramBin {
  rangeMin: number;
  rangeMax: number;
  count: number;
  densityPercent: number;
}

export interface MonteCarloRunResult {
  iterations: number;
  p10: number;
  p50: number;
  p90: number;
  mean: number;
  standardDeviation: number;
  targetQuota: number;
  quotaAttainmentProbability: number;
  expectedValue: number;
  pipelineTotalValue: number;
  commitTotalValue: number;
  bestCaseTotalValue: number;
  percentileDistribution: number[];
  histogramBins: SimulationHistogramBin[];
  simulatedAtUtc: string;
}

export interface DealSlippageRiskAlert {
  opportunityId: string;
  dealName: string;
  accountName: string;
  amount: number;
  stage: PipelineStage;
  slippageProbability: number;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical';
  riskReason: string;
  recommendedIntervention: string;
  daysStalled: number;
}

@Component({
  selector: 'app-revenue-forecast-simulation-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="revenue-forecast-studio p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-slate-200 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">
              Predictive Revenue Forecasting & Monte Carlo Simulation
            </h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-100 text-indigo-800">
              Domain I2 & F4 Engine
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Multi-iteration stochastic pipeline simulation engine with P10/P50/P90 probability density & deal slippage risk radar.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            type="button"
            data-testid="run-sim-btn"
            (click)="runSimulation()"
            [disabled]="isSimulating()"
            class="inline-flex items-center gap-2 px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white font-medium text-sm shadow-sm transition-colors disabled:opacity-50"
          >
            <svg class="w-4 h-4" [class.animate-spin]="isSimulating()" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            {{ isSimulating() ? 'Simulating...' : 'Run Monte Carlo' }}
          </button>

          <button
            type="button"
            data-testid="add-deal-btn"
            (click)="openAddModal()"
            class="inline-flex items-center gap-2 px-3 py-2 rounded-lg border border-slate-300 bg-white hover:bg-slate-50 text-slate-700 font-medium text-sm shadow-sm transition-colors"
          >
            + Add Deal
          </button>
        </div>
      </div>

      <!-- Top KPI Cards -->
      <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4" data-testid="kpi-cards">
        <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-500">Total Pipeline</div>
          <div class="text-xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-pipeline">
            \${{ (totalPipeline() / 1000000) | number:'1.2-2' }}M
          </div>
          <div class="text-[11px] text-slate-500 mt-1">{{ opportunities().length }} Opportunities</div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-500">P10 (Conservative)</div>
          <div class="text-xl font-bold text-amber-600 mt-1 font-mono" data-testid="kpi-p10">
            \${{ (p10Value() / 1000000) | number:'1.2-2' }}M
          </div>
          <div class="text-[11px] text-amber-600 font-medium mt-1">90% Confidence Floor</div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-500">P50 (Median Expected)</div>
          <div class="text-xl font-bold text-indigo-600 mt-1 font-mono" data-testid="kpi-p50">
            \${{ (p50Value() / 1000000) | number:'1.2-2' }}M
          </div>
          <div class="text-[11px] text-indigo-600 font-semibold mt-1">Stochastic Median</div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-500">P90 (Optimistic)</div>
          <div class="text-xl font-bold text-emerald-600 mt-1 font-mono" data-testid="kpi-p90">
            \${{ (p90Value() / 1000000) | number:'1.2-2' }}M
          </div>
          <div class="text-[11px] text-emerald-600 font-medium mt-1">10% Stretch Ceiling</div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-500">Quota Attainment</div>
          <div class="text-xl font-bold mt-1 font-mono" [class.text-emerald-600]="quotaAttainmentPct() >= 75" [class.text-amber-600]="quotaAttainmentPct() < 75" data-testid="kpi-quota-prob">
            {{ quotaAttainmentPct() | number:'1.1-1' }}%
          </div>
          <div class="text-[11px] text-slate-500 mt-1">Target: \${{ (quotaTarget() / 1000000) | number:'1.1-1' }}M</div>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-sm">
          <div class="text-xs font-medium text-slate-500">Slippage Exposure</div>
          <div class="text-xl font-bold text-rose-600 mt-1 font-mono" data-testid="kpi-slippage">
            \${{ (criticalSlippageValue() / 1000) | number:'1.0-0' }}k
          </div>
          <div class="text-[11px] text-rose-600 font-medium mt-1">{{ criticalAlertsCount() }} Critical Alerts</div>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="flex border-b border-slate-200 gap-6 text-sm font-medium">
        <button
          type="button"
          (click)="activeTab.set('simulation')"
          [class.border-indigo-600]="activeTab() === 'simulation'"
          [class.text-indigo-600]="activeTab() === 'simulation'"
          [class.border-transparent]="activeTab() !== 'simulation'"
          [class.text-slate-500]="activeTab() !== 'simulation'"
          class="pb-3 border-b-2 hover:text-slate-700 transition-colors"
        >
          Monte Carlo Distribution
        </button>
        <button
          type="button"
          (click)="activeTab.set('scenario-runner')"
          [class.border-indigo-600]="activeTab() === 'scenario-runner'"
          [class.text-indigo-600]="activeTab() === 'scenario-runner'"
          [class.border-transparent]="activeTab() !== 'scenario-runner'"
          [class.text-slate-500]="activeTab() !== 'scenario-runner'"
          class="pb-3 border-b-2 hover:text-slate-700 transition-colors"
        >
          Interactive Scenario Runner
        </button>
        <button
          type="button"
          (click)="activeTab.set('slippage-radar')"
          [class.border-indigo-600]="activeTab() === 'slippage-radar'"
          [class.text-indigo-600]="activeTab() === 'slippage-radar'"
          [class.border-transparent]="activeTab() !== 'slippage-radar'"
          [class.text-slate-500]="activeTab() !== 'slippage-radar'"
          class="pb-3 border-b-2 hover:text-slate-700 transition-colors"
        >
          Deal Slippage Risk Radar ({{ slippageAlerts().length }})
        </button>
        <button
          type="button"
          (click)="activeTab.set('pipeline')"
          [class.border-indigo-600]="activeTab() === 'pipeline'"
          [class.text-indigo-600]="activeTab() === 'pipeline'"
          [class.border-transparent]="activeTab() !== 'pipeline'"
          [class.text-slate-500]="activeTab() !== 'pipeline'"
          class="pb-3 border-b-2 hover:text-slate-700 transition-colors"
        >
          Pipeline Deals ({{ opportunities().length }})
        </button>
      </div>

      <!-- Tab 1: Monte Carlo Distribution -->
      @if (activeTab() === 'simulation') {
        <div class="space-y-6" data-testid="simulation-view">
          <!-- Percentile Fan Bar -->
          <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-sm space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h2 class="text-base font-semibold text-slate-900">Percentile Confidence Spectrum</h2>
                <p class="text-xs text-slate-500">P10 (floor) through P90 (ceiling) evaluated over {{ iterations() | number }} trials.</p>
              </div>
              <div class="flex items-center gap-4 text-xs font-mono">
                <span class="flex items-center gap-1.5"><span class="w-3 h-3 rounded bg-amber-400 inline-block"></span> P10: \${{ (p10Value() / 1000) | number:'1.0-0' }}k</span>
                <span class="flex items-center gap-1.5"><span class="w-3 h-3 rounded bg-indigo-500 inline-block"></span> P50: \${{ (p50Value() / 1000) | number:'1.0-0' }}k</span>
                <span class="flex items-center gap-1.5"><span class="w-3 h-3 rounded bg-emerald-500 inline-block"></span> P90: \${{ (p90Value() / 1000) | number:'1.0-0' }}k</span>
                <span class="flex items-center gap-1.5 text-rose-500 font-semibold border-l pl-4 border-slate-200">Quota: \${{ (quotaTarget() / 1000) | number:'1.0-0' }}k</span>
              </div>
            </div>

            <!-- Visual Bar -->
            <div class="relative w-full h-8 bg-slate-100 rounded-lg overflow-hidden flex items-center">
              <div
                class="h-full bg-gradient-to-r from-amber-400 via-indigo-500 to-emerald-500 rounded-lg transition-all duration-300"
                [style.width.%]="spectrumWidthPercent()"
                [style.margin-left.%]="spectrumStartPercent()"
              ></div>
            </div>

            <div class="grid grid-cols-3 text-xs text-slate-500 text-center pt-1 border-t border-slate-100">
              <div><strong>Conservative Risk</strong><br/>Worst 10% scenarios under \${{ (p10Value() / 1000000) | number:'1.2-2' }}M</div>
              <div><strong>Expected Plan</strong><br/>Weighted center at \${{ (p50Value() / 1000000) | number:'1.2-2' }}M</div>
              <div><strong>Upside Stretch</strong><br/>Top 10% scenarios reach \${{ (p90Value() / 1000000) | number:'1.2-2' }}M</div>
            </div>
          </div>

          <!-- Probability Density Histogram -->
          <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-sm space-y-4">
            <h2 class="text-base font-semibold text-slate-900">Outcome Probability Density Histogram</h2>
            <div class="h-48 flex items-end gap-2 pt-6 pb-2" data-testid="histogram-chart">
              @for (bin of histogramBins(); track bin.rangeMin) {
                <div class="flex-1 flex flex-col items-center gap-1 h-full justify-end group">
                  <div class="text-[10px] text-slate-400 opacity-0 group-hover:opacity-100 transition-opacity font-mono">
                    {{ bin.densityPercent }}%
                  </div>
                  <div
                    class="w-full rounded-t transition-all duration-300"
                    [class.bg-indigo-500]="bin.rangeMax < quotaTarget()"
                    [class.bg-emerald-500]="bin.rangeMax >= quotaTarget()"
                    [style.height.%]="getHistogramBarHeight(bin.densityPercent)"
                  ></div>
                  <div class="text-[9px] text-slate-400 truncate w-full text-center font-mono mt-1">
                    \${{ (bin.rangeMin / 1000) | number:'1.0-0' }}k
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      }

      <!-- Tab 2: Interactive Scenario Runner -->
      @if (activeTab() === 'scenario-runner') {
        <div class="bg-white border border-slate-200 rounded-xl p-6 shadow-sm space-y-6" data-testid="scenario-runner-view">
          <h2 class="text-lg font-semibold text-slate-900">Interactive Simulation Parameters</h2>

          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            <!-- Quota Target -->
            <div class="space-y-2">
              <label class="block text-xs font-medium text-slate-700">Quarterly Quota Target ($)</label>
              <input
                type="number"
                [ngModel]="quotaTarget()"
                (ngModelChange)="quotaTarget.set($event)"
                data-testid="scenario-quota-input"
                class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm font-mono focus:ring-2 focus:ring-indigo-500 focus:outline-none"
              />
              <span class="text-[11px] text-slate-400">Target to measure attainment probability against.</span>
            </div>

            <!-- Iterations -->
            <div class="space-y-2">
              <label class="block text-xs font-medium text-slate-700">Monte Carlo Iterations</label>
              <select
                [ngModel]="iterations()"
                (ngModelChange)="iterations.set(+$event)"
                data-testid="scenario-iterations-select"
                class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:outline-none"
              >
                <option [value]="1000">1,000 runs (Fast)</option>
                <option [value]="5000">5,000 runs (Standard)</option>
                <option [value]="10000">10,000 runs (Precision)</option>
                <option [value]="20000">20,000 runs (High Fidelity)</option>
              </select>
              <span class="text-[11px] text-slate-400">Total simulated futures generated per deal.</span>
            </div>

            <!-- Distribution Model -->
            <div class="space-y-2">
              <label class="block text-xs font-medium text-slate-700">Valuation Distribution Model</label>
              <select
                [ngModel]="distributionModel()"
                (ngModelChange)="distributionModel.set($event)"
                data-testid="scenario-dist-select"
                class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:ring-2 focus:ring-indigo-500 focus:outline-none"
              >
                <option value="Triangular">Triangular (Min / Likely / Max)</option>
                <option value="Normal">Normal (Gaussian Bell Curve)</option>
                <option value="LogNormal">Log-Normal (Right Skewed / Enterprise)</option>
              </select>
              <span class="text-[11px] text-slate-400">Distribution shape applied to deal outcomes.</span>
            </div>

            <!-- Market Volatility -->
            <div class="space-y-2">
              <label class="block text-xs font-medium text-slate-700">Market Volatility Index ({{ marketVolatilityIndex() }}x)</label>
              <input
                type="range"
                min="0.5"
                max="1.5"
                step="0.05"
                [ngModel]="marketVolatilityIndex()"
                (ngModelChange)="marketVolatilityIndex.set(+$event)"
                data-testid="scenario-volatility-slider"
                class="w-full accent-indigo-600 mt-2"
              />
              <div class="flex justify-between text-[11px] text-slate-400">
                <span>0.5x (Stable)</span>
                <span>1.0x (Baseline)</span>
                <span>1.5x (Turbulent)</span>
              </div>
            </div>
          </div>

          <div class="border-t border-slate-100 pt-5 flex items-center justify-between">
            <span class="text-xs text-slate-500">Parameters update the stochastic model immediately when run.</span>
            <button
              type="button"
              (click)="runSimulation()"
              class="px-5 py-2.5 rounded-lg bg-indigo-600 hover:bg-indigo-700 text-white font-medium text-sm transition-colors"
            >
              Re-run Scenario With New Parameters
            </button>
          </div>
        </div>
      }

      <!-- Tab 3: Deal Slippage Risk Radar -->
      @if (activeTab() === 'slippage-radar') {
        <div class="bg-white border border-slate-200 rounded-xl overflow-hidden shadow-sm" data-testid="slippage-radar-view">
          <div class="p-5 border-b border-slate-200">
            <h2 class="text-base font-semibold text-slate-900">Deal Slippage Risk Radar</h2>
            <p class="text-xs text-slate-500 mt-0.5">Automated detection of deal staleness, low stage conversion, and AI-recommended interventions.</p>
          </div>

          <div class="divide-y divide-slate-100">
            @for (alert of slippageAlerts(); track alert.opportunityId) {
              <div class="p-4 hover:bg-slate-50 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div class="space-y-1">
                  <div class="flex items-center gap-2">
                    <span
                      class="px-2 py-0.5 rounded text-[11px] font-semibold"
                      [class.bg-rose-100]="alert.riskLevel === 'Critical'"
                      [class.text-rose-800]="alert.riskLevel === 'Critical'"
                      [class.bg-amber-100]="alert.riskLevel === 'High'"
                      [class.text-amber-800]="alert.riskLevel === 'High'"
                      [class.bg-blue-100]="alert.riskLevel === 'Medium'"
                      [class.text-blue-800]="alert.riskLevel === 'Medium'"
                    >
                      {{ alert.riskLevel }}
                    </span>
                    <span class="font-semibold text-sm text-slate-900">{{ alert.dealName }}</span>
                    <span class="text-xs text-slate-400">({{ alert.accountName }})</span>
                  </div>
                  <p class="text-xs text-slate-600">{{ alert.riskReason }}</p>
                  <p class="text-xs text-indigo-700 font-medium">
                    ⚡ Recommended AI Action: {{ alert.recommendedIntervention }}
                  </p>
                </div>

                <div class="text-right flex md:flex-col items-center md:items-end justify-between md:justify-center">
                  <div class="text-sm font-bold font-mono text-slate-900">\${{ alert.amount | number }}</div>
                  <div class="text-xs text-rose-600 font-semibold mt-0.5">
                    {{ (alert.slippageProbability * 100) | number:'1.0-0' }}% Slippage Probability
                  </div>
                  <div class="text-[11px] text-slate-400">Stalled {{ alert.daysStalled }} days</div>
                </div>
              </div>
            }
          </div>
        </div>
      }

      <!-- Tab 4: Pipeline Deals -->
      @if (activeTab() === 'pipeline') {
        <div class="bg-white border border-slate-200 rounded-xl overflow-hidden shadow-sm" data-testid="pipeline-view">
          <div class="p-4 border-b border-slate-200 flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <h2 class="text-base font-semibold text-slate-900">Simulation Pipeline Opportunities</h2>
            <div class="flex items-center gap-3">
              <label class="text-xs text-slate-500">Filter Stage:</label>
              <select
                [ngModel]="selectedStageFilter()"
                (ngModelChange)="selectedStageFilter.set($event)"
                data-testid="stage-filter-select"
                class="px-2.5 py-1.5 border border-slate-300 rounded text-xs focus:ring-1 focus:ring-indigo-500"
              >
                <option value="All">All Stages</option>
                <option value="Discovery">Discovery</option>
                <option value="Qualification">Qualification</option>
                <option value="ProposalPresented">Proposal Presented</option>
                <option value="ContractNegotiation">Contract Negotiation</option>
                <option value="ClosedWon">Closed Won</option>
              </select>
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-50 text-slate-500 border-b border-slate-200">
                <tr>
                  <th class="px-4 py-3">Opportunity</th>
                  <th class="px-4 py-3">Stage</th>
                  <th class="px-4 py-3">Category</th>
                  <th class="px-4 py-3">Amount</th>
                  <th class="px-4 py-3">Win Prob</th>
                  <th class="px-4 py-3">Min / Likely / Max</th>
                  <th class="px-4 py-3">Distribution</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-100">
                @for (deal of filteredOpportunities(); track deal.id) {
                  <tr class="hover:bg-slate-50 transition-colors">
                    <td class="px-4 py-3">
                      <div class="font-medium text-slate-900">{{ deal.dealName }}</div>
                      <div class="text-[11px] text-slate-400">{{ deal.accountName }} &bull; Age {{ deal.dealAgeDays }}d</div>
                    </td>
                    <td class="px-4 py-3">
                      <span class="inline-flex px-2 py-0.5 rounded text-[11px] font-medium bg-slate-100 text-slate-700">
                        {{ deal.stage }}
                      </span>
                    </td>
                    <td class="px-4 py-3">
                      <span class="inline-flex px-2 py-0.5 rounded text-[11px] font-medium"
                        [class.bg-emerald-100]="deal.category === 'Closed'"
                        [class.text-emerald-800]="deal.category === 'Closed'"
                        [class.bg-indigo-100]="deal.category === 'Commit'"
                        [class.text-indigo-800]="deal.category === 'Commit'"
                        [class.bg-blue-100]="deal.category === 'BestCase'"
                        [class.text-blue-800]="deal.category === 'BestCase'"
                        [class.bg-slate-100]="deal.category === 'Pipeline'"
                        [class.text-slate-800]="deal.category === 'Pipeline'"
                      >
                        {{ deal.category }}
                      </span>
                    </td>
                    <td class="px-4 py-3 font-mono font-semibold text-slate-900">\${{ deal.amount | number }}</td>
                    <td class="px-4 py-3 font-mono">{{ (deal.winProbability * 100) | number:'1.0-0' }}%</td>
                    <td class="px-4 py-3 font-mono text-slate-500">
                      \${{ (deal.minEstimatedValue / 1000) | number:'1.0-0' }}k / \${{ (deal.mostLikelyEstimatedValue / 1000) | number:'1.0-0' }}k / \${{ (deal.maxEstimatedValue / 1000) | number:'1.0-0' }}k
                    </td>
                    <td class="px-4 py-3 text-slate-600">{{ deal.distribution }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <!-- Add Opportunity Modal -->
      @if (showAddModal()) {
        <div class="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div class="flex items-center justify-between border-b pb-3">
              <h3 class="font-bold text-slate-900">Add Pipeline Opportunity</h3>
              <button (click)="closeAddModal()" class="text-slate-400 hover:text-slate-600">&times;</button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block font-medium text-slate-700">Deal Name</label>
                <input [(ngModel)]="newDealName" class="w-full px-3 py-2 border rounded-lg mt-1" />
              </div>
              <div>
                <label class="block font-medium text-slate-700">Account Name</label>
                <input [(ngModel)]="newAccountName" class="w-full px-3 py-2 border rounded-lg mt-1" />
              </div>
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block font-medium text-slate-700">Amount ($)</label>
                  <input type="number" [(ngModel)]="newAmount" class="w-full px-3 py-2 border rounded-lg mt-1" />
                </div>
                <div>
                  <label class="block font-medium text-slate-700">Win Probability (0.0 - 1.0)</label>
                  <input type="number" step="0.05" [(ngModel)]="newWinProb" class="w-full px-3 py-2 border rounded-lg mt-1" />
                </div>
              </div>
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block font-medium text-slate-700">Stage</label>
                  <select [(ngModel)]="newStage" class="w-full px-3 py-2 border rounded-lg mt-1">
                    <option value="Discovery">Discovery</option>
                    <option value="Qualification">Qualification</option>
                    <option value="ProposalPresented">Proposal Presented</option>
                    <option value="ContractNegotiation">Contract Negotiation</option>
                    <option value="ClosedWon">Closed Won</option>
                  </select>
                </div>
                <div>
                  <label class="block font-medium text-slate-700">Category</label>
                  <select [(ngModel)]="newCategory" class="w-full px-3 py-2 border rounded-lg mt-1">
                    <option value="Pipeline">Pipeline</option>
                    <option value="BestCase">Best Case</option>
                    <option value="Commit">Commit</option>
                    <option value="Closed">Closed</option>
                  </select>
                </div>
              </div>
            </div>

            <div class="flex justify-end gap-3 pt-3 border-t">
              <button (click)="closeAddModal()" class="px-4 py-2 border rounded-lg text-slate-700">Cancel</button>
              <button (click)="submitAddOpportunity()" class="px-4 py-2 bg-indigo-600 text-white rounded-lg">Save Deal</button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class RevenueForecastSimulationStudioComponent {
  // Input parameters
  readonly quotaTarget = signal<number>(1800000);
  readonly iterations = signal<number>(5000);
  readonly distributionModel = signal<SimulationDistributionModel>('Triangular');
  readonly marketVolatilityIndex = signal<number>(1.0);
  readonly selectedStageFilter = signal<string>('All');
  readonly isSimulating = signal<boolean>(false);
  readonly activeTab = signal<'simulation' | 'scenario-runner' | 'slippage-radar' | 'pipeline'>('simulation');
  readonly showAddModal = signal<boolean>(false);

  // New Deal form models
  newDealName = 'EMEA Expansion Enterprise';
  newAccountName = 'Global Media Corp';
  newAmount = 250000;
  newWinProb = 0.65;
  newStage: PipelineStage = 'ProposalPresented';
  newCategory: ForecastCategory = 'BestCase';

  // State signals
  readonly opportunities = signal<OpportunitySimulationInput[]>([
    {
      id: 'opp-001',
      dealName: 'Acme Corp Global ERP Modernization',
      accountName: 'Acme Holdings',
      amount: 420000,
      stage: 'ContractNegotiation',
      category: 'Commit',
      winProbability: 0.85,
      targetCloseDate: '2026-11-27',
      dealAgeDays: 45,
      slippageRiskScore: 0.15,
      historicalStageConversionRate: 0.80,
      minEstimatedValue: 380000,
      mostLikelyEstimatedValue: 420000,
      maxEstimatedValue: 450000,
      distribution: 'Triangular'
    },
    {
      id: 'opp-002',
      dealName: 'FinTech Alpha Cloud Migration',
      accountName: 'Alpha Financial Tech',
      amount: 310000,
      stage: 'ProposalPresented',
      category: 'BestCase',
      winProbability: 0.60,
      targetCloseDate: '2026-12-12',
      dealAgeDays: 75,
      slippageRiskScore: 0.58,
      historicalStageConversionRate: 0.45,
      minEstimatedValue: 280000,
      mostLikelyEstimatedValue: 310000,
      maxEstimatedValue: 350000,
      distribution: 'Triangular'
    },
    {
      id: 'opp-003',
      dealName: 'HealthPlus Omnichannel Engagement Hub',
      accountName: 'HealthPlus Systems',
      amount: 260000,
      stage: 'ProposalPresented',
      category: 'BestCase',
      winProbability: 0.50,
      targetCloseDate: '2026-12-17',
      dealAgeDays: 35,
      slippageRiskScore: 0.28,
      historicalStageConversionRate: 0.55,
      minEstimatedValue: 220000,
      mostLikelyEstimatedValue: 260000,
      maxEstimatedValue: 300000,
      distribution: 'Normal'
    },
    {
      id: 'opp-004',
      dealName: 'Global Logistics IoT Track & Trace',
      accountName: 'TransLogistics Group',
      amount: 550000,
      stage: 'ContractNegotiation',
      category: 'Commit',
      winProbability: 0.90,
      targetCloseDate: '2026-11-22',
      dealAgeDays: 90,
      slippageRiskScore: 0.62,
      historicalStageConversionRate: 0.85,
      minEstimatedValue: 500000,
      mostLikelyEstimatedValue: 550000,
      maxEstimatedValue: 600000,
      distribution: 'Triangular'
    },
    {
      id: 'opp-005',
      dealName: 'Nordic Retail AI Demand Forecasting',
      accountName: 'Nordic Retailers Corp',
      amount: 180000,
      stage: 'Qualification',
      category: 'Pipeline',
      winProbability: 0.35,
      targetCloseDate: '2027-01-06',
      dealAgeDays: 20,
      slippageRiskScore: 0.20,
      historicalStageConversionRate: 0.40,
      minEstimatedValue: 150000,
      mostLikelyEstimatedValue: 180000,
      maxEstimatedValue: 210000,
      distribution: 'Triangular'
    },
    {
      id: 'opp-006',
      dealName: 'Sovereign Cloud Data Clean Room',
      accountName: 'GovTech Security Bureau',
      amount: 480000,
      stage: 'ProposalPresented',
      category: 'BestCase',
      winProbability: 0.55,
      targetCloseDate: '2026-12-22',
      dealAgeDays: 110,
      slippageRiskScore: 0.74,
      historicalStageConversionRate: 0.30,
      minEstimatedValue: 400000,
      mostLikelyEstimatedValue: 480000,
      maxEstimatedValue: 520000,
      distribution: 'LogNormal'
    },
    {
      id: 'opp-007',
      dealName: 'Telecom 5G Billing Infrastructure',
      accountName: 'TeleConnect Global',
      amount: 375000,
      stage: 'ContractNegotiation',
      category: 'Commit',
      winProbability: 0.88,
      targetCloseDate: '2026-12-02',
      dealAgeDays: 50,
      slippageRiskScore: 0.12,
      historicalStageConversionRate: 0.78,
      minEstimatedValue: 350000,
      mostLikelyEstimatedValue: 375000,
      maxEstimatedValue: 410000,
      distribution: 'Triangular'
    },
    {
      id: 'opp-008',
      dealName: 'Nexus Payments Real-time Settlement',
      accountName: 'Nexus Payments',
      amount: 225000,
      stage: 'Discovery',
      category: 'Pipeline',
      winProbability: 0.25,
      targetCloseDate: '2027-01-26',
      dealAgeDays: 15,
      slippageRiskScore: 0.18,
      historicalStageConversionRate: 0.35,
      minEstimatedValue: 190000,
      mostLikelyEstimatedValue: 225000,
      maxEstimatedValue: 250000,
      distribution: 'Normal'
    },
    {
      id: 'opp-009',
      dealName: 'CyberShield Threat Hunting Expansion',
      accountName: 'CyberShield Labs',
      amount: 140000,
      stage: 'ClosedWon',
      category: 'Closed',
      winProbability: 1.0,
      targetCloseDate: '2026-11-02',
      dealAgeDays: 30,
      slippageRiskScore: 0.0,
      historicalStageConversionRate: 1.0,
      minEstimatedValue: 140000,
      mostLikelyEstimatedValue: 140000,
      maxEstimatedValue: 140000,
      distribution: 'Triangular'
    },
    {
      id: 'opp-010',
      dealName: 'AeroSpace Defense CAD Integration',
      accountName: 'AeroSystems Ltd',
      amount: 620000,
      stage: 'Discovery',
      category: 'Pipeline',
      winProbability: 0.20,
      targetCloseDate: '2027-02-05',
      dealAgeDays: 85,
      slippageRiskScore: 0.82,
      historicalStageConversionRate: 0.22,
      minEstimatedValue: 500000,
      mostLikelyEstimatedValue: 620000,
      maxEstimatedValue: 750000,
      distribution: 'Triangular'
    }
  ]);

  readonly slippageAlerts = signal<DealSlippageRiskAlert[]>([
    {
      opportunityId: 'opp-006',
      dealName: 'Sovereign Cloud Data Clean Room',
      accountName: 'GovTech Security Bureau',
      amount: 480000,
      stage: 'ProposalPresented',
      slippageProbability: 0.74,
      riskLevel: 'Critical',
      riskReason: 'Deal in ProposalPresented for 110 days (exceeds stage benchmark by 65 days).',
      recommendedIntervention: 'Organize Mutual Action Plan (MAP) sync with VP-level economic buyer.',
      daysStalled: 80
    },
    {
      opportunityId: 'opp-010',
      dealName: 'AeroSpace Defense CAD Integration',
      accountName: 'AeroSystems Ltd',
      amount: 620000,
      stage: 'Discovery',
      slippageProbability: 0.82,
      riskLevel: 'Critical',
      riskReason: 'Deal in Discovery for 85 days (exceeds stage benchmark by 40 days).',
      recommendedIntervention: 'Conduct ICP qualification reset and re-engage business executive sponsor.',
      daysStalled: 55
    },
    {
      opportunityId: 'opp-004',
      dealName: 'Global Logistics IoT Track & Trace',
      accountName: 'TransLogistics Group',
      amount: 550000,
      stage: 'ContractNegotiation',
      slippageProbability: 0.62,
      riskLevel: 'High',
      riskReason: 'Calculated slippage velocity score (0.62) signals stalled stakeholder momentum.',
      recommendedIntervention: 'Initiate redline legal acceleration desk and offer limited-time closing concession.',
      daysStalled: 60
    },
    {
      opportunityId: 'opp-002',
      dealName: 'FinTech Alpha Cloud Migration',
      accountName: 'Alpha Financial Tech',
      amount: 310000,
      stage: 'ProposalPresented',
      slippageProbability: 0.58,
      riskLevel: 'High',
      riskReason: 'Historical stage conversion of 45% is below viable threshold.',
      recommendedIntervention: 'Organize Mutual Action Plan (MAP) sync with VP-level economic buyer.',
      daysStalled: 45
    }
  ]);

  readonly simulationResult = signal<MonteCarloRunResult | null>(null);

  // Computed signals
  readonly filteredOpportunities = computed(() => {
    const filter = this.selectedStageFilter();
    const list = this.opportunities();
    if (filter === 'All') return list;
    return list.filter(o => o.stage === filter);
  });

  readonly totalPipeline = computed(() =>
    this.opportunities().reduce((sum, o) => sum + o.amount, 0)
  );

  readonly criticalAlertsCount = computed(() =>
    this.slippageAlerts().filter(a => a.riskLevel === 'Critical' || a.riskLevel === 'High').length
  );

  readonly criticalSlippageValue = computed(() =>
    this.slippageAlerts().filter(a => a.riskLevel === 'Critical' || a.riskLevel === 'High').reduce((sum, a) => sum + a.amount, 0)
  );

  readonly p10Value = computed(() => this.simulationResult()?.p10 ?? 1620000);
  readonly p50Value = computed(() => this.simulationResult()?.p50 ?? 2180000);
  readonly p90Value = computed(() => this.simulationResult()?.p90 ?? 2740000);
  readonly quotaAttainmentPct = computed(() => this.simulationResult()?.quotaAttainmentProbability ?? 84.5);

  readonly histogramBins = computed(() => {
    const res = this.simulationResult();
    if (res && res.histogramBins.length > 0) return res.histogramBins;
    // Default fallback histogram
    return [
      { rangeMin: 1200000, rangeMax: 1400000, count: 250, densityPercent: 5.0 },
      { rangeMin: 1400000, rangeMax: 1600000, count: 600, densityPercent: 12.0 },
      { rangeMin: 1600000, rangeMax: 1800000, count: 1100, densityPercent: 22.0 },
      { rangeMin: 1800000, rangeMax: 2000000, count: 1400, densityPercent: 28.0 },
      { rangeMin: 2000000, rangeMax: 2200000, count: 900, densityPercent: 18.0 },
      { rangeMin: 2200000, rangeMax: 2400000, count: 450, densityPercent: 9.0 },
      { rangeMin: 2400000, rangeMax: 2600000, count: 200, densityPercent: 4.0 },
      { rangeMin: 2600000, rangeMax: 2800000, count: 80, densityPercent: 1.6 },
      { rangeMin: 2800000, rangeMax: 3000000, count: 20, densityPercent: 0.4 }
    ];
  });

  readonly spectrumStartPercent = computed(() => {
    const total = this.totalPipeline() || 3560000;
    return Math.max(5, Math.min(80, (this.p10Value() / total) * 100));
  });

  readonly spectrumWidthPercent = computed(() => {
    const total = this.totalPipeline() || 3560000;
    const width = ((this.p90Value() - this.p10Value()) / total) * 100;
    return Math.max(15, Math.min(90, width));
  });

  constructor(@Optional() private translationService?: TranslationService) {
    this.runSimulation();
  }

  runSimulation(): void {
    this.isSimulating.set(true);

    const deals = this.opportunities();
    const iters = this.iterations();
    const quota = this.quotaTarget();
    const model = this.distributionModel();
    const vol = this.marketVolatilityIndex();

    const outcomes: number[] = new Array(iters);

    for (let i = 0; i < iters; i++) {
      let simRev = 0;
      for (const d of deals) {
        if (d.stage === 'ClosedLost') continue;
        if (d.stage === 'ClosedWon') {
          simRev += this.sampleDealValue(d, model, vol);
          continue;
        }

        const effectiveProb = d.winProbability * Math.max(0.05, Math.min(0.98, 1.0 - (d.slippageRiskScore * 0.25)));
        if (Math.random() <= effectiveProb) {
          simRev += this.sampleDealValue(d, model, vol);
        }
      }
      outcomes[i] = simRev;
    }

    outcomes.sort((a, b) => a - b);

    const p10 = outcomes[Math.floor(iters * 0.10)] || 0;
    const p50 = outcomes[Math.floor(iters * 0.50)] || 0;
    const p90 = outcomes[Math.floor(iters * 0.90)] || 0;
    const mean = outcomes.reduce((a, b) => a + b, 0) / iters;
    const variance = outcomes.reduce((acc, val) => acc + Math.pow(val - mean, 2), 0) / iters;
    const stdDev = Math.sqrt(variance);

    const hits = outcomes.filter(x => x >= quota).length;
    const quotaProb = (hits / iters) * 100;

    const min = outcomes[0];
    const max = outcomes[outcomes.length - 1];
    const step = (max - min) / 10 || 1;
    const bins: SimulationHistogramBin[] = [];

    for (let b = 0; b < 10; b++) {
      const rMin = min + (b * step);
      const rMax = b === 9 ? max : min + ((b + 1) * step);
      const count = outcomes.filter(x => b === 9 ? (x >= rMin && x <= rMax) : (x >= rMin && x < rMax)).length;
      bins.push({
        rangeMin: rMin,
        rangeMax: rMax,
        count,
        densityPercent: +(count / iters * 100).toFixed(1)
      });
    }

    const expectedVal = deals.reduce((sum, d) => sum + (d.stage === 'ClosedWon' ? d.amount : d.amount * d.winProbability), 0);
    const pipeTotal = deals.reduce((sum, d) => sum + d.amount, 0);
    const commitTotal = deals.filter(d => d.category === 'Commit' || d.category === 'Closed').reduce((sum, d) => sum + d.amount, 0);
    const bestCaseTotal = deals.filter(d => d.category === 'BestCase' || d.category === 'Commit' || d.category === 'Closed').reduce((sum, d) => sum + d.amount, 0);

    this.simulationResult.set({
      iterations: iters,
      p10: Math.round(p10),
      p50: Math.round(p50),
      p90: Math.round(p90),
      mean: Math.round(mean),
      standardDeviation: Math.round(stdDev),
      targetQuota: quota,
      quotaAttainmentProbability: +quotaProb.toFixed(1),
      expectedValue: Math.round(expectedVal),
      pipelineTotalValue: Math.round(pipeTotal),
      commitTotalValue: Math.round(commitTotal),
      bestCaseTotalValue: Math.round(bestCaseTotal),
      percentileDistribution: [10, 20, 30, 40, 50, 60, 70, 80, 90, 99].map(p => Math.round(outcomes[Math.floor(iters * (p / 100))])),
      histogramBins: bins,
      simulatedAtUtc: new Date().toISOString()
    });

    this.isSimulating.set(false);
  }

  private sampleDealValue(deal: OpportunitySimulationInput, defaultModel: SimulationDistributionModel, vol: number): number {
    const min = deal.minEstimatedValue || deal.amount * 0.85;
    const likely = deal.mostLikelyEstimatedValue || deal.amount;
    const max = deal.maxEstimatedValue || deal.amount * 1.15;

    let sample = likely;
    const model = deal.distribution !== 'Triangular' ? deal.distribution : defaultModel;

    if (model === 'Triangular') {
      const u = Math.random();
      const fc = (likely - min) / (max - min || 1);
      sample = u < fc
        ? min + Math.sqrt(u * (max - min) * (likely - min))
        : max - Math.sqrt((1 - u) * (max - min) * (max - likely));
    } else if (model === 'Normal') {
      const u1 = Math.max(1e-6, Math.random());
      const u2 = Math.random();
      const z = Math.sqrt(-2.0 * Math.log(u1)) * Math.cos(2.0 * Math.PI * u2);
      sample = likely + z * ((max - min) / 6 || 1000);
    } else {
      const u1 = Math.max(1e-6, Math.random());
      const u2 = Math.random();
      const z = Math.sqrt(-2.0 * Math.log(u1)) * Math.cos(2.0 * Math.PI * u2);
      const sigma = 0.2;
      const mu = Math.log(likely) - (sigma * sigma / 2);
      sample = Math.exp(mu + sigma * z);
    }

    return Math.max(0, sample * vol);
  }

  getHistogramBarHeight(densityPct: number): number {
    return Math.min(100, Math.max(8, densityPct * 3.2));
  }

  openAddModal(): void {
    this.showAddModal.set(true);
  }

  closeAddModal(): void {
    this.showAddModal.set(false);
  }

  submitAddOpportunity(): void {
    const newDeal: OpportunitySimulationInput = {
      id: `opp-${Date.now().toString(36)}`,
      dealName: this.newDealName,
      accountName: this.newAccountName,
      amount: this.newAmount,
      stage: this.newStage,
      category: this.newCategory,
      winProbability: this.newWinProb,
      targetCloseDate: new Date(Date.now() + 30 * 86400000).toISOString().split('T')[0],
      dealAgeDays: 10,
      slippageRiskScore: 0.15,
      historicalStageConversionRate: 0.60,
      minEstimatedValue: Math.round(this.newAmount * 0.85),
      mostLikelyEstimatedValue: this.newAmount,
      maxEstimatedValue: Math.round(this.newAmount * 1.15),
      distribution: 'Triangular'
    };

    this.opportunities.update(list => [...list, newDeal]);
    this.closeAddModal();
    this.runSimulation();
  }
}
