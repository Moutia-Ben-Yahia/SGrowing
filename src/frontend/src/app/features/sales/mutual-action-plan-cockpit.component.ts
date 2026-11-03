import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface MutualActionPlanRecord {
  planId: string;
  accountName: string;
  opportunityId: string;
  dealValueUsd: number;
  executiveBuyerChampion: string;
  targetGoLiveDate: string;
  totalMilestonesCount: number;
  completedMilestonesCount: number;
  status: string;
}

export interface PlanMilestoneRecord {
  milestoneId: string;
  planId: string;
  title: string;
  ownerType: string;
  ownerName: string;
  dueDate: string;
  isCompleted: boolean;
}

@Component({
  selector: 'app-mutual-action-plan-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="mutual-action-plan-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
            B2B Multi-Stakeholder Mutual Action Plan (MAP) Workspace
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Collaborative Executive Deal Rooms, Cryptographic Dual-Signoff & Milestone Slip Prevention Mesh
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="completeMilestone()" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-medium rounded-lg shadow-sm transition">
            Dual-Sign Milestone Approval
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Active MAP Deals</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ plans().length }}</p>
          <span class="text-xs text-emerald-400">Collaborative Deal Rooms</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Influenced Pipeline</span>
          <p class="text-2xl font-extrabold text-indigo-400 mt-1">$730K</p>
          <span class="text-xs text-slate-400">High-Velocity Enterprise</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Milestone Velocity</span>
          <p class="text-2xl font-extrabold text-cyan-400 mt-1">{{ milestoneCompletionRatePct() }}%</p>
          <span class="text-xs text-slate-400">On-Track Milestones</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">On-Time Close Rate</span>
          <p class="text-2xl font-extrabold text-emerald-400 mt-1">92.4%</p>
          <span class="text-xs text-emerald-400">-38 Days Slippage Reduction</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Plans List -->
        <section class="bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Enterprise Deal Rooms</h2>
          <div class="space-y-3">
            <div *ngFor="let p of plans()" class="p-3 bg-slate-900/60 border border-slate-700/40 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-medium text-slate-200 text-sm">{{ p.accountName }}</span>
                <span class="px-2 py-0.5 text-xs bg-emerald-500/20 text-emerald-300 rounded font-semibold font-mono">\${{ p.dealValueUsd.toLocaleString() }}</span>
              </div>
              <p class="text-xs text-slate-400 mb-1">Champion: {{ p.executiveBuyerChampion }}</p>
              <div class="flex justify-between items-center text-[10px] text-slate-400">
                <span>Progress: {{ p.completedMilestonesCount }}/{{ p.totalMilestonesCount }}</span>
                <span class="text-emerald-400 font-semibold">{{ p.status }}</span>
              </div>
            </div>
          </div>
        </section>

        <!-- Milestones Table -->
        <section class="lg:col-span-2 bg-slate-800/40 border border-slate-700/50 rounded-xl p-5">
          <h2 class="text-lg font-semibold text-white mb-4">Mutual Action Milestones</h2>
          <div class="overflow-x-auto">
            <table class="w-full text-left border-collapse text-sm">
              <thead>
                <tr class="border-b border-slate-700 text-slate-400 text-xs">
                  <th class="pb-3 font-medium">Milestone Title</th>
                  <th class="pb-3 font-medium">Owner Type</th>
                  <th class="pb-3 font-medium">Assigned Lead</th>
                  <th class="pb-3 font-medium">Due Date</th>
                  <th class="pb-3 font-medium">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                <tr *ngFor="let m of milestones()" class="hover:bg-slate-800/50 transition">
                  <td class="py-3 font-medium text-slate-200">{{ m.title }}</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-xs rounded font-semibold font-mono"
                          [ngClass]="m.ownerType === 'MUTUAL' ? 'bg-indigo-500/20 text-indigo-300' : 'bg-slate-700 text-slate-300'">
                      {{ m.ownerType }}
                    </span>
                  </td>
                  <td class="py-3 text-slate-400 text-xs">{{ m.ownerName }}</td>
                  <td class="py-3 text-slate-400 font-mono text-xs">{{ m.dueDate }}</td>
                  <td class="py-3">
                    <span class="px-2 py-0.5 text-xs rounded font-semibold font-mono"
                          [ngClass]="m.isCompleted ? 'bg-emerald-500/20 text-emerald-300' : 'bg-amber-500/20 text-amber-300'">
                      {{ m.isCompleted ? 'SIGNED_OFF' : 'IN_PROGRESS' }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  `
})
export class MutualActionPlanCockpitComponent {
  plans = signal<MutualActionPlanRecord[]>([
    { planId: 'MAP-CORP-01', accountName: 'Acme Worldwide Financial Technologies', opportunityId: 'OPP-FIN-9988', dealValueUsd: 450000, executiveBuyerChampion: 'David Chen (Head of Infrastructure)', targetGoLiveDate: '2026-12-15', totalMilestonesCount: 5, completedMilestonesCount: 3, status: 'ON_TRACK_TO_CLOSE' },
    { planId: 'MAP-CORP-02', accountName: 'OmniHealth Life Sciences Group', opportunityId: 'OPP-HLT-4411', dealValueUsd: 280000, executiveBuyerChampion: 'Dr. Aris Thorne (CMO)', targetGoLiveDate: '2026-11-30', totalMilestonesCount: 4, completedMilestonesCount: 2, status: 'ON_TRACK_TO_CLOSE' }
  ]);

  milestones = signal<PlanMilestoneRecord[]>([
    { milestoneId: 'MLS-01', planId: 'MAP-CORP-01', title: 'Dual Architecture Security & SOC2 Type II Review', ownerType: 'MUTUAL', ownerName: 'David Chen & Security SecOps Lead', dueDate: '2026-10-24', isCompleted: true },
    { milestoneId: 'MLS-02', planId: 'MAP-CORP-01', title: 'Enterprise Procurement MSA Redlines', ownerType: 'BUYER', ownerName: 'Legal Counsel (Acme Worldwide)', dueDate: '2026-11-15', isCompleted: false }
  ]);

  milestoneCompletionRatePct = signal(75.0);

  completeMilestone() {
    const newMilestone: PlanMilestoneRecord = {
      milestoneId: `MLS-${Date.now().toString().slice(-4)}`,
      planId: 'MAP-CORP-02',
      title: 'Final Executive Board Budget Signoff',
      ownerType: 'BUYER',
      ownerName: 'Dr. Aris Thorne (CMO)',
      dueDate: '2026-11-20',
      isCompleted: true
    };
    this.milestones.update(list => [newMilestone, ...list]);
  }
}
