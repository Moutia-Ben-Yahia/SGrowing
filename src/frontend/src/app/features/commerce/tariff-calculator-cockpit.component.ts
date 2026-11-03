import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface LandedBreakdownView {
  sku: string;
  hsCode: string;
  country: string;
  productPrice: number;
  shipping: number;
  insurance: number;
  dutyRatePct: number;
  dutyAmount: number;
  vatRatePct: number;
  vatAmount: number;
  totalLanded: number;
}

@Component({
  selector: 'app-tariff-calculator-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="tariff-calculator-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-emerald-400">
            <span class="inline-block w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            Global Customs & Cross-Border Compliance • Horizon 74
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Global Sales Tax & Harmonized Tariff (HS) Calculator
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            WCO Harmonized System classification (Chapter 84/85), landed cost duty/VAT calculation & DDP compliance sealing.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="calculateUkLandedCost()"
            data-testid="calculate-uk-btn"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-emerald-600/20"
          >
            <span>🇬🇧</span> Ship to United Kingdom (GB)
          </button>
        </div>
      </div>

      <!-- KPI Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Classified HS Code</div>
          <div class="text-2xl font-bold text-white mt-1 font-mono" data-testid="stat-hs-code">
            {{ breakdown().hsCode }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">ITA Zero Duty Appliance</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Destination VAT / GST Rate</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1" data-testid="stat-vat-rate">
            {{ breakdown().vatRatePct }}% ({{ breakdown().country }})
          </div>
          <div class="text-slate-400 text-xs mt-1">Statutory Import VAT Amount: \${{ breakdown().vatAmount | number:'1.2-2' }}</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Total Landed Cost (DDP)</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-total-landed">
            \${{ breakdown().totalLanded | number:'1.2-2' }}
          </div>
          <div class="text-emerald-400 text-xs mt-1">Delivered Duty Paid Guaranteed</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Customs Clearance Status</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-clearance">
            SEALED (DDP)
          </div>
          <div class="text-slate-400 text-xs mt-1">Zero Border Hold Guarantee</div>
        </div>
      </div>

      <!-- Landed Cost Breakdown Waterfall Card -->
      <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
        <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
          <h2 class="text-lg font-bold text-white">Delivered Duty Paid (DDP) Landed Cost Waterfall</h2>
          <span class="text-xs text-slate-400 font-mono">SKU: {{ breakdown().sku }}</span>
        </div>

        <div class="grid grid-cols-1 md:grid-cols-5 gap-4 text-xs">
          <div class="p-3 bg-slate-900 rounded-lg border border-slate-800 space-y-1">
            <span class="text-slate-400">1. Product Price:</span>
            <div class="text-base font-bold text-white">\${{ breakdown().productPrice | number:'1.2-2' }}</div>
          </div>
          <div class="p-3 bg-slate-900 rounded-lg border border-slate-800 space-y-1">
            <span class="text-slate-400">2. Freight + Insurance:</span>
            <div class="text-base font-bold text-slate-200">\${{ breakdown().shipping + breakdown().insurance | number:'1.2-2' }}</div>
          </div>
          <div class="p-3 bg-slate-900 rounded-lg border border-slate-800 space-y-1">
            <span class="text-slate-400">3. Customs Duty ({{ breakdown().dutyRatePct }}%):</span>
            <div class="text-base font-bold text-cyan-400">\${{ breakdown().dutyAmount | number:'1.2-2' }}</div>
          </div>
          <div class="p-3 bg-slate-900 rounded-lg border border-slate-800 space-y-1">
            <span class="text-slate-400">4. Import VAT ({{ breakdown().vatRatePct }}%):</span>
            <div class="text-base font-bold text-amber-400">\${{ breakdown().vatAmount | number:'1.2-2' }}</div>
          </div>
          <div class="p-3 bg-emerald-950/40 rounded-lg border border-emerald-800/60 space-y-1">
            <span class="text-emerald-300 font-bold">5. Total Landed Cost:</span>
            <div class="text-base font-extrabold text-emerald-400">\${{ breakdown().totalLanded | number:'1.2-2' }}</div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class TariffCalculatorCockpitComponent {
  readonly breakdown = signal<LandedBreakdownView>({
    sku: 'sku_gpu_appliance_101',
    hsCode: '8471.50',
    country: 'DE',
    productPrice: 2000.0,
    shipping: 100.0,
    insurance: 20.0,
    dutyRatePct: 0.0,
    dutyAmount: 0.0,
    vatRatePct: 19.0,
    vatAmount: 402.8,
    totalLanded: 2522.8
  });

  calculateUkLandedCost(): void {
    this.breakdown.set({
      sku: 'sku_gpu_appliance_101',
      hsCode: '8471.50',
      country: 'GB',
      productPrice: 2000.0,
      shipping: 110.0,
      insurance: 20.0,
      dutyRatePct: 0.0,
      dutyAmount: 0.0,
      vatRatePct: 20.0,
      vatAmount: 426.0,
      totalLanded: 2556.0
    });
  }
}
