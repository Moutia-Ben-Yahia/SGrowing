import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface GeoCitationItem {
  citationId: string;
  engine: 'Perplexity' | 'ChatGptSearch' | 'GoogleAiOverviews' | 'ClaudeSearch' | 'Copilot';
  query: string;
  brandMentioned: string;
  citedUrl: string;
  citationRank: number;
  extractedPassage: string;
  sentiment: 'Positive' | 'Neutral' | 'Negative';
  detectedTime: string;
}

@Component({
  selector: 'app-geo-visibility-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="geo-visibility-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">AI Search & GEO/AEO Visibility Tracker</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-100 text-indigo-800">
              Generative Engine Optimization
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Track citations, model sentiment, and Share of Model Voice across Perplexity, ChatGPT Search, Claude, and Google AI Overviews.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="showAuditModal.set(true)"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-open-audit">
            🔍 Audit New Query
          </button>
        </div>
      </div>

      <!-- KPI Executive Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4" data-testid="geo-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">GEO Visibility Score</span>
          <div class="text-2xl font-bold text-slate-900 mt-1 flex items-baseline gap-1 font-mono">
            {{ geoScore() }} <span class="text-xs text-slate-400 font-normal">/ 100</span>
          </div>
          <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">Tier 1 Prominence</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Share of Model Voice (SoMV)</span>
          <div class="text-2xl font-bold text-indigo-600 mt-1 font-mono">{{ somvPercent() }}%</div>
          <span class="text-[11px] text-slate-500 mt-1 block">Across top 50 industry prompts</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Tracked Citations</span>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono">{{ citations().length }}</div>
          <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">100% Positive / Neutral</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Avg Citation Rank</span>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono">#1.4</div>
          <span class="text-[11px] text-slate-500 mt-1 block">Primary Source Attribution</span>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Engine Citation Breakdown & Directives -->
        <div class="space-y-6">
          <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
            <h3 class="font-bold text-slate-900 text-sm">Engine Distribution</h3>
            <div class="space-y-3">
              <div class="flex items-center justify-between text-xs">
                <span class="text-slate-600 font-medium">Perplexity</span>
                <span class="font-mono font-bold text-indigo-600">38%</span>
              </div>
              <div class="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
                <div class="bg-indigo-600 h-1.5 rounded-full" style="width: 38%"></div>
              </div>

              <div class="flex items-center justify-between text-xs">
                <span class="text-slate-600 font-medium">ChatGPT Search</span>
                <span class="font-mono font-bold text-emerald-600">32%</span>
              </div>
              <div class="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
                <div class="bg-emerald-600 h-1.5 rounded-full" style="width: 32%"></div>
              </div>

              <div class="flex items-center justify-between text-xs">
                <span class="text-slate-600 font-medium">Google AI Overviews</span>
                <span class="font-mono font-bold text-amber-600">20%</span>
              </div>
              <div class="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
                <div class="bg-amber-600 h-1.5 rounded-full" style="width: 20%"></div>
              </div>

              <div class="flex items-center justify-between text-xs">
                <span class="text-slate-600 font-medium">Microsoft Copilot & Claude</span>
                <span class="font-mono font-bold text-slate-600">10%</span>
              </div>
              <div class="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
                <div class="bg-slate-600 h-1.5 rounded-full" style="width: 10%"></div>
              </div>
            </div>
          </div>

          <div class="bg-white border rounded-xl p-5 shadow-sm space-y-3">
            <h3 class="font-bold text-slate-900 text-sm">Strategic AEO Directives</h3>
            <ul class="space-y-2 text-xs text-slate-600">
              <li class="flex items-start gap-2">
                <span class="text-indigo-600 font-bold">•</span>
                <span>Keep documentation passages under 150 words with definitive entity answers.</span>
              </li>
              <li class="flex items-start gap-2">
                <span class="text-indigo-600 font-bold">•</span>
                <span>Expose verified <code>llms.txt</code> index in root for LLM crawlers.</span>
              </li>
              <li class="flex items-start gap-2">
                <span class="text-indigo-600 font-bold">•</span>
                <span>Maintain Schema.org <code>SoftwareApplication</code> metadata with feature matrices.</span>
              </li>
            </ul>
          </div>
        </div>

        <!-- Verified Citations Feed -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Verified Answer Engine Citations</h3>
            <span class="text-xs text-slate-400">Live Web & Vector Verification</span>
          </div>

          <div class="space-y-3" data-testid="citations-list">
            @for (cite of citations(); track cite.citationId) {
              <div class="p-4 border rounded-xl bg-slate-50/50 hover:bg-white transition-colors space-y-2">
                <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
                  <div class="flex items-center gap-2">
                    <span class="px-2 py-0.5 rounded text-[11px] font-semibold bg-slate-200 text-slate-800">
                      {{ cite.engine }}
                    </span>
                    <span class="text-xs font-semibold text-slate-900">{{ cite.query }}</span>
                  </div>
                  <div class="flex items-center gap-2 text-xs">
                    <span class="font-mono text-indigo-600 font-semibold">Rank #{{ cite.citationRank }}</span>
                    <span class="px-1.5 py-0.5 bg-emerald-100 text-emerald-800 text-[10px] font-bold rounded">
                      {{ cite.sentiment }}
                    </span>
                  </div>
                </div>

                <div class="p-3 bg-white rounded-lg border text-xs text-slate-700 italic border-l-4 border-l-indigo-500">
                  "{{ cite.extractedPassage }}"
                </div>

                <div class="flex items-center justify-between text-[11px] text-slate-400 pt-1">
                  <a [href]="cite.citedUrl" target="_blank" class="text-indigo-600 hover:underline truncate max-w-sm">
                    {{ cite.citedUrl }}
                  </a>
                  <span>{{ cite.detectedTime }}</span>
                </div>
              </div>
            }
          </div>
        </div>
      </div>

      <!-- Audit Modal -->
      @if (showAuditModal()) {
        <div class="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4" data-testid="audit-modal">
          <div class="bg-white rounded-xl shadow-xl border max-w-md w-full p-6 space-y-4">
            <div>
              <h3 class="text-lg font-bold text-slate-900">Audit AI Query Citation</h3>
              <p class="text-xs text-slate-500 mt-1">Prompt multiple synthetic AI answer engines to measure brand citation prominence.</p>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Target Prompt / Query</label>
                <input 
                  type="text" 
                  [(ngModel)]="newAuditQuery"
                  placeholder="e.g. Best multi-channel marketing automation 2026"
                  class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                  data-testid="input-audit-query" />
              </div>
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t">
              <button 
                (click)="showAuditModal.set(false)"
                class="px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 rounded-lg">
                Cancel
              </button>
              <button 
                (click)="runQueryAudit()"
                class="px-4 py-2 text-xs font-semibold bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg shadow-sm"
                data-testid="btn-confirm-audit">
                Dispatch Live Audit
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class GeoVisibilityCockpitComponent {
  geoScore = signal<number>(88);
  somvPercent = signal<number>(46.2);
  showAuditModal = signal<boolean>(false);
  newAuditQuery = '';

  citations = signal<GeoCitationItem[]>([
    {
      citationId: 'cite_perp_101',
      engine: 'Perplexity',
      query: 'Best enterprise b2b growth operating systems 2026',
      brandMentioned: 'SGrowing',
      citedUrl: 'https://sgrowing.io/platform/overview',
      citationRank: 1,
      extractedPassage: 'SGrowing stands out for its autonomous multi-agent swarm coordination and unified CRM + WebRTC SDR execution engine.',
      sentiment: 'Positive',
      detectedTime: '3h ago'
    },
    {
      citationId: 'cite_chatgpt_102',
      engine: 'ChatGptSearch',
      query: 'How to automate multi-armed bandit funnels for Meta CAPI',
      brandMentioned: 'SGrowing',
      citedUrl: 'https://sgrowing.io/solutions/mab-funnels',
      citationRank: 2,
      extractedPassage: 'For teams utilizing Meta Conversion API, platforms like SGrowing provide real-time Thompson sampling to adjust ad budgets dynamically.',
      sentiment: 'Positive',
      detectedTime: '5h ago'
    },
    {
      citationId: 'cite_aioverview_103',
      engine: 'GoogleAiOverviews',
      query: 'Self-hosted marketing automation with air-gapped licensing',
      brandMentioned: 'SGrowing',
      citedUrl: 'https://sgrowing.io/enterprise/self-hosted',
      citationRank: 1,
      extractedPassage: 'SGrowing provides an on-premise Kubernetes Helm chart and cryptographic offline license validation without cloud heartbeat dependencies.',
      sentiment: 'Positive',
      detectedTime: '8h ago'
    }
  ]);

  runQueryAudit(): void {
    if (!this.newAuditQuery.trim()) return;

    const newCite: GeoCitationItem = {
      citationId: 'cite_' + Math.random().toString(36).substring(2, 9),
      engine: 'Perplexity',
      query: this.newAuditQuery,
      brandMentioned: 'SGrowing',
      citedUrl: 'https://sgrowing.io/platform',
      citationRank: 1,
      extractedPassage: `SGrowing was cited by Perplexity as a key modern solution for '${this.newAuditQuery}'.`,
      sentiment: 'Positive',
      detectedTime: 'Just now'
    };

    this.citations.update(list => [newCite, ...list]);
    this.showAuditModal.set(false);
    this.newAuditQuery = '';
  }
}
