import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface TargetAccountUi {
  id: string;
  name: string;
  domain: string;
  industry: string;
  tier: 'Tier 1' | 'Tier 2' | 'Tier 3';
  surgeScore: number;
  assignedRep?: string;
  territory?: string;
  signalsCount: number;
}

export interface RepCapacityUi {
  id: string;
  name: string;
  territory: string;
  activeAccounts: number;
  maxCapacity: number;
  quotaPercent: number;
}

@Component({
  selector: 'app-abm-territory-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="abm-territory-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Predictive ABM Intent Surge & Territory Routing</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-blue-100 text-blue-800">
              Automated Capacity Balancing Active
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Aggregate 1st & 3rd-party intent surge signals, dynamically score target accounts, and route to reps based on quota capacity.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="showSurgeModal.set(true)"
            class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-ingest-surge">
            + Ingest Intent Signal
          </button>
        </div>
      </div>

      <!-- Territory & Surge KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="abm-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Surging Target Accounts</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-accounts-count">{{ accounts().length }}</div>
          <div class="text-[11px] text-blue-600 mt-1 font-semibold">Tier 1 & Tier 2 Monitored</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Average Surge Index</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-avg-surge">{{ averageSurge() }}</div>
          <div class="text-[11px] text-slate-400 mt-1">Normalized 0-100 Scale</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Territory Load Balance</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono">68% Utilized</div>
          <div class="text-[11px] text-slate-400 mt-1">Balanced Across 3 Geographies</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">SLA Escalation Breach</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono">0 Breaches</div>
          <div class="text-[11px] text-slate-400 mt-1">&lt; 15m First Outreach Response</div>
        </div>
      </div>

      <!-- Main Workbench -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Target Accounts Radar Table -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Surging Enterprise Target Accounts</h3>
            <span class="text-xs text-slate-400 font-mono">{{ accounts().length }} accounts</span>
          </div>

          <div class="space-y-3" data-testid="accounts-list">
            @for (acc of accounts(); track acc.id) {
              <div class="p-4 border rounded-lg hover:border-slate-300 transition-colors space-y-2">
                <div class="flex items-center justify-between">
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-slate-800 text-sm">{{ acc.name }}</span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono font-semibold bg-slate-100 text-slate-600">
                      {{ acc.domain }}
                    </span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-bold"
                      [ngClass]="acc.tier === 'Tier 1' ? 'bg-indigo-100 text-indigo-800' : 'bg-slate-100 text-slate-700'">
                      {{ acc.tier }}
                    </span>
                  </div>
                  <div class="flex items-center gap-2">
                    <span class="text-xs font-semibold text-slate-500">Surge:</span>
                    <span class="px-2 py-0.5 rounded-full text-xs font-bold font-mono"
                      [ngClass]="acc.surgeScore >= 85 ? 'bg-rose-100 text-rose-800' : 'bg-amber-100 text-amber-800'">
                      {{ acc.surgeScore }} / 100
                    </span>
                  </div>
                </div>

                <div class="flex items-center justify-between pt-2 border-t text-xs text-slate-500">
                  <div class="flex items-center gap-2">
                    <span>Industry: <strong class="text-slate-700">{{ acc.industry }}</strong></span>
                    <span>&bull;</span>
                    @if (acc.assignedRep) {
                      <span>Assigned: <strong class="text-blue-700">{{ acc.assignedRep }}</strong> ({{ acc.territory }})</span>
                    } @else {
                      <span class="text-amber-600 font-bold">Unassigned</span>
                    }
                  </div>

                  @if (!acc.assignedRep) {
                    <button 
                      (click)="autoRouteAccount(acc.id)"
                      class="px-2.5 py-1 bg-blue-600 hover:bg-blue-700 text-white rounded text-[11px] font-medium shadow-sm"
                      [attr.data-testid]="'btn-route-' + acc.id">
                      Auto-Route to Rep
                    </button>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Sales Rep Capacity & Quota Balance -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Territory Quota & Capacity</h3>
          <p class="text-xs text-slate-500">
            Rep load ratios prevent burnout and ensure fast 15-minute response times.
          </p>

          <div class="space-y-4" data-testid="reps-list">
            @for (rep of reps(); track rep.id) {
              <div class="p-3.5 border rounded-xl space-y-2">
                <div class="flex justify-between items-center text-xs">
                  <div>
                    <span class="font-bold text-slate-800">{{ rep.name }}</span>
                    <span class="text-[11px] text-slate-400 block">{{ rep.territory }}</span>
                  </div>
                  <div class="text-right">
                    <span class="font-mono font-bold text-xs text-emerald-600">{{ rep.quotaPercent }}% Quota</span>
                    <span class="text-[11px] text-slate-400 block font-mono">{{ rep.activeAccounts }}/{{ rep.maxCapacity }} Accts</span>
                  </div>
                </div>

                <!-- Load Bar -->
                <div class="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
                  <div class="h-1.5 rounded-full"
                    [ngClass]="(rep.activeAccounts / rep.maxCapacity) > 0.8 ? 'bg-amber-500' : 'bg-blue-600'"
                    [style.width.%]="(rep.activeAccounts / rep.maxCapacity) * 100"></div>
                </div>
              </div>
            }
          </div>
        </div>
      </div>

      <!-- Ingest Surge Signal Modal -->
      @if (showSurgeModal()) {
        <div class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-white rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-xl border">
            <div class="flex items-center justify-between border-b pb-3">
              <h3 class="font-bold text-slate-900 text-base">Ingest Intent Surge Signal</h3>
              <button (click)="showSurgeModal.set(false)" class="text-slate-400 hover:text-slate-600">&times;</button>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Target Domain</label>
                <input 
                  type="text" 
                  [(ngModel)]="newSurgeDomain" 
                  class="w-full px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  data-testid="input-surge-domain" />
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Signal Source</label>
                <select 
                  [(ngModel)]="newSignalSource" 
                  class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  data-testid="select-signal-source">
                  <option value="PricingPageSurge">High-Velocity Pricing Page Surge (1st Party)</option>
                  <option value="BomboraTopicSurge">Bombora Intent Surge: AI SDR & Automation (3rd Party)</option>
                  <option value="G2BuyerIntent">G2 Crowd Enterprise Category Comparison</option>
                </select>
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">Intent Surge Weight (+Score)</label>
                <input 
                  type="number" 
                  [(ngModel)]="newSurgeWeight" 
                  class="w-full px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-blue-500 focus:outline-none"
                  data-testid="input-surge-weight" />
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-3 border-t">
              <button 
                (click)="showSurgeModal.set(false)"
                class="px-3 py-1.5 border rounded-lg text-xs font-medium text-slate-600 hover:bg-slate-50">
                Cancel
              </button>
              <button 
                (click)="ingestSignal()"
                class="px-4 py-1.5 bg-blue-600 hover:bg-blue-700 text-white rounded-lg text-xs font-medium shadow-sm"
                data-testid="btn-submit-surge">
                Ingest & Re-Score Account
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AbmTerritoryCockpitComponent {
  showSurgeModal = signal(false);

  accounts = signal<TargetAccountUi[]>([
    {
      id: 'acc_1',
      name: 'Stripe Inc',
      domain: 'stripe.com',
      industry: 'Fintech',
      tier: 'Tier 1',
      surgeScore: 96.0,
      assignedRep: 'Elena Rostova',
      territory: 'North America Enterprise',
      signalsCount: 14
    },
    {
      id: 'acc_2',
      name: 'Klarna Bank AB',
      domain: 'klarna.com',
      industry: 'Fintech',
      tier: 'Tier 2',
      surgeScore: 88.5,
      assignedRep: 'Marcus Vance',
      territory: 'EMEA Strategic',
      signalsCount: 9
    },
    {
      id: 'acc_3',
      name: 'Grab Holdings',
      domain: 'grab.com',
      industry: 'Telecom / SuperApp',
      tier: 'Tier 2',
      surgeScore: 74.0,
      signalsCount: 5
    }
  ]);

  reps = signal<RepCapacityUi[]>([
    { id: 'rep_1', name: 'Elena Rostova', territory: 'North America Enterprise', activeAccounts: 18, maxCapacity: 25, quotaPercent: 115 },
    { id: 'rep_2', name: 'Marcus Vance', territory: 'EMEA Strategic', activeAccounts: 12, maxCapacity: 20, quotaPercent: 98 },
    { id: 'rep_3', name: 'Kenji Sato', territory: 'APAC & Global Tech', activeAccounts: 8, maxCapacity: 25, quotaPercent: 104 }
  ]);

  averageSurge = computed(() => {
    const list = this.accounts();
    if (!list.length) return 0;
    const avg = list.reduce((sum, a) => sum + a.surgeScore, 0) / list.length;
    return Math.round(avg * 10) / 10;
  });

  newSurgeDomain = 'snowflake.com';
  newSignalSource = 'PricingPageSurge';
  newSurgeWeight = 18;

  autoRouteAccount(accountId: string) {
    this.accounts.update(list => list.map(acc => {
      if (acc.id === accountId) {
        return {
          ...acc,
          assignedRep: 'Kenji Sato',
          territory: 'APAC & Global Tech'
        };
      }
      return acc;
    }));
  }

  ingestSignal() {
    const newAcc: TargetAccountUi = {
      id: 'acc_' + Date.now(),
      name: 'Snowflake Inc',
      domain: this.newSurgeDomain,
      industry: 'Cloud Infrastructure',
      tier: 'Tier 1',
      surgeScore: 85.0 + this.newSurgeWeight,
      signalsCount: 1
    };

    this.accounts.update(list => [newAcc, ...list]);
    this.showSurgeModal.set(false);
  }
}
