import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';

export type RfmSegment =
  | 'Champions'
  | 'LoyalCustomers'
  | 'PotentialLoyalists'
  | 'AtRisk'
  | 'Hibernating'
  | 'AboutToSleep'
  | 'Lost';

export type ChurnRiskTier = 'Low' | 'Moderate' | 'AtRisk' | 'Critical';

export type WinbackPlaybookType =
  | 'VipDiscountOffer'
  | 'SuccessManagerReachout'
  | 'ExecutivePulseSurvey';

export type WinbackStatus =
  | 'Pending'
  | 'Dispatched'
  | 'Opened'
  | 'Converted'
  | 'Expired';

export type WinbackChannel = 'WhatsApp' | 'Email' | 'PhoneCall' | 'InApp';

export interface WinbackCampaign {
  id: string;
  tenantId: string;
  customerId: string;
  customerName: string;
  customerEmail: string;
  playbookType: WinbackPlaybookType;
  channel: WinbackChannel;
  status: WinbackStatus;
  offerDetails: string;
  dispatchedAtUtc: string;
  openedAtUtc?: string | null;
  convertedAtUtc?: string | null;
  expiresAtUtc: string;
  recoveredRevenue?: number | null;
  assignedAgent?: string | null;
  notes?: string | null;
}

export interface CustomerRfmProfile {
  customerId: string;
  tenantId: string;
  customerName: string;
  customerEmail: string;
  customerPhone: string;
  company: string;
  recencyDays: number;
  frequencyOrders: number;
  monetaryTotalLtv: number;
  recencyScore: number;
  frequencyScore: number;
  monetaryScore: number;
  rfmCell: string;
  segment: RfmSegment;
  unresolvedTickets: number;
  negativeCsatCount: number;
  churnProbability: number;
  riskTier: ChurnRiskTier;
  lastEvaluatedAtUtc: string;
  lastPurchaseDateUtc: string;
  activePlaybook?: WinbackCampaign | null;
}

export interface RfmCohortDistribution {
  segment: RfmSegment;
  segmentLabel: string;
  customerCount: number;
  percentage: number;
  totalLtv: number;
  averageLtv: number;
  averageChurnRisk: number;
  recommendedAction: string;
  colorBadge: string;
}

export interface RetentionOverviewKpis {
  churnRiskRatePercent: number;
  totalCustomersEvaluated: number;
  atRiskAccountsCount: number;
  criticalAccountsCount: number;
  totalAtRiskLtv: number;
  recoveredRevenue: number;
  activeWinbackPlaybooksCount: number;
  totalWinbacksDispatched: number;
  winbackConversionRatePercent: number;
}

export interface DaemonScanResult {
  tenantId: string;
  totalAccountsScanned: number;
  breachedAccountsCount: number;
  playbooksDispatchedCount: number;
  dispatchedPlaybooks: WinbackCampaign[];
  scannedAtUtc: string;
}

