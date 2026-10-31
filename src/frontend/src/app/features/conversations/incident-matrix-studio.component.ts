import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface IncidentItem {
  id: string;
  title: string;
  severity: 'P1_Critical' | 'P2_High' | 'P3_Medium' | 'P4_Low';
  status: 'Triggered' | 'Acknowledged' | 'Investigating' | 'Mitigated' | 'Resolved';
  assignedAgent: string;
  currentTier: string;
  ackDeadlineSeconds: number;
  triggeredAt: string;
}

export interface OnCallShift {
  name: string;
  primary: string;
  secondary: string;
  rotation: string;
  status: 'Active' | 'Upcoming';
}

@Component({
  selector: 'app-incident-matrix-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8 pb-6 border-b border-slate-800">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-rose-500/10 text-rose-400 rounded-lg border border-rose-500/20 text-xl font-bold">🚨</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">On-Call Incident & Paging Matrix</h1>
              <p class="text-slate-400 text-sm">Enterprise SLA breach escalation, automated on-call paging & incident response lifecycle</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="runSweep()" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-sm font-medium border border-slate-700 transition">
            ⚡ Evaluate Escalation Sweep
          </button>
          <button (click)="openTriggerModal()" class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded-lg text-sm font-medium shadow-lg shadow-rose-900/30 transition">
            + Trigger Incident
          </button>
        </div>
      </div>

      <!-- KPI Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Active Incidents</div>
          <div class="text-3xl font-extrabold text-rose-400">{{ activeCount() }}</div>
          <div class="text-xs text-rose-500/80 mt-1">Requires immediate triage</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">P1 Critical Alert</div>
          <div class="text-3xl font-extrabold text-amber-400">{{ p1Count() }}</div>
          <div class="text-xs text-amber-500/80 mt-1">15m ACK window active</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Mean Time to Ack (MTTA)</div>
          <div class="text-3xl font-extrabold text-cyan-400">4.2m</div>
          <div class="text-xs text-emerald-400 mt-1">↓ 28% vs SLA target</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Mean Time to Resolve (MTTR)</div>
          <div class="text-3xl font-extrabold text-emerald-400">22.8m</div>
          <div class="text-xs text-emerald-400 mt-1">99.4% SLA adherence</div>
        </div>
      </div>

      <!-- Main Layout -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Incidents List -->
        <div class="lg:col-span-2 bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-white">Live Incident Triage Board</h2>
            <div class="flex gap-2">
              <button *ngFor="let tab of ['All', 'Triggered', 'Acknowledged', 'Resolved']"
                (click)="selectedTab.set(tab)"
                [class.bg-slate-700]="selectedTab() === tab"
                [class.text-white]="selectedTab() === tab"
                class="px-3 py-1 text-xs rounded-md text-slate-400 hover:text-slate-200 transition">
                {{ tab }}
              </button>
            </div>
          </div>

          <div class="space-y-3">
            <div *ngFor="let inc of filteredIncidents()" class="p-4 bg-slate-800/80 border border-slate-700 rounded-lg hover:border-slate-600 transition">
              <div class="flex items-start justify-between gap-4">
                <div class="flex-1">
                  <div class="flex items-center gap-2 mb-1">
                    <span [class]="getSeverityBadge(inc.severity)" class="px-2 py-0.5 rounded text-xs font-bold uppercase">
                      {{ inc.severity }}
                    </span>
                    <span [class]="getStatusBadge(inc.status)" class="px-2 py-0.5 rounded text-xs font-medium">
                      {{ inc.status }}
                    </span>
                    <span class="text-xs text-slate-400">{{ inc.currentTier }}</span>
                  </div>
                  <h3 class="text-base font-semibold text-white">{{ inc.title }}</h3>
                  <div class="flex items-center gap-4 text-xs text-slate-400 mt-2">
                    <span>👤 Assigned: <strong class="text-slate-200">{{ inc.assignedAgent }}</strong></span>
                    <span>⏱️ Triggered: {{ inc.triggeredAt }}</span>
                    <span *ngIf="inc.status === 'Triggered'" class="text-rose-400 font-semibold animate-pulse">
                      ⏳ ACK Deadline: {{ inc.ackDeadlineSeconds }}s remaining
                    </span>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  <button *ngIf="inc.status === 'Triggered'" (click)="acknowledge(inc)" class="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-medium transition">
                    Acknowledge
                  </button>
                  <button *ngIf="inc.status === 'Acknowledged'" (click)="resolve(inc)" class="px-3 py-1.5 bg-cyan-600 hover:bg-cyan-500 text-white rounded text-xs font-medium transition">
                    Resolve
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- On-Call Rosters & Schedule -->
        <div class="space-y-6">
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-4">Active On-Call Roster</h2>
            <div class="space-y-3">
              <div *ngFor="let shift of rosters()" class="p-3 bg-slate-800/70 border border-slate-700/60 rounded-lg">
                <div class="flex justify-between items-center mb-1">
                  <span class="text-sm font-semibold text-white">{{ shift.name }}</span>
                  <span class="text-xs px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-medium">
                    {{ shift.status }}
                  </span>
                </div>
                <div class="text-xs text-slate-400 space-y-0.5">
                  <div>Primary: <span class="text-slate-200 font-medium">{{ shift.primary }}</span></div>
                  <div>Secondary: <span class="text-slate-200 font-medium">{{ shift.secondary }}</span></div>
                  <div class="text-slate-500 text-[11px] pt-1">Rotation: {{ shift.rotation }}</div>
                </div>
              </div>
            </div>
          </div>

          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-3">Escalation Policy Flow</h2>
            <div class="space-y-2 text-xs">
              <div class="flex items-center gap-2 p-2 rounded bg-slate-800/60 border border-slate-700/40">
                <span class="w-6 h-6 rounded-full bg-slate-700 text-slate-200 flex items-center justify-center font-bold">1</span>
                <div>
                  <div class="font-medium text-slate-200">Tier 1: Primary On-Call</div>
                  <div class="text-slate-400">Paging via In-App Banner & Webhook (0 - 15m)</div>
                </div>
              </div>
              <div class="flex items-center gap-2 p-2 rounded bg-slate-800/60 border border-slate-700/40">
                <span class="w-6 h-6 rounded-full bg-amber-500/20 text-amber-300 flex items-center justify-center font-bold">2</span>
                <div>
                  <div class="font-medium text-amber-200">Tier 2: On-Call Team Lead</div>
                  <div class="text-slate-400">Escalates via SMS & High-Priority Page (15 - 30m)</div>
                </div>
              </div>
              <div class="flex items-center gap-2 p-2 rounded bg-slate-800/60 border border-slate-700/40">
                <span class="w-6 h-6 rounded-full bg-rose-500/20 text-rose-300 flex items-center justify-center font-bold">3</span>
                <div>
                  <div class="font-medium text-rose-200">Tier 3: Executive & Opsgenie</div>
                  <div class="text-slate-400">Synthetic Voice Call & External Bridge (> 30m)</div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class IncidentMatrixStudioComponent {
  selectedTab = signal<string>('All');

  incidents = signal<IncidentItem[]>([
    {
      id: 'inc-101',
      title: 'WhatsApp Gateway Rate-Limit Breach (P1)',
      severity: 'P1_Critical',
      status: 'Triggered',
      assignedAgent: 'Sami Ben Ali',
      currentTier: 'Tier 1 (Assigned Agent)',
      ackDeadlineSeconds: 340,
      triggeredAt: '4m ago'
    },
    {
      id: 'inc-102',
      title: 'Checkout Payment Gateway 504 Gateway Timeout',
      severity: 'P2_High',
      status: 'Acknowledged',
      assignedAgent: 'Elena Rostova',
      currentTier: 'Tier 1 (Assigned Agent)',
      ackDeadlineSeconds: 0,
      triggeredAt: '18m ago'
    },
    {
      id: 'inc-103',
      title: 'Courier Webhook Latency > 5000ms',
      severity: 'P3_Medium',
      status: 'Resolved',
      assignedAgent: 'Karim Mansour',
      currentTier: 'Tier 1',
      ackDeadlineSeconds: 0,
      triggeredAt: '1h ago'
    }
  ]);

  rosters = signal<OnCallShift[]>([
    { name: 'EMEA Enterprise Support', primary: 'Sami Ben Ali', secondary: 'Karim Mansour', rotation: 'Daily (08:00 - 18:00 UTC)', status: 'Active' },
    { name: 'Global Night Vigil', primary: 'Elena Rostova', secondary: 'Marc Vance', rotation: 'Daily (18:00 - 08:00 UTC)', status: 'Upcoming' }
  ]);

  activeCount = computed(() => this.incidents().filter(i => i.status !== 'Resolved').length);
  p1Count = computed(() => this.incidents().filter(i => i.severity === 'P1_Critical' && i.status !== 'Resolved').length);

  filteredIncidents = computed(() => {
    const tab = this.selectedTab();
    if (tab === 'All') return this.incidents();
    return this.incidents().filter(i => i.status === tab);
  });

  getSeverityBadge(sev: string): string {
    switch (sev) {
      case 'P1_Critical': return 'bg-rose-500/20 text-rose-400 border border-rose-500/30';
      case 'P2_High': return 'bg-amber-500/20 text-amber-400 border border-amber-500/30';
      case 'P3_Medium': return 'bg-blue-500/20 text-blue-400 border border-blue-500/30';
      default: return 'bg-slate-700 text-slate-300';
    }
  }

  getStatusBadge(st: string): string {
    switch (st) {
      case 'Triggered': return 'bg-rose-600 text-white font-bold animate-pulse';
      case 'Acknowledged': return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
      case 'Resolved': return 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
      default: return 'bg-slate-700 text-slate-300';
    }
  }

  acknowledge(inc: IncidentItem): void {
    this.incidents.update(items =>
      items.map(item => item.id === inc.id ? { ...item, status: 'Acknowledged', ackDeadlineSeconds: 0 } : item)
    );
  }

  resolve(inc: IncidentItem): void {
    this.incidents.update(items =>
      items.map(item => item.id === inc.id ? { ...item, status: 'Resolved' } : item)
    );
  }

  runSweep(): void {
    // Demo sweep action
  }

  openTriggerModal(): void {
    const newInc: IncidentItem = {
      id: `inc-${Date.now().toString().slice(-3)}`,
      title: 'Database Replica Sync Latency Spillover',
      severity: 'P1_Critical',
      status: 'Triggered',
      assignedAgent: 'Sami Ben Ali',
      currentTier: 'Tier 1 (Assigned Agent)',
      ackDeadlineSeconds: 900,
      triggeredAt: 'Just now'
    };
    this.incidents.update(items => [newInc, ...items]);
  }
}
