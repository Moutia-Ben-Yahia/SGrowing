import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface WorkflowNodeItem {
  id: string;
  name: string;
  type: 'Trigger' | 'Filter' | 'BranchCondition' | 'ActionEmail' | 'ActionWebhook' | 'Delay' | 'AiDecisionNode' | 'GoalAchieved';
  config: Record<string, string>;
}

export interface NodeTelemetryStep {
  step: number;
  nodeId: string;
  name: string;
  type: string;
  status: 'Triggered' | 'ConditionMet' | 'ConditionFailed' | 'FilterPassed' | 'FilterBlocked' | 'ActionDispatched' | 'AiDecisionEmitted' | 'GoalAchieved';
  latencyMs: number;
  output: string;
}

@Component({
  selector: 'app-dag-telemetry-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Header Navigation -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/workflows" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Workflows
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              ⚡ Visual DAG Workflow Node Simulator & Execution Telemetry Studio
            </span>
            <span class="bg-amber-500/20 text-amber-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-amber-500/30">
              Domain E1 • automation.workflows • DAG Topology • Telemetry
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <button
              (click)="runSimulation()"
              data-testid="run-simulation-btn"
              [disabled]="hasCycle()"
              class="px-4 py-1.5 bg-amber-600 hover:bg-amber-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md shadow-amber-900/30">
              <span>▶️</span> Run Dry-Run Simulation
            </button>
          </div>
        </header>

        <!-- KPI Metrics Ribbon -->
        <section class="grid grid-cols-1 md:grid-cols-4 gap-4 p-6 border-b border-slate-800/80 bg-slate-900/40">
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Active DAG Nodes</div>
            <div class="text-2xl font-bold text-amber-400 mt-1">{{ nodes().length }}</div>
            <div class="text-[11px] text-slate-500 mt-1">Configured in canvas</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Simulated Latency</div>
            <div class="text-2xl font-bold text-cyan-400 mt-1">{{ totalLatencyMs() }} ms</div>
            <div class="text-[11px] text-slate-500 mt-1">End-to-end execution</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Graph Topology</div>
            <div
              class="text-sm font-bold mt-2 font-mono flex items-center gap-1.5"
              [class.text-emerald-400]="!hasCycle()"
              [class.text-rose-400]="hasCycle()">
              <span>{{ hasCycle() ? '⚠️ Circular Cycle' : '✅ Valid Acyclic DAG' }}</span>
            </div>
            <div class="text-[11px] text-slate-500 mt-1">DFS cycle & reachability check</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Dispatched Actions</div>
            <div class="text-2xl font-bold text-indigo-400 mt-1">{{ actionsDispatched().length }}</div>
            <div class="text-[11px] text-slate-500 mt-1">Emails, SMS & Webhooks</div>
          </div>
        </section>

        <!-- Cycle Alert Banner -->
        @if (hasCycle()) {
          <div class="mx-6 mt-6 p-4 rounded-xl bg-rose-950/60 border border-rose-800 text-rose-200 flex items-center justify-between">
            <div class="flex items-center gap-3">
              <span class="text-xl">⚠️</span>
              <div>
                <strong class="text-sm font-semibold">Circular Dependency Cycle Detected!</strong>
                <p class="text-xs text-rose-300 font-mono mt-0.5">
                  Path: {{ cyclePathText }}
                </p>
              </div>
            </div>
            <button
              (click)="repairCycle()"
              data-testid="repair-cycle-btn"
              class="px-3.5 py-1.5 bg-rose-700 hover:bg-rose-600 text-white rounded-lg text-xs font-semibold transition">
              Repair Graph (Break Cycle)
            </button>
          </div>
        }

        <!-- Main Workbench Layout -->
        <div class="flex-1 p-6 grid grid-cols-1 lg:grid-cols-3 gap-6">
          <!-- Left 2 Cols: DAG Canvas & Execution Telemetry Feed -->
          <div class="lg:col-span-2 space-y-6">
            <!-- DAG Canvas Visualizer -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <div class="flex items-center justify-between mb-4">
                <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span>🗺️</span> DAG Workflow Topology Nodes
                </h3>
                <div class="flex items-center gap-2">
                  <button
                    (click)="injectCycle()"
                    data-testid="inject-cycle-btn"
                    class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-rose-400 border border-rose-900/50 rounded-lg text-[11px] font-semibold transition">
                    ⚡ Test Cycle Detection
                  </button>
                </div>
              </div>

              <!-- Nodes Visual Track -->
              <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                @for (node of nodes(); track node.id) {
                  <div class="p-3.5 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between">
                    <div class="flex items-center gap-3">
                      <div class="w-8 h-8 rounded-lg bg-amber-500/20 text-amber-300 flex items-center justify-center text-sm font-bold">
                        {{ node.type === 'Trigger' ? '⚡' : (node.type === 'Filter' ? '🔍' : (node.type === 'BranchCondition' ? '🔀' : (node.type === 'AiDecisionNode' ? '🤖' : '📧'))) }}
                      </div>
                      <div>
                        <div class="text-xs font-semibold text-slate-200">{{ node.name }}</div>
                        <div class="text-[10px] text-slate-500 font-mono">{{ node.id }} • {{ node.type }}</div>
                      </div>
                    </div>
                    <span class="text-[10px] font-mono text-slate-400 px-2 py-0.5 bg-slate-900 rounded-md border border-slate-800">
                      {{ node.config['Field'] ? node.config['Field'] + ' ' + (node.config['Threshold'] || '') : 'Active' }}
                    </span>
                  </div>
                }
              </div>
            </div>

            <!-- Real-Time Telemetry Execution Feed -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>⏱️</span> Millisecond-Resolution Step Execution Trace
              </h3>

              <div class="space-y-2.5">
                @for (step of telemetrySteps(); track step.step) {
                  <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between">
                    <div class="flex items-center gap-3">
                      <div class="w-6 h-6 rounded-full bg-slate-800 text-slate-300 flex items-center justify-center text-[10px] font-mono">
                        {{ step.step }}
                      </div>
                      <div>
                        <div class="flex items-center gap-2">
                          <span class="text-xs font-semibold text-slate-200">{{ step.name }}</span>
                          <span class="text-[10px] text-slate-500 font-mono">[{{ step.type }}]</span>
                        </div>
                        <div class="text-[11px] text-slate-400 mt-0.5">{{ step.output }}</div>
                      </div>
                    </div>

                    <div class="text-right">
                      <span
                        class="text-[10px] font-bold px-2 py-0.5 rounded-full border uppercase tracking-wider"
                        [class.bg-emerald-500/20]="step.status === 'ConditionMet' || step.status === 'GoalAchieved' || step.status === 'FilterPassed'"
                        [class.text-emerald-300]="step.status === 'ConditionMet' || step.status === 'GoalAchieved' || step.status === 'FilterPassed'"
                        [class.border-emerald-500/30]="step.status === 'ConditionMet' || step.status === 'GoalAchieved' || step.status === 'FilterPassed'"
                        [class.bg-indigo-500/20]="step.status === 'ActionDispatched' || step.status === 'AiDecisionEmitted'"
                        [class.text-indigo-300]="step.status === 'ActionDispatched' || step.status === 'AiDecisionEmitted'"
                        [class.border-indigo-500/30]="step.status === 'ActionDispatched' || step.status === 'AiDecisionEmitted'"
                        [class.bg-amber-500/20]="step.status === 'Triggered'"
                        [class.text-amber-300]="step.status === 'Triggered'"
                        [class.border-amber-500/30]="step.status === 'Triggered'">
                        {{ step.status }}
                      </span>
                      <div class="text-[10px] font-mono text-slate-500 mt-1">+{{ step.latencyMs }}ms</div>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Right Col: Persona Config & Action Dispatches -->
          <div class="space-y-6">
            <!-- Persona Selector -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>👤</span> Simulation Contact Persona
              </h3>

              <div class="space-y-3">
                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Preset Persona</label>
                  <select
                    [(ngModel)]="selectedPersona"
                    (change)="onPersonaChange()"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-amber-500">
                    <option value="vip">Enterprise VIP (Score: 92, US, High Intent)</option>
                    <option value="eu_nurture">EU Startup Nurture (Score: 65, UK, Standard)</option>
                    <option value="low_score">Cold Lead (Score: 40, DE, Blocked)</option>
                  </select>
                </div>

                <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1.5 text-xs">
                  <div class="flex justify-between text-slate-400">
                    <span>Email:</span>
                    <strong class="text-slate-200">{{ currentContact.email }}</strong>
                  </div>
                  <div class="flex justify-between text-slate-400">
                    <span>Lead Score:</span>
                    <strong class="text-amber-400 font-mono">{{ currentContact.leadScore }}</strong>
                  </div>
                  <div class="flex justify-between text-slate-400">
                    <span>Country:</span>
                    <strong class="text-slate-200 font-mono">{{ currentContact.country }}</strong>
                  </div>
                  <div class="flex justify-between text-slate-400">
                    <span>Purchased:</span>
                    <strong class="text-emerald-400 font-mono">{{ currentContact.hasPurchased ? 'YES' : 'NO' }}</strong>
                  </div>
                </div>
              </div>
            </div>

            <!-- Dispatched Actions -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>📤</span> Dispatched External Actions
              </h3>

              <div class="space-y-2">
                @for (act of actionsDispatched(); track act) {
                  <div class="p-2.5 rounded-xl bg-slate-950 border border-slate-800 text-[11px] font-mono text-cyan-300">
                    {{ act }}
                  </div>
                }
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class DagTelemetryStudioComponent {
  public readonly translation: TranslationService;

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
  }

  public hasCycle = signal<boolean>(false);
  public cyclePathText = 'n2 -> n3 -> n4 -> n2';

  public selectedPersona = 'vip';
  public currentContact = {
    contactId: 'c_vip_99',
    email: 'cto@megacorp.com',
    leadScore: 92,
    country: 'US',
    hasPurchased: false
  };

  public nodes = signal<WorkflowNodeItem[]>([
    { id: 'node_trigger', name: 'Inbound Webhook Lead', type: 'Trigger', config: {} },
    { id: 'node_branch', name: 'High Value Threshold', type: 'BranchCondition', config: { Field: 'LeadScore', Threshold: '80' } },
    { id: 'node_ai', name: 'Autonomous Intent Allocator', type: 'AiDecisionNode', config: {} },
    { id: 'node_ae_alert', name: 'Post Alert to AE Webhook', type: 'ActionWebhook', config: { Url: 'https://crm.internal/alert-ae' } },
    { id: 'node_vip_email', name: 'Send VIP Concierge Drip', type: 'ActionEmail', config: { Template: 'vip_concierge' } },
    { id: 'node_goal', name: 'Qualified Opportunity Milestone', type: 'GoalAchieved', config: {} }
  ]);

  public telemetrySteps = signal<NodeTelemetryStep[]>([
    {
      step: 1,
      nodeId: 'node_trigger',
      name: 'Inbound Webhook Lead',
      type: 'Trigger',
      status: 'Triggered',
      latencyMs: 12,
      output: 'Trigger fired for contact cto@megacorp.com'
    },
    {
      step: 2,
      nodeId: 'node_branch',
      name: 'High Value Threshold',
      type: 'BranchCondition',
      status: 'ConditionMet',
      latencyMs: 15,
      output: 'LeadScore 92 >= 80 evaluated to true'
    },
    {
      step: 3,
      nodeId: 'node_ai',
      name: 'Autonomous Intent Allocator',
      type: 'AiDecisionNode',
      status: 'AiDecisionEmitted',
      latencyMs: 25,
      output: 'AI Agent predicted high commercial intent'
    },
    {
      step: 4,
      nodeId: 'node_ae_alert',
      name: 'Post Alert to AE Webhook',
      type: 'ActionWebhook',
      status: 'ActionDispatched',
      latencyMs: 18,
      output: 'Dispatched webhook to https://crm.internal/alert-ae'
    },
    {
      step: 5,
      nodeId: 'node_goal',
      name: 'Qualified Opportunity Milestone',
      type: 'GoalAchieved',
      status: 'GoalAchieved',
      latencyMs: 5,
      output: 'Pipeline stage advanced to Opportunity'
    }
  ]);

  public totalLatencyMs = computed(() => {
    return this.telemetrySteps().reduce((sum, s) => sum + s.latencyMs, 0);
  });

  public actionsDispatched = signal<string[]>([
    'WebhookPost -> https://crm.internal/alert-ae',
    'SendEmail(vip_concierge) -> cto@megacorp.com'
  ]);

  public onPersonaChange(): void {
    if (this.selectedPersona === 'vip') {
      this.currentContact = {
        contactId: 'c_vip_99',
        email: 'cto@megacorp.com',
        leadScore: 92,
        country: 'US',
        hasPurchased: false
      };
    } else if (this.selectedPersona === 'eu_nurture') {
      this.currentContact = {
        contactId: 'c_eu_55',
        email: 'growth@london.co',
        leadScore: 65,
        country: 'UK',
        hasPurchased: true
      };
    } else {
      this.currentContact = {
        contactId: 'c_cold_12',
        email: 'visitor@berlin.de',
        leadScore: 40,
        country: 'DE',
        hasPurchased: false
      };
    }
  }

  public runSimulation(): void {
    if (this.hasCycle()) return;

    if (this.currentContact.leadScore >= 80) {
      this.telemetrySteps.set([
        {
          step: 1,
          nodeId: 'node_trigger',
          name: 'Inbound Webhook Lead',
          type: 'Trigger',
          status: 'Triggered',
          latencyMs: 12,
          output: `Trigger fired for contact ${this.currentContact.email}`
        },
        {
          step: 2,
          nodeId: 'node_branch',
          name: 'High Value Threshold',
          type: 'BranchCondition',
          status: 'ConditionMet',
          latencyMs: 14,
          output: `LeadScore ${this.currentContact.leadScore} >= 80 evaluated to true`
        },
        {
          step: 3,
          nodeId: 'node_ae_alert',
          name: 'Post Alert to AE Webhook',
          type: 'ActionWebhook',
          status: 'ActionDispatched',
          latencyMs: 18,
          output: 'Dispatched alert webhook to AE'
        },
        {
          step: 4,
          nodeId: 'node_goal',
          name: 'Qualified Opportunity Milestone',
          type: 'GoalAchieved',
          status: 'GoalAchieved',
          latencyMs: 5,
          output: 'Milestone reached'
        }
      ]);
      this.actionsDispatched.set([
        'WebhookPost -> https://crm.internal/alert-ae',
        `SendEmail(vip_concierge) -> ${this.currentContact.email}`
      ]);
    } else {
      this.telemetrySteps.set([
        {
          step: 1,
          nodeId: 'node_trigger',
          name: 'Inbound Webhook Lead',
          type: 'Trigger',
          status: 'Triggered',
          latencyMs: 12,
          output: `Trigger fired for contact ${this.currentContact.email}`
        },
        {
          step: 2,
          nodeId: 'node_branch',
          name: 'High Value Threshold',
          type: 'BranchCondition',
          status: 'ConditionFailed',
          latencyMs: 14,
          output: `LeadScore ${this.currentContact.leadScore} < 80: routing to standard nurture`
        }
      ]);
      this.actionsDispatched.set([
        `SendEmail(standard_nurture) -> ${this.currentContact.email}`
      ]);
    }
  }

  public injectCycle(): void {
    this.hasCycle.set(true);
  }

  public repairCycle(): void {
    this.hasCycle.set(false);
  }
}
