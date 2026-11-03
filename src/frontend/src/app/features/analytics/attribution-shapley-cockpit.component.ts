import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ChannelScoreView {
  channel: string;
  firstTouchPct: number;
  lastTouchPct: number;
  linearPct: number;
  shapleyPct: number;
  revenue: number;
  action: string;
}

@Component({
  selector: 'app-attribution-shapley-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="attribution-shapley-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-violet-400">
            <span class="inline-block w-2 h-2 rounded-full bg-violet-400 animate-pulse"></span>
            Cooperative Game Theory & Attribution • Horizon 72
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Shapley Value Multi-Touch Pipeline Attribution
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            2^n coalition game theory revenue fairness modeling & algorithmic budget reallocation advisory.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="rebalanceMarketingBudget()"
            data-testid="rebalance-budget-btn"
            class="px-4 py-2 bg-violet-600 hover:bg-violet-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-violet-600/20"
          >
            <span>⚖️</span> Rebalance Budget to Shapley Optimum
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Closed Deal Value</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-deal-value">
            \${{ dealValue() | number:'1.0-0' }}
          </div>
          <div class="text-violet-400 text-xs mt-1">Customer: Apex Logistics Enterprise</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Top Shapley Driver</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-top-driver">
            Executive Demo (38.5%)
          </div>
          <div class="text-slate-400 text-xs mt-1">Highest Coalition Value Addition</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Last-Touch Distortion Delta</div>
          <div class="text-2xl font-bold text-rose-400 mt-1" data-testid="stat-distortion">
            +420% Over-Credited
          </div>
          <div class="text-slate-400 text-xs mt-1">Retargeting Paid Social Heuristic Bias</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Budget Optimization Shift</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-budget-shift">
            {{ budgetRebalanced() ? 'Optimized' : 'Rebalance Recommended' }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">+18.4% Blended Pipeline ROI</div>
        </div>
      </div>

      <!-- Attribution Models Comparison Table -->
      <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
        <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
          <h2 class="text-lg font-bold text-white">Multi-Touch Attribution Model Matrix</h2>
          <span class="text-xs text-slate-400 font-mono">First vs Last vs Linear vs Shapley Game Theory</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-xs">
            <thead class="bg-slate-900/60 text-slate-400 uppercase font-semibold border-b border-slate-700/60">
              <tr>
                <th class="p-3">Touchpoint Channel</th>
                <th class="p-3">First-Touch</th>
                <th class="p-3">Last-Touch</th>
                <th class="p-3">Linear Share</th>
                <th class="p-3">Shapley Fair Value</th>
                <th class="p-3">Attributed ARR</th>
                <th class="p-3">Advisory Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-700/40 text-slate-200">
              @for (row of channelScores(); track row.channel) {
                <tr class="hover:bg-slate-700/30 transition-colors">
                  <td class="p-3 font-bold text-white">{{ row.channel }}</td>
                  <td class="p-3 font-mono text-slate-400">{{ row.firstTouchPct }}%</td>
                  <td class="p-3 font-mono text-slate-400">{{ row.lastTouchPct }}%</td>
                  <td class="p-3 font-mono text-slate-400">{{ row.linearPct }}%</td>
                  <td class="p-3 font-mono font-bold text-violet-400">{{ row.shapleyPct }}%</td>
                  <td class="p-3 font-mono font-bold text-emerald-400">\${{ row.revenue | number:'1.0-0' }}</td>
                  <td class="p-3">
                    <span
                      class="px-2 py-0.5 rounded text-[10px] font-semibold"
                      [class]="row.action.includes('Increase') ? 'bg-emerald-950 text-emerald-300 border border-emerald-800' : (row.action.includes('Trim') ? 'bg-rose-950 text-rose-300 border border-rose-800' : 'bg-slate-800 text-slate-300')"
                    >
                      {{ row.action }}
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
export class AttributionShapleyCockpitComponent {
  readonly dealValue = signal(150000);
  readonly budgetRebalanced = signal(false);

  readonly channelScores = signal<ChannelScoreView[]>([
    { channel: 'PaidSearchAd', firstTouchPct: 100, lastTouchPct: 0, linearPct: 20, shapleyPct: 14.5, revenue: 21750, action: 'Maintain' },
    { channel: 'TechnicalWhitepaper', firstTouchPct: 0, lastTouchPct: 0, linearPct: 20, shapleyPct: 22.0, revenue: 33000, action: 'Increase (High Leverage Touch)' },
    { channel: 'WebinarAttendance', firstTouchPct: 0, lastTouchPct: 0, linearPct: 20, shapleyPct: 25.0, revenue: 37500, action: 'Increase (High Leverage Touch)' },
    { channel: 'ExecutiveDemo', firstTouchPct: 0, lastTouchPct: 0, linearPct: 20, shapleyPct: 38.5, revenue: 57750, action: 'Increase (High Leverage Touch)' },
    { channel: 'RetargetingSocialAd', firstTouchPct: 0, lastTouchPct: 100, linearPct: 20, shapleyPct: 0.0, revenue: 0, action: 'Trim (Over-Credited Heuristic)' }
  ]);

  rebalanceMarketingBudget(): void {
    this.budgetRebalanced.set(true);
    this.channelScores.update(list =>
      list.map(item => item.action.includes('Trim') ? { ...item, action: 'Trimmed (-35% Spend)' } : (item.action.includes('Increase') ? { ...item, action: 'Scaled (+25% Spend)' } : item))
    );
  }
}
