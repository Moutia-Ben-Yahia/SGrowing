import { Component, OnInit, inject, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { TranslationService } from '../../core/services/translation.service';

export type IcpGrade = 'GradeA' | 'GradeB' | 'GradeC' | 'GradeD';

export type IntentSignalType =
  | 'PricingPageView'
  | 'DocumentationView'
  | 'WhitepaperDownload'
  | 'WebinarAttendance'
  | 'DemoRequest'
  | 'FreeTrialSignup'
  | 'ProductFeatureUsage'
  | 'EmailEngagement'
  | 'BomboraSurgeTopic'
  | 'G2CompetitorComparison'
  | 'G2CategorySearch'
  | 'JobPostingExpansion'
  | 'FundingAnnouncement'
  | 'ExecutiveHire';

export type SignalSource = 'FirstParty' | 'ThirdPartySurge' | 'Technographic' | 'Firmographic';

export type PredictiveBuyingStage = 'Cold' | 'Awareness' | 'Consideration' | 'HighIntent' | 'ReadyToBuy';

export type LeadQualificationStage = 'Unqualified' | 'MarketingQualifiedLead' | 'SalesQualifiedLead';

export type NextActionRecommendation =
  | 'ScheduleExecutiveBriefing'
  | 'RouteToSeniorSdrImmediate'
  | 'EnrollInHighIntentCadence'
  | 'TriggerAutomatedTechnicalNurture'
  | 'MonitorAndLowTouchNewsletter'
  | 'DisqualifyOrSuppress';

export interface IntentSignal {
  signalId: string;
  type: IntentSignalType;
  source: SignalSource;
  topicOrResource: string;
  rawScore: number;
  weight: number;
  occurredAtUtc: string;
  metadata?: Record<string, string>;
}

export interface FirmographicProfile {
  domain: string;
  companyName: string;
  industry: string;
  employeeCountTier: string;
  annualRevenueTier: string;
  headquartersRegion: string;
  foundedYear: number;
  fitScore: number;
  grade: IcpGrade;
}

export interface TechnographicProfile {
  installedStack: string[];
  compatibleCategories: string[];
  hasCompetitorStack: boolean;
  technographicScore: number;
}

export interface ScoreBreakdown {
  firmographicWeight: number;
  firmographicScore: number;
  firmographicWeighted: number;
  technographicWeight: number;
  technographicScore: number;
  technographicWeighted: number;
  firstPartyBehavioralWeight: number;
  firstPartyBehavioralRawScore: number;
  firstPartyBehavioralDecayedScore: number;
  firstPartyBehavioralWeighted: number;
  thirdPartySurgeWeight: number;
  thirdPartySurgeRawScore: number;
  thirdPartySurgeDecayedScore: number;
  thirdPartySurgeWeighted: number;
  compositeScore: number;
}

export interface LeadIntentRecord {
  leadId: string;
  tenantId: string;
  contactEmail: string;
  contactName: string;
  jobTitle: string;
  firmographics: FirmographicProfile;
  technographics: TechnographicProfile;
  activeSignals: IntentSignal[];
  scoreBreakdown: ScoreBreakdown;
  compositeScore: number;
  icpGrade: IcpGrade;
  buyingStage: PredictiveBuyingStage;
  qualificationStage: LeadQualificationStage;
  recommendedAction: NextActionRecommendation;
  actionReasoning: string;
  lastEvaluatedAtUtc: string;
  createdAtUtc: string;
}

export interface IntentScoringConfig {
  tenantId: string;
  weightFirmographic: number;
  weightTechnographic: number;
  weightFirstPartyBehavioral: number;
  weightThirdPartySurge: number;
  decayHalfLifeDays: number;
  mqlThresholdScore: number;
  sqlThresholdScore: number;
  targetIndustries: string[];
  targetEmployeeTiers: string[];
  targetRegions: string[];
  updatedAtUtc: string;
}

export interface IntentScoringOverview {
  totalLeads: number;
  sqlCount: number;
  mqlCount: number;
  unqualifiedCount: number;
  gradeACount: number;
  gradeBCount: number;
  gradeCCount: number;
  gradeDCount: number;
  averageCompositeScore: number;
  topSurgingLeads: LeadIntentRecord[];
  activeConfig: IntentScoringConfig;
  computedAtUtc: string;
}

@Component({
  selector: 'app-lead-intent-scoring-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div [attr.dir]="isRtl() ? 'rtl' : 'ltr'" class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans select-none pb-16">
      <!-- Top Navigation & Header Bar -->
      <header class="border-b border-slate-800 bg-slate-900/90 backdrop-blur sticky top-0 z-30 px-6 py-4 flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <div class="h-10 w-10 rounded-xl bg-gradient-to-tr from-indigo-600 to-violet-500 flex items-center justify-center text-white text-xl shadow-lg shadow-indigo-500/20">
            ⚡
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-xl font-bold tracking-tight text-white flex items-center gap-2">
                Predictive Buying Intent & Multi-Signal Lead Scoring Engine
              </h1>
              <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-violet-500/10 text-violet-400 border border-violet-500/20 font-semibold">
                PRD Domain B4 & D1 • Exponential Decay
              </span>
            </div>
            <p class="text-xs text-slate-400 mt-0.5">
              Multi-signal ICP fit, first-party behavioral intent decay, Bombora/G2 surge fusion, and automated MQL/SQL stage transitions.
            </p>
          </div>
        </div>

        <div class="flex items-center gap-2.5">
          <button
            type="button"
            (click)="recalculateAllScores()"
            [disabled]="isRecalculating()"
            class="px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white shadow-md shadow-indigo-900/30 transition flex items-center gap-1.5 disabled:opacity-50">
            <span [class.animate-spin]="isRecalculating()">⚙</span>
            <span>{{ isRecalculating() ? 'Recalculating...' : 'Recalculate Decay Scores' }}</span>
          </button>
          <button
            type="button"
            (click)="resetToSeeds()"
            [disabled]="isLoading()"
            class="px-3 py-1.5 text-xs font-medium rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition flex items-center gap-1.5">
            <span>↺</span>
            <span>Reset Demo</span>
          </button>
        </div>
      </header>

      <!-- KPI Radar Metrics Strip -->
      <section class="grid grid-cols-2 lg:grid-cols-4 gap-4 px-6 pt-6">
        <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
          <div>
            <span class="text-xs text-slate-400 font-medium">Total Tracked Leads</span>
            <div class="text-2xl font-bold text-white mt-1">{{ leads().length }}</div>
            <div class="text-[11px] text-emerald-400 mt-0.5">{{ gradeACount() }} Grade A Fit Accounts</div>
          </div>
          <div class="h-10 w-10 rounded-lg bg-slate-800/80 flex items-center justify-center text-lg text-slate-300">👥</div>
        </div>

        <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
          <div>
            <span class="text-xs text-slate-400 font-medium">Sales Qualified (SQL)</span>
            <div class="text-2xl font-bold text-purple-400 mt-1">{{ sqlCount() }}</div>
            <div class="text-[11px] text-purple-300/80 mt-0.5">Threshold &ge; {{ config()?.sqlThresholdScore ?? 80 }} + Grade A/B</div>
          </div>
          <div class="h-10 w-10 rounded-lg bg-purple-500/10 border border-purple-500/20 flex items-center justify-center text-lg text-purple-400">🔥</div>
        </div>

        <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
          <div>
            <span class="text-xs text-slate-400 font-medium">Marketing Qualified (MQL)</span>
            <div class="text-2xl font-bold text-sky-400 mt-1">{{ mqlCount() }}</div>
            <div class="text-[11px] text-sky-300/80 mt-0.5">Threshold &ge; {{ config()?.mqlThresholdScore ?? 60 }}</div>
          </div>
          <div class="h-10 w-10 rounded-lg bg-sky-500/10 border border-sky-500/20 flex items-center justify-center text-lg text-sky-400">🎯</div>
        </div>

        <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex items-center justify-between">
          <div>
            <span class="text-xs text-slate-400 font-medium">Average Intent Score</span>
            <div class="text-2xl font-bold text-emerald-400 mt-1">{{ averageScore() }}</div>
            <div class="text-[11px] text-slate-400 mt-0.5">Half-Life {{ config()?.decayHalfLifeDays ?? 14 }} Days Decay</div>
          </div>
          <div class="h-10 w-10 rounded-lg bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center text-lg text-emerald-400">📊</div>
        </div>
      </section>

      <!-- Navigation Tab Bar -->
      <nav class="flex items-center gap-2 px-6 pt-6 border-b border-slate-800 pb-px">
        <button
          type="button"
          (click)="setTab('matrix')"
          [class.text-white]="activeTab() === 'matrix'"
          [class.border-indigo-500]="activeTab() === 'matrix'"
          [class.text-slate-400]="activeTab() !== 'matrix'"
          [class.border-transparent]="activeTab() !== 'matrix'"
          class="px-4 py-2 text-xs font-semibold border-b-2 transition flex items-center gap-2 hover:text-white">
          <span>📋</span>
          <span>Lead Scoring Matrix</span>
          <span class="px-1.5 py-0.2 rounded-full text-[10px] bg-slate-800 text-slate-300">{{ filteredLeads().length }}</span>
        </button>

        <button
          type="button"
          (click)="setTab('ingest')"
          [class.text-white]="activeTab() === 'ingest'"
          [class.border-indigo-500]="activeTab() === 'ingest'"
          [class.text-slate-400]="activeTab() !== 'ingest'"
          [class.border-transparent]="activeTab() !== 'ingest'"
          class="px-4 py-2 text-xs font-semibold border-b-2 transition flex items-center gap-2 hover:text-white">
          <span>⚡</span>
          <span>Signal Ingestion Studio</span>
        </button>

        <button
          type="button"
          (click)="setTab('config')"
          [class.text-white]="activeTab() === 'config'"
          [class.border-indigo-500]="activeTab() === 'config'"
          [class.text-slate-400]="activeTab() !== 'config'"
          [class.border-transparent]="activeTab() !== 'config'"
          class="px-4 py-2 text-xs font-semibold border-b-2 transition flex items-center gap-2 hover:text-white">
          <span>⚙️</span>
          <span>Decay & Weight Studio</span>
        </button>

        <button
          type="button"
          (click)="setTab('analytics')"
          [class.text-white]="activeTab() === 'analytics'"
          [class.border-indigo-500]="activeTab() === 'analytics'"
          [class.text-slate-400]="activeTab() !== 'analytics'"
          [class.border-transparent]="activeTab() !== 'analytics'"
          class="px-4 py-2 text-xs font-semibold border-b-2 transition flex items-center gap-2 hover:text-white">
          <span>📈</span>
          <span>Buying Stage Analytics</span>
        </button>
      </nav>

      <!-- Main Tab Content Area -->
      <main class="flex-1 px-6 pt-6">
        @if (activeTab() === 'matrix') {
          <!-- Filter and Search Header -->
          <div class="flex flex-col md:flex-row items-center justify-between gap-3 mb-4">
            <div class="relative w-full md:w-96">
              <input
                type="text"
                [ngModel]="searchQuery()"
                (ngModelChange)="searchQuery.set($event)"
                placeholder="Search by company, contact, or email..."
                class="w-full bg-slate-900 border border-slate-800 rounded-lg px-3.5 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 transition" />
            </div>

            <div class="flex items-center gap-2.5 w-full md:w-auto">
              <select
                [ngModel]="stageFilter()"
                (ngModelChange)="stageFilter.set($event)"
                class="bg-slate-900 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-indigo-500">
                <option value="all">All Stages</option>
                <option value="SalesQualifiedLead">SQL Only</option>
                <option value="MarketingQualifiedLead">MQL Only</option>
                <option value="Unqualified">Unqualified</option>
              </select>

              <select
                [ngModel]="gradeFilter()"
                (ngModelChange)="gradeFilter.set($event)"
                class="bg-slate-900 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-indigo-500">
                <option value="all">All ICP Grades</option>
                <option value="GradeA">Grade A (90-100)</option>
                <option value="GradeB">Grade B (75-89)</option>
                <option value="GradeC">Grade C (50-74)</option>
                <option value="GradeD">Grade D (< 50)</option>
              </select>
            </div>
          </div>

          <!-- Split Master-Detail Layout -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
            <!-- Left Column: Lead Cards -->
            <div class="lg:col-span-5 flex flex-col gap-3">
              @for (lead of filteredLeads(); track lead.leadId) {
                <div
                  (click)="selectLead(lead)"
                  [class.border-indigo-500]="selectedLead()?.leadId === lead.leadId"
                  [class.bg-slate-900]="selectedLead()?.leadId === lead.leadId"
                  [class.bg-slate-900/40]="selectedLead()?.leadId !== lead.leadId"
                  class="p-4 rounded-xl border border-slate-800 hover:border-slate-700 cursor-pointer transition flex flex-col gap-2.5 shadow-sm">
                  <div class="flex items-start justify-between">
                    <div>
                      <div class="font-bold text-white text-sm flex items-center gap-2">
                        <span>{{ lead.firmographics.companyName }}</span>
                        <span [class]="getGradeBadgeClass(lead.icpGrade)" class="px-2 py-0.5 rounded text-[10px] font-mono font-semibold">
                          {{ lead.icpGrade }}
                        </span>
                      </div>
                      <div class="text-xs text-slate-400 mt-0.5">
                        {{ lead.contactName }} • {{ lead.jobTitle }}
                      </div>
                    </div>

                    <div class="text-right">
                      <div class="text-lg font-extrabold text-white">
                        {{ lead.compositeScore }}
                      </div>
                      <span class="text-[10px] text-slate-400 font-mono">Score / 100</span>
                    </div>
                  </div>

                  <div class="flex items-center gap-2 flex-wrap text-xs">
                    <span [class]="getStageBadgeClass(lead.qualificationStage)" class="px-2 py-0.5 rounded text-[11px] font-medium">
                      {{ lead.qualificationStage }}
                    </span>
                    <span [class]="getBuyingStageClass(lead.buyingStage)" class="px-2 py-0.5 rounded text-[11px] font-medium">
                      {{ lead.buyingStage }}
                    </span>
                    <span class="text-slate-400 text-[11px] bg-slate-800/80 px-2 py-0.5 rounded">
                      {{ lead.firmographics.industry }}
                    </span>
                  </div>

                  <div class="text-[11px] text-indigo-300/90 font-medium pt-1 border-t border-slate-800/60 flex items-center justify-between">
                    <span>{{ getNextActionIcon(lead.recommendedAction) }}</span>
                    <span class="text-slate-400 text-[10px]">{{ lead.activeSignals.length }} signals</span>
                  </div>
                </div>
              } @empty {
                <div class="p-8 text-center text-slate-500 rounded-xl bg-slate-900/30 border border-slate-800">
                  No leads matching selected criteria.
                </div>
              }
            </div>

            <!-- Right Column: Lead Inspector -->
            <div class="lg:col-span-7">
              @if (selectedLead(); as lead) {
                <div class="p-6 rounded-xl bg-slate-900/90 border border-slate-800 flex flex-col gap-6 sticky top-24 shadow-xl">
                  <!-- Header Inspector -->
                  <div class="flex items-start justify-between pb-4 border-b border-slate-800">
                    <div>
                      <div class="flex items-center gap-2">
                        <h2 class="text-xl font-bold text-white">{{ lead.firmographics.companyName }}</h2>
                        <span [class]="getGradeBadgeClass(lead.icpGrade)" class="px-2.5 py-0.5 rounded text-xs font-mono font-bold">
                          ICP {{ lead.icpGrade }}
                        </span>
                        <span [class]="getStageBadgeClass(lead.qualificationStage)" class="px-2.5 py-0.5 rounded text-xs font-semibold">
                          {{ lead.qualificationStage }}
                        </span>
                      </div>
                      <div class="text-xs text-slate-300 mt-1 flex items-center gap-3">
                        <span>👤 {{ lead.contactName }} ({{ lead.jobTitle }})</span>
                        <span>✉️ {{ lead.contactEmail }}</span>
                        <span>🌐 {{ lead.firmographics.domain }}</span>
                      </div>
                    </div>

                    <div class="text-right">
                      <div class="text-3xl font-extrabold text-transparent bg-clip-text bg-gradient-to-r from-indigo-400 to-violet-300">
                        {{ lead.compositeScore }}
                      </div>
                      <span class="text-[10px] text-slate-400 font-mono">Composite Intent</span>
                    </div>
                  </div>

                  <!-- Recommendation Action Box -->
                  <div class="p-4 rounded-xl bg-indigo-950/40 border border-indigo-500/30 flex items-start gap-3">
                    <div class="text-2xl">💡</div>
                    <div class="flex-1">
                      <div class="text-xs font-bold text-indigo-300 uppercase tracking-wider">
                        Autonomous Next Action Recommendation
                      </div>
                      <div class="text-sm font-semibold text-white mt-0.5">
                        {{ getNextActionIcon(lead.recommendedAction) }}
                      </div>
                      <p class="text-xs text-slate-300 mt-1">
                        {{ lead.actionReasoning }}
                      </p>
                    </div>
                  </div>

                  <!-- Multi-Factor Radar Breakdown -->
                  <div class="flex flex-col gap-3">
                    <h3 class="text-xs font-bold text-slate-400 uppercase tracking-wider">
                      Weighted Score Breakdown & Signal Contributions
                    </h3>

                    <div class="space-y-3">
                      <!-- Firmographic Fit -->
                      <div>
                        <div class="flex justify-between text-xs mb-1">
                          <span class="text-slate-300 font-medium">Firmographic ICP Fit (Weight {{ lead.scoreBreakdown.firmographicWeight * 100 }}%)</span>
                          <span class="font-mono text-emerald-400">{{ lead.scoreBreakdown.firmographicScore }} / 100 &rarr; +{{ lead.scoreBreakdown.firmographicWeighted }} pts</span>
                        </div>
                        <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
                          <div class="bg-emerald-500 h-2 rounded-full" [style.width.%]="lead.scoreBreakdown.firmographicScore"></div>
                        </div>
                        <div class="text-[11px] text-slate-400 mt-0.5 flex gap-3">
                          <span>Region: {{ lead.firmographics.headquartersRegion }}</span>
                          <span>Employees: {{ lead.firmographics.employeeCountTier }}</span>
                          <span>Revenue: {{ lead.firmographics.annualRevenueTier }}</span>
                        </div>
                      </div>

                      <!-- Technographic Fit -->
                      <div>
                        <div class="flex justify-between text-xs mb-1">
                          <span class="text-slate-300 font-medium">Technographic Compatibility (Weight {{ lead.scoreBreakdown.technographicWeight * 100 }}%)</span>
                          <span class="font-mono text-sky-400">{{ lead.scoreBreakdown.technographicScore }} / 100 &rarr; +{{ lead.scoreBreakdown.technographicWeighted }} pts</span>
                        </div>
                        <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
                          <div class="bg-sky-500 h-2 rounded-full" [style.width.%]="lead.scoreBreakdown.technographicScore"></div>
                        </div>
                        <div class="text-[11px] text-slate-400 mt-0.5 flex gap-1.5 flex-wrap">
                          <span class="text-slate-400">Stack:</span>
                          @for (tech of lead.technographics.installedStack; track tech) {
                            <span class="px-1.5 py-0.2 bg-slate-800 rounded text-[10px] text-slate-300 font-mono">{{ tech }}</span>
                          }
                        </div>
                      </div>

                      <!-- First-Party Behavioral Intent -->
                      <div>
                        <div class="flex justify-between text-xs mb-1">
                          <span class="text-slate-300 font-medium">First-Party Behavioral Intent (Weight {{ lead.scoreBreakdown.firstPartyBehavioralWeight * 100 }}%)</span>
                          <span class="font-mono text-violet-400">Raw {{ lead.scoreBreakdown.firstPartyBehavioralRawScore }} | Decayed {{ lead.scoreBreakdown.firstPartyBehavioralDecayedScore }} &rarr; +{{ lead.scoreBreakdown.firstPartyBehavioralWeighted }} pts</span>
                        </div>
                        <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
                          <div class="bg-violet-500 h-2 rounded-full" [style.width.%]="lead.scoreBreakdown.firstPartyBehavioralDecayedScore"></div>
                        </div>
                      </div>

                      <!-- Third-Party Surge Intent -->
                      <div>
                        <div class="flex justify-between text-xs mb-1">
                          <span class="text-slate-300 font-medium">Third-Party Bombora/G2 Surge (Weight {{ lead.scoreBreakdown.thirdPartySurgeWeight * 100 }}%)</span>
                          <span class="font-mono text-amber-400">Raw {{ lead.scoreBreakdown.thirdPartySurgeRawScore }} | Decayed {{ lead.scoreBreakdown.thirdPartySurgeDecayedScore }} &rarr; +{{ lead.scoreBreakdown.thirdPartySurgeWeighted }} pts</span>
                        </div>
                        <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
                          <div class="bg-amber-500 h-2 rounded-full" [style.width.%]="lead.scoreBreakdown.thirdPartySurgeDecayedScore"></div>
                        </div>
                      </div>
                    </div>
                  </div>

                  <!-- Active Intent Signals Stream -->
                  <div class="flex flex-col gap-2.5">
                    <div class="flex items-center justify-between">
                      <h3 class="text-xs font-bold text-slate-400 uppercase tracking-wider">
                        Active Ingested Signals ({{ lead.activeSignals.length }})
                      </h3>
                      <button
                        type="button"
                        (click)="setTab('ingest')"
                        class="text-xs text-indigo-400 hover:text-indigo-300 font-medium transition">
                        + Ingest New Signal
                      </button>
                    </div>

                    <div class="space-y-2 max-h-48 overflow-y-auto pr-1">
                      @for (sig of lead.activeSignals; track sig.signalId) {
                        <div class="p-2.5 rounded-lg bg-slate-800/60 border border-slate-700/60 flex items-center justify-between text-xs">
                          <div class="flex items-center gap-2">
                            <span class="text-sm">{{ sig.source === 'FirstParty' ? '🌐' : '📡' }}</span>
                            <div>
                              <div class="font-semibold text-slate-200">{{ sig.type }}</div>
                              <div class="text-[11px] text-slate-400 font-mono truncate max-w-xs">{{ sig.topicOrResource }}</div>
                            </div>
                          </div>

                          <div class="text-right">
                            <div class="font-mono font-bold text-slate-200">{{ sig.rawScore }} pts</div>
                            <div class="text-[10px] text-slate-400">Weight &times;{{ sig.weight }}</div>
                          </div>
                        </div>
                      } @empty {
                        <div class="text-xs text-slate-500 text-center py-4">No active signals recorded yet.</div>
                      }
                    </div>
                  </div>
                </div>
              } @else {
                <div class="p-12 text-center text-slate-500 rounded-xl bg-slate-900/40 border border-slate-800">
                  Select a lead from the matrix on the left to inspect detailed intent signals.
                </div>
              }
            </div>
          </div>
        }

        @if (activeTab() === 'ingest') {
          <!-- Signal Ingestion Form -->
          <div class="max-w-2xl mx-auto p-6 rounded-xl bg-slate-900/90 border border-slate-800 shadow-xl flex flex-col gap-5">
            <div>
              <h2 class="text-lg font-bold text-white flex items-center gap-2">
                <span>⚡</span>
                <span>Ingest Real-Time Buying Intent Signal</span>
              </h2>
              <p class="text-xs text-slate-400 mt-1">
                Feed first-party telemetry (pricing views, demo requests, docs) or third-party surge signals (Bombora topics, G2 reviews).
              </p>
            </div>

            <!-- Target Lead Selection -->
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Target Prospect Account</label>
              <select
                [ngModel]="selectedLead()?.leadId"
                (ngModelChange)="onLeadSelectById($event)"
                class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                @for (lead of leads(); track lead.leadId) {
                  <option [value]="lead.leadId">
                    {{ lead.firmographics.companyName }} — {{ lead.contactName }} (Current Score: {{ lead.compositeScore }})
                  </option>
                }
              </select>
            </div>

            <div class="grid grid-cols-2 gap-4">
              <!-- Signal Type -->
              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Signal Type</label>
                <select
                  [(ngModel)]="signalType"
                  class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                  <option value="DemoRequest">Demo Request (High Intent)</option>
                  <option value="PricingPageView">Pricing Page View</option>
                  <option value="DocumentationView">Documentation Exploration</option>
                  <option value="WhitepaperDownload">Whitepaper Download</option>
                  <option value="WebinarAttendance">Webinar Attendance</option>
                  <option value="BomboraSurgeTopic">Bombora Intent Surge Topic</option>
                  <option value="G2CompetitorComparison">G2 Competitor Comparison</option>
                  <option value="ExecutiveHire">Executive C-Level Hire</option>
                </select>
              </div>

              <!-- Signal Source -->
              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Source Origin</label>
                <select
                  [(ngModel)]="signalSource"
                  class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500">
                  <option value="FirstParty">First-Party (Telemetry & Website)</option>
                  <option value="ThirdPartySurge">Third-Party Surge (Bombora/G2)</option>
                </select>
              </div>
            </div>

            <!-- Topic or Resource URL -->
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Topic / URL Resource</label>
              <input
                type="text"
                [(ngModel)]="signalTopic"
                placeholder="e.g., /pricing/enterprise or 'Omnichannel CRM Migration'"
                class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3.5 py-2 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500" />
            </div>

            <div class="grid grid-cols-2 gap-4">
              <!-- Raw Score -->
              <div>
                <div class="flex justify-between text-xs mb-1">
                  <span class="text-slate-300 font-medium">Raw Intent Score:</span>
                  <span class="font-mono text-indigo-400 font-bold">{{ signalRawScore }} pts</span>
                </div>
                <input
                  type="range"
                  min="5"
                  max="100"
                  step="5"
                  [(ngModel)]="signalRawScore"
                  class="w-full accent-indigo-500" />
              </div>

              <!-- Weight Multiplier -->
              <div>
                <div class="flex justify-between text-xs mb-1">
                  <span class="text-slate-300 font-medium">Signal Weight Multiplier:</span>
                  <span class="font-mono text-violet-400 font-bold">&times;{{ signalWeight }}</span>
                </div>
                <input
                  type="range"
                  min="0.5"
                  max="2.5"
                  step="0.1"
                  [(ngModel)]="signalWeight"
                  class="w-full accent-violet-500" />
              </div>
            </div>

            <!-- Ingest Button -->
            <button
              type="button"
              (click)="ingestSignalForSelectedLead()"
              [disabled]="isIngesting() || !selectedLead()"
              class="w-full py-2.5 px-4 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white font-semibold text-xs shadow-lg shadow-indigo-900/30 transition flex items-center justify-center gap-2 disabled:opacity-50">
              <span>{{ isIngesting() ? 'Ingesting & Evaluating...' : 'Ingest Signal & Re-evaluate Lead' }}</span>
            </button>
          </div>
        }

        @if (activeTab() === 'config') {
          <!-- Configuration Studio -->
          <div class="max-w-2xl mx-auto p-6 rounded-xl bg-slate-900/90 border border-slate-800 shadow-xl flex flex-col gap-6">
            <div>
              <h2 class="text-lg font-bold text-white flex items-center gap-2">
                <span>⚙️</span>
                <span>Scoring Weights & Decay Configuration</span>
              </h2>
              <p class="text-xs text-slate-400 mt-1">
                Customize relative weighting between firmographic fit, technographics, first-party actions, third-party surge, and decay half-life.
              </p>
            </div>

            <div class="space-y-4">
              <div>
                <div class="flex justify-between text-xs mb-1">
                  <span class="text-slate-300 font-medium">Firmographic Fit Weight:</span>
                  <span class="font-mono text-emerald-400 font-bold">{{ (configFirmographicWeight * 100).toFixed(0) }}%</span>
                </div>
                <input type="range" min="0" max="1" step="0.05" [(ngModel)]="configFirmographicWeight" class="w-full accent-emerald-500" />
              </div>

              <div>
                <div class="flex justify-between text-xs mb-1">
                  <span class="text-slate-300 font-medium">Technographic Compatibility Weight:</span>
                  <span class="font-mono text-sky-400 font-bold">{{ (configTechnographicWeight * 100).toFixed(0) }}%</span>
                </div>
                <input type="range" min="0" max="1" step="0.05" [(ngModel)]="configTechnographicWeight" class="w-full accent-sky-500" />
              </div>

              <div>
                <div class="flex justify-between text-xs mb-1">
                  <span class="text-slate-300 font-medium">First-Party Behavioral Intent Weight:</span>
                  <span class="font-mono text-violet-400 font-bold">{{ (configFirstPartyWeight * 100).toFixed(0) }}%</span>
                </div>
                <input type="range" min="0" max="1" step="0.05" [(ngModel)]="configFirstPartyWeight" class="w-full accent-violet-500" />
              </div>

              <div>
                <div class="flex justify-between text-xs mb-1">
                  <span class="text-slate-300 font-medium">Third-Party Surge Weight:</span>
                  <span class="font-mono text-amber-400 font-bold">{{ (configThirdPartyWeight * 100).toFixed(0) }}%</span>
                </div>
                <input type="range" min="0" max="1" step="0.05" [(ngModel)]="configThirdPartyWeight" class="w-full accent-amber-500" />
              </div>

              <div class="grid grid-cols-3 gap-4 pt-2">
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Decay Half-Life (Days)</label>
                  <input
                    type="number"
                    min="1"
                    max="90"
                    [(ngModel)]="configDecayHalfLifeDays"
                    class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white" />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">MQL Threshold Score</label>
                  <input
                    type="number"
                    min="10"
                    max="90"
                    [(ngModel)]="configMqlThreshold"
                    class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white" />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">SQL Threshold Score</label>
                  <input
                    type="number"
                    min="30"
                    max="99"
                    [(ngModel)]="configSqlThreshold"
                    class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white" />
                </div>
              </div>
            </div>

            <button
              type="button"
              (click)="saveConfiguration()"
              [disabled]="isSavingConfig()"
              class="w-full py-2.5 px-4 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white font-semibold text-xs shadow-lg shadow-indigo-900/30 transition flex items-center justify-center gap-2 disabled:opacity-50">
              <span>{{ isSavingConfig() ? 'Saving & Re-evaluating...' : 'Save Configuration & Recalculate Leads' }}</span>
            </button>
          </div>
        }

        @if (activeTab() === 'analytics') {
          <!-- Buying Stage Analytics & Leaderboard -->
          <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
            <!-- ICP Grade Distribution -->
            <div class="p-6 rounded-xl bg-slate-900/80 border border-slate-800 flex flex-col gap-4">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>🎯</span>
                <span>Ideal Customer Profile (ICP) Grade Distribution</span>
              </h3>
              <div class="space-y-3">
                <div class="flex items-center justify-between text-xs">
                  <span class="text-emerald-400 font-semibold">Grade A (90-100 Fit)</span>
                  <span class="font-mono text-white">{{ overview()?.gradeACount ?? 0 }} accounts</span>
                </div>
                <div class="flex items-center justify-between text-xs">
                  <span class="text-sky-400 font-semibold">Grade B (75-89 Fit)</span>
                  <span class="font-mono text-white">{{ overview()?.gradeBCount ?? 0 }} accounts</span>
                </div>
                <div class="flex items-center justify-between text-xs">
                  <span class="text-amber-400 font-semibold">Grade C (50-74 Fit)</span>
                  <span class="font-mono text-white">{{ overview()?.gradeCCount ?? 0 }} accounts</span>
                </div>
                <div class="flex items-center justify-between text-xs">
                  <span class="text-rose-400 font-semibold">Grade D (< 50 Fit)</span>
                  <span class="font-mono text-white">{{ overview()?.gradeDCount ?? 0 }} accounts</span>
                </div>
              </div>
            </div>

            <!-- Top Surging Accounts -->
            <div class="p-6 rounded-xl bg-slate-900/80 border border-slate-800 flex flex-col gap-4">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>🚀</span>
                <span>Top Surging Buying Accounts</span>
              </h3>

              <div class="space-y-2.5">
                @for (surge of overview()?.topSurgingLeads; track surge.leadId) {
                  <div class="p-3 rounded-lg bg-slate-800/60 border border-slate-700/60 flex items-center justify-between text-xs">
                    <div>
                      <div class="font-bold text-white">{{ surge.firmographics.companyName }}</div>
                      <div class="text-[11px] text-slate-400">{{ surge.contactName }} • {{ surge.buyingStage }}</div>
                    </div>
                    <div class="text-right">
                      <div class="text-sm font-extrabold text-indigo-400">{{ surge.compositeScore }}</div>
                      <div class="text-[10px] text-slate-400">{{ surge.qualificationStage }}</div>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>
        }
      </main>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class LeadIntentScoringStudioComponent implements OnInit {
  private readonly http = inject(HttpClient);
  public readonly translation = inject(TranslationService, { optional: true });

  public readonly isRtl = computed(() => this.translation?.currentLocale() === 'ar-AR');

  // Main Signals & Data State
  public readonly leads = signal<LeadIntentRecord[]>([]);
  public readonly overview = signal<IntentScoringOverview | null>(null);
  public readonly config = signal<IntentScoringConfig | null>(null);
  public readonly selectedLead = signal<LeadIntentRecord | null>(null);

  // UI state
  public readonly isLoading = signal<boolean>(false);
  public readonly isRecalculating = signal<boolean>(false);
  public readonly isIngesting = signal<boolean>(false);
  public readonly isSavingConfig = signal<boolean>(false);
  public readonly activeTab = signal<'matrix' | 'ingest' | 'config' | 'analytics'>('matrix');

  // Filters
  public readonly searchQuery = signal<string>('');
  public readonly stageFilter = signal<string>('all');
  public readonly gradeFilter = signal<string>('all');

  // New Signal Form
  public signalType: IntentSignalType = 'DemoRequest';
  public signalSource: SignalSource = 'FirstParty';
  public signalTopic: string = '/demo-request/enterprise';
  public signalRawScore: number = 45;
  public signalWeight: number = 1.2;

  // New Prospect Form
  public newContactEmail: string = '';
  public newContactName: string = '';
  public newJobTitle: string = 'VP Engineering';
  public newCompanyName: string = '';
  public newCompanyDomain: string = '';
  public newIndustry: string = 'B2B SaaS';
  public newEmployeeTier: string = '51-200';
  public newHeadquarters: string = 'MENA - Riyadh';
  public newFitScore: number = 88;

  // Editable Configuration Draft
  public configFirmographicWeight: number = 0.25;
  public configTechnographicWeight: number = 0.20;
  public configFirstPartyWeight: number = 0.35;
  public configThirdPartyWeight: number = 0.20;
  public configDecayHalfLifeDays: number = 14;
  public configMqlThreshold: number = 60.0;
  public configSqlThreshold: number = 80.0;

  // Computed Properties
  public readonly filteredLeads = computed(() => {
    const query = this.searchQuery().toLowerCase().trim();
    const stage = this.stageFilter();
    const grade = this.gradeFilter();

    return this.leads().filter(lead => {
      const matchesQuery =
        !query ||
        lead.contactName.toLowerCase().includes(query) ||
        lead.contactEmail.toLowerCase().includes(query) ||
        lead.firmographics.companyName.toLowerCase().includes(query) ||
        lead.firmographics.domain.toLowerCase().includes(query);

      const matchesStage = stage === 'all' || lead.qualificationStage === stage;
      const matchesGrade = grade === 'all' || lead.icpGrade === grade;

      return matchesQuery && matchesStage && matchesGrade;
    });
  });

  public readonly sqlCount = computed(() => {
    return this.leads().filter(l => l.qualificationStage === 'SalesQualifiedLead').length;
  });

  public readonly mqlCount = computed(() => {
    return this.leads().filter(l => l.qualificationStage === 'MarketingQualifiedLead').length;
  });

  public readonly averageScore = computed(() => {
    const list = this.leads();
    if (list.length === 0) return 0;
    const sum = list.reduce((acc, l) => acc + l.compositeScore, 0);
    return Math.round((sum / list.length) * 10) / 10;
  });

  public readonly gradeACount = computed(() => {
    return this.leads().filter(l => l.icpGrade === 'GradeA').length;
  });

  ngOnInit(): void {
    this.loadAllData();
  }

  public loadAllData(): void {
    this.isLoading.set(true);
    this.http.get<LeadIntentRecord[]>('/api/v1/crm/intent-scoring').subscribe({
      next: data => {
        this.leads.set(data || []);
        if (data && data.length > 0 && !this.selectedLead()) {
          this.selectedLead.set(data[0]);
        }
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
      }
    });

    this.http.get<IntentScoringOverview>('/api/v1/crm/intent-scoring/overview').subscribe({
      next: ov => {
        this.overview.set(ov);
      }
    });

    this.http.get<IntentScoringConfig>('/api/v1/crm/intent-scoring/config').subscribe({
      next: cfg => {
        this.config.set(cfg);
        if (cfg) {
          this.configFirmographicWeight = cfg.weightFirmographic;
          this.configTechnographicWeight = cfg.weightTechnographic;
          this.configFirstPartyWeight = cfg.weightFirstPartyBehavioral;
          this.configThirdPartyWeight = cfg.weightThirdPartySurge;
          this.configDecayHalfLifeDays = cfg.decayHalfLifeDays;
          this.configMqlThreshold = cfg.mqlThresholdScore;
          this.configSqlThreshold = cfg.sqlThresholdScore;
        }
      }
    });
  }

  public selectLead(lead: LeadIntentRecord): void {
    this.selectedLead.set(lead);
  }

  public onLeadSelectById(leadId: string): void {
    const found = this.leads().find(l => l.leadId === leadId);
    if (found) {
      this.selectedLead.set(found);
    }
  }

  public setTab(tab: 'matrix' | 'ingest' | 'config' | 'analytics'): void {
    this.activeTab.set(tab);
  }

  public ingestSignalForSelectedLead(): void {
    const lead = this.selectedLead();
    if (!lead) return;

    this.isIngesting.set(true);
    const payload = {
      leadId: lead.leadId,
      type: this.signalType,
      source: this.signalSource,
      topicOrResource: this.signalTopic,
      rawScore: this.signalRawScore,
      weight: this.signalWeight,
      occurredAtUtc: new Date().toISOString()
    };

    this.http.post<LeadIntentRecord>('/api/v1/crm/intent-scoring/signals', payload).subscribe({
      next: updated => {
        this.isIngesting.set(false);
        this.updateLeadInState(updated);
        this.selectedLead.set(updated);
        this.refreshOverview();
      },
      error: () => {
        this.isIngesting.set(false);
      }
    });
  }

  public createNewLead(): void {
    if (!this.newContactEmail || !this.newCompanyName) return;

    this.isLoading.set(true);
    const payload = {
      contactEmail: this.newContactEmail,
      contactName: this.newContactName || this.newContactEmail.split('@')[0],
      jobTitle: this.newJobTitle,
      firmographics: {
        domain: this.newCompanyDomain || this.newContactEmail.split('@')[1] || 'example.com',
        companyName: this.newCompanyName,
        industry: this.newIndustry,
        employeeCountTier: this.newEmployeeTier,
        annualRevenueTier: '$10M-$50M',
        headquartersRegion: this.newHeadquarters,
        foundedYear: 2021,
        fitScore: this.newFitScore,
        grade: this.newFitScore >= 90 ? 'GradeA' : this.newFitScore >= 75 ? 'GradeB' : this.newFitScore >= 50 ? 'GradeC' : 'GradeD'
      },
      technographics: {
        installedStack: ['Cloud', 'Modern DB'],
        compatibleCategories: ['CRM', 'Analytics'],
        hasCompetitorStack: false,
        technographicScore: 80.0
      }
    };

    this.http.post<LeadIntentRecord>('/api/v1/crm/intent-scoring/leads', payload).subscribe({
      next: created => {
        this.isLoading.set(false);
        this.leads.update(prev => [created, ...prev]);
        this.selectedLead.set(created);
        this.resetNewLeadForm();
        this.refreshOverview();
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  public recalculateAllScores(): void {
    this.isRecalculating.set(true);
    this.http.post('/api/v1/crm/intent-scoring/recalculate', {}).subscribe({
      next: () => {
        this.isRecalculating.set(false);
        this.loadAllData();
      },
      error: () => {
        this.isRecalculating.set(false);
      }
    });
  }

  public saveConfiguration(): void {
    this.isSavingConfig.set(true);
    const payload = {
      weightFirmographic: this.configFirmographicWeight,
      weightTechnographic: this.configTechnographicWeight,
      weightFirstPartyBehavioral: this.configFirstPartyWeight,
      weightThirdPartySurge: this.configThirdPartyWeight,
      decayHalfLifeDays: this.configDecayHalfLifeDays,
      mqlThresholdScore: this.configMqlThreshold,
      sqlThresholdScore: this.configSqlThreshold
    };

    this.http.put<IntentScoringConfig>('/api/v1/crm/intent-scoring/config', payload).subscribe({
      next: updated => {
        this.config.set(updated);
        this.isSavingConfig.set(false);
        this.loadAllData();
      },
      error: () => {
        this.isSavingConfig.set(false);
      }
    });
  }

  public resetToSeeds(): void {
    this.isLoading.set(true);
    this.http.post('/api/v1/crm/intent-scoring/reset', {}).subscribe({
      next: () => {
        this.loadAllData();
      },
      error: () => {
        this.isLoading.set(false);
      }
    });
  }

  public getGradeBadgeClass(grade: IcpGrade): string {
    switch (grade) {
      case 'GradeA':
        return 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30';
      case 'GradeB':
        return 'bg-sky-500/10 text-sky-400 border border-sky-500/30';
      case 'GradeC':
        return 'bg-amber-500/10 text-amber-400 border border-amber-500/30';
      case 'GradeD':
        return 'bg-rose-500/10 text-rose-400 border border-rose-500/30';
      default:
        return 'bg-slate-700 text-slate-300';
    }
  }

  public getStageBadgeClass(stage: LeadQualificationStage): string {
    switch (stage) {
      case 'SalesQualifiedLead':
        return 'bg-purple-500/10 text-purple-300 border border-purple-500/30';
      case 'MarketingQualifiedLead':
        return 'bg-indigo-500/10 text-indigo-300 border border-indigo-500/30';
      default:
        return 'bg-slate-800 text-slate-400 border border-slate-700';
    }
  }

  public getBuyingStageClass(stage: PredictiveBuyingStage): string {
    switch (stage) {
      case 'ReadyToBuy':
        return 'bg-emerald-600/20 text-emerald-300 border border-emerald-500/40 animate-pulse';
      case 'HighIntent':
        return 'bg-violet-600/20 text-violet-300 border border-violet-500/40';
      case 'Consideration':
        return 'bg-sky-600/20 text-sky-300 border border-sky-500/40';
      case 'Awareness':
        return 'bg-amber-600/20 text-amber-300 border border-amber-500/40';
      default:
        return 'bg-slate-800 text-slate-400 border border-slate-700';
    }
  }

  public getNextActionIcon(action: NextActionRecommendation): string {
    switch (action) {
      case 'ScheduleExecutiveBriefing':
        return '⚡ VIP Briefing';
      case 'RouteToSeniorSdrImmediate':
        return '🔥 Instant SDR Dispatch';
      case 'TriggerAutomatedTechnicalNurture':
        return '📘 Technical Nurture';
      case 'EnrollInHighIntentCadence':
        return '🚀 Outbound Cadence';
      case 'DisqualifyOrSuppress':
        return '🚫 Suppressed (Grade D)';
      default:
        return '📬 Monthly Newsletter';
    }
  }

  private updateLeadInState(updated: LeadIntentRecord): void {
    this.leads.update(current =>
      current.map(l => (l.leadId === updated.leadId ? updated : l)).sort((a, b) => b.compositeScore - a.compositeScore)
    );
  }

  private refreshOverview(): void {
    this.http.get<IntentScoringOverview>('/api/v1/crm/intent-scoring/overview').subscribe({
      next: ov => this.overview.set(ov)
    });
  }

  private resetNewLeadForm(): void {
    this.newContactEmail = '';
    this.newContactName = '';
    this.newCompanyName = '';
    this.newCompanyDomain = '';
    this.newFitScore = 85;
  }
}
