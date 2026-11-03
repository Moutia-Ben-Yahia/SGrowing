import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface PlanMilestoneItem {
  milestoneId: string;
  title: string;
  ownerRole: string;
  targetDueDate: string;
  status: 'Pending' | 'InProgress' | 'Blocked' | 'CompletedVerified';
  isCriticalPath: boolean;
  artifactUrl?: string;
}

export interface MutualActionPlanRecord {
  planId: string;
  dealId: string;
  accountName: string;
  contractValueUsd: number;
  championHealthScore: number;
  milestones: PlanMilestoneItem[];
  targetCloseDate: string;
  isAtRiskOfSlipping: boolean;
  slippageRiskRationale: string;
}

@Component({
  selector: 'app-mutual-action-plan-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="mutual-action-plan-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-violet-500/20 text-violet-400 border border-violet-500/30">Horizon 81</span>
            <span class="text-xs text-slate-400">Enterprise Deal Governance</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous AI Sales Champion Enablement & MAP Mesh</h1>
          <p class="text-sm text-slate-400">Collaborative buyer procurement milestones, champion health monitoring, and automated deal slippage rescue.</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="triggerSlippageRescue()" data-testid="trigger-rescue-btn" class="px-4 py-2 bg-violet-600 hover:bg-violet-500 text-white rounded font-medium text-sm transition-colors shadow">
            Run Slippage Rescue Playbook
          </button>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-champion-health">
          <div class="text-xs font-medium text-slate-400 uppercase">Champion Health Score</div>
          <div class="text-2xl font-bold mt-1" [ngClass]="plan().championHealthScore > 80 ? 'text-emerald-400' : 'text-amber-400'">
            {{ plan().championHealthScore }} / 100
          </div>
          <div class="text-xs text-slate-400 mt-1">Internal buyer champion advocacy</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-completion-rate">
          <div class="text-xs font-medium text-slate-400 uppercase">Milestones Verified</div>
          <div class="text-2xl font-bold text-white mt-1">{{ completedCount() }} / {{ plan().milestones.length }}</div>
          <div class="text-xs text-slate-400 mt-1">{{ completionPercentage() }}% Procurement Progress</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-deal-acv">
          <div class="text-xs font-medium text-slate-400 uppercase">Opportunity Contract Value</div>
          <div class="text-2xl font-bold text-violet-400 mt-1">\${{ plan().contractValueUsd | number }}</div>
          <div class="text-xs text-slate-400 mt-1">Account: {{ plan().accountName }}</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-risk-status">
          <div class="text-xs font-medium text-slate-400 uppercase">Schedule Risk Status</div>
          <div class="text-2xl font-bold mt-1" [ngClass]="plan().isAtRiskOfSlipping ? 'text-rose-400' : 'text-emerald-400'">
            {{ plan().isAtRiskOfSlipping ? 'Slippage Alert' : 'On Schedule' }}
          </div>
          <div class="text-xs text-slate-400 mt-1 truncate">{{ plan().slippageRiskRationale }}</div>
        </div>
      </section>

      <!-- Milestones Timeline -->
      <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5 mb-6">
        <h2 class="text-base font-semibold text-white mb-4">Mutual Procurement Milestones & Buyer Verification Timeline</h2>
        <div class="space-y-4">
          @for (m of plan().milestones; track m.milestoneId) {
            <div class="bg-slate-900/60 border border-slate-700/50 p-4 rounded-lg flex flex-col md:flex-row md:items-center justify-between gap-4" [attr.data-testid]="'milestone-card-' + m.milestoneId">
              <div class="space-y-1">
                <div class="flex items-center gap-2">
                  <span class="text-sm font-semibold text-white">{{ m.title }}</span>
                  @if (m.isCriticalPath) {
                    <span class="px-2 py-0.5 text-xs rounded bg-rose-500/20 text-rose-300 border border-rose-500/30 font-medium">Critical Path</span>
                  }
                </div>
                <div class="text-xs text-slate-400">
                  Owner: <span class="text-slate-300 font-medium">{{ m.ownerRole }}</span> | Due: <span class="text-slate-300 font-medium">{{ m.targetDueDate | date:'mediumDate' }}</span>
                </div>
                @if (m.artifactUrl) {
                  <div class="text-xs text-indigo-400">Artifact: <a [href]="m.artifactUrl" target="_blank" class="underline">{{ m.artifactUrl }}</a></div>
                }
              </div>

              <div class="flex items-center gap-3">
                <span [ngClass]="{
                  'bg-emerald-500/20 text-emerald-400 border-emerald-500/30': m.status === 'CompletedVerified',
                  'bg-blue-500/20 text-blue-300 border-blue-500/30': m.status === 'InProgress',
                  'bg-rose-500/20 text-rose-300 border-rose-500/30': m.status === 'Blocked',
                  'bg-slate-700 text-slate-400 border-slate-600': m.status === 'Pending'
                }" class="px-2.5 py-1 text-xs rounded border font-medium">
                  {{ m.status }}
                </span>

                @if (m.status !== 'CompletedVerified') {
                  <button (click)="markMilestoneComplete(m)" [attr.data-testid]="'complete-ms-btn-' + m.milestoneId" class="text-xs px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-medium transition-colors">
                    Verify Complete
                  </button>
                  <button (click)="markMilestoneBlocked(m)" [attr.data-testid]="'block-ms-btn-' + m.milestoneId" class="text-xs px-3 py-1.5 bg-rose-600/30 hover:bg-rose-600/50 text-rose-300 border border-rose-500/40 rounded font-medium transition-colors">
                    Report Blocker
                  </button>
                }
              </div>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class MutualActionPlanCockpitComponent {
  plan = signal<MutualActionPlanRecord>({
    planId: 'map_enterprise_aurora_01',
    dealId: 'deal_aurora_9901',
    accountName: 'Aurora Enterprise Cloud',
    contractValueUsd: 185000,
    championHealthScore: 88,
    targetCloseDate: '2026-11-30T18:00:00Z',
    isAtRiskOfSlipping: false,
    slippageRiskRationale: 'All critical path milestones tracking on schedule.',
    milestones: [
      {
        milestoneId: 'ms_01',
        title: 'InfoSec Architecture & SOC2 Type II Sign-off',
        ownerRole: 'BuyerSecurityLead',
        targetDueDate: '2026-11-10T18:00:00Z',
        status: 'CompletedVerified',
        isCriticalPath: true,
        artifactUrl: 'https://trust.sgrowing.internal/audits/soc2.pdf'
      },
      {
        milestoneId: 'ms_02',
        title: 'Mutual NDA & Master Services Agreement (MSA) Legal Review',
        ownerRole: 'BuyerLegalCounsel',
        targetDueDate: '2026-11-17T18:00:00Z',
        status: 'InProgress',
        isCriticalPath: true
      },
      {
        milestoneId: 'ms_03',
        title: 'Staging Environment Sandbox Pilot Validation',
        ownerRole: 'BuyerLeadEngineer',
        targetDueDate: '2026-11-24T18:00:00Z',
        status: 'Pending',
        isCriticalPath: false
      },
      {
        milestoneId: 'ms_04',
        title: 'Executive Steering Committee Final Budget Approval',
        ownerRole: 'BuyerCfoChampion',
        targetDueDate: '2026-11-28T18:00:00Z',
        status: 'Pending',
        isCriticalPath: true
      }
    ]
  });

  completedCount = computed(() => this.plan().milestones.filter(m => m.status === 'CompletedVerified').length);

  completionPercentage = computed(() => {
    const total = this.plan().milestones.length;
    if (total === 0) return 0;
    return Math.round((this.completedCount() / total) * 100);
  });

  markMilestoneComplete(milestone: PlanMilestoneItem) {
    this.plan.update(p => {
      const updated = p.milestones.map(m => {
        if (m.milestoneId === milestone.milestoneId) {
          return { ...m, status: 'CompletedVerified' as const };
        }
        return m;
      });
      const allDone = updated.every(m => m.status === 'CompletedVerified');
      return {
        ...p,
        milestones: updated,
        championHealthScore: allDone ? 100 : Math.min(95, p.championHealthScore + 5),
        isAtRiskOfSlipping: false,
        slippageRiskRationale: 'Milestones progressing on schedule.'
      };
    });
  }

  markMilestoneBlocked(milestone: PlanMilestoneItem) {
    this.plan.update(p => ({
      ...p,
      milestones: p.milestones.map(m => {
        if (m.milestoneId === milestone.milestoneId) {
          return { ...m, status: 'Blocked' as const };
        }
        return m;
      }),
      isAtRiskOfSlipping: true,
      slippageRiskRationale: `Critical path milestone ${milestone.title} is blocked by buyer stakeholders.`,
      championHealthScore: Math.max(30, p.championHealthScore - 25)
    }));
  }

  triggerSlippageRescue() {
    this.plan.update(p => ({
      ...p,
      milestones: p.milestones.map(m => {
        if (m.status === 'Blocked') {
          return { ...m, status: 'InProgress' as const };
        }
        return m;
      }),
      isAtRiskOfSlipping: false,
      slippageRiskRationale: 'Automated executive outreach dispatched to buyer counsel; blocker cleared.',
      championHealthScore: 85
    }));
  }
}
