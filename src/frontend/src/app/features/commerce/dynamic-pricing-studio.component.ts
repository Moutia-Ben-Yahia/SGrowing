import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface PricingArm {
  armId: string;
  price: number;
  alpha: number;
  beta: number;
  impressions: number;
  conversions: number;
  totalRevenue: number;
  conversionRate: number;
  expectedValue: number;
}

export interface PricingExperiment {
  sku: string;
  productName: string;
  category: string;
  baseCostCogs: number;
  shippingAllocated: number;
  minimumMarginHurdle: number;
  standardPrice: number;
  currentStock: number;
  dailyRunRate: number;
  maxMarkdownPct: number;
  markdownStep: number;
  hardMarginFloor: number;
  daysOfSupply: number;
  arms: PricingArm[];
  createdAtUtc: string;
  lastUpdatedUtc: string;
}

export interface ArmSelectionResult {
  sku: string;
  selectedArmId: string;
  proposedPrice: number;
  finalPrice: number;
  floorClampApplied: boolean;
  floorPrice: number;
  sampledTheta: number;
  sampledExpectedRevenue: number;
  allSampledThetas: Record<string, number>;
  timestampUtc: string;
}

export interface PriceElasticityReport {
  sku: string;
  baselinePrice: number;
  targetPrice: number;
  baselineQuantity: number;
  targetQuantity: number;
  elasticityCoefficient: number;
  elasticityClassification: 'Inelastic' | 'Unitary' | 'Elastic' | string;
  recommendedStrategy: 'IncreasePrice' | 'HoldPrice' | 'DiscountToDriveVolume' | string;
  rationale: string;
  calculatedAtUtc: string;
}

export interface MarginFloorCheckResult {
  sku: string;
  candidatePrice: number;
  floorPrice: number;
  baseCostCogs: number;
  minimumMarginHurdle: number;
  shippingAllocated: number;
  effectiveMarginPercent: number;
  floorClampApplied: boolean;
  effectivePrice: number;
  guardrailStatus: string;
}

export interface InventoryLiquidationSchedule {
  sku: string;
  productName: string;
  currentStock: number;
  dailyRunRate: number;
  daysOfSupply: number;
  standardPrice: number;
  hardMarginFloor: number;
  recommendedMarkdownPct: number;
  rawDiscountedPrice: number;
  finalPrice: number;
  liquidationTriggered: boolean;
  marginFloorClamped: boolean;
  liquidationTier: 'Healthy' | 'SlowMoving_60Days' | 'AtRisk_120Days' | 'Critical_180DaysPlus' | string;
  statusMessage: string;
  evaluatedAtUtc: string;
}

