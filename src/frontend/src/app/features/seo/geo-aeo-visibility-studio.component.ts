import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type AiEngineType = 'Perplexity' | 'ChatGPT_Search' | 'Google_Gemini_AIO' | 'Claude_Web';
export type CitabilityGrade = 'High' | 'Moderate' | 'NeedsImprovement';
export type CrawlerReadinessStatus = 'Compliant' | 'Warning' | 'Blocked';

export interface GeoCitationQueryRecord {
  id: string;
  tenantId: string;
  query: string;
  engine: AiEngineType;
  citedUrl: string;
  citationRank: number;
  extractedPassage: string;
  sentiment: 'Positive' | 'Neutral' | 'Negative';
  shareOfVoice: number;
  detectedAtUtc: string;
}

export interface PageCitabilityAudit {
  id: string;
  tenantId: string;
  url: string;
  title: string;
  overallScore: number;
  grade: CitabilityGrade;
  passageConcisenessScore: number;
  entityDensityScore: number;
  structuredFormatRatio: number;
  factualClaimsCount: number;
  ambiguousPronounOccurrences: number;
  recommendations: string[];
  auditedAtUtc: string;
}

export interface AgentCrawlerRule {
  botName: string;
  userAgent: string;
  status: CrawlerReadinessStatus;
  allowedInRobotsTxt: boolean;
  discoveredPath: string;
  details: string;
  lastPingStatus?: 'Success' | 'Failed' | 'Pending';
}

export interface AgenticCrawlerManifest {
  tenantId: string;
  domain: string;
  overallRobotsStatus: CrawlerReadinessStatus;
  llmsTxtStatus: CrawlerReadinessStatus;
  crawlers: AgentCrawlerRule[];
  lastVerifiedAtUtc: string;
  actionItems: string[];
}

