import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface AgentMissionStepUi {
  id: string;
  stepIndex: number;
  toolName: string;
  inputArgumentsJson: string;
  outputResultJson: string;
  reasoningThought: string;
  status: 'Pending' | 'AwaitingApproval' | 'Executing' | 'Completed' | 'Failed' | 'Skipped';
  executionDurationMs: number;
  tokensConsumed: number;
  costUsd: number;
  approvalRequired: boolean;
  approvedBy?: string;
  approvedAtUtc?: string;
}

export interface AgentMissionUi {
  id: string;
  tenantId: string;
  title: string;
  targetGoal: string;
  status: 'Pending' | 'Running' | 'PausedForApproval' | 'Completed' | 'Failed';
  autonomyMode: 'Auto-Approve' | 'Human-in-the-Loop' | 'Draft Only';
  totalBudgetUsd: number;
  spentBudgetUsd: number;
  tokensConsumed: number;
  currentStepIndex: number;
  reasoningTrace: string;
  createdAtUtc: string;
  completedAtUtc?: string;
  steps: AgentMissionStepUi[];
}

@Component({
  selector: 'app-agent-mission-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Main Content Area -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/70 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/agents/console" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Agent Hub
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              🤖 Autonomous AI SDR & RevOps Mission Cockpit
            </span>
            <span class="bg-purple-500/20 text-purple-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-purple-500/30">
              MCP Multi-Tool Loop • Self-Driving RevOps
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="showCreateModal.set(true)"
              class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md">
              <span>+</span> New Mission
            </button>
            <a routerLink="/onboarding" class="text-xs text-slate-400 hover:text-slate-200 px-3 py-1.5 rounded-lg bg-slate-800/80 border border-slate-700">
              🚀 Onboarding Wizard
            </a>
          </div>
        </header>

        <div class="p-6 max-w-7xl mx-auto w-full space-y-6 flex-1">
          <!-- 1. Mission Header & Budget Guardrail Cards -->
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- Active Mission Info -->
            <div class="lg:col-span-2 bg-slate-900/70 border border-slate-800 rounded-2xl p-5 space-y-4 shadow-xl">
              <div class="flex items-start justify-between gap-4">
                <div>
                  <div class="flex items-center gap-2">
                    <span class="h-2.5 w-2.5 rounded-full"
                      [ngClass]="{
                        'bg-emerald-400 animate-pulse': currentMission()?.status === 'Running',
                        'bg-amber-400': currentMission()?.status === 'PausedForApproval',
                        'bg-blue-400': currentMission()?.status === 'Completed',
                        'bg-slate-400': currentMission()?.status === 'Pending'
                      }"></span>
                    <h2 class="text-base font-bold text-white">{{ currentMission()?.title || 'No Active Mission' }}</h2>
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold uppercase border"
                      [ngClass]="{
                        'bg-emerald-950 text-emerald-400 border-emerald-500/30': currentMission()?.status === 'Running',
                        'bg-amber-950 text-amber-300 border-amber-500/30': currentMission()?.status === 'PausedForApproval',
                        'bg-blue-950 text-blue-300 border-blue-500/30': currentMission()?.status === 'Completed',
                        'bg-slate-800 text-slate-300 border-slate-700': currentMission()?.status === 'Pending'
                      }">
                      {{ currentMission()?.status }}
                    </span>
                  </div>
                  <p class="text-xs text-slate-400 mt-1.5 leading-relaxed">{{ currentMission()?.targetGoal }}</p>
                </div>

                <!-- Autonomy Mode Pill -->
                <div class="text-right shrink-0">
                  <span class="text-[10px] font-mono uppercase text-slate-400 block mb-0.5">Autonomy Mode</span>
                  <span class="px-2.5 py-1 rounded-md text-xs font-semibold bg-purple-950/60 text-purple-300 border border-purple-700/50">
                    {{ currentMission()?.autonomyMode }}
                  </span>
                </div>
              </div>

              <!-- Action Controls Bar -->
              <div class="flex flex-wrap items-center justify-between gap-3 pt-3 border-t border-slate-800/80">
                <div class="flex items-center gap-2">
                  @if (currentMission()?.status === 'Running') {
                    <button
                      (click)="pauseMission()"
                      class="px-3 py-1.5 bg-amber-600/20 hover:bg-amber-600/30 text-amber-300 border border-amber-500/40 rounded-lg text-xs font-semibold transition flex items-center gap-1.5">
                      <span>⏸</span> Pause Mission
                    </button>
                  } @else if (currentMission()?.status === 'PausedForApproval' || currentMission()?.status === 'Pending') {
                    <button
                      (click)="resumeMission()"
                      class="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md shadow-emerald-600/20">
                      <span>▶</span> Resume Loop
                    </button>
                  }

                  <button
                    (click)="executeNextStep()"
                    [disabled]="currentMission()?.status === 'Completed'"
                    class="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md shadow-indigo-600/20">
                    <span>⚡</span> Execute Step
                  </button>
                </div>

                <div class="text-[11px] font-mono text-slate-400">
                  Step {{ (currentMission()?.currentStepIndex || 0) + 1 }} of {{ currentMission()?.steps?.length || 5 }}
                </div>
              </div>
            </div>

            <!-- Budget Guardrail Gauge Card -->
            <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-5 space-y-4 shadow-xl flex flex-col justify-between">
              <div>
                <div class="flex items-center justify-between mb-1">
                  <h3 class="text-xs font-bold uppercase tracking-wider text-slate-300 font-mono flex items-center gap-1.5">
                    <span>🛡️</span> Budget Guardrail
                  </h3>
                  <span class="text-[10px] font-mono text-emerald-400 font-semibold bg-emerald-950/60 border border-emerald-500/30 px-1.5 py-0.5 rounded">
                    Hard-Cap Enforced
                  </span>
                </div>
                <p class="text-[11px] text-slate-400">Autonomous spend cap protecting against AI over-dispatch.</p>
              </div>

              <!-- Spend Progress Bar -->
              <div class="space-y-2 py-2">
                <div class="flex items-baseline justify-between text-xs font-mono">
                  <span class="text-slate-400">Spent: <strong class="text-white">\${{ currentMission()?.spentBudgetUsd?.toFixed(2) || '0.00' }}</strong></span>
                  <span class="text-slate-400">Cap: <strong class="text-indigo-400">\${{ currentMission()?.totalBudgetUsd?.toFixed(2) || '50.00' }}</strong></span>
                </div>
                <div class="w-full h-2.5 bg-slate-950 rounded-full overflow-hidden border border-slate-800">
                  <div
                    class="h-full rounded-full transition-all duration-500"
                    [ngClass]="budgetUsagePercent() > 80 ? 'bg-amber-500' : 'bg-indigo-500'"
                    [style.width.%]="budgetUsagePercent()"></div>
                </div>
                <div class="flex items-center justify-between text-[11px] text-slate-400">
                  <span>Tokens: {{ (currentMission()?.tokensConsumed || 0).toLocaleString() }}</span>
                  <span class="font-mono text-indigo-300">{{ budgetUsagePercent().toFixed(1) }}% Used</span>
                </div>
              </div>

              <div class="pt-2 border-t border-slate-800/80 flex items-center justify-between text-[11px] text-slate-400">
                <span>Autonomous Safety Gate:</span>
                <span class="text-emerald-400 font-medium">Auto-Stop at $50.00</span>
              </div>
            </div>
          </div>

          <!-- 2. Multi-Step MCP Tool Execution Loop Timeline -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-6">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-sm font-bold text-white uppercase tracking-wider font-mono flex items-center gap-2">
                  <span>🔄</span> MCP Tool Execution Loop Timeline
                </h3>
                <p class="text-xs text-slate-400 mt-0.5">
                  5-step autonomous cycle executing CRM lead filtering, deliverability tests, spintax campaigns, contracts, and logistics.
                </p>
              </div>
              <span class="text-xs font-mono text-slate-400">Autonomous Engine v2.4</span>
            </div>

            <!-- Steps Timeline Grid -->
            <div class="space-y-4">
              @for (step of currentMission()?.steps; track step.id; let idx = $index) {
                <div
                  class="p-4 rounded-xl border transition-all duration-300 relative"
                  [ngClass]="{
                    'bg-slate-950/60 border-slate-800/80': step.status === 'Pending',
                    'bg-indigo-950/20 border-indigo-500/50 ring-1 ring-indigo-500/30': step.status === 'Executing',
                    'bg-amber-950/30 border-amber-500/50 ring-1 ring-amber-500/40': step.status === 'AwaitingApproval',
                    'bg-emerald-950/20 border-emerald-500/30': step.status === 'Completed',
                    'bg-rose-950/20 border-rose-500/40': step.status === 'Failed'
                  }">

                  <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
                    <!-- Step Left: Icon, Number, Tool, and Description -->
                    <div class="flex items-start gap-3.5">
                      <div class="h-9 w-9 rounded-xl flex items-center justify-center font-bold text-sm shrink-0 border"
                        [ngClass]="{
                          'bg-slate-800 border-slate-700 text-slate-400': step.status === 'Pending',
                          'bg-indigo-600/30 border-indigo-500 text-indigo-300 animate-pulse': step.status === 'Executing',
                          'bg-amber-600/30 border-amber-500 text-amber-300': step.status === 'AwaitingApproval',
                          'bg-emerald-600/30 border-emerald-500 text-emerald-300': step.status === 'Completed',
                          'bg-rose-600/30 border-rose-500 text-rose-300': step.status === 'Failed'
                        }">
                        @if (step.status === 'Completed') { ✓ }
                        @else if (step.status === 'AwaitingApproval') { ⏸ }
                        @else if (step.status === 'Executing') { ⚙️ }
                        @else { {{ idx + 1 }} }
                      </div>

                      <div class="space-y-1">
                        <div class="flex items-center gap-2">
                          <span class="text-xs font-mono font-bold text-white bg-slate-900 px-2 py-0.5 rounded border border-slate-700">
                            {{ step.toolName }}
                          </span>
                          <span class="text-xs font-semibold text-slate-200">
                            {{ getToolDisplayName(step.toolName) }}
                          </span>
                          @if (step.approvalRequired) {
                            <span class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-amber-500/20 text-amber-400 border border-amber-500/30">
                              Gate Required
                            </span>
                          }
                        </div>
                        <p class="text-xs text-slate-300 italic font-sans">
                          "{{ step.reasoningThought }}"
                        </p>
                      </div>
                    </div>

                    <!-- Step Right: Status Badge & Human Approval Button -->
                    <div class="flex items-center gap-3 shrink-0 self-end md:self-center">
                      <!-- Cost & Time Info -->
                      @if (step.status === 'Completed') {
                        <div class="text-right text-[11px] font-mono text-slate-400 hidden sm:block">
                          <div>{{ step.executionDurationMs }}ms • {{ step.tokensConsumed }} tok</div>
                          <div class="text-emerald-400 font-semibold">\${{ step.costUsd.toFixed(3) }}</div>
                        </div>
                      }

                      <!-- Status Badge -->
                      <span class="px-2.5 py-1 rounded text-xs font-mono font-bold uppercase border"
                        [ngClass]="{
                          'bg-slate-800 text-slate-400 border-slate-700': step.status === 'Pending',
                          'bg-indigo-950 text-indigo-400 border-indigo-500/50 animate-pulse': step.status === 'Executing',
                          'bg-amber-950 text-amber-300 border-amber-500/50': step.status === 'AwaitingApproval',
                          'bg-emerald-950 text-emerald-400 border-emerald-500/30': step.status === 'Completed',
                          'bg-rose-950 text-rose-400 border-rose-500/50': step.status === 'Failed'
                        }">
                        {{ step.status }}
                      </span>

                      <!-- Human Approval Gate Button -->
                      @if (step.status === 'AwaitingApproval') {
                        <button
                          (click)="approveStep(step.id)"
                          class="px-4 py-1.5 bg-amber-500 hover:bg-amber-400 text-slate-950 font-bold rounded-lg text-xs transition shadow-lg shadow-amber-500/20 flex items-center gap-1.5">
                          <span>🛡️</span> Approve Step
                        </button>
                      }
                    </div>
                  </div>

                  <!-- Details Expandable: Input & Output -->
                  @if (step.status === 'Completed' || step.status === 'AwaitingApproval') {
                    <div class="mt-3 pt-3 border-t border-slate-800/80 text-[11px] font-mono grid grid-cols-1 md:grid-cols-2 gap-3">
                      <div class="p-2 rounded bg-slate-950/80 border border-slate-800/60">
                        <span class="text-slate-400 block font-sans text-[10px] mb-1">Input Payload:</span>
                        <pre class="text-slate-300 overflow-x-auto whitespace-pre-wrap max-h-24">{{ step.inputArgumentsJson }}</pre>
                      </div>
                      <div class="p-2 rounded bg-slate-950/80 border border-slate-800/60">
                        <span class="text-slate-400 block font-sans text-[10px] mb-1">Execution Output / Response:</span>
                        <pre class="text-emerald-300 overflow-x-auto whitespace-pre-wrap max-h-24">{{ step.outputResultJson }}</pre>
                      </div>
                    </div>
                  }
                </div>
              }
            </div>
          </div>

          <!-- 3. Reasoning Trace & Agent Supervisor Console -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-3">
            <div class="flex items-center justify-between">
              <h3 class="text-xs font-bold text-white uppercase tracking-wider font-mono flex items-center gap-2">
                <span>🧠</span> Autonomous Agent Reasoning Trace & Audit Stream
              </h3>
              <span class="text-[11px] font-mono text-slate-400">Stream: agent-supervisor-channel</span>
            </div>

            <div class="p-4 bg-slate-950 rounded-xl border border-slate-800/80 font-mono text-xs text-slate-300 space-y-1 max-h-48 overflow-y-auto leading-relaxed">
              <pre class="whitespace-pre-wrap text-slate-300">{{ currentMission()?.reasoningTrace || 'No reasoning trace logged yet.' }}</pre>
            </div>
          </div>
        </div>
      </main>

      <!-- New Mission Creation Modal -->
      @if (showCreateModal()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 space-y-5 shadow-2xl">
            <div class="flex items-center justify-between">
              <h3 class="text-base font-bold text-white">Initialize Autonomous Mission</h3>
              <button (click)="showCreateModal.set(false)" class="text-slate-400 hover:text-white">✕</button>
            </div>

            <div class="space-y-4 text-xs">
              <div>
                <label class="block text-slate-300 mb-1">Mission Title</label>
                <input
                  type="text"
                  [(ngModel)]="newMissionTitle"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2 text-slate-200 focus:outline-none focus:border-indigo-500" />
              </div>

              <div>
                <label class="block text-slate-300 mb-1">Target Goal Prompt</label>
                <textarea
                  rows="3"
                  [(ngModel)]="newMissionGoal"
                  class="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2 text-slate-200 focus:outline-none focus:border-indigo-500"></textarea>
              </div>

              <div class="grid grid-cols-2 gap-4">
                <div>
                  <label class="block text-slate-300 mb-1">Autonomy Mode</label>
                  <select [(ngModel)]="newMissionMode" class="w-full bg-slate-950 border border-slate-800 rounded-xl px-3 py-2 text-slate-200">
                    <option value="Human-in-the-Loop">Human-in-the-Loop</option>
                    <option value="Auto-Approve">Auto-Approve</option>
                    <option value="Draft Only">Draft Only</option>
                  </select>
                </div>

                <div>
                  <label class="block text-slate-300 mb-1">Budget Hard-Cap (USD)</label>
                  <input
                    type="number"
                    [(ngModel)]="newMissionBudget"
                    class="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2 text-slate-200" />
                </div>
              </div>
            </div>

            <div class="flex justify-end gap-3 pt-3 border-t border-slate-800">
              <button (click)="showCreateModal.set(false)" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-xl text-xs">
                Cancel
              </button>
              <button
                (click)="createMission()"
                class="px-5 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-xl text-xs font-bold transition shadow-lg shadow-indigo-600/20">
                Start Mission Loop
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AgentMissionCockpitComponent implements OnInit {
  private http = inject(HttpClient);

  currentMission = signal<AgentMissionUi | null>(null);
  showCreateModal = signal<boolean>(false);

  newMissionTitle = 'Q4 Maghreb Retail Outreach & Closing Blitz';
  newMissionGoal = 'Search CRM leads in Retail sector, run deliverability tests, dispatch spintax cold outreach, send partnership contracts, and trigger courier COD waybills.';
  newMissionMode: 'Auto-Approve' | 'Human-in-the-Loop' | 'Draft Only' = 'Human-in-the-Loop';
  newMissionBudget = 50.0;

  budgetUsagePercent = computed(() => {
    const mission = this.currentMission();
    if (!mission || mission.totalBudgetUsd <= 0) return 0;
    return Math.min(100, (mission.spentBudgetUsd / mission.totalBudgetUsd) * 100);
  });

  ngOnInit() {
    this.initDefaultMission();
  }

  initDefaultMission() {
    const mockMission: AgentMissionUi = {
      id: 'm-9001-a1b2',
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: 'Q4 Maghreb Retail Outreach & Closing Blitz',
      targetGoal: 'Search CRM leads in Retail sector, run deliverability tests, dispatch spintax cold outreach, send partnership contracts, and trigger courier COD waybills.',
      status: 'PausedForApproval',
      autonomyMode: 'Human-in-the-Loop',
      totalBudgetUsd: 50.0,
      spentBudgetUsd: 12.45,
      tokensConsumed: 18450,
      currentStepIndex: 2,
      reasoningTrace: `[Init] Mission initialized with target goal: 'Search CRM leads in Retail sector...'. Mode: Human-in-the-Loop.
[Step 1 - crm_search_leads] Executed query: sector='Retail', country='TN'. Found 45 matching accounts. Filtered top 25 high-intent accounts. Cost: $0.05.
[Step 2 - deliverability_run_placement_test] Completed seed mailbox test across 10 inboxes. 100% inbox placement rate, SPF & DKIM verified. Cost: $0.15.
[Step 3 - outbound_send_spintax_campaign] Action requires human approval under 'Human-in-the-Loop' mode. Waiting for supervisor confirmation.`,
      createdAtUtc: new Date().toISOString(),
      steps: [
        {
          id: 'step-1',
          stepIndex: 0,
          toolName: 'crm_search_leads',
          inputArgumentsJson: JSON.stringify({ query: 'Retail AND country:TN', limit: 25 }, null, 2),
          outputResultJson: JSON.stringify({ matches: 25, sample: ['Maghreb Retail', 'Carthage Logistics'] }, null, 2),
          reasoningThought: 'Query target accounts and filter B2B merchants with verified contact emails.',
          status: 'Completed',
          executionDurationMs: 420,
          tokensConsumed: 3200,
          costUsd: 0.05,
          approvalRequired: false
        },
        {
          id: 'step-2',
          stepIndex: 1,
          toolName: 'deliverability_run_placement_test',
          inputArgumentsJson: JSON.stringify({ mailbox: 'outreach@sgrowing.io', testAddresses: ['test1@seed.io'] }, null, 2),
          outputResultJson: JSON.stringify({ score: 98, inboxRate: 1.0, spamRate: 0.0 }, null, 2),
          reasoningThought: 'Verify mailbox deliverability & spam filters before triggering cold outreach sequence.',
          status: 'Completed',
          executionDurationMs: 850,
          tokensConsumed: 4800,
          costUsd: 0.15,
          approvalRequired: false
        },
        {
          id: 'step-3',
          stepIndex: 2,
          toolName: 'outbound_send_spintax_campaign',
          inputArgumentsJson: JSON.stringify({ campaignId: 'cmp-retail-q4', recipientsCount: 25, template: '{Bonjour|Salutations}...' }, null, 2),
          outputResultJson: JSON.stringify({ status: 'Awaiting human authorization gate' }, null, 2),
          reasoningThought: 'Sensitive outbound communication: dispatching cold emails requires supervisor sign-off.',
          status: 'AwaitingApproval',
          executionDurationMs: 0,
          tokensConsumed: 2100,
          costUsd: 0.03,
          approvalRequired: true
        },
        {
          id: 'step-4',
          stepIndex: 3,
          toolName: 'contracts_dispatch_for_signature',
          inputArgumentsJson: JSON.stringify({ template: 'enterprise-merchant-sla', parties: 2 }, null, 2),
          outputResultJson: '{}',
          reasoningThought: 'Prepare enterprise contract and dispatch digital signature envelope once merchant commits.',
          status: 'Pending',
          executionDurationMs: 0,
          tokensConsumed: 0,
          costUsd: 0,
          approvalRequired: true
        },
        {
          id: 'step-5',
          stepIndex: 4,
          toolName: 'courier_generate_waybill',
          inputArgumentsJson: JSON.stringify({ courier: 'Yalidine', packageType: 'merchant-hardware-pos' }, null, 2),
          outputResultJson: '{}',
          reasoningThought: 'Generate COD logistics tracking waybill and schedule merchant POS hardware delivery.',
          status: 'Pending',
          executionDurationMs: 0,
          tokensConsumed: 0,
          costUsd: 0,
          approvalRequired: true
        }
      ]
    };

    this.currentMission.set(mockMission);
  }

  getToolDisplayName(name: string): string {
    switch (name) {
      case 'crm_search_leads': return 'Search CRM Prospects';
      case 'deliverability_run_placement_test': return 'Run Deliverability Test';
      case 'outbound_send_spintax_campaign': return 'Dispatch Spintax Campaign';
      case 'contracts_dispatch_for_signature': return 'Dispatch Digital Contract';
      case 'courier_generate_waybill': return 'Generate COD Waybill';
      default: return name;
    }
  }

  approveStep(stepId: string) {
    const mission = this.currentMission();
    if (!mission) return;

    const step = mission.steps.find(s => s.id === stepId);
    if (!step) return;

    step.status = 'Completed';
    step.approvedBy = 'Operations Lead';
    step.approvedAtUtc = new Date().toISOString();
    step.executionDurationMs = 1200;
    step.tokensConsumed = 5200;
    step.costUsd = 0.22;
    step.outputResultJson = JSON.stringify({ delivered: 25, spintaxPermutations: 25, success: true }, null, 2);

    mission.spentBudgetUsd += step.costUsd;
    mission.tokensConsumed += step.tokensConsumed;
    mission.currentStepIndex = 3;
    mission.status = 'Running';
    mission.reasoningTrace += `\n[Approval] Step 3 approved by Operations Lead. Campaign dispatched to 25 leads.\n[Next] Agent advancing to Step 4: 'contracts_dispatch_for_signature'.`;

    this.currentMission.set({ ...mission });
  }

  pauseMission() {
    const mission = this.currentMission();
    if (!mission) return;
    mission.status = 'PausedForApproval';
    mission.reasoningTrace += `\n[Manual] Mission paused by supervisor.`;
    this.currentMission.set({ ...mission });
  }

  resumeMission() {
    const mission = this.currentMission();
    if (!mission) return;
    mission.status = 'Running';
    mission.reasoningTrace += `\n[Manual] Mission resumed. Resuming autonomous MCP execution loop.`;
    this.currentMission.set({ ...mission });
  }

  executeNextStep() {
    const mission = this.currentMission();
    if (!mission) return;

    const currentIdx = mission.currentStepIndex;
    if (currentIdx >= mission.steps.length) {
      mission.status = 'Completed';
      mission.reasoningTrace += `\n[Completed] All 5 steps executed successfully. Mission complete!`;
      this.currentMission.set({ ...mission });
      return;
    }

    const currentStep = mission.steps[currentIdx];
    if (currentStep.status === 'AwaitingApproval') {
      return;
    }

    if (currentStep.approvalRequired && mission.autonomyMode === 'Human-in-the-Loop') {
      currentStep.status = 'AwaitingApproval';
      mission.status = 'PausedForApproval';
      mission.reasoningTrace += `\n[Gate] Step ${currentIdx + 1} (${currentStep.toolName}) paused for human approval.`;
      this.currentMission.set({ ...mission });
      return;
    }

    currentStep.status = 'Completed';
    currentStep.executionDurationMs = 950;
    currentStep.tokensConsumed = 4100;
    currentStep.costUsd = 0.18;
    currentStep.outputResultJson = JSON.stringify({ executed: true, tool: currentStep.toolName, timestamp: new Date().toISOString() }, null, 2);

    mission.spentBudgetUsd += currentStep.costUsd;
    mission.tokensConsumed += currentStep.tokensConsumed;
    mission.currentStepIndex = currentIdx + 1;

    if (mission.currentStepIndex >= mission.steps.length) {
      mission.status = 'Completed';
      mission.reasoningTrace += `\n[Completed] Mission finished! All steps executed.`;
    } else {
      mission.reasoningTrace += `\n[Step ${currentIdx + 1}] Executed successfully. Advancing to step ${mission.currentStepIndex + 1}.`;
    }

    this.currentMission.set({ ...mission });
  }

  createMission() {
    const newMission: AgentMissionUi = {
      id: `m-${Date.now()}`,
      tenantId: '00000000-0000-0000-0000-000000000001',
      title: this.newMissionTitle,
      targetGoal: this.newMissionGoal,
      status: 'Running',
      autonomyMode: this.newMissionMode,
      totalBudgetUsd: this.newMissionBudget,
      spentBudgetUsd: 0,
      tokensConsumed: 0,
      currentStepIndex: 0,
      reasoningTrace: `[Init] Mission initialized with target goal: '${this.newMissionGoal}'. Autonomy mode: ${this.newMissionMode}.`,
      createdAtUtc: new Date().toISOString(),
      steps: [
        {
          id: 'step-1',
          stepIndex: 0,
          toolName: 'crm_search_leads',
          inputArgumentsJson: JSON.stringify({ query: 'Retail', limit: 25 }, null, 2),
          outputResultJson: '{}',
          reasoningThought: 'Query target accounts and filter B2B merchants with verified contact emails.',
          status: 'Executing',
          executionDurationMs: 0,
          tokensConsumed: 0,
          costUsd: 0,
          approvalRequired: false
        },
        {
          id: 'step-2',
          stepIndex: 1,
          toolName: 'deliverability_run_placement_test',
          inputArgumentsJson: JSON.stringify({ mailbox: 'outreach@sgrowing.io' }, null, 2),
          outputResultJson: '{}',
          reasoningThought: 'Verify mailbox deliverability & spam filters before triggering cold outreach sequence.',
          status: 'Pending',
          executionDurationMs: 0,
          tokensConsumed: 0,
          costUsd: 0,
          approvalRequired: false
        },
        {
          id: 'step-3',
          stepIndex: 2,
          toolName: 'outbound_send_spintax_campaign',
          inputArgumentsJson: JSON.stringify({ campaignId: 'cmp-q4', recipientsCount: 25 }, null, 2),
          outputResultJson: '{}',
          reasoningThought: 'Dispatch spintax-personalized emails to 25 verified leads.',
          status: 'Pending',
          executionDurationMs: 0,
          tokensConsumed: 0,
          costUsd: 0,
          approvalRequired: true
        },
        {
          id: 'step-4',
          stepIndex: 3,
          toolName: 'contracts_dispatch_for_signature',
          inputArgumentsJson: JSON.stringify({ template: 'enterprise-merchant-sla' }, null, 2),
          outputResultJson: '{}',
          reasoningThought: 'Prepare enterprise contract and dispatch digital signature envelope once merchant commits.',
          status: 'Pending',
          executionDurationMs: 0,
          tokensConsumed: 0,
          costUsd: 0,
          approvalRequired: true
        },
        {
          id: 'step-5',
          stepIndex: 4,
          toolName: 'courier_generate_waybill',
          inputArgumentsJson: JSON.stringify({ courier: 'Yalidine' }, null, 2),
          outputResultJson: '{}',
          reasoningThought: 'Generate COD logistics tracking waybill and schedule merchant POS hardware delivery.',
          status: 'Pending',
          executionDurationMs: 0,
          tokensConsumed: 0,
          costUsd: 0,
          approvalRequired: true
        }
      ]
    };

    this.currentMission.set(newMission);
    this.showCreateModal.set(false);
  }
}
