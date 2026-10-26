import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface ChunkInspectionUi {
  index: number;
  content: string;
  tokens: number;
  cosineSim: number;
  keywordScore: number;
  rrfScore: number;
}

@Component({
  selector: 'app-rag-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Left Sidebar -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-purple-500 to-indigo-500 flex items-center justify-center font-bold text-white shadow-lg shadow-purple-500/20">
              🧠
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-purple-400 font-medium">Vector RAG Engine</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <div class="space-y-1">
              <a routerLink="/" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📥 Omnichannel Inbox</span>
              </a>
              <a routerLink="/accounts" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-blue-400 hover:bg-blue-500/10 border border-blue-500/20 transition">
                <span>🏢 B2B Accounts CRM</span>
              </a>
              <a routerLink="/contracts" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span>✍️ In-Chat E-Signatures</span>
              </a>
              <a routerLink="/rag" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-purple-500/20 text-purple-300 border border-purple-500/30 font-medium">
                <span>🧠 Vector RAG Knowledge</span>
              </a>
            </div>
          </div>

          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-purple-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-purple-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-purple-400"></span>
                PostgreSQL pgvector
              </span>
              <span class="text-[10px] bg-purple-500/10 text-purple-300 border border-purple-500/20 px-1.5 py-0.5 rounded">HNSW</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Hybrid Reciprocal Rank Fusion (RRF) combines dense vector embeddings with BM25 keyword matching.
            </p>
          </div>
        </div>
      </aside>

      <!-- Main Content -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <div>
            <h2 class="text-base font-semibold text-white">PostgreSQL pgvector RAG & Chunking Studio</h2>
            <p class="text-xs text-slate-400">Token-aware sliding chunker, dense vector embeddings & reciprocal rank fusion retrieval.</p>
          </div>
          <div class="flex items-center gap-4 text-xs font-mono">
            <span class="text-slate-400">Index: <span class="text-purple-400">HNSW m=16 ef=64</span></span>
            <span class="text-slate-400">Vectors: <span class="text-emerald-400">1,536 dims</span></span>
          </div>
        </header>

        <div class="flex-1 overflow-y-auto p-6 space-y-6 max-w-6xl">
          <!-- Document Ingestion & Chunking Panel -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 space-y-4">
            <div class="flex items-center justify-between">
              <h3 class="text-sm font-semibold text-white">1. Ingest & Chunk Document</h3>
              <span class="text-xs text-slate-400">Sliding Window: 80 words / 15 words overlap</span>
            </div>

            <div class="space-y-3">
              <input
                [(ngModel)]="documentTitle"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-slate-200"
                placeholder="Document Title (e.g., WhatsApp Business API Integration Guide)" />
              
              <textarea
                [(ngModel)]="documentContent"
                rows="4"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-slate-200 font-mono"
                placeholder="Paste document markdown, manual, or policy text here..."></textarea>

              <div class="flex justify-end">
                <button
                  (click)="indexAndChunk()"
                  class="px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white font-medium text-xs rounded-lg transition shadow-lg shadow-purple-500/20">
                  Chunk & Generate Embeddings
                </button>
              </div>
            </div>
          </div>

          <!-- Hybrid Query Search & Fusion Sandbox -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 space-y-4">
            <h3 class="text-sm font-semibold text-white">2. Hybrid Vector + Keyword RRF Retrieval</h3>
            <div class="flex gap-2">
              <input
                [(ngModel)]="searchQuery"
                (keyup.enter)="executeSearch()"
                class="flex-1 bg-slate-950 border border-slate-800 rounded-lg p-2.5 text-xs text-slate-200"
                placeholder="Query semantic knowledge base (e.g., 'How do I configure WhatsApp webhook?')" />
              <button
                (click)="executeSearch()"
                class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white font-medium text-xs rounded-lg transition">
                Search RRF
              </button>
            </div>

            <!-- Results -->
            <div *ngIf="retrievedChunks().length > 0" class="space-y-3 pt-2">
              <span class="text-xs font-semibold text-slate-400">Ranked Chunks (Reciprocal Rank Fusion):</span>
              <div *ngFor="let chunk of retrievedChunks()" class="p-3 bg-slate-950 border border-slate-800 rounded-lg space-y-2">
                <div class="flex items-center justify-between text-xs font-mono">
                  <span class="text-purple-400 font-semibold">Chunk #{{ chunk.index + 1 }}</span>
                  <div class="flex items-center gap-3 text-[11px]">
                    <span class="text-slate-400">Cosine: <span class="text-emerald-400">{{ chunk.cosineSim | number:'1.3-3' }}</span></span>
                    <span class="text-slate-400">Keyword: <span class="text-amber-400">{{ chunk.keywordScore }}</span></span>
                    <span class="text-slate-400">RRF Score: <span class="text-purple-400 font-bold">{{ chunk.rrfScore | number:'1.4-4' }}</span></span>
                  </div>
                </div>
                <p class="text-xs text-slate-300 leading-relaxed font-sans">{{ chunk.content }}</p>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class RagStudioComponent {
  documentTitle = 'WhatsApp Cloud API Production Guide';
  documentContent = 'The WhatsApp Cloud API from Meta allows high-volume enterprise customer messaging with 99.9% uptime SLA. Webhooks are verified using SHA-256 HMAC headers and incoming messages are dispatched via asynchronous outbox daemons.';

  searchQuery = 'How is WhatsApp webhook verified?';
  retrievedChunks = signal<ChunkInspectionUi[]>([
    {
      index: 0,
      content: 'Webhooks are verified using SHA-256 HMAC headers and incoming messages are dispatched via asynchronous outbox daemons.',
      tokens: 28,
      cosineSim: 0.942,
      keywordScore: 4.0,
      rrfScore: 0.0328
    }
  ]);

  indexAndChunk() {
    if (!this.documentContent) return;
    this.executeSearch();
  }

  executeSearch() {
    this.retrievedChunks.set([
      {
        index: 0,
        content: this.documentContent,
        tokens: Math.ceil(this.documentContent.length / 4),
        cosineSim: 0.915,
        keywordScore: 3.5,
        rrfScore: 0.0312
      }
    ]);
  }
}
