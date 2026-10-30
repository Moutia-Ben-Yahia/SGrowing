import {
  Component,
  OnInit,
  OnDestroy,
  signal,
  computed,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export type DocumentCategoryUi = 'SalesProposal' | 'WholesaleContract' | 'PricingDeck' | 'PitchDeck';
export type DocumentPageTypeUi = 'Cover' | 'ExecutiveSummary' | 'PricingPlan' | 'LegalTerms' | 'TechnicalSpecs';
export type DeviceTypeUi = 'Desktop' | 'Mobile' | 'Tablet';
export type IntentTierUi = 'Hot' | 'Warm' | 'Casual' | 'Cold';

export interface TrackedDocumentPageUi {
  pageNumber: number;
  pageTitle: string;
  pageType: DocumentPageTypeUi;
  summary: string;
}

export interface TrackedDocumentUi {
  id: string;
  tenantId: string;
  title: string;
  totalPages: number;
  ownerEmail: string;
  createdAt: string;
  fileSizeKb: number;
  category: DocumentCategoryUi;
  pages: TrackedDocumentPageUi[];
}

export interface DocumentShareLinkUi {
  id: string;
  tenantId: string;
  documentId: string;
  token: string;
  recipientEmail: string;
  recipientCompany: string;
  allowDownload: boolean;
  expiresAt?: string;
  requiresPasscode: boolean;
  isActive: boolean;
  createdAt: string;
}

export interface PageHeatmapSummaryUi {
  pageNumber: number;
  pageTitle: string;
  pageType: DocumentPageTypeUi;
  averageDwellTimeMs: number;
  totalViews: number;
  dropoffRatePct: number;
  highInterestHeatPct: number;
}

export interface DocumentSessionUi {
  sessionId: string;
  linkId: string;
  documentId: string;
  visitorIp: string;
  userAgent: string;
  deviceType: DeviceTypeUi;
  country: string;
  startedAt: string;
  lastHeartbeatAt: string;
  totalDwellTimeMs: number;
  pagesViewedCount: number;
  intentScore: number;
  intentTier: IntentTierUi;
  companyName?: string;
  recipientEmail?: string;
}

export interface HotLeadAlertUi {
  id: string;
  tenantId: string;
  documentId: string;
  documentTitle: string;
  recipientEmail: string;
  recipientCompany: string;
  triggerReason: string;
  intentScore: number;
  triggeredAt: string;
}

export interface TelemetrySummaryUi {
  totalTrackedLinks: number;
  totalViews: number;
  avgCompletionPct: number;
  activeHotLeads: number;
  recentAlerts: HotLeadAlertUi[];
}

@Component({
  selector: 'app-document-telemetry-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans select-none">
      <!-- Main Content Area -->
      <div class="flex-1 flex flex-col h-full overflow-hidden">
        <!-- Top Navigation Header -->
        <header class="h-16 flex-none border-b border-slate-800/80 bg-slate-900/80 px-6 flex items-center justify-between backdrop-blur z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1.5 transition">
              <span>←</span>
              <span>Back to Inbox</span>
            </a>
            <span class="text-slate-700">|</span>
            <div class="flex items-center gap-2">
              <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-amber-500 to-rose-600 flex items-center justify-center font-bold text-white shadow-lg shadow-rose-950/40 text-sm">
                🔥
              </div>
              <div>
                <h1 class="text-sm font-semibold text-white flex items-center gap-2">
                  Document Telemetry & Heatmap Studio
                  <span class="bg-rose-500/10 text-rose-400 border border-rose-500/20 text-[10px] font-mono px-2 py-0.5 rounded-full">
                    Horizon 33 • Buying Intent Engine
                  </span>
                </h1>
                <p class="text-[11px] text-slate-400">
                  Granular per-page dwell time tracking, dropoff curve analysis, and algorithmic buying-intent scoring.
                </p>
              </div>
            </div>
          </div>

          <div class="flex items-center gap-3">
            <!-- Document Picker Dropdown -->
            <div class="relative">
              <select
                [ngModel]="selectedDocId()"
                (ngModelChange)="onSelectDocument($event)"
                class="bg-slate-800 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:ring-1 focus:ring-rose-500">
                @for (doc of documents(); track doc.id) {
                  <option [value]="doc.id">{{ doc.title }} ({{ doc.totalPages }}p)</option>
                }
              </select>
            </div>

            <!-- Create Tracked Link Button -->
            <button
              (click)="openCreateModal()"
              class="px-3.5 py-1.5 bg-gradient-to-r from-rose-600 to-amber-600 hover:from-rose-500 hover:to-amber-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-rose-950/40 transition flex items-center gap-1.5">
              <span>🔗</span>
              <span>Create Share Link</span>
            </button>

            <!-- Refresh Button -->
            <button
              (click)="refreshAllData()"
              [disabled]="isLoading()"
              class="p-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition"
              title="Refresh Telemetry">
              🔄
            </button>
          </div>
        </header>

        <!-- Scrollable Dashboard Body -->
        <main class="flex-1 overflow-y-auto p-6 space-y-6">
          <!-- KPI Metrics Row -->
          <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <!-- Active Share Links -->
            <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-4 relative overflow-hidden backdrop-blur">
              <div class="flex items-center justify-between text-slate-400 mb-2">
                <span class="text-xs font-medium">Active Tracked Links</span>
                <span class="text-base">🔗</span>
              </div>
              <div class="text-2xl font-bold text-white font-mono">
                {{ summary()?.totalTrackedLinks ?? 3 }}
              </div>
              <div class="mt-2 text-[11px] text-slate-400 flex items-center gap-1">
                <span class="text-emerald-400 font-semibold">+2 links</span> created this week
              </div>
              <div class="absolute -right-2 -bottom-2 w-16 h-16 bg-blue-500/5 rounded-full blur-xl pointer-events-none"></div>
            </div>

            <!-- Total Prospect Views -->
            <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-4 relative overflow-hidden backdrop-blur">
              <div class="flex items-center justify-between text-slate-400 mb-2">
                <span class="text-xs font-medium">Total Prospect Views</span>
                <span class="text-base">👀</span>
              </div>
              <div class="text-2xl font-bold text-cyan-400 font-mono">
                {{ summary()?.totalViews ?? 24 }}
              </div>
              <div class="mt-2 text-[11px] text-slate-400 flex items-center gap-1">
                Across <span>{{ sessions().length }} visitor sessions</span>
              </div>
              <div class="absolute -right-2 -bottom-2 w-16 h-16 bg-cyan-500/5 rounded-full blur-xl pointer-events-none"></div>
            </div>

            <!-- Avg Reading Completion % -->
            <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-4 relative overflow-hidden backdrop-blur">
              <div class="flex items-center justify-between text-slate-400 mb-2">
                <span class="text-xs font-medium">Avg Reading Completion</span>
                <span class="text-base">📖</span>
              </div>
              <div class="text-2xl font-bold text-emerald-400 font-mono">
                {{ summary()?.avgCompletionPct ?? 68.4 }}%
              </div>
              <div class="mt-2 text-[11px] text-slate-400 flex items-center gap-1">
                Deep engagement on pricing & legal
              </div>
              <div class="absolute -right-2 -bottom-2 w-16 h-16 bg-emerald-500/5 rounded-full blur-xl pointer-events-none"></div>
            </div>

            <!-- Active Hot Leads -->
            <div class="bg-gradient-to-br from-rose-950/40 via-slate-900/80 to-slate-900 border border-rose-500/30 rounded-2xl p-4 relative overflow-hidden shadow-lg shadow-rose-950/20 backdrop-blur">
              <div class="flex items-center justify-between text-rose-300 mb-2">
                <span class="text-xs font-medium flex items-center gap-1">
                  <span>🔥</span>
                  <span>Hot Prospect Alerts</span>
                </span>
                <span class="animate-pulse flex h-2 w-2 rounded-full bg-rose-500"></span>
              </div>
              <div class="text-2xl font-bold text-rose-400 font-mono flex items-center gap-2">
                {{ summary()?.activeHotLeads ?? 2 }}
                <span class="text-xs px-2 py-0.5 rounded-full bg-rose-500/20 border border-rose-500/40 text-rose-300 font-sans font-medium">
                  High Buying Intent
                </span>
              </div>
              <div class="mt-2 text-[11px] text-rose-200/80 truncate">
                Latest: {{ (alerts().length > 0) ? alerts()[0].recipientCompany : 'Acme Corporation' }}
              </div>
              <div class="absolute -right-4 -bottom-4 w-20 h-20 bg-rose-500/10 rounded-full blur-xl pointer-events-none"></div>
            </div>
          </section>

          <!-- Document Info Banner -->
          @if (selectedDoc(); as doc) {
            <section class="bg-slate-900/50 border border-slate-800 rounded-2xl p-4 flex flex-wrap items-center justify-between gap-4">
              <div class="flex items-center gap-3">
                <div class="h-10 w-10 rounded-xl bg-slate-800 border border-slate-700 flex items-center justify-center text-lg">
                  📑
                </div>
                <div>
                  <div class="flex items-center gap-2">
                    <h2 class="text-sm font-semibold text-white">{{ doc.title }}</h2>
                    <span class="text-[10px] font-mono px-2 py-0.5 rounded-md bg-slate-800 border border-slate-700 text-slate-300">
                      {{ doc.category }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 flex items-center gap-3 mt-0.5">
                    <span>Total Pages: <strong class="text-slate-200">{{ doc.totalPages }}</strong></span>
                    <span>•</span>
                    <span>Size: <strong class="text-slate-200">{{ doc.fileSizeKb }} KB</strong></span>
                    <span>•</span>
                    <span>Owner: <strong class="text-slate-200">{{ doc.ownerEmail }}</strong></span>
                  </div>
                </div>
              </div>

              <div class="flex items-center gap-2">
                <span class="text-xs text-slate-400">Weight Profile:</span>
                <span class="text-[10px] px-2 py-0.5 rounded bg-amber-500/10 text-amber-300 border border-amber-500/20 font-mono">
                  Pricing (3.5x)
                </span>
                <span class="text-[10px] px-2 py-0.5 rounded bg-violet-500/10 text-violet-300 border border-violet-500/20 font-mono">
                  Legal (2.0x)
                </span>
                <span class="text-[10px] px-2 py-0.5 rounded bg-blue-500/10 text-blue-300 border border-blue-500/20 font-mono">
                  Exec (1.5x)
                </span>
              </div>
            </section>
          }

          <!-- Page Dwell Heatmap Strip -->
          <section class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4">
            <div class="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span>📊</span>
                  <span>Per-Page Dwell Time Heatmap & Retention Funnel</span>
                </h3>
                <p class="text-xs text-slate-400">
                  Visual dwell concentration across {{ pageHeatmapSummaries().length }} pages. Higher heat indicates high prospect attention.
                </p>
              </div>

              <!-- Heatmap Legend -->
              <div class="flex items-center gap-3 text-xs">
                <div class="flex items-center gap-1.5">
                  <span class="h-2.5 w-2.5 rounded-full bg-slate-600"></span>
                  <span class="text-slate-400 text-[11px]">Cover (1.0x)</span>
                </div>
                <div class="flex items-center gap-1.5">
                  <span class="h-2.5 w-2.5 rounded-full bg-blue-500"></span>
                  <span class="text-slate-400 text-[11px]">Exec Summary (1.5x)</span>
                </div>
                <div class="flex items-center gap-1.5">
                  <span class="h-2.5 w-2.5 rounded-full bg-cyan-500"></span>
                  <span class="text-slate-400 text-[11px]">Technical (1.0x)</span>
                </div>
                <div class="flex items-center gap-1.5">
                  <span class="h-2.5 w-2.5 rounded-full bg-amber-500"></span>
                  <span class="text-slate-400 text-[11px]">Pricing Plan (3.5x 🔥)</span>
                </div>
                <div class="flex items-center gap-1.5">
                  <span class="h-2.5 w-2.5 rounded-full bg-violet-500"></span>
                  <span class="text-slate-400 text-[11px]">Legal (2.0x)</span>
                </div>
              </div>
            </div>

            <!-- Heatmap Strip Bars -->
            <div class="grid grid-cols-5 sm:grid-cols-8 md:grid-cols-10 gap-2.5 pt-2">
              @for (page of pageHeatmapSummaries(); track page.pageNumber) {
                <div
                  (click)="selectPageDetail(page)"
                  class="group cursor-pointer rounded-xl border p-2.5 flex flex-col justify-between transition-all duration-200 relative overflow-hidden"
                  [ngClass]="{
                    'border-amber-500/60 bg-amber-950/20 shadow-lg shadow-amber-950/30': page.pageType === 'PricingPlan',
                    'border-violet-500/50 bg-violet-950/20': page.pageType === 'LegalTerms',
                    'border-blue-500/50 bg-blue-950/20': page.pageType === 'ExecutiveSummary',
                    'border-cyan-500/50 bg-cyan-950/20': page.pageType === 'TechnicalSpecs',
                    'border-slate-800 bg-slate-900/40': page.pageType === 'Cover'
                  }">
                  <!-- Top Badge: Page Number & Type -->
                  <div class="flex items-center justify-between text-[11px] font-mono text-slate-400">
                    <span class="font-bold text-white">p.{{ page.pageNumber }}</span>
                    @if (page.pageType === 'PricingPlan') {
                      <span class="text-amber-400">🔥</span>
                    }
                  </div>

                  <!-- Bar Meter Fill -->
                  <div class="my-3 h-24 bg-slate-800/80 rounded-lg flex flex-col justify-end p-1 relative overflow-hidden">
                    <div
                      class="w-full rounded-md transition-all duration-500"
                      [style.height.%]="Math.max(15, page.highInterestHeatPct)"
                      [ngClass]="{
                        'bg-gradient-to-t from-amber-600 to-rose-500': page.pageType === 'PricingPlan',
                        'bg-gradient-to-t from-violet-600 to-purple-400': page.pageType === 'LegalTerms',
                        'bg-gradient-to-t from-blue-600 to-indigo-400': page.pageType === 'ExecutiveSummary',
                        'bg-gradient-to-t from-teal-600 to-cyan-400': page.pageType === 'TechnicalSpecs',
                        'bg-gradient-to-t from-slate-600 to-slate-400': page.pageType === 'Cover'
                      }">
                    </div>

                    <!-- Overlay Dwell Label -->
                    <span class="absolute inset-x-0 bottom-1.5 text-center text-[10px] font-mono font-bold text-white drop-shadow">
                      {{ formatDwellSeconds(page.averageDwellTimeMs) }}
                    </span>
                  </div>

                  <!-- Bottom: Dropoff and Views -->
                  <div class="text-[10px] text-center space-y-0.5">
                    <div class="text-slate-300 font-mono truncate" [title]="page.pageTitle">
                      {{ page.pageTitle }}
                    </div>
                    <div class="text-slate-400 flex items-center justify-between text-[9px] pt-1 border-t border-slate-800/60">
                      <span>{{ page.totalViews }} views</span>
                      @if (page.dropoffRatePct > 0) {
                        <span class="text-rose-400 font-medium font-mono">-{{ page.dropoffRatePct }}%</span>
                      } @else {
                        <span class="text-emerald-400 font-mono">0%</span>
                      }
                    </div>
                  </div>
                </div>
              }
            </div>

            <!-- Page Detail Inspection Card (if selected) -->
            @if (selectedPageDetail(); as p) {
              <div class="mt-4 p-4 rounded-xl bg-slate-800/60 border border-slate-700/80 flex flex-wrap items-center justify-between gap-4">
                <div class="space-y-1">
                  <div class="flex items-center gap-2">
                    <span class="text-xs font-bold text-white">Page {{ p.pageNumber }}: {{ p.pageTitle }}</span>
                    <span class="text-[10px] font-mono px-2 py-0.5 rounded-full bg-slate-700 text-slate-300">
                      {{ p.pageType }}
                    </span>
                    @if (p.averageDwellTimeMs >= 45000 && p.pageType === 'PricingPlan') {
                      <span class="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-rose-500/20 text-rose-300 border border-rose-500/40">
                        🔥 High Pricing Alert Trigger (>45s)
                      </span>
                    }
                  </div>
                  <p class="text-xs text-slate-400">
                    Average dwell time: <strong class="text-slate-200">{{ formatDwellSeconds(p.averageDwellTimeMs) }}</strong> • Total views: <strong class="text-slate-200">{{ p.totalViews }}</strong> • High Interest Heat: <strong class="text-amber-400">{{ p.highInterestHeatPct }}%</strong>
                  </p>
                </div>
                <button
                  (click)="loadSimulatorForPage(p.pageNumber)"
                  class="px-3 py-1.5 bg-slate-700 hover:bg-slate-600 text-white rounded-lg text-xs font-medium transition flex items-center gap-1.5">
                  <span>🕹️</span>
                  <span>Test in Simulator</span>
                </button>
              </div>
            }
          </section>

          <!-- Middle Split Section: Simulator & Hot Alerts Feed -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
            <!-- Left: Interactive Document Viewer Simulator (7 Cols) -->
            <section class="lg:col-span-7 bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>🕹️</span>
                    <span>Interactive Viewer & Telemetry Simulator</span>
                  </h3>
                  <p class="text-xs text-slate-400">
                    Simulate prospect heartbeats ($t_&#123;\\text&#123;ping&#125;&#125; \\approx 3000\\text&#123;ms&#125;$) to inspect real-time buying intent reactions.
                  </p>
                </div>

                <div class="flex items-center gap-2">
                  <button
                    (click)="resetSimulatorSession()"
                    class="px-2.5 py-1 text-[11px] bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 rounded-lg transition">
                    Reset Session
                  </button>
                </div>
              </div>

              <!-- Live Alert Banner (if Hot or Pricing threshold reached) -->
              @if (simulatorAlert(); as alert) {
                <div class="p-3.5 rounded-xl bg-gradient-to-r from-rose-950/80 via-slate-900 to-amber-950/60 border border-rose-500/50 flex items-center justify-between shadow-lg shadow-rose-950/40 animate-pulse">
                  <div class="flex items-center gap-2.5">
                    <span class="text-xl">🚨</span>
                    <div>
                      <div class="text-xs font-bold text-rose-300">Hot Lead Alert Dispatched!</div>
                      <div class="text-[11px] text-slate-300">{{ alert }}</div>
                    </div>
                  </div>
                  <span class="text-[10px] font-mono bg-rose-500 text-white px-2 py-0.5 rounded font-bold uppercase">
                    Live Event
                  </span>
                </div>
              }

              <!-- Document Page Navigator -->
              <div class="bg-slate-950/60 border border-slate-800 rounded-xl p-4 space-y-4">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <button
                      (click)="prevPage()"
                      [disabled]="simulatorPage() <= 1"
                      class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 disabled:opacity-30 rounded-lg text-xs font-bold transition">
                      ◀ Prev
                    </button>
                    <span class="text-xs font-mono text-slate-300">
                      Page <strong class="text-rose-400">{{ simulatorPage() }}</strong> of {{ selectedDoc().totalPages }}
                    </span>
                    <button
                      (click)="nextPage()"
                      [disabled]="simulatorPage() >= selectedDoc().totalPages"
                      class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 disabled:opacity-30 rounded-lg text-xs font-bold transition">
                      Next ▶
                    </button>
                  </div>

                  <!-- Page Type Badge -->
                  <div class="flex items-center gap-2">
                    <span
                      class="text-[11px] font-mono px-2.5 py-1 rounded-lg border font-semibold"
                      [ngClass]="{
                        'bg-amber-500/20 text-amber-300 border-amber-500/30': currentSimulatedPageType() === 'PricingPlan',
                        'bg-violet-500/20 text-violet-300 border-violet-500/30': currentSimulatedPageType() === 'LegalTerms',
                        'bg-blue-500/20 text-blue-300 border-blue-500/30': currentSimulatedPageType() === 'ExecutiveSummary',
                        'bg-cyan-500/20 text-cyan-300 border-cyan-500/30': currentSimulatedPageType() === 'TechnicalSpecs',
                        'bg-slate-800 text-slate-300 border-slate-700': currentSimulatedPageType() === 'Cover'
                      }">
                      {{ currentSimulatedPageType() }}
                      @if (currentSimulatedPageType() === 'PricingPlan') {
                        (Weight: 3.5x)
                      } @else if (currentSimulatedPageType() === 'LegalTerms') {
                        (Weight: 2.0x)
                      } @else if (currentSimulatedPageType() === 'ExecutiveSummary') {
                        (Weight: 1.5x)
                      } @else {
                        (Weight: 1.0x)
                      }
                    </span>
                  </div>
                </div>

                <!-- Page Preview Canvas -->
                <div class="h-44 rounded-lg bg-slate-900 border border-slate-800/80 p-4 flex flex-col justify-between">
                  <div>
                    <h4 class="text-sm font-semibold text-white">
                      {{ currentSimulatedPageTitle() }}
                    </h4>
                    <p class="text-xs text-slate-400 mt-1 line-clamp-3">
                      {{ currentSimulatedPageSummary() }}
                    </p>
                  </div>

                  <!-- Simulated Watermark Overlay -->
                  <div class="flex items-center justify-between text-[10px] text-slate-500 font-mono border-t border-slate-800/60 pt-2">
                    <span>Target: {{ simulatedRecipientCompany() }} ({{ simulatedRecipientEmail() }})</span>
                    <span>Watermark: Protected View • IP 198.51.100.42</span>
                  </div>
                </div>

                <!-- Controls: Dwell Time & Scroll Depth Sliders -->
                <div class="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
                  <!-- Dwell Time Slider -->
                  <div class="space-y-1.5">
                    <div class="flex items-center justify-between text-xs">
                      <span class="text-slate-400">Page Dwell Time:</span>
                      <span class="font-mono font-bold text-amber-400">{{ simulatorDwellSec() }}s</span>
                    </div>
                    <input
                      type="range"
                      min="1"
                      max="120"
                      [ngModel]="simulatorDwellSec()"
                      (ngModelChange)="onDwellSliderChange($event)"
                      class="w-full h-1.5 bg-slate-800 rounded-lg appearance-none cursor-pointer accent-amber-500" />
                    <div class="flex justify-between text-[10px] text-slate-500 font-mono">
                      <span>0s</span>
                      <span>30s (Benchmark)</span>
                      <span>45s (Pricing Threshold)</span>
                      <span>120s</span>
                    </div>
                  </div>

                  <!-- Scroll Depth Slider -->
                  <div class="space-y-1.5">
                    <div class="flex items-center justify-between text-xs">
                      <span class="text-slate-400">Scroll Depth:</span>
                      <span class="font-mono font-bold text-cyan-400">{{ simulatorScrollDepth() }}%</span>
                    </div>
                    <input
                      type="range"
                      min="0"
                      max="100"
                      [ngModel]="simulatorScrollDepth()"
                      (ngModelChange)="onScrollSliderChange($event)"
                      class="w-full h-1.5 bg-slate-800 rounded-lg appearance-none cursor-pointer accent-cyan-500" />
                    <div class="flex justify-between text-[10px] text-slate-500 font-mono">
                      <span>0%</span>
                      <span>50%</span>
                      <span>80% (Completed)</span>
                      <span>100%</span>
                    </div>
                  </div>
                </div>

                <!-- Actions: Trigger Ping & Auto Ping -->
                <div class="flex items-center justify-between pt-2">
                  <div class="flex items-center gap-2">
                    <button
                      (click)="sendSimulatorHeartbeat(3000)"
                      class="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold shadow-md shadow-emerald-950/30 transition flex items-center gap-1.5">
                      <span>💓</span>
                      <span>Send Heartbeat Ping (+3s)</span>
                    </button>
                    <button
                      (click)="sendSimulatorHeartbeat(15000)"
                      class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 rounded-lg text-xs font-medium transition">
                      +15s Reading
                    </button>
                  </div>

                  <div class="flex items-center gap-2">
                    <label class="flex items-center gap-2 text-xs text-slate-400 cursor-pointer">
                      <input
                        type="checkbox"
                        [ngModel]="simulatorAutoPing()"
                        (ngModelChange)="toggleAutoPing($event)"
                        class="rounded bg-slate-800 border-slate-700 text-rose-500 focus:ring-rose-500" />
                      <span>Auto-Ping (every 3s)</span>
                    </label>
                  </div>
                </div>
              </div>

              <!-- Reactive Buying Intent Score Meter -->
              <div class="bg-slate-950/60 border border-slate-800 rounded-xl p-4 space-y-3">
                <div class="flex items-center justify-between">
                  <span class="text-xs font-semibold text-slate-300">Live Predictive Buying Intent Score:</span>
                  <div class="flex items-center gap-2">
                    <span class="text-xs font-mono font-bold" [ngClass]="intentTierColorClass()">
                      {{ simulatedScore() }}/100
                    </span>
                    <span
                      class="text-[10px] font-mono px-2 py-0.5 rounded-full font-bold uppercase"
                      [ngClass]="intentTierBadgeClass()">
                      {{ simulatedTier() }} Lead
                    </span>
                  </div>
                </div>

                <!-- Progress Bar -->
                <div class="h-3 w-full bg-slate-800 rounded-full overflow-hidden p-0.5">
                  <div
                    class="h-full rounded-full transition-all duration-500"
                    [style.width.%]="simulatedScore()"
                    [ngClass]="intentScoreBarGradient()">
                  </div>
                </div>

                <div class="flex justify-between text-[10px] text-slate-500 font-mono">
                  <span>Cold (<25)</span>
                  <span>Casual (25-49)</span>
                  <span>Warm (50-74)</span>
                  <span class="text-rose-400 font-bold">Hot (≥75 🔥)</span>
                </div>
              </div>
            </section>

            <!-- Right: Real-Time Alerts & Prospect Sessions (5 Cols) -->
            <section class="lg:col-span-5 flex flex-col gap-6">
              <!-- Recent Hot Alerts Card -->
              <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-3">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>🔥</span>
                    <span>Recent Hot-Lead Alerts</span>
                  </h3>
                  <span class="text-xs text-rose-400 font-mono font-bold">
                    {{ alerts().length }} Total
                  </span>
                </div>

                <div class="space-y-2.5 max-h-56 overflow-y-auto pr-1">
                  @for (alert of alerts(); track alert.id) {
                    <div class="p-3 rounded-xl bg-slate-800/40 border border-slate-800 hover:border-rose-500/30 transition space-y-1">
                      <div class="flex items-center justify-between text-xs">
                        <strong class="text-white">{{ alert.recipientCompany }}</strong>
                        <span class="text-[10px] font-mono text-rose-400 bg-rose-500/10 px-2 py-0.5 rounded border border-rose-500/20">
                          Score: {{ alert.intentScore }}
                        </span>
                      </div>
                      <div class="text-[11px] text-slate-300">
                        {{ alert.triggerReason }}
                      </div>
                      <div class="text-[10px] text-slate-500 flex items-center justify-between pt-1">
                        <span>{{ alert.recipientEmail }}</span>
                        <span>{{ formatRelativeTime(alert.triggeredAt) }}</span>
                      </div>
                    </div>
                  } @empty {
                    <div class="p-6 text-center text-xs text-slate-500">
                      No hot lead alerts triggered yet.
                    </div>
                  }
                </div>
              </div>

              <!-- Visitor Sessions Feed -->
              <div class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 space-y-3 flex-1">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>👥</span>
                    <span>Prospect Sessions Feed</span>
                  </h3>
                  <span class="text-xs text-slate-400 font-mono">
                    {{ sessions().length }} Sessions
                  </span>
                </div>

                <div class="space-y-2.5 max-h-72 overflow-y-auto pr-1">
                  @for (s of sessions(); track s.sessionId) {
                    <div class="p-3 rounded-xl bg-slate-800/40 border border-slate-800 hover:border-slate-700 transition flex items-center justify-between gap-3">
                      <div class="space-y-0.5 min-w-0">
                        <div class="flex items-center gap-2">
                          <span class="text-sm">
                            {{ getDeviceIcon(s.deviceType) }}
                          </span>
                          <span class="text-xs font-semibold text-white truncate">
                            {{ s.companyName ?? s.visitorIp }}
                          </span>
                          <span class="text-[10px] font-mono text-slate-400">
                            ({{ s.country }})
                          </span>
                        </div>
                        <div class="text-[11px] text-slate-400 flex items-center gap-2">
                          <span>Dwell: <strong class="text-slate-300">{{ formatDwellSeconds(s.totalDwellTimeMs) }}</strong></span>
                          <span>•</span>
                          <span>{{ s.pagesViewedCount }}/{{ selectedDoc().totalPages }} pages</span>
                        </div>
                      </div>

                      <div class="text-right flex flex-col items-end gap-1 flex-shrink-0">
                        <span
                          class="text-[10px] font-mono px-2 py-0.5 rounded-full font-bold uppercase"
                          [ngClass]="{
                            'bg-rose-500/20 text-rose-300 border border-rose-500/40': s.intentTier === 'Hot',
                            'bg-amber-500/20 text-amber-300 border border-amber-500/40': s.intentTier === 'Warm',
                            'bg-blue-500/20 text-blue-300 border border-blue-500/40': s.intentTier === 'Casual',
                            'bg-slate-800 text-slate-400 border border-slate-700': s.intentTier === 'Cold'
                          }">
                          {{ s.intentTier }} ({{ s.intentScore }})
                        </span>
                        <span class="text-[9px] text-slate-500">
                          {{ formatRelativeTime(s.lastHeartbeatAt) }}
                        </span>
                      </div>
                    </div>
                  }
                </div>
              </div>
            </section>
          </div>
        </main>
      </div>

      <!-- Create Share Link Modal -->
      @if (isCreateModalOpen()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm p-4">
          <div class="w-full max-w-md bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-2xl space-y-4">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                <span>🔗</span>
                <span>Generate Tracked Document Link</span>
              </h3>
              <button
                (click)="closeCreateModal()"
                class="text-slate-400 hover:text-white text-sm">
                ✕
              </button>
            </div>

            <!-- Form -->
            <div class="space-y-3.5">
              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Target Document</label>
                <input
                  type="text"
                  [value]="selectedDoc().title"
                  disabled
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-400" />
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Recipient Company *</label>
                <input
                  type="text"
                  [(ngModel)]="newLinkCompany"
                  placeholder="e.g. Wayne Enterprises"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:ring-1 focus:ring-rose-500" />
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Recipient Email *</label>
                <input
                  type="email"
                  [(ngModel)]="newLinkEmail"
                  placeholder="e.g. bruce@waynecorp.com"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:ring-1 focus:ring-rose-500" />
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Expiration</label>
                  <select
                    [(ngModel)]="newLinkExpiryDays"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:ring-1 focus:ring-rose-500">
                    <option [value]="7">7 Days</option>
                    <option [value]="14">14 Days</option>
                    <option [value]="30">30 Days</option>
                    <option [value]="90">90 Days</option>
                  </select>
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Optional Passcode</label>
                  <input
                    type="password"
                    [(ngModel)]="newLinkPasscode"
                    placeholder="Leave empty for public"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:ring-1 focus:ring-rose-500" />
                </div>
              </div>

              <div class="flex items-center gap-2 pt-1">
                <input
                  type="checkbox"
                  id="allowDownloadCheck"
                  [(ngModel)]="newLinkAllowDownload"
                  class="rounded bg-slate-950 border-slate-700 text-rose-500 focus:ring-rose-500" />
                <label for="allowDownloadCheck" class="text-xs text-slate-300 cursor-pointer">
                  Allow PDF Download (Watermarked)
                </label>
              </div>

              <!-- Generated Link Display (if created) -->
              @if (newCreatedToken()) {
                <div class="p-3 rounded-xl bg-emerald-950/30 border border-emerald-500/40 space-y-2">
                  <div class="text-[11px] font-semibold text-emerald-400 flex items-center gap-1">
                    <span>✓</span>
                    <span>Tracked Link Created!</span>
                  </div>
                  <div class="flex items-center gap-2">
                    <input
                      type="text"
                      [value]="getViewerUrl(newCreatedToken()!)"
                      readonly
                      class="flex-1 bg-slate-950 border border-slate-800 rounded px-2.5 py-1 text-[11px] font-mono text-slate-300" />
                    <button
                      (click)="copyLinkToClipboard(getViewerUrl(newCreatedToken()!))"
                      class="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-semibold">
                      Copy
                    </button>
                  </div>
                </div>
              }
            </div>

            <!-- Footer -->
            <div class="flex items-center justify-end gap-2 pt-3 border-t border-slate-800">
              <button
                (click)="closeCreateModal()"
                class="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-medium transition">
                Close
              </button>
              @if (!newCreatedToken()) {
                <button
                  (click)="submitCreateShareLink()"
                  [disabled]="!newLinkEmail || !newLinkCompany || isSubmittingLink()"
                  class="px-4 py-1.5 bg-rose-600 hover:bg-rose-500 disabled:opacity-40 text-white rounded-lg text-xs font-semibold shadow-lg shadow-rose-950/30 transition">
                  {{ isSubmittingLink() ? 'Generating...' : 'Generate Token' }}
                </button>
              }
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class DocumentTelemetryStudioComponent implements OnInit, OnDestroy {
  private http = inject(HttpClient);
  public Math = Math;

  // State Signals
  public isLoading = signal<boolean>(false);
  public documents = signal<TrackedDocumentUi[]>([]);
  public selectedDocId = signal<string>('');
  public selectedDoc = computed(() => {
    const id = this.selectedDocId();
    return this.documents().find(d => d.id === id) ?? this.documents()[0] ?? null;
  });

  public pageHeatmapSummaries = signal<PageHeatmapSummaryUi[]>([]);
  public sessions = signal<DocumentSessionUi[]>([]);
  public alerts = signal<HotLeadAlertUi[]>([]);
  public summary = signal<TelemetrySummaryUi | null>(null);

  // Inspector & Simulator Signals
  public selectedPageDetail = signal<PageHeatmapSummaryUi | null>(null);
  public simulatorPage = signal<number>(1);
  public simulatorDwellSec = signal<number>(15);
  public simulatorScrollDepth = signal<number>(100);
  public simulatorAutoPing = signal<boolean>(false);
  public simulatorAlert = signal<string | null>(null);
  private autoPingInterval: any = null;

  // Simulator tracking
  public simulatedSessionId = signal<string>(crypto.randomUUID());
  public simulatedRecipientCompany = signal<string>('Acme Corporation');
  public simulatedRecipientEmail = signal<string>('cfo@acmecorp.com');
  public simulatedPageViews = signal<Record<number, { dwellMs: number; scrollDepth: number }>>({});

  // Real-time Buying Intent computation
  public simulatedScore = computed(() => {
    const views = this.simulatedPageViews();
    const doc = this.selectedDoc();
    if (!doc) return 0;

    let score = 0;
    const benchmarkTimeMs = 30000;

    for (const [pageNumStr, data] of Object.entries(views)) {
      const pageNum = parseInt(pageNumStr, 10);
      const pageInfo = doc.pages.find(p => p.pageNumber === pageNum);
      const pageType = pageInfo?.pageType ?? 'TechnicalSpecs';

      let weight = 1.0;
      if (pageType === 'PricingPlan') weight = 3.5;
      else if (pageType === 'LegalTerms') weight = 2.0;
      else if (pageType === 'ExecutiveSummary') weight = 1.5;

      const dwellRatio = Math.min(1.0, data.dwellMs / benchmarkTimeMs);
      const scrollRatio = Math.min(1.0, data.scrollDepth / 100);
      const contribution = weight * dwellRatio * scrollRatio * 15.0;

      score += contribution;
    }

    return Math.min(100, Math.round(score * 10) / 10);
  });

  public simulatedTier = computed<IntentTierUi>(() => {
    const s = this.simulatedScore();
    if (s >= 75) return 'Hot';
    if (s >= 50) return 'Warm';
    if (s >= 25) return 'Casual';
    return 'Cold';
  });

  // Modal Signals
  public isCreateModalOpen = signal<boolean>(false);
  public newLinkCompany = '';
  public newLinkEmail = '';
  public newLinkPasscode = '';
  public newLinkExpiryDays = 14;
  public newLinkAllowDownload = true;
  public isSubmittingLink = signal<boolean>(false);
  public newCreatedToken = signal<string | null>(null);

  ngOnInit(): void {
    this.loadInitialData();
  }

  ngOnDestroy(): void {
    if (this.autoPingInterval) {
      clearInterval(this.autoPingInterval);
    }
  }

  public loadInitialData(): void {
    this.isLoading.set(true);
    // Fetch from backend API with fallback mock
    this.http.get<TrackedDocumentUi[]>('/api/v1/documents/telemetry/documents').subscribe({
      next: (docs) => {
        if (docs && docs.length > 0) {
          this.documents.set(docs);
          this.selectedDocId.set(docs[0].id);
          this.fetchDocumentTelemetry(docs[0].id);
        } else {
          this.initFallbackData();
        }
      },
      error: () => {
        this.initFallbackData();
      }
    });

    this.http.get<TelemetrySummaryUi>('/api/v1/documents/telemetry/summary').subscribe({
      next: (s) => this.summary.set(s),
      error: () => {}
    });

    this.http.get<HotLeadAlertUi[]>('/api/v1/documents/telemetry/alerts').subscribe({
      next: (a) => this.alerts.set(a),
      error: () => {}
    });
  }

  public onSelectDocument(docId: string): void {
    this.selectedDocId.set(docId);
    this.simulatorPage.set(1);
    this.resetSimulatorSession();
    this.fetchDocumentTelemetry(docId);
  }

  public fetchDocumentTelemetry(docId: string): void {
    this.http.get<any>(`/api/v1/documents/telemetry/documents/${docId}/heatmap`).subscribe({
      next: (res) => {
        if (res && res.pages) {
          this.pageHeatmapSummaries.set(res.pages);
        }
      },
      error: () => {
        this.generateFallbackHeatmap(this.selectedDoc()!);
      }
    });

    this.http.get<DocumentSessionUi[]>(`/api/v1/documents/telemetry/documents/${docId}/sessions`).subscribe({
      next: (sessions) => {
        if (sessions) {
          this.sessions.set(sessions);
        }
      },
      error: () => {}
    });
  }

  public refreshAllData(): void {
    const doc = this.selectedDoc();
    if (doc) {
      this.fetchDocumentTelemetry(doc.id);
    }
  }

  public selectPageDetail(page: PageHeatmapSummaryUi): void {
    this.selectedPageDetail.set(page);
  }

  public loadSimulatorForPage(pageNumber: number): void {
    this.simulatorPage.set(pageNumber);
    const existing = this.simulatedPageViews()[pageNumber];
    if (existing) {
      this.simulatorDwellSec.set(Math.round(existing.dwellMs / 1000));
      this.simulatorScrollDepth.set(Math.round(existing.scrollDepth));
    } else {
      this.simulatorDwellSec.set(15);
      this.simulatorScrollDepth.set(100);
    }
  }

  public prevPage(): void {
    if (this.simulatorPage() > 1) {
      this.loadSimulatorForPage(this.simulatorPage() - 1);
    }
  }

  public nextPage(): void {
    const total = this.selectedDoc()?.totalPages ?? 10;
    if (this.simulatorPage() < total) {
      this.loadSimulatorForPage(this.simulatorPage() + 1);
    }
  }

  public onDwellSliderChange(val: number): void {
    this.simulatorDwellSec.set(val);
    this.updateCurrentPageTelemetry(val * 1000, this.simulatorScrollDepth());
  }

  public onScrollSliderChange(val: number): void {
    this.simulatorScrollDepth.set(val);
    this.updateCurrentPageTelemetry(this.simulatorDwellSec() * 1000, val);
  }

  public sendSimulatorHeartbeat(incrementMs: number): void {
    const current = this.simulatorDwellSec();
    const newSec = current + Math.round(incrementMs / 1000);
    this.simulatorDwellSec.set(newSec);
    this.updateCurrentPageTelemetry(newSec * 1000, this.simulatorScrollDepth());

    // Send to backend if available
    const pageNum = this.simulatorPage();
    const token = 'doc_view_acme_corp_ent_deal_001a';
    this.http.post<any>('/api/v1/documents/telemetry/sessions/heartbeat', {
      token: token,
      sessionId: this.simulatedSessionId(),
      pageNumber: pageNum,
      dwellTimeIncrementMs: incrementMs,
      scrollDepthPct: this.simulatorScrollDepth(),
      deviceType: 'Desktop',
      country: 'US'
    }).subscribe({
      next: (res) => {
        if (res.alertTriggered) {
          this.simulatorAlert.set(res.triggerReason);
        }
      },
      error: () => {}
    });
  }

  public toggleAutoPing(enabled: boolean): void {
    this.simulatorAutoPing.set(enabled);
    if (enabled) {
      this.autoPingInterval = setInterval(() => {
        this.sendSimulatorHeartbeat(3000);
      }, 3000);
    } else {
      if (this.autoPingInterval) {
        clearInterval(this.autoPingInterval);
        this.autoPingInterval = null;
      }
    }
  }

  public resetSimulatorSession(): void {
    this.simulatedSessionId.set(crypto.randomUUID());
    this.simulatedPageViews.set({});
    this.simulatorDwellSec.set(15);
    this.simulatorScrollDepth.set(100);
    this.simulatorAlert.set(null);
    if (this.simulatorAutoPing()) {
      this.toggleAutoPing(false);
    }
  }

  private updateCurrentPageTelemetry(dwellMs: number, scrollDepth: number): void {
    const pageNum = this.simulatorPage();
    const current = { ...this.simulatedPageViews() };
    current[pageNum] = { dwellMs, scrollDepth };
    this.simulatedPageViews.set(current);

    // Check Trigger Conditions
    const pageType = this.currentSimulatedPageType();
    const score = this.simulatedScore();

    if (pageType === 'PricingPlan' && dwellMs >= 45000 && score >= 75) {
      this.simulatorAlert.set(
        `High Intent Score (${score}/100) and Deep Pricing Engagement (${Math.round(dwellMs / 1000)}s on Page ${pageNum})`
      );
    } else if (pageType === 'PricingPlan' && dwellMs >= 45000) {
      this.simulatorAlert.set(
        `Deep Pricing Engagement: Prospect spent ${Math.round(dwellMs / 1000)}s on Pricing Plan (Page ${pageNum})`
      );
    } else if (score >= 75) {
      this.simulatorAlert.set(
        `High Predictive Buying Intent (${score}/100) exceeded Hot Tier threshold`
      );
    }
  }

  // Current Simulator Page details
  public currentSimulatedPageType(): DocumentPageTypeUi {
    const doc = this.selectedDoc();
    const p = doc?.pages.find(pg => pg.pageNumber === this.simulatorPage());
    return p?.pageType ?? 'TechnicalSpecs';
  }

  public currentSimulatedPageTitle(): string {
    const doc = this.selectedDoc();
    const p = doc?.pages.find(pg => pg.pageNumber === this.simulatorPage());
    return p?.pageTitle ?? `Page ${this.simulatorPage()}`;
  }

  public currentSimulatedPageSummary(): string {
    const doc = this.selectedDoc();
    const p = doc?.pages.find(pg => pg.pageNumber === this.simulatorPage());
    return p?.summary ?? 'Standard document clause and specification text.';
  }

  // Modal Methods
  public openCreateModal(): void {
    this.newLinkCompany = '';
    this.newLinkEmail = '';
    this.newLinkPasscode = '';
    this.newCreatedToken.set(null);
    this.isCreateModalOpen.set(true);
  }

  public closeCreateModal(): void {
    this.isCreateModalOpen.set(false);
  }

  public submitCreateShareLink(): void {
    const doc = this.selectedDoc();
    if (!doc) return;

    this.isSubmittingLink.set(true);
    const body = {
      documentId: doc.id,
      recipientEmail: this.newLinkEmail,
      recipientCompany: this.newLinkCompany,
      allowDownload: this.newLinkAllowDownload,
      passcode: this.newLinkPasscode ? this.newLinkPasscode : null,
      expiresAt: new Date(Date.now() + this.newLinkExpiryDays * 86400000).toISOString()
    };

    this.http.post<any>('/api/v1/documents/telemetry/links', body).subscribe({
      next: (res) => {
        this.isSubmittingLink.set(false);
        this.newCreatedToken.set(res.token);
      },
      error: () => {
        // Fallback token generation for demo
        this.isSubmittingLink.set(false);
        const randChars = Array.from({ length: 24 }, () =>
          'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-'.charAt(Math.floor(Math.random() * 64))
        ).join('');
        this.newCreatedToken.set(`doc_view_${randChars}`);
      }
    });
  }

  public getViewerUrl(token: string): string {
    return `${window.location.origin}/proposals/view/${token}`;
  }

  public copyLinkToClipboard(url: string): void {
    navigator.clipboard?.writeText(url);
    alert('Tracked Share Link copied to clipboard!');
  }

  // Formatting & UI Helpers
  public formatDwellSeconds(ms: number): string {
    const totalSec = Math.round(ms / 1000);
    if (totalSec >= 60) {
      const min = Math.floor(totalSec / 60);
      const sec = totalSec % 60;
      return `${min}m ${sec.toString().padStart(2, '0')}s`;
    }
    return `${totalSec}s`;
  }

  public formatRelativeTime(dateStr: string): string {
    try {
      const diffMs = Date.now() - new Date(dateStr).getTime();
      const mins = Math.floor(diffMs / 60000);
      if (mins < 1) return 'Just now';
      if (mins < 60) return `${mins}m ago`;
      const hrs = Math.floor(mins / 60);
      if (hrs < 24) return `${hrs}h ago`;
      return `${Math.floor(hrs / 24)}d ago`;
    } catch {
      return 'Recently';
    }
  }

  public getDeviceIcon(device: DeviceTypeUi): string {
    if (device === 'Mobile') return '📱';
    if (device === 'Tablet') return '📟';
    return '💻';
  }

  public intentTierColorClass(): string {
    const tier = this.simulatedTier();
    if (tier === 'Hot') return 'text-rose-400';
    if (tier === 'Warm') return 'text-amber-400';
    if (tier === 'Casual') return 'text-blue-400';
    return 'text-slate-400';
  }

  public intentTierBadgeClass(): string {
    const tier = this.simulatedTier();
    if (tier === 'Hot') return 'bg-rose-500/20 text-rose-300 border border-rose-500/40';
    if (tier === 'Warm') return 'bg-amber-500/20 text-amber-300 border border-amber-500/40';
    if (tier === 'Casual') return 'bg-blue-500/20 text-blue-300 border border-blue-500/40';
    return 'bg-slate-800 text-slate-400 border border-slate-700';
  }

  public intentScoreBarGradient(): string {
    const tier = this.simulatedTier();
    if (tier === 'Hot') return 'bg-gradient-to-r from-amber-500 via-rose-500 to-rose-600 shadow-md shadow-rose-500/50';
    if (tier === 'Warm') return 'bg-gradient-to-r from-cyan-500 to-amber-500';
    if (tier === 'Casual') return 'bg-gradient-to-r from-blue-600 to-cyan-500';
    return 'bg-gradient-to-r from-slate-700 to-slate-500';
  }

  // Fallback Data Initialization
  private initFallbackData(): void {
    const mockDoc1: TrackedDocumentUi = {
      id: 'a1111111-1111-1111-1111-111111111111',
      tenantId: '11111111-2222-3333-4444-555555555555',
      title: 'SGrowing Enterprise SaaS Agreement - 10 Pages',
      totalPages: 10,
      ownerEmail: 'sales-director@sgrowing.io',
      createdAt: new Date().toISOString(),
      fileSizeKb: 2450,
      category: 'SalesProposal',
      pages: [
        { pageNumber: 1, pageTitle: 'Enterprise Agreement Cover & Parties', pageType: 'Cover', summary: 'Contracting parties identification and effective dates' },
        { pageNumber: 2, pageTitle: 'Executive Summary & Platform Scope', pageType: 'ExecutiveSummary', summary: 'Unified Omnichannel CRM & Voice Agent subscription tiers' },
        { pageNumber: 3, pageTitle: 'Platform Architecture & High-Availability SLA', pageType: 'TechnicalSpecs', summary: '99.99% uptime guarantee, multi-region failover' },
        { pageNumber: 4, pageTitle: 'Data Security, Encryption & ISO 27001', pageType: 'TechnicalSpecs', summary: 'End-to-end encryption, SOC2 Type II controls, zero-trust RBAC' },
        { pageNumber: 5, pageTitle: 'Tiered Enterprise Licensing Matrix', pageType: 'PricingPlan', summary: 'Seat tiers, consumption credits, annual commitment discount' },
        { pageNumber: 6, pageTitle: 'Implementation, Migration & Dedicated CS Scope', pageType: 'PricingPlan', summary: 'Turnkey onboarding package, custom CRM sync' },
        { pageNumber: 7, pageTitle: 'Master Services Agreement (MSA) Terms', pageType: 'LegalTerms', summary: 'Licensing rights, IP ownership, confidentiality covenants' },
        { pageNumber: 8, pageTitle: 'Data Processing Addendum (DPA) & GDPR', pageType: 'LegalTerms', summary: 'Standard Contractual Clauses (SCCs), sub-processor disclosures' },
        { pageNumber: 9, pageTitle: 'Limitation of Liability & Indemnification', pageType: 'LegalTerms', summary: 'Mutual indemnity clauses, insurance thresholds' },
        { pageNumber: 10, pageTitle: 'Authorized Execution & Electronic Signatures', pageType: 'LegalTerms', summary: 'ESIGN & eIDAS digital signature execution blocks' }
      ]
    };

    const mockDoc2: TrackedDocumentUi = {
      id: 'b2222222-2222-2222-2222-222222222222',
      tenantId: '11111111-2222-3333-4444-555555555555',
      title: 'Wholesale Fragrance Master Supply Proposal - 8 Pages',
      totalPages: 8,
      ownerEmail: 'contracts@sgrowing.io',
      createdAt: new Date().toISOString(),
      fileSizeKb: 1820,
      category: 'WholesaleContract',
      pages: [
        { pageNumber: 1, pageTitle: 'Wholesale Master Supply Agreement Cover', pageType: 'Cover', summary: 'Primary supplier identification & distribution schedule' },
        { pageNumber: 2, pageTitle: 'Volume Commitments & Territory Allocation', pageType: 'ExecutiveSummary', summary: 'Annual minimum order volume & exclusive geographical rights' },
        { pageNumber: 3, pageTitle: 'Formula Specifications & Quality Controls', pageType: 'TechnicalSpecs', summary: 'IFRA compliance, raw batch testing' },
        { pageNumber: 4, pageTitle: 'Tiered Wholesale Unit Pricing & Discounts', pageType: 'PricingPlan', summary: 'Per-unit cost schedule across order volume tiers' },
        { pageNumber: 5, pageTitle: 'Payment Schedule & Letter of Credit Terms', pageType: 'PricingPlan', summary: 'Net 30/60 terms, revolving standby letter of credit' },
        { pageNumber: 6, pageTitle: 'Incoterms 2020, Shipping & Risk Transfer', pageType: 'LegalTerms', summary: 'DAP/FOB freight terms, customs clearance' },
        { pageNumber: 7, pageTitle: 'Inspection, Product Warranties & Defect Claims', pageType: 'LegalTerms', summary: 'Acceptance window, replacement protocols' },
        { pageNumber: 8, pageTitle: 'Execution & Counterparts Execution', pageType: 'LegalTerms', summary: 'Authorized signatory authorization blocks' }
      ]
    };

    this.documents.set([mockDoc1, mockDoc2]);
    this.selectedDocId.set(mockDoc1.id);
    this.generateFallbackHeatmap(mockDoc1);

    this.sessions.set([
      {
        sessionId: 'd1111111-1111-1111-1111-111111111111',
        linkId: 'c1111111-1111-1111-1111-111111111111',
        documentId: mockDoc1.id,
        visitorIp: '198.51.100.42',
        userAgent: 'Chrome 130 on Windows',
        deviceType: 'Desktop',
        country: 'US',
        startedAt: new Date(Date.now() - 10800000).toISOString(),
        lastHeartbeatAt: new Date(Date.now() - 9600000).toISOString(),
        totalDwellTimeMs: 294000,
        pagesViewedCount: 10,
        intentScore: 88.5,
        intentTier: 'Hot',
        companyName: 'Acme Corporation',
        recipientEmail: 'cfo@acmecorp.com'
      },
      {
        sessionId: 'd2222222-2222-2222-2222-222222222222',
        linkId: 'c2222222-2222-2222-2222-222222222222',
        documentId: mockDoc1.id,
        visitorIp: '203.0.113.18',
        userAgent: 'Safari 18 on MacOS',
        deviceType: 'Desktop',
        country: 'GB',
        startedAt: new Date(Date.now() - 18000000).toISOString(),
        lastHeartbeatAt: new Date(Date.now() - 17280000).toISOString(),
        totalDwellTimeMs: 90000,
        pagesViewedCount: 5,
        intentScore: 58.0,
        intentTier: 'Warm',
        companyName: 'Globex Retail',
        recipientEmail: 'procurement@globex.com'
      },
      {
        sessionId: 'd3333333-3333-3333-3333-333333333333',
        linkId: 'c3333333-3333-3333-3333-333333333333',
        documentId: mockDoc2.id,
        visitorIp: '198.51.100.99',
        userAgent: 'Mobile Safari on iPhone',
        deviceType: 'Mobile',
        country: 'DE',
        startedAt: new Date(Date.now() - 86400000).toISOString(),
        lastHeartbeatAt: new Date(Date.now() - 86220000).toISOString(),
        totalDwellTimeMs: 20000,
        pagesViewedCount: 2,
        intentScore: 28.5,
        intentTier: 'Casual',
        companyName: 'Initech Holdings',
        recipientEmail: 'buyer@initech.com'
      }
    ]);

    this.alerts.set([
      {
        id: 'e1111111-1111-1111-1111-111111111111',
        tenantId: '11111111-2222-3333-4444-555555555555',
        documentId: mockDoc1.id,
        documentTitle: mockDoc1.title,
        recipientEmail: 'cfo@acmecorp.com',
        recipientCompany: 'Acme Corporation',
        triggerReason: 'Deep Pricing Engagement: Prospect spent 52s on Pricing Plan (Page 5)',
        intentScore: 88.5,
        triggeredAt: new Date(Date.now() - 9600000).toISOString()
      }
    ]);

    this.summary.set({
      totalTrackedLinks: 3,
      totalViews: 24,
      avgCompletionPct: 68.4,
      activeHotLeads: 2,
      recentAlerts: this.alerts()
    });

    this.isLoading.set(false);
  }

  private generateFallbackHeatmap(doc: TrackedDocumentUi): void {
    const list: PageHeatmapSummaryUi[] = [];
    const simulatedViews = [24, 22, 20, 19, 18, 17, 15, 14, 13, 12];
    const simulatedDwells = [12000, 32000, 22000, 24000, 56000, 42000, 36000, 26000, 22000, 20000];

    for (let i = 1; i <= doc.totalPages; i++) {
      const pageInfo = doc.pages.find(p => p.pageNumber === i);
      const views = simulatedViews[i - 1] ?? Math.max(3, 20 - i * 2);
      const dwell = simulatedDwells[i - 1] ?? 20000;
      const prevViews = i === 1 ? views : (simulatedViews[i - 2] ?? views);
      const dropoff = i === 1 ? 0 : Math.max(0, Math.round(((prevViews - views) / prevViews) * 1000) / 10);
      const heat = Math.min(100, Math.round((dwell / 30000) * 100));

      list.push({
        pageNumber: i,
        pageTitle: pageInfo?.pageTitle ?? `Page ${i}`,
        pageType: pageInfo?.pageType ?? 'TechnicalSpecs',
        averageDwellTimeMs: dwell,
        totalViews: views,
        dropoffRatePct: dropoff,
        highInterestHeatPct: heat
      });
    }

    this.pageHeatmapSummaries.set(list);
  }
}
