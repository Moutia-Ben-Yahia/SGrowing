import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ShiftRow {
  id: string;
  agentName: string;
  channel: 'WhatsApp' | 'Voice' | 'Email' | 'Omnichannel';
  shiftType: 'Morning' | 'Afternoon' | 'Evening' | 'Graveyard';
  timeSlot: string;
  status: 'Scheduled' | 'CheckedIn' | 'Completed' | 'LateArrival';
}

export interface SwapRequestRow {
  id: string;
  requester: string;
  target: string;
  shifts: string;
  reason: string;
}

@Component({
  selector: 'app-shift-scheduling-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8 pb-6 border-b border-slate-800">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-teal-500/10 text-teal-400 rounded-lg border border-teal-500/20 text-xl font-bold">⏱️</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Workforce Shift Scheduling & Capacity Mesh</h1>
              <p class="text-slate-400 text-sm">Gauzy-inspired omnichannel shift rosters, conflict detection, punch clock & peer swap marketplace</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="openScheduleModal()" class="px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white rounded-lg text-sm font-medium shadow-lg shadow-teal-900/30 transition">
            + Schedule Shift
          </button>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">On-Duty Agents</div>
          <div class="text-3xl font-extrabold text-teal-400">{{ onDutyCount() }}</div>
          <div class="text-xs text-teal-400 mt-1">Currently clocked in</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Shift Adherence</div>
          <div class="text-3xl font-extrabold text-emerald-400">96.8%</div>
          <div class="text-xs text-emerald-400 mt-1">Punctuality SLA met</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Coverage Gap</div>
          <div class="text-3xl font-extrabold text-amber-400">0</div>
          <div class="text-xs text-slate-400 mt-1">No understaffed hours today</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Pending Swap Requests</div>
          <div class="text-3xl font-extrabold text-purple-400">{{ swaps().length }}</div>
          <div class="text-xs text-purple-400 mt-1">Awaiting manager approval</div>
        </div>
      </div>

      <!-- Live Timeclock Widget -->
      <div class="bg-gradient-to-r from-teal-900/30 to-slate-800/60 border border-teal-500/30 rounded-xl p-6 mb-8 flex flex-col md:flex-row items-center justify-between gap-4">
        <div>
          <span class="text-xs font-bold uppercase tracking-wider text-teal-400">Agent Timeclock Station</span>
          <h2 class="text-lg font-bold text-white mt-1">Sami Ben Ali (Active Shift: EMEA Morning WhatsApp)</h2>
          <p class="text-xs text-slate-400">Scheduled: 08:00 - 16:00 UTC • Rest period verified (14h rest elapsed)</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="clockIn()" class="px-5 py-2.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-semibold shadow-md transition">
            🟢 Punch Clock In
          </button>
          <button (click)="clockOut()" class="px-5 py-2.5 bg-slate-700 hover:bg-slate-600 text-slate-200 rounded-lg text-sm font-semibold transition">
            🔴 Clock Out
          </button>
        </div>
      </div>

      <!-- Main Columns -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Daily Shift Roster Grid -->
        <div class="lg:col-span-2 bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-white">Today's Omnichannel Shift Roster</h2>
            <span class="text-xs text-slate-400">Channel quotas verified</span>
          </div>

          <div class="space-y-3">
            <div *ngFor="let s of shifts()" class="p-4 bg-slate-800/80 border border-slate-700 rounded-lg hover:border-slate-600 transition flex items-center justify-between gap-4">
              <div>
                <div class="flex items-center gap-2 mb-1">
                  <span class="text-xs font-bold text-white">{{ s.agentName }}</span>
                  <span [class]="getChannelBadge(s.channel)" class="text-xs px-2 py-0.5 rounded font-medium">
                    {{ s.channel }}
                  </span>
                  <span [class]="getStatusBadge(s.status)" class="text-xs px-2 py-0.5 rounded font-medium">
                    {{ s.status }}
                  </span>
                </div>
                <div class="text-xs text-slate-400">{{ s.shiftType }} Shift • {{ s.timeSlot }}</div>
              </div>

              <div class="flex items-center gap-2">
                <button (click)="requestSwap(s)" class="px-3 py-1.5 bg-slate-700 hover:bg-slate-600 text-slate-200 rounded text-xs transition">
                  Request Swap
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- Shift Swap Approvals & Conflict Guard -->
        <div class="space-y-6">
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-3">Pending Shift Swap Approvals</h2>
            <div class="space-y-3">
              <div *ngFor="let swap of swaps()" class="p-3 bg-slate-800/70 border border-slate-700/60 rounded-lg">
                <div class="text-sm font-semibold text-white">{{ swap.requester }} ⇄ {{ swap.target }}</div>
                <div class="text-xs text-slate-400 mt-1">{{ swap.shifts }}</div>
                <div class="text-xs text-amber-300/90 mt-1">Reason: "{{ swap.reason }}"</div>
                <div class="flex gap-2 mt-3">
                  <button (click)="approveSwap(swap.id)" class="flex-1 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-medium transition">
                    Approve Swap
                  </button>
                  <button (click)="rejectSwap(swap.id)" class="flex-1 py-1 bg-rose-600/80 hover:bg-rose-500 text-white rounded text-xs font-medium transition">
                    Reject
                  </button>
                </div>
              </div>
            </div>
          </div>

          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-2">Automated Conflict Guards</h2>
            <ul class="text-xs text-slate-400 space-y-1.5 list-disc list-inside">
              <li><strong class="text-slate-300">Overlapping Block:</strong> Rejects double-booking.</li>
              <li><strong class="text-slate-300">Rest Period:</strong> Requires ≥11h between shifts.</li>
              <li><strong class="text-slate-300">Overtime Cap:</strong> Limits rolling weekly hours to 48h.</li>
              <li><strong class="text-slate-300">Punctuality:</strong> Flags >10m check-in as LateArrival.</li>
            </ul>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ShiftSchedulingStudioComponent {
  shifts = signal<ShiftRow[]>([
    { id: 'sh-1', agentName: 'Sami Ben Ali', channel: 'WhatsApp', shiftType: 'Morning', timeSlot: '08:00 - 16:00 UTC', status: 'CheckedIn' },
    { id: 'sh-2', agentName: 'Elena Rostova', channel: 'Voice', shiftType: 'Morning', timeSlot: '08:00 - 16:00 UTC', status: 'CheckedIn' },
    { id: 'sh-3', agentName: 'Karim Mansour', channel: 'Email', shiftType: 'Afternoon', timeSlot: '16:00 - 00:00 UTC', status: 'Scheduled' },
    { id: 'sh-4', agentName: 'Marc Vance', channel: 'Omnichannel', shiftType: 'Graveyard', timeSlot: '00:00 - 08:00 UTC', status: 'Completed' }
  ]);

  swaps = signal<SwapRequestRow[]>([
    {
      id: 'sw-1',
      requester: 'Karim Mansour',
      target: 'Sami Ben Ali',
      shifts: 'Friday Afternoon ⇄ Saturday Morning',
      reason: 'Attending family medical appointment'
    }
  ]);

  onDutyCount = computed(() => this.shifts().filter(s => s.status === 'CheckedIn').length);

  getChannelBadge(ch: string): string {
    switch (ch) {
      case 'WhatsApp': return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      case 'Voice': return 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/30';
      case 'Email': return 'bg-indigo-500/20 text-indigo-300 border border-indigo-500/30';
      default: return 'bg-slate-700 text-slate-300';
    }
  }

  getStatusBadge(st: string): string {
    switch (st) {
      case 'CheckedIn': return 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
      case 'Scheduled': return 'bg-blue-500/20 text-blue-300 border border-blue-500/30';
      case 'LateArrival': return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
      case 'Completed': return 'bg-slate-700 text-slate-400';
      default: return 'bg-slate-700 text-slate-300';
    }
  }

  clockIn(): void {
    // Demonstration clock in
  }

  clockOut(): void {
    // Demonstration clock out
  }

  requestSwap(s: ShiftRow): void {
    // Demonstration swap
  }

  approveSwap(id: string): void {
    this.swaps.update(items => items.filter(i => i.id !== id));
  }

  rejectSwap(id: string): void {
    this.swaps.update(items => items.filter(i => i.id !== id));
  }

  openScheduleModal(): void {
    // Demonstration schedule modal
  }
}
