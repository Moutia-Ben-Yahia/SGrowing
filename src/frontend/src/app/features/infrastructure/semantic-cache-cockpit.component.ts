import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CachedPromptView {
  key: string;
  prompt: string;
  completion: string;
  tokens: number;
  costSaved: number;
  hits: number;
}

export interface CacheQueryResultView {
  isHit: boolean;
  source: string;
  completion: string;
  similarity: number;
  latencyMs: number;
  costSaved: number;
}

@Component({
  selector: 'app-semantic-cache-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="semantic-cache-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-teal-400">
            <span class="inline-block w-2 h-2 rounded-full bg-teal-400 animate-pulse"></span>
            FinOps AI Inference & Semantic Vector Cache • Horizon 65
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous API Gateway Semantic Caching & Deduplication
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Edge cosine vector embedding deduplication, sub-5ms cache hits, and automated FinOps token cost reduction.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="testSemanticQuery()"
            data-testid="query-cache-btn"
            class="px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-teal-600/20"
          >
            <span>⚡</span> Test Semantic Query
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Semantic Hit Rate</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-hit-rate">
            {{ hitRate() }}%
          </div>
          <div class="text-slate-400 text-xs mt-1">Cosine Similarity >= 0.94 Match</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Cumulative FinOps Savings</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-dollars-saved">
            \${{ dollarsSaved() | number:'1.2-2' }}
          </div>
          <div class="text-teal-400 text-xs mt-1">28.5M Tokens Bypassed</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Edge Cache Latency</div>
          <div class="text-2xl font-bold text-teal-400 mt-1" data-testid="stat-cache-latency">
            4.2 ms
          </div>
          <div class="text-emerald-400 text-xs mt-1">vs 1,180 ms LLM Cold Inference</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Hot Vector Clusters</div>
          <div class="text-2xl font-bold text-sky-400 mt-1">
            {{ entries().length }}
          </div>
          <div class="text-slate-400 text-xs mt-1">LRU Adaptive In-Memory Mesh</div>
        </div>
      </div>

      <!-- Main Stage: Hot Vector Cache & Query Simulator -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Hot Entries (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Hot Semantic Vector Entries</h2>
            <span class="text-xs text-slate-400 font-mono">{{ entries().length }} Vector Records</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (entry of entries(); track entry.key) {
              <div class="p-4 hover:bg-slate-700/30 transition-colors space-y-2">
                <div class="flex items-center justify-between">
                  <span class="font-bold text-white text-xs">"{{ entry.prompt }}"</span>
                  <span class="text-emerald-400 font-mono text-xs font-bold">{{ entry.hits }} hits</span>
                </div>
                <p class="text-[11px] text-slate-300 bg-slate-900/60 p-2.5 rounded border border-slate-800 leading-relaxed font-mono">
                  {{ entry.completion }}
                </p>
                <div class="flex items-center justify-between text-[10px] text-slate-400 pt-1">
                  <span>Tokens: <strong class="text-white">{{ entry.tokens }}</strong></span>
                  <span>Cost Saved: <strong class="text-teal-400">\${{ entry.costSaved | number:'1.2-2' }}</strong></span>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Query Simulator & Flush Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">Semantic Cache Telemetry</h3>
              <p class="text-xs text-slate-400">Cosine Distance Deduplication</p>
            </div>
            <span class="text-xs px-2.5 py-1 rounded bg-teal-950 text-teal-300 border border-teal-800">
              Active Edge
            </span>
          </div>

          @if (queryResult(); as res) {
            <div class="p-3 bg-slate-900 rounded-xl border border-slate-800 space-y-2 text-xs" data-testid="cache-result-card">
              <div class="flex items-center justify-between font-bold">
                <span class="text-white">Query Status:</span>
                <span [class]="res.isHit ? 'text-emerald-400' : 'text-amber-400'">
                  {{ res.isHit ? '✓ Semantic Cache Hit (4.2ms)' : 'Cold Upstream Miss (1150ms)' }}
                </span>
              </div>
              <div class="text-[11px] text-slate-400 flex items-center justify-between">
                <span>Source: <strong class="text-slate-300">{{ res.source }}</strong></span>
                <span>Similarity: <strong class="text-teal-400 font-mono">{{ res.similarity }}</strong></span>
              </div>
              <p class="p-2 bg-slate-950 rounded text-slate-300 text-[11px] leading-relaxed border border-slate-800">
                {{ res.completion }}
              </p>
            </div>
          }

          <button
            (click)="flushCache()"
            data-testid="flush-cache-btn"
            class="w-full py-2 bg-slate-700 hover:bg-slate-600 text-slate-200 rounded-lg text-xs font-semibold transition-colors"
          >
            Flush Semantic Vector Cache
          </button>
        </div>
      </div>
    </div>
  `
})
export class SemanticCacheCockpitComponent {
  readonly entries = signal<CachedPromptView[]>([
    {
      key: 'cache_prompt_01',
      prompt: 'how do I configure multi-region postgresql replication on azure?',
      completion: 'To configure active-passive PostgreSQL flexible server replication on Azure, enable replication_mode=GeoReplication with sub-5s RPO.',
      tokens: 1450,
      costSaved: 0.18,
      hits: 48
    },
    {
      key: 'cache_prompt_02',
      prompt: 'what are the best practices for whatsapp flows v3.0 screen dag navigation?',
      completion: 'WhatsApp Flows v3.0 screens must return encrypted JSON payloads with data_exchange_action within 150ms.',
      tokens: 1280,
      costSaved: 0.16,
      hits: 62
    }
  ]);

  readonly hitRate = signal(77.6);
  readonly dollarsSaved = signal(3450.80);

  readonly queryResult = signal<CacheQueryResultView | null>({
    isHit: true,
    source: 'SemanticVectorCache',
    completion: 'To configure active-passive PostgreSQL flexible server replication on Azure, enable replication_mode=GeoReplication with sub-5s RPO.',
    similarity: 0.985,
    latencyMs: 4.2,
    costSaved: 0.18
  });

  testSemanticQuery(): void {
    this.queryResult.set({
      isHit: true,
      source: 'SemanticVectorCache',
      completion: 'WhatsApp Flows v3.0 screens must return encrypted JSON payloads with data_exchange_action within 150ms.',
      similarity: 0.974,
      latencyMs: 3.9,
      costSaved: 0.16
    });
    this.dollarsSaved.update(d => d + 0.16);
  }

  flushCache(): void {
    this.entries.set([]);
    this.queryResult.set({
      isHit: false,
      source: 'UpstreamInferenceProvider',
      completion: '[Synthesized Fresh Model Response]: Vector cache successfully flushed. Fresh inference triggered.',
      similarity: 0.0,
      latencyMs: 1150.0,
      costSaved: 0.0
    });
  }
}
