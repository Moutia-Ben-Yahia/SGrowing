import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface FxRateItem {
  pair: string;
  spot: number;
  hedged: number;
  slippage: string;
}

export interface NexusItem {
  jurisdiction: string;
  rule: string;
  limit: number;
  current: number;
  hasNexus: boolean;
}

@Component({
  selector: 'app-tax-compliance-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="tax-compliance-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Cross-Border FX & Tax Compliance Engine</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              EU VAT OSS & US Economic Nexus
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Real-time multi-currency settlement, foreign exchange slippage hedging, and automated cross-border VAT reverse-charge rules.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <span class="text-xs text-slate-400">ECB Live Sync &bull; 0.5% Slippage Buffer</span>
        </div>
      </div>

      <!-- Live FX Rates Ticker -->
      <div class="grid grid-cols-2 sm:grid-cols-4 gap-4" data-testid="fx-rates-cards">
        @for (rate of fxRates; track rate.pair) {
          <div class="bg-white border rounded-xl p-4 shadow-sm">
            <div class="flex justify-between items-center text-xs text-slate-500">
              <span class="font-bold text-slate-800">{{ rate.pair }}</span>
              <span class="font-mono text-emerald-600">{{ rate.slippage }} buffer</span>
            </div>
            <div class="text-xl font-bold text-slate-900 mt-1 font-mono">{{ rate.hedged }}</div>
            <div class="text-[11px] text-slate-400 mt-0.5">Spot: {{ rate.spot }}</div>
          </div>
        }
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Interactive Transaction Tax Calculator -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <h3 class="font-bold text-slate-900 text-sm">Cross-Border Transaction Tax Calculator</h3>

          <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Base Amount (USD)</label>
              <input 
                type="number" 
                [(ngModel)]="amountBase" 
                class="w-full px-3 py-2 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="input-calc-amount" />
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Settlement Currency</label>
              <select 
                [(ngModel)]="selectedCurrency" 
                class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="select-calc-currency">
                <option value="EUR">EUR (€)</option>
                <option value="GBP">GBP (£)</option>
                <option value="USD">USD ($)</option>
                <option value="JPY">JPY (¥)</option>
              </select>
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-700 mb-1">Customer Country</label>
              <select 
                [(ngModel)]="customerCountry" 
                class="w-full px-3 py-2 border rounded-lg text-xs focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                data-testid="select-calc-country">
                <option value="DE">Germany (DE - 19%)</option>
                <option value="FR">France (FR - 20%)</option>
                <option value="GB">United Kingdom (GB - 20%)</option>
                <option value="US">United States (US - 8.5%)</option>
              </select>
            </div>
          </div>

          <div class="flex items-center gap-4 pt-1">
            <label class="flex items-center gap-2 text-xs font-medium text-slate-700 cursor-pointer">
              <input type="checkbox" [(ngModel)]="isB2B" class="rounded text-indigo-600 focus:ring-indigo-500" data-testid="checkbox-is-b2b" />
              <span>B2B Business Customer</span>
            </label>

            @if (isB2B) {
              <div class="flex-1 max-w-xs">
                <input 
                  type="text" 
                  [(ngModel)]="vatNumber" 
                  placeholder="e.g. DE123456789"
                  class="w-full px-3 py-1.5 border rounded-lg text-xs font-mono focus:ring-2 focus:ring-indigo-500 focus:outline-none"
                  data-testid="input-vat-number" />
              </div>
            }
          </div>

          <!-- Calculation Result Card -->
          <div class="mt-4 p-4 rounded-xl border bg-slate-50 space-y-3" data-testid="calc-results-card">
            <div class="flex items-center justify-between text-xs">
              <span class="font-semibold text-slate-600">Tax Routing Directive:</span>
              <span class="font-mono px-2 py-0.5 rounded text-[11px] font-bold"
                [ngClass]="isReverseCharge() ? 'bg-indigo-100 text-indigo-800' : 'bg-slate-200 text-slate-800'">
                {{ taxDirective() }}
              </span>
            </div>

            <div class="grid grid-cols-3 gap-2 pt-2 border-t text-center">
              <div>
                <span class="text-[11px] text-slate-400 block">Subtotal</span>
                <span class="text-sm font-bold text-slate-800 font-mono">{{ selectedCurrency }} {{ convertedAmount() | number:'1.2-2' }}</span>
              </div>
              <div>
                <span class="text-[11px] text-slate-400 block">Tax Rate / Amount</span>
                <span class="text-sm font-bold text-slate-800 font-mono">{{ taxRate() }}% ({{ selectedCurrency }} {{ taxAmount() | number:'1.2-2' }})</span>
              </div>
              <div>
                <span class="text-[11px] text-slate-400 block">Total Charged</span>
                <span class="text-sm font-bold text-emerald-600 font-mono">{{ selectedCurrency }} {{ totalAmount() | number:'1.2-2' }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Economic Nexus Compliance Radar -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Economic Nexus Thresholds</h3>
            <span class="text-xs text-slate-400">Trailing 12 Mo</span>
          </div>

          <div class="space-y-3" data-testid="nexus-list">
            @for (nexus of nexusList; track nexus.jurisdiction) {
              <div class="p-3 border rounded-lg space-y-1.5">
                <div class="flex items-center justify-between text-xs">
                  <span class="font-semibold text-slate-800">{{ nexus.jurisdiction }}</span>
                  <span class="px-1.5 py-0.5 rounded text-[10px] font-bold"
                    [ngClass]="nexus.hasNexus ? 'bg-amber-100 text-amber-800' : 'bg-emerald-100 text-emerald-800'">
                    {{ nexus.hasNexus ? '● Nexus Active' : 'Within Safe Limit' }}
                  </span>
                </div>
                <div class="flex justify-between text-[11px] text-slate-500">
                  <span>{{ nexus.rule }}</span>
                  <span class="font-mono">\${{ (nexus.current / 1000) | number:'1.0-0' }}k / \${{ (nexus.limit / 1000) | number:'1.0-0' }}k</span>
                </div>
                <div class="w-full bg-slate-100 rounded-full h-1.5 overflow-hidden">
                  <div class="h-1.5 rounded-full" 
                    [ngClass]="nexus.hasNexus ? 'bg-amber-500' : 'bg-emerald-500'"
                    [style.width.%]="Math.min(100, (nexus.current / nexus.limit) * 100)"></div>
                </div>
              </div>
            }
          </div>
        </div>
      </div>
    </div>
  `
})
export class TaxComplianceCockpitComponent {
  Math = Math;

  fxRates: FxRateItem[] = [
    { pair: 'USD / EUR', spot: 0.925, hedged: 0.920, slippage: '0.5%' },
    { pair: 'USD / GBP', spot: 0.792, hedged: 0.788, slippage: '0.5%' },
    { pair: 'USD / JPY', spot: 154.20, hedged: 153.40, slippage: '0.5%' },
    { pair: 'USD / SGD', spot: 1.345, hedged: 1.338, slippage: '0.5%' }
  ];

  nexusList: NexusItem[] = [
    { jurisdiction: 'California, USA', rule: 'Revenue ($500k)', limit: 500000, current: 680000, hasNexus: true },
    { jurisdiction: 'New York, USA', rule: 'Revenue ($100k)', limit: 100000, current: 142000, hasNexus: true },
    { jurisdiction: 'Texas, USA', rule: 'Revenue ($500k)', limit: 500000, current: 310000, hasNexus: false },
    { jurisdiction: 'European Union (OSS)', rule: '€10k Cross-Border', limit: 10000, current: 420000, hasNexus: true }
  ];

  private _amountBase = signal(1000);
  get amountBase(): number { return this._amountBase(); }
  set amountBase(val: number) { this._amountBase.set(val); }

  private _selectedCurrency = signal('EUR');
  get selectedCurrency(): string { return this._selectedCurrency(); }
  set selectedCurrency(val: string) { this._selectedCurrency.set(val); }

  private _customerCountry = signal('DE');
  get customerCountry(): string { return this._customerCountry(); }
  set customerCountry(val: string) { this._customerCountry.set(val); }

  private _isB2B = signal(true);
  get isB2B(): boolean { return this._isB2B(); }
  set isB2B(val: boolean) { this._isB2B.set(val); }

  private _vatNumber = signal('DE123456789');
  get vatNumber(): string { return this._vatNumber(); }
  set vatNumber(val: string) { this._vatNumber.set(val); }

  convertedAmount = computed(() => {
    const rate = this._selectedCurrency() === 'EUR' ? 0.920 : this._selectedCurrency() === 'GBP' ? 0.788 : 1.0;
    return this._amountBase() * rate;
  });

  isReverseCharge = computed(() => {
    return this._isB2B() && ['DE', 'FR', 'NL'].includes(this._customerCountry()) && !!this._vatNumber().trim();
  });

  taxRate = computed(() => {
    if (this.isReverseCharge()) return 0;
    if (this._customerCountry() === 'DE') return 19;
    if (this._customerCountry() === 'FR' || this._customerCountry() === 'GB') return 20;
    return 8.5;
  });

  taxAmount = computed(() => {
    return Math.round(this.convertedAmount() * (this.taxRate() / 100) * 100) / 100;
  });

  totalAmount = computed(() => {
    return this.convertedAmount() + this.taxAmount();
  });

  taxDirective = computed(() => {
    return this.isReverseCharge() ? 'EU-VAT-REVERSE-CHARGE-ART194' : `STANDARD-VAT-${this._customerCountry()}`;
  });
}
