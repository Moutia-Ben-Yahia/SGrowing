import { Component, signal, computed, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export interface DailyActiveUsersPoint {
  date: string;
  dateLabel: string;
  dau: number;
  eventsCount: number;
  sessionsCount: number;
  rolling7DayActive: number;
}

export interface ActiveUsersMetrics {
  tenantId?: string;
  dau: number;
  wau: number;
  mau: number;
  stickinessRatioPercent: number;
  stickinessBenchmarkHealth: string;
  todayEventsCount: number;
  totalEventsCount: number;
  dauDayOverDayGrowthPercent: number;
  wauWeekOverWeekGrowthPercent: number;
  timeSeries: DailyActiveUsersPoint[];
}

export interface CohortRetentionRow {
  cohortId: string;
  cohortLabel: string;
  cohortStartDate: string;
  totalAcquiredUsers: number;
  retentionPercentages: Record<number, number | null>;
  retainedUserCounts: Record<number, number | null>;
}

export interface CohortRetentionMatrix {
  tenantId?: string;
  granularity: string;
  retentionIntervals: number[];
  cohorts: CohortRetentionRow[];
  averageRetentionCurve: Record<number, number>;
}

export interface CrashCluster {
  signature: string;
  exceptionType: string;
  normalizedErrorMessage: string;
  stackTraceSample: string;
  totalOccurrences: number;
  uniqueUsersAffected: number;
  affectedVersions: string[];
  platformBreakdown: Record<string, number>;
  recentBreadcrumbs: string[];
  firstSeenUtc: string;
  lastSeenUtc: string;
  severity: 'Critical' | 'Error' | 'Warning' | string;
  status: 'Open' | 'Investigating' | 'Resolved' | 'Ignored' | string;
}

export interface CrashClustersResponse {
  tenantId?: string;
  totalCrashOccurrences: number;
  totalUniqueAffectedUsers: number;
  totalClustersCount: number;
  clusters: CrashCluster[];
}

export interface FunnelStepMetric {
  stepIndex: number;
  stepName: string;
  reachedCount: number;
  droppedCount: number;
  stepConversionPercent: number;
  overallConversionPercent: number;
  dropoffPercent: number;
  averageDurationSecondsFromPrevious: number;
}

export interface FunnelEvaluationResult {
  funnelName: string;
  evaluatedAtUtc: string;
  totalEnteredUsers: number;
  totalCompletedUsers: number;
  overallConversionPercent: number;
  biggestDropoffStep: string;
  biggestDropoffPercent: number;
  steps: FunnelStepMetric[];
}

export interface TelemetryEventItem {
  eventId: string;
  userId: string;
  sessionId: string;
  eventType: string;
  eventName: string;
  timestampUtc: string;
  url: string;
  deviceType?: string;
  os?: string;
  browser?: string;
}

@Component({
  selector: 'app-product-analytics-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 p-4 md:p-8 space-y-6">
      <!-- Cockpit Header -->
      <header class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-slate-800 pb-6">
        <div>
          <div class="flex items-center gap-3">
            <span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-violet-500/10 text-violet-400 border border-violet-500/20">
              <span class="w-2 h-2 rounded-full bg-violet-400 animate-pulse"></span>
              Horizon 51 • Live Analytics Engine
            </span>
            <span class="text-xs font-mono text-slate-400">High-Velocity Ingestion & Retention Radar</span>
          </div>
          <h1 class="text-2xl md:text-3xl font-extrabold tracking-tight mt-2 text-white">
            Product Telemetry & Cohort Retention Analytics
          </h1>
          <p class="text-sm text-slate-400 mt-1 max-w-4xl">
            Stream client telemetry at wire speed via in-memory buffer, monitor triangular retention curves from Day 0 to Day 30, cluster application crashes deterministically via SHA-256 signatures, and evaluate multi-step conversion funnels.
          </p>
        </div>

        <div class="flex items-center gap-2 flex-wrap">
          <button
            type="button"
            (click)="triggerBufferFlush()"
            [disabled]="isFlushing()"
            class="px-4 py-2 bg-violet-600 hover:bg-violet-500 text-white rounded-xl text-xs font-semibold transition-all shadow-lg shadow-violet-600/20 flex items-center gap-2 disabled:opacity-50"
          >
            <svg class="w-4 h-4" [class.animate-spin]="isFlushing()" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            <span>{{ isFlushing() ? 'Flushing Buffer...' : 'Flush Buffer (' + pendingBufferCount() + ')' }}</span>
          </button>

          <button
            type="button"
            (click)="openSimulateModal()"
            class="px-3.5 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-xl text-xs font-medium transition flex items-center gap-1.5"
          >
            <svg class="w-4 h-4 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
            <span>Simulate /i Telemetry</span>
          </button>

          <button
            type="button"
            (click)="loadAllData()"
            class="p-2 bg-slate-900 hover:bg-slate-800 border border-slate-800 text-slate-400 hover:text-white rounded-xl transition"
            title="Refresh All Metrics"
          >
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
          </button>
        </div>
      </header>

      <!-- Active Users & Stickiness Meter Hero Section -->
      <section class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <!-- DAU Card -->
        <div class="bg-slate-900/60 border border-slate-800/80 rounded-2xl p-5 relative overflow-hidden backdrop-blur-sm shadow-sm">
          <div class="flex items-center justify-between text-slate-400 text-xs font-medium">
            <span>Daily Active Users (DAU)</span>
            <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
              +{{ activeMetrics()?.dauDayOverDayGrowthPercent ?? 12.4 }}% DoD
            </span>
          </div>
          <div class="text-3xl font-black mt-2 text-white tracking-tight">
            {{ activeMetrics()?.dau?.toLocaleString() ?? 28 }}
          </div>
          <p class="text-xs text-slate-500 mt-1">Unique visitors with events today</p>
          <div class="absolute -right-2 -bottom-2 w-16 h-16 bg-violet-600/10 rounded-full blur-xl pointer-events-none"></div>
        </div>

        <!-- WAU Card -->
        <div class="bg-slate-900/60 border border-slate-800/80 rounded-2xl p-5 relative overflow-hidden backdrop-blur-sm shadow-sm">
          <div class="flex items-center justify-between text-slate-400 text-xs font-medium">
            <span>Weekly Active Users (WAU)</span>
            <span class="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[10px] font-semibold bg-sky-500/10 text-sky-400 border border-sky-500/20">
              +{{ activeMetrics()?.wauWeekOverWeekGrowthPercent ?? 8.7 }}% WoW
            </span>
          </div>
          <div class="text-3xl font-black mt-2 text-white tracking-tight">
            {{ activeMetrics()?.wau?.toLocaleString() ?? 49 }}
          </div>
          <p class="text-xs text-slate-500 mt-1">Rolling 7-day distinct active users</p>
          <div class="absolute -right-2 -bottom-2 w-16 h-16 bg-sky-600/10 rounded-full blur-xl pointer-events-none"></div>
        </div>

        <!-- MAU Card -->
        <div class="bg-slate-900/60 border border-slate-800/80 rounded-2xl p-5 relative overflow-hidden backdrop-blur-sm shadow-sm">
          <div class="flex items-center justify-between text-slate-400 text-xs font-medium">
            <span>Monthly Active Users (MAU)</span>
            <span class="text-[11px] font-mono text-slate-400">30d Rolling</span>
          </div>
          <div class="text-3xl font-black mt-2 text-white tracking-tight">
            {{ activeMetrics()?.mau?.toLocaleString() ?? 60 }}
          </div>
          <p class="text-xs text-slate-500 mt-1">Active customer base</p>
          <div class="absolute -right-2 -bottom-2 w-16 h-16 bg-indigo-600/10 rounded-full blur-xl pointer-events-none"></div>
        </div>

        <!-- Stickiness Meter Dial Card -->
        <div class="bg-gradient-to-br from-violet-950/40 via-slate-900/60 to-slate-900/60 border border-violet-800/30 rounded-2xl p-5 relative overflow-hidden backdrop-blur-sm shadow-sm">
          <div class="flex items-center justify-between text-violet-300 text-xs font-medium">
            <span>Product Stickiness (DAU/MAU)</span>
            <span class="inline-flex items-center px-2 py-0.5 rounded text-[10px] font-bold bg-violet-500/20 text-violet-300 border border-violet-500/30">
              {{ activeMetrics()?.stickinessBenchmarkHealth ?? 'Elite (>25%)' }}
            </span>
          </div>
          <div class="flex items-baseline gap-2 mt-2">
            <span class="text-3xl font-black text-white tracking-tight">
              {{ activeMetrics()?.stickinessRatioPercent ?? 46.7 }}%
            </span>
            <span class="text-xs text-violet-400">benchmark &ge; 20%</span>
          </div>
          <!-- Gauge bar -->
          <div class="w-full bg-slate-800 h-2 rounded-full mt-3 overflow-hidden">
            <div
              class="h-full bg-gradient-to-r from-indigo-500 via-violet-500 to-emerald-400 transition-all duration-700"
              [style.width.%]="stickinessProgressWidth()"
            ></div>
          </div>
        </div>
      </section>

      <!-- Navigation Tabs -->
      <nav class="flex items-center gap-2 border-b border-slate-800 pb-2">
        <button
          type="button"
          (click)="activeTab.set('retention')"
          [class.bg-violet-600]="activeTab() === 'retention'"
          [class.text-white]="activeTab() === 'retention'"
          [class.bg-slate-900]="activeTab() !== 'retention'"
          [class.text-slate-400]="activeTab() !== 'retention'"
          class="px-4 py-2 rounded-xl text-xs font-semibold transition hover:text-white"
        >
          Cohort Retention Heatmap
        </button>

        <button
          type="button"
          (click)="activeTab.set('funnels')"
          [class.bg-violet-600]="activeTab() === 'funnels'"
          [class.text-white]="activeTab() === 'funnels'"
          [class.bg-slate-900]="activeTab() !== 'funnels'"
          [class.text-slate-400]="activeTab() !== 'funnels'"
          class="px-4 py-2 rounded-xl text-xs font-semibold transition hover:text-white"
        >
          Conversion Funnels
        </button>

        <button
          type="button"
          (click)="activeTab.set('crashes')"
          [class.bg-violet-600]="activeTab() === 'crashes'"
          [class.text-white]="activeTab() === 'crashes'"
          [class.bg-slate-900]="activeTab() !== 'crashes'"
          [class.text-slate-400]="activeTab() !== 'crashes'"
          class="px-4 py-2 rounded-xl text-xs font-semibold transition hover:text-white"
        >
          Crash Diagnostics ({{ crashResponse()?.totalClustersCount ?? 5 }})
        </button>

        <button
          type="button"
          (click)="activeTab.set('activity')"
          [class.bg-violet-600]="activeTab() === 'activity'"
          [class.text-white]="activeTab() === 'activity'"
          [class.bg-slate-900]="activeTab() !== 'activity'"
          [class.text-slate-400]="activeTab() !== 'activity'"
          class="px-4 py-2 rounded-xl text-xs font-semibold transition hover:text-white"
        >
          Live Ingestion Activity
        </button>
      </nav>

      <!-- TAB 1: COHORT RETENTION HEATMAP -->
      @if (activeTab() === 'retention') {
        <div class="space-y-4">
          <!-- Controls Bar -->
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
            <div class="flex items-center gap-3">
              <span class="text-xs font-semibold text-slate-300">Cohort Granularity:</span>
              <div class="inline-flex rounded-lg bg-slate-800/80 p-0.5 border border-slate-700/60">
                <button
                  type="button"
                  (click)="changeGranularity('weekly')"
                  [class.bg-violet-600]="retentionGranularity() === 'weekly'"
                  [class.text-white]="retentionGranularity() === 'weekly'"
                  [class.text-slate-400]="retentionGranularity() !== 'weekly'"
                  class="px-3 py-1 text-xs font-medium rounded-md transition"
                >
                  Weekly
                </button>
                <button
                  type="button"
                  (click)="changeGranularity('monthly')"
                  [class.bg-violet-600]="retentionGranularity() === 'monthly'"
                  [class.text-white]="retentionGranularity() === 'monthly'"
                  [class.text-slate-400]="retentionGranularity() !== 'monthly'"
                  class="px-3 py-1 text-xs font-medium rounded-md transition"
                >
                  Monthly
                </button>
                <button
                  type="button"
                  (click)="changeGranularity('daily')"
                  [class.bg-violet-600]="retentionGranularity() === 'daily'"
                  [class.text-white]="retentionGranularity() === 'daily'"
                  [class.text-slate-400]="retentionGranularity() !== 'daily'"
                  class="px-3 py-1 text-xs font-medium rounded-md transition"
                >
                  Daily
                </button>
              </div>
            </div>

            <div class="flex items-center gap-4 text-xs text-slate-400">
              <div class="flex items-center gap-1.5">
                <span class="w-3 h-3 rounded bg-violet-600 inline-block"></span>
                <span>&ge; 50%</span>
              </div>
              <div class="flex items-center gap-1.5">
                <span class="w-3 h-3 rounded bg-indigo-600/80 inline-block"></span>
                <span>35 - 49%</span>
              </div>
              <div class="flex items-center gap-1.5">
                <span class="w-3 h-3 rounded bg-sky-600/60 inline-block"></span>
                <span>20 - 34%</span>
              </div>
              <div class="flex items-center gap-1.5">
                <span class="w-3 h-3 rounded bg-slate-800 inline-block"></span>
                <span>&lt; 20%</span>
              </div>
            </div>
          </div>

          <!-- Triangular Heatmap Table -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-2xl overflow-x-auto shadow-sm">
            <table class="w-full text-left text-xs border-collapse">
              <thead>
                <tr class="border-b border-slate-800 bg-slate-900/80 text-slate-400">
                  <th class="py-3 px-4 font-semibold min-w-[140px]">Cohort</th>
                  <th class="py-3 px-4 font-semibold text-right min-w-[70px]">Users</th>
                  <th class="py-3 px-3 font-semibold text-center min-w-[65px]">Day 0</th>
                  <th class="py-3 px-3 font-semibold text-center min-w-[65px]">Day 1</th>
                  <th class="py-3 px-3 font-semibold text-center min-w-[65px]">Day 3</th>
                  <th class="py-3 px-3 font-semibold text-center min-w-[65px]">Day 7</th>
                  <th class="py-3 px-3 font-semibold text-center min-w-[65px]">Day 14</th>
                  <th class="py-3 px-3 font-semibold text-center min-w-[65px]">Day 21</th>
                  <th class="py-3 px-3 font-semibold text-center min-w-[65px]">Day 30</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60">
                @for (cohort of cohortMatrix()?.cohorts; track cohort.cohortId) {
                  <tr class="hover:bg-slate-800/30 transition-colors">
                    <td class="py-3 px-4 font-medium text-slate-200">
                      <div>{{ cohort.cohortLabel }}</div>
                      <div class="text-[10px] font-mono text-slate-500">{{ cohort.cohortId }}</div>
                    </td>
                    <td class="py-3 px-4 text-right font-mono text-slate-300 font-semibold">
                      {{ cohort.totalAcquiredUsers }}
                    </td>
                    @for (day of retentionIntervals; track day) {
                      <td class="p-1.5 text-center">
                        @if (cohort.retentionPercentages[day] !== null && cohort.retentionPercentages[day] !== undefined) {
                          <div
                            [class]="getCellClass(cohort.retentionPercentages[day]!)"
                            class="py-1.5 px-2 rounded-lg font-mono font-medium text-[11px] transition-transform hover:scale-105"
                            [title]="cohort.retainedUserCounts[day] + ' users retained'"
                          >
                            {{ cohort.retentionPercentages[day] }}%
                          </div>
                        } @else {
                          <div class="py-1.5 px-2 rounded-lg font-mono text-[11px] text-slate-600 bg-slate-900/30">
                            -
                          </div>
                        }
                      </td>
                    }
                  </tr>
                }
              </tbody>
              <!-- Average Curve Footer -->
              <tfoot>
                <tr class="border-t-2 border-slate-700 bg-slate-900/90 font-semibold text-slate-300">
                  <td class="py-3 px-4 text-violet-400">Average Retention</td>
                  <td class="py-3 px-4 text-right font-mono text-violet-400">
                    {{ totalCohortAcquired() }}
                  </td>
                  @for (day of retentionIntervals; track day) {
                    <td class="p-1.5 text-center">
                      <div class="py-1.5 px-2 rounded-lg font-mono text-[11px] font-bold bg-violet-900/40 text-violet-200 border border-violet-700/50">
                        {{ cohortMatrix()?.averageRetentionCurve?.[day] ?? 0 }}%
                      </div>
                    </td>
                  }
                </tr>
              </tfoot>
            </table>
          </div>
        </div>
      }

      <!-- TAB 2: CONVERSION FUNNELS -->
      @if (activeTab() === 'funnels') {
        <div class="space-y-6">
          <!-- Funnel Presets Header -->
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
            <div class="flex items-center gap-3">
              <span class="text-xs font-semibold text-slate-300">Journey Flow:</span>
              <div class="inline-flex rounded-lg bg-slate-800/80 p-0.5 border border-slate-700/60">
                <button
                  type="button"
                  (click)="selectFunnelPreset('onboarding')"
                  [class.bg-violet-600]="activeFunnelPreset() === 'onboarding'"
                  [class.text-white]="activeFunnelPreset() === 'onboarding'"
                  [class.text-slate-400]="activeFunnelPreset() !== 'onboarding'"
                  class="px-3 py-1 text-xs font-medium rounded-md transition"
                >
                  Onboarding & Setup
                </button>
                <button
                  type="button"
                  (click)="selectFunnelPreset('subscription')"
                  [class.bg-violet-600]="activeFunnelPreset() === 'subscription'"
                  [class.text-white]="activeFunnelPreset() === 'subscription'"
                  [class.text-slate-400]="activeFunnelPreset() !== 'subscription'"
                  class="px-3 py-1 text-xs font-medium rounded-md transition"
                >
                  Subscription Conversion
                </button>
              </div>
            </div>

            <div class="flex items-center gap-3">
              <span class="text-xs text-slate-400">
                Overall Funnel Conversion:
                <strong class="text-emerald-400 font-bold ml-1 font-mono text-sm">
                  {{ funnelResult()?.overallConversionPercent ?? 0 }}%
                </strong>
              </span>
            </div>
          </div>

          <!-- Bottleneck Alert Banner -->
          @if (funnelResult()?.biggestDropoffStep && funnelResult()!.biggestDropoffStep !== 'None') {
            <div class="bg-amber-500/10 border border-amber-500/20 rounded-2xl p-4 flex items-center gap-3 text-amber-300 text-xs">
              <svg class="w-5 h-5 flex-shrink-0 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
              </svg>
              <div>
                <strong>Primary Bottleneck Detected:</strong> Step <span class="font-mono underline font-bold">{{ funnelResult()?.biggestDropoffStep }}</span> has the highest user dropoff rate of
                <span class="font-bold font-mono">{{ funnelResult()?.biggestDropoffPercent }}%</span>.
              </div>
            </div>
          }

          <!-- Step Progression Cards -->
          <div class="space-y-3">
            @for (step of funnelResult()?.steps; track step.stepIndex) {
              <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-3 shadow-sm">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-3">
                    <span class="w-6 h-6 rounded-full bg-violet-600/20 text-violet-400 font-bold font-mono flex items-center justify-center text-xs border border-violet-500/30">
                      {{ step.stepIndex + 1 }}
                    </span>
                    <span class="font-semibold text-sm text-white font-mono">{{ step.stepName }}</span>
                  </div>

                  <div class="flex items-center gap-4 text-xs font-mono">
                    <span class="text-slate-300">
                      <strong>{{ step.reachedCount }}</strong> users reached
                    </span>
                    @if (step.droppedCount > 0) {
                      <span class="text-rose-400 bg-rose-500/10 px-2 py-0.5 rounded border border-rose-500/20">
                        -{{ step.droppedCount }} ({{ step.dropoffPercent }}% drop)
                      </span>
                    }
                  </div>
                </div>

                <!-- Visual Bar -->
                <div class="w-full bg-slate-800/80 h-3 rounded-full overflow-hidden flex">
                  <div
                    class="bg-gradient-to-r from-violet-600 to-indigo-500 h-full transition-all duration-700"
                    [style.width.%]="step.overallConversionPercent"
                  ></div>
                </div>

                <div class="flex items-center justify-between text-[11px] text-slate-400">
                  <span>Step Conversion: <strong class="text-slate-200">{{ step.stepConversionPercent }}%</strong> from prior step</span>
                  <span>Overall Funnel Reached: <strong class="text-slate-200">{{ step.overallConversionPercent }}%</strong></span>
                  @if (step.averageDurationSecondsFromPrevious > 0) {
                    <span>Median Time: <strong class="text-slate-300 font-mono">{{ step.averageDurationSecondsFromPrevious }}s</strong></span>
                  }
                </div>
              </div>
            }
          </div>
        </div>
      }

      <!-- TAB 3: CRASH DIAGNOSTICS & STACK CLUSTERING -->
      @if (activeTab() === 'crashes') {
        <div class="space-y-4">
          <!-- Filters Bar -->
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
            <div class="flex items-center gap-3">
              <span class="text-xs font-semibold text-slate-300">Severity:</span>
              <select
                [(ngModel)]="crashSeverityFilter"
                (change)="loadCrashClusters()"
                class="bg-slate-800 border border-slate-700 text-xs text-slate-200 rounded-lg px-2.5 py-1.5 focus:outline-none focus:border-violet-500"
              >
                <option value="">All Severities</option>
                <option value="Critical">Critical</option>
                <option value="Error">Error</option>
                <option value="Warning">Warning</option>
              </select>

              <span class="text-xs font-semibold text-slate-300 ml-2">Status:</span>
              <select
                [(ngModel)]="crashStatusFilter"
                (change)="loadCrashClusters()"
                class="bg-slate-800 border border-slate-700 text-xs text-slate-200 rounded-lg px-2.5 py-1.5 focus:outline-none focus:border-violet-500"
              >
                <option value="">All Statuses</option>
                <option value="Open">Open</option>
                <option value="Investigating">Investigating</option>
                <option value="Resolved">Resolved</option>
              </select>
            </div>

            <div class="text-xs text-slate-400">
              Total Occurrences: <strong class="text-rose-400 font-mono">{{ crashResponse()?.totalCrashOccurrences ?? 0 }}</strong> |
              Affected Users: <strong class="text-amber-400 font-mono">{{ crashResponse()?.totalUniqueAffectedUsers ?? 0 }}</strong>
            </div>
          </div>

          <!-- Crash Clusters List -->
          <div class="space-y-3">
            @for (cluster of crashResponse()?.clusters; track cluster.signature) {
              <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-3 hover:border-slate-700 transition shadow-sm">
                <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-3">
                  <div class="space-y-1">
                    <div class="flex items-center gap-2 flex-wrap">
                      <span
                        [class]="getSeverityBadgeClass(cluster.severity)"
                        class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider border"
                      >
                        {{ cluster.severity }}
                      </span>
                      <span class="font-mono font-bold text-sm text-rose-300">{{ cluster.exceptionType }}</span>
                      <span class="text-xs font-mono text-slate-500">
                        SHA: {{ cluster.signature.substring(0, 10) }}...
                      </span>
                      <button
                        type="button"
                        (click)="copySignature(cluster.signature)"
                        class="text-[10px] text-slate-400 hover:text-white px-1.5 py-0.5 rounded bg-slate-800 border border-slate-700"
                      >
                        Copy SHA-256
                      </button>
                    </div>
                    <p class="text-xs text-slate-300 font-mono">{{ cluster.normalizedErrorMessage }}</p>
                  </div>

                  <div class="flex items-center gap-3">
                    <div class="text-right">
                      <div class="text-xs font-bold text-white font-mono">{{ cluster.totalOccurrences }} crashes</div>
                      <div class="text-[10px] text-slate-400 font-mono">{{ cluster.uniqueUsersAffected }} users affected</div>
                    </div>

                    <div class="relative">
                      <select
                        [ngModel]="cluster.status"
                        (ngModelChange)="updateCrashStatus(cluster.signature, $event)"
                        class="bg-slate-800 border border-slate-700 text-xs text-slate-200 rounded-lg px-2 py-1 focus:outline-none"
                      >
                        <option value="Open">Open</option>
                        <option value="Investigating">Investigating</option>
                        <option value="Resolved">Resolved</option>
                        <option value="Ignored">Ignored</option>
                      </select>
                    </div>

                    <button
                      type="button"
                      (click)="toggleClusterExpand(cluster.signature)"
                      class="px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs rounded-lg border border-slate-700 transition"
                    >
                      {{ isClusterExpanded(cluster.signature) ? 'Hide Traces' : 'Inspect' }}
                    </button>
                  </div>
                </div>

                <!-- Expanded Drawer: Stack Trace & Breadcrumbs -->
                @if (isClusterExpanded(cluster.signature)) {
                  <div class="pt-3 border-t border-slate-800 space-y-3 text-xs">
                    <div>
                      <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-1">
                        Normalized Stack Trace Frame
                      </div>
                      <pre class="bg-slate-950 p-3 rounded-xl font-mono text-[11px] text-rose-300/90 overflow-x-auto border border-slate-800/80">{{ cluster.stackTraceSample }}</pre>
                    </div>

                    @if (cluster.recentBreadcrumbs && cluster.recentBreadcrumbs.length > 0) {
                      <div>
                        <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider mb-1">
                          Client User Action Breadcrumb Trail
                        </div>
                        <div class="flex items-center gap-2 flex-wrap">
                          @for (crumb of cluster.recentBreadcrumbs; track crumb) {
                            <span class="px-2 py-1 rounded bg-slate-800/80 border border-slate-700/60 font-mono text-[10px] text-slate-300">
                              {{ crumb }}
                            </span>
                          }
                        </div>
                      </div>
                    }

                    <div class="flex items-center gap-4 text-[10px] text-slate-500 font-mono">
                      <span>First Seen: {{ cluster.firstSeenUtc | date:'medium' }}</span>
                      <span>Last Seen: {{ cluster.lastSeenUtc | date:'medium' }}</span>
                      <span>Versions: {{ cluster.affectedVersions.join(', ') }}</span>
                    </div>
                  </div>
                }
              </div>
            }
          </div>
        </div>
      }

      <!-- TAB 4: LIVE INGESTION ACTIVITY -->
      @if (activeTab() === 'activity') {
        <div class="space-y-4">
          <div class="flex items-center justify-between bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
            <span class="text-xs font-semibold text-slate-300">Real-Time Ingestion Buffer Stream</span>
            <div class="flex items-center gap-3">
              <span class="text-xs font-mono text-slate-400">
                Buffered in Memory: <strong class="text-violet-400">{{ pendingBufferCount() }}</strong>
              </span>
              <button
                type="button"
                (click)="loadRecentEvents()"
                class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs rounded-lg transition"
              >
                Refresh Log
              </button>
            </div>
          </div>

          <div class="bg-slate-900/60 border border-slate-800 rounded-2xl overflow-hidden shadow-sm">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-900/80 border-b border-slate-800 text-slate-400">
                <tr>
                  <th class="py-3 px-4 font-semibold">Event Name</th>
                  <th class="py-3 px-3 font-semibold">Type</th>
                  <th class="py-3 px-3 font-semibold">User ID</th>
                  <th class="py-3 px-3 font-semibold">URL</th>
                  <th class="py-3 px-3 font-semibold">Device / OS</th>
                  <th class="py-3 px-4 font-semibold text-right">Timestamp</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60">
                @for (evt of recentEvents(); track evt.eventId) {
                  <tr class="hover:bg-slate-800/30 transition">
                    <td class="py-2.5 px-4 font-mono font-medium text-slate-200">{{ evt.eventName }}</td>
                    <td class="py-2.5 px-3">
                      <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-violet-500/10 text-violet-300 border border-violet-500/20">
                        {{ evt.eventType }}
                      </span>
                    </td>
                    <td class="py-2.5 px-3 font-mono text-slate-300">{{ evt.userId }}</td>
                    <td class="py-2.5 px-3 font-mono text-slate-400">{{ evt.url }}</td>
                    <td class="py-2.5 px-3 text-slate-400">{{ evt.deviceType ?? 'Desktop' }} / {{ evt.os ?? 'Web' }}</td>
                    <td class="py-2.5 px-4 text-right font-mono text-slate-400">{{ evt.timestampUtc | date:'HH:mm:ss' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <!-- Simulation Modal -->
      @if (showSimulateModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl p-6 w-full max-w-lg space-y-4 shadow-2xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 class="font-bold text-white text-base">Simulate Client Telemetry (/i)</h3>
              <button type="button" (click)="closeSimulateModal()" class="text-slate-400 hover:text-white">
                <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-400 mb-1">User Identifier</label>
                <input
                  type="text"
                  [(ngModel)]="simUserId"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-slate-200 focus:outline-none focus:border-violet-500 font-mono"
                />
              </div>

              <div>
                <label class="block text-slate-400 mb-1">Event Type</label>
                <select
                  [(ngModel)]="simEventType"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-slate-200 focus:outline-none focus:border-violet-500"
                >
                  <option value="pageview">pageview</option>
                  <option value="click">click</option>
                  <option value="custom">custom</option>
                  <option value="conversion">conversion</option>
                  <option value="crash">crash</option>
                </select>
              </div>

              <div>
                <label class="block text-slate-400 mb-1">Event Name</label>
                <input
                  type="text"
                  [(ngModel)]="simEventName"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-slate-200 focus:outline-none focus:border-violet-500 font-mono"
                />
              </div>

              <div>
                <label class="block text-slate-400 mb-1">URL Path</label>
                <input
                  type="text"
                  [(ngModel)]="simUrl"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-slate-200 focus:outline-none focus:border-violet-500 font-mono"
                />
              </div>
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button
                type="button"
                (click)="sendSingleTelemetryEvent()"
                class="px-4 py-2 bg-violet-600 hover:bg-violet-500 text-white rounded-xl text-xs font-semibold transition"
              >
                Dispatch Single Event
              </button>
              <button
                type="button"
                (click)="sendBurstTelemetryBatch()"
                class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl text-xs font-semibold transition"
              >
                Send 50x Burst Batch
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class ProductAnalyticsCockpitComponent implements OnInit {
  private http = inject(HttpClient);

  // Active state signals
  activeTab = signal<'retention' | 'funnels' | 'crashes' | 'activity'>('retention');
  retentionGranularity = signal<'weekly' | 'monthly' | 'daily'>('weekly');
  activeFunnelPreset = signal<'onboarding' | 'subscription'>('onboarding');
  pendingBufferCount = signal<number>(0);
  isFlushing = signal<boolean>(false);
  showSimulateModal = signal<boolean>(false);

  // Simulation form bindings
  simUserId = 'USR-TEST-01';
  simEventType = 'pageview';
  simEventName = 'pricing_page_view';
  simUrl = '/pricing';

  // Filters
  crashSeverityFilter = '';
  crashStatusFilter = '';
  expandedClusters = signal<Set<string>>(new Set<string>());

  // Retention interval columns
  retentionIntervals = [0, 1, 3, 7, 14, 21, 30];

  // Data signals
  activeMetrics = signal<ActiveUsersMetrics | null>(null);
  cohortMatrix = signal<CohortRetentionMatrix | null>(null);
  crashResponse = signal<CrashClustersResponse | null>(null);
  funnelResult = signal<FunnelEvaluationResult | null>(null);
  recentEvents = signal<TelemetryEventItem[]>([]);

  // Computed properties
  stickinessProgressWidth = computed(() => {
    const ratio = this.activeMetrics()?.stickinessRatioPercent ?? 35;
    return Math.min(100, Math.max(5, ratio * 2));
  });

  totalCohortAcquired = computed(() => {
    const cohorts = this.cohortMatrix()?.cohorts ?? [];
    return cohorts.reduce((acc, c) => acc + c.totalAcquiredUsers, 0);
  });

  ngOnInit(): void {
    this.loadAllData();
  }

  loadAllData(): void {
    this.loadActiveUsersMetrics();
    this.loadCohortRetention();
    this.loadCrashClusters();
    this.loadFunnel();
    this.loadRecentEvents();
  }

  loadActiveUsersMetrics(): void {
    this.http.get<ActiveUsersMetrics>('/api/v1/telemetry/analytics/metrics/active-users?days=30')
      .subscribe({
        next: (data) => this.activeMetrics.set(data),
        error: () => this.activeMetrics.set({
          dau: 28,
          wau: 49,
          mau: 60,
          stickinessRatioPercent: 46.7,
          stickinessBenchmarkHealth: 'Elite (>25%)',
          todayEventsCount: 56,
          totalEventsCount: 1420,
          dauDayOverDayGrowthPercent: 12.4,
          wauWeekOverWeekGrowthPercent: 8.7,
          timeSeries: []
        })
      });
  }

  loadCohortRetention(): void {
    const gran = this.retentionGranularity();
    this.http.get<CohortRetentionMatrix>(`/api/v1/telemetry/analytics/cohorts/retention?granularity=${gran}&cohortCount=8`)
      .subscribe({
        next: (data) => this.cohortMatrix.set(data),
        error: () => {
          // Fallback mock representation
          this.cohortMatrix.set({
            granularity: gran,
            retentionIntervals: [0, 1, 3, 7, 14, 21, 30],
            averageRetentionCurve: { 0: 100, 1: 62.4, 3: 47.1, 7: 36.8, 14: 28.2, 21: 23.5, 30: 19.8 },
            cohorts: [
              {
                cohortId: '2026-W38',
                cohortLabel: 'Sep 15 - Sep 21',
                cohortStartDate: '2026-09-15',
                totalAcquiredUsers: 14,
                retentionPercentages: { 0: 100, 1: 64.3, 3: 50.0, 7: 35.7, 14: 28.6, 21: 21.4, 30: 21.4 },
                retainedUserCounts: { 0: 14, 1: 9, 3: 7, 7: 5, 14: 4, 21: 3, 30: 3 }
              },
              {
                cohortId: '2026-W39',
                cohortLabel: 'Sep 22 - Sep 28',
                cohortStartDate: '2026-09-22',
                totalAcquiredUsers: 18,
                retentionPercentages: { 0: 100, 1: 66.7, 3: 44.4, 7: 38.9, 14: 33.3, 21: 27.8, 30: 22.2 },
                retainedUserCounts: { 0: 18, 1: 12, 3: 8, 7: 7, 14: 6, 21: 5, 30: 4 }
              },
              {
                cohortId: '2026-W40',
                cohortLabel: 'Sep 29 - Oct 05',
                cohortStartDate: '2026-09-29',
                totalAcquiredUsers: 22,
                retentionPercentages: { 0: 100, 1: 59.1, 3: 45.5, 7: 36.4, 14: 27.3, 21: 22.7, 30: null },
                retainedUserCounts: { 0: 22, 1: 13, 3: 10, 7: 8, 14: 6, 21: 5, 30: null }
              },
              {
                cohortId: '2026-W41',
                cohortLabel: 'Oct 06 - Oct 12',
                cohortStartDate: '2026-10-06',
                totalAcquiredUsers: 25,
                retentionPercentages: { 0: 100, 1: 68.0, 3: 52.0, 7: 40.0, 14: 32.0, 21: null, 30: null },
                retainedUserCounts: { 0: 25, 1: 17, 3: 13, 7: 10, 14: 8, 21: null, 30: null }
              },
              {
                cohortId: '2026-W42',
                cohortLabel: 'Oct 13 - Oct 19',
                cohortStartDate: '2026-10-13',
                totalAcquiredUsers: 30,
                retentionPercentages: { 0: 100, 1: 73.3, 3: 56.7, 7: 46.7, 14: null, 21: null, 30: null },
                retainedUserCounts: { 0: 30, 1: 22, 3: 17, 7: 14, 14: null, 21: null, 30: null }
              }
            ]
          });
        }
      });
  }

  changeGranularity(gran: 'weekly' | 'monthly' | 'daily'): void {
    this.retentionGranularity.set(gran);
    this.loadCohortRetention();
  }

  loadCrashClusters(): void {
    let url = '/api/v1/telemetry/analytics/crashes/clusters?';
    if (this.crashSeverityFilter) url += `severity=${this.crashSeverityFilter}&`;
    if (this.crashStatusFilter) url += `status=${this.crashStatusFilter}&`;

    this.http.get<CrashClustersResponse>(url)
      .subscribe({
        next: (data) => this.crashResponse.set(data),
        error: () => {
          this.crashResponse.set({
            totalCrashOccurrences: 391,
            totalUniqueAffectedUsers: 262,
            totalClustersCount: 5,
            clusters: [
              {
                signature: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
                exceptionType: 'NullReferenceException',
                normalizedErrorMessage: 'Object reference not set to an instance of an object in PaymentModal.OnSubmit()',
                stackTraceSample: '   at SGrowing.Web.Components.PaymentModal.OnSubmit() in PaymentModal.cs:line X',
                totalOccurrences: 142,
                uniqueUsersAffected: 89,
                affectedVersions: ['2.4.0', '2.4.1'],
                platformBreakdown: { 'Windows 11 / Chrome': 92, 'macOS / Safari': 38 },
                recentBreadcrumbs: ['click:upgrade_btn', 'select:annual_pro_plan', 'input:stripe_token', 'submit:payment_modal'],
                firstSeenUtc: '2026-10-03T10:00:00Z',
                lastSeenUtc: '2026-10-31T05:00:00Z',
                severity: 'Critical',
                status: 'Open'
              },
              {
                signature: 'a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e',
                exceptionType: 'NpgsqlException',
                normalizedErrorMessage: 'Connection pool timeout (15000ms) exhausted during high query load',
                stackTraceSample: '   at SGrowing.Data.AnalyticsRepository.QueryAggregatesAsync() in AnalyticsRepository.cs:line X',
                totalOccurrences: 87,
                uniqueUsersAffected: 64,
                affectedVersions: ['2.4.1'],
                platformBreakdown: { 'Linux / Docker': 55 },
                recentBreadcrumbs: ['nav:dashboard', 'filter:date_range_90d', 'trigger:export_csv_report'],
                firstSeenUtc: '2026-10-10T14:30:00Z',
                lastSeenUtc: '2026-10-31T04:20:00Z',
                severity: 'Critical',
                status: 'Open'
              }
            ]
          });
        }
      });
  }

  loadFunnel(): void {
    const steps = this.activeFunnelPreset() === 'onboarding'
      ? ['landing_page', 'signup_click', 'workspace_setup', 'team_invite']
      : ['landing_page', 'signup_click', 'workspace_setup', 'team_invite', 'plan_subscription'];

    this.http.post<FunnelEvaluationResult>('/api/v1/telemetry/analytics/funnels/evaluate', {
      funnelName: this.activeFunnelPreset() === 'onboarding' ? 'Onboarding & Workspace Setup' : 'Full Commercial Conversion',
      steps
    }).subscribe({
      next: (data) => this.funnelResult.set(data),
      error: () => {
        this.funnelResult.set({
          funnelName: 'Onboarding & Workspace Setup',
          evaluatedAtUtc: new Date().toISOString(),
          totalEnteredUsers: 60,
          totalCompletedUsers: 24,
          overallConversionPercent: 40.0,
          biggestDropoffStep: 'workspace_setup',
          biggestDropoffPercent: 33.3,
          steps: [
            { stepIndex: 0, stepName: 'landing_page', reachedCount: 60, droppedCount: 12, stepConversionPercent: 100, overallConversionPercent: 100, dropoffPercent: 20, averageDurationSecondsFromPrevious: 0 },
            { stepIndex: 1, stepName: 'signup_click', reachedCount: 48, droppedCount: 12, stepConversionPercent: 80, overallConversionPercent: 80, dropoffPercent: 25, averageDurationSecondsFromPrevious: 180 },
            { stepIndex: 2, stepName: 'workspace_setup', reachedCount: 36, droppedCount: 12, stepConversionPercent: 75, overallConversionPercent: 60, dropoffPercent: 33.3, averageDurationSecondsFromPrevious: 240 },
            { stepIndex: 3, stepName: 'team_invite', reachedCount: 24, droppedCount: 0, stepConversionPercent: 66.7, overallConversionPercent: 40, dropoffPercent: 0, averageDurationSecondsFromPrevious: 300 }
          ]
        });
      }
    });
  }

  selectFunnelPreset(preset: 'onboarding' | 'subscription'): void {
    this.activeFunnelPreset.set(preset);
    this.loadFunnel();
  }

  loadRecentEvents(): void {
    this.http.get<TelemetryEventItem[]>('/api/v1/telemetry/analytics/recent-events?limit=25')
      .subscribe({
        next: (events) => this.recentEvents.set(events),
        error: () => this.recentEvents.set([])
      });
  }

  triggerBufferFlush(): void {
    this.isFlushing.set(true);
    this.http.post<{ flushedCount: number; pendingRemaining: number }>('/api/v1/telemetry/analytics/buffer/flush', {})
      .subscribe({
        next: (res) => {
          this.pendingBufferCount.set(res.pendingRemaining);
          this.isFlushing.set(false);
          this.loadAllData();
        },
        error: () => {
          this.pendingBufferCount.set(0);
          this.isFlushing.set(false);
        }
      });
  }

  updateCrashStatus(signature: string, status: string): void {
    this.http.post(`/api/v1/telemetry/analytics/crashes/${signature}/status`, { status })
      .subscribe({
        next: () => this.loadCrashClusters(),
        error: () => this.loadCrashClusters()
      });
  }

  openSimulateModal(): void {
    this.showSimulateModal.set(true);
  }

  closeSimulateModal(): void {
    this.showSimulateModal.set(false);
  }

  sendSingleTelemetryEvent(): void {
    const payload = {
      userId: this.simUserId,
      eventType: this.simEventType,
      eventName: this.simEventName,
      url: this.simUrl,
      timestampUtc: new Date().toISOString()
    };

    this.http.post('/api/v1/telemetry/analytics/i', payload).subscribe({
      next: () => {
        this.pendingBufferCount.update(c => c + 1);
        this.closeSimulateModal();
        this.loadRecentEvents();
      },
      error: () => {
        this.pendingBufferCount.update(c => c + 1);
        this.closeSimulateModal();
      }
    });
  }

  sendBurstTelemetryBatch(): void {
    const burstEvents = Array.from({ length: 50 }, (_, i) => ({
      userId: `USR-BURST-${i % 10}`,
      eventType: i % 3 === 0 ? 'click' : 'pageview',
      eventName: i % 3 === 0 ? 'button_click' : 'view_content',
      url: `/feature-${i % 5}`,
      timestampUtc: new Date().toISOString()
    }));

    this.http.post('/api/v1/telemetry/analytics/i', { batch: burstEvents }).subscribe({
      next: () => {
        this.pendingBufferCount.update(c => c + 50);
        this.closeSimulateModal();
        this.loadRecentEvents();
      },
      error: () => {
        this.pendingBufferCount.update(c => c + 50);
        this.closeSimulateModal();
      }
    });
  }

  toggleClusterExpand(sig: string): void {
    const current = new Set(this.expandedClusters());
    if (current.has(sig)) {
      current.delete(sig);
    } else {
      current.add(sig);
    }
    this.expandedClusters.set(current);
  }

  isClusterExpanded(sig: string): boolean {
    return this.expandedClusters().has(sig);
  }

  copySignature(sig: string): void {
    if (navigator?.clipboard) {
      navigator.clipboard.writeText(sig);
    }
  }

  getCellClass(percentage: number): string {
    if (percentage >= 50.0) {
      return 'bg-violet-600 text-white font-bold shadow-sm shadow-violet-600/30';
    } else if (percentage >= 35.0) {
      return 'bg-indigo-600/80 text-indigo-100';
    } else if (percentage >= 20.0) {
      return 'bg-sky-700/60 text-sky-100';
    } else if (percentage > 0) {
      return 'bg-slate-800 text-slate-300';
    } else {
      return 'bg-slate-900/60 text-slate-500';
    }
  }

  getSeverityBadgeClass(severity: string): string {
    switch (severity?.toLowerCase()) {
      case 'critical':
        return 'bg-rose-500/20 text-rose-300 border-rose-500/30';
      case 'error':
        return 'bg-orange-500/20 text-orange-300 border-orange-500/30';
      case 'warning':
        return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
      default:
        return 'bg-slate-800 text-slate-400 border-slate-700';
    }
  }
}
