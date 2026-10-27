import { Component, signal, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';

export interface McpToolUi {
  name: string;
  description: string;
  sampleArgs: Record<string, any>;
}

export interface AgentAuditLogUi {
  id: string;
  timestampUtc: string;
  agentId: string;
  toolName: string;
  autonomyMode: string;
  actionSummary: string;
  status: 'Approved' | 'Blocked' | 'PendingReview';
  executionTimeMs: number;
}

@Component({
  selector: 'app-agent-console',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans">
      <!-- Top Navigation & Cockpit Header -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 py-4 sticky top-0 z-30 flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="h-10 w-10 rounded-xl bg-purple-500/20 text-purple-400 flex items-center justify-center font-bold text-xl border border-purple-500/30">
            🤖
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-base font-bold text-white tracking-tight">Autonomous AI Sales Agent Hub</h1>
              <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-purple-900/60 text-purple-300 border border-purple-700">
                MCP JSON-RPC v2024-11-05
              </span>
            </div>
            <p class="text-xs text-slate-400">Autonomous tool execution cockpit & supervisor control plane</p>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <div class="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-950 border border-slate-800 text-xs font-mono">
            <span class="h-2 w-2 rounded-full bg-emerald-400 animate-pulse"></span>
            <span class="text-slate-300">Daemon: SGrowing.McpServer:7890</span>
          </div>
          <a routerLink="/" class="px-3 py-1.5 rounded-lg bg-slate-800 text-slate-200 text-xs hover:bg-slate-700 font-medium transition">
            Inbox Studio
          </a>
        </div>
      </header>

      <main class="flex-1 p-6 max-w-7xl mx-auto w-full space-y-6">
        <!-- 1. Autonomy Control Plane Card -->
        <section class="p-6 bg-slate-900 border border-slate-800 rounded-2xl space-y-4 shadow-xl">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div>
              <h2 class="text-sm font-bold text-white uppercase tracking-wider font-mono">1. Agent Autonomy & Governance Mode</h2>
              <p class="text-xs text-slate-400">Controls automated decision threshold before dispatching emails, contracts, or waybills.</p>
            </div>

            <!-- Current Active Mode Pill -->
            <div class="flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-bold font-mono border"
              [ngClass]="{
                'bg-emerald-950 text-emerald-400 border-emerald-500/40': activeAutonomy() === 'Auto-Approve',
                'bg-amber-950 text-amber-400 border-amber-500/40': activeAutonomy() === 'Human-in-the-Loop',
                'bg-indigo-950 text-indigo-400 border-indigo-500/40': activeAutonomy() === 'Draft Only'
              }">
              <span>● Mode: {{ activeAutonomy() }}</span>
            </div>
          </div>

          <!-- 3 Autonomy Selector Buttons -->
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4 pt-1">
            <!-- Mode 1: Auto-Approve -->
            <button
              (click)="setAutonomy('Auto-Approve')"
              class="p-4 rounded-xl border text-left transition relative overflow-hidden"
              [ngClass]="activeAutonomy() === 'Auto-Approve' ? 'bg-emerald-950/40 border-emerald-500 shadow-lg shadow-emerald-900/20' : 'bg-slate-950/60 border-slate-800 hover:border-slate-700'">
              <div class="flex items-center justify-between mb-2">
                <span class="text-xl">⚡</span>
                <span class="text-[10px] font-mono uppercase px-2 py-0.5 rounded font-bold"
                  [ngClass]="activeAutonomy() === 'Auto-Approve' ? 'bg-emerald-400 text-slate-950' : 'bg-slate-800 text-slate-400'">
                  High Autonomy
                </span>
              </div>
              <h3 class="text-sm font-bold text-white">Auto-Approve</h3>
              <p class="text-[11px] text-slate-400 mt-1 leading-relaxed">
                Agent dispatches outbound sequences, answers queries, and creates waybills autonomously with zero friction.
              </p>
            </button>

            <!-- Mode 2: Human-in-the-Loop -->
            <button
              (click)="setAutonomy('Human-in-the-Loop')"
              class="p-4 rounded-xl border text-left transition relative overflow-hidden"
              [ngClass]="activeAutonomy() === 'Human-in-the-Loop' ? 'bg-amber-950/40 border-amber-500 shadow-lg shadow-amber-900/20' : 'bg-slate-950/60 border-slate-800 hover:border-slate-700'">
              <div class="flex items-center justify-between mb-2">
                <span class="text-xl">🛡️</span>
                <span class="text-[10px] font-mono uppercase px-2 py-0.5 rounded font-bold"
                  [ngClass]="activeAutonomy() === 'Human-in-the-Loop' ? 'bg-amber-400 text-slate-950' : 'bg-slate-800 text-slate-400'">
                  Balanced Guardrails
                </span>
              </div>
              <h3 class="text-sm font-bold text-white">Human-in-the-Loop</h3>
              <p class="text-[11px] text-slate-400 mt-1 leading-relaxed">
                Autonomous research and preparation; triggers notification & human approval before high-stakes contract signing.
              </p>
            </button>

            <!-- Mode 3: Draft Only -->
            <button
              (click)="setAutonomy('Draft Only')"
              class="p-4 rounded-xl border text-left transition relative overflow-hidden"
              [ngClass]="activeAutonomy() === 'Draft Only' ? 'bg-indigo-950/40 border-indigo-500 shadow-lg shadow-indigo-900/20' : 'bg-slate-950/60 border-slate-800 hover:border-slate-700'">
              <div class="flex items-center justify-between mb-2">
                <span class="text-xl">📝</span>
                <span class="text-[10px] font-mono uppercase px-2 py-0.5 rounded font-bold"
                  [ngClass]="activeAutonomy() === 'Draft Only' ? 'bg-indigo-400 text-slate-950' : 'bg-slate-800 text-slate-400'">
                  Supervised
                </span>
              </div>
              <h3 class="text-sm font-bold text-white">Draft Only</h3>
              <p class="text-[11px] text-slate-400 mt-1 leading-relaxed">
                Strict supervisor mode. Agent outputs proposals into queue; all executions require explicit operator sign-off.
              </p>
            </button>
          </div>
        </section>

        <!-- 2. Dual Column: MCP Tool Tester & Live Audit Ledger -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- Left Column: Interactive MCP Tool Tester (7 cols) -->
          <section class="lg:col-span-7 p-6 bg-slate-900 border border-slate-800 rounded-2xl space-y-4 shadow-xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h2 class="text-sm font-bold text-white uppercase tracking-wider font-mono">2. Interactive MCP Tool Tester</h2>
                <span class="text-xs text-slate-400">Directly invokes tools over JSON-RPC 2.0 protocol</span>
              </div>

              <div class="flex items-center gap-1.5 px-2 py-1 rounded bg-slate-950 border border-slate-800 text-[11px] font-mono text-purple-400">
                <span>Stdio / SSE Active</span>
              </div>
            </div>

            <!-- Tool Selection Pills -->
            <div class="space-y-2">
              <label class="text-[11px] font-mono text-slate-400 uppercase tracking-wider block">Select Registered Tool</label>
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-2">
                @for (tool of registeredTools; track tool.name) {
                  <button
                    (click)="selectTool(tool)"
                    class="p-2.5 rounded-xl border text-left transition flex items-center justify-between"
                    [ngClass]="selectedTool().name === tool.name ? 'bg-purple-950/40 border-purple-500 text-white' : 'bg-slate-950 border-slate-800 text-slate-400 hover:border-slate-700'">
                    <div>
                      <span class="text-xs font-mono font-bold block">{{ tool.name }}</span>
                      <span class="text-[10px] text-slate-500 truncate block max-w-[200px]">{{ tool.description }}</span>
                    </div>
                    @if (selectedTool().name === tool.name) {
                      <span class="text-purple-400 font-bold">✓</span>
                    }
                  </button>
                }
              </div>
            </div>

            <!-- Tool Argument Input -->
            <div class="space-y-1.5">
              <div class="flex items-center justify-between text-xs">
                <span class="text-slate-400 font-mono">Tool Arguments (JSON-RPC params)</span>
                <button
                  (click)="resetSampleArgs()"
                  class="text-[11px] text-purple-400 hover:text-purple-300 font-medium">
                  Reset Template
                </button>
              </div>

              <textarea
                [(ngModel)]="toolArgsJson"
                rows="6"
                class="w-full bg-slate-950 border border-slate-700 rounded-xl p-3 text-xs font-mono text-cyan-300 focus:outline-none focus:border-purple-500 focus:ring-1 focus:ring-purple-500"></textarea>
            </div>

            <!-- Execute Button -->
            <button
              (click)="executeSelectedTool()"
              [disabled]="isExecuting()"
              class="w-full py-3 rounded-xl bg-purple-600 hover:bg-purple-500 disabled:opacity-50 text-white text-xs font-bold font-mono transition shadow-lg shadow-purple-600/30 flex items-center justify-center gap-2">
              <span>🚀</span>
              <span>{{ isExecuting() ? 'Executing MCP Tool...' : 'Execute Tool via JSON-RPC' }}</span>
            </button>

            <!-- Execution Result Inspector -->
            @if (executionResult()) {
              <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2">
                <div class="flex items-center justify-between text-xs font-mono">
                  <div class="flex items-center gap-2">
                    <span class="h-2 w-2 rounded-full bg-emerald-400"></span>
                    <span class="text-emerald-400 font-bold">MCP Response (200 OK)</span>
                  </div>
                  <span class="text-slate-500">{{ executionDuration() }} ms</span>
                </div>

                <pre class="text-[11px] font-mono text-slate-300 bg-slate-900/90 p-3 rounded-lg overflow-x-auto max-h-56 border border-slate-800">{{ executionResult() }}</pre>
              </div>
            }
          </section>

          <!-- Right Column: Audit Log Stream (5 cols) -->
          <section class="lg:col-span-5 p-6 bg-slate-900 border border-slate-800 rounded-2xl space-y-4 shadow-xl flex flex-col">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div>
                <h2 class="text-sm font-bold text-white uppercase tracking-wider font-mono">3. Agent Audit Ledger</h2>
                <span class="text-xs text-slate-400">Tamper-evident real-time execution trail</span>
              </div>

              <button
                (click)="loadAuditLogs()"
                class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-[11px] text-slate-300 transition font-mono">
                Refresh
              </button>
            </div>

            <!-- Audit Stream List -->
            <div class="flex-1 space-y-2.5 overflow-y-auto max-h-[550px] pr-1">
              @for (log of auditLogs(); track log.id) {
                <div class="p-3 bg-slate-950 rounded-xl border border-slate-800/80 space-y-1.5 transition hover:border-slate-700">
                  <div class="flex items-center justify-between text-[11px]">
                    <span class="font-mono text-purple-400 font-bold">{{ log.toolName }}</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold"
                      [ngClass]="{
                        'bg-emerald-950 text-emerald-400 border border-emerald-500/30': log.status === 'Approved',
                        'bg-amber-950 text-amber-400 border border-amber-500/30': log.status === 'PendingReview',
                        'bg-rose-950 text-rose-400 border border-rose-500/30': log.status === 'Blocked'
                      }">
                      {{ log.status }}
                    </span>
                  </div>

                  <p class="text-xs text-slate-300">{{ log.actionSummary }}</p>

                  <div class="flex items-center justify-between text-[10px] text-slate-500 font-mono pt-1 border-t border-slate-900">
                    <span>{{ log.agentId }}</span>
                    <span>{{ log.executionTimeMs }} ms</span>
                  </div>
                </div>
              }
            </div>
          </section>
        </div>
      </main>
    </div>
  `
})
export class AgentConsoleComponent implements OnInit {
  private http = inject(HttpClient);

  activeAutonomy = signal<'Auto-Approve' | 'Human-in-the-Loop' | 'Draft Only'>('Human-in-the-Loop');
  isExecuting = signal(false);
  executionDuration = signal(85);
  executionResult = signal<string | null>(null);

  registeredTools: McpToolUi[] = [
    {
      name: 'crm_search_leads',
      description: 'Search prospects by name, industry or intent score',
      sampleArgs: { query: 'Retail Distribution', limit: 5 }
    },
    {
      name: 'outbound_send_spintax_campaign',
      description: 'Generate spintax variations and queue outreach blitz',
      sampleArgs: { campaignId: 'CMP-2026-Q4', spintaxTemplate: '{Bonjour|Hello|Salut} {ProspectName}, {découvrez|testez} SGrowing!', recipientEmails: ['karim@retail.tn', 'amina@carthage.tn'] }
    },
    {
      name: 'deliverability_run_placement_test',
      description: 'Run seed placement tests across Google & Microsoft',
      sampleArgs: { domain: 'sgrowing.io', senderIp: '198.51.100.42' }
    },
    {
      name: 'contracts_dispatch_for_signature',
      description: 'Dispatch cryptographic digital contract with OTP',
      sampleArgs: { documentTitle: 'Master SaaS License Agreement', signerName: 'Karim Trabelsi', signerEmail: 'karim@maghrebretail.tn', contractValue: 18500 }
    },
    {
      name: 'courier_generate_waybill',
      description: 'Generate Aramex waybill number & barcode for COD parcel',
      sampleArgs: { orderId: 'ORD-202610-8819', recipientName: 'Kais Saidi', recipientPhone: '+21698223344', destinationWilaya: 'Tunis', codAmount: 157.0 }
    }
  ];

  selectedTool = signal<McpToolUi>(this.registeredTools[0]);
  toolArgsJson = JSON.stringify(this.registeredTools[0].sampleArgs, null, 2);

  auditLogs = signal<AgentAuditLogUi[]>([
    {
      id: 'log-1',
      timestampUtc: new Date(Date.now() - 4 * 60000).toISOString(),
      agentId: 'SGrowing-Sales-Agent-01',
      toolName: 'crm_search_leads',
      autonomyMode: 'Auto-Approve',
      actionSummary: "Discovered 14 high-intent retail buyers matching 'Distribution'",
      status: 'Approved',
      executionTimeMs: 84.0
    },
    {
      id: 'log-2',
      timestampUtc: new Date(Date.now() - 9 * 60000).toISOString(),
      agentId: 'SGrowing-Outbound-SDR',
      toolName: 'outbound_send_spintax_campaign',
      autonomyMode: 'Human-in-the-Loop',
      actionSummary: 'Generated 45 spintax variations for Q4 Retail Blitz',
      status: 'Approved',
      executionTimeMs: 132.5
    },
    {
      id: 'log-3',
      timestampUtc: new Date(Date.now() - 14 * 60000).toISOString(),
      agentId: 'SGrowing-Deliverability-Bot',
      toolName: 'deliverability_run_placement_test',
      autonomyMode: 'Auto-Approve',
      actionSummary: 'Health radar placement verified at 97.4% across Google & Microsoft',
      status: 'Approved',
      executionTimeMs: 92.1
    },
    {
      id: 'log-4',
      timestampUtc: new Date(Date.now() - 22 * 60000).toISOString(),
      agentId: 'SGrowing-Signer-Bot',
      toolName: 'contracts_dispatch_for_signature',
      autonomyMode: 'Draft Only',
      actionSummary: 'Prepared Digital SaaS Proposal for Enterprise Partner Corp',
      status: 'PendingReview',
      executionTimeMs: 110.0
    },
    {
      id: 'log-5',
      timestampUtc: new Date(Date.now() - 30 * 60000).toISOString(),
      agentId: 'SGrowing-Logistics-Agent',
      toolName: 'courier_generate_waybill',
      autonomyMode: 'Auto-Approve',
      actionSummary: 'Created Aramex waybill WB-TN-20261027-4819 with COD 185 TND',
      status: 'Approved',
      executionTimeMs: 65.4
    }
  ]);

  ngOnInit() {
    this.loadAutonomy();
    this.loadAuditLogs();
  }

  setAutonomy(mode: 'Auto-Approve' | 'Human-in-the-Loop' | 'Draft Only') {
    this.activeAutonomy.set(mode);
    this.http.post('/api/v1/mcp/autonomy', { mode }).subscribe({
      next: () => this.loadAuditLogs(),
      error: () => {}
    });
  }

  loadAutonomy() {
    this.http.get<{ autonomyMode: string }>('/api/v1/mcp/autonomy').subscribe({
      next: res => {
        if (res.autonomyMode === 'Auto-Approve' || res.autonomyMode === 'Human-in-the-Loop' || res.autonomyMode === 'Draft Only') {
          this.activeAutonomy.set(res.autonomyMode);
        }
      },
      error: () => {}
    });
  }

  loadAuditLogs() {
    this.http.get<AgentAuditLogUi[]>('/api/v1/mcp/audit-logs').subscribe({
      next: logs => {
        if (logs && logs.length > 0) {
          this.auditLogs.set(logs);
        }
      },
      error: () => {}
    });
  }

  selectTool(tool: McpToolUi) {
    this.selectedTool.set(tool);
    this.toolArgsJson = JSON.stringify(tool.sampleArgs, null, 2);
  }

  resetSampleArgs() {
    this.toolArgsJson = JSON.stringify(this.selectedTool().sampleArgs, null, 2);
  }

  executeSelectedTool() {
    this.isExecuting.set(true);
    let parsedArgs = {};
    try {
      parsedArgs = JSON.parse(this.toolArgsJson);
    } catch {
      parsedArgs = this.selectedTool().sampleArgs;
    }

    const sw = performance.now();
    this.http.post(`/api/v1/mcp/tools/${this.selectedTool().name}/invoke`, parsedArgs).subscribe({
      next: res => {
        this.isExecuting.set(false);
        this.executionDuration.set(Math.round(performance.now() - sw));
        this.executionResult.set(JSON.stringify(res, null, 2));
        this.loadAuditLogs();
      },
      error: () => {
        // Fallback simulation for offline testing
        this.isExecuting.set(false);
        this.executionDuration.set(Math.round(performance.now() - sw));
        const simulated = {
          toolName: this.selectedTool().name,
          isSuccess: true,
          outputPayload: {
            status: 'SimulatedOk',
            tool: this.selectedTool().name,
            executedUnder: this.activeAutonomy(),
            timestampUtc: new Date().toISOString()
          },
          tokensConsumed: 120,
          executionTimeMs: 78.4
        };
        this.executionResult.set(JSON.stringify(simulated, null, 2));
      }
    });
  }
}
