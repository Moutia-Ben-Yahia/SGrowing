import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface EnvelopeSignerUi {
  id: string;
  name: string;
  email: string;
  role: 'Signer' | 'Approver' | 'Viewer';
  orderIndex: number;
  status: 'Pending' | 'Signed' | 'Declined';
  signedAtUtc?: string;
}

export interface DocumentEnvelopeUi {
  id: string;
  title: string;
  documentUrl: string;
  status: 'Draft' | 'Sent' | 'PartiallySigned' | 'Completed';
  checksumSha256: string;
  isSequentialOrder: boolean;
  signers: EnvelopeSignerUi[];
  completedAtUtc?: string;
  totalDwellSeconds?: number;
  uniqueViewers?: number;
}

export interface PageDwellMetricUi {
  pageNumber: number;
  totalDwellSeconds: number;
  viewersCount: number;
  averageDwellSeconds: number;
}

@Component({
  selector: 'app-documents-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Left Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-cyan-500 to-blue-600 flex items-center justify-center font-bold text-white shadow-lg shadow-cyan-500/20">
              📜
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-cyan-400 font-medium">E-Sign & Doc Telemetry</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Core Suites</span>
            <div class="space-y-1">
              <a routerLink="/" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📥 Omnichannel Inbox</span>
              </a>
              <a routerLink="/documents" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 font-medium">
                <span>📜 Document Envelopes & Telemetry</span>
              </a>
              <a routerLink="/publications" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span>📰 Publication CMS & Newsletters</span>
              </a>
              <a routerLink="/forecasting" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span>📈 Sales Forecasts & Quotas</span>
              </a>
              <a routerLink="/roadmap" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-indigo-400 hover:bg-indigo-500/10 border border-indigo-500/20 transition">
                <span>🗺️ Public Roadmap & Voting</span>
              </a>
            </div>
          </div>

          <!-- Feature Highlight Box -->
          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-cyan-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-cyan-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-cyan-400 animate-pulse"></span>
                Documenso + Papermark
              </span>
              <span class="text-[10px] bg-cyan-500/10 text-cyan-300 border border-cyan-500/20 px-1.5 py-0.5 rounded">SHA-256</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Tamper-evident sequential multi-signer envelopes with slide dwell heatmaps & dynamic IP/email watermarking.
            </p>
          </div>
        </div>
      </aside>

      <!-- Main Stage -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <div>
            <h2 class="text-base font-semibold text-white">Cryptographic Document Tracking & Multi-Signer Envelopes</h2>
            <p class="text-xs text-slate-400">Sequential multi-signer execution, SHA-256 audit certificates, and page-by-page dwell telemetry.</p>
          </div>
          <div class="flex items-center gap-3">
            <button
              (click)="showCreateModal.set(true)"
              class="px-3.5 py-1.5 bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-cyan-500/20 flex items-center gap-1.5 transition">
              <span>+ Create Envelope</span>
            </button>
          </div>
        </header>

        <!-- Body Dashboard -->
        <div class="flex-1 overflow-y-auto p-6 space-y-6">
          <!-- KPI Metrics Row -->
          <div class="grid grid-cols-4 gap-4">
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Active Envelopes</span>
              <div class="text-2xl font-bold text-white mt-1">{{ envelopes().length }}</div>
              <span class="text-[11px] text-cyan-400 font-medium">Sequential verification active</span>
            </div>
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Completed & Verified</span>
              <div class="text-2xl font-bold text-emerald-400 mt-1">{{ completedCount() }}</div>
              <span class="text-[11px] text-emerald-400/80">Tamper-evident SHA-256 audit</span>
            </div>
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Total Dwell Time</span>
              <div class="text-2xl font-bold text-blue-400 mt-1">42m 18s</div>
              <span class="text-[11px] text-blue-400/80">Papermark page-by-page telemetry</span>
            </div>
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <span class="text-xs font-medium text-slate-400">Gated Documents</span>
              <div class="text-2xl font-bold text-purple-400 mt-1">100%</div>
              <span class="text-[11px] text-purple-400/80">Watermark + Passcode protected</span>
            </div>
          </div>

          <!-- Envelopes Table & Selected Details Grid -->
          <div class="grid grid-cols-3 gap-6">
            <!-- Left 2 Cols: Envelope List -->
            <div class="col-span-2 bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden">
              <div class="p-4 border-b border-slate-800 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-white">Document Envelopes</h3>
                <span class="text-xs text-slate-400">Click envelope to inspect telemetry & certificates</span>
              </div>
              <div class="divide-y divide-slate-800/80">
                @for (env of envelopes(); track env.id) {
                  <div
                    (click)="selectedEnvelope.set(env)"
                    [class.bg-slate-800-40]="selectedEnvelope()?.id === env.id"
                    class="p-4 hover:bg-slate-800/40 cursor-pointer transition flex items-center justify-between">
                    <div class="space-y-1">
                      <div class="flex items-center gap-2">
                        <span class="text-sm font-medium text-white">{{ env.title }}</span>
                        <span
                          [class.bg-emerald-500-10]="env.status === 'Completed'"
                          [class.text-emerald-400]="env.status === 'Completed'"
                          [class.border-emerald-500-20]="env.status === 'Completed'"
                          [class.bg-blue-500-10]="env.status === 'Sent'"
                          [class.text-blue-400]="env.status === 'Sent'"
                          [class.border-blue-500-20]="env.status === 'Sent'"
                          class="text-[10px] px-2 py-0.5 rounded-full border">
                          {{ env.status }}
                        </span>
                        @if (env.isSequentialOrder) {
                          <span class="text-[10px] bg-slate-800 text-slate-300 px-1.5 py-0.5 rounded border border-slate-700">
                            Sequential
                          </span>
                        }
                      </div>
                      <div class="text-xs text-slate-400 flex items-center gap-3">
                        <span class="font-mono text-[10px] text-slate-500">Hash: {{ env.checksumSha256.slice(0, 16) }}...</span>
                        <span>•</span>
                        <span>{{ env.signers.length }} Signers</span>
                        <span>•</span>
                        <span>{{ env.uniqueViewers || 3 }} Viewers ({{ env.totalDwellSeconds || 45 }}s dwell)</span>
                      </div>
                    </div>
                    <div class="flex items-center gap-2">
                      <button
                        (click)="openTelemetryModal(env); $event.stopPropagation()"
                        class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-xs transition border border-slate-700">
                        📊 Heatmap
                      </button>
                      <button
                        (click)="simulateSign(env); $event.stopPropagation()"
                        [disabled]="env.status === 'Completed'"
                        class="px-2.5 py-1 bg-cyan-600/20 hover:bg-cyan-600/30 text-cyan-300 border border-cyan-500/30 rounded text-xs transition disabled:opacity-40">
                        ✍️ Sign Next
                      </button>
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Right 1 Col: Selected Envelope Inspector -->
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 space-y-5">
              @if (selectedEnvelope(); as sel) {
                <div>
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-xs font-semibold text-cyan-400 uppercase tracking-wider">Envelope Inspector</span>
                    <span class="text-xs text-slate-400 font-mono">{{ sel.id.slice(0, 8) }}</span>
                  </div>
                  <h4 class="text-sm font-bold text-white">{{ sel.title }}</h4>
                </div>

                <!-- Integrity Certificate Box -->
                <div class="p-3 bg-slate-950/80 border border-slate-800 rounded-lg space-y-2">
                  <div class="flex items-center justify-between text-xs">
                    <span class="text-slate-400">Cryptographic Integrity</span>
                    <span class="text-emerald-400 font-medium">Valid SHA-256</span>
                  </div>
                  <div class="p-2 bg-slate-900 rounded font-mono text-[10px] text-slate-300 break-all select-all">
                    {{ sel.checksumSha256 }}
                  </div>
                </div>

                <!-- Signers Execution Order -->
                <div class="space-y-2">
                  <span class="text-xs font-semibold text-slate-300">Signers Execution Chain</span>
                  <div class="space-y-2">
                    @for (s of sel.signers; track s.id) {
                      <div class="p-2.5 bg-slate-950/60 border border-slate-800 rounded-lg flex items-center justify-between text-xs">
                        <div>
                          <div class="font-medium text-slate-200">
                            #{{ s.orderIndex }} {{ s.name }} ({{ s.role }})
                          </div>
                          <div class="text-slate-400 text-[11px]">{{ s.email }}</div>
                        </div>
                        <div>
                          <span
                            [class.text-emerald-400]="s.status === 'Signed'"
                            [class.text-amber-400]="s.status === 'Pending'"
                            class="font-semibold text-[11px]">
                            {{ s.status }}
                          </span>
                        </div>
                      </div>
                    }
                  </div>
                </div>

                <!-- Dynamic Security Gate -->
                <div class="p-3 bg-cyan-950/20 border border-cyan-800/40 rounded-lg space-y-1.5 text-xs">
                  <div class="font-semibold text-cyan-300">Papermark Security Gate</div>
                  <div class="text-slate-400 text-[11px]">Dynamic Watermark: <code class="text-slate-300">CONFIDENTIAL - &#123;EMAIL&#125; - &#123;IP&#125;</code></div>
                  <div class="text-slate-400 text-[11px]">Passcode Verification: <span class="text-emerald-400 font-mono">Enforced (SHA-256)</span></div>
                </div>
              } @else {
                <div class="text-center py-12 text-slate-500 text-xs">
                  Select an envelope from the list to view cryptographic integrity and signers chain.
                </div>
              }
            </div>
          </div>
        </div>
      </main>
    </div>

    <!-- Create Envelope Modal -->
    @if (showCreateModal()) {
      <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg p-6 space-y-4 shadow-2xl">
          <div class="flex items-center justify-between border-b border-slate-800 pb-3">
            <h3 class="text-sm font-semibold text-white">New Cryptographic Multi-Signer Envelope</h3>
            <button (click)="showCreateModal.set(false)" class="text-slate-400 hover:text-white text-sm">✕</button>
          </div>

          <div class="space-y-3">
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Envelope Title</label>
              <input
                type="text"
                [(ngModel)]="newTitle"
                placeholder="e.g. Master Services Agreement 2026"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-cyan-500" />
            </div>

            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Document URL / Raw PDF</label>
              <input
                type="text"
                [(ngModel)]="newDocUrl"
                placeholder="https://cdn.sgrowing.io/contracts/msa-corp.pdf"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-cyan-500" />
            </div>

            <div class="flex items-center gap-2 pt-1">
              <input type="checkbox" [(ngModel)]="newIsSequential" id="seqOrder" class="rounded bg-slate-950 border-slate-700 text-cyan-500 focus:ring-0" />
              <label for="seqOrder" class="text-xs text-slate-300 cursor-pointer">Enforce Sequential Execution Order (Signer 1 must complete before Signer 2)</label>
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-3 border-t border-slate-800">
            <button
              (click)="showCreateModal.set(false)"
              class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-medium transition">
              Cancel
            </button>
            <button
              (click)="saveEnvelope()"
              class="px-4 py-2 bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-500 hover:to-blue-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-cyan-500/20 transition">
              Create & Dispatch Envelope
            </button>
          </div>
        </div>
      </div>
    }

    <!-- Telemetry Dwell Modal -->
    @if (showTelemetryModal()) {
      <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-xl p-6 space-y-4 shadow-2xl">
          <div class="flex items-center justify-between border-b border-slate-800 pb-3">
            <div>
              <h3 class="text-sm font-semibold text-white">Papermark Dwell Time Heatmap</h3>
              <p class="text-xs text-slate-400">Page-by-page engagement telemetry & viewer attention analysis</p>
            </div>
            <button (click)="showTelemetryModal.set(false)" class="text-slate-400 hover:text-white text-sm">✕</button>
          </div>

          <div class="space-y-3">
            @for (p of samplePageMetrics; track p.pageNumber) {
              <div class="space-y-1">
                <div class="flex justify-between text-xs">
                  <span class="text-slate-300 font-medium">Slide / Page {{ p.pageNumber }}</span>
                  <span class="text-cyan-400 font-mono">{{ p.totalDwellSeconds }}s total (avg {{ p.averageDwellSeconds }}s)</span>
                </div>
                <div class="w-full bg-slate-950 rounded-full h-3 overflow-hidden border border-slate-800">
                  <div
                    class="bg-gradient-to-r from-cyan-500 to-blue-500 h-full rounded-full transition-all duration-500"
                    [style.width.%]="(p.totalDwellSeconds / 60) * 100">
                  </div>
                </div>
              </div>
            }
          </div>

          <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 flex justify-between text-xs text-slate-300">
            <div>Total Viewers: <span class="font-bold text-white">4 VCs</span></div>
            <div>Completion Rate: <span class="font-bold text-emerald-400">92%</span></div>
            <div>Avg Dwell / Slide: <span class="font-bold text-cyan-400">18.4s</span></div>
          </div>

          <div class="flex justify-end pt-2">
            <button (click)="showTelemetryModal.set(false)" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs">
              Close Telemetry
            </button>
          </div>
        </div>
      </div>
    }
  `
})
export class DocumentsStudioComponent implements OnInit {
  showCreateModal = signal(false);
  showTelemetryModal = signal(false);
  selectedEnvelope = signal<DocumentEnvelopeUi | null>(null);

  newTitle = '';
  newDocUrl = '';
  newIsSequential = true;

  samplePageMetrics: PageDwellMetricUi[] = [
    { pageNumber: 1, totalDwellSeconds: 45, viewersCount: 4, averageDwellSeconds: 11.2 },
    { pageNumber: 2, totalDwellSeconds: 78, viewersCount: 4, averageDwellSeconds: 19.5 },
    { pageNumber: 3, totalDwellSeconds: 110, viewersCount: 4, averageDwellSeconds: 27.5 },
    { pageNumber: 4, totalDwellSeconds: 32, viewersCount: 3, averageDwellSeconds: 10.6 }
  ];

  envelopes = signal<DocumentEnvelopeUi[]>([
    {
      id: 'env-101',
      title: 'Enterprise Annual License - Acme Corp',
      documentUrl: 'https://cdn.sgrowing.io/contracts/acme-2026.pdf',
      status: 'PartiallySigned',
      checksumSha256: 'B473A426651312C4D1DBE5C773E05D824408E69242CD771B08B60E32AE85D021',
      isSequentialOrder: true,
      totalDwellSeconds: 265,
      uniqueViewers: 4,
      signers: [
        { id: 'sig-1', name: 'Patrick Collison', email: 'patrick@acme.com', role: 'Signer', orderIndex: 1, status: 'Signed', signedAtUtc: '2026-10-26T10:15:00Z' },
        { id: 'sig-2', name: 'John Collison', email: 'john@acme.com', role: 'Approver', orderIndex: 2, status: 'Pending' }
      ]
    },
    {
      id: 'env-102',
      title: 'Series B Investor Shareholder Agreement',
      documentUrl: 'https://cdn.sgrowing.io/docs/shareholder-agreement.pdf',
      status: 'Completed',
      checksumSha256: '7EBC3802E2A89DCB6043C0C8BF1A7C218B80D2CA94380D47C35B9BA558299C94',
      isSequentialOrder: true,
      totalDwellSeconds: 490,
      uniqueViewers: 6,
      signers: [
        { id: 'sig-3', name: 'Marc Andreessen', email: 'marc@a16z.com', role: 'Signer', orderIndex: 1, status: 'Signed', signedAtUtc: '2026-10-25T14:30:00Z' },
        { id: 'sig-4', name: 'Ben Horowitz', email: 'ben@a16z.com', role: 'Signer', orderIndex: 2, status: 'Signed', signedAtUtc: '2026-10-25T16:45:00Z' }
      ]
    }
  ]);

  completedCount = signal(1);

  ngOnInit() {
    this.selectedEnvelope.set(this.envelopes()[0]);
  }

  openTelemetryModal(env: DocumentEnvelopeUi) {
    this.selectedEnvelope.set(env);
    this.showTelemetryModal.set(true);
  }

  simulateSign(env: DocumentEnvelopeUi) {
    const pendingSigner = env.signers.find(s => s.status === 'Pending');
    if (!pendingSigner) return;

    pendingSigner.status = 'Signed';
    pendingSigner.signedAtUtc = new Date().toISOString();

    const remaining = env.signers.filter(s => s.status === 'Pending');
    if (remaining.length === 0) {
      env.status = 'Completed';
      env.completedAtUtc = new Date().toISOString();
      this.completedCount.update(c => c + 1);
    } else {
      env.status = 'PartiallySigned';
    }
  }

  saveEnvelope() {
    if (!this.newTitle) return;
    const newEnv: DocumentEnvelopeUi = {
      id: 'env-' + (Math.floor(Math.random() * 900) + 100),
      title: this.newTitle,
      documentUrl: this.newDocUrl || 'https://cdn.sgrowing.io/docs/generic.pdf',
      status: 'Sent',
      checksumSha256: '9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08',
      isSequentialOrder: this.newIsSequential,
      signers: [
        { id: 'sig-a', name: 'Lead Signer', email: 'lead@client.com', role: 'Signer', orderIndex: 1, status: 'Pending' },
        { id: 'sig-b', name: 'VP Legal', email: 'legal@client.com', role: 'Approver', orderIndex: 2, status: 'Pending' }
      ],
      totalDwellSeconds: 0,
      uniqueViewers: 0
    };

    this.envelopes.update(list => [newEnv, ...list]);
    this.selectedEnvelope.set(newEnv);
    this.showCreateModal.set(false);
    this.newTitle = '';
    this.newDocUrl = '';
  }
}
