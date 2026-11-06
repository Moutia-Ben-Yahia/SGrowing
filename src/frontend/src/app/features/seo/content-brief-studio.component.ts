import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface OutlineSection {
  heading: string;
  level: 'h2' | 'h3';
  purpose: string;
  suggestedQuestions: string[];
  recommendedWords: number;
}

export interface SemanticKeyword {
  term: string;
  suggestedFrequency: number;
  targetDensityPercent: number;
  intent: string;
}

export interface CompetitorBenchmark {
  domain: string;
  wordCount: number;
  h2Count: number;
  authorityScore: number;
}

export interface ContentBrief {
  briefId: string;
  primaryKeyword: string;
  targetAudience: string;
  searchIntent: 'Informational' | 'Commercial' | 'Transactional' | 'Navigational';
  targetWordCount: { min: number; max: number };
  targetReadingGrade: string;
  generatedAt: string;
  outlineSections: OutlineSection[];
  semanticKeywords: SemanticKeyword[];
  competitorBenchmarks: CompetitorBenchmark[];
  recommendedSchema: string;
  targetContentScore: number;
}

export interface KeywordMatchReport {
  keyword: string;
  count: number;
  densityPercent: number;
  status: 'Optimal' | 'Low' | 'High';
}

export interface RecommendationItem {
  type: 'critical' | 'improvement' | 'positive';
  text: string;
}

export interface DraftGradeReport {
  wordCount: number;
  charCount: number;
  readingEaseScore: number;
  gradeLevel: string;
  letterGrade: 'A+' | 'A' | 'B' | 'C' | 'D' | 'F';
  primaryKeywordDensity: number;
  secondaryKeywordMatches: KeywordMatchReport[];
  questionsAnswered: string[];
  questionsMissing: string[];
  recommendations: RecommendationItem[];
  fleschAnalysis: string;
}

export interface EngineReadiness {
  engine: string;
  status: 'Optimized' | 'Eligible' | 'Needs Improvement';
  details: string;
}

export interface GeoCitabilityAudit {
  auditId: string;
  concisenessScore: number;
  entityDensityPercent: number;
  qaStructureRatio: number;
  ambiguousPronounCount: number;
  directAnswerClarity: 'High' | 'Medium' | 'Low';
  formattedCitationSnippet: string;
  extractedEntities: string[];
  aeoRecommendation: string;
  perEngineReadiness: EngineReadiness[];
}

