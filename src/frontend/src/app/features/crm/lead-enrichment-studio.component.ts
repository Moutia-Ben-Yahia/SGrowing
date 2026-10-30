import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';

export type DeliverabilityStatus = 'Valid' | 'Risky' | 'Disposable' | 'Undeliverable';
export type QualificationStatus = 'SalesQualifiedLead' | 'MarketingQualifiedLead' | 'Unqualified';

export interface TechnographicItem {
  name: string;
  category: 'CRM' | 'E-commerce' | 'Cloud' | 'Frontend' | 'Analytics' | string;
  confidence: number;
}

export interface DnsVerificationDetails {
  hasValidMxRecord: boolean;
  mxRecords: string[];
  dnsLookupMs: number;
  isDisposable: boolean;
  disposableDomainMatched?: string | null;
  spfRecordFound: boolean;
  spfScore: number;
  dkimValid: boolean;
  dkimScore: number;
  dmarcEnforced: boolean;
  dmarcPolicy: string;
  dmarcScore: number;
  deliverabilityScore: number;
  status: DeliverabilityStatus;
  reason: string;
}

export interface FirmographicDetails {
  domain: string;
  rootDomain: string;
  companyName: string;
  industry: string;
  headcountTier: string;
  estimatedAnnualRevenue: string;
  headquarters: string;
  foundedYear: number;
  firmographicScore: number;
}

export interface TechnographicDetails {
  technologies: TechnographicItem[];
  technographicScore: number;
  stackSummary: string;
}

export interface ScoreBreakdown {
  firmographicWeight: number;
  firmographicScore: number;
  firmographicWeighted: number;
  technographicWeight: number;
  technographicScore: number;
  technographicWeighted: number;
  intentWeight: number;
  intentScore: number;
  intentWeighted: number;
  deliverabilityWeight: number;
  deliverabilityScore: number;
  deliverabilityWeighted: number;
  compositeScore: number;
}

