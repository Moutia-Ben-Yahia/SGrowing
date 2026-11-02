import { Component, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export interface SwarmTaskItem {
  id: string;
  role: string;
  title: string;
  status: 'Completed' | 'AwaitingApproval' | 'Executing' | 'Rejected';
  requiresHitl: boolean;
  output: string;
}

export interface MissionItem {
  id: string;
  title: string;
  goal: string;
  status: 'Active' | 'Completed';
  tasks: SwarmTaskItem[];
  scratchpad: Record<string, string>;
}

@Component({
  selector: 'app-swarm-orchestration-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="p-6 max-w-7xl mx-auto space-y-6 text-slate-100 font-sans">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-2">
            <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
              Horizon 20 • Domain J1, J2 & J3
            </span>
            <span class="text-xs text-slate-400">Autonomous Multi-Agent Swarm & HITL Protocol</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">
            Autonomous Multi-Agent Swarm Orchestration Cockpit
          </h1>
          <p class="text-sm text-slate-400">
            Multi-persona task decomposition, contextual shared scratchpad memory, and Human-in-the-Loop policy gatekeepers.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button (click)="launchNewMission()"
                  class="px-3.5 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold shadow-lg shadow-indigo-600/30 transition">
            + Launch Swarm Mission 🤖
          </button>
        </div>
      </div>

      <!-- Top Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Active Swarm Personas</div>
          <div class="text-2xl font-bold text-white mt-1">5 Agents</div>
          <div class="text-xs text-slate-500 mt-0.5">Researcher • Copy • SEO • Ads • Guard</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Total Mission Tasks</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">{{ activeMission().tasks.length }}</div>
          <div class="text-xs text-slate-500 mt-0.5">{{ completedTasksCount() }} Completed Autonomously</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Pending HITL Approvals</div>
          <div class="text-2xl font-bold mt-1" [ngClass]="pendingApprovalTasks().length > 0 ? 'text-amber-400' : 'text-emerald-400'">
            {{ pendingApprovalTasks().length }}
          </div>
          <div class="text-xs text-slate-500 mt-0.5">High-Risk Safeguard Policy Gate</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Mission Status</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ activeMission().status }}</div>
          <div class="text-xs text-slate-500 mt-0.5">Autonomous Execution Loop</div>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Agent Task Waterfall (2 Cols) -->
        <div class="lg:col-span-2 space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">Mission Task Waterfall</h2>
            <span class="text-xs text-slate-500">Mission: {{ activeMission().title }}</span>
          </div>

          <div class="space-y-3">
            @for (task of activeMission().tasks; track task.id) {
              <div class="bg-slate-900/80 border rounded-2xl p-4 space-y-3 transition"
                   [ngClass]="task.status === 'AwaitingApproval' ? 'border-amber-500/50 bg-amber-950/20' : 'border-slate-800'">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-3">
                    <span class="text-xs font-semibold px-2.5 py-1 rounded-xl bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                      {{ task.role }}
                    </span>
                    <span class="font-bold text-sm text-white">{{ task.title }}</span>
                  </div>

                  <span class="text-xs px-2 py-0.5 rounded font-bold"
                        [ngClass]="task.status === 'Completed' ? 'bg-emerald-500/20 text-emerald-300' : (task.status === 'AwaitingApproval' ? 'bg-amber-500/20 text-amber-300' : 'bg-slate-800 text-slate-400')">
                    {{ task.status }}
                  </span>
                </div>

                <div class="text-xs font-mono text-slate-300 bg-slate-950/80 p-3 rounded-xl border border-slate-900">
                  <span class="text-slate-500">Artifact:</span> {{ task.output }}
                </div>

                @if (task.status === 'AwaitingApproval') {
                  <div class="flex items-center justify-between pt-2 border-t border-amber-500/20">
                    <div class="text-xs text-amber-300 flex items-center gap-1.5 font-medium">
                      <span>⚠️</span>
                      <span>Admin authorization required for live ad spend & outreach blast</span>
                    </div>

                    <div class="flex items-center gap-2">
                      <button (click)="approveTask(task.id, true)"
                              class="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold rounded-xl shadow-md transition">
                        Authorize Execution ✓
                      </button>
                      <button (click)="approveTask(task.id, false)"
                              class="px-3 py-1.5 bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 text-xs font-bold rounded-xl border border-rose-500/30 transition">
                        Reject ✕
                      </button>
                    </div>
                  </div>
                }
              </div>
            }
          </div>
        </div>

        <!-- Shared Scratchpad Working Memory (1 Col) -->
        <div class="space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">Shared Scratchpad Memory</h2>
            <span class="text-xs text-indigo-400 font-mono">Real-Time State</span>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 space-y-3">
            @for (entry of scratchpadEntries(); track entry.key) {
              <div class="p-3 bg-slate-950 rounded-xl border border-slate-800/80 space-y-1">
                <div class="text-[11px] font-mono font-bold text-slate-400 uppercase tracking-wider">{{ entry.key }}</div>
                <div class="text-xs font-medium text-white">{{ entry.val }}</div>
              </div>
            }

            <div class="pt-2">
              <button (click)="addMemoryNote()"
                      class="w-full py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-xl border border-slate-700 transition">
                + Append Memory Context 🧠
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class SwarmOrchestrationCockpitComponent {
  translation: TranslationService;
  private cdr?: ChangeDetectorRef;

  activeMission = signal<MissionItem>({
    id: 'mis_001',
    title: 'Enterprise Q4 DACH Market Expansion',
    goal: 'Acquire 25 mid-market and enterprise accounts in Germany, Austria, and Switzerland.',
    status: 'Active',
    scratchpad: {
      'mission_phase': 'AwaitingHumanAuthorization',
      'target_accounts': '42 Tier-A Enterprises identified ($50M+ ARR)',
      'angles_tested': 'ROAS Decay vs. Autonomous SDR Workforce',
      'budget_cap': '$2,500/day Meta & Google CAPI'
    },
    tasks: [
      { id: 't1', role: 'LeadResearcher', title: 'Target Account ICP Mining', status: 'Completed', requiresHitl: false, output: 'Harvested 42 enterprise accounts with verified buying committee.' },
      { id: 't2', role: 'OutreachCopywriter', title: 'Hyper-Personalized Spintax Generation', status: 'Completed', requiresHitl: false, output: 'Generated 3 localized German & English cold outreach angles.' },
      { id: 't3', role: 'TechnicalSeoAuditor', title: 'Competitor SERP & Content Cluster Audit', status: 'Completed', requiresHitl: false, output: 'Identified 3 high-intent comparison search terms.' },
      { id: 't4', role: 'ComplianceGuardian', title: 'GDPR & CAN-SPAM Opt-Out Validation', status: 'Completed', requiresHitl: false, output: '100% compliant: Opt-out tokens verified, zero PII leakage.' },
      { id: 't5', role: 'AdOptimizer', title: 'Allocate $2,500 Daily Ad Spend & Dispatch Blast', status: 'AwaitingApproval', requiresHitl: true, output: 'PROPOSED: $1,500 Meta CAPI + $1,000 Google Ads + 500 invitations.' }
    ]
  });

  pendingApprovalTasks = computed(() => {
    return this.activeMission().tasks.filter(t => t.status === 'AwaitingApproval');
  });

  completedTasksCount = computed(() => {
    return this.activeMission().tasks.filter(t => t.status === 'Completed').length;
  });

  scratchpadEntries = computed(() => {
    const mem = this.activeMission().scratchpad;
    return Object.entries(mem).map(([key, val]) => ({ key, val }));
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

  approveTask(taskId: string, approved: boolean): void {
    const mission = this.activeMission();
    const updatedTasks = mission.tasks.map(t => {
      if (t.id === taskId) {
        return {
          ...t,
          status: (approved ? 'Completed' : 'Rejected') as SwarmTaskItem['status'],
          output: approved ? t.output + ' [AUTHORIZED BY ADMIN]' : 'REJECTED: Budget exceeds monthly cap.'
        };
      }
      return t;
    });

    const allFinished = updatedTasks.every(t => t.status === 'Completed' || t.status === 'Rejected');

    this.activeMission.set({
      ...mission,
      status: allFinished ? 'Completed' : 'Active',
      tasks: updatedTasks,
      scratchpad: {
        ...mission.scratchpad,
        'mission_phase': allFinished ? 'MissionObjectiveAchieved' : 'InExecution'
      }
    });
    this.cdr?.markForCheck();
  }

  launchNewMission(): void {
    const newMission: MissionItem = {
      id: 'mis_' + Math.random().toString(36).substring(2, 6),
      title: 'Real-Time Black Friday Viral Blitz',
      goal: 'Scale DTC affiliate activations and dynamic promo landing pages.',
      status: 'Active',
      scratchpad: {
        'mission_phase': 'ActiveSwarmExecution',
        'target_cluster': 'Black Friday SaaS Deals',
        'affiliate_tier': 'Top 100 Creators'
      },
      tasks: [
        { id: 'nt1', role: 'LeadResearcher', title: 'Creator Discovery', status: 'Completed', requiresHitl: false, output: 'Indexed 120 verified tech influencers.' },
        { id: 'nt2', role: 'OutreachCopywriter', title: 'Affiliate Pitch Sequences', status: 'Completed', requiresHitl: false, output: 'Commission structure drafted with 30% revenue share.' }
      ]
    };

    this.activeMission.set(newMission);
    this.cdr?.markForCheck();
  }

  addMemoryNote(): void {
    const mission = this.activeMission();
    this.activeMission.set({
      ...mission,
      scratchpad: {
        ...mission.scratchpad,
        ['note_' + Date.now()]: 'Live sync with CRM pipeline verified.'
      }
    });
    this.cdr?.markForCheck();
  }
}
