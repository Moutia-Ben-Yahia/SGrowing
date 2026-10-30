import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export enum FunnelStepOutcome {
  Bounce = 0,
  LeadCaptured = 1,
  CheckoutInitiated = 2,
  OrderPaid = 3,
  UpsellAccepted = 4
}

export interface FunnelOutcomeValueConfig {
  bounce: number;
  leadCaptured: number;
  checkoutInitiated: number;
  orderPaid: number;
  upsellAccepted: number;
}

export interface FunnelVariant {
  variantId: string;
  variantName: string;
  description: string;
  isBaseline: boolean;
  isWinner: boolean;
  isCannibalized: boolean;
  alphas: number[];
  initialAlphas: number[];
  totalVisitors: number;
  totalRevenue: number;
  winProbability: number;
  superiorityVsBaseline: number;
  expectedRpv: number;
  credibleIntervalLow: number;
  credibleIntervalHigh: number;
  paidConversionRate: number;
  leadConversionRate: number;
  recommendedTrafficShare: number;
}

export interface FunnelExperiment {
  funnelId: string;
  funnelName: string;
  description: string;
  currency: string;
  outcomeValues: FunnelOutcomeValueConfig;
  explorationFloor: number;
  minVisitorThreshold: number;
  superiorityThreshold: number;
  autoCannibalizationActive: boolean;
  winningVariantId: string | null;
  createdAt: string;
  lastActivityAt: string;
  variants: FunnelVariant[];
}

export interface FunnelExperimentSummary {
  funnelId: string;
  funnelName: string;
  description: string;
  currency: string;
  totalVisitors: number;
  totalRevenue: number;
  variantCount: number;
  autoCannibalizationActive: boolean;
  winningVariantId: string | null;
  lastActivityAt: string;
}

export interface RouteVisitorResult {
  funnelId: string;
  visitorId: string;
  selectedVariantId: string;
  selectedVariantName: string;
  routingReason: string;
  sampledRpvs: Record<string, number>;
  explorationRate: number;
  timestamp: string;
}

export interface RecordOutcomeResult {
  funnelId: string;
  variantId: string;
  outcome: FunnelStepOutcome;
  attributedValue: number;
  totalVisitors: number;
  updatedAlphas: number[];
  expectedRpv: number;
  paidConversionRate: number;
  leadConversionRate: number;
  autoCannibalizationActive: boolean;
  winningVariantId: string | null;
  statusMessage: string;
}

export interface VariantSimulationReport {
  variantId: string;
  variantName: string;
  winProbability: number;
  superiorityVsBaseline: number;
  expectedRpv: number;
  credibleIntervalLow: number;
  credibleIntervalHigh: number;
  totalVisitors: number;
  totalRevenue: number;
  paidConversionRate: number;
  leadConversionRate: number;
  isWinner: boolean;
  isCannibalized: boolean;
  recommendedTrafficShare: number;
}

export interface SimulateSuperiorityResponse {
  funnelId: string;
  iterations: number;
  baselineVariantId: string;
  variants: VariantSimulationReport[];
  autoCannibalizationActive: boolean;
  winningVariantId: string | null;
  decisionSummary: string;
  timestamp: string;
}