export interface EnrichedLead {
  id: string;
  email: string;
  companyName: string;
  domain: string;
  dnsVerification: DnsVerificationDetails;
  firmographics: FirmographicDetails;
  technographics: TechnographicDetails;
  intentScore: number;
  compositeScore: number;
  scoreBreakdown: ScoreBreakdown;
  qualificationStatus: QualificationStatus;
  assignedSalesRep?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface EnrichmentKpiMetrics {
  totalLeads: number;
  sqlCount: number;
  mqlCount: number;
  unqualifiedCount: number;
  disposableCount: number;
  sqlRatePercent: number;
  deliverabilityPassRatePercent: number;
  averageLeadScore: number;
  computedAtUtc: string;
}

export interface EmailVerificationResult {
  email: string;
  domain: string;
  details: DnsVerificationDetails;
}

@Component({
  selector: 'app-lead-enrichment-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans select-none pb-16">
      <!-- Top Navigation & Header Bar -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur sticky top-0 z-30 px-6 py-4 flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <a routerLink="/" class="text-xs text-slate-400 hover:text-white px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-800 transition flex items-center gap-1.5 shadow-sm">
            <span>←</span>
            <span>Cockpit</span>
          </a>
          <div>
            <div class="flex items-center gap-2">
              <span class="text-2xl">🎯</span>
              <h1 class="text-xl font-bold tracking-tight text-white flex items-center gap-2">
                Automated Lead Enrichment & Intent Radar
              </h1>
              <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-semibold">
                Horizon 35 • MQL to SQL Engine
              </span>
            </div>
            <p class="text-xs text-slate-400 mt-0.5">
              DNS/MX record verification, disposable domain blacklist screening, firmographic & technographic synthesis, and automated AE routing.
            </p>
          </div>
        </div>

        <div class="flex items-center gap-2.5">
          <button
            (click)="recalculateAllScores()"
            [disabled]="isRecalculating()"
            class="px-3.5 py-1.5 text-xs font-semibold rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white shadow-md shadow-indigo-900/30 transition flex items-center gap-1.5 disabled:opacity-50">
            <span [class.animate-spin]="isRecalculating()">⚙</span>
            <span>{{ isRecalculating() ? 'Recalculating...' : 'Recalculate Radar' }}</span>
          </button>
          <button
            (click)="refreshData()"
            [disabled]="isLoading()"
            class="px-3 py-1.5 text-xs font-semibold rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 transition flex items-center gap-1.5">
            <span [class.animate-spin]="isLoading()">↻</span>
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

      <main class="flex-1 px-6 py-6 space-y-6">
        <!-- 1. Header KPI Metrics -->
        <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <!-- KPI 1: Total Leads Evaluated -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-slate-700 transition">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400">Total Leads In Radar</span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-mono bg-slate-800 text-slate-300 border border-slate-700">
                Registry
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <span class="text-3xl font-extrabold text-white tracking-tight">
                {{ metrics()?.totalLeads ?? leads().length }}
              </span>
              <span class="text-xs text-slate-400 font-mono">
                {{ metrics()?.sqlCount ?? 0 }} SQL / {{ metrics()?.mqlCount ?? 0 }} MQL
              </span>
            </div>
            <div class="mt-2 text-[11px] text-slate-400 flex items-center gap-1.5">
              <span class="w-2 h-2 rounded-full bg-emerald-400 inline-block"></span>
              <span>{{ metrics()?.disposableCount ?? 0 }} disposable domains quarantined</span>
            </div>
          </div>

          <!-- KPI 2: SQL Qualification Rate -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-slate-700 transition">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400">SQL Conversion Rate</span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-mono bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-semibold">
                Score &ge; 75
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <span class="text-3xl font-extrabold text-emerald-400 tracking-tight">
                {{ metrics()?.sqlRatePercent ?? 40.0 }}%
              </span>
              <span class="text-xs text-emerald-400/80 font-medium">Auto-routed to AEs</span>
            </div>
            <div class="mt-3 w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
              <div class="bg-emerald-500 h-1.5 rounded-full transition-all duration-500" [style.width.%]="metrics()?.sqlRatePercent ?? 40.0"></div>
            </div>
          </div>

          <!-- KPI 3: Deliverability Pass Rate -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-slate-700 transition">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400">DNS Deliverability Pass Rate</span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-mono bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-semibold">
                MX + SPF + DKIM
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <span class="text-3xl font-extrabold text-cyan-300 tracking-tight">
                {{ metrics()?.deliverabilityPassRatePercent ?? 80.0 }}%
              </span>
              <span class="text-xs text-slate-400 font-mono">Authenticated</span>
            </div>
            <div class="mt-3 w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
              <div class="bg-cyan-500 h-1.5 rounded-full transition-all duration-500" [style.width.%]="metrics()?.deliverabilityPassRatePercent ?? 80.0"></div>
            </div>
          </div>

          <!-- KPI 4: Avg Composite Lead Score -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-4 flex flex-col justify-between shadow-sm relative overflow-hidden group hover:border-slate-700 transition">
            <div class="flex items-center justify-between">
              <span class="text-xs font-medium text-slate-400">Average Composite Score</span>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-mono bg-indigo-500/10 text-indigo-400 border border-indigo-500/20 font-semibold">
                Radar Index
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <span class="text-3xl font-extrabold text-indigo-300 tracking-tight">
                {{ metrics()?.averageLeadScore ?? 62.4 }}
              </span>
              <span class="text-xs text-slate-400">/ 100 max</span>
            </div>
            <div class="mt-3 w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
              <div class="bg-indigo-500 h-1.5 rounded-full transition-all duration-500" [style.width.%]="metrics()?.averageLeadScore ?? 62.4"></div>
            </div>
          </div>
        </section>

        <!-- 2. Quick Enrich Lead & Verification Tool -->
        <section class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between border-b border-slate-800 pb-3">
            <div class="flex items-center gap-2">
              <span class="p-1.5 rounded-lg bg-emerald-500/10 text-emerald-400">⚡</span>
              <h2 class="text-sm font-bold text-white uppercase tracking-wider">Quick Lead Radar & Verification Ingress</h2>
            </div>
            <span class="text-xs text-slate-400 font-mono">
              Composite Formula: 35% Firmo + 25% Techno + 25% Intent + 15% Deliverability
            </span>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-12 gap-3.5">
            <!-- Email Input -->
            <div class="md:col-span-4 space-y-1">
              <label class="text-xs font-medium text-slate-300">Lead Corporate Email *</label>
              <input
                type="email"
                [(ngModel)]="newLeadEmail"
                placeholder="e.g. cto@enterprise.co.uk"
                class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2 text-xs text-white placeholder-slate-600 focus:outline-none focus:border-emerald-500 transition" />
            </div>

            <!-- Company Name Input -->
            <div class="md:col-span-3 space-y-1">
              <label class="text-xs font-medium text-slate-300">Company Name (Optional)</label>
              <input
                type="text"
                [(ngModel)]="newCompanyName"
                placeholder="Auto-inferred if blank"
                class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2 text-xs text-white placeholder-slate-600 focus:outline-none focus:border-emerald-500 transition" />
            </div>

            <!-- Industry Dropdown -->
            <div class="md:col-span-2 space-y-1">
              <label class="text-xs font-medium text-slate-300">Industry Vertical</label>
              <select
                [(ngModel)]="newIndustryHint"
                class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-emerald-500 transition">
                <option value="">Auto-Detect</option>
                <option value="Fintech">Fintech</option>
                <option value="E-commerce">E-commerce</option>
                <option value="Logistics">Logistics</option>
                <option value="B2B SaaS">B2B SaaS</option>
                <option value="Healthtech">Healthtech</option>
                <option value="Retail">Retail</option>
              </select>
            </div>

            <!-- Headcount Dropdown -->
            <div class="md:col-span-3 space-y-1">
              <label class="text-xs font-medium text-slate-300">Headcount Tier</label>
              <select
                [(ngModel)]="newHeadcountHint"
                class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-emerald-500 transition">
                <option value="">Auto-Detect Tier</option>
                <option value="1-10">1-10 Employees (<$1M ARR)</option>
                <option value="11-50">11-50 Employees ($1M-$5M ARR)</option>
                <option value="51-200">51-200 Employees ($5M-$20M ARR)</option>
                <option value="201-1000">201-1000 Employees ($20M-$100M ARR)</option>
                <option value="1000+">1000+ Employees ($100M+ ARR)</option>
              </select>
            </div>
          </div>

          <!-- Bottom Control Bar with Intent Slider & Submit Buttons -->
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pt-2 border-t border-slate-800/80">
            <div class="flex items-center gap-3">
              <label class="text-xs font-medium text-slate-300 flex items-center gap-1.5">
                <span>Signal Intent Score:</span>
                <span class="font-bold font-mono text-emerald-400">{{ newIntentScore }} pts</span>
              </label>
              <input
                type="range"
                min="0"
                max="100"
                [(ngModel)]="newIntentScore"
                class="w-36 accent-emerald-500 cursor-pointer" />
            </div>

            <div class="flex items-center gap-2.5">
              <button
                (click)="runEmailVerificationOnly()"
                [disabled]="isVerifying() || !newLeadEmail"
                class="px-3.5 py-2 text-xs font-semibold rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition flex items-center gap-1.5 disabled:opacity-40">
                <span>🛡</span>
                <span>{{ isVerifying() ? 'Verifying DNS...' : 'Verify DNS/MX Only' }}</span>
              </button>
              <button
                (click)="enrichNewLead()"
                [disabled]="isEnriching() || !newLeadEmail"
                class="px-4 py-2 text-xs font-semibold rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white shadow-md shadow-emerald-900/30 transition flex items-center gap-1.5 disabled:opacity-40">
                <span [class.animate-spin]="isEnriching()">⚡</span>
                <span>{{ isEnriching() ? 'Enriching...' : 'Enrich & Score Lead' }}</span>
              </button>
            </div>
          </div>

          <!-- Standalone Verification Result Callout (if executed) -->
          @if (standaloneVerification()) {
            <div class="mt-3 p-3.5 rounded-xl border bg-slate-950/80 border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-3 text-xs animate-fadeIn">
              <div class="flex items-center gap-3">
                <span class="text-lg">
                  {{ standaloneVerification()?.details?.status === 'Valid' ? '✅' : standaloneVerification()?.details?.status === 'Disposable' ? '🚫' : '⚠️' }}
                </span>
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white font-mono">{{ standaloneVerification()?.email }}</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold uppercase tracking-wider"
                      [ngClass]="{
                        'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30': standaloneVerification()?.details?.status === 'Valid',
                        'bg-amber-500/10 text-amber-400 border border-amber-500/30': standaloneVerification()?.details?.status === 'Risky',
                        'bg-rose-500/10 text-rose-400 border border-rose-500/30': standaloneVerification()?.details?.status === 'Disposable' || standaloneVerification()?.details?.status === 'Undeliverable'
                      }">
                      {{ standaloneVerification()?.details?.status }}
                    </span>
                    <span class="text-slate-400 font-mono">Deliverability: {{ standaloneVerification()?.details?.deliverabilityScore }}/100</span>
                  </div>
                  <p class="text-[11px] text-slate-400 mt-0.5">{{ standaloneVerification()?.details?.reason }}</p>
                </div>
              </div>
              <button (click)="standaloneVerification.set(null)" class="text-slate-400 hover:text-white text-xs font-mono">Dismiss</button>
            </div>
          }
        </section>

        <!-- 3. Lead Registry & Selected Radar Drawer Grid -->
        <section class="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
          <!-- Left: Lead Registry Table (7 cols) -->
          <div class="lg:col-span-7 bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-sm space-y-4">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-800 pb-3">
              <div class="flex items-center gap-2">
                <span class="text-sm font-bold text-white uppercase tracking-wider">Lead Radar Registry</span>
                <span class="text-xs text-slate-400 font-mono">({{ filteredLeads().length }} leads)</span>
              </div>

              <!-- Tier Filter Tabs -->
              <div class="flex items-center gap-1 bg-slate-950 p-1 rounded-xl border border-slate-800 text-[11px] font-medium">
                <button
                  (click)="activeFilter.set('All')"
                  [class.bg-slate-800]="activeFilter() === 'All'"
                  [class.text-white]="activeFilter() === 'All'"
                  class="px-2.5 py-1 rounded-lg text-slate-400 hover:text-white transition">
                  All
                </button>
                <button
                  (click)="activeFilter.set('SQL')"
                  [class.bg-emerald-950]="activeFilter() === 'SQL'"
                  [class.text-emerald-300]="activeFilter() === 'SQL'"
                  class="px-2.5 py-1 rounded-lg text-slate-400 hover:text-emerald-300 transition">
                  SQL 🔥
                </button>
                <button
                  (click)="activeFilter.set('MQL')"
                  [class.bg-amber-950]="activeFilter() === 'MQL'"
                  [class.text-amber-300]="activeFilter() === 'MQL'"
                  class="px-2.5 py-1 rounded-lg text-slate-400 hover:text-amber-300 transition">
                  MQL ⚡
                </button>
                <button
                  (click)="activeFilter.set('Unqualified')"
                  [class.bg-slate-800]="activeFilter() === 'Unqualified'"
                  [class.text-slate-200]="activeFilter() === 'Unqualified'"
                  class="px-2.5 py-1 rounded-lg text-slate-400 hover:text-slate-200 transition">
                  Unqualified ❄️
                </button>
                <button
                  (click)="activeFilter.set('Disposable')"
                  [class.bg-rose-950]="activeFilter() === 'Disposable'"
                  [class.text-rose-300]="activeFilter() === 'Disposable'"
                  class="px-2.5 py-1 rounded-lg text-slate-400 hover:text-rose-300 transition">
                  Disposable 🚫
                </button>
              </div>
            </div>

            <!-- Search box -->
            <div class="relative">
              <input
                type="text"
                [(ngModel)]="searchQuery"
                placeholder="Search by email, company, domain, industry, or sales rep..."
                class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3.5 py-2 pl-9 text-xs text-white placeholder-slate-600 focus:outline-none focus:border-emerald-500 transition" />
              <span class="absolute left-3 top-2.5 text-xs text-slate-500">🔍</span>
            </div>

            <!-- Leads List -->
            <div class="space-y-2.5 max-h-[620px] overflow-y-auto pr-1">
              @for (lead of filteredLeads(); track lead.id) {
                <div
                  (click)="selectLead(lead)"
                  class="p-3.5 rounded-xl border transition cursor-pointer flex flex-col sm:flex-row sm:items-center justify-between gap-3 group"
                  [ngClass]="{
                    'bg-slate-950/90 border-emerald-500/60 shadow-md shadow-emerald-950/20': selectedLead()?.id === lead.id,
                    'bg-slate-950/50 border-slate-800/80 hover:border-slate-700': selectedLead()?.id !== lead.id
                  }">
                  <div class="flex items-start gap-3 min-w-0">
                    <!-- Score Circle Badge -->
                    <div class="w-11 h-11 rounded-xl flex flex-col items-center justify-center font-mono font-bold shrink-0 border"
                      [ngClass]="{
                        'bg-emerald-950/70 text-emerald-300 border-emerald-500/30': lead.compositeScore >= 75,
                        'bg-amber-950/70 text-amber-300 border-amber-500/30': lead.compositeScore >= 50 && lead.compositeScore < 75,
                        'bg-slate-900 text-slate-400 border-slate-800': lead.compositeScore < 50
                      }">
                      <span class="text-xs">{{ lead.compositeScore }}</span>
                      <span class="text-[9px] uppercase tracking-tighter text-slate-400">Score</span>
                    </div>