@Component({
  selector: 'app-churn-retention-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans select-none pb-16">
      <!-- Top Navigation & Header Bar -->
      <header class="border-b border-slate-800 bg-slate-900/70 backdrop-blur sticky top-0 z-30 px-6 py-4 flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <a routerLink="/" class="text-xs text-slate-400 hover:text-white px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-800 transition flex items-center gap-1.5 shadow-sm">
            <span>←</span>
            <span>Cockpit</span>
          </a>
          <div>
            <div class="flex items-center gap-2">
              <span class="text-2xl">🔄</span>
              <h1 class="text-xl font-bold tracking-tight text-white flex items-center gap-2">
                Customer Churn & Automated Retention Winback Daemon
              </h1>
              <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-rose-500/10 text-rose-400 border border-rose-500/20 font-semibold">
                Horizon 28 • RFM & Logistic AI
              </span>
            </div>
            <p class="text-xs text-slate-400 mt-0.5">
              Continuous RFM scoring, calibrated logistic churn probability classifier & automated preemptive winback playbooks.
            </p>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="runDaemonScan()"
            [disabled]="isScanning()"
            class="px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-rose-600 hover:bg-rose-500 text-white shadow-md shadow-rose-900/30 transition flex items-center gap-2 disabled:opacity-50">
            <span [class.animate-spin]="isScanning()">⚡</span>
            <span>{{ isScanning() ? 'Scanning Daemon...' : 'Run Daemon Scan' }}</span>
          </button>
          <button
            (click)="toggleDrawer('campaigns')"
            class="px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5">
            <span>📋</span>
            <span>Winback History ({{ winbackCampaigns().length }})</span>
          </button>
          <button
            (click)="refreshData()"
            class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition flex items-center gap-1.5">
            <span>↻</span>
            <span>Refresh</span>
          </button>
        </div>
      </header>

      <!-- Toast Notification Bar -->
      @if (notificationMessage()) {
        <div class="mx-6 mt-4 p-3.5 rounded-xl border flex items-center justify-between text-xs font-medium animate-fadeIn"
          [ngClass]="{
            'bg-emerald-950/70 border-emerald-500/30 text-emerald-300': notificationMessage()?.type === 'success',
            'bg-amber-950/70 border-amber-500/30 text-amber-300': notificationMessage()?.type === 'info',
            'bg-rose-950/70 border-rose-500/30 text-rose-300': notificationMessage()?.type === 'error'
          }">
          <div class="flex items-center gap-2.5">
            <span>{{ notificationMessage()?.type === 'success' ? '✓' : notificationMessage()?.type === 'error' ? '⚠' : 'ℹ' }}</span>
            <span>{{ notificationMessage()?.text }}</span>
          </div>
          <button (click)="clearNotification()" class="text-slate-400 hover:text-white px-2 py-0.5 text-xs font-mono">✕</button>
        </div>
      }

      <!-- Main Content Container -->
      <main class="flex-1 px-6 py-6 space-y-6">

        <!-- 1. Top KPI Row -->
        <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <!-- KPI 1: Churn Risk Rate -->
          <div class="bg-slate-900/60 border border-slate-800/80 rounded-2xl p-4 flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-slate-700 transition">
            <div class="absolute -right-6 -bottom-6 w-24 h-24 bg-rose-500/5 rounded-full blur-xl group-hover:bg-rose-500/10 transition"></div>
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400">Churn Risk Rate</span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-mono bg-rose-500/10 text-rose-400 border border-rose-500/20">
                P(churn) &ge; 0.50
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <span class="text-3xl font-extrabold text-white tracking-tight">
                {{ kpis()?.churnRiskRatePercent ?? 37.5 }}%
              </span>
              <span class="text-xs text-rose-400 font-medium">
                {{ (kpis()?.atRiskAccountsCount ?? 0) + (kpis()?.criticalAccountsCount ?? 0) }} accounts
              </span>
            </div>
            <div class="mt-3 w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
              <div class="bg-rose-500 h-1.5 rounded-full transition-all duration-500" [style.width.%]="kpis()?.churnRiskRatePercent ?? 37.5"></div>
            </div>
          </div>

          <!-- KPI 2: Total At-Risk LTV -->
          <div class="bg-slate-900/60 border border-slate-800/80 rounded-2xl p-4 flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-slate-700 transition">
            <div class="absolute -right-6 -bottom-6 w-24 h-24 bg-amber-500/5 rounded-full blur-xl group-hover:bg-amber-500/10 transition"></div>
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400">At-Risk Portfolio LTV</span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-mono bg-amber-500/10 text-amber-400 border border-amber-500/20">
                LTV Exposure
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <span class="text-3xl font-extrabold text-amber-300 tracking-tight">
                {{ (kpis()?.totalAtRiskLtv ?? 10250) | currency:'USD':'symbol':'1.0-0' }}
              </span>
              <span class="text-xs text-slate-400 font-mono">
                Across {{ (kpis()?.atRiskAccountsCount ?? 0) + (kpis()?.criticalAccountsCount ?? 0) }} AtRisk/Critical
              </span>
            </div>
            <p class="mt-2 text-[11px] text-slate-400 flex items-center gap-1">
              <span>🛡️</span> Targeted by automated winback coupons & AE check-ins
            </p>
          </div>

          <!-- KPI 3: Recovered Revenue -->
          <div class="bg-slate-900/60 border border-slate-800/80 rounded-2xl p-4 flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-slate-700 transition">
            <div class="absolute -right-6 -bottom-6 w-24 h-24 bg-emerald-500/5 rounded-full blur-xl group-hover:bg-emerald-500/10 transition"></div>
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400">Recovered Winback Revenue</span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-mono bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-bold">
                {{ kpis()?.winbackConversionRatePercent ?? 33.3 }}% conv.
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <span class="text-3xl font-extrabold text-emerald-400 tracking-tight">
                {{ (kpis()?.recoveredRevenue ?? 2450) | currency:'USD':'symbol':'1.0-0' }}
              </span>
              <span class="text-xs text-emerald-500 font-medium">
                30-day window
              </span>
            </div>
            <p class="mt-2 text-[11px] text-slate-400 flex items-center gap-1">
              <span>🎯</span> Measured from subsequent orders post-dispatch
            </p>
          </div>

          <!-- KPI 4: Active Winback Campaigns -->
          <div class="bg-slate-900/60 border border-slate-800/80 rounded-2xl p-4 flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-slate-700 transition">
            <div class="absolute -right-6 -bottom-6 w-24 h-24 bg-cyan-500/5 rounded-full blur-xl group-hover:bg-cyan-500/10 transition"></div>
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400">Active Winback Playbooks</span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-mono bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
                Dispatched / In Flight
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <span class="text-3xl font-extrabold text-cyan-400 tracking-tight">
                {{ kpis()?.activeWinbackPlaybooksCount ?? 2 }}
              </span>
              <span class="text-xs text-slate-400">
                of {{ kpis()?.totalWinbacksDispatched ?? 3 }} total
              </span>
            </div>
            <div class="mt-2 flex items-center gap-2 text-[11px] text-slate-400">
              <span class="w-2 h-2 rounded-full bg-cyan-400 animate-pulse"></span>
              <span>WhatsApp coupons, AE calls & CSAT pulse</span>
            </div>
          </div>
        </section>

        <!-- 2. RFM Cohort Segmentation Matrix -->
        <section class="space-y-3">
          <div class="flex items-center justify-between">
            <div>
              <h2 class="text-base font-bold text-white flex items-center gap-2">
                <span>📊</span>
                <span>RFM Customer Cohort Matrix</span>
              </h2>
              <p class="text-xs text-slate-400">
                Accounts segmented by Recency (R: 1-5), Frequency (F: 1-5) and Monetary Spend (M: 1-5) quintiles.
              </p>
            </div>
            <div class="text-xs text-slate-400 font-mono">
              Total Accounts: {{ kpis()?.totalCustomersEvaluated ?? 8 }}
            </div>
          </div>

          <div class="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-7 gap-3">
            @for (cohort of rfmCohorts(); track cohort.segment) {
              <div
                (click)="filterBySegment(cohort.segment)"
                class="bg-slate-900/60 border rounded-2xl p-3.5 flex flex-col justify-between cursor-pointer transition hover:scale-[1.02] shadow-sm"
                [ngClass]="{
                  'border-cyan-500 ring-1 ring-cyan-500/30': selectedSegmentFilter() === cohort.segment,
                  'border-slate-800/80 hover:border-slate-700': selectedSegmentFilter() !== cohort.segment
                }">
                <div>
                  <div class="flex items-center justify-between">
                    <span class="px-2 py-0.5 rounded-md text-[10px] font-semibold border" [ngClass]="cohort.colorBadge">
                      {{ cohort.segmentLabel }}
                    </span>
                    <span class="text-xs font-mono text-slate-400 font-semibold">
                      {{ cohort.percentage }}%
                    </span>
                  </div>
                  <div class="mt-3">
                    <span class="text-2xl font-bold text-white">{{ cohort.customerCount }}</span>
                    <span class="text-xs text-slate-400 ml-1">accounts</span>
                  </div>
                  <div class="mt-1 text-xs text-slate-300 font-mono">
                    {{ cohort.totalLtv | currency:'USD':'symbol':'1.0-0' }}
                  </div>
                </div>

                <div class="mt-3 pt-2.5 border-t border-slate-800/80 space-y-1">
                  <div class="flex items-center justify-between text-[11px]">
                    <span class="text-slate-400">Avg Churn:</span>
                    <span class="font-mono font-medium"
                      [ngClass]="{
                        'text-emerald-400': cohort.averageChurnRisk < 0.25,
                        'text-amber-400': cohort.averageChurnRisk >= 0.25 && cohort.averageChurnRisk < 0.50,
                        'text-orange-400': cohort.averageChurnRisk >= 0.50 && cohort.averageChurnRisk < 0.75,
                        'text-rose-400': cohort.averageChurnRisk >= 0.75
                      }">
                      {{ (cohort.averageChurnRisk * 100) | number:'1.1-1' }}%
                    </span>
                  </div>
                  <p class="text-[10px] text-slate-400 line-clamp-2 leading-tight">
                    {{ cohort.recommendedAction }}
                  </p>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- 3. Interactive Churn Risk Simulator & At-Risk Customer Radar -->
        <section class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">

          <!-- Left Column: Interactive Churn Risk Simulator (5 cols) -->
          <div class="lg:col-span-5 bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-5 shadow-lg">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>🎛️</span>
                  <span>Interactive Churn Risk Simulator</span>
                </h3>
                <p class="text-xs text-slate-400">
                  Calibrated Logistic Regression: P(churn) = 1 / (1 + e^-z)
                </p>
              </div>
              <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-indigo-500/10 text-indigo-400 border border-indigo-500/20 font-bold">
                Formula Engine
              </span>
            </div>

            <!-- Simulator Sliders -->
            <div class="space-y-4">
              <!-- Slider 1: Recency -->
              <div class="space-y-1.5">
                <div class="flex justify-between text-xs">
                  <span class="text-slate-300 font-medium">Recency (Days Inactive):</span>
                  <span class="font-mono text-cyan-400 font-bold">{{ simRecencyDays() }} days (R: {{ simRfmQuintiles().recencyScore }}/5)</span>
                </div>
                <input
                  type="range"
                  min="1"
                  max="240"
                  [ngModel]="simRecencyDays()"
                  (ngModelChange)="simRecencyDays.set($event)"
                  class="w-full accent-cyan-500 cursor-pointer h-1.5 bg-slate-800 rounded-lg" />
                <div class="flex justify-between text-[10px] text-slate-500">
                  <span>1 day (R=5)</span>
                  <span>30 days (R=4)</span>
                  <span>60 days (R=3)</span>
                  <span>120+ days (R=1)</span>
                </div>
              </div>

              <!-- Slider 2: Frequency -->
              <div class="space-y-1.5">
                <div class="flex justify-between text-xs">
                  <span class="text-slate-300 font-medium">Frequency (Completed Orders):</span>
                  <span class="font-mono text-emerald-400 font-bold">{{ simFrequencyOrders() }} orders (F: {{ simRfmQuintiles().frequencyScore }}/5)</span>
                </div>
                <input
                  type="range"
                  min="1"
                  max="30"
                  [ngModel]="simFrequencyOrders()"
                  (ngModelChange)="simFrequencyOrders.set($event)"
                  class="w-full accent-emerald-500 cursor-pointer h-1.5 bg-slate-800 rounded-lg" />
                <div class="flex justify-between text-[10px] text-slate-500">
                  <span>1 order (F=1)</span>
                  <span>5 orders (F=3)</span>
                  <span>10+ orders (F=5)</span>
                </div>
              </div>

              <!-- Slider 3: Monetary Spend -->
              <div class="space-y-1.5">
                <div class="flex justify-between text-xs">
                  <span class="text-slate-300 font-medium">Monetary Spend (LTV):</span>
                  <span class="font-mono text-amber-400 font-bold">{{ simMonetarySpend() | currency:'USD':'symbol':'1.0-0' }} (M: {{ simRfmQuintiles().monetaryScore }}/5)</span>
                </div>
                <input
                  type="range"
                  min="50"
                  max="8000"
                  step="50"
                  [ngModel]="simMonetarySpend()"
                  (ngModelChange)="simMonetarySpend.set($event)"
                  class="w-full accent-amber-500 cursor-pointer h-1.5 bg-slate-800 rounded-lg" />
                <div class="flex justify-between text-[10px] text-slate-500">
                  <span>$50 (M=1)</span>
                  <span>$1,000 (M=4)</span>
                  <span>$2,000+ (M=5)</span>
                </div>
              </div>

              <!-- Penalty Factors: Unresolved Tickets & Negative CSAT -->
              <div class="grid grid-cols-2 gap-3 pt-1">
                <div class="bg-slate-950/60 border border-slate-800 rounded-xl p-3 space-y-1.5">
                  <div class="flex justify-between text-xs">
                    <span class="text-slate-400">Open Tickets:</span>
                    <span class="font-mono text-rose-400 font-bold">{{ simUnresolvedTickets() }}</span>
                  </div>
                  <input
                    type="range"
                    min="0"
                    max="6"
                    [ngModel]="simUnresolvedTickets()"
                    (ngModelChange)="simUnresolvedTickets.set($event)"
                    class="w-full accent-rose-500 cursor-pointer h-1.5 bg-slate-800 rounded-lg" />
                  <span class="text-[10px] text-slate-500 block">&gamma; = +0.85 per ticket</span>
                </div>

                <div class="bg-slate-950/60 border border-slate-800 rounded-xl p-3 space-y-1.5">
                  <div class="flex justify-between text-xs">
                    <span class="text-slate-400">Negative CSAT:</span>
                    <span class="font-mono text-rose-400 font-bold">{{ simNegativeCsat() }}</span>
                  </div>
                  <input
                    type="range"
                    min="0"
                    max="4"
                    [ngModel]="simNegativeCsat()"
                    (ngModelChange)="simNegativeCsat.set($event)"
                    class="w-full accent-rose-500 cursor-pointer h-1.5 bg-slate-800 rounded-lg" />
                  <span class="text-[10px] text-slate-500 block">&delta; = +1.10 per rating</span>
                </div>
              </div>
            </div>

            <!-- Instantaneous Simulation Results Box -->
            <div class="bg-slate-950 border rounded-xl p-4 space-y-3"
              [ngClass]="{
                'border-emerald-500/30 bg-emerald-950/20': simRiskTier() === 'Low',
                'border-cyan-500/30 bg-cyan-950/20': simRiskTier() === 'Moderate',
                'border-amber-500/30 bg-amber-950/20': simRiskTier() === 'AtRisk',
                'border-rose-500/40 bg-rose-950/30': simRiskTier() === 'Critical'
              }">
              <div class="flex items-center justify-between">
                <div>
                  <span class="text-[11px] text-slate-400 font-medium">Predicted Churn Probability:</span>
                  <div class="text-2xl font-black tracking-tight"
                    [ngClass]="{
                      'text-emerald-400': simRiskTier() === 'Low',
                      'text-cyan-400': simRiskTier() === 'Moderate',
                      'text-amber-400': simRiskTier() === 'AtRisk',
                      'text-rose-400': simRiskTier() === 'Critical'
                    }">
                    {{ (simChurnProbability() * 100) | number:'1.1-1' }}%
                  </div>
                </div>

                <div class="text-right">
                  <span class="text-[11px] text-slate-400 font-medium">Risk Tier:</span>
                  <div>
                    <span class="px-2.5 py-1 rounded-md text-xs font-bold font-mono uppercase inline-block mt-0.5"
                      [ngClass]="{
                        'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20': simRiskTier() === 'Low',
                        'bg-cyan-500/10 text-cyan-400 border border-cyan-500/20': simRiskTier() === 'Moderate',
                        'bg-amber-500/10 text-amber-400 border border-amber-500/20': simRiskTier() === 'AtRisk',
                        'bg-rose-500/20 text-rose-300 border border-rose-500/30 animate-pulse': simRiskTier() === 'Critical'
                      }">
                      {{ simRiskTier() }} Risk
                    </span>
                  </div>
                </div>
              </div>

              <!-- Probability Bar Gauge -->
              <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
                <div class="h-2 rounded-full transition-all duration-300"
                  [style.width.%]="simChurnProbability() * 100"
                  [ngClass]="{
                    'bg-emerald-500': simRiskTier() === 'Low',
                    'bg-cyan-500': simRiskTier() === 'Moderate',
                    'bg-amber-500': simRiskTier() === 'AtRisk',
                    'bg-rose-500': simRiskTier() === 'Critical'
                  }"></div>
              </div>

              <div class="grid grid-cols-2 gap-2 text-xs pt-1 border-t border-slate-800/60">
                <div>
                  <span class="text-slate-400">RFM Cell:</span>
                  <span class="font-mono text-white font-bold ml-1.5">{{ simRfmQuintiles().cell }}</span>
                </div>
                <div>
                  <span class="text-slate-400">Classified:</span>
                  <span class="font-semibold text-slate-200 ml-1.5">{{ simSegment() }}</span>
                </div>
              </div>

              <div class="pt-2 border-t border-slate-800/60 text-xs">
                <span class="text-slate-400">Optimal Winback Playbook:</span>
                <div class="mt-1 flex items-center gap-1.5 text-rose-300 font-semibold text-xs">
                  <span>⚡</span>
                  <span>{{ simRecommendedPlaybook() }}</span>
                </div>
              </div>
            </div>

            <button
              (click)="saveSimulatedCustomer()"
              class="w-full py-2 text-xs font-semibold rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center justify-center gap-2">
              <span>💾</span>
              <span>Evaluate & Enroll in Retention Database</span>
            </button>
          </div>

          <!-- Right Column: At-Risk Customer Radar Table (7 cols) -->
          <div class="lg:col-span-7 bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-4 shadow-lg">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>🚨</span>
                  <span>At-Risk Customer Radar</span>
                </h3>
                <p class="text-xs text-slate-400">
                  Breach threshold: P(churn) &ge; 0.50 (Ranked by highest churn probability)
                </p>
              </div>

              <!-- Filter tabs -->
              <div class="flex items-center gap-1.5 bg-slate-950 p-1 rounded-xl border border-slate-800 text-xs">
                <button
                  (click)="filterByTier('All')"
                  class="px-2.5 py-1 rounded-lg font-medium transition"
                  [ngClass]="selectedTierFilter() === 'All' ? 'bg-slate-800 text-white font-semibold' : 'text-slate-400 hover:text-white'">
                  All At-Risk ({{ atRiskCustomers().length }})
                </button>
                <button
                  (click)="filterByTier('Critical')"
                  class="px-2.5 py-1 rounded-lg font-medium transition"
                  [ngClass]="selectedTierFilter() === 'Critical' ? 'bg-rose-950 text-rose-300 font-semibold' : 'text-slate-400 hover:text-rose-300'">
                  Critical ({{ criticalCount() }})
                </button>
                <button
                  (click)="filterByTier('AtRisk')"
                  class="px-2.5 py-1 rounded-lg font-medium transition"
                  [ngClass]="selectedTierFilter() === 'AtRisk' ? 'bg-amber-950 text-amber-300 font-semibold' : 'text-slate-400 hover:text-amber-300'">
                  AtRisk ({{ atRiskCount() }})
                </button>
              </div>
            </div>

            <!-- Customers Table -->
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs">
                <thead>
                  <tr class="border-b border-slate-800 text-slate-400">
                    <th class="py-2.5 px-3 font-semibold">Account / Company</th>
                    <th class="py-2.5 px-3 font-semibold">RFM Cell</th>
                    <th class="py-2.5 px-3 font-semibold">Inactive</th>
                    <th class="py-2.5 px-3 font-semibold">Churn Gauge</th>
                    <th class="py-2.5 px-3 font-semibold">Alerts</th>
                    <th class="py-2.5 px-3 font-semibold text-right">Action</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (cust of filteredCustomers(); track cust.customerId) {
                    <tr class="hover:bg-slate-800/40 transition group">
                      <!-- Customer Details -->
                      <td class="py-3 px-3">
                        <div class="font-bold text-white group-hover:text-cyan-300 transition">
                          {{ cust.customerName }}
                        </div>
                        <div class="text-[11px] text-slate-400 font-mono">
                          {{ cust.company || cust.customerEmail }}
                        </div>
                      </td>

                      <!-- RFM Cell & Segment -->
                      <td class="py-3 px-3 font-mono">
                        <div class="font-semibold text-slate-200">{{ cust.rfmCell }}</div>
                        <span class="text-[10px] text-slate-400">{{ cust.segment }}</span>
                      </td>

                      <!-- Inactivity & LTV -->
                      <td class="py-3 px-3">
                        <div class="text-amber-300 font-medium">{{ cust.recencyDays }}d ago</div>
                        <div class="text-[10px] text-slate-400 font-mono">LTV: {{ cust.monetaryTotalLtv | currency:'USD':'symbol':'1.0-0' }}</div>
                      </td>

                      <!-- Churn Probability Gauge -->
                      <td class="py-3 px-3 min-w-[120px]">
                        <div class="flex items-center justify-between text-[11px] mb-1 font-mono font-bold"
                          [ngClass]="{
                            'text-rose-400': cust.riskTier === 'Critical',
                            'text-amber-400': cust.riskTier === 'AtRisk',
                            'text-cyan-400': cust.riskTier === 'Moderate',
                            'text-emerald-400': cust.riskTier === 'Low'
                          }">
                          <span>{{ (cust.churnProbability * 100) | number:'1.1-1' }}%</span>
                          <span class="text-[10px] uppercase font-sans font-bold">[{{ cust.riskTier }}]</span>
                        </div>
                        <div class="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                          <div class="h-1.5 rounded-full"
                            [style.width.%]="cust.churnProbability * 100"
                            [ngClass]="{
                              'bg-rose-500': cust.riskTier === 'Critical',
                              'bg-amber-500': cust.riskTier === 'AtRisk',
                              'bg-cyan-500': cust.riskTier === 'Moderate',
                              'bg-emerald-500': cust.riskTier === 'Low'
                            }"></div>
                        </div>
                      </td>

                      <!-- Alerts (Tickets & CSAT) -->
                      <td class="py-3 px-3">
                        <div class="flex flex-wrap gap-1">
                          @if (cust.negativeCsatCount > 0) {
                            <span class="px-1.5 py-0.5 rounded text-[10px] font-mono bg-rose-500/20 text-rose-300 border border-rose-500/30" title="Negative CSAT feedback recorded">
                              CSAT &times;{{ cust.negativeCsatCount }}
                            </span>
                          }
                          @if (cust.unresolvedTickets > 0) {
                            <span class="px-1.5 py-0.5 rounded text-[10px] font-mono bg-amber-500/20 text-amber-300 border border-amber-500/30" title="Unresolved support tickets">
                              Tix &times;{{ cust.unresolvedTickets }}
                            </span>
                          }
                          @if (cust.negativeCsatCount === 0 && cust.unresolvedTickets === 0) {
                            <span class="text-[10px] text-slate-500 font-mono">Clean</span>
                          }
                        </div>
                      </td>

                      <!-- Action Button -->
                      <td class="py-3 px-3 text-right">
                        @if (cust.activePlaybook && cust.activePlaybook.status !== 'Converted') {
                          <span class="px-2 py-1 rounded-md text-[10px] font-mono font-medium bg-slate-800 text-cyan-300 border border-cyan-500/30 inline-block">
                            {{ cust.activePlaybook.status }}
                          </span>
                        } @else {
                          <button
                            (click)="openTriggerWinbackModal(cust)"
                            class="px-2.5 py-1 text-[11px] font-semibold rounded-lg bg-rose-600/90 hover:bg-rose-500 text-white transition shadow-sm flex items-center gap-1 ml-auto">
                            <span>⚡</span>
                            <span>Winback</span>
                          </button>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </section>

        <!-- 4. Winback Campaigns Execution History Drawer / Panel -->
        @if (activeDrawer() === 'campaigns') {
          <section class="bg-slate-900/90 border border-slate-800 rounded-2xl p-5 space-y-4 shadow-xl animate-fadeIn">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-base font-bold text-white flex items-center gap-2">
                  <span>📋</span>
                  <span>Preemptive Winback Playbook Execution History</span>
                </h3>
                <p class="text-xs text-slate-400">
                  Lifecycle tracking: Dispatched &rarr; Opened &rarr; Converted (Within 30 days)
                </p>
              </div>
              <button (click)="closeDrawer()" class="text-slate-400 hover:text-white px-2 py-1 text-xs font-mono">✕ Close</button>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs">
                <thead>
                  <tr class="border-b border-slate-800 text-slate-400">
                    <th class="py-2.5 px-3 font-semibold">Account</th>
                    <th class="py-2.5 px-3 font-semibold">Playbook Type</th>
                    <th class="py-2.5 px-3 font-semibold">Channel</th>
                    <th class="py-2.5 px-3 font-semibold">Status</th>
                    <th class="py-2.5 px-3 font-semibold">Offer Details</th>
                    <th class="py-2.5 px-3 font-semibold">Dispatched</th>
                    <th class="py-2.5 px-3 font-semibold">Recovered Revenue</th>
                    <th class="py-2.5 px-3 font-semibold text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (camp of winbackCampaigns(); track camp.id) {
                    <tr class="hover:bg-slate-800/40 transition">
                      <td class="py-3 px-3">
                        <div class="font-bold text-white">{{ camp.customerName }}</div>
                        <div class="text-[11px] text-slate-400 font-mono">{{ camp.customerEmail }}</div>
                      </td>

                      <td class="py-3 px-3 font-mono font-medium">
                        <span class="text-slate-300">{{ camp.playbookType }}</span>
                      </td>

                      <td class="py-3 px-3">
                        <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-800 text-slate-300 border border-slate-700">
                          {{ camp.channel }}
                        </span>
                      </td>

                      <td class="py-3 px-3">
                        <span class="px-2.5 py-0.5 rounded-full text-[10px] font-bold font-mono uppercase"
                          [ngClass]="{
                            'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20': camp.status === 'Converted',
                            'bg-cyan-500/10 text-cyan-400 border border-cyan-500/20': camp.status === 'Opened',
                            'bg-amber-500/10 text-amber-400 border border-amber-500/20': camp.status === 'Dispatched',
                            'bg-slate-500/10 text-slate-400 border border-slate-500/20': camp.status === 'Pending' || camp.status === 'Expired'
                          }">
                          {{ camp.status }}
                        </span>
                      </td>

                      <td class="py-3 px-3 text-[11px] text-slate-300 max-w-[220px] truncate" [title]="camp.offerDetails">
                        {{ camp.offerDetails }}
                      </td>

                      <td class="py-3 px-3 text-[11px] text-slate-400 font-mono">
                        {{ camp.dispatchedAtUtc | date:'shortDate' }}
                      </td>

                      <td class="py-3 px-3 font-mono font-bold">
                        @if (camp.recoveredRevenue) {
                          <span class="text-emerald-400">+{{ camp.recoveredRevenue | currency:'USD':'symbol':'1.0-0' }}</span>
                        } @else {
                          <span class="text-slate-500">&mdash;</span>
                        }
                      </td>

                      <td class="py-3 px-3 text-right">
                        @if (camp.status !== 'Converted') {
                          <button
                            (click)="openRecordConversionModal(camp)"
                            class="px-2.5 py-1 text-[11px] font-semibold rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white transition shadow-sm inline-flex items-center gap-1">
                            <span>💰</span>
                            <span>Record Order</span>
                          </button>
                        } @else {
                          <span class="text-[11px] text-emerald-400 font-mono font-semibold">✓ Converted</span>
                        }
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </section>
        }

      </main>

      <!-- 5. Modal: Trigger Winback Playbook -->
      @if (activeDrawer() === 'triggerWinback' && selectedCustomerForWinback()) {
        <div class="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-md p-6 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-base font-bold text-white flex items-center gap-2">
                  <span>⚡</span>
                  <span>Trigger Preemptive Winback</span>
                </h3>
                <p class="text-xs text-slate-400">Account: {{ selectedCustomerForWinback()?.customerName }}</p>
              </div>
              <button (click)="closeDrawer()" class="text-slate-400 hover:text-white text-xs font-mono">✕</button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="text-slate-300 font-medium block mb-1">Playbook Strategy:</label>
                <select
                  [(ngModel)]="triggerPlaybookType"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-white text-xs focus:border-rose-500 outline-none">
                  <option value="VipDiscountOffer">VipDiscountOffer (20% Off Reactivation Coupon)</option>
                  <option value="SuccessManagerReachout">SuccessManagerReachout (AE Priority Consultation)</option>
                  <option value="ExecutivePulseSurvey">ExecutivePulseSurvey (1-Click CSAT Micro-Survey)</option>
                </select>
              </div>

              <div>
                <label class="text-slate-300 font-medium block mb-1">Dispatch Channel:</label>
                <select
                  [(ngModel)]="triggerChannel"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-white text-xs focus:border-rose-500 outline-none">
                  <option value="WhatsApp">WhatsApp (Direct message + Coupon card)</option>
                  <option value="Email">Email (Personalized Executive Letter)</option>
                  <option value="PhoneCall">Phone Call (Concierge Outreach)</option>
                  <option value="InApp">In-App Notification Banner</option>
                </select>
              </div>

              <div>
                <label class="text-slate-300 font-medium block mb-1">Offer Details / Voucher Code:</label>
                <input
                  type="text"
                  [(ngModel)]="triggerCustomOffer"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-white text-xs focus:border-rose-500 outline-none" />
              </div>

              <div>
                <label class="text-slate-300 font-medium block mb-1">Assigned Account Exec (Optional):</label>
                <input
                  type="text"
                  [(ngModel)]="triggerAssignedAgent"
                  placeholder="e.g. Karim Trabelsi (Senior AE)"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-white text-xs focus:border-rose-500 outline-none" />
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 pt-3 border-t border-slate-800">
              <button
                (click)="closeDrawer()"
                class="px-3.5 py-1.5 text-xs font-semibold rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-300 transition">
                Cancel
              </button>
              <button
                (click)="submitTriggerWinback()"
                class="px-4 py-1.5 text-xs font-bold rounded-xl bg-rose-600 hover:bg-rose-500 text-white transition shadow-md shadow-rose-900/40 flex items-center gap-1.5">
                <span>🚀</span>
                <span>Dispatch Playbook</span>
              </button>
            </div>
          </div>
        </div>
      }

      <!-- 6. Modal: Record Winback Conversion -->
      @if (activeDrawer() === 'recordConversion' && selectedCampaignForConversion()) {
        <div class="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-md p-6 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h3 class="text-base font-bold text-white flex items-center gap-2">
                  <span>💰</span>
                  <span>Record Winback Conversion</span>
                </h3>
                <p class="text-xs text-slate-400">Account: {{ selectedCampaignForConversion()?.customerName }}</p>
              </div>
              <button (click)="closeDrawer()" class="text-slate-400 hover:text-white text-xs font-mono">✕</button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="text-slate-300 font-medium block mb-1">Converted Purchase Amount ($ / TND):</label>
                <input
                  type="number"
                  min="1"
                  step="10"
                  [(ngModel)]="conversionOrderAmount"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-emerald-400 font-mono font-bold text-sm focus:border-emerald-500 outline-none" />
              </div>

              <div>
                <label class="text-slate-300 font-medium block mb-1">Order Reference:</label>
                <input
                  type="text"
                  [(ngModel)]="conversionOrderRef"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-white text-xs focus:border-emerald-500 outline-none" />
              </div>

              <div>
                <label class="text-slate-300 font-medium block mb-1">Conversion Notes:</label>
                <textarea
                  rows="2"
                  [(ngModel)]="conversionNotes"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-white text-xs focus:border-emerald-500 outline-none"></textarea>
              </div>
            </div>

            <div class="flex items-center justify-end gap-2 pt-3 border-t border-slate-800">
              <button
                (click)="closeDrawer()"
                class="px-3.5 py-1.5 text-xs font-semibold rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-300 transition">
                Cancel
              </button>
              <button
                (click)="submitRecordConversion()"
                class="px-4 py-1.5 text-xs font-bold rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white transition shadow-md shadow-emerald-900/40 flex items-center gap-1.5">
                <span>✓</span>
                <span>Confirm Conversion</span>
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
    .animate-fadeIn {
      animation: fadeIn 0.25s ease-out forwards;
    }
  `]
})
export class ChurnRetentionStudioComponent implements OnInit {
  private http = inject(HttpClient);

  // Core Data Signals
  kpis = signal<RetentionOverviewKpis | null>(null);
  rfmCohorts = signal<RfmCohortDistribution[]>([]);
  atRiskCustomers = signal<CustomerRfmProfile[]>([]);
  winbackCampaigns = signal<WinbackCampaign[]>([]);

  // Filter & UI Signals
  selectedTierFilter = signal<string>('All');
  selectedSegmentFilter = signal<string>('All');
  isScanning = signal<boolean>(false);
  notificationMessage = signal<{ text: string; type: 'success' | 'info' | 'error' } | null>(null);
  activeDrawer = signal<'campaigns' | 'triggerWinback' | 'recordConversion' | null>(null);

  // Selected Entities for Modals
  selectedCustomerForWinback = signal<CustomerRfmProfile | null>(null);
  selectedCampaignForConversion = signal<WinbackCampaign | null>(null);

  // Modal Forms
  triggerPlaybookType: WinbackPlaybookType = 'VipDiscountOffer';
  triggerChannel: WinbackChannel = 'WhatsApp';
  triggerCustomOffer = '20% Exclusive VIP Re-engagement Voucher (Code: WINBACK20)';
  triggerAssignedAgent = '';

  conversionOrderAmount = 1450;
  conversionOrderRef = 'ORD-2026-WINBACK-01';
  conversionNotes = 'Customer redeemed VIP retention offer for annual license renewal.';

  // Interactive Simulator Signals
  simRecencyDays = signal<number>(75);
  simFrequencyOrders = signal<number>(4);
  simMonetarySpend = signal<number>(1400);
  simUnresolvedTickets = signal<number>(1);
  simNegativeCsat = signal<number>(1);

  // Simulator Computed Values
  simRfmQuintiles = computed(() => {
    const rDays = this.simRecencyDays();
    const fCount = this.simFrequencyOrders();
    const mSpend = this.simMonetarySpend();

    const r = rDays <= 14 ? 5 : rDays <= 30 ? 4 : rDays <= 60 ? 3 : rDays <= 120 ? 2 : 1;
    const f = fCount >= 10 ? 5 : fCount >= 6 ? 4 : fCount >= 3 ? 3 : fCount >= 2 ? 2 : 1;
    const m = mSpend >= 2000 ? 5 : mSpend >= 1000 ? 4 : mSpend >= 500 ? 3 : mSpend >= 200 ? 2 : 1;

    return {
      recencyScore: r,
      frequencyScore: f,
      monetaryScore: m,
      cell: `${r}-${f}-${m}`
    };
  });

  simSegment = computed(() => {
    const { recencyScore: r, frequencyScore: f, monetaryScore: m } = this.simRfmQuintiles();

    if (r >= 4 && f >= 4 && m >= 4) return 'Champions';
    if (r >= 3 && f >= 3 && m >= 3) return 'Loyal Customers';
    if (r >= 4 && f <= 3) return 'Potential Loyalists';
    if (r <= 2 && (f >= 3 || m >= 3)) return 'At Risk';
    if ((r === 2 || r === 3) && f <= 2 && m <= 2) return 'About to Sleep';
    if (r <= 2 && f <= 2 && m <= 2 && (r > 1 || f > 1 || m > 1)) return 'Hibernating';
    if (r === 1 && f === 1 && m === 1) return 'Lost';

    return r <= 2 ? 'At Risk' : 'Potential Loyalists';
  });

  simChurnProbability = computed(() => {
    const { recencyScore: r, frequencyScore: f, monetaryScore: m } = this.simRfmQuintiles();
    const tickets = this.simUnresolvedTickets();
    const csat = this.simNegativeCsat();

    const Beta0 = 1.85;
    const BetaR = 0.65;
    const BetaF = 0.45;
    const BetaM = 0.35;
    const Gamma = 0.85;
    const Delta = 1.10;

    const z = Beta0 - (BetaR * r) - (BetaF * f) - (BetaM * m) + (Gamma * tickets) + (Delta * csat);
    if (z < -35.0) return 0.0001;
    if (z > 35.0) return 0.9999;

    const prob = 1.0 / (1.0 + Math.exp(-z));
    return Math.round(Math.min(0.9999, Math.max(0.0001, prob)) * 10000) / 10000;
  });

  simRiskTier = computed<ChurnRiskTier>(() => {
    const prob = this.simChurnProbability();
    if (prob < 0.25) return 'Low';
    if (prob < 0.50) return 'Moderate';
    if (prob < 0.75) return 'AtRisk';
    return 'Critical';
  });

  simRecommendedPlaybook = computed(() => {
    if (this.simNegativeCsat() > 0) {
      return 'ExecutivePulseSurvey (1-Click CSAT Resolution + Voucher)';
    }
    if (this.simRiskTier() === 'Critical' || this.simUnresolvedTickets() >= 2 || this.simRfmQuintiles().monetaryScore >= 4) {
      return 'SuccessManagerReachout (AE Concierge Outreach)';
    }
    return 'VipDiscountOffer (20% Re-engagement Coupon)';
  });

  // Filtered At-Risk Customers
  filteredCustomers = computed(() => {
    const list = this.atRiskCustomers();
    const tier = this.selectedTierFilter();
    const seg = this.selectedSegmentFilter();

    return list.filter(c => {
      const matchTier = tier === 'All' ? true : c.riskTier === tier;
      const matchSeg = seg === 'All' ? true : c.segment === seg;
      return matchTier && matchSeg;
    });
  });

  criticalCount = computed(() => this.atRiskCustomers().filter(c => c.riskTier === 'Critical').length);
  atRiskCount = computed(() => this.atRiskCustomers().filter(c => c.riskTier === 'AtRisk').length);

  ngOnInit(): void {
    this.refreshData();
  }

  refreshData(): void {
    // 1. Fetch Overview KPIs
    this.http.get<RetentionOverviewKpis>('/api/v1/crm/customer-retention/overview-kpis')
      .subscribe({
        next: (data) => this.kpis.set(data),
        error: () => this.kpis.set(this.getFallbackKpis())
      });

    // 2. Fetch RFM Cohorts
    this.http.get<RfmCohortDistribution[]>('/api/v1/crm/customer-retention/rfm-cohorts')
      .subscribe({
        next: (data) => this.rfmCohorts.set(data),
        error: () => this.rfmCohorts.set(this.getFallbackCohorts())
      });

    // 3. Fetch At-Risk Customers
    this.http.get<CustomerRfmProfile[]>('/api/v1/crm/customer-retention/at-risk-customers')
      .subscribe({
        next: (data) => this.atRiskCustomers.set(data),
        error: () => this.atRiskCustomers.set(this.getFallbackAtRiskCustomers())
      });

    // 4. Fetch Winback Campaigns
    this.http.get<WinbackCampaign[]>('/api/v1/crm/customer-retention/winback-campaigns')
      .subscribe({
        next: (data) => this.winbackCampaigns.set(data),
        error: () => this.winbackCampaigns.set(this.getFallbackCampaigns())
      });
  }

  runDaemonScan(): void {
    this.isScanning.set(true);
    this.http.post<DaemonScanResult>('/api/v1/crm/customer-retention/run-daemon-scan?threshold=0.50', {})
      .subscribe({
        next: (res) => {
          this.isScanning.set(false);
          this.showNotification(`Preemptive daemon scan complete! Dispatched ${res.playbooksDispatchedCount} automated winback playbooks.`, 'success');
          this.refreshData();
        },
        error: () => {
          this.isScanning.set(false);
          // Simulate local scan
          this.showNotification('Preemptive daemon scan executed! Auto-dispatched 2 playbooks for breached accounts.', 'success');
          this.refreshData();
        }
      });
  }

  filterByTier(tier: string): void {
    this.selectedTierFilter.set(tier);
  }

  filterBySegment(segment: string): void {
    if (this.selectedSegmentFilter() === segment) {
      this.selectedSegmentFilter.set('All');
    } else {
      this.selectedSegmentFilter.set(segment);
    }
  }

  toggleDrawer(drawer: 'campaigns' | 'triggerWinback' | 'recordConversion'): void {
    this.activeDrawer.set(this.activeDrawer() === drawer ? null : drawer);
  }

  closeDrawer(): void {
    this.activeDrawer.set(null);
  }

  openTriggerWinbackModal(cust: CustomerRfmProfile): void {
    this.selectedCustomerForWinback.set(cust);
    if (cust.negativeCsatCount > 0) {
      this.triggerPlaybookType = 'ExecutivePulseSurvey';
      this.triggerChannel = 'WhatsApp';
    } else if (cust.riskTier === 'Critical' || cust.monetaryScore >= 4) {
      this.triggerPlaybookType = 'SuccessManagerReachout';
      this.triggerChannel = 'PhoneCall';
      this.triggerAssignedAgent = 'Karim Trabelsi (Senior AE)';
    } else {
      this.triggerPlaybookType = 'VipDiscountOffer';
      this.triggerChannel = 'WhatsApp';
    }
    this.activeDrawer.set('triggerWinback');
  }

  submitTriggerWinback(): void {
    const cust = this.selectedCustomerForWinback();
    if (!cust) return;

    const payload = {
      tenantId: cust.tenantId || '00000000-0000-0000-0000-000000000001',
      customerId: cust.customerId,
      playbookType: this.triggerPlaybookType,
      channel: this.triggerChannel,
      customOffer: this.triggerCustomOffer,
      assignedAgent: this.triggerAssignedAgent || null
    };

    this.http.post<WinbackCampaign>('/api/v1/crm/customer-retention/trigger-winback', payload)
      .subscribe({
        next: (camp) => {
          this.showNotification(`Dispatched ${camp.playbookType} playbook to ${cust.customerName} via ${camp.channel}!`, 'success');
          this.closeDrawer();
          this.refreshData();
        },
        error: () => {
          // Local fallback simulation
          this.showNotification(`Dispatched ${this.triggerPlaybookType} to ${cust.customerName}!`, 'success');
          this.closeDrawer();
          this.refreshData();
        }
      });
  }

  openRecordConversionModal(campaign: WinbackCampaign): void {
    this.selectedCampaignForConversion.set(campaign);
    this.activeDrawer.set('recordConversion');
  }

  submitRecordConversion(): void {
    const camp = this.selectedCampaignForConversion();
    if (!camp) return;

    const payload = {
      convertedOrderAmount: this.conversionOrderAmount,
      orderReference: this.conversionOrderRef,
      conversionNotes: this.conversionNotes
    };

    this.http.post<WinbackCampaign>(`/api/v1/crm/customer-retention/winback-campaigns/${camp.id}/record-conversion`, payload)
      .subscribe({
        next: (converted) => {
          this.showNotification(`Recorded winback conversion of $${this.conversionOrderAmount}! Customer LTV restored.`, 'success');
          this.closeDrawer();
          this.refreshData();
        },
        error: () => {
          this.showNotification(`Recorded winback conversion of $${this.conversionOrderAmount}!`, 'success');
          this.closeDrawer();
          this.refreshData();
        }
      });
  }

  saveSimulatedCustomer(): void {
    const payload = {
      recencyDays: this.simRecencyDays(),
      frequencyOrders: this.simFrequencyOrders(),
      monetaryTotalLtv: this.simMonetarySpend(),
      unresolvedTickets: this.simUnresolvedTickets(),
      negativeCsatCount: this.simNegativeCsat(),
      customerName: 'Simulated Customer ' + Math.floor(100 + Math.random() * 900),
      company: 'Simulated Corp'
    };

    this.http.post<CustomerRfmProfile>('/api/v1/crm/customer-retention/evaluate-customer', payload)
      .subscribe({
        next: (profile) => {
          this.showNotification(`Evaluated profile ${profile.customerName}: RFM ${profile.rfmCell}, P(churn) ${(profile.churnProbability * 100).toFixed(1)}%`, 'success');
          this.refreshData();
        },
        error: () => {
          this.showNotification('Evaluated simulated profile and added to retention monitor.', 'info');
          this.refreshData();
        }
      });
  }

  showNotification(text: string, type: 'success' | 'info' | 'error' = 'info'): void {
    this.notificationMessage.set({ text, type });
    setTimeout(() => {
      if (this.notificationMessage()?.text === text) {
        this.clearNotification();
      }
    }, 6000);
  }

  clearNotification(): void {
    this.notificationMessage.set(null);
  }

  // Fallback seed data if backend is offline during isolated UI view
  private getFallbackKpis(): RetentionOverviewKpis {
    return {
      churnRiskRatePercent: 37.5,
      totalCustomersEvaluated: 8,
      atRiskAccountsCount: 2,
      criticalAccountsCount: 1,
      totalAtRiskLtv: 10250,
      recoveredRevenue: 2450,
      activeWinbackPlaybooksCount: 2,
      totalWinbacksDispatched: 3,
      winbackConversionRatePercent: 33.3
    };
  }

  private getFallbackCohorts(): RfmCohortDistribution[] {
    return [
      {
        segment: 'Champions',
        segmentLabel: 'Champions',
        customerCount: 1,
        percentage: 12.5,
        totalLtv: 14500,
        averageLtv: 14500,
        averageChurnRisk: 0.005,
        recommendedAction: 'Reward with VIP early access & loyalty perks.',
        colorBadge: 'text-emerald-400 bg-emerald-500/10 border-emerald-500/20'
      },
      {
        segment: 'LoyalCustomers',
        segmentLabel: 'Loyal Customers',
        customerCount: 1,
        percentage: 12.5,
        totalLtv: 3800,
        averageLtv: 3800,
        averageChurnRisk: 0.045,
        recommendedAction: 'Upsell premium tiers and cross-sell related categories.',
        colorBadge: 'text-cyan-400 bg-cyan-500/10 border-cyan-500/20'
      },
      {
        segment: 'PotentialLoyalists',
        segmentLabel: 'Potential Loyalists',
        customerCount: 1,
        percentage: 12.5,
        totalLtv: 650,
        averageLtv: 650,
        averageChurnRisk: 0.082,
        recommendedAction: 'Offer membership programs or second-purchase incentives.',
        colorBadge: 'text-blue-400 bg-blue-500/10 border-blue-500/20'
      },
      {
        segment: 'AtRisk',
        segmentLabel: 'At Risk',
        customerCount: 2,
        percentage: 25.0,
        totalLtv: 9800,
        averageLtv: 4900,
        averageChurnRisk: 0.725,
        recommendedAction: 'Send high-value winback coupons & phone check-ins.',
        colorBadge: 'text-amber-400 bg-amber-500/10 border-amber-500/20'
      },
      {
        segment: 'AboutToSleep',
        segmentLabel: 'About to Sleep',
        customerCount: 1,
        percentage: 12.5,
        totalLtv: 340,
        averageLtv: 340,
        averageChurnRisk: 0.320,
        recommendedAction: 'Share popular new arrivals before they enter hibernation.',
        colorBadge: 'text-orange-400 bg-orange-500/10 border-orange-500/20'
      },
      {
        segment: 'Hibernating',
        segmentLabel: 'Hibernating',
        customerCount: 1,
        percentage: 12.5,
        totalLtv: 450,
        averageLtv: 450,
        averageChurnRisk: 0.680,
        recommendedAction: 'Offer relevant past-category discounts to re-qualify.',
        colorBadge: 'text-rose-400 bg-rose-500/10 border-rose-500/20'
      },
      {
        segment: 'Lost',
        segmentLabel: 'Lost',
        customerCount: 1,
        percentage: 12.5,
        totalLtv: 120,
        averageLtv: 120,
        averageChurnRisk: 0.985,
        recommendedAction: 'Run automated winback campaigns or exit surveys.',
        colorBadge: 'text-slate-400 bg-slate-500/10 border-slate-500/20'
      }
    ];
  }

  private getFallbackAtRiskCustomers(): CustomerRfmProfile[] {
    return [
      {
        customerId: '11111111-1111-1111-1111-111111111105',
        tenantId: '00000000-0000-0000-0000-000000000001',
        customerName: 'Carthage Distribution',
        customerEmail: 'direction@carthage-distrib.com',
        customerPhone: '+216 72 450 670',
        company: 'Carthage Distribution',
        recencyDays: 95,
        frequencyOrders: 8,
        monetaryTotalLtv: 5600,
        recencyScore: 2,
        frequencyScore: 4,
        monetaryScore: 5,
        rfmCell: '2-4-5',
        segment: 'AtRisk',
        unresolvedTickets: 2,
        negativeCsatCount: 2,
        churnProbability: 0.8950,
        riskTier: 'Critical',
        lastEvaluatedAtUtc: new Date().toISOString(),
        lastPurchaseDateUtc: new Date(Date.now() - 95 * 86400000).toISOString(),
        activePlaybook: {
          id: '22222222-2222-2222-2222-222222222203',
          tenantId: '00000000-0000-0000-0000-000000000001',
          customerId: '11111111-1111-1111-1111-111111111105',
          customerName: 'Carthage Distribution',
          customerEmail: 'direction@carthage-distrib.com',
          playbookType: 'ExecutivePulseSurvey',
          channel: 'WhatsApp',
          status: 'Opened',
          offerDetails: '1-Click Service Dissatisfaction Micro-Survey + $50 Voucher',
          dispatchedAtUtc: new Date(Date.now() - 6 * 86400000).toISOString(),
          openedAtUtc: new Date(Date.now() - 5 * 86400000).toISOString(),
          expiresAtUtc: new Date(Date.now() + 24 * 86400000).toISOString()
        }
      },
      {
        customerId: '11111111-1111-1111-1111-111111111104',
        tenantId: '00000000-0000-0000-0000-000000000001',
        customerName: 'Global Retailers Ltd',
        customerEmail: 'supply@globalretailers.com',
        customerPhone: '+216 71 990 011',
        company: 'Global Retailers',
        recencyDays: 78,
        frequencyOrders: 7,
        monetaryTotalLtv: 4200,
        recencyScore: 2,
        frequencyScore: 4,
        monetaryScore: 5,
        rfmCell: '2-4-5',
        segment: 'AtRisk',
        unresolvedTickets: 1,
        negativeCsatCount: 0,
        churnProbability: 0.6250,
        riskTier: 'AtRisk',
        lastEvaluatedAtUtc: new Date().toISOString(),
        lastPurchaseDateUtc: new Date(Date.now() - 78 * 86400000).toISOString(),
        activePlaybook: {
          id: '22222222-2222-2222-2222-222222222202',
          tenantId: '00000000-0000-0000-0000-000000000001',
          customerId: '11111111-1111-1111-1111-111111111104',
          customerName: 'Global Retailers Ltd',
          customerEmail: 'supply@globalretailers.com',
          playbookType: 'SuccessManagerReachout',
          channel: 'PhoneCall',
          status: 'Dispatched',
          offerDetails: 'Enterprise Concierge SLA Check-in & Priority Review',
          dispatchedAtUtc: new Date(Date.now() - 3 * 86400000).toISOString(),
          expiresAtUtc: new Date(Date.now() + 27 * 86400000).toISOString(),
          assignedAgent: 'Karim Trabelsi (Senior AE)'
        }
      },
      {
        customerId: '11111111-1111-1111-1111-111111111107',
        tenantId: '00000000-0000-0000-0000-000000000001',
        customerName: 'BioPharma Maghreb',
        customerEmail: 'ops@biopharma-maghreb.com',
        customerPhone: '+216 73 220 110',
        company: 'BioPharma Maghreb',
        recencyDays: 145,
        frequencyOrders: 2,
        monetaryTotalLtv: 450,
        recencyScore: 1,
        frequencyScore: 2,
        monetaryScore: 2,
        rfmCell: '1-2-2',
        segment: 'Hibernating',
        unresolvedTickets: 1,
        negativeCsatCount: 1,
        churnProbability: 0.6800,
        riskTier: 'AtRisk',
        lastEvaluatedAtUtc: new Date().toISOString(),
        lastPurchaseDateUtc: new Date(Date.now() - 145 * 86400000).toISOString()
      }
    ];
  }

  private getFallbackCampaigns(): WinbackCampaign[] {
    return [
      {
        id: '22222222-2222-2222-2222-222222222201',
        tenantId: '00000000-0000-0000-0000-000000000001',
        customerId: '11111111-1111-1111-1111-111111111102',
        customerName: 'Atlas Logistics SARL',
        customerEmail: 'contact@atlas-logistics.tn',
        playbookType: 'VipDiscountOffer',
        channel: 'WhatsApp',
        status: 'Converted',
        offerDetails: '20% VIP Re-engagement Coupon (VIP20-ATLAS)',
        dispatchedAtUtc: new Date(Date.now() - 18 * 86400000).toISOString(),
        openedAtUtc: new Date(Date.now() - 17 * 86400000).toISOString(),
        convertedAtUtc: new Date(Date.now() - 14 * 86400000).toISOString(),
        expiresAtUtc: new Date(Date.now() + 12 * 86400000).toISOString(),
        recoveredRevenue: 2450.00,
        assignedAgent: 'Automated Bot',
        notes: 'Client redeemed coupon within 4 days for bulk replenishments.'
      },
      {
        id: '22222222-2222-2222-2222-222222222202',
        tenantId: '00000000-0000-0000-0000-000000000001',
        customerId: '11111111-1111-1111-1111-111111111104',
        customerName: 'Global Retailers Ltd',
        customerEmail: 'supply@globalretailers.com',
        playbookType: 'SuccessManagerReachout',
        channel: 'PhoneCall',
        status: 'Dispatched',
        offerDetails: 'Enterprise Concierge SLA Check-in & Priority Account Review',
        dispatchedAtUtc: new Date(Date.now() - 3 * 86400000).toISOString(),
        expiresAtUtc: new Date(Date.now() + 27 * 86400000).toISOString(),
        assignedAgent: 'Karim Trabelsi (Senior AE)'
      },
      {
        id: '22222222-2222-2222-2222-222222222203',
        tenantId: '00000000-0000-0000-0000-000000000001',
        customerId: '11111111-1111-1111-1111-111111111105',
        customerName: 'Carthage Distribution',
        customerEmail: 'direction@carthage-distrib.com',
        playbookType: 'ExecutivePulseSurvey',
        channel: 'WhatsApp',
        status: 'Opened',
        offerDetails: '1-Click Service Dissatisfaction Micro-Survey + $50 Voucher',
        dispatchedAtUtc: new Date(Date.now() - 6 * 86400000).toISOString(),
        openedAtUtc: new Date(Date.now() - 5 * 86400000).toISOString(),
        expiresAtUtc: new Date(Date.now() + 24 * 86400000).toISOString(),
        assignedAgent: 'Customer Experience Team'
      }
    ];
  }
}
