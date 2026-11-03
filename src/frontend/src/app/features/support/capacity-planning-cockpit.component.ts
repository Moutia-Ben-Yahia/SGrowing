import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface HourlyIntervalRecord {
  hourOfDay: number;
  expectedIncomingVolume: number;
  requiredStaffHeadcount: number;
  scheduledStaffHeadcount: number;
  predictedSlaAttainmentPct: number;
  hasStaffingDeficit: boolean;
}

export interface CapacityPlanRecord {
  planId: string;
  queueName: string;
  totalDailyVolume: number;
  aiDeflectionRatePct: number;
  hourlyIntervals: HourlyIntervalRecord[];
}

@Component({
  selector: 'app-capacity-planning-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="capacity-planning-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-sky-500/20 text-sky-400 border border-sky-500/30">Horizon 83</span>
            <span class="text-xs text-slate-400">Support Workforce Optimization</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Workforce Capacity Planning & Shift Scheduling Mesh</h1>
          <p class="text-sm text-slate-400">Erlang-C staffing calculations, predictive queue surge forecasting, and autonomous AI shift leveling.</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="simulateSurge()" data-testid="simulate-surge-btn" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded font-medium text-sm transition-colors">
            Simulate 40% Volume Surge
          </button>
          <button (click)="rebalanceShifts()" data-testid="rebalance-shifts-btn" class="px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded font-medium text-sm transition-colors shadow">
            Auto-Level Staffing Deficits
          </button>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-deficit-hours">
          <div class="text-xs font-medium text-slate-400 uppercase">Staffing Deficit Windows</div>
          <div class="text-2xl font-bold mt-1" [ngClass]="deficitCount() > 0 ? 'text-rose-400' : 'text-emerald-400'">
            {{ deficitCount() }} Hours
          </div>
          <div class="text-xs text-slate-400 mt-1">{{ deficitCount() > 0 ? 'SLA breach vulnerability' : 'Full Erlang-C staffing parity' }}</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-daily-volume">
          <div class="text-xs font-medium text-slate-400 uppercase">Forecasted Inbound Volume</div>
          <div class="text-2xl font-bold text-white mt-1">{{ totalVolume() | number }} Tickets</div>
          <div class="text-xs text-slate-400 mt-1">Across 24 hour global intervals</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-ai-deflection">
          <div class="text-xs font-medium text-slate-400 uppercase">Autonomous AI Deflection</div>
          <div class="text-2xl font-bold text-sky-400 mt-1">{{ plan().aiDeflectionRatePct }}%</div>
          <div class="text-xs text-slate-400 mt-1">Resolved without human escalation</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-avg-sla">
          <div class="text-xs font-medium text-slate-400 uppercase">Projected SLA Attainment</div>
          <div class="text-2xl font-bold mt-1" [ngClass]="avgSla() > 95 ? 'text-emerald-400' : 'text-amber-400'">
            {{ avgSla() }}%
          </div>
          <div class="text-xs text-slate-400 mt-1">Target contract threshold: 95.0%</div>
        </div>
      </section>

      <!-- Hourly Shifts Table -->
      <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-base font-semibold text-white">24-Hour Queue Erlang-C Intervals & Agent Allocations</h2>
          <span class="text-xs text-slate-400">Queue: {{ plan().queueName }}</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="intervals-table">
            <thead class="text-xs text-slate-400 uppercase border-b border-slate-700/60 bg-slate-800/40">
              <tr>
                <th class="p-3">Hour (UTC)</th>
                <th class="p-3">Expected Volume</th>
                <th class="p-3">Required Agents</th>
                <th class="p-3">Scheduled Agents</th>
                <th class="p-3">Projected SLA %</th>
                <th class="p-3">Deficit Status</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-700/40">
              @for (int of plan().hourlyIntervals; track int.hourOfDay) {
                <tr class="hover:bg-slate-700/20" [attr.data-testid]="'interval-row-' + int.hourOfDay">
                  <td class="p-3 font-mono text-xs text-slate-300">{{ int.hourOfDay }}:00</td>
                  <td class="p-3 font-medium text-white">{{ int.expectedIncomingVolume }}</td>
                  <td class="p-3 text-slate-300">{{ int.requiredStaffHeadcount }}</td>
                  <td class="p-3 font-bold" [ngClass]="int.hasStaffingDeficit ? 'text-rose-400' : 'text-slate-200'">
                    {{ int.scheduledStaffHeadcount }}
                  </td>
                  <td class="p-3 font-mono text-xs" [ngClass]="int.predictedSlaAttainmentPct < 90 ? 'text-rose-400' : 'text-emerald-400'">
                    {{ int.predictedSlaAttainmentPct }}%
                  </td>
                  <td class="p-3">
                    <span [ngClass]="{
                      'bg-rose-500/20 text-rose-300 border-rose-500/30': int.hasStaffingDeficit,
                      'bg-emerald-500/20 text-emerald-400 border-emerald-500/30': !int.hasStaffingDeficit
                    }" class="px-2 py-0.5 text-xs rounded border font-medium">
                      {{ int.hasStaffingDeficit ? 'Deficit' : 'Optimal' }}
                    </span>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class CapacityPlanningCockpitComponent {
  plan = signal<CapacityPlanRecord>({
    planId: 'cap_plan_global_support',
    queueName: 'Global Omnichannel Tier-1 Support',
    totalDailyVolume: 3240,
    aiDeflectionRatePct: 45.0,
    hourlyIntervals: [
      { hourOfDay: 8, expectedIncomingVolume: 90, requiredStaffHeadcount: 6, scheduledStaffHeadcount: 6, predictedSlaAttainmentPct: 98.2, hasStaffingDeficit: false },
      { hourOfDay: 9, expectedIncomingVolume: 220, requiredStaffHeadcount: 15, scheduledStaffHeadcount: 15, predictedSlaAttainmentPct: 97.5, hasStaffingDeficit: false },
      { hourOfDay: 14, expectedIncomingVolume: 250, requiredStaffHeadcount: 17, scheduledStaffHeadcount: 13, predictedSlaAttainmentPct: 82.5, hasStaffingDeficit: true },
      { hourOfDay: 18, expectedIncomingVolume: 160, requiredStaffHeadcount: 11, scheduledStaffHeadcount: 11, predictedSlaAttainmentPct: 98.0, hasStaffingDeficit: false }
    ]
  });

  totalVolume = computed(() => this.plan().hourlyIntervals.reduce((sum, i) => sum + i.expectedIncomingVolume, 0));
  deficitCount = computed(() => this.plan().hourlyIntervals.filter(i => i.hasStaffingDeficit).length);

  avgSla = computed(() => {
    const list = this.plan().hourlyIntervals;
    if (list.length === 0) return 0;
    const sum = list.reduce((acc, i) => acc + i.predictedSlaAttainmentPct, 0);
    return Math.round((sum / list.length) * 10) / 10;
  });

  rebalanceShifts() {
    this.plan.update(p => ({
      ...p,
      hourlyIntervals: p.hourlyIntervals.map(i => {
        if (i.hasStaffingDeficit) {
          return {
            ...i,
            scheduledStaffHeadcount: i.requiredStaffHeadcount,
            hasStaffingDeficit: false,
            predictedSlaAttainmentPct: 98.5
          };
        }
        return i;
      })
    }));
  }

  simulateSurge() {
    this.plan.update(p => ({
      ...p,
      hourlyIntervals: p.hourlyIntervals.map(i => {
        const surgedVol = Math.round(i.expectedIncomingVolume * 1.4);
        const newReq = Math.ceil(surgedVol / 14);
        const deficit = i.scheduledStaffHeadcount < newReq;
        return {
          ...i,
          expectedIncomingVolume: surgedVol,
          requiredStaffHeadcount: newReq,
          hasStaffingDeficit: deficit,
          predictedSlaAttainmentPct: deficit ? 78.4 : 96.0
        };
      })
    }));
  }
}