@Component({
  selector: 'app-dynamic-pricing-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="min-h-screen bg-slate-950 text-slate-100 p-4 md:p-8 space-y-6">
      <!-- TOP NAVIGATION & HEADER -->
      <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-slate-800 pb-6">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold uppercase tracking-wider rounded-md bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
              Horizon 34
            </span>
            <span class="px-2.5 py-1 text-xs font-semibold uppercase tracking-wider rounded-md bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              Bayesian Thompson Sampling
            </span>
            <span class="px-2.5 py-1 text-xs font-semibold uppercase tracking-wider rounded-md bg-amber-500/20 text-amber-400 border border-amber-500/30">
              Margin Guardrails
            </span>
          </div>
          <h1 class="text-2xl md:text-3xl font-bold tracking-tight text-white mt-2">
            Algorithmic Dynamic Pricing & Multi-Armed Bandit Studio
          </h1>
          <p class="text-sm text-slate-400 mt-1 max-w-3xl">
            Autonomous multi-armed bandit price optimization modeling conversion rates with Beta(α, β) priors,
            arc price elasticity of demand heuristics, hard margin floor protection, and automated inventory liquidation markdowns.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="loadExperiments()"
            [disabled]="isLoading()"
            class="px-4 py-2 bg-slate-900 hover:bg-slate-800 text-slate-300 hover:text-white rounded-lg border border-slate-700 transition flex items-center gap-2 text-sm font-medium disabled:opacity-50"
          >
            <span [class.animate-spin]="isLoading()">🔄</span>
            Refresh Telemetry
          </button>
          <a
            routerLink="/commerce"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-sm font-medium transition shadow-lg shadow-indigo-500/20"
          >
            ← Commerce Hub
          </a>
        </div>
      </div>

      <!-- ERROR ALERT -->
      @if (errorMessage()) {
        <div class="p-4 rounded-xl bg-rose-950/60 border border-rose-800/80 text-rose-200 text-sm flex items-center justify-between">
          <div class="flex items-center gap-2">
            <span>⚠️</span>
            <span>{{ errorMessage() }}</span>
          </div>
          <button (click)="errorMessage.set(null)" class="text-rose-400 hover:text-rose-100 font-bold">✕</button>
        </div>
      }

      <!-- STUDIO TAB NAVIGATION -->
      <div class="flex flex-wrap items-center gap-2 border-b border-slate-800 pb-3">
        <button
          (click)="activeTab.set('bandit')"
          [class]="activeTab() === 'bandit'
            ? 'px-4 py-2 rounded-lg bg-indigo-600 text-white font-medium text-sm transition shadow-md'
            : 'px-4 py-2 rounded-lg bg-slate-900/60 hover:bg-slate-800 text-slate-400 hover:text-slate-200 font-medium text-sm transition border border-slate-800'"
        >
          🎯 Bandit Arms & Simulator
        </button>
        <button
          (click)="activeTab.set('elasticity')"
          [class]="activeTab() === 'elasticity'
            ? 'px-4 py-2 rounded-lg bg-indigo-600 text-white font-medium text-sm transition shadow-md'
            : 'px-4 py-2 rounded-lg bg-slate-900/60 hover:bg-slate-800 text-slate-400 hover:text-slate-200 font-medium text-sm transition border border-slate-800'"
        >
          📊 Price Elasticity Analyzer
        </button>
        <button
          (click)="activeTab.set('guardrails')"
          [class]="activeTab() === 'guardrails'
            ? 'px-4 py-2 rounded-lg bg-indigo-600 text-white font-medium text-sm transition shadow-md'
            : 'px-4 py-2 rounded-lg bg-slate-900/60 hover:bg-slate-800 text-slate-400 hover:text-slate-200 font-medium text-sm transition border border-slate-800'"
        >
          🛡️ Margin Floor Guardrail
        </button>
        <button
          (click)="activeTab.set('liquidation')"
          [class]="activeTab() === 'liquidation'
            ? 'px-4 py-2 rounded-lg bg-indigo-600 text-white font-medium text-sm transition shadow-md'
            : 'px-4 py-2 rounded-lg bg-slate-900/60 hover:bg-slate-800 text-slate-400 hover:text-slate-200 font-medium text-sm transition border border-slate-800'"
        >
          📉 Liquidation Markdown Schedule
        </button>
      </div>

      <!-- SKU SELECTOR RIBBON -->
      <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        @for (exp of experiments(); track exp.sku) {
          <div
            (click)="selectSku(exp.sku)"
            [class]="selectedSku() === exp.sku
              ? 'p-4 rounded-xl cursor-pointer border-2 border-indigo-500 bg-indigo-950/20 shadow-lg shadow-indigo-500/10 transition'
              : 'p-4 rounded-xl cursor-pointer border border-slate-800 bg-slate-900/60 hover:border-slate-700 transition'"
          >
            <div class="flex items-center justify-between text-xs text-slate-400">
              <span>{{ exp.category }}</span>
              <span class="font-mono text-slate-500">{{ exp.sku }}</span>
            </div>
            <h3 class="font-semibold text-white mt-1 line-clamp-1">{{ exp.productName }}</h3>

            <div class="mt-3 flex items-baseline justify-between">
              <div>
                <span class="text-xs text-slate-400">Standard Price:</span>
                <span class="text-lg font-bold text-emerald-400 ml-1.5">\${{ exp.standardPrice | number:'1.2-2' }}</span>
              </div>
              <div class="text-right">
                <span class="text-xs text-slate-400">Hard Floor:</span>
                <span class="text-sm font-semibold text-rose-300 ml-1">\${{ exp.hardMarginFloor | number:'1.2-2' }}</span>
              </div>
            </div>

            <div class="mt-2.5 pt-2 border-t border-slate-800/80 flex items-center justify-between text-xs text-slate-400">
              <span>Stock: <strong class="text-slate-200">{{ exp.currentStock }}</strong> units</span>
              <span>DOS: <strong [class]="getDosColor(exp.daysOfSupply)">{{ exp.daysOfSupply | number:'1.1-1' }}d</strong></span>
            </div>
          </div>
        }
      </div>

      <!-- TAB 1: BANDIT ARMS & SIMULATOR -->
      @if (activeTab() === 'bandit' && currentExperiment()) {
        <div class="space-y-6">
          <!-- SKU SUMMARY METRICS -->
          <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
            <div class="p-3.5 rounded-xl bg-slate-900/70 border border-slate-800">
              <span class="text-xs text-slate-400">Base Cost (COGS)</span>
              <p class="text-lg font-bold text-slate-200 mt-1">\${{ currentExperiment()!.baseCostCogs | number:'1.2-2' }}</p>
            </div>
            <div class="p-3.5 rounded-xl bg-slate-900/70 border border-slate-800">
              <span class="text-xs text-slate-400">Shipping Allocated</span>
              <p class="text-lg font-bold text-slate-200 mt-1">\${{ currentExperiment()!.shippingAllocated | number:'1.2-2' }}</p>
            </div>
            <div class="p-3.5 rounded-xl bg-slate-900/70 border border-slate-800">
              <span class="text-xs text-slate-400">Min Margin Hurdle</span>
              <p class="text-lg font-bold text-amber-400 mt-1">{{ currentExperiment()!.minimumMarginHurdle * 100 | number:'1.0-0' }}%</p>
            </div>
            <div class="p-3.5 rounded-xl bg-slate-900/70 border border-slate-800">
              <span class="text-xs text-slate-400">Hard Margin Floor</span>
              <p class="text-lg font-bold text-rose-400 mt-1">\${{ currentExperiment()!.hardMarginFloor | number:'1.2-2' }}</p>
            </div>
            <div class="p-3.5 rounded-xl bg-slate-900/70 border border-slate-800">
              <span class="text-xs text-slate-400">Daily Run Rate</span>
              <p class="text-lg font-bold text-slate-200 mt-1">{{ currentExperiment()!.dailyRunRate | number:'1.1-1' }} /day</p>
            </div>
            <div class="p-3.5 rounded-xl bg-slate-900/70 border border-slate-800">
              <span class="text-xs text-slate-400">Days of Supply (DOS)</span>
              <p class="text-lg font-bold mt-1" [class]="getDosColor(currentExperiment()!.daysOfSupply)">
                {{ currentExperiment()!.daysOfSupply | number:'1.1-1' }} days
              </p>
            </div>
          </div>

          <!-- PRICING ARMS GRID -->
          <div class="bg-slate-900/80 rounded-xl border border-slate-800 p-5">
            <div class="flex items-center justify-between mb-4">
              <div>
                <h3 class="font-bold text-lg text-white">Multi-Armed Bandit Price Distributions</h3>
                <p class="text-xs text-slate-400">Posterior parameters θ ~ Beta(α, β). Expected Revenue = Price × E[θ].</p>
              </div>
              <button
                (click)="resetPriors()"
                class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white rounded-lg text-xs font-medium border border-slate-700 transition"
              >
                Reset Priors to Beta(1,1)
              </button>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              @for (arm of currentExperiment()!.arms; track arm.armId) {
                <div class="p-4 rounded-xl bg-slate-950/60 border border-slate-800/80 flex flex-col justify-between space-y-3">
                  <div>
                    <div class="flex items-center justify-between">
                      <span class="px-2 py-0.5 text-xs font-mono rounded bg-slate-800 text-slate-300 border border-slate-700">
                        {{ arm.armId }}
                      </span>
                      <span class="text-lg font-extrabold text-emerald-400">\${{ arm.price | number:'1.2-2' }}</span>
                    </div>

                    <div class="mt-3 space-y-1.5 text-xs text-slate-300">
                      <div class="flex justify-between">
                        <span class="text-slate-400">Bayesian Parameters:</span>
                        <span class="font-mono">α = {{ arm.alpha | number:'1.0-0' }}, β = {{ arm.beta | number:'1.0-0' }}</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-slate-400">Conversion Rate:</span>
                        <span class="font-semibold text-sky-400">{{ arm.conversionRate * 100 | number:'1.1-2' }}%</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-slate-400">Expected Value / Visitor:</span>
                        <span class="font-bold text-amber-300">\${{ arm.expectedValue | number:'1.2-2' }}</span>
                      </div>
                      <div class="flex justify-between">
                        <span class="text-slate-400">Volume & Revenue:</span>
                        <span>{{ arm.conversions }} / {{ arm.impressions }} (\${{ arm.totalRevenue | number:'1.0-0' }})</span>
                      </div>
                    </div>

                    <!-- PROGRESS BAR FOR CONVERSION RATE -->
                    <div class="mt-3">
                      <div class="w-full bg-slate-800 rounded-full h-2">
                        <div
                          class="bg-gradient-to-r from-indigo-500 to-sky-400 h-2 rounded-full transition-all duration-300"
                          [style.width.%]="Math.min(100, arm.conversionRate * 300)"
                        ></div>
                      </div>
                    </div>
                  </div>

                  <div class="pt-2 border-t border-slate-800/60 flex items-center gap-2">
                    <button
                      (click)="recordImpression(arm.armId)"
                      class="flex-1 py-1.5 px-2 bg-slate-900 hover:bg-slate-800 text-slate-300 hover:text-white rounded text-xs font-medium border border-slate-700/60 transition"
                      title="Simulate visitor viewing this price without buying"
                    >
                      +1 Imp (β)
                    </button>
                    <button
                      (click)="recordConversion(arm.armId)"
                      class="flex-1 py-1.5 px-2 bg-emerald-950/80 hover:bg-emerald-900 text-emerald-300 hover:text-emerald-100 rounded text-xs font-medium border border-emerald-800/60 transition"
                      title="Simulate visitor buying at this price"
                    >
                      +1 Sale (α)
                    </button>
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- INTERACTIVE THOMPSON SAMPLING SIMULATOR -->
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- TRIGGER CARD -->
            <div class="lg:col-span-1 p-5 rounded-xl bg-slate-900/80 border border-slate-800 flex flex-col justify-between space-y-4">
              <div>
                <span class="text-xs font-semibold uppercase tracking-wider text-indigo-400">Interactive Runtime</span>
                <h3 class="font-bold text-lg text-white mt-1">Thompson Sampling Simulator</h3>
                <p class="text-xs text-slate-400 mt-2 leading-relaxed">
                  Triggers an algorithmic session: draws random conversion probability theta_k ~ Beta(alpha_k, beta_k)
                  for each price arm, chooses k* = argmax(Price_k * theta_k), and verifies against the hard margin floor guardrail.
                </p>
              </div>

              <div class="space-y-3">
                <button
                  (click)="simulateSession()"
                  [disabled]="isSimulating()"
                  class="w-full py-3 px-4 bg-indigo-600 hover:bg-indigo-500 text-white font-bold rounded-xl text-sm transition shadow-lg shadow-indigo-600/30 flex items-center justify-center gap-2 disabled:opacity-50"
                >
                  <span [class.animate-spin]="isSimulating()">🎲</span>
                  Simulate Buyer Arrival & Select Arm
                </button>

                @if (simulationResult()) {
                  <button
                    (click)="recordSimulationSale()"
                    [disabled]="isConverting()"
                    class="w-full py-2.5 px-4 bg-emerald-600 hover:bg-emerald-500 text-white font-bold rounded-xl text-sm transition shadow-lg shadow-emerald-600/30 flex items-center justify-center gap-2 disabled:opacity-50"
                  >
                    <span>💳</span>
                    Convert Sale at Offered Price (\${{ simulationResult()!.finalPrice | number:'1.2-2' }})
                  </button>
                }
              </div>

              @if (conversionSuccessMessage()) {
                <div class="p-3 rounded-lg bg-emerald-950/70 border border-emerald-800 text-emerald-300 text-xs flex items-center gap-2">
                  <span>✅</span>
                  <span>{{ conversionSuccessMessage() }}</span>
                </div>
              }
            </div>

            <!-- SIMULATION OUTPUT CARD -->
            <div class="lg:col-span-2 p-5 rounded-xl bg-slate-900/80 border border-slate-800">
              <h3 class="font-bold text-lg text-white mb-3">Sampling Decision Output</h3>

              @if (simulationResult(); as res) {
                <div class="space-y-4">
                  <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
                    <div class="p-3.5 rounded-xl bg-slate-950/70 border border-slate-800">
                      <span class="text-xs text-slate-400">Selected Pricing Arm</span>
                      <p class="text-xl font-extrabold text-indigo-400 mt-1">{{ res.selectedArmId }}</p>
                    </div>

                    <div class="p-3.5 rounded-xl bg-slate-950/70 border border-slate-800">
                      <span class="text-xs text-slate-400">Proposed vs Final Price</span>
                      <div class="flex items-baseline gap-2 mt-1">
                        <span class="text-xl font-extrabold text-emerald-400">\${{ res.finalPrice | number:'1.2-2' }}</span>
                        @if (res.floorClampApplied) {
                          <span class="text-xs line-through text-slate-500">\${{ res.proposedPrice | number:'1.2-2' }}</span>
                        }
                      </div>
                    </div>

                    <div class="p-3.5 rounded-xl bg-slate-950/70 border border-slate-800">
                      <span class="text-xs text-slate-400">Sampled Expected Rev</span>
                      <p class="text-xl font-extrabold text-amber-300 mt-1">\${{ res.sampledExpectedRevenue | number:'1.2-2' }}</p>
                    </div>
                  </div>

                  <!-- GUARDRAIL CLAMP BADGE -->
                  @if (res.floorClampApplied) {
                    <div class="p-3 rounded-lg bg-rose-950/70 border border-rose-800 text-rose-200 text-xs flex items-center gap-2">
                      <span>🛡️</span>
                      <span>
                        <strong>HARD MARGIN FLOOR CLAMP APPLIED:</strong>
                        The bandit arm proposed \${{ res.proposedPrice | number:'1.2-2' }}, which fell below the minimum margin hurdle floor of
                        \${{ res.floorPrice | number:'1.2-2' }}. The price was clamped to protect unit economics.
                      </span>
                    </div>
                  } @else {
                    <div class="p-3 rounded-lg bg-emerald-950/70 border border-emerald-800 text-emerald-300 text-xs flex items-center gap-2">
                      <span>🛡️</span>
                      <span>
                        <strong>MARGIN FLOOR HURDLE SATISFIED:</strong>
                        Offered price of \${{ res.finalPrice | number:'1.2-2' }} exceeds hard floor threshold of \${{ res.floorPrice | number:'1.2-2' }}.
                      </span>
                    </div>
                  }

                  <!-- SAMPLED THETA COMPARISON -->
                  <div class="p-4 rounded-xl bg-slate-950/60 border border-slate-800">
                    <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">
                      Sampled Conversion Probability θ_k across Arms this Session
                    </span>
                    <div class="mt-3 space-y-2">
                      @for (entry of getThetaEntries(res.allSampledThetas); track entry.armId) {
                        <div class="space-y-1">
                          <div class="flex items-center justify-between text-xs">
                            <span [class.font-bold]="entry.armId === res.selectedArmId" [class.text-indigo-400]="entry.armId === res.selectedArmId">
                              {{ entry.armId }} {{ entry.armId === res.selectedArmId ? '★ (Selected)' : '' }}
                            </span>
                            <span class="font-mono text-slate-300">{{ entry.theta * 100 | number:'1.1-2' }}%</span>
                          </div>
                          <div class="w-full bg-slate-800 rounded-full h-1.5">
                            <div
                              [class]="entry.armId === res.selectedArmId ? 'bg-indigo-500' : 'bg-slate-600'"
                              class="h-1.5 rounded-full transition-all duration-300"
                              [style.width.%]="Math.min(100, entry.theta * 300)"
                            ></div>
                          </div>
                        </div>
                      }
                    </div>
                  </div>
                </div>
              } @else {
                <div class="py-12 text-center text-slate-500">
                  <span class="text-3xl">🎲</span>
                  <p class="mt-2 text-sm">Click "Simulate Buyer Arrival" to execute Bayesian Thompson sampling.</p>
                </div>
              }
            </div>
          </div>
        </div>
      }

      <!-- TAB 2: PRICE ELASTICITY ANALYZER -->
      @if (activeTab() === 'elasticity') {
        <div class="space-y-6">
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- INPUT PANEL -->
            <div class="lg:col-span-1 p-5 rounded-xl bg-slate-900/80 border border-slate-800 space-y-4">
              <div>
                <span class="text-xs font-semibold uppercase tracking-wider text-indigo-400">Heuristic Econometrics</span>
                <h3 class="font-bold text-lg text-white mt-1">Arc Price Elasticity Calculator</h3>
                <p class="text-xs text-slate-400 mt-1">
                  Evaluates price sensitivity using midpoint formula: ε = [(Q1 - Q0) / Q_avg] / [(P1 - P0) / P_avg].
                </p>
              </div>

              <!-- PRESETS -->
              <div class="space-y-2">
                <span class="text-xs font-medium text-slate-400">Quick Test Scenarios:</span>
                <div class="grid grid-cols-1 gap-2">
                  <button
                    (click)="applyElasticityPreset('inelastic')"
                    class="py-1.5 px-3 bg-slate-950 hover:bg-slate-800 text-xs text-slate-300 rounded border border-slate-800 text-left transition"
                  >
                    🟢 Inelastic Scenario (+20% Price, -5% Volume)
                  </button>
                  <button
                    (click)="applyElasticityPreset('unitary')"
                    class="py-1.5 px-3 bg-slate-950 hover:bg-slate-800 text-xs text-slate-300 rounded border border-slate-800 text-left transition"
                  >
                    🔵 Unitary Scenario (+10% Price, -10% Volume)
                  </button>
                  <button
                    (click)="applyElasticityPreset('elastic')"
                    class="py-1.5 px-3 bg-slate-950 hover:bg-slate-800 text-xs text-slate-300 rounded border border-slate-800 text-left transition"
                  >
                    🟠 Elastic Scenario (-15% Price, +45% Volume)
                  </button>
                </div>
              </div>

              <!-- FORM INPUTS -->
              <div class="space-y-3 pt-2 border-t border-slate-800">
                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-xs text-slate-400 mb-1">Baseline Price (P0)</label>
                    <input
                      type="number"
                      [(ngModel)]="elasticityInputs.price0"
                      class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-sm text-white focus:outline-none focus:border-indigo-500"
                    />
                  </div>
                  <div>
                    <label class="block text-xs text-slate-400 mb-1">Target Price (P1)</label>
                    <input
                      type="number"
                      [(ngModel)]="elasticityInputs.price1"
                      class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-sm text-white focus:outline-none focus:border-indigo-500"
                    />
                  </div>
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-xs text-slate-400 mb-1">Baseline Qty (Q0)</label>
                    <input
                      type="number"
                      [(ngModel)]="elasticityInputs.quantity0"
                      class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-sm text-white focus:outline-none focus:border-indigo-500"
                    />
                  </div>
                  <div>
                    <label class="block text-xs text-slate-400 mb-1">Target Qty (Q1)</label>
                    <input
                      type="number"
                      [(ngModel)]="elasticityInputs.quantity1"
                      class="w-full px-3 py-2 bg-slate-950 border border-slate-800 rounded-lg text-sm text-white focus:outline-none focus:border-indigo-500"
                    />
                  </div>
                </div>

                <button
                  (click)="calculateElasticity()"
                  [disabled]="isCalculatingElasticity()"
                  class="w-full py-2.5 px-4 bg-indigo-600 hover:bg-indigo-500 text-white font-bold rounded-lg text-sm transition shadow-lg shadow-indigo-600/30 flex items-center justify-center gap-2 disabled:opacity-50 mt-2"
                >
                  <span [class.animate-spin]="isCalculatingElasticity()">⚙️</span>
                  Analyze Elasticity & Strategy
                </button>
              </div>
            </div>

            <!-- RESULT DISPLAY -->
            <div class="lg:col-span-2 p-5 rounded-xl bg-slate-900/80 border border-slate-800">
              <h3 class="font-bold text-lg text-white mb-4">Elasticity Analysis & Strategic Guidance</h3>

              @if (elasticityReport(); as rep) {
                <div class="space-y-5">
                  <div class="grid grid-cols-1 sm:grid-cols-3 gap-4">
                    <div class="p-4 rounded-xl bg-slate-950/70 border border-slate-800">
                      <span class="text-xs text-slate-400">Elasticity Coefficient (ε)</span>
                      <p class="text-2xl font-black mt-1" [class]="getElasticityColor(rep.elasticityClassification)">
                        {{ rep.elasticityCoefficient | number:'1.3-3' }}
                      </p>
                    </div>

                    <div class="p-4 rounded-xl bg-slate-950/70 border border-slate-800">
                      <span class="text-xs text-slate-400">Demand Classification</span>
                      <div class="mt-1">
                        <span [class]="getElasticityBadge(rep.elasticityClassification)">
                          {{ rep.elasticityClassification }} Demand
                        </span>
                      </div>
                    </div>

                    <div class="p-4 rounded-xl bg-slate-950/70 border border-slate-800">
                      <span class="text-xs text-slate-400">Strategic Recommendation</span>
                      <p class="text-base font-bold text-white mt-1">{{ rep.recommendedStrategy }}</p>
                    </div>
                  </div>

                  <!-- EXPLANATION RATIONALE -->
                  <div class="p-4 rounded-xl bg-slate-950/60 border border-slate-800 space-y-2">
                    <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">
                      Executive Pricing Policy Rationale
                    </span>
                    <p class="text-sm text-slate-300 leading-relaxed">{{ rep.rationale }}</p>
                  </div>

                  <!-- REVENUE IMPACT COMPARISON -->
                  <div class="p-4 rounded-xl bg-slate-950/60 border border-slate-800">
                    <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">
                      Revenue Impact Modeling
                    </span>
                    <div class="grid grid-cols-2 gap-4 mt-3">
                      <div>
                        <span class="text-xs text-slate-500">Baseline Revenue (P0 × Q0):</span>
                        <p class="text-lg font-bold text-slate-200">\${{ rep.baselinePrice * rep.baselineQuantity | number:'1.2-2' }}</p>
                      </div>
                      <div>
                        <span class="text-xs text-slate-500">Projected Revenue (P1 × Q1):</span>
                        <p class="text-lg font-bold text-emerald-400">\${{ rep.targetPrice * rep.targetQuantity | number:'1.2-2' }}</p>
                      </div>
                    </div>
                  </div>
                </div>
              } @else {
                <div class="py-12 text-center text-slate-500">
                  <span class="text-3xl">📊</span>
                  <p class="mt-2 text-sm">Select a preset or enter price/quantity parameters to calculate arc elasticity.</p>
                </div>
              }
            </div>
          </div>
        </div>
      }

      <!-- TAB 3: MARGIN FLOOR GUARDRAIL -->
      @if (activeTab() === 'guardrails') {
        <div class="space-y-6">
          <div class="p-5 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="max-w-2xl">
              <span class="text-xs font-semibold uppercase tracking-wider text-rose-400">Unit Economics Safety</span>
              <h3 class="font-bold text-xl text-white mt-1">Hard Margin Floor Guardrail Engine</h3>
              <p class="text-xs text-slate-400 mt-2 leading-relaxed">
                Guarantees that dynamic pricing never breaches unit profitability. Even during aggressive bandit exploration or deep inventory liquidation markdowns, prices are clamped at the hard margin floor:
              </p>
              <div class="mt-3 p-3 rounded-lg bg-slate-950 font-mono text-xs text-emerald-300 border border-slate-800">
                Price_floor = COGS × (1.0 + MinimumMarginHurdle) + ShippingAllocated
              </div>
            </div>

            <div class="mt-6 grid grid-cols-1 md:grid-cols-3 gap-6">
              <!-- CANDIDATE PRICE TESTER -->
              <div class="p-4 rounded-xl bg-slate-950/60 border border-slate-800 space-y-4">
                <h4 class="font-semibold text-white text-sm">Live Guardrail Simulator</h4>

                <div>
                  <label class="block text-xs text-slate-400 mb-1">Select SKU to Test</label>
                  <select
                    [ngModel]="selectedSku()"
                    (ngModelChange)="selectSku($event)"
                    class="w-full px-3 py-2 bg-slate-900 border border-slate-800 rounded-lg text-sm text-white focus:outline-none focus:border-indigo-500"
                  >
                    @for (exp of experiments(); track exp.sku) {
                      <option [value]="exp.sku">{{ exp.productName }}</option>
                    }
                  </select>
                </div>

                <div>
                  <label class="block text-xs text-slate-400 mb-1">Candidate Price to Evaluate ($)</label>
                  <input
                    type="number"
                    [(ngModel)]="candidatePriceInput"
                    class="w-full px-3 py-2 bg-slate-900 border border-slate-800 rounded-lg text-sm text-white focus:outline-none focus:border-indigo-500"
                  />
                </div>

                <button
                  (click)="evaluateGuardrail()"
                  class="w-full py-2 px-4 bg-indigo-600 hover:bg-indigo-500 text-white font-medium rounded-lg text-sm transition"
                >
                  Evaluate Guardrail Check
                </button>
              </div>

              <!-- GUARDRAIL EVALUATION RESULT -->
              <div class="md:col-span-2 p-4 rounded-xl bg-slate-950/60 border border-slate-800">
                <h4 class="font-semibold text-white text-sm mb-3">Guardrail Safety Verdict</h4>

                @if (guardrailCheck(); as chk) {
                  <div class="space-y-4">
                    <div class="grid grid-cols-2 sm:grid-cols-4 gap-3">
                      <div class="p-3 rounded-lg bg-slate-900/60 border border-slate-800">
                        <span class="text-xs text-slate-400">Candidate Price</span>
                        <p class="text-lg font-bold text-slate-200 mt-1">\${{ chk.candidatePrice | number:'1.2-2' }}</p>
                      </div>
                      <div class="p-3 rounded-lg bg-slate-900/60 border border-slate-800">
                        <span class="text-xs text-slate-400">Hard Floor</span>
                        <p class="text-lg font-bold text-rose-400 mt-1">\${{ chk.floorPrice | number:'1.2-2' }}</p>
                      </div>
                      <div class="p-3 rounded-lg bg-slate-900/60 border border-slate-800">
                        <span class="text-xs text-slate-400">Effective Price</span>
                        <p class="text-lg font-bold text-emerald-400 mt-1">\${{ chk.effectivePrice | number:'1.2-2' }}</p>
                      </div>
                      <div class="p-3 rounded-lg bg-slate-900/60 border border-slate-800">
                        <span class="text-xs text-slate-400">Net Unit Margin</span>
                        <p class="text-lg font-bold text-amber-300 mt-1">{{ chk.effectiveMarginPercent | number:'1.1-1' }}%</p>
                      </div>
                    </div>

                    <div
                      [class]="chk.floorClampApplied
                        ? 'p-4 rounded-xl bg-rose-950/60 border border-rose-800 text-rose-200 text-xs'
                        : 'p-4 rounded-xl bg-emerald-950/60 border border-emerald-800 text-emerald-200 text-xs'"
                    >
                      <div class="font-bold text-sm mb-1">
                        {{ chk.floorClampApplied ? '⛔ PRICE CLAMPED AT HARD MARGIN FLOOR' : '✅ PRICE COMPLIANT WITH MARGIN HURDLE' }}
                      </div>
                      <div>{{ chk.guardrailStatus }}</div>
                    </div>
                  </div>
                } @else {
                  <p class="text-sm text-slate-500 py-6 text-center">Click "Evaluate Guardrail Check" to verify margin safety.</p>
                }
              </div>
            </div>
          </div>
        </div>
      }

      <!-- TAB 4: LIQUIDATION MARKDOWN SCHEDULE -->
      @if (activeTab() === 'liquidation') {
        <div class="space-y-6">
          <div class="bg-slate-900/80 rounded-xl border border-slate-800 p-5">
            <div class="flex flex-col md:flex-row md:items-center justify-between gap-3 mb-4">
              <div>
                <span class="text-xs font-semibold uppercase tracking-wider text-amber-400">Autonomous Inventory Clearing</span>
                <h3 class="font-bold text-lg text-white mt-1">Aging Stock Liquidation Markdown Schedule</h3>
                <p class="text-xs text-slate-400">
                  Computes progressive liquidation discounts when Days of Supply (DOS) exceeds 60 days.
                  Clamped automatically by the hard margin floor to maintain positive unit economics.
                </p>
              </div>
              <button
                (click)="loadLiquidationSchedule()"
                [disabled]="isLoadingLiquidation()"
                class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white rounded-lg text-xs font-medium border border-slate-700 transition"
              >
                Recalculate Schedules
              </button>
            </div>

            <!-- LIQUIDATION TABLE -->
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-950/80 text-slate-400 uppercase tracking-wider border-b border-slate-800">
                  <tr>
                    <th class="py-3 px-4">SKU & Product</th>
                    <th class="py-3 px-4">Stock & Velocity</th>
                    <th class="py-3 px-4">Days of Supply (DOS)</th>
                    <th class="py-3 px-4">Standard Price</th>
                    <th class="py-3 px-4">Recommended Markdown</th>
                    <th class="py-3 px-4">Hard Margin Floor</th>
                    <th class="py-3 px-4">Guarded Final Price</th>
                    <th class="py-3 px-4">Liquidation Tier</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (item of liquidationSchedules(); track item.sku) {
                    <tr class="hover:bg-slate-950/40 transition">
                      <td class="py-3.5 px-4">
                        <div class="font-semibold text-white">{{ item.productName }}</div>
                        <div class="text-slate-500 font-mono text-[10px]">{{ item.sku }}</div>
                      </td>
                      <td class="py-3.5 px-4 text-slate-300">
                        {{ item.currentStock }} units
                        <span class="text-slate-500">({{ item.dailyRunRate | number:'1.1-1' }}/day)</span>
                      </td>
                      <td class="py-3.5 px-4 font-bold" [class]="getDosColor(item.daysOfSupply)">
                        {{ item.daysOfSupply | number:'1.1-1' }} days
                      </td>
                      <td class="py-3.5 px-4 text-slate-300 font-medium">
                        \${{ item.standardPrice | number:'1.2-2' }}
                      </td>
                      <td class="py-3.5 px-4">
                        @if (item.liquidationTriggered) {
                          <span class="px-2 py-0.5 rounded text-amber-400 bg-amber-950/60 border border-amber-800/80 font-bold">
                            -{{ item.recommendedMarkdownPct * 100 | number:'1.1-1' }}%
                          </span>
                        } @else {
                          <span class="text-slate-500">0% (Healthy)</span>
                        }
                      </td>
                      <td class="py-3.5 px-4 text-rose-300 font-mono">
                        \${{ item.hardMarginFloor | number:'1.2-2' }}
                      </td>
                      <td class="py-3.5 px-4">
                        <div class="flex items-center gap-1.5 font-extrabold text-emerald-400">
                          \${{ item.finalPrice | number:'1.2-2' }}
                          @if (item.marginFloorClamped) {
                            <span class="px-1.5 py-0.2 rounded text-[10px] bg-rose-950 text-rose-400 border border-rose-800" title="Clamped at margin floor">
                              CLAMPED
                            </span>
                          }
                        </div>
                      </td>
                      <td class="py-3.5 px-4">
                        <span [class]="getTierBadge(item.liquidationTier)">
                          {{ item.liquidationTier }}
                        </span>
                      </td>
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
export class DynamicPricingStudioComponent implements OnInit {
  private http = inject(HttpClient);
  protected readonly Math = Math;

  // Active Tab
  activeTab = signal<'bandit' | 'elasticity' | 'guardrails' | 'liquidation'>('bandit');

  // Experiments & Selection
  experiments = signal<PricingExperiment[]>([]);
  selectedSku = signal<string>('SKU-SAAS-ENTERPRISE-ADDON');
  isLoading = signal<boolean>(false);
  errorMessage = signal<string | null>(null);

  currentExperiment = computed(() =>
    this.experiments().find(e => e.sku.toLowerCase() === this.selectedSku().toLowerCase()) ?? null
  );

  // Bandit Simulator State
  simulationResult = signal<ArmSelectionResult | null>(null);
  isSimulating = signal<boolean>(false);
  isConverting = signal<boolean>(false);
  conversionSuccessMessage = signal<string | null>(null);

  // Elasticity Analyzer State
  elasticityInputs = {
    price0: 49.0,
    price1: 59.0,
    quantity0: 500,
    quantity1: 410
  };
  elasticityReport = signal<PriceElasticityReport | null>(null);
  isCalculatingElasticity = signal<boolean>(false);

  // Guardrail Safety State
  candidatePriceInput = 20.0;
  guardrailCheck = signal<MarginFloorCheckResult | null>(null);

  // Liquidation Schedule State
  liquidationSchedules = signal<InventoryLiquidationSchedule[]>([]);
  isLoadingLiquidation = signal<boolean>(false);

  ngOnInit(): void {
    this.loadExperiments();
    this.loadLiquidationSchedule();
    this.applyElasticityPreset('inelastic');
  }

  loadExperiments(): void {
    this.isLoading.set(true);
    this.http.get<PricingExperiment[]>('/api/v1/commerce/pricing/bandit').subscribe({
      next: (data) => {
        this.experiments.set(data);
        if (data.length > 0 && !this.selectedSku()) {
          this.selectedSku.set(data[0].sku);
        }
        this.isLoading.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.message || 'Failed to fetch dynamic pricing bandit experiments.');
        this.isLoading.set(false);
      }
    });
  }

  selectSku(sku: string): void {
    this.selectedSku.set(sku);
    this.simulationResult.set(null);
    this.conversionSuccessMessage.set(null);

    const exp = this.currentExperiment();
    if (exp) {
      this.candidatePriceInput = Number((exp.hardMarginFloor * 0.9).toFixed(2));
      this.evaluateGuardrail();
    }
  }

  simulateSession(): void {
    const sku = this.selectedSku();
    if (!sku) return;

    this.isSimulating.set(true);
    this.conversionSuccessMessage.set(null);

    this.http.post<ArmSelectionResult>('/api/v1/commerce/pricing/bandit/select-arm', { sku }).subscribe({
      next: (result) => {
        this.simulationResult.set(result);
        this.isSimulating.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.error || 'Failed to run Thompson sampling simulation.');
        this.isSimulating.set(false);
      }
    });
  }

  recordSimulationSale(): void {
    const sim = this.simulationResult();
    if (!sim) return;

    this.isConverting.set(true);
    this.http.post<PricingExperiment>('/api/v1/commerce/pricing/bandit/record-conversion', {
      sku: sim.sku,
      armId: sim.selectedArmId,
      revenue: sim.finalPrice
    }).subscribe({
      next: (updatedExp) => {
        this.updateLocalExperiment(updatedExp);
        this.conversionSuccessMessage.set(
          `Converted sale at \$${sim.finalPrice.toFixed(2)} on arm ${sim.selectedArmId}! Alpha incremented, Beta updated.`
        );
        this.isConverting.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.error || 'Failed to record conversion.');
        this.isConverting.set(false);
      }
    });
  }

  recordImpression(armId: string): void {
    const sku = this.selectedSku();
    if (!sku) return;

    this.http.post<PricingExperiment>('/api/v1/commerce/pricing/bandit/record-impression', { sku, armId }).subscribe({
      next: (updated) => this.updateLocalExperiment(updated),
      error: (err) => this.errorMessage.set(err.error?.error || 'Failed to record impression.')
    });
  }

  recordConversion(armId: string): void {
    const sku = this.selectedSku();
    if (!sku) return;

    this.http.post<PricingExperiment>('/api/v1/commerce/pricing/bandit/record-conversion', { sku, armId }).subscribe({
      next: (updated) => {
        this.updateLocalExperiment(updated);
        this.conversionSuccessMessage.set(`Manual conversion registered on arm ${armId}!`);
      },
      error: (err) => this.errorMessage.set(err.error?.error || 'Failed to record conversion.')
    });
  }

  resetPriors(): void {
    const sku = this.selectedSku();
    if (!sku) return;

    this.http.post<PricingExperiment>('/api/v1/commerce/pricing/bandit/reset-prior', { sku, alphaPrior: 1.0, betaPrior: 1.0 }).subscribe({
      next: (updated) => {
        this.updateLocalExperiment(updated);
        this.simulationResult.set(null);
        this.conversionSuccessMessage.set('Bayesian priors reset to standard uniform Beta(1,1).');
      },
      error: (err) => this.errorMessage.set(err.error?.error || 'Failed to reset priors.')
    });
  }

  calculateElasticity(): void {
    this.isCalculatingElasticity.set(true);
    this.http.post<PriceElasticityReport>('/api/v1/commerce/pricing/bandit/elasticity', {
      sku: this.selectedSku(),
      price0: this.elasticityInputs.price0,
      price1: this.elasticityInputs.price1,
      quantity0: this.elasticityInputs.quantity0,
      quantity1: this.elasticityInputs.quantity1
    }).subscribe({
      next: (report) => {
        this.elasticityReport.set(report);
        this.isCalculatingElasticity.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.error || 'Failed to calculate elasticity.');
        this.isCalculatingElasticity.set(false);
      }
    });
  }

  applyElasticityPreset(preset: 'inelastic' | 'unitary' | 'elastic'): void {
    if (preset === 'inelastic') {
      this.elasticityInputs = { price0: 100, price1: 120, quantity0: 1000, quantity1: 950 };
    } else if (preset === 'unitary') {
      this.elasticityInputs = { price0: 100, price1: 110, quantity0: 1000, quantity1: 900 };
    } else {
      this.elasticityInputs = { price0: 100, price1: 85, quantity0: 1000, quantity1: 1450 };
    }
    this.calculateElasticity();
  }

  evaluateGuardrail(): void {
    const sku = this.selectedSku();
    if (!sku) return;

    this.http.post<MarginFloorCheckResult>('/api/v1/commerce/pricing/bandit/evaluate-guardrail', {
      sku,
      candidatePrice: this.candidatePriceInput
    }).subscribe({
      next: (chk) => this.guardrailCheck.set(chk),
      error: (err) => this.errorMessage.set(err.error?.error || 'Failed to evaluate guardrail.')
    });
  }

  loadLiquidationSchedule(): void {
    this.isLoadingLiquidation.set(true);
    this.http.post<InventoryLiquidationSchedule[]>('/api/v1/commerce/pricing/bandit/markdown-schedule', {}).subscribe({
      next: (schedules) => {
        this.liquidationSchedules.set(schedules);
        this.isLoadingLiquidation.set(false);
      },
      error: (err) => {
        this.errorMessage.set(err.error?.error || 'Failed to load liquidation schedules.');
        this.isLoadingLiquidation.set(false);
      }
    });
  }

  private updateLocalExperiment(updated: PricingExperiment): void {
    const current = this.experiments();
    const idx = current.findIndex(e => e.sku.toLowerCase() === updated.sku.toLowerCase());
    if (idx !== -1) {
      const copy = [...current];
      copy[idx] = updated;
      this.experiments.set(copy);
    }
  }

  getThetaEntries(record: Record<string, number>): Array<{ armId: string; theta: number }> {
    return Object.entries(record).map(([armId, theta]) => ({ armId, theta }));
  }

  getDosColor(dos: number): string {
    if (dos <= 60) return 'text-emerald-400';
    if (dos <= 120) return 'text-amber-400';
    if (dos <= 180) return 'text-orange-400';
    return 'text-rose-400';
  }

  getTierBadge(tier: string): string {
    switch (tier) {
      case 'Healthy':
        return 'px-2 py-0.5 rounded text-xs font-semibold bg-emerald-950/80 text-emerald-400 border border-emerald-800';
      case 'SlowMoving_60Days':
        return 'px-2 py-0.5 rounded text-xs font-semibold bg-amber-950/80 text-amber-400 border border-amber-800';
      case 'AtRisk_120Days':
        return 'px-2 py-0.5 rounded text-xs font-semibold bg-orange-950/80 text-orange-400 border border-orange-800';
      case 'Critical_180DaysPlus':
        return 'px-2 py-0.5 rounded text-xs font-semibold bg-rose-950/80 text-rose-400 border border-rose-800';
      default:
        return 'px-2 py-0.5 rounded text-xs font-semibold bg-slate-800 text-slate-300';
    }
  }

  getElasticityColor(classification: string): string {
    if (classification === 'Inelastic') return 'text-emerald-400';
    if (classification === 'Unitary') return 'text-sky-400';
    return 'text-amber-400';
  }

  getElasticityBadge(classification: string): string {
    if (classification === 'Inelastic') {
      return 'px-2.5 py-1 text-xs font-bold rounded-lg bg-emerald-950 text-emerald-300 border border-emerald-800';
    }
    if (classification === 'Unitary') {
      return 'px-2.5 py-1 text-xs font-bold rounded-lg bg-sky-950 text-sky-300 border border-sky-800';
    }
    return 'px-2.5 py-1 text-xs font-bold rounded-lg bg-amber-950 text-amber-300 border border-amber-800';
  }
}
