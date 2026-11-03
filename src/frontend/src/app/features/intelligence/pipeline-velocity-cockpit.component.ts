import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface StageVelocity {
  stageId: string;
  stageName: string;
  dealsCount: number;
  averageDwellDays: number;
  benchmarkDays: number;
  stageDropoffRate: number;
  isBottleneck: boolean;
}

@Component({
  selector: 'app-pipeline-velocity-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="pipeline-velocity-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-amber-500/20 text-amber-400 border border-amber-500/30">Horizon 91</span>
            <span class="text-xs text-slate-400">Sales Operations & Velocity Forecasting</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Sales Pipeline Velocity & Bottleneck Forecasting</h1>
          <p class="text-sm text-slate-400">Real-time dwell time analysis, stage decay curve modeling, and prescriptive cycle acceleration.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-daily-velocity">
          <div class="text-xs font-medium text-slate-400 uppercase">Daily Pipeline Velocity</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">\${{ dailyVelocity() | number:'1.0-0' }} / day</div>
          <div class="text-xs text-slate-400 mt-1">Calculated ARR through funnel per 24h</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-deals">
          <div class="text-xs font-medium text-slate-400 uppercase">Active Enterprise Deals</div>
          <div class="text-2xl font-bold text-white mt-1">{{ totalDeals() }} Deals</div>
          <div class="text-xs text-slate-400 mt-1">\$3,850,000 Pipeline Value</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-sales-cycle">
          <div class="text-xs font-medium text-slate-400 uppercase">Average Sales Cycle</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ totalCycleDays() | number:'1.1-1' }} Days</div>
          <div class="text-xs text-slate-400 mt-1">Total funnel dwell duration</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-bottlenecks-count">
          <div class="text-xs font-medium text-slate-400 uppercase">Stage Bottlenecks</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ bottleneckCount() }} Flagged</div>
          <div class="text-xs text-slate-400 mt-1">Exceeding benchmark dwell by >20%</div>
        </div>
      </section>

      <!-- Stages Velocity Grid -->
      <section class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div class="lg:col-span-2 space-y-4">
          <h2 class="text-lg font-semibold text-white">Funnel Stage Dwell & Velocity Breakdown</h2>
          <div class="space-y-3">
            @for (stg of stages(); track stg.stageId) {
              <div
                class="bg-slate-800/60 border border-slate-700 p-4 rounded-xl hover:border-slate-600 transition-all cursor-pointer"
                [class.ring-2]="selectedStage()?.stageId === stg.stageId"
                [class.ring-amber-500]="selectedStage()?.stageId === stg.stageId"
                (click)="selectStage(stg)"
                data-testid="stage-velocity-card"
              >
                <div class="flex items-center justify-between">
                  <div>
                    <h3 class="font-semibold text-white text-base">{{ stg.stageName }}</h3>
                    <p class="text-xs text-slate-400 mt-0.5">{{ stg.dealsCount }} active deals &bull; Drop-off rate: {{ stg.stageDropoffRate }}%</p>
                  </div>
                  <div class="text-right">
                    @if (stg.isBottleneck) {
                      <span class="text-xs px-2.5 py-1 rounded bg-rose-500/20 text-rose-400 border border-rose-500/30 font-medium" data-testid="bottleneck-flag">
                        Bottleneck
                      </span>
                    } @else {
                      <span class="text-xs px-2.5 py-1 rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 font-medium" data-testid="healthy-stage-flag">
                        Healthy Flow
                      </span>
                    }
                    <div class="text-xs text-slate-300 mt-1">Dwell: {{ stg.averageDwellDays }}d (Benchmark: {{ stg.benchmarkDays }}d)</div>
                  </div>
                </div>

                <div class="mt-4 flex items-center justify-between pt-2 border-t border-slate-700/60">
                  <div class="text-xs text-slate-400">
                    Cycle Delta: <span class="font-mono text-amber-300">+{{ (stg.averageDwellDays - stg.benchmarkDays) | number:'1.1-1' }} days vs SLA</span>
                  </div>

                  @if (stg.isBottleneck) {
                    <button
                      (click)="accelerateStage(stg.stageId); $event.stopPropagation()"
                      data-testid="btn-accelerate-stage"
                      class="px-3 py-1 bg-amber-600 hover:bg-amber-500 text-white text-xs font-semibold rounded shadow transition-colors"
                    >
                      Accelerate Stage
                    </button>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Detail Sidebar -->
        <div class="bg-slate-800/60 border border-slate-700 p-5 rounded-xl flex flex-col justify-between">
          <div>
            <h2 class="text-lg font-semibold text-white mb-3">Stage Intervention Intelligence</h2>
            @if (selectedStage(); as sel) {
              <div class="space-y-4">
                <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-700/60">
                  <div class="text-xs text-slate-400 uppercase">Selected Funnel Stage</div>
                  <div class="text-base font-bold text-white mt-0.5">{{ sel.stageName }}</div>
                  <div class="text-xs text-amber-400 mt-1">Stage ID: {{ sel.stageId }}</div>
                </div>

                <div class="space-y-2 text-sm">
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Average Stage Dwell:</span>
                    <span class="font-mono text-white">{{ sel.averageDwellDays }} days</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Target Benchmark:</span>
                    <span class="font-mono text-emerald-400">{{ sel.benchmarkDays }} days</span>
                  </div>
                  <div class="flex justify-between py-1 border-b border-slate-700/50">
                    <span class="text-slate-400">Deals at Risk:</span>
                    <span class="font-mono text-rose-300">{{ sel.dealsCount }} deals</span>
                  </div>
                </div>

                @if (sel.isBottleneck) {
                  <button
                    (click)="accelerateStage(sel.stageId)"
                    class="w-full mt-4 py-2 bg-gradient-to-r from-amber-600 to-orange-600 hover:from-amber-500 hover:to-orange-500 text-white font-semibold rounded text-sm shadow transition-all"
                  >
                    Execute Automated Stage Playbook
                  </button>
                }
              </div>
            } @else {
              <p class="text-xs text-slate-400">Select a stage to view bottleneck telemetry.</p>
            }
          </div>

          <div class="mt-6 p-3 bg-slate-900/80 rounded border border-slate-800 text-xs text-slate-400">
            <span class="text-amber-400 font-semibold">Prescriptive Playbooks:</span> Automatically schedules mutual action plans and fast-tracks security audits.
          </div>
        </div>
      </section>
    </div>
  `
})
export class PipelineVelocityCockpitComponent {
  stages = signal<StageVelocity[]>([
    {
      stageId: 'stg_discovery',
      stageName: 'Qualification & Discovery',
      dealsCount: 45,
      averageDwellDays: 12.0,
      benchmarkDays: 10.0,
      stageDropoffRate: 18.5,
      isBottleneck: false
    },
    {
      stageId: 'stg_demo',
      stageName: 'Technical Deep Dive & Demo',
      dealsCount: 32,
      averageDwellDays: 16.5,
      benchmarkDays: 12.0,
      stageDropoffRate: 22.0,
      isBottleneck: true
    },
    {
      stageId: 'stg_security',
      stageName: 'InfoSec & Compliance Review',
      dealsCount: 24,
      averageDwellDays: 28.0,
      benchmarkDays: 14.0,
      stageDropoffRate: 12.0,
      isBottleneck: true
    },
    {
      stageId: 'stg_procurement',
      stageName: 'Procurement & Legal Redlines',
      dealsCount: 18,
      averageDwellDays: 24.5,
      benchmarkDays: 15.0,
      stageDropoffRate: 8.0,
      isBottleneck: true
    },
    {
      stageId: 'stg_closing',
      stageName: 'Executive Sign-off & Closing',
      dealsCount: 14,
      averageDwellDays: 7.5,
      benchmarkDays: 7.0,
      stageDropoffRate: 4.0,
      isBottleneck: false
    }
  ]);

  selectedStage = signal<StageVelocity | null>(this.stages()[2]);

  totalDeals = computed(() =>
    this.stages().reduce((sum, s) => sum + s.dealsCount, 0)
  );

  totalCycleDays = computed(() =>
    this.stages().reduce((sum, s) => sum + s.averageDwellDays, 0)
  );

  bottleneckCount = computed(() =>
    this.stages().filter(s => s.isBottleneck).length
  );

  dailyVelocity = computed(() => {
    const cycle = this.totalCycleDays();
    if (cycle <= 0) return 0;
    // Estimated ARR won per day
    const estimatedWon = 3850000 * 0.285;
    return estimatedWon / cycle;
  });

  selectStage(stage: StageVelocity): void {
    this.selectedStage.set(stage);
  }

  accelerateStage(stageId: string): void {
    this.stages.update(list =>
      list.map(s => {
        if (s.stageId === stageId) {
          const reducedDays = Math.max(s.benchmarkDays, s.averageDwellDays - 7.0);
          return {
            ...s,
            averageDwellDays: reducedDays,
            isBottleneck: false
          };
        }
        return s;
      })
    );

    if (this.selectedStage()?.stageId === stageId) {
      this.selectedStage.update(curr => {
        if (!curr) return null;
        const reducedDays = Math.max(curr.benchmarkDays, curr.averageDwellDays - 7.0);
        return {
          ...curr,
          averageDwellDays: reducedDays,
          isBottleneck: false
        };
      });
    }
  }
}
