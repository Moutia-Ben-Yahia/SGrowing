import { Component, ChangeDetectionStrategy, signal, computed, Optional, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type HealthTier = 'Healthy' | 'AtRisk' | 'Critical';
export type RiskTriggerType = 'NpsDrop' | 'TicketSpike' | 'InactiveSeatRatio' | 'ContractRenewalRisk' | 'PaymentOverdue';
export type RiskSeverity = 'Low' | 'Medium' | 'High' | 'Critical';
export type PlaybookType = 'ExecutiveSponsorCheckin' | 'AdoptionWorkshop' | 'EmergencyDiscountReview' | 'TechnicalSwatTriage';
export type PlaybookPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type PlaybookStatus = 'Suggested' | 'Active' | 'Completed' | 'Dismissed';

export interface EarlyWarningRiskTrigger {
  triggerId: string;
  triggerType: RiskTriggerType;
  severity: RiskSeverity;
  description: string;
  currentMetricValue: number;
  threshold: number;
  detectedAtUtc: string;
}

export interface CsmPlaybookRecord {
  playbookId: string;
  accountId: string;
  accountName: string;
  title: string;
  playbookType: PlaybookType;
  priority: PlaybookPriority;
  status: PlaybookStatus;
  objective: string;
  assignedRole: string;
  recommendedActions: string[];
  notes?: string | null;
  createdAtUtc: string;
  completedAtUtc?: string | null;
}

export interface HealthScoreSnapshotRecord {
  snapshotId: string;
  recordedAtUtc: string;
  compositeHealthScore: number;
  adoptionScore: number;
  sentimentScore: number;
  utilizationScore: number;
  commercialScore: number;
  healthTier: HealthTier;
}

export interface CustomerAccountHealthRecord {
  accountId: string;
  tenantId: string;
  accountName: string;
  domain: string;
  industry: string;
  assignedCsm: string;
  annualRecurringRevenue: number;
  totalSeats: number;
  activeSeats: number;
  inactiveSeatRatio: number;
  npsScore: number;
  previousNpsScore: number;
  openTicketsCount: number;
  urgentTicketsCount: number;
  daysUntilRenewal: number;
  daysOverdueInvoice: number;
  adoptionScore: number;
  sentimentScore: number;
  utilizationScore: number;
  commercialScore: number;
  compositeHealthScore: number;
  healthTier: HealthTier;
  predictedChurnProbability: number;
  lastEvaluatedAtUtc: string;
  riskTriggers: EarlyWarningRiskTrigger[];
  activePlaybooks: CsmPlaybookRecord[];
  historicalSnapshots: HealthScoreSnapshotRecord[];
  tags: string[];
}

@Component({
  selector: 'app-customer-health-radar-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="min-h-screen bg-slate-950 text-slate-100 p-6 flex flex-col gap-6"
      [attr.dir]="isRtl() ? 'rtl' : 'ltr'"
      data-testid="customer-health-radar-studio">

      @let totalCount = totalAccountsCount();
      @let hCount = healthyCount();
      @let arCount = atRiskCount();
      @let cCount = criticalCount();
      @let avgHealth = averageHealthScore();
      @let totArr = totalArr();
      @let riskArr = arrAtRisk();
      @let playbooksCount = activePlaybooksCount();

      <!-- Header Section -->
      <header class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-2 mb-1.5">
            <span class="px-2.5 py-0.5 text-xs font-semibold rounded-full bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              PRD Domain H3 & I3 · Customer Success Retention
            </span>
            <span class="text-xs text-slate-400">Proactive Health Radar & Automated CSM Playbooks</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-3">
            Customer Success Health & Proactive Retention Radar Studio
          </h1>
          <p class="text-sm text-slate-400 mt-0.5">
            Composite health engine (Adoption 35%, Sentiment 25%, Utilization 20%, Commercial 20%), early-warning churn detector & automated CSM rescue playbooks.
          </p>
        </div>

        <div class="flex flex-wrap items-center gap-3">
          <button
            (click)="openCreateModal()"
            data-testid="register-account-btn"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 active:bg-emerald-700 text-white font-medium text-sm rounded-lg transition-colors shadow-lg shadow-emerald-950/40 flex items-center gap-2">
            <span>+</span> Register Account
          </button>
          <button
            (click)="recalculateAllRadar()"
            data-testid="recalculate-radar-btn"
            class="px-3.5 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 font-medium text-sm rounded-lg transition-colors flex items-center gap-2">
            <span>⚡</span> Recalculate Radar
          </button>
          <button
            (click)="resetToDefaults()"
            data-testid="reset-radar-btn"
            class="px-3 py-2 bg-slate-900 hover:bg-slate-800 text-slate-400 hover:text-slate-200 border border-slate-800 text-sm rounded-lg transition-colors">
            ↺ Reset
          </button>
        </div>
      </header>

      <!-- Notification Toast -->
      @if (actionToast(); as toast) {
        <div
          class="p-3.5 rounded-xl border flex items-center justify-between text-sm transition-all"
          [ngClass]="{
            'bg-emerald-950/60 border-emerald-500/40 text-emerald-200': toast.type === 'success',
            'bg-amber-950/60 border-amber-500/40 text-amber-200': toast.type === 'warning',
            'bg-blue-950/60 border-blue-500/40 text-blue-200': toast.type === 'info'
          }"
          data-testid="radar-toast">
          <div class="flex items-center gap-2.5">
            <span class="text-base">{{ toast.type === 'success' ? '✓' : toast.type === 'warning' ? '⚠' : 'ℹ' }}</span>
            <span>{{ toast.message }}</span>
          </div>
          <button (click)="actionToast.set(null)" class="text-xs hover:opacity-75">✕</button>
        </div>
      }

      <!-- Executive Retention Radar KPI Strip -->
      <section class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3.5" data-testid="radar-kpis">
        <div class="bg-slate-900/90 border border-slate-800/80 rounded-xl p-4 flex flex-col justify-between">
          <div class="flex items-center justify-between">
            <span class="text-xs font-medium text-slate-400">Average Health</span>
            <span class="text-xs font-semibold px-2 py-0.5 rounded bg-slate-800 text-slate-300">Target >= 75</span>
          </div>
          <div class="mt-2">
            <div class="text-2xl font-bold" [ngClass]="getHealthColorClass(avgHealth)">
              {{ avgHealth }}<span class="text-sm font-normal text-slate-500"> / 100</span>
            </div>
            <div class="w-full bg-slate-800 h-1.5 rounded-full mt-2 overflow-hidden">
              <div
                class="h-full rounded-full transition-all"
                [ngClass]="getHealthBarClass(avgHealth)"
                [style.width.%]="avgHealth"></div>
            </div>
          </div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800/80 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-emerald-400">Healthy (>= 75)</span>
          <div class="mt-2 flex items-baseline justify-between">
            <span class="text-2xl font-bold text-emerald-300" data-testid="kpi-healthy-count">{{ hCount }}</span>
            <span class="text-xs text-slate-400">{{ totalCount > 0 ? (hCount / totalCount * 100 | number:'1.0-0') : 0 }}% of accounts</span>
          </div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800/80 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-amber-400">At-Risk (50-74)</span>
          <div class="mt-2 flex items-baseline justify-between">
            <span class="text-2xl font-bold text-amber-300" data-testid="kpi-atrisk-count">{{ arCount }}</span>
            <span class="text-xs text-slate-400">{{ totalCount > 0 ? (arCount / totalCount * 100 | number:'1.0-0') : 0 }}% of accounts</span>
          </div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800/80 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-rose-400">Critical (&lt; 50)</span>
          <div class="mt-2 flex items-baseline justify-between">
            <span class="text-2xl font-bold text-rose-300" data-testid="kpi-critical-count">{{ cCount }}</span>
            <span class="text-xs text-rose-400 font-medium">Immediate Rescue</span>
          </div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800/80 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-rose-300">ARR at Churn Hazard</span>
          <div class="mt-2">
            <span class="text-2xl font-bold text-rose-200" data-testid="kpi-risk-arr">
              \${{ riskArr | number:'1.0-0' }}
            </span>
            <p class="text-xs text-slate-400 mt-0.5">out of \${{ totArr | number:'1.0-0' }} total ARR</p>
          </div>
        </div>

        <div class="bg-slate-900/90 border border-slate-800/80 rounded-xl p-4 flex flex-col justify-between">
          <span class="text-xs font-medium text-cyan-400">Active Playbooks</span>
          <div class="mt-2 flex items-baseline justify-between">
            <span class="text-2xl font-bold text-cyan-200" data-testid="kpi-active-playbooks">{{ playbooksCount }}</span>
            <span class="text-xs text-cyan-400 font-medium">CSM Actions</span>
          </div>
        </div>
      </section>

      <!-- Filter, Search & View Controls -->
      <section class="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-3 bg-slate-900/60 p-3.5 rounded-xl border border-slate-800">
        <div class="flex flex-wrap items-center gap-2">
          <button
            (click)="selectedTierFilter.set('ALL')"
            [class.bg-slate-800]="selectedTierFilter() === 'ALL'"
            [class.text-white]="selectedTierFilter() === 'ALL'"
            [class.text-slate-400]="selectedTierFilter() !== 'ALL'"
            data-testid="filter-tier-all"
            class="px-3 py-1.5 rounded-lg text-xs font-medium border border-slate-700/60 hover:bg-slate-800 hover:text-white transition-colors">
            All Accounts ({{ totalCount }})
          </button>
          <button
            (click)="selectedTierFilter.set('Healthy')"
            [class.bg-emerald-950]="selectedTierFilter() === 'Healthy'"
            [class.text-emerald-300]="selectedTierFilter() === 'Healthy'"
            [class.border-emerald-600]="selectedTierFilter() === 'Healthy'"
            [class.text-slate-400]="selectedTierFilter() !== 'Healthy'"
            data-testid="filter-tier-healthy"
            class="px-3 py-1.5 rounded-lg text-xs font-medium border border-slate-700/60 hover:bg-slate-800 transition-colors flex items-center gap-1.5">
            <span class="w-2 h-2 rounded-full bg-emerald-400"></span> Healthy ({{ hCount }})
          </button>
          <button
            (click)="selectedTierFilter.set('AtRisk')"
            [class.bg-amber-950]="selectedTierFilter() === 'AtRisk'"
            [class.text-amber-300]="selectedTierFilter() === 'AtRisk'"
            [class.border-amber-600]="selectedTierFilter() === 'AtRisk'"
            [class.text-slate-400]="selectedTierFilter() !== 'AtRisk'"
            data-testid="filter-tier-atrisk"
            class="px-3 py-1.5 rounded-lg text-xs font-medium border border-slate-700/60 hover:bg-slate-800 transition-colors flex items-center gap-1.5">
            <span class="w-2 h-2 rounded-full bg-amber-400"></span> At-Risk ({{ arCount }})
          </button>
          <button
            (click)="selectedTierFilter.set('Critical')"
            [class.bg-rose-950]="selectedTierFilter() === 'Critical'"
            [class.text-rose-300]="selectedTierFilter() === 'Critical'"
            [class.border-rose-600]="selectedTierFilter() === 'Critical'"
            [class.text-slate-400]="selectedTierFilter() !== 'Critical'"
            data-testid="filter-tier-critical"
            class="px-3 py-1.5 rounded-lg text-xs font-medium border border-slate-700/60 hover:bg-slate-800 transition-colors flex items-center gap-1.5">
            <span class="w-2 h-2 rounded-full bg-rose-400"></span> Critical ({{ cCount }})
          </button>

          <div class="h-4 w-px bg-slate-800 mx-1"></div>

          <!-- Trigger filter dropdown -->
          <select
            [ngModel]="selectedTriggerFilter()"
            (ngModelChange)="selectedTriggerFilter.set($event)"
            data-testid="filter-trigger-select"
            class="bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-slate-300">
            <option value="ALL">All Risk Triggers</option>
            <option value="NpsDrop">NPS Drop / Detractor</option>
            <option value="TicketSpike">Ticket Escalation Spike</option>
            <option value="InactiveSeatRatio">Inactive Seat Ratio (&gt;=35%)</option>
            <option value="ContractRenewalRisk">Renewal Due (&lt;=60d)</option>
            <option value="PaymentOverdue">Overdue Invoices (&gt;=30d)</option>
          </select>
        </div>

        <div class="flex items-center gap-2.5">
          <div class="relative w-full md:w-64">
            <input
              type="text"
              [ngModel]="searchQuery()"
              (ngModelChange)="searchQuery.set($event)"
              placeholder="Search account, domain, CSM..."
              data-testid="search-input"
              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-emerald-500" />
            @if (searchQuery()) {
              <button
                (click)="searchQuery.set('')"
                class="absolute right-2.5 top-1.5 text-xs text-slate-400 hover:text-white">✕</button>
            }
          </div>

          <div class="flex items-center rounded-lg border border-slate-700 p-0.5 bg-slate-950">
            <button
              (click)="activeViewMode.set('grid')"
              [class.bg-slate-800]="activeViewMode() === 'grid'"
              [class.text-white]="activeViewMode() === 'grid'"
              [class.text-slate-400]="activeViewMode() !== 'grid'"
              data-testid="view-grid-btn"
              title="Kanban Radar Grid"
              class="px-2.5 py-1 rounded text-xs transition-colors">
              Grid
            </button>
            <button
              (click)="activeViewMode.set('table')"
              [class.bg-slate-800]="activeViewMode() === 'table'"
              [class.text-white]="activeViewMode() === 'table'"
              [class.text-slate-400]="activeViewMode() !== 'table'"
              data-testid="view-table-btn"
              title="Tabular Account Matrix"
              class="px-2.5 py-1 rounded text-xs transition-colors">
              Table
            </button>
          </div>
        </div>
      </section>

      <!-- View 1: 3-Column Radar Kanban Grid -->
      @if (activeViewMode() === 'grid') {
        <section class="grid grid-cols-1 lg:grid-cols-3 gap-5" data-testid="kanban-grid-view">
          <!-- Column 1: Healthy -->
          <div class="flex flex-col gap-3 bg-slate-900/30 p-3.5 rounded-2xl border border-slate-800/80">
            <div class="flex items-center justify-between px-1 pb-1 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <span class="w-2.5 h-2.5 rounded-full bg-emerald-400 shadow-sm shadow-emerald-400/50"></span>
                <h3 class="text-sm font-semibold text-white">Healthy Tier</h3>
                <span class="text-xs text-emerald-400 font-mono">(&gt;= 75)</span>
              </div>
              <span class="text-xs px-2 py-0.5 rounded-full bg-emerald-950/80 text-emerald-300 border border-emerald-800/50">
                {{ getTierAccounts('Healthy').length }}
              </span>
            </div>

            <div class="flex flex-col gap-3">
              @for (account of getTierAccounts('Healthy'); track account.accountId) {
                <div
                  (click)="openDeepDive(account)"
                  data-testid="account-card"
                  [attr.data-account-id]="account.accountId"
                  class="bg-slate-900 border border-slate-800 hover:border-emerald-500/50 rounded-xl p-4 transition-all cursor-pointer shadow-md hover:shadow-emerald-950/20 group">
                  <div class="flex items-start justify-between gap-2">
                    <div>
                      <h4 class="text-sm font-bold text-white group-hover:text-emerald-300 transition-colors flex items-center gap-2">
                        {{ account.accountName }}
                      </h4>
                      <p class="text-xs text-slate-400">{{ account.domain }} · {{ account.industry }}</p>
                    </div>
                    <div class="text-right">
                      <span class="text-lg font-extrabold text-emerald-400 font-mono">
                        {{ account.compositeHealthScore | number:'1.1-1' }}
                      </span>
                      <p class="text-[10px] text-slate-500">Churn Prob: {{ account.predictedChurnProbability * 100 | number:'1.0-0' }}%</p>
                    </div>
                  </div>

                  <!-- 4-Dimension Progress Bars -->
                  <div class="grid grid-cols-4 gap-1.5 my-3 pt-2 border-t border-slate-800/60 text-[10px]">
                    <div>
                      <span class="text-slate-400 block mb-0.5">Adoption (35%)</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-emerald-500 h-full" [style.width.%]="account.adoptionScore"></div>
                      </div>
                      <span class="font-mono text-slate-300 mt-0.5 block">{{ account.adoptionScore | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-400 block mb-0.5">Sentiment (25%)</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-cyan-500 h-full" [style.width.%]="account.sentimentScore"></div>
                      </div>
                      <span class="font-mono text-slate-300 mt-0.5 block">{{ account.sentimentScore | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-400 block mb-0.5">Util (20%)</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-indigo-500 h-full" [style.width.%]="account.utilizationScore"></div>
                      </div>
                      <span class="font-mono text-slate-300 mt-0.5 block">{{ account.utilizationScore | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-400 block mb-0.5">Comm (20%)</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-teal-500 h-full" [style.width.%]="account.commercialScore"></div>
                      </div>
                      <span class="font-mono text-slate-300 mt-0.5 block">{{ account.commercialScore | number:'1.0-0' }}</span>
                    </div>
                  </div>

                  <div class="flex items-center justify-between text-xs text-slate-400 pt-2 border-t border-slate-800/60">
                    <span class="font-semibold text-slate-200">\${{ account.annualRecurringRevenue | number:'1.0-0' }} ARR</span>
                    <span class="text-[11px]">CSM: {{ account.assignedCsm }}</span>
                  </div>
                </div>
              } @empty {
                <div class="p-6 text-center text-xs text-slate-500 border border-dashed border-slate-800 rounded-xl">
                  No accounts in Healthy tier.
                </div>
              }
            </div>
          </div>

          <!-- Column 2: At-Risk -->
          <div class="flex flex-col gap-3 bg-slate-900/30 p-3.5 rounded-2xl border border-slate-800/80">
            <div class="flex items-center justify-between px-1 pb-1 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <span class="w-2.5 h-2.5 rounded-full bg-amber-400 shadow-sm shadow-amber-400/50"></span>
                <h3 class="text-sm font-semibold text-white">At-Risk Tier</h3>
                <span class="text-xs text-amber-400 font-mono">(50 - 74)</span>
              </div>
              <span class="text-xs px-2 py-0.5 rounded-full bg-amber-950/80 text-amber-300 border border-amber-800/50">
                {{ getTierAccounts('AtRisk').length }}
              </span>
            </div>

            <div class="flex flex-col gap-3">
              @for (account of getTierAccounts('AtRisk'); track account.accountId) {
                <div
                  (click)="openDeepDive(account)"
                  data-testid="account-card"
                  [attr.data-account-id]="account.accountId"
                  class="bg-slate-900 border border-slate-800 hover:border-amber-500/50 rounded-xl p-4 transition-all cursor-pointer shadow-md hover:shadow-amber-950/20 group">
                  <div class="flex items-start justify-between gap-2">
                    <div>
                      <h4 class="text-sm font-bold text-white group-hover:text-amber-300 transition-colors flex items-center gap-2">
                        {{ account.accountName }}
                      </h4>
                      <p class="text-xs text-slate-400">{{ account.domain }} · {{ account.industry }}</p>
                    </div>
                    <div class="text-right">
                      <span class="text-lg font-extrabold text-amber-400 font-mono">
                        {{ account.compositeHealthScore | number:'1.1-1' }}
                      </span>
                      <p class="text-[10px] text-amber-400 font-medium">Churn: {{ account.predictedChurnProbability * 100 | number:'1.0-0' }}%</p>
                    </div>
                  </div>

                  <!-- Early Warning Triggers Badges -->
                  @if (account.riskTriggers.length > 0) {
                    <div class="flex flex-wrap gap-1.5 my-2.5">
                      @for (trigger of account.riskTriggers; track trigger.triggerId) {
                        <span
                          class="px-2 py-0.5 text-[10px] font-semibold rounded-md flex items-center gap-1"
                          [ngClass]="{
                            'bg-rose-950 text-rose-300 border border-rose-800': trigger.severity === 'Critical',
                            'bg-amber-950 text-amber-300 border border-amber-800': trigger.severity === 'High',
                            'bg-slate-800 text-slate-300': trigger.severity === 'Medium' || trigger.severity === 'Low'
                          }">
                          ⚠ {{ getTriggerLabel(trigger.triggerType) }}
                        </span>
                      }
                    </div>
                  }

                  <!-- 4-Dimension Progress Bars -->
                  <div class="grid grid-cols-4 gap-1.5 my-2 pt-2 border-t border-slate-800/60 text-[10px]">
                    <div>
                      <span class="text-slate-400 block mb-0.5">Adoption</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-amber-500 h-full" [style.width.%]="account.adoptionScore"></div>
                      </div>
                      <span class="font-mono text-slate-300">{{ account.adoptionScore | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-400 block mb-0.5">Sentiment</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-cyan-500 h-full" [style.width.%]="account.sentimentScore"></div>
                      </div>
                      <span class="font-mono text-slate-300">{{ account.sentimentScore | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-400 block mb-0.5">Util</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-indigo-500 h-full" [style.width.%]="account.utilizationScore"></div>
                      </div>
                      <span class="font-mono text-slate-300">{{ account.utilizationScore | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-400 block mb-0.5">Comm</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-teal-500 h-full" [style.width.%]="account.commercialScore"></div>
                      </div>
                      <span class="font-mono text-slate-300">{{ account.commercialScore | number:'1.0-0' }}</span>
                    </div>
                  </div>

                  <div class="flex items-center justify-between text-xs text-slate-400 pt-2 border-t border-slate-800/60">
                    <span class="font-semibold text-slate-200">\${{ account.annualRecurringRevenue | number:'1.0-0' }} ARR</span>
                    <span class="text-cyan-400 font-medium text-[11px]">{{ account.activePlaybooks.length }} Playbooks</span>
                  </div>
                </div>
              } @empty {
                <div class="p-6 text-center text-xs text-slate-500 border border-dashed border-slate-800 rounded-xl">
                  No accounts in At-Risk tier.
                </div>
              }
            </div>
          </div>

          <!-- Column 3: Critical -->
          <div class="flex flex-col gap-3 bg-slate-900/30 p-3.5 rounded-2xl border border-slate-800/80">
            <div class="flex items-center justify-between px-1 pb-1 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <span class="w-2.5 h-2.5 rounded-full bg-rose-500 shadow-sm shadow-rose-500/50 animate-pulse"></span>
                <h3 class="text-sm font-semibold text-white">Critical Tier</h3>
                <span class="text-xs text-rose-400 font-mono">(&lt; 50)</span>
              </div>
              <span class="text-xs px-2 py-0.5 rounded-full bg-rose-950/80 text-rose-300 border border-rose-800/50">
                {{ getTierAccounts('Critical').length }}
              </span>
            </div>

            <div class="flex flex-col gap-3">
              @for (account of getTierAccounts('Critical'); track account.accountId) {
                <div
                  (click)="openDeepDive(account)"
                  data-testid="account-card"
                  [attr.data-account-id]="account.accountId"
                  class="bg-slate-900 border border-rose-900/40 hover:border-rose-500 rounded-xl p-4 transition-all cursor-pointer shadow-md hover:shadow-rose-950/30 group">
                  <div class="flex items-start justify-between gap-2">
                    <div>
                      <h4 class="text-sm font-bold text-white group-hover:text-rose-300 transition-colors flex items-center gap-2">
                        {{ account.accountName }}
                      </h4>
                      <p class="text-xs text-slate-400">{{ account.domain }} · {{ account.industry }}</p>
                    </div>
                    <div class="text-right">
                      <span class="text-lg font-extrabold text-rose-400 font-mono">
                        {{ account.compositeHealthScore | number:'1.1-1' }}
                      </span>
                      <p class="text-[10px] text-rose-400 font-bold">Churn: {{ account.predictedChurnProbability * 100 | number:'1.0-0' }}%</p>
                    </div>
                  </div>

                  <!-- Early Warning Triggers Badges -->
                  <div class="flex flex-wrap gap-1.5 my-2.5">
                    @for (trigger of account.riskTriggers; track trigger.triggerId) {
                      <span
                        class="px-2 py-0.5 text-[10px] font-semibold rounded-md flex items-center gap-1 bg-rose-950 text-rose-300 border border-rose-800">
                        ⚠ {{ getTriggerLabel(trigger.triggerType) }}
                      </span>
                    }
                  </div>

                  <!-- 4-Dimension Progress Bars -->
                  <div class="grid grid-cols-4 gap-1.5 my-2 pt-2 border-t border-slate-800/60 text-[10px]">
                    <div>
                      <span class="text-slate-400 block mb-0.5">Adoption</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-rose-500 h-full" [style.width.%]="account.adoptionScore"></div>
                      </div>
                      <span class="font-mono text-slate-300">{{ account.adoptionScore | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-400 block mb-0.5">Sentiment</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-rose-400 h-full" [style.width.%]="account.sentimentScore"></div>
                      </div>
                      <span class="font-mono text-slate-300">{{ account.sentimentScore | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-400 block mb-0.5">Util</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-rose-500 h-full" [style.width.%]="account.utilizationScore"></div>
                      </div>
                      <span class="font-mono text-slate-300">{{ account.utilizationScore | number:'1.0-0' }}</span>
                    </div>
                    <div>
                      <span class="text-slate-400 block mb-0.5">Comm</span>
                      <div class="w-full bg-slate-800 h-1.5 rounded-full overflow-hidden">
                        <div class="bg-rose-500 h-full" [style.width.%]="account.commercialScore"></div>
                      </div>
                      <span class="font-mono text-slate-300">{{ account.commercialScore | number:'1.0-0' }}</span>
                    </div>
                  </div>

                  <div class="flex items-center justify-between text-xs text-slate-400 pt-2 border-t border-slate-800/60">
                    <span class="font-semibold text-rose-300">\${{ account.annualRecurringRevenue | number:'1.0-0' }} ARR</span>
                    <span class="text-rose-400 font-semibold text-[11px]">{{ account.activePlaybooks.length }} Rescue Playbooks</span>
                  </div>
                </div>
              } @empty {
                <div class="p-6 text-center text-xs text-slate-500 border border-dashed border-slate-800 rounded-xl">
                  No accounts in Critical tier.
                </div>
              }
            </div>
          </div>
        </section>
      }

      <!-- View 2: Tabular Matrix View -->
      @if (activeViewMode() === 'table') {
        <section class="bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-xl" data-testid="tabular-matrix-view">
          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-950/80 border-b border-slate-800 text-slate-400 uppercase text-[10px] tracking-wider font-semibold">
                <tr>
                  <th class="p-3.5">Account & Industry</th>
                  <th class="p-3.5">Tier & Composite</th>
                  <th class="p-3.5">Dimensions (Adopt/Sent/Util/Comm)</th>
                  <th class="p-3.5">Seats & Dormancy</th>
                  <th class="p-3.5">Support & NPS</th>
                  <th class="p-3.5">Renewal & ARR</th>
                  <th class="p-3.5">Playbooks</th>
                  <th class="p-3.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60 text-slate-300">
                @for (account of filteredAccounts(); track account.accountId) {
                  <tr class="hover:bg-slate-800/40 transition-colors">
                    <td class="p-3.5">
                      <div class="font-bold text-white text-sm">{{ account.accountName }}</div>
                      <div class="text-[11px] text-slate-400">{{ account.domain }} · {{ account.industry }}</div>
                    </td>
                    <td class="p-3.5">
                      <div class="flex items-center gap-2">
                        <span
                          class="px-2 py-0.5 rounded text-[10px] font-bold"
                          [ngClass]="{
                            'bg-emerald-950 text-emerald-300 border border-emerald-800': account.healthTier === 'Healthy',
                            'bg-amber-950 text-amber-300 border border-amber-800': account.healthTier === 'AtRisk',
                            'bg-rose-950 text-rose-300 border border-rose-800': account.healthTier === 'Critical'
                          }">
                          {{ account.healthTier }}
                        </span>
                        <span class="font-mono font-extrabold text-sm" [ngClass]="getHealthColorClass(account.compositeHealthScore)">
                          {{ account.compositeHealthScore | number:'1.1-1' }}
                        </span>
                      </div>
                      <div class="text-[10px] text-slate-500 mt-0.5">
                        Churn: {{ account.predictedChurnProbability * 100 | number:'1.0-0' }}%
                      </div>
                    </td>
                    <td class="p-3.5">
                      <div class="flex items-center gap-1.5 font-mono text-[11px]">
                        <span class="text-emerald-400" title="Adoption">{{ account.adoptionScore | number:'1.0-0' }}</span> /
                        <span class="text-cyan-400" title="Sentiment">{{ account.sentimentScore | number:'1.0-0' }}</span> /
                        <span class="text-indigo-400" title="Utilization">{{ account.utilizationScore | number:'1.0-0' }}</span> /
                        <span class="text-teal-400" title="Commercial">{{ account.commercialScore | number:'1.0-0' }}</span>
                      </div>
                    </td>
                    <td class="p-3.5">
                      <div>{{ account.activeSeats }} / {{ account.totalSeats }} active</div>
                      <div class="text-[10px]" [class.text-rose-400]="account.inactiveSeatRatio >= 0.35">
                        {{ account.inactiveSeatRatio * 100 | number:'1.0-0' }}% dormant
                      </div>
                    </td>
                    <td class="p-3.5">
                      <div class="flex items-center gap-2">
                        <span>{{ account.openTicketsCount }} open ({{ account.urgentTicketsCount }} urg)</span>
                      </div>
                      <div class="text-[10px]" [class.text-rose-400]="account.npsScore <= 0">
                        NPS: {{ account.npsScore }} (prev: {{ account.previousNpsScore }})
                      </div>
                    </td>
                    <td class="p-3.5">
                      <div class="font-semibold text-white">\${{ account.annualRecurringRevenue | number:'1.0-0' }}</div>
                      <div class="text-[10px] text-slate-400">{{ account.daysUntilRenewal }} days left</div>
                    </td>
                    <td class="p-3.5">
                      <span class="px-2 py-0.5 rounded-full text-[10px] bg-slate-800 text-slate-300">
                        {{ account.activePlaybooks.length }} active
                      </span>
                    </td>
                    <td class="p-3.5 text-right">
                      <div class="flex items-center justify-end gap-2">
                        <button
                          (click)="openSimulator(account)"
                          data-testid="open-simulator-btn"
                          class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-xs rounded text-slate-300 transition-colors">
                          Simulate
                        </button>
                        <button
                          (click)="openDeepDive(account)"
                          data-testid="deep-dive-btn"
                          class="px-2.5 py-1 bg-emerald-700 hover:bg-emerald-600 text-xs rounded font-medium text-white transition-colors">
                          Deep-Dive
                        </button>
                      </div>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
      }

      <!-- Modal 1: Account Deep-Dive Drawer / Modal -->
      @if (selectedAccount(); as acc) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4" data-testid="deep-dive-modal">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-4xl max-h-[90vh] overflow-y-auto shadow-2xl flex flex-col">
            <!-- Modal Header -->
            <div class="p-5 border-b border-slate-800 flex items-start justify-between">
              <div>
                <div class="flex items-center gap-2 mb-1">
                  <span
                    class="px-2.5 py-0.5 rounded-full text-xs font-bold"
                    [ngClass]="{
                      'bg-emerald-950 text-emerald-300 border border-emerald-800': acc.healthTier === 'Healthy',
                      'bg-amber-950 text-amber-300 border border-amber-800': acc.healthTier === 'AtRisk',
                      'bg-rose-950 text-rose-300 border border-rose-800': acc.healthTier === 'Critical'
                    }">
                    {{ acc.healthTier }} Tier
                  </span>
                  <span class="text-xs text-slate-400">CSM: {{ acc.assignedCsm }}</span>
                </div>
                <h2 class="text-xl font-bold text-white">{{ acc.accountName }}</h2>
                <p class="text-xs text-slate-400">{{ acc.domain }} · {{ acc.industry }} · \${{ acc.annualRecurringRevenue | number:'1.0-0' }} ARR</p>
              </div>

              <div class="flex items-center gap-3">
                <div class="text-right">
                  <div class="text-3xl font-extrabold font-mono" [ngClass]="getHealthColorClass(acc.compositeHealthScore)">
                    {{ acc.compositeHealthScore | number:'1.1-1' }}
                  </div>
                  <span class="text-[11px] text-slate-400">Composite Health</span>
                </div>
                <button
                  (click)="closeDeepDive()"
                  data-testid="close-deep-dive-btn"
                  class="p-2 text-slate-400 hover:text-white text-lg leading-none">✕</button>
              </div>
            </div>

            <!-- Modal Body -->
            <div class="p-6 flex flex-col gap-6 text-sm">
              <!-- 4-Dimension Breakdown Cards -->
              <div>
                <h4 class="text-xs font-bold uppercase tracking-wider text-slate-400 mb-3">Health Dimension Composition</h4>
                <div class="grid grid-cols-2 md:grid-cols-4 gap-3">
                  <div class="bg-slate-950 p-3.5 rounded-xl border border-slate-800">
                    <div class="flex items-center justify-between text-xs mb-1">
                      <span class="text-slate-400 font-medium">Adoption (35%)</span>
                      <span class="font-mono font-bold text-emerald-400">{{ acc.adoptionScore | number:'1.0-0' }}</span>
                    </div>
                    <div class="w-full bg-slate-800 h-2 rounded-full overflow-hidden">
                      <div class="bg-emerald-500 h-full" [style.width.%]="acc.adoptionScore"></div>
                    </div>
                    <span class="text-[10px] text-slate-500 mt-1 block">Weighted contribution: {{ acc.adoptionScore * 0.35 | number:'1.1-1' }}</span>
                  </div>

                  <div class="bg-slate-950 p-3.5 rounded-xl border border-slate-800">
                    <div class="flex items-center justify-between text-xs mb-1">
                      <span class="text-slate-400 font-medium">Sentiment (25%)</span>
                      <span class="font-mono font-bold text-cyan-400">{{ acc.sentimentScore | number:'1.0-0' }}</span>
                    </div>
                    <div class="w-full bg-slate-800 h-2 rounded-full overflow-hidden">
                      <div class="bg-cyan-500 h-full" [style.width.%]="acc.sentimentScore"></div>
                    </div>
                    <span class="text-[10px] text-slate-500 mt-1 block">Weighted contribution: {{ acc.sentimentScore * 0.25 | number:'1.1-1' }}</span>
                  </div>

                  <div class="bg-slate-950 p-3.5 rounded-xl border border-slate-800">
                    <div class="flex items-center justify-between text-xs mb-1">
                      <span class="text-slate-400 font-medium">Utilization (20%)</span>
                      <span class="font-mono font-bold text-indigo-400">{{ acc.utilizationScore | number:'1.0-0' }}</span>
                    </div>
                    <div class="w-full bg-slate-800 h-2 rounded-full overflow-hidden">
                      <div class="bg-indigo-500 h-full" [style.width.%]="acc.utilizationScore"></div>
                    </div>
                    <span class="text-[10px] text-slate-500 mt-1 block">Weighted contribution: {{ acc.utilizationScore * 0.20 | number:'1.1-1' }}</span>
                  </div>

                  <div class="bg-slate-950 p-3.5 rounded-xl border border-slate-800">
                    <div class="flex items-center justify-between text-xs mb-1">
                      <span class="text-slate-400 font-medium">Commercial (20%)</span>
                      <span class="font-mono font-bold text-teal-400">{{ acc.commercialScore | number:'1.0-0' }}</span>
                    </div>
                    <div class="w-full bg-slate-800 h-2 rounded-full overflow-hidden">
                      <div class="bg-teal-500 h-full" [style.width.%]="acc.commercialScore"></div>
                    </div>
                    <span class="text-[10px] text-slate-500 mt-1 block">Weighted contribution: {{ acc.commercialScore * 0.20 | number:'1.1-1' }}</span>
                  </div>
                </div>
              </div>

              <!-- Early Warning Churn Triggers -->
              <div>
                <h4 class="text-xs font-bold uppercase tracking-wider text-slate-400 mb-3 flex items-center justify-between">
                  <span>Early-Warning Churn Triggers</span>
                  <span class="text-xs font-normal text-slate-500">{{ acc.riskTriggers.length }} Detected</span>
                </h4>
                @if (acc.riskTriggers.length > 0) {
                  <div class="flex flex-col gap-2.5">
                    @for (trig of acc.riskTriggers; track trig.triggerId) {
                      <div
                        class="p-3 rounded-xl border flex items-start gap-3"
                        [ngClass]="{
                          'bg-rose-950/40 border-rose-900/60': trig.severity === 'Critical',
                          'bg-amber-950/40 border-amber-900/60': trig.severity === 'High',
                          'bg-slate-950 border-slate-800': trig.severity === 'Medium' || trig.severity === 'Low'
                        }">
                        <span class="text-base">⚠</span>
                        <div class="flex-1">
                          <div class="flex items-center gap-2">
                            <span class="font-semibold text-xs text-white">{{ getTriggerLabel(trig.triggerType) }}</span>
                            <span
                              class="text-[10px] uppercase font-bold px-1.5 py-0.2 rounded"
                              [ngClass]="{
                                'bg-rose-900 text-rose-200': trig.severity === 'Critical',
                                'bg-amber-900 text-amber-200': trig.severity === 'High',
                                'bg-slate-800 text-slate-300': trig.severity === 'Medium' || trig.severity === 'Low'
                              }">
                              {{ trig.severity }}
                            </span>
                          </div>
                          <p class="text-xs text-slate-300 mt-0.5">{{ trig.description }}</p>
                        </div>
                      </div>
                    }
                  </div>
                } @else {
                  <div class="p-3.5 bg-slate-950 rounded-xl border border-slate-800 text-xs text-emerald-400 flex items-center gap-2">
                    <span>✓</span> All early warning signals nominal. No critical churn triggers detected.
                  </div>
                }
              </div>

              <!-- Automated CSM Playbooks -->
              <div>
                <div class="flex items-center justify-between mb-3">
                  <h4 class="text-xs font-bold uppercase tracking-wider text-slate-400">Automated CSM Playbooks</h4>
                  <button
                    (click)="openAddPlaybookPrompt(acc)"
                    data-testid="add-playbook-btn"
                    class="text-xs text-emerald-400 hover:text-emerald-300 font-semibold">
                    + Add Custom Playbook
                  </button>
                </div>

                @if (acc.activePlaybooks.length > 0) {
                  <div class="flex flex-col gap-3">
                    @for (pb of acc.activePlaybooks; track pb.playbookId) {
                      <div
                        class="p-4 rounded-xl border bg-slate-950 border-slate-800 flex flex-col gap-2.5"
                        data-testid="playbook-item"
                        [attr.data-playbook-id]="pb.playbookId">
                        <div class="flex items-start justify-between gap-3">
                          <div>
                            <div class="flex items-center gap-2">
                              <h5 class="text-sm font-bold text-white">{{ pb.title }}</h5>
                              <span
                                class="text-[10px] font-semibold px-2 py-0.5 rounded"
                                [ngClass]="{
                                  'bg-rose-950 text-rose-300 border border-rose-800': pb.priority === 'Urgent',
                                  'bg-amber-950 text-amber-300 border border-amber-800': pb.priority === 'High',
                                  'bg-slate-800 text-slate-300': pb.priority === 'Medium' || pb.priority === 'Low'
                                }">
                                {{ pb.priority }}
                              </span>
                              <span
                                class="text-[10px] font-semibold px-2 py-0.5 rounded"
                                [ngClass]="{
                                  'bg-blue-950 text-blue-300 border border-blue-800': pb.status === 'Active',
                                  'bg-slate-800 text-slate-400': pb.status === 'Suggested',
                                  'bg-emerald-950 text-emerald-300 border border-emerald-800': pb.status === 'Completed',
                                  'bg-slate-900 text-slate-500': pb.status === 'Dismissed'
                                }">
                                {{ pb.status }}
                              </span>
                            </div>
                            <p class="text-xs text-slate-400 mt-1">{{ pb.objective }}</p>
                            <span class="text-[11px] text-cyan-400 block mt-0.5">Role: {{ pb.assignedRole }}</span>
                          </div>

                          <div class="flex items-center gap-2">
                            @if (pb.status === 'Suggested') {
                              <button
                                (click)="updatePlaybookStatus(acc, pb, 'Active')"
                                data-testid="activate-playbook-btn"
                                class="px-3 py-1.5 bg-cyan-700 hover:bg-cyan-600 text-white text-xs font-semibold rounded-lg transition-colors">
                                Activate
                              </button>
                            }
                            @if (pb.status === 'Active') {
                              <button
                                (click)="updatePlaybookStatus(acc, pb, 'Completed')"
                                data-testid="complete-playbook-btn"
                                class="px-3 py-1.5 bg-emerald-700 hover:bg-emerald-600 text-white text-xs font-semibold rounded-lg transition-colors">
                                Mark Done
                              </button>
                            }
                            @if (pb.status === 'Completed') {
                              <span class="text-xs text-emerald-400 font-semibold">✓ Completed</span>
                            }
                          </div>
                        </div>

                        <!-- Recommended Action Steps -->
                        <div class="bg-slate-900/60 p-2.5 rounded-lg border border-slate-800/60 mt-1">
                          <span class="text-[11px] font-semibold text-slate-300 block mb-1">Recommended Action Protocol:</span>
                          <ul class="text-xs text-slate-400 space-y-1 list-disc list-inside">
                            @for (action of pb.recommendedActions; track action) {
                              <li>{{ action }}</li>
                            }
                          </ul>
                        </div>
                      </div>
                    }
                  </div>
                } @else {
                  <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 text-xs text-slate-500 text-center">
                    No active playbooks currently assigned to this account.
                  </div>
                }
              </div>

              <!-- Historical Trendline Snapshots -->
              <div>
                <h4 class="text-xs font-bold uppercase tracking-wider text-slate-400 mb-3">Historical Health Snapshots</h4>
                <div class="bg-slate-950 rounded-xl border border-slate-800 p-3 overflow-x-auto">
                  <div class="flex items-center gap-4 min-w-max">
                    @for (snap of acc.historicalSnapshots; track snap.snapshotId) {
                      <div class="p-2.5 rounded-lg bg-slate-900 border border-slate-800 flex flex-col items-center gap-1 text-center w-28">
                        <span class="text-[10px] text-slate-500">{{ snap.recordedAtUtc | date:'shortDate' }}</span>
                        <span class="text-base font-extrabold font-mono" [ngClass]="getHealthColorClass(snap.compositeHealthScore)">
                          {{ snap.compositeHealthScore | number:'1.1-1' }}
                        </span>
                        <span
                          class="text-[9px] uppercase font-bold px-1.5 rounded"
                          [ngClass]="{
                            'bg-emerald-950 text-emerald-400': snap.healthTier === 'Healthy',
                            'bg-amber-950 text-amber-400': snap.healthTier === 'AtRisk',
                            'bg-rose-950 text-rose-400': snap.healthTier === 'Critical'
                          }">
                          {{ snap.healthTier }}
                        </span>
                      </div>
                    }
                  </div>
                </div>
              </div>
            </div>

            <!-- Modal Footer -->
            <div class="p-4 border-t border-slate-800 flex items-center justify-between bg-slate-950/50">
              <button
                (click)="openSimulator(acc)"
                data-testid="deep-dive-simulator-btn"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-lg transition-colors">
                ⚡ What-If Simulator
              </button>
              <button
                (click)="closeDeepDive()"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs rounded-lg transition-colors">
                Close
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Modal 2: What-If Health Simulator Modal -->
      @if (showSimulationModal() && simulationAccount(); as simAcc) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4" data-testid="simulation-modal">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-2xl overflow-hidden shadow-2xl flex flex-col">
            <div class="p-5 border-b border-slate-800 flex items-center justify-between">
              <div>
                <h3 class="text-base font-bold text-white">What-If Retention & Health Simulator</h3>
                <p class="text-xs text-slate-400">Simulate metric remediations for {{ simAcc.accountName }}</p>
              </div>
              <button (click)="closeSimulator()" class="text-slate-400 hover:text-white">&times;</button>
            </div>

            <div class="p-6 flex flex-col gap-4 text-xs">
              <!-- Live Preview Card -->
              <div class="bg-slate-950 p-4 rounded-xl border border-slate-800 flex items-center justify-between">
                <div>
                  <span class="text-slate-400 text-xs">Simulated Composite Health</span>
                  <div class="flex items-center gap-3 mt-1">
                    <span class="text-3xl font-extrabold font-mono" [ngClass]="getHealthColorClass(simulatedComposite())" data-testid="sim-preview-score">
                      {{ simulatedComposite() | number:'1.1-1' }}
                    </span>
                    <span
                      class="px-2.5 py-0.5 text-xs font-bold rounded-full"
                      [ngClass]="{
                        'bg-emerald-950 text-emerald-300 border border-emerald-800': simulatedTier() === 'Healthy',
                        'bg-amber-950 text-amber-300 border border-amber-800': simulatedTier() === 'AtRisk',
                        'bg-rose-950 text-rose-300 border border-rose-800': simulatedTier() === 'Critical'
                      }"
                      data-testid="sim-preview-tier">
                      {{ simulatedTier() }} Tier
                    </span>
                  </div>
                </div>
                <div class="text-right">
                  <span class="text-slate-400 text-xs">Simulated Churn Hazard</span>
                  <div class="text-xl font-bold text-rose-300 mt-1">
                    {{ simulatedChurnProb() * 100 | number:'1.0-0' }}%
                  </div>
                </div>
              </div>

              <!-- Simulator Sliders -->
              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label class="block font-semibold text-slate-300 mb-1">
                    Adoption Score (35% weight): <span class="font-mono text-emerald-400">{{ simAdoption() }}</span>
                  </label>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    [ngModel]="simAdoption()"
                    (ngModelChange)="simAdoption.set(+$event)"
                    data-testid="sim-adoption-slider"
                    class="w-full accent-emerald-500" />
                </div>

                <div>
                  <label class="block font-semibold text-slate-300 mb-1">
                    Sentiment Score (25% weight): <span class="font-mono text-cyan-400">{{ simSentiment() }}</span>
                  </label>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    [ngModel]="simSentiment()"
                    (ngModelChange)="simSentiment.set(+$event)"
                    data-testid="sim-sentiment-slider"
                    class="w-full accent-cyan-500" />
                </div>

                <div>
                  <label class="block font-semibold text-slate-300 mb-1">
                    Utilization Score (20% weight): <span class="font-mono text-indigo-400">{{ simUtilization() }}</span>
                  </label>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    [ngModel]="simUtilization()"
                    (ngModelChange)="simUtilization.set(+$event)"
                    data-testid="sim-utilization-slider"
                    class="w-full accent-indigo-500" />
                </div>

                <div>
                  <label class="block font-semibold text-slate-300 mb-1">
                    Commercial Score (20% weight): <span class="font-mono text-teal-400">{{ simCommercial() }}</span>
                  </label>
                  <input
                    type="range"
                    min="0"
                    max="100"
                    [ngModel]="simCommercial()"
                    (ngModelChange)="simCommercial.set(+$event)"
                    data-testid="sim-commercial-slider"
                    class="w-full accent-teal-500" />
                </div>
              </div>

              <div class="grid grid-cols-3 gap-3 pt-3 border-t border-slate-800">
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Active Seats (of {{ simAcc.totalSeats }})</label>
                  <input
                    type="number"
                    [ngModel]="simActiveSeats()"
                    (ngModelChange)="simActiveSeats.set(+$event)"
                    min="0"
                    [max]="simAcc.totalSeats"
                    data-testid="sim-seats-input"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200" />
                </div>
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Open Tickets</label>
                  <input
                    type="number"
                    [ngModel]="simOpenTickets()"
                    (ngModelChange)="simOpenTickets.set(+$event)"
                    min="0"
                    data-testid="sim-tickets-input"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200" />
                </div>
                <div>
                  <label class="block font-medium text-slate-400 mb-1">NPS Score</label>
                  <input
                    type="number"
                    [ngModel]="simNps()"
                    (ngModelChange)="simNps.set(+$event)"
                    min="-100"
                    max="100"
                    data-testid="sim-nps-input"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200" />
                </div>
              </div>
            </div>

            <div class="p-4 border-t border-slate-800 flex justify-end gap-3 bg-slate-950/60">
              <button
                (click)="closeSimulator()"
                class="px-4 py-2 bg-slate-800 text-slate-300 text-xs rounded-lg">Cancel</button>
              <button
                (click)="applySimulation(simAcc)"
                data-testid="apply-simulation-btn"
                class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-xs rounded-lg transition-colors">
                Commit Recalculation
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Modal 3: Register New Account Modal -->
      @if (showCreateModal()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4" data-testid="register-account-modal">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg overflow-hidden shadow-2xl flex flex-col">
            <div class="p-5 border-b border-slate-800 flex items-center justify-between">
              <h3 class="text-base font-bold text-white">Register Account for Health Tracking</h3>
              <button (click)="closeCreateModal()" class="text-slate-400 hover:text-white">&times;</button>
            </div>

            <div class="p-6 flex flex-col gap-3 text-xs max-h-[75vh] overflow-y-auto">
              <div>
                <label class="block font-medium text-slate-400 mb-1">Company / Account Name</label>
                <input
                  type="text"
                  [(ngModel)]="newAccountName"
                  placeholder="e.g. Riyadh Cloud Banking"
                  data-testid="new-account-name"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" />
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Domain</label>
                  <input
                    type="text"
                    [(ngModel)]="newDomain"
                    placeholder="e.g. riyadhbanking.sa"
                    data-testid="new-domain"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" />
                </div>
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Industry</label>
                  <input
                    type="text"
                    [(ngModel)]="newIndustry"
                    placeholder="e.g. Banking & FinTech"
                    data-testid="new-industry"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" />
                </div>
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Assigned CSM</label>
                  <input
                    type="text"
                    [(ngModel)]="newAssignedCsm"
                    placeholder="e.g. Leila Al-Husseini"
                    data-testid="new-csm"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" />
                </div>
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Annual Recurring Revenue ($)</label>
                  <input
                    type="number"
                    [(ngModel)]="newArr"
                    data-testid="new-arr"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" />
                </div>
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Total Seats</label>
                  <input
                    type="number"
                    [(ngModel)]="newTotalSeats"
                    data-testid="new-total-seats"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" />
                </div>
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Active Seats</label>
                  <input
                    type="number"
                    [(ngModel)]="newActiveSeats"
                    data-testid="new-active-seats"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200" />
                </div>
              </div>

              <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 grid grid-cols-2 gap-3">
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Adoption Score (0-100)</label>
                  <input type="number" [(ngModel)]="newAdoption" min="0" max="100" class="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-slate-200" />
                </div>
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Sentiment Score (0-100)</label>
                  <input type="number" [(ngModel)]="newSentiment" min="0" max="100" class="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-slate-200" />
                </div>
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Utilization Score (0-100)</label>
                  <input type="number" [(ngModel)]="newUtilization" min="0" max="100" class="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-slate-200" />
                </div>
                <div>
                  <label class="block font-medium text-slate-400 mb-1">Commercial Score (0-100)</label>
                  <input type="number" [(ngModel)]="newCommercial" min="0" max="100" class="w-full bg-slate-900 border border-slate-700 rounded p-1.5 text-slate-200" />
                </div>
              </div>
            </div>

            <div class="p-4 border-t border-slate-800 flex justify-end gap-3 bg-slate-950/60">
              <button (click)="closeCreateModal()" class="px-4 py-2 bg-slate-800 text-slate-300 text-xs rounded-lg">Cancel</button>
              <button
                (click)="saveNewAccount()"
                data-testid="submit-account-btn"
                class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-xs rounded-lg transition-colors">
                Save & Track
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class CustomerHealthRadarStudioComponent {
  public readonly translationService: TranslationService | null = null;
  public readonly isRtl = computed(() => this.translationService?.currentLocale() === 'ar-AR');

  constructor(@Optional() translationService?: TranslationService) {
    if (translationService) {
      this.translationService = translationService;
    } else {
      try {
        this.translationService = inject(TranslationService, { optional: true });
      } catch {
        this.translationService = null;
      }
    }
  }

  // Weights
  public static readonly AdoptionWeight = 0.35;
  public static readonly SentimentWeight = 0.25;
  public static readonly UtilizationWeight = 0.20;
  public static readonly CommercialWeight = 0.20;

  // Primary Signals
  public readonly accounts = signal<CustomerAccountHealthRecord[]>(this.getInitialSeededAccounts());
  public readonly searchQuery = signal<string>('');
  public readonly selectedTierFilter = signal<string>('ALL');
  public readonly selectedTriggerFilter = signal<string>('ALL');
  public readonly activeViewMode = signal<'grid' | 'table'>('grid');

  // Modal / Interaction Signals
  public readonly selectedAccount = signal<CustomerAccountHealthRecord | null>(null);
  public readonly showSimulationModal = signal<boolean>(false);
  public readonly simulationAccount = signal<CustomerAccountHealthRecord | null>(null);
  public readonly showCreateModal = signal<boolean>(false);
  public readonly actionToast = signal<{ message: string; type: 'success' | 'warning' | 'info' } | null>(null);

  // Simulation Form Signals
  public readonly simAdoption = signal<number>(80);
  public readonly simSentiment = signal<number>(80);
  public readonly simUtilization = signal<number>(80);
  public readonly simCommercial = signal<number>(80);
  public readonly simActiveSeats = signal<number>(50);
  public readonly simOpenTickets = signal<number>(0);
  public readonly simUrgentTickets = signal<number>(0);
  public readonly simNps = signal<number>(50);

  // Register Account Form Fields
  public newAccountName: string = '';
  public newDomain: string = '';
  public newIndustry: string = 'Enterprise Tech';
  public newAssignedCsm: string = 'Leila Al-Husseini';
  public newArr: number = 75000;
  public newTotalSeats: number = 50;
  public newActiveSeats: number = 45;
  public newAdoption: number = 85;
  public newSentiment: number = 80;
  public newUtilization: number = 85;
  public newCommercial: number = 90;

  // Computed KPIs
  public readonly totalAccountsCount = computed(() => this.accounts().length);

  public readonly healthyAccounts = computed(() =>
    this.accounts().filter(a => a.healthTier === 'Healthy')
  );

  public readonly atRiskAccounts = computed(() =>
    this.accounts().filter(a => a.healthTier === 'AtRisk')
  );

  public readonly criticalAccounts = computed(() =>
    this.accounts().filter(a => a.healthTier === 'Critical')
  );

  public readonly healthyCount = computed(() => this.healthyAccounts().length);
  public readonly atRiskCount = computed(() => this.atRiskAccounts().length);
  public readonly criticalCount = computed(() => this.criticalAccounts().length);

  public readonly averageHealthScore = computed(() => {
    const list = this.accounts();
    if (list.length === 0) return 0;
    const sum = list.reduce((acc, a) => acc + a.compositeHealthScore, 0);
    return Math.round((sum / list.length) * 10) / 10;
  });

  public readonly totalArr = computed(() =>
    this.accounts().reduce((sum, a) => sum + a.annualRecurringRevenue, 0)
  );

  public readonly arrAtRisk = computed(() =>
    this.accounts()
      .filter(a => a.healthTier !== 'Healthy')
      .reduce((sum, a) => sum + a.annualRecurringRevenue, 0)
  );

  public readonly activePlaybooksCount = computed(() =>
    this.accounts().reduce((sum, a) =>
      sum + a.activePlaybooks.filter(p => p.status === 'Active' || p.status === 'Suggested').length, 0)
  );

  // Computed Filtered List
  public readonly filteredAccounts = computed(() => {
    let list = this.accounts();
    const tier = this.selectedTierFilter();
    const trigger = this.selectedTriggerFilter();
    const query = this.searchQuery().trim().toLowerCase();

    if (tier !== 'ALL') {
      list = list.filter(a => a.healthTier === tier);
    }

    if (trigger !== 'ALL') {
      list = list.filter(a => a.riskTriggers.some(t => t.triggerType === trigger));
    }

    if (query) {
      list = list.filter(a =>
        a.accountName.toLowerCase().includes(query) ||
        a.domain.toLowerCase().includes(query) ||
        a.industry.toLowerCase().includes(query) ||
        a.assignedCsm.toLowerCase().includes(query)
      );
    }

    return list;
  });

  // Simulated Result Computeds
  public readonly simulatedComposite = computed(() => {
    const adopt = Math.max(0, Math.min(100, this.simAdoption()));
    const sent = Math.max(0, Math.min(100, this.simSentiment()));
    const util = Math.max(0, Math.min(100, this.simUtilization()));
    const comm = Math.max(0, Math.min(100, this.simCommercial()));

    const comp = (adopt * 0.35) + (sent * 0.25) + (util * 0.20) + (comm * 0.20);
    return Math.round(comp * 10) / 10;
  });

  public readonly simulatedTier = computed<HealthTier>(() => {
    const comp = this.simulatedComposite();
    if (comp >= 75.0) return 'Healthy';
    if (comp >= 50.0) return 'AtRisk';
    return 'Critical';
  });

  public readonly simulatedChurnProb = computed(() => {
    const comp = this.simulatedComposite();
    if (comp >= 75.0) {
      return Math.round(Math.max(0.01, Math.min(0.25, (100 - comp) * 0.005)) * 100) / 100;
    }
    if (comp >= 50.0) {
      return Math.round(Math.max(0.25, Math.min(0.65, 0.25 + ((75 - comp) * 0.012))) * 100) / 100;
    }
    return Math.round(Math.max(0.65, Math.min(0.98, 0.65 + ((50 - comp) * 0.006))) * 100) / 100;
  });

  public getTierAccounts(tier: HealthTier): CustomerAccountHealthRecord[] {
    return this.filteredAccounts().filter(a => a.healthTier === tier);
  }

  public getTriggerLabel(type: RiskTriggerType): string {
    switch (type) {
      case 'NpsDrop': return 'NPS Drop';
      case 'TicketSpike': return 'Ticket Spike';
      case 'InactiveSeatRatio': return 'Dormant Seats';
      case 'ContractRenewalRisk': return 'Renewal Due';
      case 'PaymentOverdue': return 'Invoice Overdue';
    }
  }

  public getHealthColorClass(score: number): string {
    if (score >= 75.0) return 'text-emerald-400';
    if (score >= 50.0) return 'text-amber-400';
    return 'text-rose-400';
  }

  public getHealthBarClass(score: number): string {
    if (score >= 75.0) return 'bg-emerald-500';
    if (score >= 50.0) return 'bg-amber-500';
    return 'bg-rose-500';
  }

  // Deep-Dive Modal Interactions
  public openDeepDive(account: CustomerAccountHealthRecord): void {
    this.selectedAccount.set(account);
  }

  public closeDeepDive(): void {
    this.selectedAccount.set(null);
  }

  // Simulator Modal Interactions
  public openSimulator(account: CustomerAccountHealthRecord): void {
    this.simulationAccount.set(account);
    this.simAdoption.set(account.adoptionScore);
    this.simSentiment.set(account.sentimentScore);
    this.simUtilization.set(account.utilizationScore);
    this.simCommercial.set(account.commercialScore);
    this.simActiveSeats.set(account.activeSeats);
    this.simOpenTickets.set(account.openTicketsCount);
    this.simUrgentTickets.set(account.urgentTicketsCount);
    this.simNps.set(account.npsScore);
    this.showSimulationModal.set(true);
  }

  public closeSimulator(): void {
    this.showSimulationModal.set(false);
    this.simulationAccount.set(null);
  }

  public applySimulation(account: CustomerAccountHealthRecord): void {
    const comp = this.simulatedComposite();
    const tier = this.simulatedTier();
    const churn = this.simulatedChurnProb();
    const activeSeats = Math.min(account.totalSeats, Math.max(0, this.simActiveSeats()));
    const inactiveRatio = Math.round(((account.totalSeats - activeSeats) / account.totalSeats) * 1000) / 1000;

    const newSnapshot: HealthScoreSnapshotRecord = {
      snapshotId: 'sim-snap-' + Date.now(),
      recordedAtUtc: new Date().toISOString(),
      compositeHealthScore: comp,
      adoptionScore: this.simAdoption(),
      sentimentScore: this.simSentiment(),
      utilizationScore: this.simUtilization(),
      commercialScore: this.simCommercial(),
      healthTier: tier
    };

    const updated: CustomerAccountHealthRecord = {
      ...account,
      adoptionScore: this.simAdoption(),
      sentimentScore: this.simSentiment(),
      utilizationScore: this.simUtilization(),
      commercialScore: this.simCommercial(),
      activeSeats: activeSeats,
      inactiveSeatRatio: inactiveRatio,
      openTicketsCount: this.simOpenTickets(),
      urgentTicketsCount: this.simUrgentTickets(),
      npsScore: this.simNps(),
      compositeHealthScore: comp,
      healthTier: tier,
      predictedChurnProbability: churn,
      lastEvaluatedAtUtc: new Date().toISOString(),
      historicalSnapshots: [...account.historicalSnapshots, newSnapshot]
    };

    this.accounts.update(list => list.map(a => a.accountId === account.accountId ? updated : a));
    if (this.selectedAccount()?.accountId === account.accountId) {
      this.selectedAccount.set(updated);
    }

    this.closeSimulator();
    this.actionToast.set({
      message: `Recalculation applied to ${account.accountName}: Score updated to ${comp} (${tier}).`,
      type: 'success'
    });
  }

  // Playbook Management
  public updatePlaybookStatus(account: CustomerAccountHealthRecord, playbook: CsmPlaybookRecord, newStatus: PlaybookStatus): void {
    const updatedPlaybooks = account.activePlaybooks.map(p =>
      p.playbookId === playbook.playbookId
        ? {
            ...p,
            status: newStatus,
            completedAtUtc: newStatus === 'Completed' ? new Date().toISOString() : p.completedAtUtc
          }
        : p
    );

    const updatedAccount = { ...account, activePlaybooks: updatedPlaybooks };
    this.accounts.update(list => list.map(a => a.accountId === account.accountId ? updatedAccount : a));
    if (this.selectedAccount()?.accountId === account.accountId) {
      this.selectedAccount.set(updatedAccount);
    }

    this.actionToast.set({
      message: `Playbook "${playbook.title}" updated to status: ${newStatus}.`,
      type: 'info'
    });
  }

  public openAddPlaybookPrompt(account: CustomerAccountHealthRecord): void {
    const title = 'Executive sponsor check-in (Manual Urgent)';
    const customPlaybook: CsmPlaybookRecord = {
      playbookId: 'custom-pb-' + Date.now(),
      accountId: account.accountId,
      accountName: account.accountName,
      title: title,
      playbookType: 'ExecutiveSponsorCheckin',
      priority: 'Urgent',
      status: 'Active',
      objective: 'Direct leadership intervention to secure client retention and roadmap alignment.',
      assignedRole: 'VP of Customer Success',
      recommendedActions: [
        'Convene urgent alignment sync with C-suite sponsor within 48 hours.',
        'Review contract terms and address strategic escalation items.'
      ],
      notes: 'Manually logged by CSM desk operator.',
      createdAtUtc: new Date().toISOString()
    };

    const updatedAccount = {
      ...account,
      activePlaybooks: [...account.activePlaybooks, customPlaybook]
    };

    this.accounts.update(list => list.map(a => a.accountId === account.accountId ? updatedAccount : a));
    if (this.selectedAccount()?.accountId === account.accountId) {
      this.selectedAccount.set(updatedAccount);
    }

    this.actionToast.set({
      message: `Custom playbook added for ${account.accountName}.`,
      type: 'success'
    });
  }

  // Create Account Modal Interactions
  public openCreateModal(): void {
    this.newAccountName = '';
    this.newDomain = '';
    this.newIndustry = 'FinTech & Commerce';
    this.newAssignedCsm = 'Leila Al-Husseini';
    this.newArr = 95000;
    this.newTotalSeats = 60;
    this.newActiveSeats = 54;
    this.newAdoption = 88;
    this.newSentiment = 82;
    this.newUtilization = 90;
    this.newCommercial = 92;
    this.showCreateModal.set(true);
  }

  public closeCreateModal(): void {
    this.showCreateModal.set(false);
  }

  public saveNewAccount(): void {
    if (!this.newAccountName.trim()) {
      this.actionToast.set({ message: 'Account name is required.', type: 'warning' });
      return;
    }

    const totalSeats = Math.max(1, this.newTotalSeats);
    const activeSeats = Math.max(0, Math.min(totalSeats, this.newActiveSeats));
    const inactiveRatio = Math.round(((totalSeats - activeSeats) / totalSeats) * 1000) / 1000;

    const comp = Math.round(
      ((this.newAdoption * 0.35) + (this.newSentiment * 0.25) + (this.newUtilization * 0.20) + (this.newCommercial * 0.20)) * 10
    ) / 10;

    const tier: HealthTier = comp >= 75.0 ? 'Healthy' : comp >= 50.0 ? 'AtRisk' : 'Critical';
    const churnProb = comp >= 75.0 ? 0.08 : comp >= 50.0 ? 0.35 : 0.78;

    const newAcc: CustomerAccountHealthRecord = {
      accountId: 'acc-' + Date.now(),
      tenantId: '11111111-1111-1111-1111-111111111111',
      accountName: this.newAccountName.trim(),
      domain: this.newDomain.trim() || 'example.com',
      industry: this.newIndustry.trim(),
      assignedCsm: this.newAssignedCsm.trim(),
      annualRecurringRevenue: this.newArr,
      totalSeats: totalSeats,
      activeSeats: activeSeats,
      inactiveSeatRatio: inactiveRatio,
      npsScore: 60,
      previousNpsScore: 60,
      openTicketsCount: 0,
      urgentTicketsCount: 0,
      daysUntilRenewal: 180,
      daysOverdueInvoice: 0,
      adoptionScore: this.newAdoption,
      sentimentScore: this.newSentiment,
      utilizationScore: this.newUtilization,
      commercialScore: this.newCommercial,
      compositeHealthScore: comp,
      healthTier: tier,
      predictedChurnProbability: churnProb,
      lastEvaluatedAtUtc: new Date().toISOString(),
      riskTriggers: [],
      activePlaybooks: [],
      historicalSnapshots: [
        {
          snapshotId: 'snap-' + Date.now(),
          recordedAtUtc: new Date().toISOString(),
          compositeHealthScore: comp,
          adoptionScore: this.newAdoption,
          sentimentScore: this.newSentiment,
          utilizationScore: this.newUtilization,
          commercialScore: this.newCommercial,
          healthTier: tier
        }
      ],
      tags: ['Tracked', 'CustomerHealth']
    };

    this.accounts.update(list => [newAcc, ...list]);
    this.closeCreateModal();
    this.actionToast.set({
      message: `Account "${newAcc.accountName}" registered successfully with score ${comp} (${tier}).`,
      type: 'success'
    });
  }

  // Tenant-Wide Recalculation
  public recalculateAllRadar(): void {
    const updated = this.accounts().map(acc => {
      const comp = Math.round(
        ((acc.adoptionScore * 0.35) + (acc.sentimentScore * 0.25) + (acc.utilizationScore * 0.20) + (acc.commercialScore * 0.20)) * 10
      ) / 10;
      const tier: HealthTier = comp >= 75.0 ? 'Healthy' : comp >= 50.0 ? 'AtRisk' : 'Critical';
      return {
        ...acc,
        compositeHealthScore: comp,
        healthTier: tier,
        lastEvaluatedAtUtc: new Date().toISOString()
      };
    });

    this.accounts.set(updated);
    this.actionToast.set({
      message: 'Radar recalculation completed across all accounts.',
      type: 'success'
    });
  }

  // Reset to Defaults
  public resetToDefaults(): void {
    this.accounts.set(this.getInitialSeededAccounts());
    this.selectedAccount.set(null);
    this.searchQuery.set('');
    this.selectedTierFilter.set('ALL');
    this.selectedTriggerFilter.set('ALL');
    this.actionToast.set({
      message: 'Retention health radar reset to seeded enterprise demo dataset.',
      type: 'info'
    });
  }

  // Seeded Demo Accounts
  private getInitialSeededAccounts(): CustomerAccountHealthRecord[] {
    const now = Date.now();
    return [
      {
        accountId: '20000000-0000-0000-0000-000000000001',
        tenantId: '11111111-1111-1111-1111-111111111111',
        accountName: 'Aramco Digital Energy',
        domain: 'aramcodigital.sa',
        industry: 'Energy & Utilities',
        assignedCsm: 'Leila Al-Husseini',
        annualRecurringRevenue: 240000,
        totalSeats: 150,
        activeSeats: 142,
        inactiveSeatRatio: 0.053,
        npsScore: 68,
        previousNpsScore: 65,
        openTicketsCount: 1,
        urgentTicketsCount: 0,
        daysUntilRenewal: 180,
        daysOverdueInvoice: 0,
        adoptionScore: 92,
        sentimentScore: 88,
        utilizationScore: 94,
        commercialScore: 95,
        compositeHealthScore: 92.0,
        healthTier: 'Healthy',
        predictedChurnProbability: 0.04,
        lastEvaluatedAtUtc: new Date(now - 2 * 3600000).toISOString(),
        riskTriggers: [],
        activePlaybooks: [],
        historicalSnapshots: [
          {
            snapshotId: 'seed-snap-1a',
            recordedAtUtc: new Date(now - 60 * 86400000).toISOString(),
            compositeHealthScore: 89.0,
            adoptionScore: 90,
            sentimentScore: 85,
            utilizationScore: 92,
            commercialScore: 92,
            healthTier: 'Healthy'
          },
          {
            snapshotId: 'seed-snap-1b',
            recordedAtUtc: new Date(now - 30 * 86400000).toISOString(),
            compositeHealthScore: 91.0,
            adoptionScore: 91,
            sentimentScore: 87,
            utilizationScore: 93,
            commercialScore: 94,
            healthTier: 'Healthy'
          },
          {
            snapshotId: 'seed-snap-1c',
            recordedAtUtc: new Date(now - 2 * 3600000).toISOString(),
            compositeHealthScore: 92.0,
            adoptionScore: 92,
            sentimentScore: 88,
            utilizationScore: 94,
            commercialScore: 95,
            healthTier: 'Healthy'
          }
        ],
        tags: ['Enterprise', 'GCC', 'ExpansionTarget']
      },
      {
        accountId: '20000000-0000-0000-0000-000000000002',
        tenantId: '11111111-1111-1111-1111-111111111111',
        accountName: 'Al-Rajhi Retail Tech',
        domain: 'alrajhiretail.com',
        industry: 'Financial Retail & FinTech',
        assignedCsm: 'Omar Al-Farhan',
        annualRecurringRevenue: 120000,
        totalSeats: 80,
        activeSeats: 48,
        inactiveSeatRatio: 0.40,
        npsScore: 22,
        previousNpsScore: 45,
        openTicketsCount: 3,
        urgentTicketsCount: 0,
        daysUntilRenewal: 45,
        daysOverdueInvoice: 0,
        adoptionScore: 58,
        sentimentScore: 60,
        utilizationScore: 60,
        commercialScore: 75,
        compositeHealthScore: 62.3,
        healthTier: 'AtRisk',
        predictedChurnProbability: 0.38,
        lastEvaluatedAtUtc: new Date(now - 5 * 3600000).toISOString(),
        riskTriggers: [
          {
            triggerId: 'trig-seed-2a',
            triggerType: 'InactiveSeatRatio',
            severity: 'High',
            description: 'Elevated inactive seat ratio: 40% of licensed seats idle (32/80).',
            currentMetricValue: 40.0,
            threshold: 35.0,
            detectedAtUtc: new Date(now - 5 * 3600000).toISOString()
          },
          {
            triggerId: 'trig-seed-2b',
            triggerType: 'NpsDrop',
            severity: 'High',
            description: 'Severe NPS decline of 23 points (from 45 down to 22).',
            currentMetricValue: 22,
            threshold: 25,
            detectedAtUtc: new Date(now - 5 * 3600000).toISOString()
          },
          {
            triggerId: 'trig-seed-2c',
            triggerType: 'ContractRenewalRisk',
            severity: 'High',
            description: 'Contract renewal approaching in 45 days with impaired health (62.3).',
            currentMetricValue: 45,
            threshold: 60,
            detectedAtUtc: new Date(now - 5 * 3600000).toISOString()
          }
        ],
        activePlaybooks: [
          {
            playbookId: 'pb-seed-2a',
            accountId: '20000000-0000-0000-0000-000000000002',
            accountName: 'Al-Rajhi Retail Tech',
            title: 'Adoption workshop',
            playbookType: 'AdoptionWorkshop',
            priority: 'High',
            status: 'Active',
            objective: 'Drive user onboarding and feature activation to eliminate seat dormancy.',
            assignedRole: 'Lead Customer Success Manager',
            recommendedActions: [
              'Conduct tailored 45-minute interactive workflow training session with department leads.',
              'Deploy in-app role-based onboarding checklists and guided tooltips for inactive users.',
              'Provide customized automation template library for immediate time-to-value.'
            ],
            notes: 'CSM scheduled session for Thursday with retail admin.',
            createdAtUtc: new Date(now - 5 * 3600000).toISOString()
          },
          {
            playbookId: 'pb-seed-2b',
            accountId: '20000000-0000-0000-0000-000000000002',
            accountName: 'Al-Rajhi Retail Tech',
            title: 'Executive sponsor check-in',
            playbookType: 'ExecutiveSponsorCheckin',
            priority: 'High',
            status: 'Suggested',
            objective: 'Restore executive relationship, realign on business goals, and clear blockers.',
            assignedRole: 'VP of Customer Success',
            recommendedActions: [
              'Convene 30-minute urgent alignment sync with primary VP/C-suite sponsor.',
              'Review customer ROI scorecard and present targeted executive remediation roadmap.'
            ],
            notes: 'High ARR account ($120k) with AtRisk status.',
            createdAtUtc: new Date(now - 5 * 3600000).toISOString()
          }
        ],
        historicalSnapshots: [
          {
            snapshotId: 'seed-snap-2a',
            recordedAtUtc: new Date(now - 60 * 86400000).toISOString(),
            compositeHealthScore: 78.5,
            adoptionScore: 76,
            sentimentScore: 80,
            utilizationScore: 80,
            commercialScore: 80,
            healthTier: 'Healthy'
          },
          {
            snapshotId: 'seed-snap-2b',
            recordedAtUtc: new Date(now - 30 * 86400000).toISOString(),
            compositeHealthScore: 70.0,
            adoptionScore: 68,
            sentimentScore: 72,
            utilizationScore: 70,
            commercialScore: 75,
            healthTier: 'AtRisk'
          },
          {
            snapshotId: 'seed-snap-2c',
            recordedAtUtc: new Date(now - 5 * 3600000).toISOString(),
            compositeHealthScore: 62.3,
            adoptionScore: 58,
            sentimentScore: 60,
            utilizationScore: 60,
            commercialScore: 75,
            healthTier: 'AtRisk'
          }
        ],
        tags: ['FinTech', 'RenewalRisk', 'CSMReview']
      },
      {
        accountId: '20000000-0000-0000-0000-000000000003',
        tenantId: '11111111-1111-1111-1111-111111111111',
        accountName: 'Dubai Logistics One',
        domain: 'dubailogistics1.ae',
        industry: 'Supply Chain & Logistics',
        assignedCsm: 'Rami Al-Ghamdi',
        annualRecurringRevenue: 85000,
        totalSeats: 60,
        activeSeats: 22,
        inactiveSeatRatio: 0.633,
        npsScore: -15,
        previousNpsScore: 20,
        openTicketsCount: 7,
        urgentTicketsCount: 3,
        daysUntilRenewal: 28,
        daysOverdueInvoice: 42,
        adoptionScore: 40,
        sentimentScore: 30,
        utilizationScore: 36,
        commercialScore: 35,
        compositeHealthScore: 35.7,
        healthTier: 'Critical',
        predictedChurnProbability: 0.85,
        lastEvaluatedAtUtc: new Date(now - 45 * 60000).toISOString(),
        riskTriggers: [
          {
            triggerId: 'trig-seed-3a',
            triggerType: 'NpsDrop',
            severity: 'Critical',
            description: 'Negative NPS score (-15). Client operates as active detractor.',
            currentMetricValue: -15,
            threshold: 0,
            detectedAtUtc: new Date(now - 45 * 60000).toISOString()
          },
          {
            triggerId: 'trig-seed-3b',
            triggerType: 'TicketSpike',
            severity: 'Critical',
            description: 'Support ticket escalation spike: 7 open tickets with 3 urgent escalations.',
            currentMetricValue: 7,
            threshold: 5,
            detectedAtUtc: new Date(now - 45 * 60000).toISOString()
          },
          {
            triggerId: 'trig-seed-3c',
            triggerType: 'InactiveSeatRatio',
            severity: 'Critical',
            description: 'Critical seat dormancy: 63% of licensed seats are inactive (38/60).',
            currentMetricValue: 63.3,
            threshold: 50.0,
            detectedAtUtc: new Date(now - 45 * 60000).toISOString()
          },
          {
            triggerId: 'trig-seed-3d',
            triggerType: 'PaymentOverdue',
            severity: 'High',
            description: 'Unsettled accounts receivable: payment overdue by 42 days.',
            currentMetricValue: 42,
            threshold: 30,
            detectedAtUtc: new Date(now - 45 * 60000).toISOString()
          }
        ],
        activePlaybooks: [
          {
            playbookId: 'pb-seed-3a',
            accountId: '20000000-0000-0000-0000-000000000003',
            accountName: 'Dubai Logistics One',
            title: 'Executive sponsor check-in',
            playbookType: 'ExecutiveSponsorCheckin',
            priority: 'Urgent',
            status: 'Active',
            objective: 'Restore executive relationship, realign on business goals, and clear blockers.',
            assignedRole: 'VP of Customer Success / Executive CSM',
            recommendedActions: [
              'Convene 30-minute urgent alignment sync with primary VP/C-suite sponsor.',
              'Review customer ROI scorecard and present targeted executive remediation roadmap.'
            ],
            notes: 'Emergency war room activated with CTO.',
            createdAtUtc: new Date(now - 45 * 60000).toISOString()
          },
          {
            playbookId: 'pb-seed-3b',
            accountId: '20000000-0000-0000-0000-000000000003',
            accountName: 'Dubai Logistics One',
            title: 'Technical SWAT triage',
            playbookType: 'TechnicalSwatTriage',
            priority: 'Urgent',
            status: 'Active',
            objective: 'Resolve open support tickets and stabilize technical integration to prevent platform churn.',
            assignedRole: 'Principal Solutions Architect / SWAT Lead',
            recommendedActions: [
              'Dispatch Tier 3 SWAT engineer to resolve open support tickets within 24 hours.',
              'Audit customer webhook latency and API failure error logs.'
            ],
            notes: 'Tier 3 engineering dispatched for API gateway timeout investigation.',
            createdAtUtc: new Date(now - 45 * 60000).toISOString()
          },
          {
            playbookId: 'pb-seed-3c',
            accountId: '20000000-0000-0000-0000-000000000003',
            accountName: 'Dubai Logistics One',
            title: 'Emergency discount review',
            playbookType: 'EmergencyDiscountReview',
            priority: 'Urgent',
            status: 'Suggested',
            objective: 'Mitigate impending commercial churn through flexible terms or billing restructuring.',
            assignedRole: 'Commercial Deal Desk & Customer Success Director',
            recommendedActions: [
              'Prepare flexible contract restructuring proposal (multi-year incentive or quarterly payment frequency).',
              'Consult Commercial Deal Desk & VP Finance for emergency concession approval.'
            ],
            notes: 'Renewal within 28 days and invoice 42 days overdue.',
            createdAtUtc: new Date(now - 45 * 60000).toISOString()
          }
        ],
        historicalSnapshots: [
          {
            snapshotId: 'seed-snap-3a',
            recordedAtUtc: new Date(now - 60 * 86400000).toISOString(),
            compositeHealthScore: 65.0,
            adoptionScore: 62,
            sentimentScore: 68,
            utilizationScore: 65,
            commercialScore: 68,
            healthTier: 'AtRisk'
          },
          {
            snapshotId: 'seed-snap-3b',
            recordedAtUtc: new Date(now - 30 * 86400000).toISOString(),
            compositeHealthScore: 48.0,
            adoptionScore: 48,
            sentimentScore: 45,
            utilizationScore: 50,
            commercialScore: 52,
            healthTier: 'Critical'
          },
          {
            snapshotId: 'seed-snap-3c',
            recordedAtUtc: new Date(now - 45 * 60000).toISOString(),
            compositeHealthScore: 35.7,
            adoptionScore: 40,
            sentimentScore: 30,
            utilizationScore: 36,
            commercialScore: 35,
            healthTier: 'Critical'
          }
        ],
        tags: ['CriticalChurn', 'EscalationDesk', 'OverdueInvoice']
      },
      {
        accountId: '20000000-0000-0000-0000-000000000004',
        tenantId: '11111111-1111-1111-1111-111111111111',
        accountName: 'Careem Fleet Ops',
        domain: 'careemfleet.com',
        industry: 'Mobility & Micro-Logistics',
        assignedCsm: 'Leila Al-Husseini',
        annualRecurringRevenue: 180000,
        totalSeats: 120,
        activeSeats: 108,
        inactiveSeatRatio: 0.10,
        npsScore: 55,
        previousNpsScore: 50,
        openTicketsCount: 2,
        urgentTicketsCount: 0,
        daysUntilRenewal: 210,
        daysOverdueInvoice: 0,
        adoptionScore: 82,
        sentimentScore: 78,
        utilizationScore: 88,
        commercialScore: 85,
        compositeHealthScore: 82.8,
        healthTier: 'Healthy',
        predictedChurnProbability: 0.09,
        lastEvaluatedAtUtc: new Date(now - 12 * 3600000).toISOString(),
        riskTriggers: [],
        activePlaybooks: [],
        historicalSnapshots: [
          {
            snapshotId: 'seed-snap-4a',
            recordedAtUtc: new Date(now - 30 * 86400000).toISOString(),
            compositeHealthScore: 80.0,
            adoptionScore: 80,
            sentimentScore: 75,
            utilizationScore: 85,
            commercialScore: 84,
            healthTier: 'Healthy'
          },
          {
            snapshotId: 'seed-snap-4b',
            recordedAtUtc: new Date(now - 12 * 3600000).toISOString(),
            compositeHealthScore: 82.8,
            adoptionScore: 82,
            sentimentScore: 78,
            utilizationScore: 88,
            commercialScore: 85,
            healthTier: 'Healthy'
          }
        ],
        tags: ['GrowthAccount', 'Logistics', 'Loyal']
      },
      {
        accountId: '20000000-0000-0000-0000-000000000005',
        tenantId: '11111111-1111-1111-1111-111111111111',
        accountName: 'Cairo E-Commerce Hub',
        domain: 'cairohub.eg',
        industry: 'Omnichannel Retail',
        assignedCsm: 'Omar Al-Farhan',
        annualRecurringRevenue: 64000,
        totalSeats: 45,
        activeSeats: 26,
        inactiveSeatRatio: 0.422,
        npsScore: 10,
        previousNpsScore: 25,
        openTicketsCount: 4,
        urgentTicketsCount: 1,
        daysUntilRenewal: 95,
        daysOverdueInvoice: 15,
        adoptionScore: 52,
        sentimentScore: 54,
        utilizationScore: 55,
        commercialScore: 58,
        compositeHealthScore: 54.3,
        healthTier: 'AtRisk',
        predictedChurnProbability: 0.45,
        lastEvaluatedAtUtc: new Date(now - 1 * 3600000).toISOString(),
        riskTriggers: [
          {
            triggerId: 'trig-seed-5a',
            triggerType: 'InactiveSeatRatio',
            severity: 'High',
            description: 'Elevated inactive seat ratio: 42% of seats idle (19/45 unused).',
            currentMetricValue: 42.2,
            threshold: 35.0,
            detectedAtUtc: new Date(now - 1 * 3600000).toISOString()
          },
          {
            triggerId: 'trig-seed-5b',
            triggerType: 'TicketSpike',
            severity: 'Medium',
            description: 'High-priority support escalation active without technical SWAT assignment.',
            currentMetricValue: 1,
            threshold: 1,
            detectedAtUtc: new Date(now - 1 * 3600000).toISOString()
          }
        ],
        activePlaybooks: [
          {
            playbookId: 'pb-seed-5a',
            accountId: '20000000-0000-0000-0000-000000000005',
            accountName: 'Cairo E-Commerce Hub',
            title: 'Adoption workshop',
            playbookType: 'AdoptionWorkshop',
            priority: 'Medium',
            status: 'Suggested',
            objective: 'Drive user onboarding and feature activation to eliminate seat dormancy.',
            assignedRole: 'Lead Customer Success Manager',
            recommendedActions: [
              'Conduct tailored 45-minute interactive workflow training session with department leads.',
              'Deploy in-app role-based onboarding checklists and guided tooltips for inactive users.'
            ],
            notes: 'Suggested due to 42% dormant seats.',
            createdAtUtc: new Date(now - 1 * 3600000).toISOString()
          }
        ],
        historicalSnapshots: [
          {
            snapshotId: 'seed-snap-5a',
            recordedAtUtc: new Date(now - 30 * 86400000).toISOString(),
            compositeHealthScore: 60.0,
            adoptionScore: 58,
            sentimentScore: 60,
            utilizationScore: 62,
            commercialScore: 64,
            healthTier: 'AtRisk'
          },
          {
            snapshotId: 'seed-snap-5b',
            recordedAtUtc: new Date(now - 1 * 3600000).toISOString(),
            compositeHealthScore: 54.3,
            adoptionScore: 52,
            sentimentScore: 54,
            utilizationScore: 55,
            commercialScore: 58,
            healthTier: 'AtRisk'
          }
        ],
        tags: ['Retail', 'AdoptionRisk', 'CSMReview']
      }
    ];
  }
}
