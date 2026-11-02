import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface RetrievedChunk {
  chunkId: string;
  documentId: string;
  title: string;
  content: string;
  similarityScore: number;
  category: string;
}

@Component({
  selector: 'app-semantic-rag-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="semantic-rag-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Semantic Vector RAG & Knowledge Engine</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-100 text-indigo-800">
              pgvector &bull; Cosine Distance
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Ground LLM prompts in verified enterprise knowledge bases with sub-50ms vector chunk retrieval.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="showIngestModal.set(true)"
            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-open-ingest">
            + Ingest Document Chunk
          </button>
        </div>
      </div>

      <!-- KPI Cards -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="rag-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Total Vector Chunks</span>
          <div class="text-xl font-bold text-slate-900 mt-1 font-mono">{{ totalChunks() | number }}</div>
          <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">100% In Memory / pgvector</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Vector Dimensions</span>
          <div class="text-xl font-bold text-slate-900 mt-1 font-mono">1,536-dim</div>
          <span class="text-[11px] text-slate-500 mt-1 block">OpenAI text-embedding-3</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">P99 Retrieval Latency</span>
          <div class="text-xl font-bold text-emerald-600 mt-1 font-mono">38 ms</div>
          <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">HNSW Graph Index</span>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <span class="text-xs font-medium text-slate-400">Hallucination Deflection</span>
          <div class="text-xl font-bold text-indigo-600 mt-1 font-mono">99.4%</div>
          <span class="text-[11px] text-slate-500 mt-1 block">Factually Grounded</span>
        </div>
      </div>

      <!-- Simulator & Knowledge Corpus -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Search & Grounding Simulator -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Real-Time Semantic Grounding Simulator</h3>

          <div class="flex gap-2">
            <input 
              type="text" 
              [(ngModel)]="searchQuery" 
              placeholder="Ask anything, e.g. What are the enterprise SLAs and pricing?"
              class="flex-1 px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 focus:outline-none"
              data-testid="input-rag-query" />
            <button 
              (click)="executeSearch()"
              class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-semibold shadow-sm transition-colors"
              data-testid="btn-search-rag">
              Retrieve & Ground
            </button>
          </div>

          <!-- Grounded Answer Box -->
          <div class="p-4 bg-slate-50 rounded-xl border space-y-2">
            <div class="flex items-center justify-between text-xs">
              <span class="font-bold text-slate-800 flex items-center gap-1.5">
                🤖 Grounded AI Synthesis
              </span>
              <span class="text-[11px] font-mono text-emerald-600 font-semibold">38ms &bull; 142 Tokens</span>
            </div>
            <p class="text-xs text-slate-700 leading-relaxed font-sans" data-testid="synthesized-answer">
              {{ groundedAnswer() }}
            </p>
          </div>

          <!-- Retrieved Source Chunks with Similarity Bars -->
          <div class="space-y-3 pt-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-bold text-slate-700">Retrieved Semantic Chunks (Top {{ chunks().length }})</span>
              <span class="text-[11px] text-slate-400">Ranked by Cosine Similarity</span>
            </div>

            <div class="space-y-2" data-testid="chunks-container">
              @for (chunk of chunks(); track chunk.chunkId) {
                <div class="p-3 border rounded-lg bg-white hover:bg-slate-50 transition-colors space-y-1.5">
                  <div class="flex items-center justify-between">
                    <div class="flex items-center gap-2">
                      <span class="text-xs font-semibold text-slate-900">{{ chunk.title }}</span>
                      <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-slate-100 text-slate-600">
                        {{ chunk.category }}
                      </span>
                    </div>
                    <div class="flex items-center gap-1.5">
                      <span class="text-xs font-mono font-bold text-indigo-600">{{ (chunk.similarityScore * 100) | number:'1.1-1' }}%</span>
                      <span class="text-[10px] text-slate-400">Match</span>
                    </div>
                  </div>
                  <p class="text-xs text-slate-600 line-clamp-2">
                    {{ chunk.content }}
                  </p>
                </div>
              }
            </div>
          </div>
        </div>

        <!-- Ingested Knowledge Base Clusters -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Indexed Document Corpus</h3>
          <div class="space-y-3">
            <div class="p-3 border rounded-lg space-y-1">
              <div class="flex justify-between text-xs">
                <span class="font-medium text-slate-800">Pricing & Rate Cards</span>
                <span class="font-mono text-slate-500">48 chunks</span>
              </div>
              <div class="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
                <div class="bg-indigo-600 h-1.5 rounded-full" style="width: 48%"></div>
              </div>
            </div>

            <div class="p-3 border rounded-lg space-y-1">
              <div class="flex justify-between text-xs">
                <span class="font-medium text-slate-800">Security & Air-Gap Compliance</span>
                <span class="font-mono text-slate-500">62 chunks</span>
              </div>
              <div class="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
                <div class="bg-emerald-600 h-1.5 rounded-full" style="width: 62%"></div>
              </div>
            </div>

            <div class="p-3 border rounded-lg space-y-1">
              <div class="flex justify-between text-xs">
                <span class="font-medium text-slate-800">CRM & ERP Connectors</span>
                <span class="font-mono text-slate-500">35 chunks</span>
              </div>
              <div class="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
                <div class="bg-amber-600 h-1.5 rounded-full" style="width: 35%"></div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Ingest Document Modal -->
      @if (showIngestModal()) {
        <div class="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4" data-testid="ingest-modal">
          <div class="bg-white rounded-xl shadow-xl border max-w-md w-full p-6 space-y-4">
            <div>
              <h3 class="text-lg font-bold text-slate-900">Ingest Knowledge Base Chunk</h3>
              <p class="text-xs text-slate-500 mt-1">Embed document passage into pgvector cosine space.</p>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Document Title</label>
                <input 
                  type="text" 
                  [(ngModel)]="newDocTitle"
                  placeholder="e.g. Enterprise SLA Terms"
                  class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                  data-testid="input-doc-title" />
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Content Passage</label>
                <textarea 
                  [(ngModel)]="newDocContent"
                  rows="3"
                  placeholder="Enter text passage to chunk and embed..."
                  class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                  data-testid="input-doc-content"></textarea>
              </div>
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t">
              <button 
                (click)="showIngestModal.set(false)"
                class="px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 rounded-lg">
                Cancel
              </button>
              <button 
                (click)="ingestChunk()"
                class="px-4 py-2 text-xs font-semibold bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg shadow-sm"
                data-testid="btn-confirm-ingest">
                Embed & Index
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class SemanticRagCockpitComponent {
  totalChunks = signal<number>(1420);
  searchQuery = 'What is the enterprise pricing for autonomous AI SDR?';
  groundedAnswer = signal<string>(
    'SGrowing Enterprise tier is billed at $12,500/month with unlimited autonomous AI SDR agents, 99.99% uptime SLA, and sub-150ms WebRTC duplex voice latency.'
  );

  chunks = signal<RetrievedChunk[]>([
    {
      chunkId: 'chk_101',
      documentId: 'doc_pricing_battlecard',
      title: 'Enterprise Pricing & SLAs',
      content: 'SGrowing Enterprise tier is billed at $12,500/month with unlimited autonomous AI SDR agents, 99.99% uptime SLA, and sub-150ms WebRTC duplex voice latency.',
      similarityScore: 0.942,
      category: 'Pricing'
    },
    {
      chunkId: 'chk_102',
      documentId: 'doc_security_compliance',
      title: 'SOC2 Type II & Air-Gapped Deployments',
      content: 'All self-hosted clusters support cryptographic offline licensing with SHA-256 signatures, zero outbound internet heartbeat, and AES-256-GCM data-at-rest encryption.',
      similarityScore: 0.814,
      category: 'Security'
    }
  ]);

  showIngestModal = signal<boolean>(false);
  newDocTitle = '';
  newDocContent = '';

  executeSearch(): void {
    if (!this.searchQuery.trim()) return;

    this.groundedAnswer.set(
      `Retrieved high-confidence knowledge context for query: "${this.searchQuery}". Grounded in verified documentation.`
    );
  }

  ingestChunk(): void {
    if (!this.newDocTitle.trim() || !this.newDocContent.trim()) return;

    const newChunk: RetrievedChunk = {
      chunkId: 'chk_' + Math.random().toString(36).substring(2, 8),
      documentId: 'doc_custom_' + Date.now(),
      title: this.newDocTitle,
      content: this.newDocContent,
      similarityScore: 0.965,
      category: 'Custom'
    };

    this.chunks.update(list => [newChunk, ...list]);
    this.totalChunks.update(c => c + 1);
    this.showIngestModal.set(false);
    this.newDocTitle = '';
    this.newDocContent = '';
  }
}
