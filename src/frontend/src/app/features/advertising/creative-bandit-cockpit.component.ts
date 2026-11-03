import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CreativeVariantView {
  variantId: string;
  angle: string;
  headline: string;
  theme: string;
  cta: string;
  impressions: number;
  conversions: number;
  ctr: number;
  conversionRate: number;
  allocatedShare: number; // %
  isFatigued: boolean;
}

@Component({
  selector: 'app-creative-bandit-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="creative-bandit-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-rose-400">
            <span class="inline-block w-2 h-2 rounded-full bg-rose-400 animate-pulse"></span>
            Ad Optimization & Thompson Sampling • Horizon 67
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Multimodal Ad Creative Variance Bandit
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Dynamic impression share allocation via Beta distribution sampling & automated creative fatigue circuit breakers.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="mutateWinningCreative()"
            data-testid="mutate-creative-btn"
            class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-rose-600/20"
          >
            <span>🧬</span> Synthesize AI Mutant
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Active Creative Variants</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-variants-count">
            {{ variants().length }} Variants
          </div>
          <div class="text-rose-400 text-xs mt-1">Campaign: camp_growth_q1</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Total Conversions</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-total-conversions">
            {{ totalConversions() }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Blended CVR: {{ blendedCvr() }}%</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Top Allocation Share</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-top-share">
            {{ topAllocatedShare() }}%
          </div>
          <div class="text-emerald-400 text-xs mt-1">Thompson Sampling Lead</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Fatigued Creatives</div>
          <div class="text-2xl font-bold text-amber-400 mt-1" data-testid="stat-fatigued-count">
            {{ fatiguedCount() }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Auto-Phased Out by Circuit Breaker</div>
        </div>
      </div>

      <!-- Variants Table -->
      <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
        <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
          <h2 class="text-lg font-bold text-white">Creative Matrix & Real-Time Traffic Split</h2>
          <span class="text-xs text-slate-400 font-mono">Thompson Sampling Beta(α, β)</span>
        </div>

        <div class="divide-y divide-slate-700/40">
          @for (v of variants(); track v.variantId) {
            <div class="p-4 hover:bg-slate-700/30 transition-colors flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div class="space-y-1 max-w-xl">
                <div class="flex items-center gap-2">
                  <span class="font-bold text-white text-sm">{{ v.headline }}</span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-rose-950 text-rose-300 border border-rose-800">
                    {{ v.angle }}
                  </span>
                  @if (v.isFatigued) {
                    <span class="px-1.5 py-0.5 rounded text-[10px] font-semibold bg-amber-950 text-amber-300 border border-amber-800">
                      Fatigued
                    </span>
                  }
                </div>
                <div class="text-xs text-slate-400">
                  Theme: <strong class="text-slate-200">{{ v.theme }}</strong> • CTA: <span class="font-mono text-cyan-300">{{ v.cta }}</span>
                </div>
                <div class="text-xs text-slate-400">
                  Impressions: <strong class="text-white">{{ v.impressions | number }}</strong> • Conversions: <strong class="text-emerald-400">{{ v.conversions }}</strong> (CVR: {{ v.conversionRate * 100 | number:'1.2-2' }}%)
                </div>
              </div>

              <div class="flex items-center gap-4 min-w-[200px]">
                <div class="flex-1">
                  <div class="flex justify-between text-xs mb-1">
                    <span class="text-slate-400">Traffic Allocation:</span>
                    <span class="font-bold text-white font-mono">{{ v.allocatedShare }}%</span>
                  </div>
                  <div class="w-full bg-slate-700 rounded-full h-2 overflow-hidden">
                    <div
                      class="h-full rounded-full transition-all duration-500"
                      [class]="v.isFatigued ? 'bg-amber-500' : 'bg-emerald-500'"
                      [style.width.%]="v.allocatedShare"
                    ></div>
                  </div>
                </div>
              </div>
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class CreativeBanditCockpitComponent {
  readonly variants = signal<CreativeVariantView[]>([
    {
      variantId: 'var_101',
      angle: 'PainPointAgitation',
      headline: 'Stop Burning Ad Spend on Unqualified Traffic',
      theme: 'HighContrastCyber',
      cta: 'Deploy Free Autopilot',
      impressions: 12500,
      conversions: 312,
      ctr: 0.038,
      conversionRate: 0.025,
      allocatedShare: 45.0,
      isFatigued: false
    },
    {
      variantId: 'var_102',
      angle: 'DirectOutcomeTransformation',
      headline: 'Scale Your ARR 3.2x in 90 Days Without New SDRs',
      theme: 'MinimalistDark',
      cta: 'Claim Live Demo',
      impressions: 9800,
      conversions: 265,
      ctr: 0.042,
      conversionRate: 0.027,
      allocatedShare: 35.0,
      isFatigued: false
    },
    {
      variantId: 'var_103',
      angle: 'SocialProofCelebrity',
      headline: 'Why Top YC & Series B Founders Switched to SGrowing',
      theme: 'EnterpriseCleanLight',
      cta: 'Read Customer Case Studies',
      impressions: 5200,
      conversions: 78,
      ctr: 0.021,
      conversionRate: 0.015,
      allocatedShare: 20.0,
      isFatigued: true
    }
  ]);

  readonly totalConversions = computed(() =>
    this.variants().reduce((acc, v) => acc + v.conversions, 0)
  );

  readonly fatiguedCount = computed(() =>
    this.variants().filter(v => v.isFatigued).length
  );

  readonly topAllocatedShare = computed(() => {
    const shares = this.variants().map(v => v.allocatedShare);
    return shares.length > 0 ? Math.max(...shares) : 0;
  });

  readonly blendedCvr = computed(() => {
    const totalImp = this.variants().reduce((acc, v) => acc + v.impressions, 0);
    const totalConv = this.totalConversions();
    return totalImp > 0 ? ((totalConv / totalImp) * 100).toFixed(2) : '0.00';
  });

  mutateWinningCreative(): void {
    const mutant: CreativeVariantView = {
      variantId: `mutant_${Date.now()}`,
      angle: 'DirectOutcomeTransformation',
      headline: '[AI-Evolved] Scale Your Pipeline 3.5x with Autonomous Swarms',
      theme: 'CinematicGraded',
      cta: 'Deploy Live Pilot',
      impressions: 500,
      conversions: 22,
      ctr: 0.048,
      conversionRate: 0.044,
      allocatedShare: 25.0,
      isFatigued: false
    };

    this.variants.update(list => [mutant, ...list]);
  }
}