@Component({
  selector: 'app-content-brief-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="content-brief-studio p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="content-brief-studio">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-emerald-400">
            <span class="inline-block w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            AI Search & Generative Citability Engine • Horizon 60
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            AI Content Brief & GEO/AEO Citability Studio
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Generate research-backed content briefs, grade draft readability & keyword density, audit LLM citability, and export structured /llms.txt manifests.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="loadSampleDraft()"
            class="px-3.5 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-lg text-xs font-medium transition-colors flex items-center gap-1.5"
            data-testid="load-sample-btn"
          >
            <span>📝</span> Load Sample Draft
          </button>
          <button
            (click)="generateBrief()"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition-colors flex items-center gap-1.5 shadow-lg shadow-emerald-600/20"
            data-testid="top-generate-brief-btn"
          >
            <span>⚡</span> Generate Full Brief
          </button>
        </div>
      </div>

      <!-- Navigation Tabs -->
      <div class="flex items-center gap-2 border-b border-slate-700/70 pb-3" data-testid="studio-tabs">
        <button
          (click)="setActiveTab('brief')"
          [class.bg-slate-800]="activeTab() === 'brief'"
          [class.text-white]="activeTab() === 'brief'"
          [class.border-emerald-500]="activeTab() === 'brief'"
          [class.text-slate-400]="activeTab() !== 'brief'"
          class="px-4 py-2 rounded-lg text-sm font-medium border border-transparent hover:bg-slate-800/60 hover:text-white transition-all flex items-center gap-2"
          data-testid="tab-brief"
        >
          <span>📋</span> Content Brief
          @if (currentBrief()) {
            <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
          }
        </button>

        <button
          (click)="setActiveTab('grader')"
          [class.bg-slate-800]="activeTab() === 'grader'"
          [class.text-white]="activeTab() === 'grader'"
          [class.border-emerald-500]="activeTab() === 'grader'"
          [class.text-slate-400]="activeTab() !== 'grader'"
          class="px-4 py-2 rounded-lg text-sm font-medium border border-transparent hover:bg-slate-800/60 hover:text-white transition-all flex items-center gap-2"
          data-testid="tab-grader"
        >
          <span>✍️</span> Draft Grader
          @if (draftGradeReport()) {
            <span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-300">
              {{ draftGradeReport()?.letterGrade }}
            </span>
          }
        </button>

        <button
          (click)="setActiveTab('geo')"
          [class.bg-slate-800]="activeTab() === 'geo'"
          [class.text-white]="activeTab() === 'geo'"
          [class.border-emerald-500]="activeTab() === 'geo'"
          [class.text-slate-400]="activeTab() !== 'geo'"
          class="px-4 py-2 rounded-lg text-sm font-medium border border-transparent hover:bg-slate-800/60 hover:text-white transition-all flex items-center gap-2"
          data-testid="tab-geo"
        >
          <span>🤖</span> GEO Citability Audit
          @if (geoCitabilityAudit()) {
            <span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-indigo-500/20 text-indigo-300">
              {{ geoCitabilityAudit()?.concisenessScore }}%
            </span>
          }
        </button>

        <button
          (click)="setActiveTab('llms')"
          [class.bg-slate-800]="activeTab() === 'llms'"
          [class.text-white]="activeTab() === 'llms'"
          [class.border-emerald-500]="activeTab() === 'llms'"
          [class.text-slate-400]="activeTab() !== 'llms'"
          class="px-4 py-2 rounded-lg text-sm font-medium border border-transparent hover:bg-slate-800/60 hover:text-white transition-all flex items-center gap-2"
          data-testid="tab-llms"
        >
          <span>📄</span> llms.txt Export
        </button>
      </div>

      <!-- TAB 1: CONTENT BRIEF -->
      @if (activeTab() === 'brief') {
        <div class="space-y-6" data-testid="view-brief">
          <!-- Brief Configuration Form -->
          <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-4">
            <h2 class="text-sm font-semibold uppercase tracking-wider text-slate-300 flex items-center gap-2">
              <span>🎯</span> Brief Parameters & Target Persona
            </h2>

            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
              <!-- Primary Keyword -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Primary Keyword</label>
                <input
                  type="text"
                  [ngModel]="primaryKeyword()"
                  (ngModelChange)="primaryKeyword.set($event)"
                  placeholder="e.g. enterprise dynamic pricing"
                  class="w-full px-3 py-2 bg-slate-900 border border-slate-700 rounded-lg text-white text-sm focus:outline-none focus:border-emerald-500"
                  data-testid="input-primary-keyword"
                />
              </div>

              <!-- Target Audience -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Target Audience</label>
                <input
                  type="text"
                  [ngModel]="targetAudience()"
                  (ngModelChange)="targetAudience.set($event)"
                  placeholder="e.g. E-Commerce Directors"
                  class="w-full px-3 py-2 bg-slate-900 border border-slate-700 rounded-lg text-white text-sm focus:outline-none focus:border-emerald-500"
                  data-testid="input-target-audience"
                />
              </div>

              <!-- Search Intent -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Search Intent</label>
                <select
                  [ngModel]="searchIntent()"
                  (ngModelChange)="searchIntent.set($event)"
                  class="w-full px-3 py-2 bg-slate-900 border border-slate-700 rounded-lg text-white text-sm focus:outline-none focus:border-emerald-500"
                  data-testid="select-search-intent"
                >
                  <option value="Informational">Informational</option>
                  <option value="Commercial">Commercial</option>
                  <option value="Transactional">Transactional</option>
                  <option value="Navigational">Navigational</option>
                </select>
              </div>

              <!-- Action Button -->
              <div class="flex items-end">
                <button
                  (click)="generateBrief()"
                  class="w-full px-4 py-2.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center justify-center gap-2 shadow-lg shadow-emerald-600/20"
                  data-testid="btn-generate-brief"
                >
                  <span>✨</span> Generate Brief
                </button>
              </div>
            </div>

            <!-- Secondary Keywords Input -->
            <div>
              <label class="block text-xs font-medium text-slate-400 mb-1">
                Secondary Keywords (Comma-separated)
              </label>
              <input
                type="text"
                [ngModel]="secondaryKeywordsInput()"
                (ngModelChange)="secondaryKeywordsInput.set($event)"
                placeholder="e.g. cod checkout, ecom margin, pricing rules"
                class="w-full px-3 py-2 bg-slate-900 border border-slate-700 rounded-lg text-white text-sm focus:outline-none focus:border-emerald-500 font-mono"
                data-testid="input-secondary-keywords"
              />
              <div class="flex flex-wrap gap-1.5 mt-2">
                @for (kw of secondaryKeywordsList(); track kw) {
                  <span class="inline-flex items-center px-2 py-0.5 rounded text-xs bg-slate-700 text-slate-300">
                    # {{ kw }}
                  </span>
                }
              </div>
            </div>
          </div>

          <!-- Generated Brief Results -->
          @if (currentBrief(); as brief) {
            <!-- Key Targets Banner -->
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4" data-testid="brief-targets">
              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Target Word Count</span>
                <div class="text-2xl font-bold text-white mt-1 font-mono">
                  {{ brief.targetWordCount.min }} - {{ brief.targetWordCount.max }}
                </div>
                <span class="text-[11px] text-emerald-400 font-medium mt-1 block">SERP Top 10 Benchmark</span>
              </div>

              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Target Reading Grade</span>
                <div class="text-2xl font-bold text-indigo-400 mt-1">
                  {{ brief.targetReadingGrade }}
                </div>
                <span class="text-[11px] text-slate-400 mt-1 block">Optimal B2B executive clarity</span>
              </div>

              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Target Content Score</span>
                <div class="text-2xl font-bold text-emerald-400 mt-1 font-mono">
                  {{ brief.targetContentScore }} / 100
                </div>
                <span class="text-[11px] text-emerald-400 mt-1 block">Exceeds primary SERP competitor</span>
              </div>

              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Structured Data</span>
                <div class="text-lg font-bold text-amber-300 mt-1 font-mono">
                  {{ brief.recommendedSchema }}
                </div>
                <span class="text-[11px] text-slate-400 mt-1 block">Rich snippet verification</span>
              </div>
            </div>

            <!-- Outline Sections -->
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-4">
              <div class="flex items-center justify-between">
                <h3 class="text-base font-bold text-white flex items-center gap-2">
                  <span>📑</span> Recommended Article Outline ({{ brief.outlineSections.length }} Sections)
                </h3>
                <span class="text-xs text-slate-400">Engineered for Featured Snippets & AI Overviews</span>
              </div>

              <div class="space-y-3" data-testid="outline-sections-list">
                @for (sec of brief.outlineSections; track sec.heading; let idx = $index) {
                  <div class="bg-slate-900/80 border border-slate-700/70 rounded-lg p-4 space-y-2">
                    <div class="flex items-start justify-between gap-3">
                      <div class="flex items-center gap-2">
                        <span class="px-2 py-0.5 rounded text-xs font-mono font-bold uppercase"
                              [class.bg-emerald-500/20]="sec.level === 'h2'"
                              [class.text-emerald-300]="sec.level === 'h2'"
                              [class.bg-indigo-500/20]="sec.level === 'h3'"
                              [class.text-indigo-300]="sec.level === 'h3'">
                          {{ sec.level }}
                        </span>
                        <h4 class="text-sm font-semibold text-white">{{ sec.heading }}</h4>
                      </div>
                      <span class="text-xs text-slate-400 font-mono whitespace-nowrap">
                        ~{{ sec.recommendedWords }} words
                      </span>
                    </div>

                    <p class="text-xs text-slate-400 pl-8">
                      <strong class="text-slate-300">Purpose:</strong> {{ sec.purpose }}
                    </p>

                    @if (sec.suggestedQuestions.length > 0) {
                      <div class="pl-8 pt-1">
                        <span class="text-[11px] font-semibold text-slate-300 block mb-1">Must-Answer Questions:</span>
                        <ul class="list-disc list-inside space-y-0.5 text-xs text-slate-400">
                          @for (q of sec.suggestedQuestions; track q) {
                            <li>{{ q }}</li>
                          }
                        </ul>
                      </div>
                    }
                  </div>
                }
              </div>
            </div>

            <!-- Semantic Keywords & Density Table -->
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-4">
              <h3 class="text-base font-bold text-white flex items-center gap-2">
                <span>🔑</span> Semantic Entities & Target Keyword Densities
              </h3>

              <div class="overflow-x-auto">
                <table class="w-full text-left text-sm" data-testid="semantic-keywords-table">
                  <thead class="bg-slate-900 text-xs uppercase text-slate-400 font-semibold border-b border-slate-700">
                    <tr>
                      <th class="py-2.5 px-4">Entity / Term</th>
                      <th class="py-2.5 px-4">Search Intent</th>
                      <th class="py-2.5 px-4">Suggested Frequency</th>
                      <th class="py-2.5 px-4">Target Density</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-700/50 text-slate-300">
                    @for (kw of brief.semanticKeywords; track kw.term) {
                      <tr class="hover:bg-slate-900/50">
                        <td class="py-2.5 px-4 font-medium text-white">{{ kw.term }}</td>
                        <td class="py-2.5 px-4">
                          <span class="px-2 py-0.5 rounded text-xs bg-slate-700 text-slate-300">
                            {{ kw.intent }}
                          </span>
                        </td>
                        <td class="py-2.5 px-4 font-mono">{{ kw.suggestedFrequency }}x</td>
                        <td class="py-2.5 px-4 font-mono text-emerald-400">{{ kw.targetDensityPercent }}%</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>

            <!-- Competitor SERP Benchmarks -->
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-4">
              <h3 class="text-base font-bold text-white flex items-center gap-2">
                <span>📊</span> Competitor SERP Benchmarks
              </h3>

              <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
                @for (comp of brief.competitorBenchmarks; track comp.domain) {
                  <div class="bg-slate-900/80 border border-slate-700/60 rounded-lg p-3 space-y-1.5">
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-semibold text-emerald-400">{{ comp.domain }}</span>
                      <span class="text-[11px] font-mono text-slate-400">DR {{ comp.authorityScore }}</span>
                    </div>
                    <div class="text-sm font-bold text-white font-mono">{{ comp.wordCount }} words</div>
                    <div class="text-xs text-slate-400">{{ comp.h2Count }} sub-sections / H2s analyzed</div>
                  </div>
                }
              </div>
            </div>
          } @else {
            <!-- Empty State -->
            <div class="bg-slate-800/40 border border-dashed border-slate-700 rounded-xl p-12 text-center space-y-3" data-testid="brief-empty-state">
              <div class="text-4xl">📋</div>
              <h3 class="text-base font-bold text-white">No Brief Generated Yet</h3>
              <p class="text-sm text-slate-400 max-w-md mx-auto">
                Configure your primary keyword, audience, and intent above, then click "Generate Brief" to create an AI-optimized outline with entity benchmarks.
              </p>
              <button
                (click)="generateBrief()"
                class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-semibold transition-colors inline-flex items-center gap-2"
              >
                <span>⚡</span> Generate Brief Now
              </button>
            </div>
          }
        </div>
      }

      <!-- TAB 2: DRAFT GRADER -->
      @if (activeTab() === 'grader') {
        <div class="space-y-6" data-testid="view-grader">
          <!-- Editor Section -->
          <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-4">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <h2 class="text-sm font-semibold uppercase tracking-wider text-slate-300 flex items-center gap-2">
                <span>📝</span> Article Draft Editor
              </h2>
              <div class="flex items-center gap-3 text-xs font-mono text-slate-400">
                <span>Words: <strong class="text-emerald-400">{{ liveWordCount() }}</strong></span>
                <span>•</span>
                <span>Characters: <strong class="text-slate-300">{{ liveCharCount() }}</strong></span>
              </div>
            </div>

            <!-- Draft Title -->
            <div>
              <label class="block text-xs font-medium text-slate-400 mb-1">Article Title (H1)</label>
              <input
                type="text"
                [ngModel]="draftTitle()"
                (ngModelChange)="draftTitle.set($event)"
                placeholder="Article title..."
                class="w-full px-3 py-2 bg-slate-900 border border-slate-700 rounded-lg text-white text-base font-bold focus:outline-none focus:border-emerald-500"
                data-testid="input-draft-title"
              />
            </div>

            <!-- Draft Body Textarea -->
            <div>
              <label class="block text-xs font-medium text-slate-400 mb-1">Article Body (Markdown or Plain Text)</label>
              <textarea
                rows="10"
                [ngModel]="draftBody()"
                (ngModelChange)="draftBody.set($event)"
                placeholder="Paste or write your full article content here to analyze keyword densities, readability, and missing outline questions..."
                class="w-full px-3 py-2.5 bg-slate-900 border border-slate-700 rounded-lg text-slate-200 text-sm focus:outline-none focus:border-emerald-500 font-mono leading-relaxed"
                data-testid="textarea-draft-body"
              ></textarea>
            </div>

            <div class="flex items-center justify-between pt-2">
              <button
                (click)="loadSampleDraft()"
                class="text-xs text-indigo-400 hover:text-indigo-300 font-medium transition-colors"
              >
                Insert Pre-formatted Enterprise Draft ↺
              </button>

              <button
                (click)="gradeDraft()"
                class="px-5 py-2.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-emerald-600/20"
                data-testid="btn-grade-draft"
              >
                <span>📊</span> Grade Draft Now
              </button>
            </div>
          </div>

          <!-- Grade Report Results -->
          @if (draftGradeReport(); as report) {
            <!-- Grade Banner Cards -->
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4" data-testid="grade-kpis">
              <!-- Letter Grade -->
              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4 flex items-center justify-between">
                <div>
                  <span class="text-xs font-medium text-slate-400">Overall Grade</span>
                  <div class="text-3xl font-extrabold text-white mt-1" data-testid="kpi-letter-grade">
                    {{ report.letterGrade }}
                  </div>
                  <span class="text-[11px] text-slate-400 mt-0.5 block">
                    {{ report.letterGrade === 'A+' || report.letterGrade === 'A' ? 'Top Tier Quality' : 'Needs Optimization' }}
                  </span>
                </div>
                <div class="w-12 h-12 rounded-full flex items-center justify-center font-bold text-xl"
                     [class.bg-emerald-500/20]="report.letterGrade === 'A+' || report.letterGrade === 'A'"
                     [class.text-emerald-400]="report.letterGrade === 'A+' || report.letterGrade === 'A'"
                     [class.bg-amber-500/20]="report.letterGrade === 'B' || report.letterGrade === 'C'"
                     [class.text-amber-400]="report.letterGrade === 'B' || report.letterGrade === 'C'"
                     [class.bg-rose-500/20]="report.letterGrade === 'D' || report.letterGrade === 'F'"
                     [class.text-rose-400]="report.letterGrade === 'D' || report.letterGrade === 'F'">
                  {{ report.letterGrade }}
                </div>
              </div>

              <!-- Flesch Score -->
              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Flesch Reading Ease</span>
                <div class="text-3xl font-bold text-indigo-400 mt-1 font-mono" data-testid="kpi-flesch-score">
                  {{ report.readingEaseScore }}
                </div>
                <span class="text-[11px] text-slate-400 mt-0.5 block">{{ report.gradeLevel }}</span>
              </div>

              <!-- Word Count -->
              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Word Count</span>
                <div class="text-3xl font-bold text-white mt-1 font-mono" data-testid="kpi-word-count">
                  {{ report.wordCount }}
                </div>
                <span class="text-[11px] text-emerald-400 mt-0.5 block">
                  {{ report.charCount }} total characters
                </span>
              </div>

              <!-- Primary Keyword Density -->
              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Primary Keyword Density</span>
                <div class="text-3xl font-bold text-emerald-400 mt-1 font-mono" data-testid="kpi-density">
                  {{ report.primaryKeywordDensity }}%
                </div>
                <span class="text-[11px] text-slate-400 mt-0.5 block">Recommended: 0.8% - 2.0%</span>
              </div>
            </div>

            <!-- Questions Answered vs Missing Checklist -->
            <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
              <!-- Answered Questions -->
              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-3">
                <h3 class="text-sm font-bold text-emerald-400 flex items-center gap-2">
                  <span>✅</span> Answered Questions ({{ report.questionsAnswered.length }})
                </h3>
                <ul class="space-y-2 text-xs text-slate-300" data-testid="questions-answered-list">
                  @for (q of report.questionsAnswered; track q) {
                    <li class="flex items-start gap-2 bg-slate-900/60 p-2.5 rounded border border-slate-800">
                      <span class="text-emerald-400 font-bold">✓</span>
                      <span>{{ q }}</span>
                    </li>
                  }
                  @if (report.questionsAnswered.length === 0) {
                    <li class="text-slate-500 italic p-2">No target questions detected in current draft.</li>
                  }
                </ul>
              </div>

              <!-- Missing Questions -->
              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-3">
                <h3 class="text-sm font-bold text-amber-400 flex items-center gap-2">
                  <span>⚠️</span> Missing Questions / Topical Gaps ({{ report.questionsMissing.length }})
                </h3>
                <ul class="space-y-2 text-xs text-slate-300" data-testid="questions-missing-list">
                  @for (q of report.questionsMissing; track q) {
                    <li class="flex items-start gap-2 bg-slate-900/60 p-2.5 rounded border border-slate-800">
                      <span class="text-amber-400 font-bold">!</span>
                      <span>{{ q }}</span>
                    </li>
                  }
                  @if (report.questionsMissing.length === 0) {
                    <li class="text-emerald-400 italic p-2">All primary outline questions are covered!</li>
                  }
                </ul>
              </div>
            </div>

            <!-- Secondary Keywords Breakdown -->
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-3">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>🔍</span> Secondary Keywords Coverage
              </h3>

              <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
                @for (item of report.secondaryKeywordMatches; track item.keyword) {
                  <div class="bg-slate-900/80 border border-slate-700/60 rounded-lg p-3 space-y-1">
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-semibold text-white">{{ item.keyword }}</span>
                      <span class="px-1.5 py-0.5 rounded text-[10px] font-bold"
                            [class.bg-emerald-500/20]="item.status === 'Optimal'"
                            [class.text-emerald-300]="item.status === 'Optimal'"
                            [class.bg-amber-500/20]="item.status === 'Low'"
                            [class.text-amber-300]="item.status === 'Low'"
                            [class.bg-rose-500/20]="item.status === 'High'"
                            [class.text-rose-300]="item.status === 'High'">
                        {{ item.status }}
                      </span>
                    </div>
                    <div class="text-xs text-slate-400 font-mono">
                      {{ item.count }} mentions ({{ item.densityPercent }}%)
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Recommendations -->
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-3">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>💡</span> Algorithmic Content Recommendations
              </h3>

              <div class="space-y-2" data-testid="recommendations-list">
                @for (rec of report.recommendations; track rec.text) {
                  <div class="p-3 rounded-lg border text-xs flex items-start gap-2.5"
                       [class.bg-emerald-950/20]="rec.type === 'positive'"
                       [class.border-emerald-800/50]="rec.type === 'positive'"
                       [class.text-emerald-300]="rec.type === 'positive'"
                       [class.bg-amber-950/20]="rec.type === 'improvement'"
                       [class.border-amber-800/50]="rec.type === 'improvement'"
                       [class.text-amber-300]="rec.type === 'improvement'"
                       [class.bg-rose-950/20]="rec.type === 'critical'"
                       [class.border-rose-800/50]="rec.type === 'critical'"
                       [class.text-rose-300]="rec.type === 'critical'">
                    <span>
                      @if (rec.type === 'positive') { ✨ }
                      @else if (rec.type === 'improvement') { ⚡ }
                      @else { 🚨 }
                    </span>
                    <span>{{ rec.text }}</span>
                  </div>
                }
              </div>
            </div>
          }
        </div>
      }

      <!-- TAB 3: GEO CITABILITY AUDIT -->
      @if (activeTab() === 'geo') {
        <div class="space-y-6" data-testid="view-geo">
          <!-- Audit Trigger Banner -->
          <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <h2 class="text-base font-bold text-white flex items-center gap-2">
                <span>🤖</span> Generative Engine Optimization (GEO/AEO) Citability
              </h2>
              <p class="text-xs text-slate-400 mt-1">
                Audits sentence conciseness, ambiguous pronoun resolution, entity density, and extracts direct citable snippets for Perplexity, ChatGPT Search, and Claude.
              </p>
            </div>

            <button
              (click)="auditGeoCitability()"
              class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-indigo-600/20 whitespace-nowrap"
              data-testid="btn-run-geo-audit"
            >
              <span>🔬</span> Run GEO Citability Audit
            </button>
          </div>

          <!-- GEO Audit Results -->
          @if (geoCitabilityAudit(); as geo) {
            <!-- Metrics Row -->
            <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4" data-testid="geo-metrics">
              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Conciseness Score</span>
                <div class="text-2xl font-bold text-white mt-1 font-mono" data-testid="geo-conciseness">
                  {{ geo.concisenessScore }} <span class="text-xs text-slate-400 font-normal">/ 100</span>
                </div>
                <span class="text-[11px] text-emerald-400 mt-1 block">Optimal sentence lengths for RAG</span>
              </div>

              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Entity Density</span>
                <div class="text-2xl font-bold text-indigo-400 mt-1 font-mono" data-testid="geo-entity-density">
                  {{ geo.entityDensityPercent }}%
                </div>
                <span class="text-[11px] text-slate-400 mt-1 block">High informational grounding</span>
              </div>

              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Q&A Structure Ratio</span>
                <div class="text-2xl font-bold text-emerald-400 mt-1 font-mono">
                  {{ geo.qaStructureRatio }}%
                </div>
                <span class="text-[11px] text-slate-400 mt-1 block">Direct answer heading frequency</span>
              </div>

              <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
                <span class="text-xs font-medium text-slate-400">Ambiguous Pronouns</span>
                <div class="text-2xl font-bold text-amber-300 mt-1 font-mono" data-testid="geo-pronouns">
                  {{ geo.ambiguousPronounCount }}
                </div>
                <span class="text-[11px] text-slate-400 mt-1 block">Unclear antecedents detected</span>
              </div>
            </div>

            <!-- Formatted Citation Snippet -->
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-3">
              <div class="flex items-center justify-between">
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>📌</span> Formatted Passage for LLM Citations
                </h3>
                <span class="px-2 py-0.5 rounded text-[11px] font-semibold bg-emerald-500/20 text-emerald-300">
                  Direct Answer Clarity: {{ geo.directAnswerClarity }}
                </span>
              </div>

              <div class="bg-slate-900 border border-slate-700 rounded-lg p-4 font-mono text-xs text-emerald-300 leading-relaxed"
                   data-testid="formatted-citation-snippet">
                "{{ geo.formattedCitationSnippet }}"
              </div>
              <p class="text-[11px] text-slate-400">
                This passage is synthesized and structured for Perplexity / ChatGPT Search quote extraction when answering queries about
                <code class="text-slate-300 font-bold">{{ primaryKeyword() }}</code>.
              </p>
            </div>

            <!-- Extracted Named Entities -->
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-3">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>🏷️</span> Extracted Semantic Entities ({{ geo.extractedEntities.length }})
              </h3>

              <div class="flex flex-wrap gap-2" data-testid="extracted-entities-list">
                @for (ent of geo.extractedEntities; track ent) {
                  <span class="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-medium bg-slate-900 border border-slate-700 text-slate-200">
                    {{ ent }}
                  </span>
                }
              </div>
            </div>

            <!-- Per-Engine Readiness Matrix -->
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-4">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <span>🌐</span> AI Search Engine Citability Readiness
              </h3>

              <div class="grid grid-cols-1 md:grid-cols-2 gap-3" data-testid="engine-readiness-matrix">
                @for (engine of geo.perEngineReadiness; track engine.engine) {
                  <div class="bg-slate-900/80 border border-slate-700/60 rounded-lg p-3.5 space-y-1.5">
                    <div class="flex items-center justify-between">
                      <span class="text-sm font-bold text-white">{{ engine.engine }}</span>
                      <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                            [class.bg-emerald-500/20]="engine.status === 'Optimized'"
                            [class.text-emerald-300]="engine.status === 'Optimized'"
                            [class.bg-indigo-500/20]="engine.status === 'Eligible'"
                            [class.text-indigo-300]="engine.status === 'Eligible'"
                            [class.bg-amber-500/20]="engine.status === 'Needs Improvement'"
                            [class.text-amber-300]="engine.status === 'Needs Improvement'">
                        {{ engine.status }}
                      </span>
                    </div>
                    <p class="text-xs text-slate-400">{{ engine.details }}</p>
                  </div>
                }
              </div>
            </div>
          } @else {
            <div class="bg-slate-800/40 border border-dashed border-slate-700 rounded-xl p-12 text-center space-y-3">
              <div class="text-4xl">🤖</div>
              <h3 class="text-base font-bold text-white">Citability Audit Not Run Yet</h3>
              <p class="text-sm text-slate-400 max-w-md mx-auto">
                Evaluate how AI crawlers and answer engines (Perplexity, ChatGPT Search, Claude) extract quotes and identify entities from your draft.
              </p>
              <button
                (click)="auditGeoCitability()"
                class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-sm font-semibold transition-colors inline-flex items-center gap-2"
              >
                <span>🔬</span> Run Audit Now
              </button>
            </div>
          }
        </div>
      }

      <!-- TAB 4: LLMS.TXT EXPORT -->
      @if (activeTab() === 'llms') {
        <div class="space-y-6" data-testid="view-llms">
          <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 shadow-sm space-y-4">
            <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div>
                <h2 class="text-sm font-semibold uppercase tracking-wider text-slate-300 flex items-center gap-2">
                  <span>📄</span> Standard /llms.txt Specification Manifest
                </h2>
                <p class="text-xs text-slate-400 mt-1">
                  Machine-readable Markdown file placed in domain root for autonomous agents and LLMs to index core entities and documentation pages.
                </p>
              </div>

              <div class="flex items-center gap-2">
                <button
                  (click)="generateLlmsTxt()"
                  class="px-3 py-1.5 bg-slate-700 hover:bg-slate-600 text-white rounded-lg text-xs font-medium transition-colors"
                  data-testid="btn-regen-llms"
                >
                  Regenerate
                </button>
                <button
                  (click)="copyLlmsTxt()"
                  class="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition-colors flex items-center gap-1.5 shadow-sm"
                  data-testid="btn-copy-llms"
                >
                  <span>📋</span>
                  {{ copiedLlms() ? 'Copied to Clipboard!' : 'Copy /llms.txt' }}
                </button>
              </div>
            </div>

            <!-- Content Display Area -->
            <div class="relative">
              <textarea
                rows="14"
                readonly
                [ngModel]="llmsTxtContent()"
                class="w-full px-4 py-3 bg-slate-950 border border-slate-700/80 rounded-lg text-emerald-300 font-mono text-xs leading-relaxed focus:outline-none select-all"
                data-testid="textarea-llms-content"
              ></textarea>

              @if (copiedLlms()) {
                <div class="absolute top-3 right-3 bg-emerald-500 text-slate-950 text-xs font-bold px-3 py-1 rounded-md shadow-lg transition-all animate-fade-in"
                     data-testid="copied-badge">
                  ✓ Copied to clipboard!
                </div>
              }
            </div>

            <div class="p-3.5 bg-slate-900/80 border border-slate-700/60 rounded-lg text-xs text-slate-400 space-y-1">
              <div class="font-semibold text-slate-300">Deployment Instructions:</div>
              <p>1. Save this content as <code class="text-emerald-400 font-mono">/llms.txt</code> in your site's root directory (alongside <code class="text-slate-300 font-mono">robots.txt</code>).</p>
              <p>2. Link comprehensive documentation in <code class="text-emerald-400 font-mono">/llms-full.txt</code> for LLM agents performing high-context deep dives.</p>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class ContentBriefStudioComponent {
  // Reactive signals required by specification
  readonly primaryKeyword = signal<string>('enterprise dynamic pricing');
  readonly secondaryKeywordsInput = signal<string>('cod checkout, ecom margin, pricing rules');
  readonly targetAudience = signal<string>('E-Commerce Directors');
  readonly searchIntent = signal<'Informational' | 'Commercial' | 'Transactional' | 'Navigational'>('Commercial');
  readonly draftTitle = signal<string>('The Definitive Guide to Dynamic Pricing');
  readonly draftBody = signal<string>('');
  readonly currentBrief = signal<ContentBrief | null>(null);
  readonly draftGradeReport = signal<DraftGradeReport | null>(null);
  readonly geoCitabilityAudit = signal<GeoCitabilityAudit | null>(null);
  readonly llmsTxtContent = signal<string>('');
  readonly activeTab = signal<'brief' | 'grader' | 'geo' | 'llms'>('brief');

  // UI state signals
  readonly copiedLlms = signal<boolean>(false);

  // Computed signals
  readonly liveWordCount = computed(() => {
    const text = this.draftBody().trim();
    if (!text) return 0;
    return text.split(/\s+/).filter(w => w.length > 0).length;
  });

  readonly liveCharCount = computed(() => this.draftBody().length);

  readonly secondaryKeywordsList = computed(() => {
    const raw = this.secondaryKeywordsInput();
    if (!raw.trim()) return [];
    return raw
      .split(',')
      .map(k => k.trim())
      .filter(k => k.length > 0);
  });

  setActiveTab(tab: 'brief' | 'grader' | 'geo' | 'llms'): void {
    this.activeTab.set(tab);
    if (tab === 'llms' && !this.llmsTxtContent()) {
      this.generateLlmsTxt();
    }
  }

  loadSampleDraft(): void {
    const pk = this.primaryKeyword();
    this.draftTitle.set(`The Definitive Guide to ${pk ? this.capitalize(pk) : 'Enterprise Dynamic Pricing'}`);
    this.draftBody.set(
      `# The Definitive Guide to ${this.capitalize(pk || 'Enterprise Dynamic Pricing')}\n\n` +
      `What is enterprise dynamic pricing and how does it protect ecom margin?\n\n` +
      `Enterprise dynamic pricing is an algorithmic pricing strategy where product prices continuously adjust in response to real-time supply, competitor price movements, historical sales velocity, and fulfillment risk. For high-growth retailers, implementing automated pricing rules ensures that margin targets remain intact without sacrificing sales conversion.\n\n` +
      `## How Do Dynamic Pricing Rules Prevent COD Checkout Loss?\n\n` +
      `In modern retail, cash-on-delivery (cod checkout) orders suffer significant return-to-origin cancellations. High-performing pricing engines apply predictive risk scoring directly at checkout. By adjusting incentives or calculating real-time margin buffers, e-commerce brands mitigate return losses before dispatch.\n\n` +
      `## Margin Protection in Multi-Channel Commerce\n\n` +
      `Protecting ecom margin requires setting algorithmic margin floors. When competitor scrapers trigger automated price drops, the system enforces a strict threshold. Enterprise pricing algorithms evaluate inventory turnover, supplier replacement cost, and advertising customer acquisition cost (CAC) before approving any price markdown.\n\n` +
      `## Core Benefits for E-Commerce Directors\n` +
      `1. Real-time competitor tracking within sub-second reaction windows.\n` +
      `2. Protection against negative margin sales across marketplace channels.\n` +
      `3. Automated inventory clearance without blanket discounting.`
    );

    // If grader is active, trigger grade right away
    if (this.activeTab() === 'grader') {
      this.gradeDraft();
    }
  }

  generateBrief(): void {
    const pk = this.primaryKeyword().trim() || 'enterprise dynamic pricing';
    const audience = this.targetAudience().trim() || 'E-Commerce Directors';
    const intent = this.searchIntent();
    const secondaries = this.secondaryKeywordsList();

    const brief: ContentBrief = {
      briefId: 'brief_' + Math.random().toString(36).substring(2, 9),
      primaryKeyword: pk,
      targetAudience: audience,
      searchIntent: intent,
      targetWordCount: { min: 2200, max: 2800 },
      targetReadingGrade: 'Grade 9-11 (Flesch-Kincaid)',
      generatedAt: new Date().toISOString(),
      targetContentScore: 88,
      recommendedSchema: 'Article + FAQPage + HowTo',
      outlineSections: [
        {
          heading: `Introduction & Executive Definition of ${this.capitalize(pk)}`,
          level: 'h2',
          purpose: `Establish direct definition for Perplexity and Google AI Overviews within first 40 words.`,
          suggestedQuestions: [
            `What is ${pk}?`,
            `Why is ${pk} critical for ${audience}?`
          ],
          recommendedWords: 350
        },
        {
          heading: `How Modern Retailers Implement Automated Pricing Rules`,
          level: 'h2',
          purpose: `Technical walkthrough of algorithmic rule engines and margin floors.`,
          suggestedQuestions: [
            `What pricing rules protect gross margins best?`,
            `How does real-time competitor tracking integrate with ERP catalogues?`
          ],
          recommendedWords: 550
        },
        {
          heading: `Protecting Margin at Checkout: COD & Fulfillment Risk Mitigation`,
          level: 'h3',
          purpose: `Practical tactics for ecom margin preservation during high-friction checkouts.`,
          suggestedQuestions: [
            `How to minimize return-to-origin on COD checkout?`,
            `Can dynamic pricing offset elevated shipping fees?`
          ],
          recommendedWords: 450
        },
        {
          heading: `Step-by-Step Implementation Framework for ${audience}`,
          level: 'h2',
          purpose: `Actionable deployment timeline, tech stack prerequisites, and risk control.`,
          suggestedQuestions: [
            `What are the software requirements for enterprise pricing engines?`,
            `How to establish safe discount limits and price floors?`
          ],
          recommendedWords: 600
        },
        {
          heading: `Frequently Asked Questions About ${this.capitalize(pk)}`,
          level: 'h2',
          purpose: `FAQ schema rich snippet capture targeted at high-intent long-tail queries.`,
          suggestedQuestions: [
            `Does dynamic pricing violate marketplace terms of service?`,
            `How frequently should price points recalculate?`
          ],
          recommendedWords: 350
        }
      ],
      semanticKeywords: [
        { term: pk, suggestedFrequency: 12, targetDensityPercent: 1.4, intent: intent },
        ...secondaries.map((s, idx) => ({
          term: s,
          suggestedFrequency: 5 + (idx * 2),
          targetDensityPercent: Number((0.6 + (idx * 0.2)).toFixed(1)),
          intent: 'Commercial'
        })),
        { term: 'margin floor constraint', suggestedFrequency: 6, targetDensityPercent: 0.8, intent: 'Informational' },
        { term: 'real-time inventory velocity', suggestedFrequency: 5, targetDensityPercent: 0.7, intent: 'Informational' },
        { term: 'algorithmic price elasticity', suggestedFrequency: 4, targetDensityPercent: 0.5, intent: 'Commercial' }
      ],
      competitorBenchmarks: [
        { domain: 'pricerunner-insights.com', wordCount: 2450, h2Count: 6, authorityScore: 78 },
        { domain: 'retailmarginreview.org', wordCount: 2100, h2Count: 5, authorityScore: 71 },
        { domain: 'ecommerce-operations.io', wordCount: 2680, h2Count: 7, authorityScore: 82 }
      ]
    };

    this.currentBrief.set(brief);

    // If llms content is blank, initialize it
    if (!this.llmsTxtContent()) {
      this.generateLlmsTxt();
    }
  }

  gradeDraft(): void {
    const body = this.draftBody().trim();
    const pk = this.primaryKeyword().trim().toLowerCase();
    const secondaries = this.secondaryKeywordsList();

    if (!body) {
      this.draftGradeReport.set({
        wordCount: 0,
        charCount: 0,
        readingEaseScore: 0,
        gradeLevel: 'No content',
        letterGrade: 'F',
        primaryKeywordDensity: 0,
        secondaryKeywordMatches: secondaries.map(s => ({ keyword: s, count: 0, densityPercent: 0, status: 'Low' })),
        questionsAnswered: [],
        questionsMissing: ['Draft is empty. Add article text or load sample draft.'],
        recommendations: [
          { type: 'critical', text: 'Article draft is empty. Insert content to receive an algorithmic grade.' }
        ],
        fleschAnalysis: 'Empty draft.'
      });
      return;
    }

    const words = body.split(/\s+/).filter(w => w.length > 0);
    const wordCount = words.length;
    const charCount = body.length;

    // Sentences count
    const sentences = body.split(/[.!?]+/).filter(s => s.trim().length > 0);
    const sentenceCount = Math.max(1, sentences.length);

    // Syllables estimation
    let totalSyllables = 0;
    for (const word of words) {
      totalSyllables += this.estimateSyllables(word);
    }
    const asl = wordCount / sentenceCount;
    const asw = totalSyllables / wordCount;

    // Flesch Reading Ease Formula: 206.835 - (1.015 * ASL) - (84.6 * ASW)
    let rawFlesch = 206.835 - (1.015 * asl) - (84.6 * asw);
    rawFlesch = Math.max(10, Math.min(95, Math.round(rawFlesch * 10) / 10));

    // Grade Level
    let gradeLevel = 'Standard (8th & 9th Grade)';
    if (rawFlesch >= 80) gradeLevel = 'Easy (6th Grade)';
    else if (rawFlesch >= 70) gradeLevel = 'Fairly Easy (7th Grade)';
    else if (rawFlesch >= 60) gradeLevel = 'Standard (8th & 9th Grade)';
    else if (rawFlesch >= 50) gradeLevel = 'Fairly Difficult (10th-12th Grade)';
    else if (rawFlesch >= 30) gradeLevel = 'Difficult (College)';
    else gradeLevel = 'Very Confusing (Graduate)';

    // Keyword counting
    const lowerBody = body.toLowerCase();
    const pkRegex = new RegExp(pk.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi');
    const pkMatches = lowerBody.match(pkRegex);
    const pkCount = pkMatches ? pkMatches.length : 0;
    const pkWordLen = pk.split(/\s+/).length;
    const pkDensity = Number(((pkCount * pkWordLen / wordCount) * 100).toFixed(2));

    const secondaryMatches: KeywordMatchReport[] = secondaries.map(sec => {
      const secLower = sec.toLowerCase();
      const regex = new RegExp(secLower.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi');
      const matches = lowerBody.match(regex);
      const count = matches ? matches.length : 0;
      const density = Number(((count * secLower.split(/\s+/).length / wordCount) * 100).toFixed(2));
      let status: 'Optimal' | 'Low' | 'High' = 'Optimal';
      if (density < 0.4) status = 'Low';
      else if (density > 2.5) status = 'High';
      return { keyword: sec, count, densityPercent: density, status };
    });

    // Check answered vs missing questions
    const candidateQuestions = this.currentBrief()?.outlineSections.flatMap(s => s.suggestedQuestions) ?? [
      'What is enterprise dynamic pricing?',
      'How do dynamic pricing rules prevent COD checkout loss?',
      'How to protect ecom margin at checkout?',
      'What are the core benefits for E-Commerce Directors?'
    ];

    const questionsAnswered: string[] = [];
    const questionsMissing: string[] = [];

    for (const q of candidateQuestions) {
      // Check if key words of question appear in body
      const cleanQWords = q
        .toLowerCase()
        .replace(/[^a-z0-9\s]/g, '')
        .split(/\s+/)
        .filter(w => !['what', 'is', 'how', 'do', 'to', 'the', 'a', 'an', 'for', 'are', 'does'].includes(w));

      const matchedWords = cleanQWords.filter(qw => lowerBody.includes(qw));
      if (matchedWords.length >= Math.max(1, Math.floor(cleanQWords.length * 0.6))) {
        questionsAnswered.push(q);
      } else {
        questionsMissing.push(q);
      }
    }

    // Determine Letter Grade
    let letterGrade: 'A+' | 'A' | 'B' | 'C' | 'D' | 'F' = 'B';
    const hasPk = pkCount > 0;
    const hasGoodDensity = pkDensity >= 0.5 && pkDensity <= 2.8;

    if (wordCount >= 150 && hasPk && hasGoodDensity && rawFlesch >= 50 && questionsAnswered.length >= 2) {
      letterGrade = (rawFlesch >= 65 && questionsMissing.length <= 1) ? 'A+' : 'A';
    } else if (wordCount >= 100 && hasPk) {
      letterGrade = 'B';
    } else if (wordCount >= 50) {
      letterGrade = 'C';
    } else {
      letterGrade = 'D';
    }

    // Recommendations
    const recs: RecommendationItem[] = [];
    if (pkDensity < 0.8) {
      recs.push({ type: 'improvement', text: `Primary keyword density is ${pkDensity}%. Increase usage to between 0.8% - 2.0% for optimal SERP prominence.` });
    } else if (pkDensity > 2.5) {
      recs.push({ type: 'critical', text: `Primary keyword density is high (${pkDensity}%). Reduce frequency to avoid search engine over-optimization penalties.` });
    } else {
      recs.push({ type: 'positive', text: `Primary keyword density is well-balanced at ${pkDensity}%.` });
    }

    if (rawFlesch < 50) {
      recs.push({ type: 'improvement', text: `Reading ease is ${rawFlesch} (${gradeLevel}). Shorten long compound sentences to improve executive readability.` });
    } else {
      recs.push({ type: 'positive', text: `Readability score of ${rawFlesch} matches B2B decision-maker consumption benchmarks.` });
    }

    if (questionsMissing.length > 0) {
      recs.push({ type: 'improvement', text: `Address ${questionsMissing.length} unanswered topical questions to prevent SERP competitors from winning featured snippets.` });
    } else {
      recs.push({ type: 'positive', text: `All primary outline questions are thoroughly answered.` });
    }

    this.draftGradeReport.set({
      wordCount,
      charCount,
      readingEaseScore: rawFlesch,
      gradeLevel,
      letterGrade,
      primaryKeywordDensity: pkDensity,
      secondaryKeywordMatches: secondaryMatches,
      questionsAnswered,
      questionsMissing,
      recommendations: recs,
      fleschAnalysis: `${gradeLevel} with average sentence length of ${Math.round(asl)} words.`
    });
  }

  auditGeoCitability(): void {
    const body = this.draftBody().trim();
    const pk = this.primaryKeyword().trim() || 'enterprise dynamic pricing';

    let textToAnalyze = body;
    if (!textToAnalyze) {
      this.loadSampleDraft();
      textToAnalyze = this.draftBody().trim();
    }

    const sentences = textToAnalyze.split(/[.!?]+/).map(s => s.trim()).filter(s => s.length > 0);
    const words = textToAnalyze.split(/\s+/).filter(w => w.length > 0);

    // 1. Conciseness Score (ideal sentence length 12-20 words)
    const avgLen = words.length / Math.max(1, sentences.length);
    let conciseness = 92;
    if (avgLen > 25) conciseness -= (avgLen - 25) * 2;
    else if (avgLen < 10) conciseness -= (10 - avgLen) * 3;
    conciseness = Math.max(50, Math.min(98, Math.round(conciseness)));

    // 2. Entity Density
    const knownEntities = [
      'dynamic pricing', 'ecom margin', 'cod checkout', 'pricing rules',
      'cac', 'margin floor', 'return-to-origin', 'sku', 'erp', 'b2b',
      'api', 'saas', 'perplexity', 'claude', 'chatgpt'
    ];
    let entityCount = 0;
    const lower = textToAnalyze.toLowerCase();
    for (const e of knownEntities) {
      const count = (lower.match(new RegExp(e, 'g')) || []).length;
      entityCount += count;
    }
    // Also capitalized phrases
    const capitalizedMatches = textToAnalyze.match(/[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+/g) || [];
    entityCount += capitalizedMatches.length;

    const entityDensityPercent = Number(Math.min(30, Math.max(8, (entityCount * 2 / Math.max(1, words.length)) * 100)).toFixed(1));

    // 3. Q&A Structure Ratio
    const questionHeaders = sentences.filter(s => s.includes('?') || /^(what|how|why|can|which|where)/i.test(s)).length;
    const qaRatio = Math.round(Math.min(100, (questionHeaders / Math.max(1, sentences.length)) * 300));

    // 4. Ambiguous Pronouns
    const vagueStarts = textToAnalyze.match(/\b(it is|this is|they are|there are|these are|which makes)\b/gi) || [];
    const ambiguousCount = vagueStarts.length;

    // 5. Formatted Citation Snippet
    const citation = `According to SGrowing benchmark intelligence, ${pk} systems responding in sub-second velocity directly protect e-commerce gross margins while reducing checkout cancellations.`;

    const audit: GeoCitabilityAudit = {
      auditId: 'geo_' + Math.random().toString(36).substring(2, 9),
      concisenessScore: conciseness,
      entityDensityPercent: entityDensityPercent,
      qaStructureRatio: Math.max(40, qaRatio),
      ambiguousPronounCount: ambiguousCount,
      directAnswerClarity: ambiguousCount <= 3 ? 'High' : 'Medium',
      formattedCitationSnippet: citation,
      extractedEntities: [
        this.capitalize(pk),
        'Cash-on-Delivery (COD)',
        'Gross Margin Floor',
        'Real-time Velocity Rules',
        'Customer Acquisition Cost (CAC)'
      ],
      aeoRecommendation: 'Passages feature strong direct-answer assertions suited for AI Overviews and Perplexity summaries.',
      perEngineReadiness: [
        {
          engine: 'Perplexity Search',
          status: 'Optimized',
          details: 'Direct question-answer headers match conversational search extraction patterns.'
        },
        {
          engine: 'Google AI Overviews',
          status: 'Optimized',
          details: 'Definitional 40-word lead paragraph satisfies top-of-SERP summary extraction.'
        },
        {
          engine: 'Claude Search & RAG',
          status: 'Eligible',
          details: 'High factual grounding and entity density allow high-fidelity citation.'
        },
        {
          engine: 'ChatGPT Search (SearchGPT)',
          status: 'Optimized',
          details: 'Clear Markdown headers and numbered key takeaways facilitate deep answer rendering.'
        }
      ]
    };

    this.geoCitabilityAudit.set(audit);
  }

  generateLlmsTxt(): void {
    const pk = this.primaryKeyword() || 'Enterprise Dynamic Pricing';
    const capPk = this.capitalize(pk);
    const audience = this.targetAudience() || 'E-Commerce Directors';

    const content = 
`# ${capPk} Knowledge Hub
> Authoritative system architecture, margin rules, and autonomous pricing algorithms engineered for ${audience}.

## Core Specifications & Guides
- [${capPk} Architecture](/docs/architecture.md): Real-time ingestion engine and sub-second calculation loops.
- [Algorithmic Margin Floors](/docs/margin-floors.md): Hard stop constraints protecting gross profit against competitor price wars.
- [COD Checkout Mitigation](/docs/cod-optimization.md): Predictive cancellation scoring and risk-adjusted pricing.
- [Rule Engine Syntax](/docs/rules-engine.md): Declarative JSON-based price adjustments and seasonal triggers.

## Key Named Entities
- PriceRule: Atomic condition-action mapping adjusting SKU prices based on competitor velocity.
- MarginFloor: Non-negotiable floor percentage preventing loss-leader runaway.
- RealTimeCrawler: Distributed scraper delivering fresh marketplace baseline quotes.
- ReturnRiskScore: Checkout scoring engine assessing order return probability.

## Optional Extended References
- [Full LLM Dump](/llms-full.txt): Complete uncompressed corpus for high-context agent reasoning.
- [OpenAPI Documentation](/api/v1/swagger.json): REST endpoints for price feed ingestion.
`;

    this.llmsTxtContent.set(content);
  }

  copyLlmsTxt(): void {
    if (!this.llmsTxtContent()) {
      this.generateLlmsTxt();
    }

    const text = this.llmsTxtContent();
    if (typeof navigator !== 'undefined' && navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(() => {});
    }

    this.copiedLlms.set(true);
    setTimeout(() => {
      this.copiedLlms.set(false);
    }, 2500);
  }

  private estimateSyllables(word: string): number {
    const clean = word.toLowerCase().replace(/[^a-z]/g, '');
    if (!clean) return 1;
    if (clean.length <= 3) return 1;
    const matches = clean.match(/[aeiouy]{1,2}/g);
    let count = matches ? matches.length : 1;
    if (clean.endsWith('e') && !clean.endsWith('le')) {
      count = Math.max(1, count - 1);
    }
    return Math.max(1, count);
  }

  private capitalize(str: string): string {
    return str
      .split(' ')
      .map(w => w.charAt(0).toUpperCase() + w.slice(1))
      .join(' ');
  }
}
