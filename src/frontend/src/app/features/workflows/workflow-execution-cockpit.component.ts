import { Component, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export interface StepTraceItem {
  id: string;
  name: string;
  kind: string;
  status: 'Completed' | 'Failed' | 'Running' | 'Pending';
  durationMs: number;
  input: string;
  output: string;
  error?: string;
}

export interface ExecutionItem {
  id: string;
  workflowName: string;
  triggerEvent: string;
  status: 'Completed' | 'Failed' | 'Running';
  time: string;
  steps: StepTraceItem[];
}

@Component({
  selector: 'app-workflow-execution-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="p-6 max-w-7xl mx-auto space-y-6 text-slate-100 font-sans">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-2">
            <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
              Horizon 18 • Domain E1, E2 & E3
            </span>
            <span class="text-xs text-slate-400">Stateful DAG Workflow Execution Mesh</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">
            Stateful Event Mesh & DAG Workflow Execution Cockpit
          </h1>
          <p class="text-sm text-slate-400">
            Persistent execution checkpoints, resilient retry backoff policies, and step-by-step payload inspector.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button (click)="triggerDemoExecution()"
                  class="px-3.5 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold shadow-lg shadow-indigo-600/30 transition">
            Trigger Webhook Workflow ⚡
          </button>
        </div>
      </div>

      <!-- Top Metric Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Total Executions</div>
          <div class="text-2xl font-bold text-white mt-1">{{ executions().length }}</div>
          <div class="text-xs text-slate-500 mt-0.5">Recorded in Event Mesh</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Execution Success Rate</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ successRate() }}%</div>
          <div class="text-xs text-slate-500 mt-0.5">Automated DAG Traversal</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Active Execution</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">{{ activeExecution()?.id }}</div>
          <div class="text-xs text-slate-500 mt-0.5">{{ activeExecution()?.workflowName }}</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Checkpointed Steps</div>
          <div class="text-2xl font-bold text-white mt-1">{{ activeExecution()?.steps?.length || 0 }}</div>
          <div class="text-xs text-slate-500 mt-0.5">Zero State Loss on Failures</div>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Executions History (1 Col) -->
        <div class="space-y-4">
          <h2 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">Execution Runs</h2>

          <div class="space-y-2.5">
            @for (exec of executions(); track exec.id) {
              <div (click)="selectExecution(exec.id)"
                   class="p-3.5 rounded-2xl border cursor-pointer transition space-y-1"
                   [ngClass]="exec.id === activeExecutionId() ? 'bg-indigo-950/40 border-indigo-500/60 shadow-lg' : 'bg-slate-900/80 border-slate-800 hover:border-slate-700'">
                <div class="flex items-center justify-between text-xs">
                  <span class="font-mono text-slate-400">{{ exec.id }}</span>
                  <span class="px-2 py-0.5 rounded-full text-[10px] font-bold"
                        [ngClass]="exec.status === 'Completed' ? 'bg-emerald-500/20 text-emerald-300' : 'bg-rose-500/20 text-rose-300'">
                    {{ exec.status }}
                  </span>
                </div>
                <div class="text-sm font-bold text-white">{{ exec.workflowName }}</div>
                <div class="flex justify-between text-[11px] text-slate-500">
                  <span>{{ exec.triggerEvent }}</span>
                  <span>{{ exec.time }}</span>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- DAG Steps Trace (2 Cols) -->
        <div class="lg:col-span-2 space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">Step-by-Step DAG Execution Trace</h2>
            <span class="text-xs text-slate-500">Click step to view payload</span>
          </div>

          <div class="space-y-3">
            @for (step of activeExecution()?.steps; track step.id; let idx = $index) {
              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 space-y-2">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-3">
                    <div class="w-7 h-7 rounded-lg flex items-center justify-center font-bold text-xs"
                         [ngClass]="step.status === 'Completed' ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' : 'bg-rose-500/20 text-rose-400 border border-rose-500/30'">
                      {{ idx + 1 }}
                    </div>
                    <div>
                      <span class="font-bold text-sm text-white">{{ step.name }}</span>
                      <span class="text-xs text-slate-500 ml-2 font-mono">({{ step.kind }})</span>
                    </div>
                  </div>

                  <div class="flex items-center gap-3">
                    <span class="text-xs text-slate-400 font-mono">{{ step.durationMs }}ms</span>
                    <span class="text-xs px-2 py-0.5 rounded font-bold"
                          [ngClass]="step.status === 'Completed' ? 'bg-emerald-500/20 text-emerald-400' : 'bg-rose-500/20 text-rose-400'">
                      {{ step.status }}
                    </span>
                    @if (step.status === 'Failed') {
                      <button (click)="replayStep(step.id)"
                              class="text-xs px-2.5 py-1 rounded bg-indigo-600 hover:bg-indigo-500 text-white font-semibold">
                        Replay Step 🔄
                      </button>
                    }
                  </div>
                </div>

                @if (step.error) {
                  <div class="text-xs font-mono text-rose-400 bg-rose-950/40 border border-rose-900/50 p-2.5 rounded-xl">
                    Error: {{ step.error }}
                  </div>
                }

                <div class="grid grid-cols-2 gap-2 text-xs font-mono">
                  <div class="p-2 bg-slate-950 rounded-xl border border-slate-900 text-slate-400 truncate">
                    <span class="text-slate-500">In:</span> {{ step.input }}
                  </div>
                  <div class="p-2 bg-slate-950 rounded-xl border border-slate-900 text-emerald-400 truncate">
                    <span class="text-slate-500">Out:</span> {{ step.output }}
                  </div>
                </div>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class WorkflowExecutionCockpitComponent {
  translation: TranslationService;
  private cdr?: ChangeDetectorRef;

  activeExecutionId = signal<string>('exec_9901');

  executions = signal<ExecutionItem[]>([
    {
      id: 'exec_9901',
      workflowName: 'Inbound Lead Qualification & CAPI Dispatch',
      triggerEvent: 'Webhook: Typeform Submit',
      status: 'Completed',
      time: '2 mins ago',
      steps: [
        { id: 'st_1', name: 'Webhook Ingestion', kind: 'WebhookTrigger', status: 'Completed', durationMs: 14, input: '{"email":"lead@corp.io"}', output: '{"status":200}' },
        { id: 'st_2', name: 'Lead Score Filter (>= 60)', kind: 'FilterCondition', status: 'Completed', durationMs: 8, input: '{"score":85}', output: '{"passed":true}' },
        { id: 'st_3', name: 'CAPI Conversion Sync', kind: 'HttpDispatch', status: 'Completed', durationMs: 110, input: '{"action":"Lead"}', output: '{"events_received":1}' },
        { id: 'st_4', name: 'SDR Priority Notification', kind: 'SendEmail', status: 'Completed', durationMs: 45, input: '{"rep":"Alex"}', output: '{"sent":true}' }
      ]
    },
    {
      id: 'exec_9902',
      workflowName: 'Enterprise Quote Signed Handler',
      triggerEvent: 'Event: Contract.Signed',
      status: 'Failed',
      time: '15 mins ago',
      steps: [
        { id: 'st_1', name: 'Signature Event Receive', kind: 'WebhookTrigger', status: 'Completed', durationMs: 12, input: '{"contractId":"c_88"}', output: '{"verified":true}' },
        { id: 'st_2', name: 'ERP Order Sync', kind: 'HttpDispatch', status: 'Failed', durationMs: 250, input: '{"total":12000}', output: '{}', error: 'Connection timeout to remote ERP billing gateway.' }
      ]
    }
  ]);

  activeExecution = computed(() => {
    return this.executions().find(e => e.id === this.activeExecutionId());
  });

  successRate = computed(() => {
    const list = this.executions();
    if (list.length === 0) return 100;
    const completed = list.filter(e => e.status === 'Completed').length;
    return Math.round((completed / list.length) * 100);
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
    try {
      this.cdr = inject(ChangeDetectorRef);
    } catch {
      // Direct unit test instantiation
    }
  }

  selectExecution(id: string): void {
    this.activeExecutionId.set(id);
    this.cdr?.markForCheck();
  }

  triggerDemoExecution(): void {
    const newExec: ExecutionItem = {
      id: 'exec_' + Math.random().toString(36).substring(2, 6),
      workflowName: 'Real-Time Abandoned Cart Recovery',
      triggerEvent: 'Webhook: Cart.Abandoned',
      status: 'Completed',
      time: 'Just now',
      steps: [
        { id: 's1', name: 'Trigger Ingest', kind: 'WebhookTrigger', status: 'Completed', durationMs: 15, input: '{"cartId":"c99"}', output: '{"ok":true}' },
        { id: 's2', name: 'Cart Value Check (> $100)', kind: 'FilterCondition', status: 'Completed', durationMs: 5, input: '{"value":250}', output: '{"passed":true}' },
        { id: 's3', name: 'Dispatch WhatsApp Offer', kind: 'HttpDispatch', status: 'Completed', durationMs: 95, input: '{"template":"discount_10"}', output: '{"dispatched":true}' }
      ]
    };

    this.executions.update(list => [newExec, ...list]);
    this.activeExecutionId.set(newExec.id);
    this.cdr?.markForCheck();
  }

  replayStep(stepId: string): void {
    const currentExec = this.activeExecution();
    if (!currentExec) return;

    const updatedSteps = currentExec.steps.map(s => {
      if (s.id === stepId) {
        return {
          ...s,
          status: 'Completed' as const,
          error: undefined,
          output: '{"recovered":true, "status":200}'
        };
      }
      return s;
    });

    const updatedExecutions = this.executions().map(e => {
      if (e.id === currentExec.id) {
        return {
          ...e,
          status: 'Completed' as const,
          steps: updatedSteps
        };
      }
      return e;
    });

    this.executions.set(updatedExecutions);
    this.cdr?.markForCheck();
  }
}
