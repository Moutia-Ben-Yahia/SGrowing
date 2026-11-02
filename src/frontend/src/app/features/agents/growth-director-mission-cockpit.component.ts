import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface DecomposedTaskItem {
  id: string;
  agentType: 'SEO_Specialist' | 'Content_Copywriter' | 'Ad_Optimizer' | 'Automation_Architect';
  agentIcon: string;
  title: string;
  description: string;
  allocatedBudget: number;
  requiresApproval: boolean;
  isApproved: boolean;
  status: 'Pending' | 'Approved' | 'Executed' | 'Rejected';
}

@Component({
  selector: 'app-growth-director-mission-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Header -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/agents" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Agents
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              🎯 Autonomous AI Growth Director Mission Cockpit & HITL Guardrails
            </span>
            <span class="bg-violet-500/20 text-violet-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-violet-500/30">
              Domain J3 • Supervised Autonomy • Multi-Agent Mission Loop
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <button
              (click)="decomposeMission()"
              data-testid="decompose-mission-btn"
              class="px-3.5 py-1.5 bg-violet-600 hover:bg-violet-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md shadow-violet-900/30">
              <span>🤖</span> Decompose Objective
            </button>
          </div>
        </header>

        <!-- KPI Metrics Ribbon -->
        <section class="grid grid-cols-1 md:grid-cols-4 gap-4 p-6 border-b border-slate-800/80 bg-slate-900/40">
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Mission Status</div>
            <div
              class="text-base font-bold mt-1 font-mono flex items-center gap-1.5"
              [class.text-amber-400]="missionStatus() === 'PendingApproval'"
              [class.text-emerald-400]="missionStatus() === 'Executed'">
              {{ missionStatus() === 'PendingApproval' ? '⏳ Pending Approval' : (missionStatus() === 'Executed' ? '🚀 Active Execution' : '📋 Planned') }}
            </div>
            <div class="text-[11px] text-slate-500 mt-1">Autonomous supervisor state</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Total Allocated Budget</div>
            <div class="text-2xl font-bold text-cyan-400 mt-1">{{ totalAllocatedBudget() | currency:'USD':'symbol':'1.0-0' }}</div>
            <div class="text-[11px] text-slate-500 mt-1">Cap: {{ maxBudgetUsd | currency:'USD':'symbol':'1.0-0' }}</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Projected CAC</div>
            <div class="text-2xl font-bold text-emerald-400 mt-1">$12.50</div>
            <div class="text-[11px] text-slate-500 mt-1">Target: &lt; $40.00 / Lead</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Projected Pipeline Value</div>
            <div class="text-2xl font-bold text-indigo-400 mt-1">$84,000</div>
            <div class="text-[11px] text-slate-500 mt-1">Estimated ROI: 5.6x</div>
          </div>
        </section>

        <!-- HITL Financial Guardrail Warning Banner -->
        @if (requiresHumanReview()) {
          <div class="mx-6 mt-6 p-4 rounded-xl bg-amber-950/60 border border-amber-800 text-amber-200 flex items-center justify-between">
            <div class="flex items-center gap-3">
              <span class="text-xl">🛡️</span>
              <div>
                <strong class="text-sm font-semibold">Human-in-the-Loop Financial Guardrail Alert</strong>
                <p class="text-xs text-amber-300 font-mono mt-0.5">
                  Sub-tasks exceed the $250 autonomous spend threshold. Financial authorization required before executing paid advertising.
                </p>
              </div>
            </div>
            <button
              (click)="approveAndExecute()"
              data-testid="approve-and-execute-btn"
              class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md shadow-emerald-900/30">
              <span>✅</span> Approve & Execute Mission
            </button>
          </div>
        }

        <!-- Main Workbench Layout -->
        <div class="flex-1 p-6 grid grid-cols-1 lg:grid-cols-3 gap-6">
          <!-- Left 2 Cols: Objective Form & 4 Agent Task Cards -->
          <div class="lg:col-span-2 space-y-6">
            <!-- Objective Definition Card -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>🎯</span> Growth Mission Objective
              </h3>

              <div class="grid grid-cols-1 md:grid-cols-3 gap-3 mb-2">
                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Target Metric</label>
                  <select
                    [(ngModel)]="targetMetric"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-violet-500">
                    <option value="Qualified Leads">Qualified Leads</option>
                    <option value="Demo Bookings">Inbound Demo Bookings</option>
                    <option value="MRR Growth">Monthly Recurring Revenue</option>
                  </select>
                </div>

                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Target Metric Goal</label>
                  <input
                    type="number"
                    [(ngModel)]="targetValue"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-violet-500" />
                </div>

                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Max Budget Cap ($)</label>
                  <input
                    type="number"
                    [(ngModel)]="maxBudgetUsd"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-violet-500" />
                </div>
              </div>
            </div>

            <!-- 4 Decomposed Specialist Agent Tasks Grid -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-4">
                <span>🤖</span> Coordinated Multi-Agent Task Decomposition Canvas
              </h3>

              <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                @for (task of tasks(); track task.id) {
                  <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 flex flex-col justify-between">
                    <div>
                      <div class="flex items-center justify-between mb-2">
                        <span class="text-base flex items-center gap-1.5">
                          <span>{{ task.agentIcon }}</span>
                          <strong class="text-xs text-white">{{ task.title }}</strong>
                        </span>
                        <span
                          class="text-[10px] font-mono px-2 py-0.5 rounded-full border uppercase tracking-wider"
                          [class.bg-emerald-500/20]="task.status === 'Executed' || task.status === 'Approved'"
                          [class.text-emerald-300]="task.status === 'Executed' || task.status === 'Approved'"
                          [class.border-emerald-500/30]="task.status === 'Executed' || task.status === 'Approved'"
                          [class.bg-amber-500/20]="task.status === 'Pending'"
                          [class.text-amber-300]="task.status === 'Pending'"
                          [class.border-amber-500/30]="task.status === 'Pending'">
                          {{ task.status }}
                        </span>
                      </div>

                      <p class="text-xs text-slate-400 mb-3">{{ task.description }}</p>
                    </div>

                    <div class="pt-3 border-t border-slate-900 flex items-center justify-between text-xs">
                      <span class="text-slate-400">
                        Allocated: <strong class="text-cyan-400 font-mono">{{ task.allocatedBudget | currency:'USD':'symbol':'1.0-0' }}</strong>
                      </span>

                      @if (task.requiresApproval) {
                        <span class="text-[10px] text-amber-400 font-mono flex items-center gap-1">
                          ⚠️ Requires HITL Review
                        </span>
                      } @else {
                        <span class="text-[10px] text-emerald-400 font-mono flex items-center gap-1">
                          ⚡ Auto-Approved
                        </span>
                      }
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Right Col: Financial Policies & Mission Audit Log -->
          <div class="space-y-6">
            <!-- Financial Policy Card -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>🛡️</span> Financial Guardrail Policies
              </h3>
              <div class="space-y-2 text-xs">
                <div class="flex justify-between p-2.5 rounded-lg bg-slate-950 border border-slate-800">
                  <span class="text-slate-400">Auto-Approve Limit:</span>
                  <strong class="text-slate-200 font-mono">$250 / Task</strong>
                </div>
                <div class="flex justify-between p-2.5 rounded-lg bg-slate-950 border border-slate-800">
                  <span class="text-slate-400">Max Daily Spend:</span>
                  <strong class="text-slate-200 font-mono">$2,000 / Day</strong>
                </div>
                <div class="flex justify-between p-2.5 rounded-lg bg-slate-950 border border-slate-800">
                  <span class="text-slate-400">Audience Hash Sync:</span>
                  <strong class="text-slate-200 font-mono">HITL Mandated</strong>
                </div>
              </div>
            </div>

            <!-- Execution Audit Trail -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>📜</span> Mission Execution Log
              </h3>
              <div class="space-y-2.5">
                @for (log of auditTrail(); track log) {
                  <div class="p-2.5 rounded-lg bg-slate-950 border border-slate-800 text-[11px] font-mono text-slate-400">
                    {{ log }}
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
export class GrowthDirectorMissionCockpitComponent {
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

  public targetMetric = 'Qualified Leads';
  public targetValue = 100;
  public maxBudgetUsd = 1500;

  public missionStatus = signal<'Planned' | 'PendingApproval' | 'Executed'>('PendingApproval');

  public tasks = signal<DecomposedTaskItem[]>([
    {
      id: 'task-seo',
      agentType: 'SEO_Specialist',
      agentIcon: '🔍',
      title: 'SEO Keyword Gap Audit',
      description: 'Audit competitor keyword gaps and generate high-intent search briefs.',
      allocatedBudget: 200,
      requiresApproval: false,
      isApproved: true,
      status: 'Approved'
    },
    {
      id: 'task-copy',
      agentType: 'Content_Copywriter',
      agentIcon: '✍️',
      title: 'High-Converting Copy Draft',
      description: 'Draft B2B landing page variants, lead magnet guides, and email hooks.',
      allocatedBudget: 250,
      requiresApproval: false,
      isApproved: true,
      status: 'Approved'
    },
    {
      id: 'task-ads',
      agentType: 'Ad_Optimizer',
      agentIcon: '📈',
      title: 'Meta & Google CAPI Campaign Allocation',
      description: 'Allocate advertising budget across Meta & Google CAPI audiences.',
      allocatedBudget: 650,
      requiresApproval: true,
      isApproved: false,
      status: 'Pending'
    },
    {
      id: 'task-auto',
      agentType: 'Automation_Architect',
      agentIcon: '⚡',
      title: 'Nurture DAG Workflow Configuration',
      description: 'Configure and test the multi-branch customer onboarding nurture DAG.',
      allocatedBudget: 150,
      requiresApproval: false,
      isApproved: true,
      status: 'Approved'
    }
  ]);

  public totalAllocatedBudget = computed(() => {
    return this.tasks().reduce((sum, t) => sum + t.allocatedBudget, 0);
  });

  public requiresHumanReview = computed(() => {
    return this.tasks().some(t => t.requiresApproval && !t.isApproved);
  });

  public auditTrail = signal<string[]>([
    'Objective initialized: 100 Qualified Leads ($1,500 Budget Cap)',
    'Growth Director decomposed objective into 4 specialist agent tasks',
    'Financial policy check: Task "task-ads" ($650) flagged for HITL review'
  ]);

  public decomposeMission(): void {
    this.missionStatus.set('PendingApproval');
    this.auditTrail.update(logs => [
      `Decomposed mission for target: ${this.targetValue} ${this.targetMetric}`,
      ...logs
    ]);
  }

  public approveAndExecute(): void {
    this.tasks.update(ts => ts.map(t => ({
      ...t,
      isApproved: true,
      status: 'Executed'
    })));

    this.missionStatus.set('Executed');
    this.auditTrail.update(logs => [
      'HITL Review approved by Operator: All 4 specialist agents dispatched',
      ...logs
    ]);
  }
}
