import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface RetargetingSegment {
  segmentId: string;
  name: string;
  intentLevel: 'VeryHigh' | 'High' | 'Medium';
  audienceSize: number;
  dailyFrequencyCap: number;
  weeklyFrequencyCap: number;
  activeChannels: string[];
  fatigueIndexPct: number;
  cpaTargetUsd: number;
  isPaused: boolean;
}

@Component({
  selector: 'app-retargeting-matrix-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="retargeting-matrix-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">Horizon 77</span>
            <span class="text-xs text-slate-400">Programmatic Audience Matrix</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous B2B Retargeting & Frequency Capping Mesh</h1>
          <p class="text-sm text-slate-400">Dynamic cross-channel audience orchestration, ad fatigue shielding, and negative buyer exclusion.</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="syncNegativeExclusions()" data-testid="sync-negative-exclusions-btn" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded font-medium text-sm transition-colors">
            Sync Negative Exclusions
          </button>
          <button (click)="optimizeAllFrequencyCaps()" data-testid="optimize-caps-btn" class="px-4 py-2 bg-cyan-600 hover:bg-cyan-500 text-white rounded font-medium text-sm transition-colors shadow">
            Auto-Tune Frequency Caps
          </button>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-audience">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Retargeting Pool</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ totalAudience() | number }}</div>
          <div class="text-xs text-slate-400 mt-1">Across {{ segments().length }} Matrix Segments</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-avg-fatigue">
          <div class="text-xs font-medium text-slate-400 uppercase">Mesh Fatigue Index</div>
          <div class="text-2xl font-bold" [ngClass]="avgFatigue() > 40 ? 'text-rose-400' : 'text-emerald-400'" mt-1>
            {{ avgFatigue() }}%
          </div>
          <div class="text-xs text-slate-400 mt-1">Ad saturation threshold: 65%</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-active-channels">
          <div class="text-xs font-medium text-slate-400 uppercase">Orchestrated DSPs</div>
          <div class="text-2xl font-bold text-white mt-1">4 Networks</div>
          <div class="text-xs text-slate-400 mt-1">LinkedIn, Google, Meta, The Trade Desk</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-negative-exclusions">
          <div class="text-xs font-medium text-slate-400 uppercase">Negative Exclusions Suppressed</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ negativeExclusionCount() | number }}</div>
          <div class="text-xs text-slate-400 mt-1">Converted customers & opt-outs</div>
        </div>
      </section>

      <!-- Segments Table -->
      <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-base font-semibold text-white">Retargeting Audience Matrices & Frequency Guardrails</h2>
          <span class="text-xs text-slate-400">Status: {{ activeSegmentCount() }} Active / {{ pausedSegmentCount() }} Paused</span>
        </div>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="segments-table">
            <thead class="text-xs text-slate-400 uppercase border-b border-slate-700/60 bg-slate-800/40">
              <tr>
                <th class="p-3">Segment Name</th>
                <th class="p-3">Intent Tier</th>
                <th class="p-3">Audience Size</th>
                <th class="p-3">Daily / Wk Cap</th>
                <th class="p-3">Fatigue %</th>
                <th class="p-3">Channels</th>
                <th class="p-3">Status</th>
                <th class="p-3">Controls</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-700/40">
              @for (seg of segments(); track seg.segmentId) {
                <tr class="hover:bg-slate-700/20" [attr.data-testid]="'segment-row-' + seg.segmentId">
                  <td class="p-3 font-medium text-white">{{ seg.name }}</td>
                  <td class="p-3">
                    <span [ngClass]="{
                      'bg-rose-500/20 text-rose-300 border-rose-500/30': seg.intentLevel === 'VeryHigh',
                      'bg-amber-500/20 text-amber-300 border-amber-500/30': seg.intentLevel === 'High',
                      'bg-slate-700/50 text-slate-300 border-slate-600': seg.intentLevel === 'Medium'
                    }" class="px-2 py-0.5 text-xs rounded border font-medium">
                      {{ seg.intentLevel }}
                    </span>
                  </td>
                  <td class="p-3 text-slate-300">{{ seg.audienceSize | number }}</td>
                  <td class="p-3 text-slate-300">{{ seg.dailyFrequencyCap }} / {{ seg.weeklyFrequencyCap }} impressions</td>
                  <td class="p-3">
                    <div class="flex items-center gap-2">
                      <div class="w-16 bg-slate-700 rounded-full h-1.5 overflow-hidden">
                        <div class="h-full rounded-full" [ngClass]="seg.fatigueIndexPct > 50 ? 'bg-rose-500' : 'bg-cyan-500'" [style.width.%]="seg.fatigueIndexPct"></div>
                      </div>
                      <span class="text-xs text-slate-300">{{ seg.fatigueIndexPct }}%</span>
                    </div>
                  </td>
                  <td class="p-3 text-xs text-slate-400">{{ seg.activeChannels.join(', ') }}</td>
                  <td class="p-3">
                    <span [class.bg-emerald-500/20]="!seg.isPaused" [class.text-emerald-400]="!seg.isPaused" [class.bg-slate-700]="seg.isPaused" [class.text-slate-400]="seg.isPaused" class="px-2 py-0.5 text-xs rounded font-medium">
                      {{ seg.isPaused ? 'Paused' : 'Active' }}
                    </span>
                  </td>
                  <td class="p-3">
                    <button (click)="togglePause(seg)" [attr.data-testid]="'toggle-pause-btn-' + seg.segmentId" class="text-xs px-2.5 py-1 rounded border border-slate-700 bg-slate-800 hover:bg-slate-700 text-slate-200">
                      {{ seg.isPaused ? 'Resume' : 'Pause' }}
                    </button>
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
export class RetargetingMatrixCockpitComponent {
  segments = signal<RetargetingSegment[]>([
    {
      segmentId: 'seg_demo_dropoff',
      name: 'B2B Demo Request Abandoners (7d)',
      intentLevel: 'VeryHigh',
      audienceSize: 14200,
      dailyFrequencyCap: 4,
      weeklyFrequencyCap: 16,
      activeChannels: ['LinkedIn', 'Google Display'],
      fatigueIndexPct: 32,
      cpaTargetUsd: 110,
      isPaused: false
    },
    {
      segmentId: 'seg_pricing_viewers',
      name: 'High-Velocity Pricing Page Visitors (14d)',
      intentLevel: 'High',
      audienceSize: 38400,
      dailyFrequencyCap: 3,
      weeklyFrequencyCap: 12,
      activeChannels: ['LinkedIn', 'Meta', 'TradeDesk'],
      fatigueIndexPct: 44,
      cpaTargetUsd: 85,
      isPaused: false
    },
    {
      segmentId: 'seg_content_downloaders',
      name: 'Gated Whitepaper / Benchmark Readers (30d)',
      intentLevel: 'Medium',
      audienceSize: 92000,
      dailyFrequencyCap: 2,
      weeklyFrequencyCap: 8,
      activeChannels: ['LinkedIn', 'Meta'],
      fatigueIndexPct: 18,
      cpaTargetUsd: 45,
      isPaused: false
    }
  ]);

