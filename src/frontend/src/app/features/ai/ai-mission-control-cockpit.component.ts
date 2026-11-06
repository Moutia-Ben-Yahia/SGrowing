import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export type MissionTaskStatus = 'Pending' | 'Running' | 'WaitingApproval' | 'Succeeded' | 'Failed';

export interface MissionTaskNode {
  id: string;
  name: string;
  agentName: string;
  description: string;
  dependencies: string[];
  status: MissionTaskStatus;
  requiresApproval: boolean;
  output?: Record<string, any>;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
}

export interface MissionPlan {
  id: string;
  objectiveTitle: string;
  strategicGoal: string;
  budgetLimit: number;
  targetGeo: string;
  status: 'Draft' | 'InProgress' | 'WaitingApproval' | 'Completed' | 'Failed';
  tasks: MissionTaskNode[];
  createdAt: string;
  approvedAt?: string;
  reviewerNotes?: string;
}

@Component({
  selector: 'app-ai-mission-control-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Header -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-50">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-white">AI Mission Control DAG Cockpit</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-medium">
              Autonomous Agent Orchestration
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <span class="text-xs text-slate-400 font-mono">Mission:</span>
          <span [class]="missionBadgeClass()">
            {{ missionStatusLabel() }}
          </span>
          <button
            type="button"
            (click)="planNewMission()"
            class="px-3.5 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
          >
            <span>🔄</span> Plan New Mission
          </button>
          <button
            type="button"
            (click)="executeNextAutonomousStep()"
            [disabled]="!canExecuteNext() || isExecuting()"
            class="px-4 py-2 rounded-lg bg-gradient-to-r from-cyan-600 to-teal-600 hover:from-cyan-500 hover:to-teal-500 disabled:opacity-40 disabled:cursor-not-allowed text-xs font-bold text-white shadow-lg shadow-cyan-950/40 transition flex items-center gap-2"
          >
            @if (isExecuting()) {
              <span class="animate-spin inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full"></span>
              Executing Step...
            } @else {
              <span>⚡</span> Execute Next Autonomous Step
            }
          </button>
        </div>
      </header>

      <!-- Main Layout -->
      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- Top Mission Strategic Parameters -->
        <section class="bg-slate-900/60 border border-slate-800 rounded-2xl p-5 shadow-xl">
          <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-2">
              <span class="text-lg">🎯</span>
              <h2 class="text-sm font-bold uppercase tracking-wider text-slate-300">Strategic Mission Parameters</h2>
            </div>
            <div class="flex items-center gap-4 text-xs">
              <span class="text-slate-400">DAG Progress:</span>
              <div class="w-36 bg-slate-800 rounded-full h-2.5 overflow-hidden">
                <div class="bg-gradient-to-r from-cyan-500 to-emerald-400 h-2.5 transition-all duration-500" [style.width.%]="progressPercent()"></div>
              </div>
              <span class="font-mono font-bold text-cyan-300">{{ progressPercent() }}%</span>
              <span class="text-slate-400">({{ completedTasksCount() }}/5 tasks)</span>
            </div>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div class="bg-slate-950/60 p-3.5 rounded-xl border border-slate-800/80">
              <label class="text-[11px] font-medium text-slate-400 uppercase tracking-wide">Objective Title</label>
              <input
                type="text"
                [ngModel]="objectiveTitle()"
                (ngModelChange)="objectiveTitle.set($event)"
                class="mt-1 w-full bg-slate-900 border border-slate-700/60 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-cyan-500"
              />
            </div>

            <div class="bg-slate-950/60 p-3.5 rounded-xl border border-slate-800/80 md:col-span-2">
              <label class="text-[11px] font-medium text-slate-400 uppercase tracking-wide">Strategic Goal</label>
              <input
                type="text"
                [ngModel]="strategicGoal()"
                (ngModelChange)="strategicGoal.set($event)"
                class="mt-1 w-full bg-slate-900 border border-slate-700/60 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-cyan-500"
              />
            </div>

            <div class="bg-slate-950/60 p-3.5 rounded-xl border border-slate-800/80 flex gap-3">
              <div class="flex-1">
                <label class="text-[11px] font-medium text-slate-400 uppercase tracking-wide">Budget (SAR)</label>
                <input
                  type="number"
                  [ngModel]="budgetLimit()"
                  (ngModelChange)="budgetLimit.set(+$event)"
                  class="mt-1 w-full bg-slate-900 border border-slate-700/60 rounded-lg px-2.5 py-1.5 text-xs text-emerald-400 font-mono font-bold focus:outline-none focus:border-cyan-500"
                />
              </div>
              <div class="flex-1">
                <label class="text-[11px] font-medium text-slate-400 uppercase tracking-wide">Target Geo</label>
                <input
                  type="text"
                  [ngModel]="targetGeo()"
                  (ngModelChange)="targetGeo.set($event)"
                  class="mt-1 w-full bg-slate-900 border border-slate-700/60 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-cyan-500"
                />
              </div>
            </div>
          </div>
        </section>

        <!-- Main Workspace: DAG Pipeline Visualizer + Selected Task Inspector -->
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
          <!-- Left: 5-Node DAG Pipeline Visualization (7 cols) -->
          <section class="lg:col-span-7 bg-slate-900/60 border border-slate-800 rounded-2xl p-5 shadow-xl flex flex-col">
            <div class="flex items-center justify-between mb-4">
              <div>
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>🧬</span> Multi-Agent DAG Pipeline
                </h3>
                <p class="text-xs text-slate-400 mt-0.5">Sequential dependencies with Sovereign Gatekeeper approval gate</p>
              </div>
              <span class="text-xs text-slate-500 font-mono">5 Autonomous Nodes</span>
            </div>

            <!-- DAG Flow Container -->
            <div class="space-y-3 flex-1">
              @for (task of currentTasks(); track task.id; let i = $index) {
                <div
                  (click)="selectTask(task)"
                  [class]="getTaskCardClass(task)"
                >
                  <div class="flex items-start justify-between gap-3">
                    <div class="flex items-start gap-3">
                      <!-- Node Sequence Index -->
                      <div class="w-7 h-7 rounded-lg bg-slate-800 flex items-center justify-center font-mono text-xs font-bold shrink-0 mt-0.5"
                           [class.text-cyan-400]="task.status === 'Running'"
                           [class.text-emerald-400]="task.status === 'Succeeded'"
                           [class.text-amber-400]="task.status === 'WaitingApproval'"
                           [class.text-slate-400]="task.status === 'Pending'">
                        {{ i + 1 }}
                      </div>

                      <div>
                        <div class="flex items-center gap-2">
                          <h4 class="text-xs font-bold text-white">{{ task.name }}</h4>
                          @if (task.requiresApproval) {
                            <span class="text-[10px] px-1.5 py-0.5 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30 font-medium">
                              Approval Gate
                            </span>
                          }
                        </div>
                        <p class="text-[11px] text-slate-400 mt-0.5">{{ task.description }}</p>
                        <div class="flex items-center gap-3 mt-1.5 text-[11px]">
                          <span class="text-slate-500">Agent:</span>
                          <span class="text-cyan-300 font-medium font-mono">{{ task.agentName }}</span>
                          @if (task.durationMs) {
                            <span class="text-slate-600">•</span>
                            <span class="text-slate-400 font-mono">{{ task.durationMs }}ms</span>
                          }
                        </div>
                      </div>
                    </div>

                    <!-- Status Badge -->
                    <div class="shrink-0">
                      <span [class]="getTaskStatusBadgeClass(task.status)">
                        @if (task.status === 'WaitingApproval') {
                          <span class="w-1.5 h-1.5 rounded-full bg-amber-400 animate-ping"></span>
                        }
                        {{ task.status }}
                      </span>
                    </div>
                  </div>

                  <!-- Connector Arrow (except last) -->
                  @if (i < 4) {
                    <div class="flex justify-center -mb-2 mt-2">
                      <span class="text-slate-700 text-xs">↓</span>
                    </div>
                  }
                </div>
              }
            </div>
          </section>

          <!-- Right: Selected Task Inspector & Human-In-The-Loop Approval (5 cols) -->
          <section class="lg:col-span-5 bg-slate-900/60 border border-slate-800 rounded-2xl p-5 shadow-xl flex flex-col justify-between">
            <div>
              <div class="flex items-center justify-between pb-3 border-b border-slate-800">
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>🔍</span> Node Inspector & Payload Output
                </h3>
                @if (selectedTask(); as sel) {
                  <span class="font-mono text-xs text-cyan-400 font-semibold">{{ sel.id }}</span>
                }
              </div>

              @if (selectedTask(); as sel) {
                <div class="mt-4 space-y-4">
                  <div class="bg-slate-950/80 p-3.5 rounded-xl border border-slate-800">
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-bold text-white">{{ sel.name }}</span>
                      <span [class]="getTaskStatusBadgeClass(sel.status)">{{ sel.status }}</span>
                    </div>
                    <div class="text-xs text-slate-400 mt-1">Orchestrated by <strong class="text-cyan-300">{{ sel.agentName }}</strong></div>
                    <div class="text-xs text-slate-400 mt-2">{{ sel.description }}</div>
                    @if (sel.completedAt) {
                      <div class="text-[10px] text-slate-500 font-mono mt-2">Completed At: {{ sel.completedAt }}</div>
                    }
                  </div>

                  <!-- Payload JSON Inspector -->
                  <div>
                    <div class="flex items-center justify-between mb-1.5">
                      <label class="text-[11px] uppercase tracking-wider font-semibold text-slate-400">Node Output Payload</label>
                      @if (sel.output) {
                        <span class="text-[10px] text-emerald-400 font-mono">Payload Generated</span>
                      } @else {
                        <span class="text-[10px] text-slate-500 font-mono">No Output Yet</span>
                      }
                    </div>
                    <pre class="bg-slate-950 p-3 rounded-xl border border-slate-800 text-[11px] font-mono text-emerald-300/90 overflow-x-auto max-h-56 select-text whitespace-pre-wrap">{{ sel.output ? (sel.output | json) : '// Output payload will populate upon task completion' }}</pre>
                  </div>
                </div>
              } @else {
                <div class="p-8 text-center text-slate-500 text-xs">
                  Select a task from the DAG to view its metadata, agent configuration, and execution output payload.
                </div>
              }
            </div>

            <!-- Human-in-the-loop Gatekeeper Approval Panel -->
            <div class="mt-6 pt-4 border-t border-slate-800">
              <div class="bg-amber-950/20 border border-amber-500/30 rounded-xl p-4">
                <div class="flex items-center gap-2 mb-2">
                  <span class="w-2.5 h-2.5 rounded-full bg-amber-400 animate-pulse"></span>
                  <h4 class="text-xs font-bold text-amber-300">Human-In-The-Loop Approval Gate</h4>
                </div>
                <p class="text-[11px] text-amber-200/80 mb-3">
                  Task 5 (Budget Allocation & Launch Sign-Off) enforces human oversight before ad spend commitments.
                </p>

                <div class="space-y-2">
                  <label class="text-[11px] text-slate-400 font-medium">Reviewer Sign-off Notes:</label>
                  <textarea
                    rows="2"
                    [ngModel]="reviewerNotes()"
                    (ngModelChange)="reviewerNotes.set($event)"
                    placeholder="Enter approval audit comments, budget adjustments, or launch constraints..."
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-xs text-white placeholder-slate-600 focus:outline-none focus:border-amber-400"
                  ></textarea>

                  <div class="flex items-center gap-2 pt-1">
                    <button
                      type="button"
                      (click)="approveMission()"
                      [disabled]="!canApprove()"
                      class="flex-1 py-2 px-3 rounded-lg bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 disabled:cursor-not-allowed text-xs font-bold text-white shadow-lg shadow-emerald-950/40 transition flex items-center justify-center gap-1.5"
                    >
                      <span>✅</span> Approve & Launch Mission
                    </button>
                    <button
                      type="button"
                      (click)="rejectMission()"
                      [disabled]="!canApprove()"
                      class="py-2 px-3 rounded-lg bg-rose-950/60 hover:bg-rose-900 border border-rose-800/80 disabled:opacity-40 disabled:cursor-not-allowed text-xs font-bold text-rose-300 transition"
                    >
                      Reject
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </section>
        </div>
      </main>
    </div>
  `
})
export class AiMissionControlCockpitComponent {
  // Required signals matching spec
  readonly objectiveTitle = signal('Summer COD Promo in Riyadh');
  readonly strategicGoal = signal('Acquire 2,500 new high-LTV COD orders with 3.5x Target ROAS');
  readonly budgetLimit = signal<number>(15000);
  readonly targetGeo = signal('KSA / Riyadh & Jeddah');
  readonly currentMission = signal<MissionPlan | null>(null);
  readonly selectedTask = signal<MissionTaskNode | null>(null);
  readonly reviewerNotes = signal('');
  readonly isExecuting = signal(false);

  // Computed signals
  readonly currentTasks = computed<MissionTaskNode[]>(() => {
    return this.currentMission()?.tasks ?? [];
  });

  readonly completedTasksCount = computed<number>(() => {
    return this.currentTasks().filter(t => t.status === 'Succeeded').length;
  });

  readonly progressPercent = computed<number>(() => {
    const total = this.currentTasks().length;
    if (total === 0) return 0;
    return Math.round((this.completedTasksCount() / total) * 100);
  });

  readonly canApprove = computed<boolean>(() => {
    const task5 = this.currentTasks().find(t => t.id === 'task-5');
    return task5?.status === 'WaitingApproval';
  });

  readonly canExecuteNext = computed<boolean>(() => {
    const mission = this.currentMission();
    if (!mission || mission.status === 'Completed' || mission.status === 'Failed' || mission.status === 'WaitingApproval') {
      return false;
    }
    const nextTask = this.findNextPendingTask();
    return !!nextTask && nextTask.status !== 'WaitingApproval';
  });

  readonly missionStatusLabel = computed<string>(() => {
    const mission = this.currentMission();
    if (!mission) return 'No Active Mission';
    switch (mission.status) {
      case 'Draft': return 'Ready to Start';
      case 'InProgress': return 'Executing DAG';
      case 'WaitingApproval': return 'Awaiting Gatekeeper';
      case 'Completed': return 'Mission Launched';
      case 'Failed': return 'Mission Aborted';
      default: return mission.status;
    }
  });

  readonly missionBadgeClass = computed<string>(() => {
    const mission = this.currentMission();
    if (!mission) return 'text-xs px-2.5 py-1 rounded-md bg-slate-800 text-slate-400 font-mono';
    switch (mission.status) {
      case 'Draft':
        return 'text-xs px-2.5 py-1 rounded-md bg-slate-800 text-slate-300 font-mono border border-slate-700';
      case 'InProgress':
        return 'text-xs px-2.5 py-1 rounded-md bg-cyan-500/20 text-cyan-300 font-mono border border-cyan-500/40 animate-pulse';
      case 'WaitingApproval':
        return 'text-xs px-2.5 py-1 rounded-md bg-amber-500/20 text-amber-300 font-mono border border-amber-500/40';
      case 'Completed':
        return 'text-xs px-2.5 py-1 rounded-md bg-emerald-500/20 text-emerald-300 font-mono border border-emerald-500/40';
      case 'Failed':
        return 'text-xs px-2.5 py-1 rounded-md bg-rose-500/20 text-rose-300 font-mono border border-rose-500/40';
      default:
        return 'text-xs px-2.5 py-1 rounded-md bg-slate-800 text-slate-400 font-mono';
    }
  });

  constructor() {
    this.planNewMission();
  }

  planNewMission(): void {
    const tasks: MissionTaskNode[] = [
      {
        id: 'task-1',
        name: 'Market & Competitor Signal Analysis',
        agentName: 'ResearchAgent',
        description: 'Aggregate localized search demand, competitor discounts, and cash-on-delivery success rates in Riyadh and Jeddah.',
        dependencies: [],
        status: 'Pending',
        requiresApproval: false
      },
      {
        id: 'task-2',
        name: 'SEO Keyword Strategy & Content Brief',
        agentName: 'SeoAgent',
        description: 'Formulate high-conversion Arabic commercial intent keyword cluster and structured landing page outline.',
        dependencies: ['task-1'],
        status: 'Pending',
        requiresApproval: false
      },
      {
        id: 'task-3',
        name: 'Multilingual Landing Page Copy',
        agentName: 'ContentAgent',
        description: 'Generate high-urgency Modern Standard Arabic and Gulf dialect persuasive copy with bilingual trust signals.',
        dependencies: ['task-2'],
        status: 'Pending',
        requiresApproval: false
      },
      {
        id: 'task-4',
        name: 'Omnichannel Spintax Ad Campaigns',
        agentName: 'AdvertisingAgent',
        description: 'Create multi-variant ad headlines, visual prompts, and Spintax templates for TikTok, Snapchat, and Meta.',
        dependencies: ['task-3'],
        status: 'Pending',
        requiresApproval: false
      },
      {
        id: 'task-5',
        name: 'Budget Allocation & Launch Sign-Off',
        agentName: 'ApprovalGatekeeper',
        description: 'Verify 3.5x ROAS model constraints and enforce human-in-the-loop executive sign-off prior to ad dispatch.',
        dependencies: ['task-4'],
        status: 'Pending',
        requiresApproval: true
      }
    ];

    const plan: MissionPlan = {
      id: `mission-${Date.now()}`,
      objectiveTitle: this.objectiveTitle(),
      strategicGoal: this.strategicGoal(),
      budgetLimit: this.budgetLimit(),
      targetGeo: this.targetGeo(),
      status: 'Draft',
      tasks,
      createdAt: new Date().toISOString()
    };

    this.currentMission.set(plan);
    this.selectedTask.set(tasks[0]);
    this.reviewerNotes.set('');
    this.isExecuting.set(false);
  }

  selectTask(task: MissionTaskNode): void {
    this.selectedTask.set(task);
  }

  executeNextAutonomousStep(): void {
    const mission = this.currentMission();
    if (!mission) return;

    const nextTask = this.findNextPendingTask();
    if (!nextTask) return;

    this.isExecuting.set(true);
    mission.status = 'InProgress';

    // Update task status to Running
    nextTask.status = 'Running';
    nextTask.startedAt = new Date().toISOString();
    this.selectedTask.set({ ...nextTask });

    // Simulate completion
    const sampleOutput = this.generateSampleOutput(nextTask.id);
    nextTask.output = sampleOutput;
    nextTask.completedAt = new Date().toISOString();
    nextTask.durationMs = Math.floor(Math.random() * 450) + 180;

    if (nextTask.requiresApproval) {
      nextTask.status = 'WaitingApproval';
      mission.status = 'WaitingApproval';
    } else {
      nextTask.status = 'Succeeded';
      // If task-4 just succeeded, automatically transition task-5 to WaitingApproval for review
      if (nextTask.id === 'task-4') {
        const task5 = mission.tasks.find(t => t.id === 'task-5');
        if (task5) {
          task5.status = 'WaitingApproval';
          task5.startedAt = new Date().toISOString();
          task5.output = this.generateSampleOutput('task-5');
          mission.status = 'WaitingApproval';
          this.selectedTask.set({ ...task5 });
        }
      }
    }

    this.isExecuting.set(false);
    this.currentMission.set({
      ...mission,
      tasks: mission.tasks.map(t => ({ ...t }))
    });
    const updatedTask = this.currentMission()?.tasks.find(t => t.id === nextTask.id) ?? nextTask;
    this.selectedTask.set(updatedTask);
  }

  approveMission(notes?: string): void {
    const mission = this.currentMission();
    if (!mission) return;

    const task5 = mission.tasks.find(t => t.id === 'task-5');
    if (!task5 || task5.status !== 'WaitingApproval') return;

    const finalNotes = notes ?? this.reviewerNotes() ?? 'Approved with standard guardrails';
    this.reviewerNotes.set(finalNotes);

    task5.status = 'Succeeded';
    task5.completedAt = new Date().toISOString();
    task5.output = {
      ...(task5.output ?? {}),
      humanApproval: {
        signOffGranted: true,
        reviewerNotes: finalNotes,
        approvedAt: new Date().toISOString(),
        authorizedBudget: this.budgetLimit(),
        targetGeo: this.targetGeo()
      }
    };

    mission.status = 'Completed';
    mission.approvedAt = new Date().toISOString();
    mission.reviewerNotes = finalNotes;

    this.currentMission.set({
      ...mission,
      tasks: mission.tasks.map(t => ({ ...t }))
    });
    this.selectedTask.set(this.currentMission()?.tasks.find(t => t.id === 'task-5') ?? task5);
  }

  rejectMission(reason?: string): void {
    const mission = this.currentMission();
    if (!mission) return;

    const task5 = mission.tasks.find(t => t.id === 'task-5');
    if (!task5) return;

    task5.status = 'Failed';
    task5.completedAt = new Date().toISOString();
    task5.output = {
      rejectionReason: reason || this.reviewerNotes() || 'Budget threshold exceeded or strategic parameters adjusted'
    };

    mission.status = 'Failed';
    this.currentMission.set({
      ...mission,
      tasks: mission.tasks.map(t => ({ ...t }))
    });
    this.selectedTask.set(this.currentMission()?.tasks.find(t => t.id === 'task-5') ?? task5);
  }

  private findNextPendingTask(): MissionTaskNode | undefined {
    const tasks = this.currentTasks();
    return tasks.find(t => {
      if (t.status !== 'Pending') return false;
      const depsMet = t.dependencies.every(depId => {
        const dep = tasks.find(d => d.id === depId);
        return dep && dep.status === 'Succeeded';
      });
      return depsMet;
    });
  }

  private generateSampleOutput(taskId: string): Record<string, any> {
    switch (taskId) {
      case 'task-1':
        return {
          marketSignals: [
            'High mobile commerce velocity in Riyadh Olaya & Al-Malqa districts',
            'COD preference 64% among first-time buyers on seasonal SKUs'
          ],
          competitorDiscountRange: '15% - 25% SAR',
          primePeakWindow: '19:00 - 23:30 AST',
          predictedCODSuccessRate: '86.4%'
        };
      case 'task-2':
        return {
          targetKeywords: [
            'عروض الصيف بالرياض',
            'توصيل سريع الدفع عند الاستلام',
            'كود خصم صيف الرياض 2026'
          ],
          monthlySearchVolume: 142500,
          intentCategory: 'High Commercial Intent (COD Ready)'
        };
      case 'task-3':
        return {
          heroCopyArabic: 'عروض الصيف الكبرى في الرياض وجدة — الدفع عند الاستلام وضمان استرجاع 14 يوم!',
          heroCopyEnglish: 'Riyadh & Jeddah Mega Summer Deals — Cash On Delivery with 14-Day Hassle-Free Returns!',
          callToAction: 'اطلب الآن والدفع عند الباب',
          trustBadges: ['SAR Guaranteed Pricing', 'Fast 24H Dispatch', 'Zero Prepayment Risk']
        };
      case 'task-4':
        return {
          spintaxTemplate: '{احصل على|اكتشف|وفر مع} أفضل عروض الصيف في {الرياض|جدة} مع {شحن سريع|توصيل فوري|دفع عند الاستلام}.',
          channels: ['TikTok Spark Ads', 'Snapchat Story Ads', 'Meta Reels'],
          campaignBudgetAllocationSAR: 13500,
          projectedROAS: '3.62x'
        };
      case 'task-5':
        return {
          financialAudit: {
            plannedSpendSAR: this.budgetLimit(),
            modelTargetROAS: 3.5,
            expectedGrossRevenueSAR: this.budgetLimit() * 3.5,
            gatekeeperStatus: 'ReadyForHumanSignoff'
          }
        };
      default:
        return { status: 'Executed', timestamp: new Date().toISOString() };
    }
  }

  getTaskCardClass(task: MissionTaskNode): string {
    const isSelected = this.selectedTask()?.id === task.id;
    const base = 'p-3.5 rounded-xl border transition cursor-pointer ';
    const selectedRing = isSelected ? ' ring-2 ring-cyan-400 ' : '';

    switch (task.status) {
      case 'Succeeded':
        return base + selectedRing + 'bg-emerald-950/20 border-emerald-500/30 hover:border-emerald-500/60';
      case 'Running':
        return base + selectedRing + 'bg-cyan-950/30 border-cyan-500/40 hover:border-cyan-500/70';
      case 'WaitingApproval':
        return base + selectedRing + 'bg-amber-950/30 border-amber-500/40 hover:border-amber-500/70 animate-pulse';
      case 'Failed':
        return base + selectedRing + 'bg-rose-950/20 border-rose-500/30 hover:border-rose-500/60';
      default:
        return base + selectedRing + 'bg-slate-950/40 border-slate-800 hover:border-slate-700';
    }
  }

  getTaskStatusBadgeClass(status: MissionTaskStatus): string {
    const base = 'text-[10px] font-mono font-semibold px-2 py-0.5 rounded-full inline-flex items-center gap-1.5 ';
    switch (status) {
      case 'Succeeded':
        return base + 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      case 'Running':
        return base + 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30';
      case 'WaitingApproval':
        return base + 'bg-amber-500/20 text-amber-300 border border-amber-500/30 animate-pulse';
      case 'Failed':
        return base + 'bg-rose-500/20 text-rose-300 border border-rose-500/30';
      default:
        return base + 'bg-slate-800 text-slate-400 border border-slate-700';
    }
  }
}
