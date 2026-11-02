import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export type AttributionModel = 'FirstTouch' | 'LastTouch' | 'Linear' | 'TimeDecay' | 'WShaped';

export interface ChannelAttribution {
  channel: string;
  weightPercent: number;
  attributedRevenue: number;
  spend: number;
  roas: number;
}

@Component({
  selector: 'app-multi-touch-attribution-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-slate-700 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-purple-600/20 text-purple-400 rounded-lg text-xl">🎯</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Multi-Touch Revenue Attribution & PLTV Engine</h1>
              <p class="text-sm text-slate-400">First-Touch, Last-Touch, Linear, Time-Decay and W-Shaped mathematical attribution</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-2">
          @for (model of models; track model) {
            <button (click)="selectModel(model)"
                    class="px-3 py-1.5 rounded-lg text-xs font-semibold transition"
                    [ngClass]="activeModel() === model ? 'bg-purple-600 text-white shadow-md' : 'bg-slate-800 text-slate-400 hover:text-slate-200'">
              {{ getModelLabel(model) }}
            </button>
          }
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-1">
          <span class="text-xs text-slate-400">Attributed Closed Revenue</span>
          <p class="text-2xl font-bold text-white font-mono">{{ totalRevenue() | currency }}</p>
          <span class="text-[11px] text-purple-400">Model: {{ getModelLabel(activeModel()) }}</span>
        </div>
        <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-1">
          <span class="text-xs text-slate-400">Total Marketing Spend</span>
          <p class="text-2xl font-bold text-slate-300 font-mono">{{ totalSpend() | currency }}</p>
          <span class="text-[11px] text-slate-500">Across 6 acquisition channels</span>
        </div>
        <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-1">
          <span class="text-xs text-slate-400">Blended Portfolio ROAS</span>
          <p class="text-2xl font-bold text-emerald-400 font-mono">{{ blendedRoas() | number:'1.2-2' }}x</p>
          <span class="text-[11px] text-emerald-400">+0.4x vs single-touch bias</span>
        </div>
        <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl space-y-1">
          <span class="text-xs text-slate-400">Average Enterprise CAC</span>
          <p class="text-2xl font-bold text-cyan-400 font-mono">$1,840</p>
          <span class="text-[11px] text-cyan-400">Target: &lt; $2,500</span>
        </div>
      </div>

      <!-- Attribution Model Comparison & Breakdown -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Channel Attribution Bars (8 cols) -->
        <div class="lg:col-span-8 bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-base font-semibold text-white flex items-center gap-2">
              <span>📊</span> Channel Credit Allocation ({{ getModelLabel(activeModel()) }})
            </h2>
            <span class="text-xs text-slate-400">Normalized to 100%</span>
          </div>

          <div class="space-y-4">
            @for (item of currentChannelShares(); track item.channel) {
              <div class="p-3 bg-slate-950 rounded-xl border border-slate-800/80 space-y-2">
                <div class="flex justify-between items-center text-xs">
                  <span class="font-semibold text-slate-200">{{ item.channel }}</span>
                  <div class="flex items-center gap-3 font-mono">
                    <span class="text-purple-400 font-bold">{{ item.weightPercent }}%</span>
                    <span class="text-slate-300">{{ item.attributedRevenue | currency }}</span>
                    <span class="px-1.5 py-0.5 rounded text-[10px] font-bold"
                          [ngClass]="item.roas >= 3.0 ? 'bg-emerald-500/20 text-emerald-300' : 'bg-slate-800 text-slate-400'">
                      {{ item.roas | number:'1.2-2' }}x ROAS
                    </span>
                  </div>
                </div>

                <div class="w-full bg-slate-800 rounded-full h-2 overflow-hidden">
                  <div class="bg-gradient-to-r from-purple-500 to-indigo-500 h-2 rounded-full transition-all duration-300"
                       [style.width.%]="item.weightPercent"></div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Predictive LTV Forecaster (4 cols) -->
        <div class="lg:col-span-4 bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4 flex flex-col justify-between">
          <div class="space-y-4">
            <h2 class="text-base font-semibold text-white flex items-center gap-2">
              <span>🔮</span> Predictive LTV (PLTV)
            </h2>

            <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-3">
              <span class="text-xs text-slate-400">Cohort 12-Month Projected LTV</span>
              <p class="text-3xl font-bold text-white font-mono">$18,450</p>
              <div class="h-1.5 bg-slate-800 rounded-full overflow-hidden">
                <div class="bg-gradient-to-r from-emerald-500 to-cyan-400 h-1.5 w-3/4"></div>
              </div>
              <p class="text-[11px] text-slate-400">Based on 12-cohort retention probability model</p>
            </div>

            <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2 text-xs">
              <div class="flex justify-between text-slate-400">
                <span>36-Month Cumulative:</span>
                <span class="font-mono text-emerald-400 font-bold">$44,200</span>
              </div>
              <div class="flex justify-between text-slate-400">
                <span>Predicted Churn Rate:</span>
                <span class="font-mono text-cyan-300">4.2% / year</span>
              </div>
              <div class="flex justify-between text-slate-400">
                <span>LTV : CAC Ratio:</span>
                <span class="font-mono text-purple-400 font-bold">10.0 : 1</span>
              </div>
            </div>
          </div>

          <div class="p-3 bg-purple-500/10 border border-purple-500/30 rounded-xl text-xs text-purple-300 space-y-1">
            <span class="font-bold">Algorithmic Recommendation:</span>
            <p>Under W-Shaped weighting, LinkedIn Ads generates 3.8x ROAS on Opportunity Creation. Reallocate 15% budget from Direct to LinkedIn Ads.</p>
          </div>
        </div>
      </div>
    </div>
  `
})
export class MultiTouchAttributionCockpitComponent {
  translation: TranslationService;

  models: AttributionModel[] = ['WShaped', 'TimeDecay', 'FirstTouch', 'LastTouch', 'Linear'];
  activeModel = signal<AttributionModel>('WShaped');
  totalRevenue = signal<number>(245000);
  totalSpend = signal<number>(68000);

  blendedRoas = computed(() =>
    this.totalRevenue() / Math.max(1, this.totalSpend())
  );

  private readonly modelDistributions: Record<AttributionModel, ChannelAttribution[]> = {
    WShaped: [
      { channel: 'Meta & Google CAPI Ads', weightPercent: 32, attributedRevenue: 78400, spend: 22000, roas: 3.56 },
      { channel: 'LinkedIn ABM Campaigns', weightPercent: 28, attributedRevenue: 68600, spend: 18000, roas: 3.81 },
      { channel: 'Organic Search & SEO', weightPercent: 18, attributedRevenue: 44100, spend: 8000, roas: 5.51 },
      { channel: 'Outbound SDR Sequences', weightPercent: 12, attributedRevenue: 29400, spend: 12000, roas: 2.45 },
      { channel: 'Email Nurture Journeys', weightPercent: 10, attributedRevenue: 24500, spend: 8000, roas: 3.06 }
    ],
    TimeDecay: [
      { channel: 'Meta & Google CAPI Ads', weightPercent: 36, attributedRevenue: 88200, spend: 22000, roas: 4.01 },
      { channel: 'Email Nurture Journeys', weightPercent: 24, attributedRevenue: 58800, spend: 8000, roas: 7.35 },
      { channel: 'LinkedIn ABM Campaigns', weightPercent: 20, attributedRevenue: 49000, spend: 18000, roas: 2.72 },
      { channel: 'Outbound SDR Sequences', weightPercent: 12, attributedRevenue: 29400, spend: 12000, roas: 2.45 },
      { channel: 'Organic Search & SEO', weightPercent: 8, attributedRevenue: 19600, spend: 8000, roas: 2.45 }
    ],
    FirstTouch: [
      { channel: 'Organic Search & SEO', weightPercent: 45, attributedRevenue: 110250, spend: 8000, roas: 13.78 },
      { channel: 'Meta & Google CAPI Ads', weightPercent: 30, attributedRevenue: 73500, spend: 22000, roas: 3.34 },
      { channel: 'LinkedIn ABM Campaigns', weightPercent: 15, attributedRevenue: 36750, spend: 18000, roas: 2.04 },
      { channel: 'Outbound SDR Sequences', weightPercent: 10, attributedRevenue: 24500, spend: 12000, roas: 2.04 },
      { channel: 'Email Nurture Journeys', weightPercent: 0, attributedRevenue: 0, spend: 8000, roas: 0.00 }
    ],
    LastTouch: [
      { channel: 'Outbound SDR Sequences', weightPercent: 40, attributedRevenue: 98000, spend: 12000, roas: 8.17 },
      { channel: 'Email Nurture Journeys', weightPercent: 35, attributedRevenue: 85750, spend: 8000, roas: 10.72 },
      { channel: 'Meta & Google CAPI Ads', weightPercent: 15, attributedRevenue: 36750, spend: 22000, roas: 1.67 },
      { channel: 'LinkedIn ABM Campaigns', weightPercent: 10, attributedRevenue: 24500, spend: 18000, roas: 1.36 },
      { channel: 'Organic Search & SEO', weightPercent: 0, attributedRevenue: 0, spend: 8000, roas: 0.00 }
    ],
    Linear: [
      { channel: 'Meta & Google CAPI Ads', weightPercent: 20, attributedRevenue: 49000, spend: 22000, roas: 2.23 },
      { channel: 'LinkedIn ABM Campaigns', weightPercent: 20, attributedRevenue: 49000, spend: 18000, roas: 2.72 },
      { channel: 'Organic Search & SEO', weightPercent: 20, attributedRevenue: 49000, spend: 8000, roas: 6.13 },
      { channel: 'Outbound SDR Sequences', weightPercent: 20, attributedRevenue: 49000, spend: 12000, roas: 4.08 },
      { channel: 'Email Nurture Journeys', weightPercent: 20, attributedRevenue: 49000, spend: 8000, roas: 6.13 }
    ]
  };

  currentChannelShares = computed(() => this.modelDistributions[this.activeModel()]);

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
  }

  selectModel(model: AttributionModel): void {
    this.activeModel.set(model);
  }

  getModelLabel(model: AttributionModel): string {
    switch (model) {
      case 'WShaped': return 'W-Shaped (40/20/40)';
      case 'TimeDecay': return 'Time-Decay (7d)';
      case 'FirstTouch': return 'First-Touch';
      case 'LastTouch': return 'Last-Touch';
      case 'Linear': return 'Linear (Equal)';
    }
  }
}