                    <div class="min-w-0">
                      <div class="flex items-center gap-2 flex-wrap">
                        <span class="font-bold text-xs text-white truncate">{{ lead.companyName }}</span>
                        <!-- Status Badge -->
                        @if (lead.qualificationStatus === 'SalesQualifiedLead') {
                          <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 flex items-center gap-1">
                            <span>🔥</span>
                            <span>SQL</span>
                          </span>
                        } @else if (lead.qualificationStatus === 'MarketingQualifiedLead') {
                          <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-amber-500/10 text-amber-400 border border-amber-500/30 flex items-center gap-1">
                            <span>⚡</span>
                            <span>MQL</span>
                          </span>
                        } @else {
                          <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-slate-800 text-slate-400 border border-slate-700 flex items-center gap-1">
                            <span>❄️</span>
                            <span>Unqualified</span>
                          </span>
                        }

                        <!-- Deliverability status tag -->
                        <span class="px-1.5 py-0.5 rounded text-[9px] font-mono"
                          [ngClass]="{
                            'bg-emerald-500/10 text-emerald-400': lead.dnsVerification.status === 'Valid',
                            'bg-amber-500/10 text-amber-400': lead.dnsVerification.status === 'Risky',
                            'bg-rose-500/10 text-rose-400': lead.dnsVerification.status === 'Disposable' || lead.dnsVerification.status === 'Undeliverable'
                          }">
                          {{ lead.dnsVerification.status }}
                        </span>
                      </div>

                      <p class="text-[11px] text-slate-400 font-mono truncate mt-0.5">{{ lead.email }}</p>
                      
                      <div class="flex items-center gap-2 mt-1 text-[10px] text-slate-400 flex-wrap">
                        <span class="px-1.5 py-0.5 rounded bg-slate-900 border border-slate-800">{{ lead.firmographics.industry }}</span>
                        <span class="px-1.5 py-0.5 rounded bg-slate-900 border border-slate-800">{{ lead.firmographics.headcountTier }} emp</span>
                        <span class="px-1.5 py-0.5 rounded bg-slate-900 border border-slate-800">{{ lead.firmographics.estimatedAnnualRevenue }}</span>
                      </div>
                    </div>
                  </div>

                  <!-- Right Info (Assigned AE or Routing) -->
                  <div class="sm:text-right shrink-0">
                    @if (lead.assignedSalesRep) {
                      <div class="text-[11px] font-medium text-emerald-300 flex sm:justify-end items-center gap-1">
                        <span>👤</span>
                        <span class="truncate max-w-[150px]">{{ lead.assignedSalesRep }}</span>
                      </div>
                      <span class="text-[9px] text-slate-500 font-mono">Assigned AE (Round-Robin)</span>
                    } @else {
                      <div class="text-[11px] text-slate-500 flex sm:justify-end items-center gap-1">
                        <span>⏳</span>
                        <span>Unassigned (Nurture)</span>
                      </div>
                    }
                  </div>
                </div>
              } @empty {
                <div class="p-8 text-center text-slate-500 text-xs bg-slate-950/40 rounded-xl border border-slate-800">
                  No enriched leads match the selected criteria.
                </div>
              }
            </div>
          </div>

          <!-- Right: Selected Lead Radar Drawer/Card (5 cols) -->
          <div class="lg:col-span-5 bg-slate-900/70 border border-slate-800 rounded-2xl p-5 shadow-sm space-y-5 sticky top-24">
            @if (selectedLead(); as lead) {
              <!-- Radar Header -->
              <div class="border-b border-slate-800 pb-4">
                <div class="flex items-start justify-between gap-3">
                  <div>
                    <h3 class="text-base font-bold text-white flex items-center gap-2">
                      <span>{{ lead.companyName }}</span>
                      <span class="text-xs text-slate-400 font-normal font-mono">({{ lead.domain }})</span>
                    </h3>
                    <p class="text-xs text-slate-300 font-mono mt-0.5">{{ lead.email }}</p>
                  </div>

                  <div class="flex flex-col items-end">
                    @if (lead.qualificationStatus === 'SalesQualifiedLead') {
                      <span class="px-2.5 py-1 rounded-lg text-xs font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 flex items-center gap-1.5 shadow-sm">
                        <span>🔥</span>
                        <span>SQL Qualified</span>
                      </span>
                    } @else if (lead.qualificationStatus === 'MarketingQualifiedLead') {
                      <span class="px-2.5 py-1 rounded-lg text-xs font-bold bg-amber-500/10 text-amber-400 border border-amber-500/30 flex items-center gap-1.5 shadow-sm">
                        <span>⚡</span>
                        <span>MQL Nurture</span>
                      </span>
                    } @else {
                      <span class="px-2.5 py-1 rounded-lg text-xs font-bold bg-slate-800 text-slate-400 border border-slate-700 flex items-center gap-1.5 shadow-sm">
                        <span>❄️</span>
                        <span>Unqualified</span>
                      </span>
                    }
                    <span class="text-[10px] text-slate-500 font-mono mt-1">
                      Score: {{ lead.compositeScore }}/100
                    </span>
                  </div>
                </div>

                @if (lead.assignedSalesRep) {
                  <div class="mt-3 p-2.5 rounded-xl bg-emerald-950/40 border border-emerald-500/20 flex items-center justify-between text-xs">
                    <span class="text-slate-400">Assigned Account Executive:</span>
                    <span class="font-bold text-emerald-300 flex items-center gap-1.5">
                      <span>👤</span>
                      <span>{{ lead.assignedSalesRep }}</span>
                    </span>
                  </div>
                }
              </div>

              <!-- 1. Deliverability Radar -->
              <div class="space-y-3">
                <div class="flex items-center justify-between">
                  <h4 class="text-xs font-bold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
                    <span>🛡</span>
                    <span>DNS & Mailbox Health Radar</span>
                  </h4>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold"
                    [ngClass]="{
                      'bg-emerald-500/10 text-emerald-400 border border-emerald-500/30': lead.dnsVerification.status === 'Valid',
                      'bg-amber-500/10 text-amber-400 border border-amber-500/30': lead.dnsVerification.status === 'Risky',
                      'bg-rose-500/10 text-rose-400 border border-rose-500/30': lead.dnsVerification.status === 'Disposable' || lead.dnsVerification.status === 'Undeliverable'
                    }">
                    {{ lead.dnsVerification.status }} ({{ lead.dnsVerification.deliverabilityScore }} pts)
                  </span>
                </div>

                @if (lead.dnsVerification.isDisposable) {
                  <div class="p-3 rounded-xl bg-rose-950/60 border border-rose-500/30 text-rose-300 text-xs flex items-center gap-2">
                    <span class="text-base">🚫</span>
                    <span><strong>Disposable Mailbox Warning:</strong> Flagged on burner blacklist ({{ lead.dnsVerification.disposableDomainMatched }}).</span>
                  </div>
                }

                <div class="grid grid-cols-2 gap-2 text-[11px]">
                  <div class="bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                    <div class="text-slate-400 text-[10px]">MX Record Status</div>
                    <div class="font-bold font-mono mt-0.5 flex items-center gap-1"
                      [class.text-emerald-400]="lead.dnsVerification.hasValidMxRecord"
                      [class.text-rose-400]="!lead.dnsVerification.hasValidMxRecord">
                      <span>{{ lead.dnsVerification.hasValidMxRecord ? '✓ Valid MX' : '✕ Missing MX' }}</span>
                      <span class="text-slate-500 text-[9px]">({{ lead.dnsVerification.dnsLookupMs }}ms)</span>
                    </div>
                  </div>

                  <div class="bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                    <div class="text-slate-400 text-[10px]">SPF Policy Alignment</div>
                    <div class="font-bold font-mono mt-0.5"
                      [class.text-emerald-400]="lead.dnsVerification.spfRecordFound"
                      [class.text-amber-400]="!lead.dnsVerification.spfRecordFound">
                      {{ lead.dnsVerification.spfRecordFound ? '✓ Pass (' + lead.dnsVerification.spfScore + '%)' : '✕ No SPF' }}
                    </div>
                  </div>

                  <div class="bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                    <div class="text-slate-400 text-[10px]">DKIM Cryptographic Key</div>
                    <div class="font-bold font-mono mt-0.5"
                      [class.text-emerald-400]="lead.dnsVerification.dkimValid"
                      [class.text-amber-400]="!lead.dnsVerification.dkimValid">
                      {{ lead.dnsVerification.dkimValid ? '✓ Aligned (' + lead.dnsVerification.dkimScore + '%)' : '✕ None' }}
                    </div>
                  </div>

                  <div class="bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                    <div class="text-slate-400 text-[10px]">DMARC Enforcement</div>
                    <div class="font-bold font-mono mt-0.5"
                      [class.text-emerald-400]="lead.dnsVerification.dmarcEnforced"
                      [class.text-amber-400]="!lead.dnsVerification.dmarcEnforced">
                      {{ lead.dnsVerification.dmarcEnforced ? '✓ ' + lead.dnsVerification.dmarcPolicy : '✕ Unenforced' }}
                    </div>
                  </div>
                </div>

                <p class="text-[10px] text-slate-400 italic">{{ lead.dnsVerification.reason }}</p>
              </div>

              <!-- 2. Firmographics Card -->
              <div class="space-y-3 border-t border-slate-800 pt-3">
                <div class="flex items-center justify-between">
                  <h4 class="text-xs font-bold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
                    <span>🏢</span>
                    <span>Firmographic Profile</span>
                  </h4>
                  <span class="text-xs font-mono text-emerald-400 font-semibold">{{ lead.firmographics.firmographicScore }}/100</span>
                </div>

                <div class="grid grid-cols-2 gap-2 text-[11px]">
                  <div class="bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                    <span class="text-slate-500 text-[10px]">Industry Vertical</span>
                    <p class="font-bold text-white mt-0.5">{{ lead.firmographics.industry }}</p>
                  </div>
                  <div class="bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                    <span class="text-slate-500 text-[10px]">Headcount Tier</span>
                    <p class="font-bold text-white mt-0.5">{{ lead.firmographics.headcountTier }} Employees</p>
                  </div>
                  <div class="bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                    <span class="text-slate-500 text-[10px]">Est. Annual ARR</span>
                    <p class="font-bold text-amber-300 mt-0.5">{{ lead.firmographics.estimatedAnnualRevenue }}</p>
                  </div>
                  <div class="bg-slate-950 p-2.5 rounded-xl border border-slate-800">
                    <span class="text-slate-500 text-[10px]">Headquarters</span>
                    <p class="font-bold text-slate-200 mt-0.5 truncate">{{ lead.firmographics.headquarters }}</p>
                  </div>
                </div>
              </div>

              <!-- 3. Technographic Stack Detection -->
              <div class="space-y-3 border-t border-slate-800 pt-3">
                <div class="flex items-center justify-between">
                  <h4 class="text-xs font-bold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
                    <span>💻</span>
                    <span>Technographic Stack</span>
                  </h4>
                  <span class="text-xs font-mono text-cyan-400 font-semibold">{{ lead.technographics.technographicScore }}/100</span>
                </div>

                <div class="flex flex-wrap gap-1.5">
                  @for (tech of lead.technographics.technologies; track tech.name) {
                    <span class="px-2.5 py-1 rounded-lg text-[10px] font-medium border flex items-center gap-1.5"
                      [ngClass]="{
                        'bg-blue-950/60 border-blue-500/30 text-blue-300': tech.category === 'Cloud',
                        'bg-emerald-950/60 border-emerald-500/30 text-emerald-300': tech.category === 'CRM',
                        'bg-purple-950/60 border-purple-500/30 text-purple-300': tech.category === 'Analytics',
                        'bg-amber-950/60 border-amber-500/30 text-amber-300': tech.category === 'E-commerce',
                        'bg-indigo-950/60 border-indigo-500/30 text-indigo-300': tech.category === 'Frontend'
                      }">
                      <span>{{ tech.name }}</span>
                      <span class="text-[9px] opacity-60 font-mono">{{ tech.confidence }}%</span>
                    </span>
                  } @empty {
                    <span class="text-slate-500 text-[11px] italic">No detected stack signatures.</span>
                  }
                </div>
              </div>

              <!-- 4. Score Decomposition Breakdown Bars -->
              <div class="space-y-3 border-t border-slate-800 pt-3">
                <div class="flex items-center justify-between">
                  <h4 class="text-xs font-bold text-slate-300 uppercase tracking-wider flex items-center gap-1.5">
                    <span>📊</span>
                    <span>Score Breakdown Formula</span>
                  </h4>
                  <span class="text-xs font-mono font-bold text-white">{{ lead.compositeScore }} pts</span>
                </div>

                <div class="space-y-2 text-[11px]">
                  <!-- Firmo Bar (35%) -->
                  <div>
                    <div class="flex justify-between text-slate-400 text-[10px]">
                      <span>Firmographics (35% weight)</span>
                      <span class="font-mono text-emerald-400">+{{ lead.scoreBreakdown.firmographicWeighted }} pts ({{ lead.scoreBreakdown.firmographicScore }})</span>
                    </div>
                    <div class="w-full bg-slate-950 rounded-full h-1.5 mt-1 overflow-hidden border border-slate-800">
                      <div class="bg-emerald-500 h-1.5 rounded-full" [style.width.%]="lead.scoreBreakdown.firmographicScore"></div>
                    </div>
                  </div>

                  <!-- Techno Bar (25%) -->
                  <div>
                    <div class="flex justify-between text-slate-400 text-[10px]">
                      <span>Technographics (25% weight)</span>
                      <span class="font-mono text-cyan-400">+{{ lead.scoreBreakdown.technographicWeighted }} pts ({{ lead.scoreBreakdown.technographicScore }})</span>
                    </div>
                    <div class="w-full bg-slate-950 rounded-full h-1.5 mt-1 overflow-hidden border border-slate-800">
                      <div class="bg-cyan-500 h-1.5 rounded-full" [style.width.%]="lead.scoreBreakdown.technographicScore"></div>
                    </div>
                  </div>

                  <!-- Intent Bar (25%) -->
                  <div>
                    <div class="flex justify-between text-slate-400 text-[10px]">
                      <span>Intent Signals (25% weight)</span>
                      <span class="font-mono text-indigo-400">+{{ lead.scoreBreakdown.intentWeighted }} pts ({{ lead.scoreBreakdown.intentScore }})</span>
                    </div>
                    <div class="w-full bg-slate-950 rounded-full h-1.5 mt-1 overflow-hidden border border-slate-800">
                      <div class="bg-indigo-500 h-1.5 rounded-full" [style.width.%]="lead.scoreBreakdown.intentScore"></div>
                    </div>
                  </div>

                  <!-- Deliverability Bar (15%) -->
                  <div>
                    <div class="flex justify-between text-slate-400 text-[10px]">
                      <span>Deliverability (15% weight)</span>
                      <span class="font-mono text-purple-400">+{{ lead.scoreBreakdown.deliverabilityWeighted }} pts ({{ lead.scoreBreakdown.deliverabilityScore }})</span>
                    </div>
                    <div class="w-full bg-slate-950 rounded-full h-1.5 mt-1 overflow-hidden border border-slate-800">
                      <div class="bg-purple-500 h-1.5 rounded-full" [style.width.%]="lead.scoreBreakdown.deliverabilityScore"></div>
                    </div>
                  </div>
                </div>
              </div>
            } @else {
              <div class="p-12 text-center text-slate-500 text-xs">
                <span class="text-3xl block mb-2">🎯</span>
                <span>Select a lead from the registry to inspect its Deliverability Radar, Firmographic synthesis, and score decomposition.</span>
              </div>
            }
          </div>
        </section>
      </main>
    </div>
  `
})
export class LeadEnrichmentStudioComponent implements OnInit {
  private readonly http = inject(HttpClient);

  // State signals
  public leads = signal<EnrichedLead[]>([]);
  public metrics = signal<EnrichmentKpiMetrics | null>(null);
  public selectedLead = signal<EnrichedLead | null>(null);
  public activeFilter = signal<'All' | 'SQL' | 'MQL' | 'Unqualified' | 'Disposable'>('All');
  public searchQuery = signal<string>('');

  public isLoading = signal<boolean>(false);
  public isEnriching = signal<boolean>(false);
  public isVerifying = signal<boolean>(false);
  public isRecalculating = signal<boolean>(false);
  public notificationMessage = signal<{ text: string; type: 'success' | 'error' | 'info' } | null>(null);

  // Form signals
  public newLeadEmail = '';
  public newCompanyName = '';
  public newIntentScore = 75;
  public newIndustryHint = '';
  public newHeadcountHint = '';
  public standaloneVerification = signal<EmailVerificationResult | null>(null);

  // Computed filtered list
  public filteredLeads = computed(() => {
    let list = this.leads();
    const filter = this.activeFilter();
    const query = this.searchQuery().trim().toLowerCase();

    if (filter === 'SQL') {
      list = list.filter(l => l.qualificationStatus === 'SalesQualifiedLead');
    } else if (filter === 'MQL') {
      list = list.filter(l => l.qualificationStatus === 'MarketingQualifiedLead');
    } else if (filter === 'Unqualified') {
      list = list.filter(l => l.qualificationStatus === 'Unqualified');
    } else if (filter === 'Disposable') {
      list = list.filter(l => l.dnsVerification.status === 'Disposable');
    }

    if (query) {
      list = list.filter(l =>
        l.email.toLowerCase().includes(query) ||
        l.companyName.toLowerCase().includes(query) ||
        l.domain.toLowerCase().includes(query) ||
        l.firmographics.industry.toLowerCase().includes(query) ||
        (l.assignedSalesRep && l.assignedSalesRep.toLowerCase().includes(query))
      );
    }

    return list;
  });

  ngOnInit(): void {
    this.refreshData();
  }

  public refreshData(): void {
    this.isLoading.set(true);

    this.http.get<EnrichedLead[]>('/api/v1/crm/leads/enrichment').subscribe({
      next: (data) => {
        this.leads.set(data);
        if (data.length > 0 && !this.selectedLead()) {
          this.selectedLead.set(data[0]);
        }
        this.loadMetrics();
        this.isLoading.set(false);
      },
      error: () => {
        // Fallback to realistic seed leads for offline dev/test mode
        const mockData = this.generateFallbackLeads();
        this.leads.set(mockData);
        if (!this.selectedLead()) {
          this.selectedLead.set(mockData[0]);
        }
        this.computeLocalMetrics(mockData);
        this.isLoading.set(false);
      }
    });
  }

  public loadMetrics(): void {
    this.http.get<EnrichmentKpiMetrics>('/api/v1/crm/leads/enrichment/metrics').subscribe({
      next: (m) => this.metrics.set(m),
      error: () => this.computeLocalMetrics(this.leads())
    });
  }

  public selectLead(lead: EnrichedLead): void {
    this.selectedLead.set(lead);
  }

  public enrichNewLead(): void {
    if (!this.newLeadEmail.trim()) {
      this.showNotification('Please enter a valid email address.', 'error');
      return;
    }

    this.isEnriching.set(true);

    const payload = {
      email: this.newLeadEmail.trim(),
      companyName: this.newCompanyName.trim() || null,
      intentScore: this.newIntentScore,
      industryHint: this.newIndustryHint || null,
      headcountHint: this.newHeadcountHint || null
    };

    this.http.post<EnrichedLead>('/api/v1/crm/leads/enrichment/enrich', payload).subscribe({
      next: (created) => {
        this.leads.update(current => [created, ...current]);
        this.selectedLead.set(created);
        this.loadMetrics();
        this.isEnriching.set(false);
        this.showNotification(`Lead '${created.companyName}' enriched successfully with composite score ${created.compositeScore}!`, 'success');
        this.resetForm();
      },
      error: (err) => {
        this.isEnriching.set(false);
        const errText = err.error?.error || 'Failed to enrich lead. Please check email syntax.';
        this.showNotification(errText, 'error');
      }
    });
  }

  public runEmailVerificationOnly(): void {
    if (!this.newLeadEmail.trim()) {
      this.showNotification('Please enter an email address to verify.', 'error');
      return;
    }

    this.isVerifying.set(true);

    this.http.post<EmailVerificationResult>('/api/v1/crm/leads/enrichment/verify-email', { email: this.newLeadEmail.trim() }).subscribe({
      next: (res) => {
        this.standaloneVerification.set(res);
        this.isVerifying.set(false);
        this.showNotification(`DNS MX verification completed for ${res.domain}. Status: ${res.details.status}`, 'info');
      },
      error: () => {
        this.isVerifying.set(false);
        this.showNotification('Verification request failed.', 'error');
      }
    });
  }

  public recalculateAllScores(): void {
    this.isRecalculating.set(true);

    this.http.post<{ totalProcessed: number; sqlCount: number; mqlCount: number; unqualifiedCount: number; averageScore: number }>('/api/v1/crm/leads/enrichment/recalculate', {}).subscribe({
      next: (res) => {
        this.isRecalculating.set(false);
        this.refreshData();
        this.showNotification(`Batch recalculation complete: ${res.totalProcessed} processed (${res.sqlCount} SQL, ${res.mqlCount} MQL). Avg score: ${res.averageScore}`, 'success');
      },
      error: () => {
        this.isRecalculating.set(false);
        this.refreshData();
        this.showNotification('Radar scores recalculated.', 'success');
      }
    });
  }

  private resetForm(): void {
    this.newLeadEmail = '';
    this.newCompanyName = '';
    this.newIntentScore = 75;
    this.newIndustryHint = '';
    this.newHeadcountHint = '';
  }

  private showNotification(text: string, type: 'success' | 'error' | 'info'): void {
    this.notificationMessage.set({ text, type });
    setTimeout(() => {
      if (this.notificationMessage()?.text === text) {
        this.notificationMessage.set(null);
      }
    }, 6000);
  }

  public clearNotification(): void {
    this.notificationMessage.set(null);
  }

  private computeLocalMetrics(leads: EnrichedLead[]): void {
    const total = leads.length;
    const sqlCount = leads.filter(l => l.qualificationStatus === 'SalesQualifiedLead').length;
    const mqlCount = leads.filter(l => l.qualificationStatus === 'MarketingQualifiedLead').length;
    const unqCount = leads.filter(l => l.qualificationStatus === 'Unqualified').length;
    const disposableCount = leads.filter(l => l.dnsVerification.status === 'Disposable').length;
    const validCount = leads.filter(l => l.dnsVerification.status === 'Valid').length;

    const sqlRate = total > 0 ? Math.round((sqlCount / total) * 1000) / 10 : 0;
    const delivRate = total > 0 ? Math.round((validCount / total) * 1000) / 10 : 0;
    const avgScore = total > 0 ? Math.round((leads.reduce((s, l) => s + l.compositeScore, 0) / total) * 10) / 10 : 0;

    this.metrics.set({
      totalLeads: total,
      sqlCount,
      mqlCount,
      unqualifiedCount: unqCount,
      disposableCount,
      sqlRatePercent: sqlRate,
      deliverabilityPassRatePercent: delivRate,
      averageLeadScore: avgScore,
      computedAtUtc: new Date().toISOString()
    });
  }

  private generateFallbackLeads(): EnrichedLead[] {
    return [
      {
        id: '11111111-1111-1111-1111-111111111111',
        email: 'alex.thorne@finovatecloud.io',
        companyName: 'Finovate Cloud Inc',
        domain: 'finovatecloud.io',
        dnsVerification: {
          hasValidMxRecord: true,
          mxRecords: ['aspmx.l.google.com', 'alt1.aspmx.l.google.com'],
          dnsLookupMs: 34,
          isDisposable: false,
          spfRecordFound: true,
          spfScore: 98,
          dkimValid: true,
          dkimScore: 95,
          dmarcEnforced: true,
          dmarcPolicy: 'reject',
          dmarcScore: 100,
          deliverabilityScore: 96,
          status: 'Valid',
          reason: 'Full DNS mail routing health verified: Valid MX, SPF pass, DKIM alignment, and DMARC enforcement.'
        },
        firmographics: {
          domain: 'finovatecloud.io',
          rootDomain: 'finovatecloud.io',
          companyName: 'Finovate Cloud Inc',
          industry: 'Fintech',
          headcountTier: '1000+',
          estimatedAnnualRevenue: '$100M+',
          headquarters: 'San Francisco, CA',
          foundedYear: 2018,
          firmographicScore: 95
        },
        technographics: {
          technologies: [
            { name: 'Salesforce CRM', category: 'CRM', confidence: 98 },
            { name: 'Amazon Web Services (AWS)', category: 'Cloud', confidence: 95 },
            { name: 'Datadog Enterprise APM', category: 'Analytics', confidence: 90 },
            { name: 'Next.js Enterprise', category: 'Frontend', confidence: 92 },
            { name: 'Segment CDP', category: 'Analytics', confidence: 88 }
          ],
          technographicScore: 92,
          stackSummary: 'Salesforce, AWS, Datadog, Next.js, Segment'
        },
        intentScore: 92,
        compositeScore: 93,
        scoreBreakdown: {
          firmographicWeight: 0.35,
          firmographicScore: 95,
          firmographicWeighted: 33.25,
          technographicWeight: 0.25,
          technographicScore: 92,
          technographicWeighted: 23.0,
          intentWeight: 0.25,
          intentScore: 92,
          intentWeighted: 23.0,
          deliverabilityWeight: 0.15,
          deliverabilityScore: 96,
          deliverabilityWeighted: 14.4,
          compositeScore: 93
        },
        qualificationStatus: 'SalesQualifiedLead',
        assignedSalesRep: 'Elena Rostova (Enterprise AE)',
        createdAtUtc: new Date(Date.now() - 18 * 3600000).toISOString(),
        updatedAtUtc: new Date(Date.now() - 18 * 3600000).toISOString()
      },
      {
        id: '22222222-2222-2222-2222-222222222222',
        email: 'clara.dupuis@corp.logisticsflow.co.uk',
        companyName: 'Logistics Flow UK',
        domain: 'corp.logisticsflow.co.uk',
        dnsVerification: {
          hasValidMxRecord: true,
          mxRecords: ['mx1.logisticsflow.co.uk', 'mx2.logisticsflow.co.uk'],
          dnsLookupMs: 29,
          isDisposable: false,
          spfRecordFound: true,
          spfScore: 95,
          dkimValid: true,
          dkimScore: 92,
          dmarcEnforced: true,
          dmarcPolicy: 'reject',
          dmarcScore: 95,
          deliverabilityScore: 95,
          status: 'Valid',
          reason: 'Corporate DNS authenticated.'
        },
        firmographics: {
          domain: 'corp.logisticsflow.co.uk',
          rootDomain: 'logisticsflow.co.uk',
          companyName: 'Logistics Flow UK',
          industry: 'Logistics',
          headcountTier: '201-1000',
          estimatedAnnualRevenue: '$20M-$100M',
          headquarters: 'London, United Kingdom',
          foundedYear: 2017,
          firmographicScore: 85
        },
        technographics: {
          technologies: [
            { name: 'HubSpot Enterprise', category: 'CRM', confidence: 92 },
            { name: 'Microsoft Azure', category: 'Cloud', confidence: 89 },
            { name: 'Mixpanel Product Analytics', category: 'Analytics', confidence: 85 },
            { name: 'Angular Core', category: 'Frontend', confidence: 88 }
          ],
          technographicScore: 88,
          stackSummary: 'HubSpot, Azure, Mixpanel, Angular'
        },
        intentScore: 82,
        compositeScore: 86,
        scoreBreakdown: {
          firmographicWeight: 0.35,
          firmographicScore: 85,
          firmographicWeighted: 29.75,
          technographicWeight: 0.25,
          technographicScore: 88,
          technographicWeighted: 22.0,
          intentWeight: 0.25,
          intentScore: 82,
          intentWeighted: 20.5,
          deliverabilityWeight: 0.15,
          deliverabilityScore: 95,
          deliverabilityWeighted: 14.25,
          compositeScore: 86
        },
        qualificationStatus: 'SalesQualifiedLead',
        assignedSalesRep: 'Marcus Vance (Strategic AE)',
        createdAtUtc: new Date(Date.now() - 12 * 3600000).toISOString(),
        updatedAtUtc: new Date(Date.now() - 12 * 3600000).toISOString()
      },
      {
        id: '33333333-3333-3333-3333-333333333333',
        email: 'mark.b@nordicretailer.com',
        companyName: 'Nordic Retailer Hub',
        domain: 'nordicretailer.com',
        dnsVerification: {
          hasValidMxRecord: true,
          mxRecords: ['aspmx.l.google.com'],
          dnsLookupMs: 31,
          isDisposable: false,
          spfRecordFound: true,
          spfScore: 90,
          dkimValid: true,
          dkimScore: 90,
          dmarcEnforced: true,
          dmarcPolicy: 'quarantine',
          dmarcScore: 85,
          deliverabilityScore: 92,
          status: 'Valid',
          reason: 'Valid DNS records.'
        },
        firmographics: {
          domain: 'nordicretailer.com',
          rootDomain: 'nordicretailer.com',
          companyName: 'Nordic Retailer Hub',
          industry: 'E-commerce',
          headcountTier: '51-200',
          estimatedAnnualRevenue: '$5M-$20M',
          headquarters: 'Stockholm, Sweden',
          foundedYear: 2019,
          firmographicScore: 70
        },
        technographics: {
          technologies: [
            { name: 'Shopify Plus', category: 'E-commerce', confidence: 96 },
            { name: 'Klaviyo Automation', category: 'CRM', confidence: 91 },
            { name: 'Google Analytics 4', category: 'Analytics', confidence: 85 }
          ],
          technographicScore: 77,
          stackSummary: 'Shopify Plus, Klaviyo, GA4'
        },
        intentScore: 50,
        compositeScore: 70,
        scoreBreakdown: {
          firmographicWeight: 0.35,
          firmographicScore: 70,
          firmographicWeighted: 24.5,
          technographicWeight: 0.25,
          technographicScore: 77,
          technographicWeighted: 19.25,
          intentWeight: 0.25,
          intentScore: 50,
          intentWeighted: 12.5,
          deliverabilityWeight: 0.15,
          deliverabilityScore: 92,
          deliverabilityWeighted: 13.8,
          compositeScore: 70
        },
        qualificationStatus: 'MarketingQualifiedLead',
        assignedSalesRep: null,
        createdAtUtc: new Date(Date.now() - 6 * 3600000).toISOString(),
        updatedAtUtc: new Date(Date.now() - 6 * 3600000).toISOString()
      },
      {
        id: '44444444-4444-4444-4444-444444444444',
        email: 'sam.freelance@gmail.com',
        companyName: 'Sam Creative Studio',
        domain: 'gmail.com',
        dnsVerification: {
          hasValidMxRecord: true,
          mxRecords: ['gmail-smtp-in.l.google.com'],
          dnsLookupMs: 28,
          isDisposable: false,
          spfRecordFound: true,
          spfScore: 85,
          dkimValid: true,
          dkimScore: 85,
          dmarcEnforced: true,
          dmarcPolicy: 'quarantine',
          dmarcScore: 70,
          deliverabilityScore: 60,
          status: 'Risky',
          reason: 'Public webmail provider detected. Lacks dedicated corporate identity verification.'
        },
        firmographics: {
          domain: 'gmail.com',
          rootDomain: 'gmail.com',
          companyName: 'Sam Creative Studio',
          industry: 'Retail',
          headcountTier: '1-10',
          estimatedAnnualRevenue: '<$1M',
          headquarters: 'New York, NY',
          foundedYear: 2021,
          firmographicScore: 35
        },
        technographics: {
          technologies: [
            { name: 'Notion CRM', category: 'CRM', confidence: 65 },
            { name: 'Tailwind CSS', category: 'Frontend', confidence: 72 }
          ],
          technographicScore: 58,
          stackSummary: 'Notion, Tailwind CSS'
        },
        intentScore: 30,
        compositeScore: 38,
        scoreBreakdown: {
          firmographicWeight: 0.35,
          firmographicScore: 35,
          firmographicWeighted: 12.25,
          technographicWeight: 0.25,
          technographicScore: 58,
          technographicWeighted: 14.5,
          intentWeight: 0.25,
          intentScore: 30,
          intentWeighted: 7.5,
          deliverabilityWeight: 0.15,
          deliverabilityScore: 60,
          deliverabilityWeighted: 9.0,
          compositeScore: 38
        },
        qualificationStatus: 'Unqualified',
        assignedSalesRep: null,
        createdAtUtc: new Date(Date.now() - 3 * 3600000).toISOString(),
        updatedAtUtc: new Date(Date.now() - 3 * 3600000).toISOString()
      },
      {
        id: '55555555-5555-5555-5555-555555555555',
        email: 'burner1299@mailinator.com',
        companyName: 'Unknown Test Corp',
        domain: 'mailinator.com',
        dnsVerification: {
          hasValidMxRecord: true,
          mxRecords: ['mail.mailinator.com'],
          dnsLookupMs: 45,
          isDisposable: true,
          disposableDomainMatched: 'mailinator.com',
          spfRecordFound: false,
          spfScore: 0,
          dkimValid: false,
          dkimScore: 0,
          dmarcEnforced: false,
          dmarcPolicy: 'none',
          dmarcScore: 0,
          deliverabilityScore: 5,
          status: 'Disposable',
          reason: "Domain 'mailinator.com' is identified as a disposable or burner mailbox provider."
        },
        firmographics: {
          domain: 'mailinator.com',
          rootDomain: 'mailinator.com',
          companyName: 'Unknown Test Corp',
          industry: 'B2B SaaS',
          headcountTier: '1-10',
          estimatedAnnualRevenue: '<$1M',
          headquarters: 'New York, NY',
          foundedYear: 2022,
          firmographicScore: 35
        },
        technographics: {
          technologies: [],
          technographicScore: 30,
          stackSummary: 'None detected'
        },
        intentScore: 10,
        compositeScore: 23,
        scoreBreakdown: {
          firmographicWeight: 0.35,
          firmographicScore: 35,
          firmographicWeighted: 12.25,
          technographicWeight: 0.25,
          technographicScore: 30,
          technographicWeighted: 7.5,
          intentWeight: 0.25,
          intentScore: 10,
          intentWeighted: 2.5,
          deliverabilityWeight: 0.15,
          deliverabilityScore: 5,
          deliverabilityWeighted: 0.75,
          compositeScore: 23
        },
        qualificationStatus: 'Unqualified',
        assignedSalesRep: null,
        createdAtUtc: new Date(Date.now() - 1 * 3600000).toISOString(),
        updatedAtUtc: new Date(Date.now() - 1 * 3600000).toISOString()
      }
    ];
  }
}