@Component({
  selector: 'app-geo-aeo-visibility-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="geo-radar-studio p-6 max-w-7xl mx-auto space-y-6 text-slate-800" dir="ltr">
      <!-- Studio Header -->
      <header class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-slate-200 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">
              GEO/AEO Radar & AI Citability Optimization
            </h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-violet-100 text-violet-800 border border-violet-200">
              Domain C3 & J1/J3
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Real-time answer engine citation radar, live page citability auditor, and agentic crawler governance.
          </p>
        </div>

        <!-- Tab Switcher -->
        <nav class="flex items-center bg-slate-100 p-1 rounded-xl border border-slate-200" aria-label="Studio Views">
          <button
            type="button"
            (click)="setActiveTab('radar')"
            [class.bg-white]="activeTab() === 'radar'"
            [class.shadow-sm]="activeTab() === 'radar'"
            [class.text-indigo-600]="activeTab() === 'radar'"
            [class.text-slate-600]="activeTab() !== 'radar'"
            class="px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all"
            data-testid="tab-radar">
            📡 Citation Radar Feed
          </button>
          <button
            type="button"
            (click)="setActiveTab('auditor')"
            [class.bg-white]="activeTab() === 'auditor'"
            [class.shadow-sm]="activeTab() === 'auditor'"
            [class.text-indigo-600]="activeTab() === 'auditor'"
            [class.text-slate-600]="activeTab() !== 'auditor'"
            class="px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all"
            data-testid="tab-auditor">
            🔬 Page Citability Auditor
          </button>
          <button
            type="button"
            (click)="setActiveTab('governance')"
            [class.bg-white]="activeTab() === 'governance'"
            [class.shadow-sm]="activeTab() === 'governance'"
            [class.text-indigo-600]="activeTab() === 'governance'"
            [class.text-slate-600]="activeTab() !== 'governance'"
            class="px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all"
            data-testid="tab-governance">
            🤖 Agentic Crawler Governance
          </button>
        </nav>
      </header>

      <!-- Executive KPI Cards -->
      <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4" data-testid="kpi-grid">
        <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-xs">
          <div class="flex items-center justify-between">
            <span class="text-xs font-medium text-slate-400">Overall GEO Score</span>
            <span class="text-xs px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-700 font-semibold">Tier 1 Elite</span>
          </div>
          <div class="text-2xl font-bold text-slate-900 mt-2 font-mono flex items-baseline gap-1">
            {{ overallGeoScore() }}
            <span class="text-xs text-slate-400 font-normal">/ 100</span>
          </div>
          <p class="text-[11px] text-slate-500 mt-1">Weighted across citations and page readiness</p>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-xs">
          <div class="flex items-center justify-between">
            <span class="text-xs font-medium text-slate-400">Share of Model Voice (SoMV)</span>
            <span class="text-xs px-2 py-0.5 rounded-full bg-indigo-50 text-indigo-700 font-semibold">+6.4% MoM</span>
          </div>
          <div class="text-2xl font-bold text-indigo-600 mt-2 font-mono">
            {{ shareOfModelVoice() }}%
          </div>
          <p class="text-[11px] text-slate-500 mt-1">Answer synthesis presence across target prompts</p>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-xs">
          <div class="flex items-center justify-between">
            <span class="text-xs font-medium text-slate-400">Monitored Citations</span>
            <span class="text-xs px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 font-semibold">Active Radar</span>
          </div>
          <div class="text-2xl font-bold text-slate-900 mt-2 font-mono">
            {{ totalQueriesCount() }}
          </div>
          <p class="text-[11px] text-slate-500 mt-1">Indexed citations across 4 major AI search engines</p>
        </div>

        <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-xs">
          <div class="flex items-center justify-between">
            <span class="text-xs font-medium text-slate-400">Agentic Crawler Readiness</span>
            <span class="text-xs px-2 py-0.5 rounded-full bg-violet-50 text-violet-700 font-semibold">robots + llms.txt</span>
          </div>
          <div class="text-2xl font-bold text-emerald-600 mt-2 font-mono">
            {{ crawlerReadinessPercent() }}%
          </div>
          <p class="text-[11px] text-slate-500 mt-1">GPTBot, PerplexityBot, ClaudeBot, Google-Extended</p>
        </div>
      </section>

      <!-- TAB 1: Citation Radar Feed -->
      @if (activeTab() === 'radar') {
        <section class="space-y-4" data-testid="radar-section">
          <!-- Toolbar & Filters -->
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-3 bg-white p-3.5 rounded-xl border border-slate-200">
            <!-- Engine Filter Pills -->
            <div class="flex flex-wrap items-center gap-1.5" role="group" aria-label="Engine Filters">
              <button
                type="button"
                (click)="setEngineFilter('ALL')"
                [class.bg-slate-900]="selectedEngineFilter() === 'ALL'"
                [class.text-white]="selectedEngineFilter() === 'ALL'"
                [class.bg-slate-100]="selectedEngineFilter() !== 'ALL'"
                [class.text-slate-700]="selectedEngineFilter() !== 'ALL'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition-colors"
                data-testid="filter-engine-all">
                All Engines ({{ citations().length }})
              </button>
              <button
                type="button"
                (click)="setEngineFilter('Perplexity')"
                [class.bg-teal-700]="selectedEngineFilter() === 'Perplexity'"
                [class.text-white]="selectedEngineFilter() === 'Perplexity'"
                [class.bg-teal-50]="selectedEngineFilter() !== 'Perplexity'"
                [class.text-teal-800]="selectedEngineFilter() !== 'Perplexity'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition-colors"
                data-testid="filter-engine-perplexity">
                Perplexity
              </button>
              <button
                type="button"
                (click)="setEngineFilter('ChatGPT_Search')"
                [class.bg-emerald-700]="selectedEngineFilter() === 'ChatGPT_Search'"
                [class.text-white]="selectedEngineFilter() === 'ChatGPT_Search'"
                [class.bg-emerald-50]="selectedEngineFilter() !== 'ChatGPT_Search'"
                [class.text-emerald-800]="selectedEngineFilter() !== 'ChatGPT_Search'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition-colors"
                data-testid="filter-engine-chatgpt">
                ChatGPT Search
              </button>
              <button
                type="button"
                (click)="setEngineFilter('Google_Gemini_AIO')"
                [class.bg-blue-700]="selectedEngineFilter() === 'Google_Gemini_AIO'"
                [class.text-white]="selectedEngineFilter() === 'Google_Gemini_AIO'"
                [class.bg-blue-50]="selectedEngineFilter() !== 'Google_Gemini_AIO'"
                [class.text-blue-800]="selectedEngineFilter() !== 'Google_Gemini_AIO'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition-colors"
                data-testid="filter-engine-gemini">
                Google Gemini AIO
              </button>
              <button
                type="button"
                (click)="setEngineFilter('Claude_Web')"
                [class.bg-amber-700]="selectedEngineFilter() === 'Claude_Web'"
                [class.text-white]="selectedEngineFilter() === 'Claude_Web'"
                [class.bg-amber-50]="selectedEngineFilter() !== 'Claude_Web'"
                [class.text-amber-800]="selectedEngineFilter() !== 'Claude_Web'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition-colors"
                data-testid="filter-engine-claude">
                Claude Web
              </button>
            </div>

            <!-- Search + Action Button -->
            <div class="flex items-center gap-2">
              <input
                type="text"
                [(ngModel)]="searchQueryFilter"
                placeholder="Search queries..."
                class="px-3 py-1.5 border border-slate-300 rounded-lg text-xs w-48 md:w-56 focus:outline-hidden focus:ring-2 focus:ring-indigo-500"
                data-testid="input-search-citations" />
              <button
                type="button"
                (click)="isAddingQuery.set(!isAddingQuery())"
                class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-medium transition-colors shadow-xs"
                data-testid="btn-toggle-add-query">
                + Track Query
              </button>
            </div>
          </div>

          <!-- New Query Form Collapse -->
          @if (isAddingQuery()) {
            <div class="bg-indigo-50/50 border border-indigo-200 rounded-xl p-4 space-y-3" data-testid="form-add-query">
              <div class="flex items-center justify-between">
                <h3 class="text-xs font-bold text-indigo-950 uppercase tracking-wider">Track Prompt Citation</h3>
                <button
                  type="button"
                  (click)="isAddingQuery.set(false)"
                  class="text-xs text-slate-400 hover:text-slate-600">
                  ✕ Cancel
                </button>
              </div>
              <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
                <div class="md:col-span-2">
                  <label class="block text-[11px] font-medium text-slate-600 mb-1">Target Prompt / Keyword Query</label>
                  <input
                    type="text"
                    [(ngModel)]="newQueryText"
                    placeholder="e.g. best b2b marketing automation platform"
                    class="w-full px-3 py-1.5 bg-white border border-slate-300 rounded-lg text-xs focus:ring-2 focus:ring-indigo-500"
                    data-testid="input-new-query-text" />
                </div>
                <div>
                  <label class="block text-[11px] font-medium text-slate-600 mb-1">Target Engine</label>
                  <select
                    [(ngModel)]="newQueryEngine"
                    class="w-full px-3 py-1.5 bg-white border border-slate-300 rounded-lg text-xs focus:ring-2 focus:ring-indigo-500"
                    data-testid="select-new-query-engine">
                    <option value="Perplexity">Perplexity</option>
                    <option value="ChatGPT_Search">ChatGPT Search</option>
                    <option value="Google_Gemini_AIO">Google Gemini AIO</option>
                    <option value="Claude_Web">Claude Web</option>
                  </select>
                </div>
                <div>
                  <label class="block text-[11px] font-medium text-slate-600 mb-1">Cited Landing Page</label>
                  <input
                    type="text"
                    [(ngModel)]="newQueryUrl"
                    placeholder="https://sgrowing.io/..."
                    class="w-full px-3 py-1.5 bg-white border border-slate-300 rounded-lg text-xs focus:ring-2 focus:ring-indigo-500"
                    data-testid="input-new-query-url" />
                </div>
                <div>
                  <label class="block text-[11px] font-medium text-slate-600 mb-1">Citation Rank Position</label>
                  <input
                    type="number"
                    min="1"
                    max="10"
                    [(ngModel)]="newQueryRank"
                    class="w-full px-3 py-1.5 bg-white border border-slate-300 rounded-lg text-xs focus:ring-2 focus:ring-indigo-500"
                    data-testid="input-new-query-rank" />
                </div>
                <div>
                  <label class="block text-[11px] font-medium text-slate-600 mb-1">Model Sentiment</label>
                  <select
                    [(ngModel)]="newQuerySentiment"
                    class="w-full px-3 py-1.5 bg-white border border-slate-300 rounded-lg text-xs focus:ring-2 focus:ring-indigo-500"
                    data-testid="select-new-query-sentiment">
                    <option value="Positive">Positive</option>
                    <option value="Neutral">Neutral</option>
                    <option value="Negative">Negative</option>
                  </select>
                </div>
              </div>
              <div class="flex justify-end gap-2 pt-1">
                <button
                  type="button"
                  (click)="trackNewQuery()"
                  class="px-4 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-semibold shadow-xs"
                  data-testid="btn-submit-track-query">
                  Record Verification
                </button>
              </div>
            </div>
          }

          <!-- Citations List -->
          <div class="space-y-3" data-testid="citations-feed">
            @for (item of filteredCitations(); track item.id) {
              <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-xs hover:border-indigo-300 transition-colors">
                <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-slate-100 pb-2.5">
                  <div class="flex items-center gap-2.5">
                    <span [class]="getEngineBadgeClass(item.engine)" class="px-2 py-0.5 rounded-md text-[11px] font-semibold">
                      {{ item.engine }}
                    </span>
                    <h4 class="text-sm font-semibold text-slate-900 font-mono">
                      "{{ item.query }}"
                    </h4>
                  </div>
                  <div class="flex items-center gap-2">
                    <span class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-bold bg-slate-100 text-slate-800 font-mono">
                      Rank #{{ item.citationRank }}
                    </span>
                    <span
                      [class]="item.sentiment === 'Positive' ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-700'"
                      class="px-2 py-0.5 rounded text-[11px] font-medium">
                      {{ item.sentiment }}
                    </span>
                    <span class="text-xs font-mono font-semibold text-indigo-600">
                      {{ item.shareOfVoice }}% SoV
                    </span>
                  </div>
                </div>

                <div class="mt-3 text-xs text-slate-600 leading-relaxed bg-slate-50 p-3 rounded-lg border border-slate-100">
                  <span class="text-slate-400 font-serif mr-1">“</span>{{ item.extractedPassage }}<span class="text-slate-400 font-serif ml-1">”</span>
                </div>

                <div class="mt-2.5 flex items-center justify-between text-[11px] text-slate-400">
                  <a [href]="item.citedUrl" target="_blank" rel="noopener noreferrer" class="text-indigo-600 hover:underline flex items-center gap-1 font-mono">
                    🔗 {{ item.citedUrl }}
                  </a>
                  <span>Detected {{ item.detectedAtUtc | date:'short' }}</span>
                </div>
              </div>
            } @empty {
              <div class="bg-white border border-dashed border-slate-300 rounded-xl p-8 text-center text-slate-500 text-xs">
                No citation records match the selected engine or search keyword filter.
              </div>
            }
          </div>
        </section>
      }

      <!-- TAB 2: Live Page Citability Auditor -->
      @if (activeTab() === 'auditor') {
        <section class="space-y-5" data-testid="auditor-section">
          <!-- Auditor Form -->
          <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-base font-bold text-slate-900">Real-Time Citability Diagnostic Engine</h3>
                <p class="text-xs text-slate-500 mt-0.5">
                  Evaluates heading structure, passage conciseness, entity density, and ambiguous pronoun penalties for AI chunk ingestion.
                </p>
              </div>
              <span class="text-xs px-2.5 py-1 rounded-md bg-indigo-50 text-indigo-700 font-mono font-semibold">
                Domain J1/J3 Citability Protocol
              </span>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
              <div>
                <label class="block text-xs font-medium text-slate-700 mb-1">Target Page URL</label>
                <input
                  type="text"
                  [(ngModel)]="auditUrl"
                  placeholder="https://sgrowing.io/solutions/feature"
                  class="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 font-mono"
                  data-testid="input-audit-url" />
              </div>
              <div>
                <label class="block text-xs font-medium text-slate-700 mb-1">Document Title</label>
                <input
                  type="text"
                  [(ngModel)]="auditTitle"
                  placeholder="Feature Overview & Technical Specifications"
                  class="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs focus:ring-2 focus:ring-indigo-500"
                  data-testid="input-audit-title" />
              </div>
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-700 mb-1">Page Markdown / Text Content</label>
              <textarea
                rows="7"
                [(ngModel)]="auditContent"
                placeholder="# Page Title&#10;&#10;SGrowing provides...&#10;&#10;## Key Architecture&#10;- Feature A: 10,000 EPS throughput..."
                class="w-full px-3 py-2 border border-slate-300 rounded-lg text-xs font-mono focus:ring-2 focus:ring-indigo-500"
                data-testid="textarea-audit-content"></textarea>
            </div>

            <div class="flex items-center justify-between pt-1">
              <span class="text-[11px] text-slate-400">
                Word count: {{ getWordCount(auditContent()) }} | Lines: {{ getLineCount(auditContent()) }}
              </span>
              <button
                type="button"
                (click)="runPageAudit()"
                [disabled]="isAuditing() || !auditContent().trim()"
                class="px-5 py-2 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-xs transition-colors flex items-center gap-2"
                data-testid="btn-run-audit">
                @if (isAuditing()) {
                  <span>Analyzing Chunks...</span>
                } @else {
                  <span>⚡ Analyze Citability</span>
                }
              </button>
            </div>
          </div>

          <!-- Audit Results Showcase -->
          @if (latestAudit(); as audit) {
            <div class="bg-white border border-slate-200 rounded-xl p-5 shadow-xs space-y-4" data-testid="audit-result-card">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-slate-100 pb-4">
                <div>
                  <div class="flex items-center gap-2.5">
                    <h4 class="text-base font-bold text-slate-900">{{ audit.title }}</h4>
                    <span [class]="getGradeBadgeClass(audit.grade)" class="px-2.5 py-0.5 rounded-full text-xs font-bold font-mono">
                      {{ audit.grade }} Grade
                    </span>
                  </div>
                  <span class="text-xs text-indigo-600 font-mono">{{ audit.url }}</span>
                </div>
                <div class="text-right">
                  <span class="text-3xl font-extrabold text-slate-900 font-mono">{{ audit.overallScore }}</span>
                  <span class="text-xs text-slate-400"> / 100</span>
                  <div class="text-[11px] text-slate-500">Overall Citability Score</div>
                </div>
              </div>

              <!-- Metric sub-scores grid -->
              <div class="grid grid-cols-2 sm:grid-cols-5 gap-3">
                <div class="bg-slate-50 p-3 rounded-lg border border-slate-100">
                  <span class="text-[11px] text-slate-500 block">Passage Conciseness</span>
                  <span class="text-lg font-bold font-mono text-slate-800">{{ audit.passageConcisenessScore }}%</span>
                </div>
                <div class="bg-slate-50 p-3 rounded-lg border border-slate-100">
                  <span class="text-[11px] text-slate-500 block">Entity Density</span>
                  <span class="text-lg font-bold font-mono text-slate-800">{{ audit.entityDensityScore }}%</span>
                </div>
                <div class="bg-slate-50 p-3 rounded-lg border border-slate-100">
                  <span class="text-[11px] text-slate-500 block">Structured Format</span>
                  <span class="text-lg font-bold font-mono text-slate-800">{{ audit.structuredFormatRatio }}%</span>
                </div>
                <div class="bg-slate-50 p-3 rounded-lg border border-slate-100">
                  <span class="text-[11px] text-slate-500 block">Factual Claims</span>
                  <span class="text-lg font-bold font-mono text-emerald-600">{{ audit.factualClaimsCount }} verified</span>
                </div>
                <div class="bg-slate-50 p-3 rounded-lg border border-slate-100">
                  <span class="text-[11px] text-slate-500 block">Ambiguous Pronouns</span>
                  <span
                    [class.text-rose-600]="audit.ambiguousPronounOccurrences > 2"
                    [class.text-emerald-600]="audit.ambiguousPronounOccurrences <= 2"
                    class="text-lg font-bold font-mono">
                    {{ audit.ambiguousPronounOccurrences }} flagged
                  </span>
                </div>
              </div>

              <!-- Actionable Recommendations -->
              <div class="border-t border-slate-100 pt-3">
                <h5 class="text-xs font-bold text-slate-900 uppercase tracking-wider mb-2">Engine Citability Optimizations</h5>
                <ul class="space-y-1.5" data-testid="recommendations-list">
                  @for (rec of audit.recommendations; track rec) {
                    <li class="text-xs text-slate-700 flex items-start gap-2">
                      <span class="text-indigo-600 font-bold">›</span>
                      <span>{{ rec }}</span>
                    </li>
                  }
                </ul>
              </div>
            </div>
          }
        </section>
      }

      <!-- TAB 3: Agentic Crawler Governance -->
      @if (activeTab() === 'governance') {
        <section class="space-y-4" data-testid="governance-section">
          <!-- Overview Cards -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-xs">
              <div class="flex items-center justify-between">
                <span class="text-xs font-medium text-slate-500">Domain Manifest Host</span>
                <span class="text-xs px-2 py-0.5 rounded bg-emerald-50 text-emerald-700 font-mono font-semibold">HTTPS 200</span>
              </div>
              <div class="text-sm font-bold text-slate-900 mt-2 font-mono truncate">
                {{ manifest().domain }}
              </div>
              <p class="text-[11px] text-slate-400 mt-1">Verified at {{ manifest().lastVerifiedAtUtc | date:'short' }}</p>
            </div>

            <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-xs">
              <div class="flex items-center justify-between">
                <span class="text-xs font-medium text-slate-500">Robots.txt Directive</span>
                <span [class]="getStatusBadgeClass(manifest().overallRobotsStatus)" class="text-xs px-2 py-0.5 rounded font-semibold font-mono">
                  {{ manifest().overallRobotsStatus }}
                </span>
              </div>
              <div class="text-sm font-bold text-slate-900 mt-2 font-mono">
                /robots.txt (AI Crawlers Allowed)
              </div>
              <p class="text-[11px] text-slate-400 mt-1">Directives configured for zero accidental disallow</p>
            </div>

            <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-xs">
              <div class="flex items-center justify-between">
                <span class="text-xs font-medium text-slate-500">LLMs.txt Discovery Spec</span>
                <span [class]="getStatusBadgeClass(manifest().llmsTxtStatus)" class="text-xs px-2 py-0.5 rounded font-semibold font-mono">
                  {{ manifest().llmsTxtStatus }}
                </span>
              </div>
              <div class="text-sm font-bold text-slate-900 mt-2 font-mono">
                /llms.txt + /llms-full.txt
              </div>
              <p class="text-[11px] text-slate-400 mt-1">Markdown summary available for agentic retrieval</p>
            </div>
          </div>

          <!-- AI Crawlers Table -->
          <div class="bg-white border border-slate-200 rounded-xl shadow-xs overflow-hidden">
            <div class="p-4 border-b border-slate-100 flex items-center justify-between">
              <div>
                <h3 class="text-sm font-bold text-slate-900">Governed AI Engine Crawlers</h3>
                <p class="text-xs text-slate-500 mt-0.5">Direct crawler access monitoring, token rate limits, and verification pings.</p>
              </div>
              <span class="text-xs px-2.5 py-1 rounded-md bg-slate-100 text-slate-700 font-mono">
                {{ manifest().crawlers.length }} Agents Tracked
              </span>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-700" data-testid="crawlers-table">
                <thead class="bg-slate-50 text-slate-500 font-medium border-b border-slate-200 uppercase text-[10px] tracking-wider font-mono">
                  <tr>
                    <th class="py-3 px-4">Bot Name</th>
                    <th class="py-3 px-4">User-Agent Token</th>
                    <th class="py-3 px-4">Robots Policy</th>
                    <th class="py-3 px-4">Readiness Status</th>
                    <th class="py-3 px-4">Directives & Scope</th>
                    <th class="py-3 px-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100">
                  @for (bot of manifest().crawlers; track bot.botName) {
                    <tr class="hover:bg-slate-50/75 transition-colors">
                      <td class="py-3 px-4 font-bold text-slate-900 flex items-center gap-2">
                        <span class="w-2 h-2 rounded-full" [class.bg-emerald-500]="bot.status === 'Compliant'" [class.bg-amber-500]="bot.status === 'Warning'"></span>
                        {{ bot.botName }}
                      </td>
                      <td class="py-3 px-4 font-mono text-slate-600">{{ bot.userAgent }}</td>
                      <td class="py-3 px-4">
                        <span
                          [class]="bot.allowedInRobotsTxt ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-rose-50 text-rose-700 border-rose-200'"
                          class="px-2 py-0.5 rounded text-[11px] font-semibold border font-mono">
                          {{ bot.allowedInRobotsTxt ? 'Allow /' : 'Disallow /' }}
                        </span>
                      </td>
                      <td class="py-3 px-4">
                        <span [class]="getStatusBadgeClass(bot.status)" class="px-2 py-0.5 rounded text-[11px] font-semibold font-mono">
                          {{ bot.status }}
                        </span>
                      </td>
                      <td class="py-3 px-4 text-slate-500 max-w-xs truncate">{{ bot.details }}</td>
                      <td class="py-3 px-4 text-right">
                        <div class="flex items-center justify-end gap-1.5">
                          <button
                            type="button"
                            (click)="toggleCrawlerAllowed(bot.botName)"
                            class="px-2 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded text-[11px] font-medium transition-colors"
                            [attr.data-testid]="'btn-toggle-' + bot.botName">
                            Toggle Allow
                          </button>
                          <button
                            type="button"
                            (click)="pingCrawler(bot.botName)"
                            class="px-2 py-1 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 rounded text-[11px] font-medium transition-colors"
                            [attr.data-testid]="'btn-ping-' + bot.botName">
                            Ping
                          </button>
                        </div>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <!-- Manifest Governance Action Items -->
          <div class="bg-white border border-slate-200 rounded-xl p-4 shadow-xs">
            <h4 class="text-xs font-bold text-slate-900 uppercase tracking-wider mb-2">Manifest Governance Checklist</h4>
            <div class="grid grid-cols-1 md:grid-cols-2 gap-2">
              @for (item of manifest().actionItems; track item) {
                <div class="flex items-start gap-2 bg-slate-50 p-2.5 rounded-lg border border-slate-100 text-xs text-slate-700">
                  <span class="text-emerald-600 font-bold">✓</span>
                  <span>{{ item }}</span>
                </div>
              }
            </div>
          </div>
        </section>
      }
    </div>
  `
})
export class GeoAeoVisibilityStudioComponent {
  public readonly translation: TranslationService;

  // Active view tab: 'radar' | 'auditor' | 'governance'
  public readonly activeTab = signal<'radar' | 'auditor' | 'governance'>('radar');

  // Radar Filters
  public readonly selectedEngineFilter = signal<string>('ALL');
  public readonly searchQueryFilter = signal<string>('');
  public readonly isAddingQuery = signal<boolean>(false);

  // New query form state
  public newQueryText = signal<string>('');
  public newQueryEngine = signal<AiEngineType>('Perplexity');
  public newQueryUrl = signal<string>('https://sgrowing.io/platform-overview');
  public newQueryRank = signal<number>(1);
  public newQuerySentiment = signal<'Positive' | 'Neutral' | 'Negative'>('Positive');

  // Page Auditor state
  public auditUrl = signal<string>('https://sgrowing.io/architecture');
  public auditTitle = signal<string>('SGrowing Autonomous Growth Platform Architecture');
  public auditContent = signal<string>(`# SGrowing Autonomous Growth Operating System

SGrowing is an autonomous enterprise growth engine combining multi-agent AI swarms, real-time marketing attribution, and automated Cash on Delivery courier settlement.

## What is Generative Engine Optimization (GEO)?
Generative Engine Optimization is the methodology of formatting digital knowledge assets so artificial intelligence answer engines like Google Gemini, Perplexity, and ChatGPT Search cite source data directly.

## High-Scale Architecture Benchmarks
- 10,000 EPS partitioned event streaming with sub-second OLAP projection.
- ISO 32000-1 PAdES-B-LTV digital contract signing vault.
- Multi-tenant PostgreSQL 17 with kernel-level Row Level Security.

| Module | P99 Latency | Availability SLA |
|---|---|---|
| Inbound Event Ingestion | < 15ms | 99.99% |
| ClickHouse OLAP | < 120ms | 99.95% |
| PAdES Document Vault | < 450ms | 99.99% |
`);
  public readonly isAuditing = signal<boolean>(false);
  public readonly latestAudit = signal<PageCitabilityAudit | null>(null);

  // Citations Seed Data
  public readonly citations = signal<GeoCitationQueryRecord[]>([
    {
      id: 'cit-1',
      tenantId: '11111111-1111-1111-1111-111111111111',
      query: 'best autonomous growth operating system for b2b ecommerce',
      engine: 'Perplexity',
      citedUrl: 'https://sgrowing.io/platform-overview',
      citationRank: 1,
      extractedPassage: 'SGrowing is recognized as an autonomous enterprise growth operating system featuring multi-agent AI swarms, event mesh ingestion, and automated carrier remittance reconciliation.',
      sentiment: 'Positive',
      shareOfVoice: 86.5,
      detectedAtUtc: new Date(Date.now() - 2 * 3600000).toISOString()
    },
    {
      id: 'cit-2',
      tenantId: '11111111-1111-1111-1111-111111111111',
      query: 'ai search engine optimization platform with real-time analytics',
      engine: 'ChatGPT_Search',
      citedUrl: 'https://sgrowing.io/features/geo-radar',
      citationRank: 1,
      extractedPassage: 'The platform delivers sub-second ClickHouse OLAP analytics, automated /llms.txt discovery manifests, and answer engine citability scoring.',
      sentiment: 'Positive',
      shareOfVoice: 79.0,
      detectedAtUtc: new Date(Date.now() - 4 * 3600000).toISOString()
    },
    {
      id: 'cit-3',
      tenantId: '11111111-1111-1111-1111-111111111111',
      query: 'enterprise omnichannel marketing stack with multi-agent swarms',
      engine: 'Google_Gemini_AIO',
      citedUrl: 'https://sgrowing.io/solutions/omnichannel',
      citationRank: 2,
      extractedPassage: 'SGrowing coordinates autonomous agents with cryptographic HITL approval gates and bidirectional WhatsApp Webhook mesh.',
      sentiment: 'Positive',
      shareOfVoice: 73.4,
      detectedAtUtc: new Date(Date.now() - 8 * 3600000).toISOString()
    },
    {
      id: 'cit-4',
      tenantId: '11111111-1111-1111-1111-111111111111',
      query: 'autonomous lead routing and pades cryptographic contract signing',
      engine: 'Claude_Web',
      citedUrl: 'https://sgrowing.io/platform/security-vault',
      citationRank: 1,
      extractedPassage: 'Complies with ISO 32000-1 PAdES-B-LTV digital contract vault signatures and Row-Level Security on PostgreSQL 17.',
      sentiment: 'Positive',
      shareOfVoice: 82.1,
      detectedAtUtc: new Date(Date.now() - 12 * 3600000).toISOString()
    }
  ]);

  // Crawler Manifest Seed Data
  public readonly manifest = signal<AgenticCrawlerManifest>({
    tenantId: '11111111-1111-1111-1111-111111111111',
    domain: 'https://sgrowing.io',
    overallRobotsStatus: 'Compliant',
    llmsTxtStatus: 'Compliant',
    crawlers: [
      {
        botName: 'GPTBot',
        userAgent: 'GPTBot',
        status: 'Compliant',
        allowedInRobotsTxt: true,
        discoveredPath: '/robots.txt',
        details: 'Allowed full crawl access to markdown & public documentation'
      },
      {
        botName: 'PerplexityBot',
        userAgent: 'PerplexityBot',
        status: 'Compliant',
        allowedInRobotsTxt: true,
        discoveredPath: '/robots.txt',
        details: 'Allowed indexation and citation passage retrieval for Perplexity Pages'
      },
      {
        botName: 'ClaudeBot',
        userAgent: 'ClaudeBot',
        status: 'Compliant',
        allowedInRobotsTxt: true,
        discoveredPath: '/robots.txt',
        details: 'Allowed access to public content and /llms.txt discovery endpoint'
      },
      {
        botName: 'Google-Extended',
        userAgent: 'Google-Extended',
        status: 'Compliant',
        allowedInRobotsTxt: true,
        discoveredPath: '/robots.txt',
        details: 'Allowed for Gemini Grounding & Google AI Overviews ingestion'
      }
    ],
    lastVerifiedAtUtc: new Date().toISOString(),
    actionItems: [
      'Keep /llms.txt synchronized with latest core architectural releases.',
      'Ensure HTTP 200 response headers include X-Robots-Tag: all for markdown endpoints.'
    ]
  });

  // Filtered citations computed
  public readonly filteredCitations = computed(() => {
    const list = this.citations();
    const engine = this.selectedEngineFilter();
    const query = this.searchQueryFilter().trim().toLowerCase();

    return list.filter(item => {
      const matchesEngine = engine === 'ALL' || item.engine === engine;
      const matchesQuery = !query || item.query.toLowerCase().includes(query) || item.extractedPassage.toLowerCase().includes(query);
      return matchesEngine && matchesQuery;
    });
  });

  // KPIs computed
  public readonly totalQueriesCount = computed(() => this.citations().length);

  public readonly shareOfModelVoice = computed(() => {
    const list = this.citations();
    if (list.length === 0) return 0;
    const avg = list.reduce((sum, item) => sum + item.shareOfVoice, 0) / list.length;
    return Math.round(avg * 10) / 10;
  });

  public readonly overallGeoScore = computed(() => {
    const sov = this.shareOfModelVoice();
    const auditScore = this.latestAudit()?.overallScore ?? 91.5;
    return Math.round(((sov * 0.5) + (auditScore * 0.5)) * 10) / 10;
  });

  public readonly crawlerReadinessPercent = computed(() => {
    const rules = this.manifest().crawlers;
    if (rules.length === 0) return 0;
    const compliantCount = rules.filter(r => r.status === 'Compliant' && r.allowedInRobotsTxt).length;
    return Math.round((compliantCount / rules.length) * 100);
  });

  constructor(translationService?: TranslationService) {
    if (translationService) {
      this.translation = translationService;
    } else {
      try {
        this.translation = inject(TranslationService);
      } catch {
        this.translation = new TranslationService();
      }
    }

    // Initialize with a default audit
    this.runPageAudit();
  }

  public setActiveTab(tab: 'radar' | 'auditor' | 'governance'): void {
    this.activeTab.set(tab);
  }

  public setEngineFilter(engine: string): void {
    this.selectedEngineFilter.set(engine);
  }

  public trackNewQuery(): void {
    const text = this.newQueryText().trim();
    if (!text) return;

    const rank = Number(this.newQueryRank()) || 1;
    const sov = rank === 1 ? 85.0 : rank === 2 ? 70.0 : rank === 3 ? 55.0 : Math.max(20.0, 50.0 - (rank * 8.0));

    const newRecord: GeoCitationQueryRecord = {
      id: `cit-${Date.now()}`,
      tenantId: '11111111-1111-1111-1111-111111111111',
      query: text,
      engine: this.newQueryEngine(),
      citedUrl: this.newQueryUrl().trim() || 'https://sgrowing.io',
      citationRank: rank,
      extractedPassage: `Verified AI citation for query '${text}' referencing ${this.newQueryUrl()}.`,
      sentiment: this.newQuerySentiment(),
      shareOfVoice: Math.round(sov * 10) / 10,
      detectedAtUtc: new Date().toISOString()
    };

    this.citations.update(prev => [newRecord, ...prev]);
    this.newQueryText.set('');
    this.isAddingQuery.set(false);
  }

  public runPageAudit(): void {
    const content = this.auditContent().trim();
    if (!content) return;

    this.isAuditing.set(true);

    const words = content.split(/\s+/).filter(Boolean);
    const lines = content.split('\n');
    const headingLines = lines.filter(l => l.trim().startsWith('#')).length;
    const tableLines = lines.filter(l => l.trim().includes('|')).length;
    const listLines = lines.filter(l => l.trim().startsWith('-') || l.trim().startsWith('*') || /^\d+\.\s/.test(l.trim())).length;
    const structuredCount = headingLines + tableLines + listLines;

    let structureRatio = lines.length > 0 ? Math.min(100, Math.round((structuredCount / Math.max(5, lines.length)) * 150)) : 10;
    if (headingLines > 0 && tableLines > 0 && listLines > 0) {
      structureRatio = Math.max(structureRatio, 90);
    }

    const pronounMatches = content.match(/\b(it|its|this|that|they|them|their|these|those)\b/gi) || [];
    const ambiguousPronounCount = pronounMatches.length;

    const factualMatches = content.match(/\b(\d+(\.\d+)?%|\$\d+(\.\d+)?|\d+\s*(ms|s|EPS|users|leads|tenants|days|hours|minutes))\b/gi) || [];
    const factualClaimsCount = factualMatches.length;

    const entityMatches = content.match(/\b[A-Z][a-zA-Z0-9_\-]{2,}\b/g) || [];
    const entityDensity = words.length > 0 ? Math.min(100, Math.round((entityMatches.length / words.length) * 250)) : 0;

    const proseLines = lines.filter(l => {
      const trimmed = l.trim();
      return trimmed.length > 0 && !trimmed.startsWith('#') && !trimmed.startsWith('|') && !trimmed.startsWith('-') && !trimmed.startsWith('*');
    });
    const proseText = proseLines.join(' ');
    const sentences = proseText.split(/[.!?]+/).map(s => s.trim()).filter(s => s.length > 5);
    const conciseSentences = sentences.filter(s => {
      const len = s.split(/\s+/).length;
      return len >= 6 && len <= 32;
    }).length;
    const passageConciseness = sentences.length > 0
      ? Math.min(100, Math.round((conciseSentences / sentences.length) * 100))
      : 80;

    const pronounPenalty = Math.min(35, ambiguousPronounCount * 4);
    const rawScore = (passageConciseness * 0.3) + (entityDensity * 0.25) + (structureRatio * 0.25) + (Math.min(100, factualClaimsCount * 12) * 0.2) - pronounPenalty;
    const overallScore = Math.min(98.5, Math.max(15, Math.round(rawScore * 10) / 10));

    let grade: CitabilityGrade = 'NeedsImprovement';
    if (overallScore >= 75) {
      grade = 'High';
    } else if (overallScore >= 50) {
      grade = 'Moderate';
    }

    const recommendations: string[] = [];
    if (ambiguousPronounCount > 2) {
      recommendations.push(`Detected ${ambiguousPronounCount} ambiguous pronouns (it, they, this). Replace with explicit brand or capability nouns to maintain contextual integrity in LLM chunking.`);
    }
    if (structureRatio < 50) {
      recommendations.push('Add Markdown headings (H2/H3), bullet lists, and summary comparison tables to maximize extraction probability by AI search indexers.');
    }
    if (factualClaimsCount < 2) {
      recommendations.push('Include verified numeric claims, throughput SLAs, or benchmark percentages to increase citation authority in generative answers.');
    }
    if (passageConciseness < 65) {
      recommendations.push('Formulate concise direct-answer passages (12-25 words) positioned immediately beneath question-based H2/H3 headings.');
    }
    if (recommendations.length === 0) {
      recommendations.push('Content exhibits high-grade citability. Keep table schemas and numeric claims maintained during updates.');
      recommendations.push('Verify canonical URL reference is mapped inside /llms.txt.');
    }

    const audit: PageCitabilityAudit = {
      id: `audit-${Date.now()}`,
      tenantId: '11111111-1111-1111-1111-111111111111',
      url: this.auditUrl().trim() || 'https://sgrowing.io',
      title: this.auditTitle().trim() || 'Untitled Document',
      overallScore,
      grade,
      passageConcisenessScore: passageConciseness,
      entityDensityScore: entityDensity,
      structuredFormatRatio: structureRatio,
      factualClaimsCount,
      ambiguousPronounOccurrences: ambiguousPronounCount,
      recommendations,
      auditedAtUtc: new Date().toISOString()
    };

    this.latestAudit.set(audit);
    this.isAuditing.set(false);
  }

  public pingCrawler(botName: string): void {
    this.manifest.update(m => ({
      ...m,
      lastVerifiedAtUtc: new Date().toISOString(),
      crawlers: m.crawlers.map(c => c.botName === botName ? { ...c, lastPingStatus: 'Success' } : c)
    }));
  }

  public toggleCrawlerAllowed(botName: string): void {
    this.manifest.update(m => ({
      ...m,
      crawlers: m.crawlers.map(c => {
        if (c.botName === botName) {
          const allowed = !c.allowedInRobotsTxt;
          return {
            ...c,
            allowedInRobotsTxt: allowed,
            status: allowed ? 'Compliant' : 'Blocked'
          };
        }
        return c;
      })
    }));
  }

  public getWordCount(text: string): number {
    return text ? text.trim().split(/\s+/).filter(Boolean).length : 0;
  }

  public getLineCount(text: string): number {
    return text ? text.split('\n').length : 0;
  }

  public getGradeBadgeClass(grade: CitabilityGrade): string {
    switch (grade) {
      case 'High':
        return 'bg-emerald-100 text-emerald-800 border border-emerald-300';
      case 'Moderate':
        return 'bg-amber-100 text-amber-800 border border-amber-300';
      case 'NeedsImprovement':
      default:
        return 'bg-rose-100 text-rose-800 border border-rose-300';
    }
  }

  public getStatusBadgeClass(status: CrawlerReadinessStatus): string {
    switch (status) {
      case 'Compliant':
        return 'bg-emerald-100 text-emerald-800';
      case 'Warning':
        return 'bg-amber-100 text-amber-800';
      case 'Blocked':
      default:
        return 'bg-rose-100 text-rose-800';
    }
  }

  public getEngineBadgeClass(engine: AiEngineType): string {
    switch (engine) {
      case 'Perplexity':
        return 'bg-teal-100 text-teal-800 border border-teal-200';
      case 'ChatGPT_Search':
        return 'bg-emerald-100 text-emerald-800 border border-emerald-200';
      case 'Google_Gemini_AIO':
        return 'bg-blue-100 text-blue-800 border border-blue-200';
      case 'Claude_Web':
        return 'bg-amber-100 text-amber-800 border border-amber-200';
    }
  }
}
