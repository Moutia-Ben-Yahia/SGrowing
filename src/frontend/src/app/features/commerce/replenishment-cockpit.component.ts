import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ReplenishmentRecommendation {
  skuId: string;
  skuName: string;
  warehouseId: string;
  currentAvailable: number;
  reorderPoint: number;
  suggestedOrderQty: number;
  estimatedCost: number;
  recommendedSupplierId: string;
  severity: 'Critical' | 'Warning' | 'Optimal';
  estimatedDaysUntilStockout: number;
}

export interface SupplierScorecard {
  supplierId: string;
  supplierName: string;
  totalOrdersFulfilled: number;
  onTimeDeliveryPercent: number;
  fillRatePercent: number;
  qualityScoreOutOf100: number;
  reliabilityTier: string;
}

export interface EdiPurchaseOrder {
  poNumber: string;
  supplierId: string;
  warehouseId: string;
  totalAmount: number;
  edi850RawPayload: string;
  status: string;
  createdAtUtc: string;
}

@Component({
  selector: 'app-replenishment-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr" data-testid="replenishment-cockpit">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-700/60 pb-6">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-amber-400">
            <span class="inline-block w-2 h-2 rounded-full bg-amber-400 animate-pulse"></span>
            Supply Chain & Autonomous Logistics • Horizon 48
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            Autonomous Inventory Replenishment & Supplier EDI 850 Mesh
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Dynamic EOQ & ROP calculations, multi-tier supplier scorecarding, and automated ANSI X12 EDI 850 PO synthesis.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="generateEdiPurchaseOrder()"
            data-testid="generate-po-btn"
            class="px-4 py-2 bg-amber-600 hover:bg-amber-500 text-white rounded-lg text-sm font-semibold transition-colors flex items-center gap-2 shadow-lg shadow-amber-600/20"
          >
            <span>⚡</span> Generate EDI 850 PO
          </button>
        </div>
      </div>

      <!-- Overview Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Depletion Velocity</div>
          <div class="text-2xl font-bold text-white mt-1" data-testid="stat-velocity">35.5 units/day</div>
          <div class="text-amber-400 text-xs mt-1">{{ criticalDepletionsCount() }} Critical Stockout Alerts</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Staged Capital Reorder</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1" data-testid="stat-staged-capital">
            \${{ totalStagedCost() | number:'1.0-0' }}
          </div>
          <div class="text-slate-400 text-xs mt-1">Optimized EOQ Holding Cost</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">Supplier SLA Fill Rate</div>
          <div class="text-2xl font-bold text-white mt-1">98.2%</div>
          <div class="text-emerald-400 text-xs mt-1">Tier-1 Preferred Network</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4">
          <div class="text-slate-400 text-xs font-medium uppercase">EDI Protocol Standard</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">ANSI X12 850</div>
          <div class="text-slate-400 text-xs mt-1">Version 004010 Direct B2B</div>
        </div>
      </div>

      <!-- Main Layout: Replenishment Recommendations & EDI Payload Viewer -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Recommendations Table (7 cols) -->
        <div class="lg:col-span-7 bg-slate-800/80 border border-slate-700/60 rounded-xl overflow-hidden">
          <div class="p-4 border-b border-slate-700/60 flex items-center justify-between">
            <h2 class="text-lg font-bold text-white">Stock Depletion & Reorder Recommendations</h2>
            <span class="text-xs text-slate-400">{{ recommendations().length }} SKUs evaluated</span>
          </div>

          <div class="divide-y divide-slate-700/40">
            @for (rec of recommendations(); track rec.skuId) {
              <div
                (click)="selectRecommendation(rec)"
                [class.bg-slate-700/40]="selectedRecommendation()?.skuId === rec.skuId"
                class="p-4 hover:bg-slate-700/30 transition-colors cursor-pointer flex flex-col md:flex-row md:items-center justify-between gap-3"
                [attr.data-testid]="'sku-row-' + rec.skuId"
              >
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-white text-sm">{{ rec.skuName }}</span>
                    <span
                      [class]="rec.severity === 'Critical' ? 'bg-rose-900/60 text-rose-300 border-rose-700/60' : 'bg-amber-900/60 text-amber-300 border-amber-700/60'"
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border uppercase"
                      [attr.data-testid]="'severity-badge-' + rec.skuId"
                    >
                      {{ rec.severity }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    SKU: <span class="font-mono text-slate-300">{{ rec.skuId }}</span> • Warehouse: <span class="text-slate-300">{{ rec.warehouseId }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Avail: <strong class="text-white">{{ rec.currentAvailable }}</strong> • ROP: {{ rec.reorderPoint }} • Days to Stockout:
                    <strong [class]="rec.estimatedDaysUntilStockout < 5 ? 'text-rose-400' : 'text-amber-400'">
                      {{ rec.estimatedDaysUntilStockout }}d
                    </strong>
                  </div>
                </div>

                <div class="text-right self-end md:self-auto">
                  <div class="text-xs text-slate-400">EOQ Suggested</div>
                  <div class="text-sm font-bold text-emerald-400">+{{ rec.suggestedOrderQty }} units</div>
                  <div class="text-[11px] text-slate-500">\${{ rec.estimatedCost | number:'1.2-2' }}</div>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- EDI 850 Raw Payload Viewer & Dispatch Console (5 cols) -->
        <div class="lg:col-span-5 bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4">
          <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
            <div>
              <h3 class="font-bold text-white text-base">ANSI X12 EDI 850 Inspector</h3>
              <p class="text-xs text-slate-400 font-mono">{{ stagedPo()?.poNumber || 'PO-STAGED-DRAFT' }}</p>
            </div>
            <span
              [class]="stagedPo()?.status === 'Dispatched' ? 'bg-emerald-900/80 text-emerald-300 border-emerald-700' : 'bg-amber-900/80 text-amber-300 border-amber-700'"
              class="px-2.5 py-1 rounded-full text-xs font-semibold border"
              data-testid="po-status-badge"
            >
              {{ stagedPo()?.status || 'Staged' }}
            </span>
          </div>

          <!-- Raw EDI Terminal -->
          <div class="bg-slate-950 p-3.5 rounded-xl border border-slate-800 font-mono text-[11px] text-emerald-400 overflow-x-auto max-h-60 space-y-1">
            <pre class="whitespace-pre-wrap leading-relaxed">{{ stagedPo()?.edi850RawPayload }}</pre>
          </div>

          <!-- Total & Dispatch Action -->
          <div class="flex items-center justify-between pt-2">
            <div>
              <div class="text-xs text-slate-400">Total Purchase Order:</div>
              <div class="text-xl font-extrabold text-white">\${{ stagedPo()?.totalAmount | number:'1.2-2' }}</div>
            </div>

            <button
              (click)="dispatchPoToSupplier()"
              data-testid="dispatch-po-btn"
              [disabled]="stagedPo()?.status === 'Dispatched'"
              [class]="stagedPo()?.status === 'Dispatched' ? 'bg-slate-700 text-slate-400 cursor-not-allowed' : 'bg-emerald-600 hover:bg-emerald-500 text-white'"
              class="px-4 py-2 rounded-lg text-xs font-semibold transition-colors shadow-lg"
            >
              {{ stagedPo()?.status === 'Dispatched' ? '✓ Transmitted to Supplier' : 'Dispatch to Supplier EDI Mesh' }}
            </button>
          </div>

          <!-- Supplier Scorecard Summary -->
          <div class="pt-3 border-t border-slate-700/60 space-y-2">
            <div class="text-xs font-bold text-slate-300">Supplier Reliability Scorecard:</div>
            <div class="grid grid-cols-2 gap-2 text-xs">
              <div class="p-2.5 bg-slate-900/60 rounded-lg border border-slate-800">
                <div class="text-slate-400">On-Time Delivery</div>
                <div class="text-base font-bold text-emerald-400 mt-0.5">98.4%</div>
              </div>
              <div class="p-2.5 bg-slate-900/60 rounded-lg border border-slate-800">
                <div class="text-slate-400">Order Fill Rate</div>
                <div class="text-base font-bold text-emerald-400 mt-0.5">99.1%</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ReplenishmentCockpitComponent {
  readonly recommendations = signal<ReplenishmentRecommendation[]>([
    {
      skuId: 'SKU-OPTIC-400G',
      skuName: '400G QSFP-DD Transceiver Module',
      warehouseId: 'wh_frankfurt_eu',
      currentAvailable: 40,
      reorderPoint: 138,
      suggestedOrderQty: 250,
      estimatedCost: 36250,
      recommendedSupplierId: 'sup_apex_logistics',
      severity: 'Critical',
      estimatedDaysUntilStockout: 3
    },
    {
      skuId: 'SKU-FIBER-LC10M',
      skuName: 'OM4 Duplex LC-LC Patch Cable 10m',
      warehouseId: 'wh_chicago_us',
      currentAvailable: 80,
      reorderPoint: 150,
      suggestedOrderQty: 500,
      estimatedCost: 9250,
      recommendedSupplierId: 'sup_apex_logistics',
      severity: 'Warning',
      estimatedDaysUntilStockout: 5
    }
  ]);

  readonly selectedRecommendation = signal<ReplenishmentRecommendation | null>(this.recommendations()[0]);

  readonly stagedPo = signal<EdiPurchaseOrder | null>({
    poNumber: 'PO-20261102-7741',
    supplierId: 'sup_apex_logistics',
    warehouseId: 'wh_frankfurt_eu',
    totalAmount: 45500,
    status: 'Staged',
    createdAtUtc: new Date().toISOString(),
    edi850RawPayload: `ISA*00*          *00*          *ZZ*GROWTHOS       *ZZ*APEXGLOBAL01   *261102*1430*U*00401*000000001*0*P*>~
GS*PO*GROWTHOS*APEXGLOBAL01*20261102*1430*1*X*004010~
ST*850*0001~
BEG*00*NE*PO-20261102-7741**20261102~
N1*ST*wh_frankfurt_eu*92*wh_frankfurt_eu~
PO1*1*250*EA*145.00**VC*SKU-OPTIC-400G~
PID*F****400G QSFP-DD Transceiver Module~
PO1*2*500*EA*18.50**VC*SKU-FIBER-LC10M~
PID*F****OM4 Duplex LC-LC Patch Cable 10m~
CTT*2*750~
AMT*TT*45500.00~
SE*10*0001~
GE*1*1~
IEA*1*000000001~`
  });

  readonly criticalDepletionsCount = computed(() =>
    this.recommendations().filter(r => r.severity === 'Critical').length
  );

  readonly totalStagedCost = computed(() =>
    this.recommendations().reduce((acc, r) => acc + r.estimatedCost, 0)
  );

  selectRecommendation(rec: ReplenishmentRecommendation): void {
    this.selectedRecommendation.set(rec);
  }

  generateEdiPurchaseOrder(): void {
    const poNum = `PO-20261102-${Math.floor(1000 + Math.random() * 9000)}`;
    const cost = this.totalStagedCost();

    this.stagedPo.set({
      poNumber: poNum,
      supplierId: 'sup_apex_logistics',
      warehouseId: 'wh_frankfurt_eu',
      totalAmount: cost,
      status: 'Staged',
      createdAtUtc: new Date().toISOString(),
      edi850RawPayload: `ISA*00*          *00*          *ZZ*GROWTHOS       *ZZ*APEXGLOBAL01   *261102*1430*U*00401*000000001*0*P*>~
GS*PO*GROWTHOS*APEXGLOBAL01*20261102*1430*1*X*004010~
ST*850*0001~
BEG*00*NE*${poNum}**20261102~
PO1*1*250*EA*145.00**VC*SKU-OPTIC-400G~
CTT*1*250~
AMT*TT*${cost.toFixed(2)}~
SE*6*0001~
GE*1*1~
IEA*1*000000001~`
    });
  }

  dispatchPoToSupplier(): void {
    if (this.stagedPo()) {
      this.stagedPo.update(po => (po ? { ...po, status: 'Dispatched' } : null));
    }
  }
}