@Component({
  selector: 'app-funnel-split-testing-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex flex-col min-h-screen w-full bg-slate-950 text-slate-100 font-sans selection:bg-emerald-500/30 selection:text-emerald-200">
      
      <!-- Top Navigation & Header Bar -->
      <header class="h-16 border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 flex items-center justify-between shrink-0 sticky top-0 z-40">
        <div class="flex items-center gap-3">
          <a routerLink="/funnels" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition-colors">
            ← Funnels
          </a>
          <span class="text-slate-700">|</span>
          <div class="flex items-center gap-2">
            <span class="text-xl">🎯</span>
            <h1 class="text-base font-bold text-white tracking-tight">
              Continuous Split-Testing & Dirichlet Multi-Armed Bandit
            </h1>
          </div>
          <span class="bg-emerald-500/10 text-emerald-400 text-[11px] font-mono px-2.5 py-0.5 rounded-full border border-emerald-500/20 flex items-center gap-1.5 shadow-sm">
            <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
            Horizon 38 • Bayesian Dirichlet-Multinomial
          </span>
        </div>

        <!-- Funnel Selector Dropdown & Actions -->
        <div class="flex items-center gap-3">
          <div class="flex items-center gap-2 bg-slate-800/80 border border-slate-700 rounded-lg px-3 py-1.5">
            <span class="text-xs text-slate-400 font-medium">Funnel:</span>
            <select
              [ngModel]="selectedFunnelId()"
              (ngModelChange)="onSelectFunnel($event)"
              class="bg-transparent text-xs font-semibold text-white focus:outline-none cursor-pointer">
              @for (f of experimentSummaries(); track f.funnelId) {
                <option [value]="f.funnelId" class="bg-slate-900 text-white">
                  {{ f.funnelName }}
                </option>
              }
            </select>
          </div>

          <button
            (click)="runMonteCarloSimulation()"
            [disabled]="isSimulating()"
            class="px-3 py-1.5 bg-violet-600 hover:bg-violet-500 active:bg-violet-700 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-lg shadow-violet-950/30 flex items-center gap-1.5 transition">
            @if (isSimulating()) {
              <span class="animate-spin text-xs">⟳</span>
              Simulating 1,000 Draws...
            } @else {
              <span>⚡</span>
              Run Monte Carlo (1k)
            }
          </button>

          <button
            (click)="resetExperiment()"
            class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 active:bg-slate-900 text-slate-300 rounded-lg text-xs font-medium border border-slate-700 transition">
            Reset Priors
          </button>
        </div>
      </header>

      <!-- Main Content Container -->
      <main class="flex-1 p-6 space-y-6 max-w-7xl mx-auto w-full">

        <!-- Auto-Cannibalization & Bayesian Superiority Banner -->
        @if (currentExperiment()) {
          @if (currentExperiment()!.autoCannibalizationActive) {
            <div class="p-4 rounded-xl bg-gradient-to-r from-emerald-950/70 via-slate-900 to-emerald-950/40 border border-emerald-500/40 flex items-start justify-between shadow-xl shadow-emerald-950/20">
              <div class="flex items-start gap-3.5">
                <div class="p-2.5 rounded-lg bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 text-xl shrink-0">
                  🏆
                </div>
                <div>
                  <div class="flex items-center gap-2">
                    <h3 class="text-sm font-bold text-emerald-300 tracking-wide uppercase">
                      Decisive Auto-Cannibalization Activated
                    </h3>
                    <span class="bg-emerald-500 text-slate-950 text-[10px] font-black uppercase px-2 py-0.5 rounded-full">
                      Winner: Variant {{ currentExperiment()!.winningVariantId }}
                    </span>
                  </div>
                  <p class="text-xs text-emerald-200/80 mt-1 leading-relaxed max-w-4xl">
                    Variant <strong>{{ currentExperiment()!.winningVariantId }}</strong> has crossed the <strong>95% Bayesian superiority threshold</strong> with sufficient visitor sample volume. 
                    Traffic routing has shifted: <strong>95%</strong> of all incoming traffic is channeled into the winning variant, absorbing inferior paths while preserving a 5% exploration floor.
                  </p>
                </div>
              </div>

              <div class="text-right shrink-0">
                <span class="text-[11px] text-slate-400 font-mono block">Winner Traffic Share</span>
                <span class="text-2xl font-black text-emerald-400">95.0%</span>
              </div>
            </div>
          } @else {
            <div class="p-4 rounded-xl bg-gradient-to-r from-amber-950/40 via-slate-900 to-amber-950/20 border border-amber-500/30 flex items-start justify-between shadow-lg">
              <div class="flex items-start gap-3.5">
                <div class="p-2.5 rounded-lg bg-amber-500/20 text-amber-400 border border-amber-500/30 text-xl shrink-0">
                  🔬
                </div>
                <div>
                  <div class="flex items-center gap-2">
                    <h3 class="text-sm font-bold text-amber-300 tracking-wide uppercase">
                      Bayesian Exploration In Progress
                    </h3>
                    <span class="bg-amber-500/20 text-amber-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-amber-500/30">
                      Thompson Sampling Dynamic Routing (ε = 5%)
                    </span>
                  </div>
                  <p class="text-xs text-amber-200/80 mt-1 leading-relaxed max-w-4xl">
                    No variant has decisively cleared ≥ 95% superiority with at least 100 visitors. Traffic is dynamically weighted using Dirichlet-Multinomial Thompson Sampling, allocating traffic in real time to variants with higher posterior probability of superior Revenue Per Visitor (RPV).
                  </p>
                </div>
              </div>

              <div class="text-right shrink-0">
                <span class="text-[11px] text-slate-400 font-mono block">Exploration Floor</span>
                <span class="text-xl font-bold text-amber-400">5.0%</span>
              </div>
            </div>
          }
        }

        <!-- Top Funnel Summary KPI Cards -->
        @if (currentExperiment()) {
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            
            <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 flex flex-col justify-between shadow-sm">
              <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Total Sampled Visitors</span>
              <div class="flex items-baseline justify-between mt-3">
                <h3 class="text-2xl font-bold text-white tracking-tight">{{ totalExperimentVisitors() | number }}</h3>
                <span class="text-xs text-slate-400 font-mono">across {{ currentExperiment()!.variants.length }} arms</span>
              </div>
            </div>

            <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 flex flex-col justify-between shadow-sm">
              <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Gross Attributed Revenue</span>
              <div class="flex items-baseline justify-between mt-3">
                <h3 class="text-2xl font-bold text-emerald-400 tracking-tight">
                  \${{ totalExperimentRevenue() | number:'1.2-2' }}
                </h3>
                <span class="text-xs text-emerald-500/80 font-mono">{{ currentExperiment()!.currency }}</span>
              </div>
            </div>

            <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 flex flex-col justify-between shadow-sm">
              <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Top Performing RPV</span>
              <div class="flex items-baseline justify-between mt-3">
                <h3 class="text-2xl font-bold text-violet-400 tracking-tight">
                  \${{ bestVariantRpv() | number:'1.2-2' }}
                </h3>
                <span class="text-xs text-slate-400">Variant {{ bestVariantId() }}</span>
              </div>
            </div>

            <div class="p-4 rounded-xl bg-slate-900/70 border border-slate-800 flex flex-col justify-between shadow-sm">
              <span class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Model Calibration</span>
              <div class="flex items-baseline justify-between mt-3">
                <h3 class="text-2xl font-bold text-cyan-400 tracking-tight">K = 5 Steps</h3>
                <span class="text-xs text-cyan-500/80 font-mono">Dirichlet(α)</span>
              </div>
            </div>

          </div>
        }

        <!-- 3 Variant Comparison Cards (A, B, C) -->
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
          @for (v of currentExperiment()?.variants; track v.variantId) {
            <div class="rounded-2xl border transition-all duration-300 flex flex-col justify-between shadow-lg relative overflow-hidden"
                 [ngClass]="{
                   'bg-emerald-950/20 border-emerald-500/60 ring-1 ring-emerald-500/40 shadow-emerald-950/20': v.isWinner,
                   'bg-slate-900/70 border-slate-800 hover:border-slate-700': !v.isWinner && !v.isCannibalized,
                   'bg-slate-950/60 border-slate-900 opacity-60 grayscale-[40%]': v.isCannibalized
                 }">

              <!-- Variant Card Header -->
              <div class="p-5 border-b border-slate-800/80">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="w-8 h-8 rounded-lg flex items-center justify-center font-black text-sm"
                          [ngClass]="{
                            'bg-emerald-500 text-slate-950': v.isWinner,
                            'bg-slate-800 text-slate-200 border border-slate-700': !v.isWinner
                          }">
                      {{ v.variantId }}
                    </span>
                    <div>
                      <h4 class="text-sm font-bold text-white tracking-tight">{{ v.variantName }}</h4>
                      <span class="text-[11px] text-slate-400 line-clamp-1">{{ v.description }}</span>
                    </div>
                  </div>

                  <!-- Status Pill -->
                  @if (v.isWinner) {
                    <span class="px-2.5 py-0.5 rounded-full text-[10px] font-black uppercase tracking-wider bg-emerald-500 text-slate-950 shadow-sm animate-pulse">
                      Dominant Winner
                    </span>
                  } @else if (v.isCannibalized) {
                    <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20">
                      Cannibalized
                    </span>
                  } @else if (v.isBaseline) {
                    <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-blue-500/10 text-blue-400 border border-blue-500/20">
                      Baseline Arm
                    </span>
                  } @else {
                    <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-slate-800 text-slate-300 border border-slate-700">
                      Contender
                    </span>
                  }
                </div>

                <!-- Win Probability Meter -->
                <div class="mt-4">
                  <div class="flex justify-between items-center text-xs mb-1.5">
                    <span class="text-slate-400 font-medium">Win Probability:</span>
                    <span class="font-bold text-white font-mono">{{ (v.winProbability * 100) | number:'1.1-1' }}%</span>
                  </div>
                  <div class="w-full bg-slate-800/80 rounded-full h-2 overflow-hidden border border-slate-700/60">
                    <div class="h-full rounded-full transition-all duration-700"
                         [style.width.%]="v.winProbability * 100"
                         [ngClass]="{
                           'bg-emerald-500': v.winProbability >= 0.7,
                           'bg-amber-500': v.winProbability >= 0.3 && v.winProbability < 0.7,
                           'bg-slate-500': v.winProbability < 0.3
                         }">
                    </div>
                  </div>
                </div>
              </div>

              <!-- Metrics Body -->
              <div class="p-5 space-y-4 flex-1">
                <div class="grid grid-cols-2 gap-3">
                  <div class="p-3 rounded-xl bg-slate-950/50 border border-slate-800/70">
                    <span class="text-[11px] text-slate-400 block font-medium">Expected RPV</span>
                    <span class="text-lg font-black text-white mt-0.5 block tracking-tight">
                      \${{ v.expectedRpv | number:'1.2-2' }}
                    </span>
                    <span class="text-[10px] text-slate-500 font-mono block mt-0.5">
                      95% CI: [\${{ v.credibleIntervalLow | number:'1.1-1' }} - \${{ v.credibleIntervalHigh | number:'1.1-1' }}]
                    </span>
                  </div>

                  <div class="p-3 rounded-xl bg-slate-950/50 border border-slate-800/70">
                    <span class="text-[11px] text-slate-400 block font-medium">Traffic Share</span>
                    <span class="text-lg font-black mt-0.5 block tracking-tight"
                          [ngClass]="v.isWinner ? 'text-emerald-400' : 'text-slate-200'">
                      {{ (v.recommendedTrafficShare * 100) | number:'1.1-1' }}%
                    </span>
                    <span class="text-[10px] text-slate-500 font-mono block mt-0.5">
                      {{ v.totalVisitors | number }} visitors
                    </span>
                  </div>
                </div>

                <div class="grid grid-cols-2 gap-3 text-xs">
                  <div class="flex justify-between items-center py-1 border-b border-slate-800/60">
                    <span class="text-slate-400">Paid Conv Rate:</span>
                    <span class="font-bold text-emerald-400 font-mono">{{ (v.paidConversionRate * 100) | number:'1.2-2' }}%</span>
                  </div>
                  <div class="flex justify-between items-center py-1 border-b border-slate-800/60">
                    <span class="text-slate-400">Lead Capture Rate:</span>
                    <span class="font-bold text-cyan-400 font-mono">{{ (v.leadConversionRate * 100) | number:'1.2-2' }}%</span>
                  </div>
                </div>

                @if (!v.isBaseline) {
                  <div class="flex justify-between items-center text-xs py-1.5 px-3 rounded-lg bg-slate-800/30 border border-slate-800">
                    <span class="text-slate-400">P(Superiority vs Baseline A):</span>
                    <span class="font-mono font-bold"
                          [ngClass]="v.superiorityVsBaseline >= 0.95 ? 'text-emerald-400' : 'text-amber-400'">
                      {{ (v.superiorityVsBaseline * 100) | number:'1.1-1' }}%
                    </span>
                  </div>
                }

                <!-- Dirichlet Parameters α breakdown -->
                <div class="pt-2">
                  <span class="text-[10px] text-slate-500 uppercase tracking-wider font-semibold block mb-1.5">
                    Dirichlet Counts α (Bounce, Lead, Cart, Paid, Upsell)
                  </span>
                  <div class="grid grid-cols-5 gap-1 text-center font-mono text-[10px]">
                    <div class="p-1 rounded bg-slate-800/60 text-slate-400 border border-slate-700/40" title="Bounce">
                      <span class="block text-[8px] text-slate-500">BNC</span>
                      {{ v.alphas[0] | number:'1.0-0' }}
                    </div>
                    <div class="p-1 rounded bg-cyan-950/40 text-cyan-300 border border-cyan-800/30" title="Lead Captured">
                      <span class="block text-[8px] text-cyan-500">LEAD</span>
                      {{ v.alphas[1] | number:'1.0-0' }}
                    </div>
                    <div class="p-1 rounded bg-indigo-950/40 text-indigo-300 border border-indigo-800/30" title="Checkout Initiated">
                      <span class="block text-[8px] text-indigo-500">CART</span>
                      {{ v.alphas[2] | number:'1.0-0' }}
                    </div>
                    <div class="p-1 rounded bg-emerald-950/40 text-emerald-300 border border-emerald-800/30" title="Order Paid">
                      <span class="block text-[8px] text-emerald-500">PAID</span>
                      {{ v.alphas[3] | number:'1.0-0' }}
                    </div>
                    <div class="p-1 rounded bg-violet-950/40 text-violet-300 border border-violet-800/30" title="Upsell Accepted">
                      <span class="block text-[8px] text-violet-500">UPSELL</span>
                      {{ v.alphas[4] | number:'1.0-0' }}
                    </div>
                  </div>
                </div>

              </div>

              <!-- Footer with Quick Action -->
              <div class="p-4 bg-slate-950/60 border-t border-slate-800/80 flex items-center justify-between text-xs">
                <span class="text-slate-500 font-mono">Revenue: \${{ v.totalRevenue | number:'1.0-0' }}</span>
                <button
                  (click)="recordManualOutcome(v.variantId, FunnelStepOutcome.OrderPaid)"
                  class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded font-medium transition text-[11px] flex items-center gap-1 border border-slate-700/80">
                  + Record Paid
                </button>
              </div>

            </div>
          }
        </div>

        <!-- Funnel Step Conversion Pipeline Dropoff Visualization -->
        @if (currentExperiment()) {
          <div class="p-6 rounded-2xl bg-slate-900/60 border border-slate-800 shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-sm font-bold text-white tracking-tight flex items-center gap-2">
                  <span>📊</span> Funnel Conversion Dropoff Pipeline & Economic Flow
                </h3>
                <p class="text-xs text-slate-400 mt-0.5">
                  Visualizes cumulative funnel progression across terminal states with economic attribution.
                </p>
              </div>

              <!-- Step Values Pill -->
              <div class="flex items-center gap-2 text-[11px] font-mono text-slate-400">
                <span class="px-2 py-0.5 rounded bg-slate-800 text-slate-300 border border-slate-700">Lead: \${{ currentExperiment()!.outcomeValues.leadCaptured }}</span>
                <span class="px-2 py-0.5 rounded bg-slate-800 text-slate-300 border border-slate-700">Cart: \${{ currentExperiment()!.outcomeValues.checkoutInitiated }}</span>
                <span class="px-2 py-0.5 rounded bg-slate-800 text-slate-300 border border-slate-700">Order: \${{ currentExperiment()!.outcomeValues.orderPaid }}</span>
                <span class="px-2 py-0.5 rounded bg-slate-800 text-slate-300 border border-slate-700">Upsell: \${{ currentExperiment()!.outcomeValues.upsellAccepted }}</span>
              </div>
            </div>

            <!-- Pipeline Comparative Bars -->
            <div class="space-y-4 pt-2">
              @for (v of currentExperiment()!.variants; track v.variantId) {
                <div class="p-3.5 rounded-xl bg-slate-950/60 border border-slate-800/80 space-y-2">
                  <div class="flex justify-between items-center text-xs">
                    <div class="flex items-center gap-2">
                      <span class="font-bold text-white">Variant {{ v.variantId }}: {{ v.variantName }}</span>
                      @if (v.isWinner) {
                        <span class="text-[9px] font-extrabold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 px-1.5 py-0.2 rounded">WINNER</span>
                      }
                    </div>
                    <span class="text-slate-400 font-mono">
                      RPV: <strong class="text-white">\${{ v.expectedRpv | number:'1.2-2' }}</strong> | Conv: <strong class="text-emerald-400">{{ (v.paidConversionRate * 100) | number:'1.1-1' }}%</strong>
                    </span>
                  </div>

                  <!-- Multi-Segment Progression Bar -->
                  <div class="w-full h-4 rounded-lg bg-slate-900 border border-slate-800 flex overflow-hidden">
                    <!-- Bounce -->
                    <div class="bg-rose-950/80 border-r border-slate-950 relative group"
                         [style.width.%]="getStepPercentage(v, 0)"
                         title="Bounce: {{ getStepPercentage(v, 0) | number:'1.1-1' }}%">
                    </div>
                    <!-- Lead Captured -->
                    <div class="bg-cyan-600/70 border-r border-slate-950 relative group"
                         [style.width.%]="getStepPercentage(v, 1)"
                         title="Lead Captured: {{ getStepPercentage(v, 1) | number:'1.1-1' }}%">
                    </div>
                    <!-- Checkout Initiated -->
                    <div class="bg-indigo-600/70 border-r border-slate-950 relative group"
                         [style.width.%]="getStepPercentage(v, 2)"
                         title="Checkout: {{ getStepPercentage(v, 2) | number:'1.1-1' }}%">
                    </div>
                    <!-- Order Paid -->
                    <div class="bg-emerald-500 border-r border-slate-950 relative group"
                         [style.width.%]="getStepPercentage(v, 3)"
                         title="Paid: {{ getStepPercentage(v, 3) | number:'1.1-1' }}%">
                    </div>
                    <!-- Upsell Accepted -->
                    <div class="bg-violet-500 relative group"
                         [style.width.%]="getStepPercentage(v, 4)"
                         title="Upsell: {{ getStepPercentage(v, 4) | number:'1.1-1' }}%">
                    </div>
                  </div>

                  <div class="flex justify-between items-center text-[10px] text-slate-400 font-mono pt-0.5">
                    <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-rose-800"></span> Bounce ({{ getStepPercentage(v, 0) | number:'1.0-0' }}%)</span>
                    <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-cyan-600"></span> Lead ({{ getStepPercentage(v, 1) | number:'1.0-0' }}%)</span>
                    <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-indigo-600"></span> Checkout ({{ getStepPercentage(v, 2) | number:'1.0-0' }}%)</span>
                    <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-emerald-500"></span> Order Paid ({{ getStepPercentage(v, 3) | number:'1.0-0' }}%)</span>
                    <span class="flex items-center gap-1"><span class="w-2 h-2 rounded-full bg-violet-500"></span> Upsell ({{ getStepPercentage(v, 4) | number:'1.0-0' }}%)</span>
                  </div>
                </div>
              }
            </div>
          </div>
        }

        <!-- Interactive Simulator & Live Traffic Routing Engine -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">

          <!-- Left Column: Inbound Visitor Simulator -->
          <div class="lg:col-span-7 p-6 rounded-2xl bg-slate-900/60 border border-slate-800 shadow-xl space-y-5">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-sm font-bold text-white tracking-tight flex items-center gap-2">
                  <span>⚡</span> Live Traffic Routing & Thompson Sampling Simulator
                </h3>
                <p class="text-xs text-slate-400 mt-0.5">
                  Route inbound visitors through Bayesian Thompson sampling and observe arm convergence.
                </p>
              </div>

              <div class="flex items-center gap-2">
                <button
                  (click)="simulateBatchVisitors(25)"
                  [disabled]="isSimulating()"
                  class="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 active:bg-slate-900 text-slate-300 rounded-lg text-xs font-semibold border border-slate-700 transition">
                  Fast Batch (+25)
                </button>

                <button
                  (click)="routeSingleVisitor()"
                  [disabled]="isSimulating()"
                  class="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 active:bg-emerald-700 text-white rounded-lg text-xs font-bold shadow-md shadow-emerald-950/30 flex items-center gap-1.5 transition">
                  <span>👤</span>
                  Route Inbound Visitor
                </button>
              </div>
            </div>

            <!-- Last Routing Result Box -->
            @if (lastRouteResult()) {
              <div class="p-4 rounded-xl bg-slate-950/80 border border-slate-800/80 space-y-3">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2.5">
                    <span class="w-7 h-7 rounded-lg bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 flex items-center justify-center font-black text-sm">
                      {{ lastRouteResult()!.selectedVariantId }}
                    </span>
                    <div>
                      <h4 class="text-xs font-bold text-white">
                        Visitor Routed to Variant {{ lastRouteResult()!.selectedVariantId }}: {{ lastRouteResult()!.selectedVariantName }}
                      </h4>
                      <span class="text-[10px] text-slate-400 font-mono">{{ lastRouteResult()!.visitorId }}</span>
                    </div>
                  </div>

                  <span class="px-2.5 py-0.5 rounded-full text-[10px] font-mono font-semibold"
                        [ngClass]="{
                          'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30': lastRouteResult()!.routingReason === 'CannibalizationFloor',
                          'bg-cyan-500/10 text-cyan-400 border border-cyan-500/30': lastRouteResult()!.routingReason === 'ThompsonSampling',
                          'bg-amber-500/10 text-amber-400 border border-amber-500/30': lastRouteResult()!.routingReason === 'ExplorationFloor'
                        }">
                    {{ lastRouteResult()!.routingReason }}
                  </span>
                </div>

                <!-- Sampled RPVs in this draw -->
                <div class="p-2.5 rounded-lg bg-slate-900/60 border border-slate-800/60">
                  <span class="text-[10px] text-slate-400 font-medium uppercase tracking-wider block mb-1">
                    Dirichlet Sampled RPVs For This Decision:
                  </span>
                  <div class="flex items-center gap-3 text-xs font-mono">
                    @for (entry of getSampledRpvEntries(); track entry.variantId) {
                      <div class="flex items-center gap-1.5"
                           [ngClass]="entry.variantId === lastRouteResult()!.selectedVariantId ? 'text-emerald-400 font-bold' : 'text-slate-400'">
                        <span>Arm {{ entry.variantId }}:</span>
                        <span>\${{ entry.rpv | number:'1.2-2' }}</span>
                      </div>
                    }
                  </div>
                </div>

                <!-- Funnel Step Progression Action Buttons for this visitor -->
                <div class="pt-2 border-t border-slate-800/80">
                  <span class="text-[11px] text-slate-300 font-semibold block mb-2">
                    Advance Visitor Through Funnel Steps (Record Outcome):
                  </span>
                  <div class="grid grid-cols-2 sm:grid-cols-5 gap-2">
                    <button
                      (click)="recordManualOutcome(lastRouteResult()!.selectedVariantId, FunnelStepOutcome.Bounce)"
                      class="px-2 py-1.5 rounded-lg bg-rose-950/40 hover:bg-rose-900/50 text-rose-300 border border-rose-800/40 text-[11px] font-medium transition text-center">
                      Bounce ($0)
                    </button>
                    <button
                      (click)="recordManualOutcome(lastRouteResult()!.selectedVariantId, FunnelStepOutcome.LeadCaptured)"
                      class="px-2 py-1.5 rounded-lg bg-cyan-950/40 hover:bg-cyan-900/50 text-cyan-300 border border-cyan-800/40 text-[11px] font-medium transition text-center">
                      Lead (+\${{ currentExperiment()?.outcomeValues?.leadCaptured }})
                    </button>
                    <button
                      (click)="recordManualOutcome(lastRouteResult()!.selectedVariantId, FunnelStepOutcome.CheckoutInitiated)"
                      class="px-2 py-1.5 rounded-lg bg-indigo-950/40 hover:bg-indigo-900/50 text-indigo-300 border border-indigo-800/40 text-[11px] font-medium transition text-center">
                      Cart (+\${{ currentExperiment()?.outcomeValues?.checkoutInitiated }})
                    </button>
                    <button
                      (click)="recordManualOutcome(lastRouteResult()!.selectedVariantId, FunnelStepOutcome.OrderPaid)"
                      class="px-2 py-1.5 rounded-lg bg-emerald-950/40 hover:bg-emerald-900/50 text-emerald-300 border border-emerald-800/40 text-[11px] font-bold transition text-center shadow-sm">
                      Paid (+\${{ currentExperiment()?.outcomeValues?.orderPaid }})
                    </button>
                    <button
                      (click)="recordManualOutcome(lastRouteResult()!.selectedVariantId, FunnelStepOutcome.UpsellAccepted)"
                      class="px-2 py-1.5 rounded-lg bg-violet-950/40 hover:bg-violet-900/50 text-violet-300 border border-violet-800/40 text-[11px] font-bold transition text-center shadow-sm">
                      Upsell (+\${{ currentExperiment()?.outcomeValues?.upsellAccepted }})
                    </button>
                  </div>
                </div>

              </div>
            } @else {
              <div class="p-8 rounded-xl bg-slate-950/40 border border-dashed border-slate-800 text-center space-y-2">
                <span class="text-3xl block">👥</span>
                <h4 class="text-xs font-semibold text-slate-300">No Visitor Currently in Inbound Flow</h4>
                <p class="text-[11px] text-slate-500 max-w-sm mx-auto">
                  Click "Route Inbound Visitor" above to trigger Dirichlet Thompson sampling and assign an optimal funnel path.
                </p>
              </div>
            }

            @if (actionStatusMessage()) {
              <div class="p-3 rounded-lg bg-emerald-950/30 border border-emerald-500/30 text-emerald-300 text-xs flex items-center gap-2">
                <span>✓</span>
                <span>{{ actionStatusMessage() }}</span>
              </div>
            }
          </div>

          <!-- Right Column: Bayesian Theory & Specification -->
          <div class="lg:col-span-5 p-6 rounded-2xl bg-slate-900/60 border border-slate-800 shadow-xl space-y-4">
            <h3 class="text-sm font-bold text-white tracking-tight flex items-center gap-2">
              <span>📖</span> Mathematical Engine Specifications
            </h3>

            <div class="space-y-3 text-xs text-slate-300 leading-relaxed">
              <div class="p-3 rounded-xl bg-slate-950/60 border border-slate-800/80">
                <h4 class="font-bold text-slate-200">Dirichlet-Multinomial Conjugate Model</h4>
                <p class="text-[11px] text-slate-400 mt-1">
                  Each variant path maintains a vector parameter α<sub>v</sub> = (α<sub>v,1</sub>, ..., α<sub>v,K</sub>). 
                  Upon observing an outcome <em>j</em>, the posterior update is exact and closed-form:
                  <code class="block font-mono text-emerald-400 mt-1 bg-slate-900 px-2 py-0.5 rounded">α[v, j] ← α[v, j] + 1</code>
                </p>
              </div>

              <div class="p-3 rounded-xl bg-slate-950/60 border border-slate-800/80">
                <h4 class="font-bold text-slate-200">Marsaglia & Tsang Gamma Sampling</h4>
                <p class="text-[11px] text-slate-400 mt-1">
                  Samples random variates <em>X<sub>j</sub> ~ Gamma(α<sub>j</sub>, 1)</em> and normalizes across dimensions 
                  to draw exact probability vectors <strong>p<sub>v</sub> ~ Dirichlet(α<sub>v</sub>)</strong> without approximation.
                </p>
              </div>

              <div class="p-3 rounded-xl bg-slate-950/60 border border-slate-800/80">
                <h4 class="font-bold text-slate-200">Auto-Cannibalization Floor Rule</h4>
                <p class="text-[11px] text-slate-400 mt-1">
                  When a variant attains ≥ 95% superiority vs baseline after 100 visitors, traffic is automatically cannibalized 
                  from inferior arms: 95% traffic routed to the winner, with 5% retained for safety exploration.
                </p>
              </div>
            </div>
          </div>

        </div>

      </main>

      <!-- Footer -->
      <footer class="h-12 border-t border-slate-800/80 bg-slate-900/60 px-6 flex items-center justify-between text-xs text-slate-500 shrink-0">
        <div class="flex items-center gap-2">
          <span>SGrowing Autonomous Multi-Armed Funnel Optimization Engine</span>
          <span>•</span>
          <span>Dirichlet-Multinomial Bayesian Bandit</span>
        </div>
        <div>
          <span>Last Activity: {{ currentExperiment()?.lastActivityAt | date:'mediumTime' }}</span>
        </div>
      </footer>

    </div>
  `
})
export class FunnelSplitTestingStudioComponent implements OnInit {
  private readonly http = inject(HttpClient);

  readonly FunnelStepOutcome = FunnelStepOutcome;

  // Signals
  readonly experimentSummaries = signal<FunnelExperimentSummary[]>([]);
  readonly selectedFunnelId = signal<string>('funnel-saas-enterprise');
  readonly currentExperiment = signal<FunnelExperiment | null>(null);
  readonly lastRouteResult = signal<RouteVisitorResult | null>(null);
  readonly isSimulating = signal<boolean>(false);
  readonly actionStatusMessage = signal<string>('');

  // Computed signals
  readonly totalExperimentVisitors = computed(() => {
    const exp = this.currentExperiment();
    if (!exp) return 0;
    return exp.variants.reduce((acc, v) => acc + v.totalVisitors, 0);
  });

  readonly totalExperimentRevenue = computed(() => {
    const exp = this.currentExperiment();
    if (!exp) return 0;
    return exp.variants.reduce((acc, v) => acc + v.totalRevenue, 0);
  });

  readonly bestVariantRpv = computed(() => {
    const exp = this.currentExperiment();
    if (!exp || exp.variants.length === 0) return 0;
    return Math.max(...exp.variants.map(v => v.expectedRpv));
  });

  readonly bestVariantId = computed(() => {
    const exp = this.currentExperiment();
    if (!exp || exp.variants.length === 0) return 'A';
    const best = exp.variants.reduce((prev, curr) => curr.expectedRpv > prev.expectedRpv ? curr : prev);
    return best.variantId;
  });

  ngOnInit(): void {
    this.loadAllExperiments();
  }

  loadAllExperiments(): void {
    this.http.get<FunnelExperimentSummary[]>('/api/v1/funnels/split-testing')
      .subscribe({
        next: (summaries) => {
          this.experimentSummaries.set(summaries);
          if (summaries.length > 0) {
            const currentId = this.selectedFunnelId();
            const exists = summaries.some(s => s.funnelId === currentId);
            const targetId = exists ? currentId : summaries[0].funnelId;
            this.selectedFunnelId.set(targetId);
            this.loadExperimentDetails(targetId);
          }
        },
        error: (err) => console.error('Failed to load funnel experiments:', err)
      });
  }

  loadExperimentDetails(funnelId: string): void {
    this.http.get<FunnelExperiment>(`/api/v1/funnels/split-testing/${funnelId}`)
      .subscribe({
        next: (exp) => {
          this.currentExperiment.set(exp);
        },
        error: (err) => console.error(`Failed to load experiment ${funnelId}:`, err)
      });
  }

  onSelectFunnel(funnelId: string): void {
    this.selectedFunnelId.set(funnelId);
    this.lastRouteResult.set(null);
    this.actionStatusMessage.set('');
    this.loadExperimentDetails(funnelId);
  }

  routeSingleVisitor(): void {
    const funnelId = this.selectedFunnelId();
    this.http.post<RouteVisitorResult>('/api/v1/funnels/split-testing/route-visitor', { funnelId })
      .subscribe({
        next: (res) => {
          this.lastRouteResult.set(res);
          this.actionStatusMessage.set(`Visitor successfully routed to Variant ${res.selectedVariantId} via ${res.routingReason}.`);
        },
        error: (err) => console.error('Failed to route visitor:', err)
      });
  }

  recordManualOutcome(variantId: string, outcome: FunnelStepOutcome): void {
    const funnelId = this.selectedFunnelId();
    const visitorId = this.lastRouteResult()?.visitorId;

    this.http.post<RecordOutcomeResult>('/api/v1/funnels/split-testing/record-outcome', {
      funnelId,
      variantId,
      outcome,
      visitorId
    }).subscribe({
      next: (res) => {
        this.actionStatusMessage.set(res.statusMessage);
        this.loadExperimentDetails(funnelId);
      },
      error: (err) => console.error('Failed to record outcome:', err)
    });
  }

  runMonteCarloSimulation(): void {
    const funnelId = this.selectedFunnelId();
    this.isSimulating.set(true);

    this.http.post<SimulateSuperiorityResponse>('/api/v1/funnels/split-testing/simulate-superiority', {
      funnelId,
      iterations: 1000
    }).subscribe({
      next: (res) => {
        this.isSimulating.set(false);
        this.actionStatusMessage.set(res.decisionSummary);
        this.loadExperimentDetails(funnelId);
      },
      error: (err) => {
        this.isSimulating.set(false);
        console.error('Failed to run Monte Carlo simulation:', err);
      }
    });
  }

  simulateBatchVisitors(count: number): void {
    this.isSimulating.set(true);
    const funnelId = this.selectedFunnelId();
    let completed = 0;

    const outcomesPool = [
      FunnelStepOutcome.Bounce,
      FunnelStepOutcome.Bounce,
      FunnelStepOutcome.LeadCaptured,
      FunnelStepOutcome.CheckoutInitiated,
      FunnelStepOutcome.OrderPaid,
      FunnelStepOutcome.UpsellAccepted
    ];

    for (let i = 0; i < count; i++) {
      this.http.post<RouteVisitorResult>('/api/v1/funnels/split-testing/route-visitor', { funnelId })
        .subscribe({
          next: (route) => {
            const randomOutcome = outcomesPool[Math.floor(Math.random() * outcomesPool.length)];
            this.http.post<RecordOutcomeResult>('/api/v1/funnels/split-testing/record-outcome', {
              funnelId,
              variantId: route.selectedVariantId,
              outcome: randomOutcome
            }).subscribe({
              next: () => {
                completed++;
                if (completed >= count) {
                  this.isSimulating.set(false);
                  this.actionStatusMessage.set(`Simulated ${count} visitors through bandit routing and updated Dirichlet posteriors.`);
                  this.loadExperimentDetails(funnelId);
                }
              },
              error: () => {
                completed++;
                if (completed >= count) {
                  this.isSimulating.set(false);
                  this.loadExperimentDetails(funnelId);
                }
              }
            });
          },
          error: () => {
            completed++;
            if (completed >= count) {
              this.isSimulating.set(false);
              this.loadExperimentDetails(funnelId);
            }
          }
        });
    }
  }

  resetExperiment(): void {
    const funnelId = this.selectedFunnelId();
    this.http.post(`/api/v1/funnels/split-testing/${funnelId}/reset`, {})
      .subscribe({
        next: () => {
          this.lastRouteResult.set(null);
          this.actionStatusMessage.set(`Funnel experiment '${funnelId}' reset back to baseline priors.`);
          this.loadExperimentDetails(funnelId);
        },
        error: (err) => console.error('Failed to reset experiment:', err)
      });
  }

  getStepPercentage(variant: FunnelVariant, stepIdx: number): number {
    const total = variant.alphas.reduce((sum, a) => sum + a, 0);
    if (total <= 0) return 20;
    return Math.max(4, (variant.alphas[stepIdx] / total) * 100);
  }

  getSampledRpvEntries(): { variantId: string; rpv: number }[] {
    const result = this.lastRouteResult();
    if (!result || !result.sampledRpvs) return [];
    return Object.entries(result.sampledRpvs).map(([variantId, rpv]) => ({ variantId, rpv }));
  }
}
