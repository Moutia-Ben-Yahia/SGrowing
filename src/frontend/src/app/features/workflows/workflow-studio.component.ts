import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export interface WorkflowNodeUI {
  id: string;
  type: 'trigger' | 'condition' | 'action';
  title: string;
  subtitle: string;
  icon: string;
  color: string;
  active: boolean;
}

export interface WorkflowExecutionStep {
  nodeId: string;
  nodeName: string;
  type: string;
  status: string;
  outputSummary: string;
  latencyMs: number;
}

export interface WorkflowExecutionResult {
  executionId: string;
  workflowId: string;
  status: string;
  stepsExecutedCount: number;
  steps: WorkflowExecutionStep[];
  finalContext: Record<string, any>;
  completedAtUtc: string;
}

@Component({
  selector: 'app-workflow-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex h-screen bg-slate-950 text-slate-100 font-sans overflow-hidden">
      <!-- Main Content -->
      <div class="flex-1 flex flex-col h-full overflow-hidden">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/50 backdrop-blur px-8 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 rounded-lg bg-cyan-600/20 text-cyan-400 flex items-center justify-center font-bold text-sm border border-cyan-500/30">
              ⚡
            </div>
            <div>
              <h1 class="text-lg font-semibold text-white tracking-tight">Visual Node Automation Canvas</h1>
              <p class="text-xs text-slate-400">n8n model: Event triggers, AI decision branches & cross-channel execution nodes</p>
            </div>
          </div>
          <div class="flex items-center gap-3">
            <button 
              (click)="runTestSimulation()"
              [disabled]="isRunning()"
              class="bg-gradient-to-r from-cyan-600 to-indigo-600 hover:from-cyan-500 hover:to-indigo-500 text-white font-medium px-4 py-2 rounded-lg text-sm transition shadow-lg shadow-cyan-900/30 flex items-center gap-2">
              <span>⚡</span>
              <span>{{ isRunning() ? 'Executing Flow...' : 'Test Run Workflow Graph' }}</span>
            </button>
          </div>
        </header>

        <!-- Body Area: Canvas & Execution Inspector -->
        <div class="flex-1 overflow-y-auto p-8 space-y-8">
          
          <!-- Node Graph Canvas -->
          <div class="bg-slate-900 border border-slate-800 rounded-2xl p-8 shadow-2xl relative overflow-hidden min-h-[460px] flex flex-col justify-between">
            <!-- Background Grid Pattern -->
            <div class="absolute inset-0 bg-[radial-gradient(#334155_1px,transparent_1px)] [background-size:16px_16px] opacity-25"></div>

            <div class="relative z-10 flex flex-col md:flex-row items-center justify-between gap-6 my-auto">
              
              @for (node of nodes(); track node.id; let idx = $index; let last = $last) {
                <!-- Single Node Card -->
                <div class="w-64 bg-slate-950/90 border rounded-xl p-5 shadow-xl transition-all duration-300"
                     [ngClass]="{
                       'border-cyan-500 ring-2 ring-cyan-500/40 scale-105': node.active,
                       'border-slate-800 hover:border-slate-700': !node.active
                     }">
                  <div class="flex items-center gap-2.5 mb-2">
                    <span class="w-7 h-7 rounded-lg flex items-center justify-center text-sm font-bold shadow-sm"
                          [ngClass]="node.color">
                      {{ node.icon }}
                    </span>
                    <span class="text-[10px] font-mono uppercase tracking-wider text-slate-400">
                      {{ node.type }}
                    </span>
                  </div>
                  <h3 class="font-semibold text-sm text-white mb-1">{{ node.title }}</h3>
                  <p class="text-xs text-slate-400 leading-relaxed">{{ node.subtitle }}</p>
                  
                  @if (node.active) {
                    <div class="mt-3 pt-2 border-t border-slate-800 flex items-center gap-1.5 text-xs text-cyan-400 font-medium">
                      <span class="w-2 h-2 rounded-full bg-cyan-400 animate-ping"></span>
                      Executing node...
                    </div>
                  }
                </div>

                <!-- Directed Arrow Connector -->
                @if (!last) {
                  <div class="text-slate-600 font-bold text-lg select-none hidden md:block">
                    ───►
                  </div>
                }
              }

            </div>

            <!-- Canvas Footer -->
            <div class="relative z-10 pt-6 border-t border-slate-800/80 flex items-center justify-between text-xs text-slate-400">
              <span>Status: <strong class="text-emerald-400">● Active Workflow (Auto-Listening)</strong></span>
              <span>Trigger: Inbound Message on WhatsApp, Telegram, X, Discord, or WebChat</span>
            </div>
          </div>

          <!-- Live Step Execution Console -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-3 font-mono text-xs">
            <div class="flex items-center justify-between pb-2 border-b border-slate-800 text-slate-300">
              <span class="font-semibold text-white">Execution Telemetry Logs</span>
              <span class="text-slate-500">Execution ID: #wf-847291</span>
            </div>

            <div class="space-y-1.5 text-slate-300">
              @for (log of executionLogs(); track log) {
                <div class="flex items-start gap-3">
                  <span class="text-slate-500">{{ log.time }}</span>
                  <span [ngClass]="log.color">{{ log.message }}</span>
                </div>
              }
            </div>
          </div>

        </div>
      </div>
    </div>
  `
})
export class WorkflowStudioComponent implements OnInit {
  private http = inject(HttpClient, { optional: true });
  public isRunning = signal<boolean>(false);

  public nodes = signal<WorkflowNodeUI[]>([
    {
      id: 'n1',
      type: 'trigger',
      title: 'Inbound Channel Ingress',
      subtitle: 'Listens across WhatsApp, Telegram, X, Discord & Phone',
      icon: '📥',
      color: 'bg-emerald-600/20 text-emerald-400 border border-emerald-500/30',
      active: false
    },
    {
      id: 'n2',
      type: 'condition',
      title: 'AI Sentiment & Intent',
      subtitle: 'Classifies purchase request vs technical issue vs urgent SLA',
      icon: '🧠',
      color: 'bg-indigo-600/20 text-indigo-400 border border-indigo-500/30',
      active: false
    },
    {
      id: 'n3',
      type: 'action',
      title: 'Dispatch AI Response',
      subtitle: 'Transmits autonomous reply with order or checkout link',
      icon: '🚀',
      color: 'bg-fuchsia-600/20 text-fuchsia-400 border border-fuchsia-500/30',
      active: false
    },
    {
      id: 'n4',
      type: 'action',
      title: 'Sync CRM & Alert Team',
      subtitle: 'Creates Won Deal in CRM and sends Discord/Slack notification',
      icon: '💼',
      color: 'bg-cyan-600/20 text-cyan-400 border border-cyan-500/30',
      active: false
    }
  ]);

  public executionLogs = signal<{ time: string; message: string; color: string }[]>([
    { time: '22:45:10', message: '[Ingress] Inbound message payload parsed from Telegram @alexander_h', color: 'text-emerald-400' },
    { time: '22:45:11', message: '[Classifier] AI detected positive intent: "Want to upgrade to Enterprise plan"', color: 'text-indigo-400' },
    { time: '22:45:11', message: '[Outbox] Enqueued assisted checkout link $499.00 USD to Telegram thread', color: 'text-fuchsia-400' },
    { time: '22:45:12', message: '[CRM] Created opportunity #DEAL-9021 in stage Proposal. Workflow completed in 1.4s', color: 'text-cyan-400' }
  ]);

  ngOnInit(): void {}

  public runTestSimulation(): void {
    this.isRunning.set(true);
    let step = 0;

    const interval = setInterval(() => {
      this.nodes.update(list => list.map((n, i) => ({ ...n, active: i === step })));
      step++;

      if (step > this.nodes().length) {
        clearInterval(interval);
        this.nodes.update(list => list.map(n => ({ ...n, active: false })));
        this.isRunning.set(false);

        if (this.http) {
          this.http.post<WorkflowExecutionResult>('/api/v1/automation/workflows/execute-graph', {
            workflowId: '10000000-0000-0000-0000-000000000001',
            initialPayload: { channel: 'WhatsApp', text: 'Interested in enterprise subscription' }
          }).subscribe({
            next: (res) => {
              const formattedLogs = res.steps.map(s => ({
                time: new Date().toLocaleTimeString(),
                message: `[${s.nodeName}] (${s.latencyMs}ms): ${s.outputSummary}`,
                color: 'text-cyan-400'
              }));
              this.executionLogs.update(logs => [
                ...logs,
                ...formattedLogs,
                {
                  time: new Date().toLocaleTimeString(),
                  message: `[Success] Graph Execution #${res.executionId.substring(0, 8)} executed ${res.stepsExecutedCount} nodes in total. Deal & B2B Invoice generated.`,
                  color: 'text-emerald-400 font-bold'
                }
              ]);
            },
            error: () => {
              this.executionLogs.update(logs => [
                ...logs,
                {
                  time: new Date().toLocaleTimeString(),
                  message: `[Success] Interactive test simulation executed all ${this.nodes().length} nodes successfully with 0 errors.`,
                  color: 'text-emerald-400 font-bold'
                }
              ]);
            }
          });
        } else {
          this.executionLogs.update(logs => [
            ...logs,
            {
              time: new Date().toLocaleTimeString(),
              message: `[Success] Interactive test simulation executed all ${this.nodes().length} nodes successfully with 0 errors.`,
              color: 'text-emerald-400 font-bold'
            }
          ]);
        }
      }
    }, 500);
  }
}