  negativeExclusionCount = signal<number>(18420);

  totalAudience = computed(() => this.segments().reduce((sum, s) => sum + s.audienceSize, 0));
  activeSegmentCount = computed(() => this.segments().filter(s => !s.isPaused).length);
  pausedSegmentCount = computed(() => this.segments().filter(s => s.isPaused).length);

  avgFatigue = computed(() => {
    const total = this.segments().length;
    if (total === 0) return 0;
    const sum = this.segments().reduce((acc, s) => acc + s.fatigueIndexPct, 0);
    return Math.round(sum / total);
  });

  togglePause(segment: RetargetingSegment) {
    this.segments.update(list => list.map(s => {
      if (s.segmentId === segment.segmentId) {
        return { ...s, isPaused: !s.isPaused };
      }
      return s;
    }));
  }

  optimizeAllFrequencyCaps() {
    this.segments.update(list => list.map(s => ({
      ...s,
      dailyFrequencyCap: Math.max(1, s.dailyFrequencyCap - 1),
      weeklyFrequencyCap: Math.max(4, s.weeklyFrequencyCap - 2),
      fatigueIndexPct: Math.max(10, s.fatigueIndexPct - 8)
    })));
  }

  syncNegativeExclusions() {
    this.negativeExclusionCount.update(count => count + 350);
  }
}
