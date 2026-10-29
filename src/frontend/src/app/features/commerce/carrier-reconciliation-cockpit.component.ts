import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface FinancialKpis {
  totalCodInTransit: number;
  totalCashCollectedByCouriers: number;
  clearedMerchantSettlement: number;
  unresolvedDiscrepanciesCount: number;
  unresolvedDiscrepanciesValue: number;
}

export interface CourierRoutingScore {
  courierProviderId: string;
  courierName: string;
  apiCode: string;
  destinationGovernorate: string;
  countryCode: string;
  historicalDeliveryRatePercent: number;
  baseDeliveryFee: number;
  returnFeePenalty: number;
  averageTransitDays: number;
  codAmount: number;
  expectedNetValue: number;
  formulaExpression: string;
  recommendationReason: string;
}

export interface ConfirmedOrderRow {
  id: string;
  orderNumber: string;
  customerName: string;
  phone: string;
  governorate: string;
  delegation: string;
  address: string;
  codAmount: number;
  confirmedAt: string;
  selected: boolean;
}

export interface ShippingManifestItem {
  id: string;
  manifestNumber: string;
  courierName: string;
  courierCode: string;
  orderCount: number;
  totalCashAmount: number;
  expectedFreightCost: number;
  status: 'PendingPickup' | 'Dispatched' | 'Settled';
  createdAt: string;
  barcodeString: string;
  parcels: ManifestParcel[];
}

export interface ManifestParcel {
  orderId: string;
  orderNumber: string;
  customerName: string;
  phone: string;
  destination: string;
  trackingNumber: string;
  awbBarcode: string;
  codAmount: number;
  freightFee: number;
}

export interface RemittanceMatchRow {
  matchId: string;
  trackingNumber: string;
  orderNumber: string;
  customerName: string;
  reportedStatus: string;
  reportedCashCollected: number;
  systemExpectedAmount: number;
  cashVariance: number;
  courierFeeDeducted: number;
  discrepancyType: 'ExactMatch' | 'ShortPayment' | 'Overpayment' | 'PhantomReturn' | 'UnaccountedParcel';
  disputeTicketNumber?: string;
  holdbackAmount: number;
  resolutionNotes?: string;
  isResolved: boolean;
}

export interface RemittanceAuditSummary {
  statementReference: string;
  courierName: string;
  totalParcels: number;
  totalCollectedCash: number;
  approvedDeliveryFees: number;
  returnPenalties: number;
  discrepancyHoldback: number;
  netCashSettlement: number;
  clearedCount: number;
  discrepancyCount: number;
  matches: RemittanceMatchRow[];
}

@Component({
  selector: 'app-carrier-reconciliation-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <!-- Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <!-- Brand Badge -->
          <div class="flex items-center gap-2 mb-6">
            <div class="h-9 w-9 rounded-xl bg-gradient-to-tr from-cyan-500 to-indigo-600 flex items-center justify-center font-bold text-white shadow-lg shadow-cyan-500/20 text-lg">
              🚚
            </div>
            <div>
              <h1 class="text-sm font-bold tracking-wide text-white">SGrowing Logistics</h1>
              <p class="text-[11px] text-cyan-400 font-medium">COD Courier & Cash Ledger</p>
            </div>
          </div>

          <!-- Navigation Links -->
          <div class="space-y-1 mb-6">
            <span class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider px-2">Commerce ERP</span>
            <div class="space-y-1">
              <a routerLink="/commerce/callcenter" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📞 Call Center & Anti-Collision</span>
              </a>
              <a routerLink="/commerce/reconciliation" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 font-medium">
                <span>📊 Courier Dispatch & COD Ledger</span>
              </a>
              <a routerLink="/commerce/warehouses" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span>🏬 Multi-Warehouse Allocation</span>
              </a>
              <a routerLink="/organizations/margin" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span>🏢 Organization & Margin</span>
              </a>
            </div>
          </div>

          <!-- Regional Protocol Badge -->
          <div class="p-3 bg-slate-900 border border-slate-800 rounded-xl space-y-2">
            <div class="flex items-center justify-between text-xs">
              <span class="text-slate-400">Regional Gateway:</span>
              <span class="font-semibold text-cyan-400">Mallatech & TikTak PRO</span>
            </div>
            <div class="flex items-center justify-between text-xs">
              <span class="text-slate-400">Courier Mesh:</span>
              <span class="flex items-center gap-1.5 text-emerald-400 font-medium">
                <span class="h-2 w-2 rounded-full bg-emerald-500 animate-pulse"></span> 15+ Couriers Active
              </span>
            </div>
          </div>
        </div>

        <!-- Formula Explainer Box -->
        <div class="p-3 bg-cyan-950/30 border border-cyan-800/40 rounded-xl text-xs space-y-1.5">
          <div class="text-cyan-400 font-semibold flex items-center gap-1.5">
            <span>📐 Expected Net Value Rule</span>
          </div>
          <p class="text-slate-300 text-[10.5px] leading-relaxed font-mono">
            E[V] = P(deliv)*COD - Fee(deliv) - (1-P)*Fee(ret)
          </p>
          <p class="text-slate-400 text-[10.5px] leading-relaxed">
            Routes parcels where historical delivery profitability is highest.
          </p>
        </div>
      </aside>

      <!-- Main Workspace -->
      <main class="flex-1 flex flex-col overflow-y-auto bg-slate-950">
        <!-- Top App Header -->
        <header class="border-b border-slate-800 bg-slate-900/60 px-8 py-5 flex items-center justify-between backdrop-blur sticky top-0 z-20">
          <div>
            <div class="flex items-center gap-3">
              <h2 class="text-lg font-bold text-white">Regional Courier Auto-Dispatch & COD Cash Reconciliation</h2>
              <span class="text-xs px-2.5 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-medium">
                Frontier 8 • Live Payout Audit
              </span>
            </div>
            <p class="text-xs text-slate-400 mt-0.5">
              Automated routing matrix across 24 Tunisian & Egyptian governorates, locked manifest generation, and phantom return dispute clearing.
            </p>
          </div>

          <!-- Tab Bar -->
          <div class="flex items-center gap-1 bg-slate-900 border border-slate-800 p-1 rounded-xl">
            <button
              (click)="activeTab.set('matrix')"
              [class]="activeTab() === 'matrix' ? 'bg-cyan-600 text-white font-semibold shadow-md shadow-cyan-600/30' : 'text-slate-400 hover:text-white'"
              class="px-3.5 py-1.5 rounded-lg text-xs transition flex items-center gap-1.5">
              <span>🎯 Auto-Routing Matrix</span>
            </button>
            <button
              (click)="activeTab.set('manifests')"
              [class]="activeTab() === 'manifests' ? 'bg-cyan-600 text-white font-semibold shadow-md shadow-cyan-600/30' : 'text-slate-400 hover:text-white'"
              class="px-3.5 py-1.5 rounded-lg text-xs transition flex items-center gap-1.5">
              <span>📦 Manifest Batches</span>
              <span class="text-[10px] px-1.5 py-0.2 rounded-full bg-slate-800 text-cyan-300 font-mono">{{ pendingOrders().length }}</span>
            </button>
            <button
              (click)="activeTab.set('remittance')"
              [class]="activeTab() === 'remittance' ? 'bg-cyan-600 text-white font-semibold shadow-md shadow-cyan-600/30' : 'text-slate-400 hover:text-white'"
              class="px-3.5 py-1.5 rounded-lg text-xs transition flex items-center gap-1.5">
              <span>💵 Cash Remittance Reconciler</span>
            </button>
          </div>
        </header>

        <!-- Top Financial KPI Cards Banner -->
        <section class="grid grid-cols-1 md:grid-cols-4 gap-4 px-8 pt-6 pb-2">
          <!-- KPI 1: COD In Transit -->
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 flex items-start justify-between shadow-sm">
            <div>
              <span class="text-xs font-medium text-slate-400">Total COD In Transit</span>
              <div class="text-xl font-bold text-amber-400 mt-1 font-mono">
                {{ kpis().totalCodInTransit | number:'1.2-2' }} <span class="text-xs text-amber-500/80">TND</span>
              </div>
              <p class="text-[11px] text-slate-500 mt-0.5">Parcels with active courier custody</p>
            </div>
            <div class="p-2.5 rounded-lg bg-amber-500/10 text-amber-400 border border-amber-500/20 text-lg">
              🚚
            </div>
          </div>

          <!-- KPI 2: Cash Collected by Couriers -->
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 flex items-start justify-between shadow-sm">
            <div>
              <span class="text-xs font-medium text-slate-400">Cash Collected by Couriers</span>
              <div class="text-xl font-bold text-sky-400 mt-1 font-mono">
                {{ kpis().totalCashCollectedByCouriers | number:'1.2-2' }} <span class="text-xs text-sky-500/80">TND</span>
              </div>
              <p class="text-[11px] text-slate-500 mt-0.5">Delivered, awaiting courier wire</p>
            </div>
            <div class="p-2.5 rounded-lg bg-sky-500/10 text-sky-400 border border-sky-500/20 text-lg">
              💰
            </div>
          </div>

          <!-- KPI 3: Cleared Merchant Settlement -->
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 flex items-start justify-between shadow-sm">
            <div>
              <span class="text-xs font-medium text-slate-400">Cleared Merchant Settlement</span>
              <div class="text-xl font-bold text-emerald-400 mt-1 font-mono">
                {{ kpis().clearedMerchantSettlement | number:'1.2-2' }} <span class="text-xs text-emerald-500/80">TND</span>
              </div>
              <p class="text-[11px] text-slate-500 mt-0.5">Reconciled funds ready for payout</p>
            </div>
            <div class="p-2.5 rounded-lg bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 text-lg">
              🏦
            </div>
          </div>

          <!-- KPI 4: Unresolved Discrepancies -->
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800 flex items-start justify-between shadow-sm">
            <div>
              <span class="text-xs font-medium text-slate-400">Discrepancy Holdback</span>
              <div class="text-xl font-bold text-rose-400 mt-1 font-mono flex items-center gap-1.5">
                <span>{{ kpis().unresolvedDiscrepanciesValue | number:'1.2-2' }}</span>
                <span class="text-xs text-rose-500/80">TND</span>
                <span class="text-[10px] px-1.5 py-0.5 rounded-full bg-rose-500/20 text-rose-300 border border-rose-500/30">
                  {{ kpis().unresolvedDiscrepanciesCount }} open
                </span>
              </div>
              <p class="text-[11px] text-slate-500 mt-0.5">Short collections & phantom returns</p>
            </div>
            <div class="p-2.5 rounded-lg bg-rose-500/10 text-rose-400 border border-rose-500/20 text-lg">
              ⚠️
            </div>
          </div>
        </section>

        <!-- Interactive Toast Feedback -->
        @if (toastMessage()) {
          <div class="mx-8 mt-4 p-3 rounded-xl bg-cyan-950/80 border border-cyan-500/40 text-cyan-300 text-xs flex items-center justify-between animate-fadeIn">
            <div class="flex items-center gap-2">
              <span class="text-base">✨</span>
              <span>{{ toastMessage() }}</span>
            </div>
            <button (click)="toastMessage.set(null)" class="text-slate-400 hover:text-white text-xs px-2">✕</button>
          </div>
        }

        <!-- TAB 1: Auto-Routing Matrix -->
        @if (activeTab() === 'matrix') {
          <section class="p-8 space-y-6">
            <!-- Filter & Simulator Controls -->
            <div class="p-5 rounded-2xl bg-slate-900/70 border border-slate-800 space-y-4">
              <div class="flex flex-wrap items-center justify-between gap-4">
                <div>
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>🧭 Regional Destination Selector</span>
                    <span class="text-xs px-2 py-0.5 rounded bg-cyan-500/20 text-cyan-300 font-mono">
                      {{ selectedCountry() === 'TN' ? '24 Tunisian Gouvernorats' : 'Egyptian Governorates' }}
                    </span>
                  </h3>
                  <p class="text-xs text-slate-400 mt-0.5">
                    Evaluates expected value E[Value] factoring in carrier rate cards, local delivery speed, and return penalties.
                  </p>
                </div>

                <div class="flex items-center gap-3">
                  <!-- Country Switcher -->
                  <div class="flex bg-slate-800/80 p-1 rounded-lg border border-slate-700 text-xs">
                    <button
                      (click)="switchCountry('TN')"
                      [class]="selectedCountry() === 'TN' ? 'bg-cyan-600 text-white font-medium' : 'text-slate-400 hover:text-white'"
                      class="px-2.5 py-1 rounded transition">
                      🇹🇳 Tunisia (24 Govs)
                    </button>
                    <button
                      (click)="switchCountry('EG')"
                      [class]="selectedCountry() === 'EG' ? 'bg-cyan-600 text-white font-medium' : 'text-slate-400 hover:text-white'"
                      class="px-2.5 py-1 rounded transition">
                      🇪🇬 Egypt (Delta & Cities)
                    </button>
                  </div>

                  <!-- Simulated COD Amount -->
                  <div class="flex items-center gap-2 bg-slate-800/80 px-3 py-1.5 rounded-lg border border-slate-700">
                    <span class="text-xs text-slate-400">Order COD:</span>
                    <input
                      type="number"
                      [(ngModel)]="simulatedCodAmount"
                      (change)="recomputeMatrix()"
                      class="w-16 bg-slate-950 border border-slate-700 rounded px-1.5 py-0.5 text-xs text-cyan-400 font-mono text-right" />
                    <span class="text-xs text-slate-400">{{ selectedCountry() === 'TN' ? 'TND' : 'EGP' }}</span>
                  </div>
                </div>
              </div>

              <!-- Quick Governorate Selector Pills -->
              <div class="flex flex-wrap items-center gap-1.5 pt-2 border-t border-slate-800/60">
                @for (gov of currentGovernorates(); track gov) {
                  <button
                    (click)="selectGovernorate(gov)"
                    [class]="selectedGovernorate() === gov ? 'bg-cyan-500 text-slate-950 font-bold shadow-md shadow-cyan-500/20' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
                    class="px-2.5 py-1 rounded-lg text-xs transition">
                    {{ gov }}
                  </button>
                }
              </div>
            </div>

            <!-- Ranked Couriers Table -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/60 overflow-hidden shadow-lg">
              <div class="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-900/80">
                <div class="flex items-center gap-2">
                  <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">Ranked Couriers in</span>
                  <span class="text-sm font-bold text-cyan-400">{{ selectedGovernorate() }}</span>
                  <span class="text-xs text-slate-500">({{ matrixCarriers().length }} available providers)</span>
                </div>
                <span class="text-xs text-slate-400 font-mono">Sorted by Expected Net Value Descending</span>
              </div>

              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-900/90 text-slate-400 uppercase text-[11px] tracking-wider border-b border-slate-800">
                    <tr>
                      <th class="p-3.5">Rank</th>
                      <th class="p-3.5">Courier Partner</th>
                      <th class="p-3.5">Historical Delivery Rate</th>
                      <th class="p-3.5">Transit Time</th>
                      <th class="p-3.5">Base Delivery Fee</th>
                      <th class="p-3.5">Return Penalty</th>
                      <th class="p-3.5 text-right">Expected Net Value E[V]</th>
                      <th class="p-3.5 text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (carrier of matrixCarriers(); track carrier.courierProviderId; let idx = $index) {
                      <tr class="hover:bg-slate-800/40 transition" [class.bg-cyan-950/20]="idx === 0">
                        <!-- Rank -->
                        <td class="p-3.5 font-bold">
                          @if (idx === 0) {
                            <span class="px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 text-[10px]">
                              #1 Optimal
                            </span>
                          } @else {
                            <span class="text-slate-400 font-mono">#{{ idx + 1 }}</span>
                          }
                        </td>

                        <!-- Courier Partner -->
                        <td class="p-3.5">
                          <div class="font-semibold text-white flex items-center gap-2">
                            <span>{{ carrier.courierName }}</span>
                            <span class="text-[10px] px-1.5 py-0.2 rounded bg-slate-800 text-slate-400 font-mono">{{ carrier.apiCode }}</span>
                          </div>
                          <p class="text-[11px] text-slate-400 mt-0.5">{{ carrier.recommendationReason }}</p>
                        </td>

                        <!-- Delivery Rate -->
                        <td class="p-3.5">
                          <div class="flex items-center gap-2">
                            <div class="w-16 h-1.5 rounded-full bg-slate-800 overflow-hidden">
                              <div
                                class="h-full rounded-full"
                                [class]="carrier.historicalDeliveryRatePercent >= 90 ? 'bg-emerald-500' : 'bg-amber-500'"
                                [style.width.%]="carrier.historicalDeliveryRatePercent">
                              </div>
                            </div>
                            <span class="font-mono font-semibold" [class.text-emerald-400]="carrier.historicalDeliveryRatePercent >= 90">
                              {{ carrier.historicalDeliveryRatePercent }}%
                            </span>
                          </div>
                        </td>

                        <!-- Transit -->
                        <td class="p-3.5 text-slate-300 font-mono">
                          ⚡ {{ carrier.averageTransitDays }} days
                        </td>

                        <!-- Base Delivery Fee -->
                        <td class="p-3.5 font-mono text-slate-300">
                          {{ carrier.baseDeliveryFee | number:'1.2-2' }} {{ selectedCountry() === 'TN' ? 'TND' : 'EGP' }}
                        </td>

                        <!-- Return Fee Penalty -->
                        <td class="p-3.5 font-mono text-rose-400">
                          -{{ carrier.returnFeePenalty | number:'1.2-2' }} {{ selectedCountry() === 'TN' ? 'TND' : 'EGP' }}
                        </td>

                        <!-- Expected Net Value -->
                        <td class="p-3.5 text-right font-mono">
                          <div class="text-sm font-bold text-emerald-400">
                            +{{ carrier.expectedNetValue | number:'1.2-2' }} <span class="text-[10px]">{{ selectedCountry() === 'TN' ? 'TND' : 'EGP' }}</span>
                          </div>
                          <span class="text-[10px] text-slate-500">Net after return risk</span>
                        </td>

                        <!-- Action -->
                        <td class="p-3.5 text-right">
                          <button
                            (click)="simulateAutoRouteOrder(carrier)"
                            class="px-2.5 py-1 rounded-lg text-xs bg-slate-800 hover:bg-cyan-600 text-slate-200 hover:text-white transition border border-slate-700">
                            Simulate Route
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </section>
        }

        <!-- TAB 2: Manifest Batches Generator -->
        @if (activeTab() === 'manifests') {
          <section class="p-8 space-y-6">
            <!-- Confirmed Orders Ready for Dispatch -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/70 p-5 space-y-4 shadow-lg">
              <div class="flex flex-wrap items-center justify-between gap-4">
                <div>
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>📋 Confirmed COD Orders Ready for Courier Dispatch</span>
                    <span class="text-xs px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300 font-mono">
                      {{ selectedOrderIds().length }} / {{ pendingOrders().length }} Selected
                    </span>
                  </h3>
                  <p class="text-xs text-slate-400 mt-0.5">
                    Select orders to bundle into a locked manifest batch (MNF-YYYYMMDD-XXXXX) with sequential AWB tracking codes.
                  </p>
                </div>

                <!-- Batch Actions -->
                <div class="flex items-center gap-3">
                  <div class="flex items-center gap-2 text-xs">
                    <span class="text-slate-400">Assign Courier:</span>
                    <select
                      [(ngModel)]="selectedCourierForManifest"
                      class="bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white">
                      <option value="ARX">Aramex Tunisia (ARX)</option>
                      <option value="FST">First Delivery (FST)</option>
                      <option value="LVR">Livri Express (LVR)</option>
                      <option value="EXP">Express Courrier (EXP)</option>
                    </select>
                  </div>

                  <button
                    (click)="generateCourierManifest()"
                    [disabled]="selectedOrderIds().length === 0"
                    class="px-4 py-1.5 rounded-lg text-xs font-semibold bg-gradient-to-r from-cyan-600 to-indigo-600 hover:from-cyan-500 hover:to-indigo-500 text-white disabled:opacity-40 disabled:cursor-not-allowed transition shadow-md shadow-cyan-600/20 flex items-center gap-1.5">
                    <span>📦 Generate Manifest ({{ selectedOrderIds().length }})</span>
                  </button>
                </div>
              </div>

              <!-- Orders Table -->
              <div class="overflow-x-auto rounded-xl border border-slate-800">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-900 text-slate-400 uppercase text-[11px] tracking-wider border-b border-slate-800">
                    <tr>
                      <th class="p-3 w-8">
                        <input
                          type="checkbox"
                          [checked]="isAllSelected()"
                          (change)="toggleSelectAll()"
                          class="rounded border-slate-700 bg-slate-950 text-cyan-500" />
                      </th>
                      <th class="p-3">Order #</th>
                      <th class="p-3">Customer</th>
                      <th class="p-3">Phone</th>
                      <th class="p-3">Destination</th>
                      <th class="p-3 text-right">COD Amount</th>
                      <th class="p-3 text-right">Confirmed At</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (order of pendingOrders(); track order.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="p-3">
                          <input
                            type="checkbox"
                            [(ngModel)]="order.selected"
                            class="rounded border-slate-700 bg-slate-950 text-cyan-500" />
                        </td>
                        <td class="p-3 font-mono font-semibold text-cyan-400">{{ order.orderNumber }}</td>
                        <td class="p-3 font-medium text-white">{{ order.customerName }}</td>
                        <td class="p-3 text-slate-300 font-mono">{{ order.phone }}</td>
                        <td class="p-3 text-slate-300">
                          <span class="font-medium text-white">{{ order.governorate }}</span>, {{ order.delegation }}
                        </td>
                        <td class="p-3 text-right font-mono text-emerald-400 font-bold">
                          {{ order.codAmount | number:'1.2-2' }} TND
                        </td>
                        <td class="p-3 text-right text-slate-400 font-mono">{{ order.confirmedAt }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>

            <!-- Manifest Batches History -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/60 p-5 space-y-4 shadow-lg">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-semibold text-white">📑 Generated Shipping Manifests</h3>
                  <p class="text-xs text-slate-400 mt-0.5">Locked batches ready for warehouse driver hand-over and barcode scanning.</p>
                </div>
              </div>

              <div class="overflow-x-auto rounded-xl border border-slate-800">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-900 text-slate-400 uppercase text-[11px] tracking-wider border-b border-slate-800">
                    <tr>
                      <th class="p-3">Manifest #</th>
                      <th class="p-3">Courier</th>
                      <th class="p-3">Parcels</th>
                      <th class="p-3">Total Declared COD</th>
                      <th class="p-3">Freight Cost</th>
                      <th class="p-3">Status</th>
                      <th class="p-3">Created Date</th>
                      <th class="p-3 text-right">Slip Action</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (mnf of manifestBatches(); track mnf.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="p-3 font-mono font-bold text-cyan-400">{{ mnf.manifestNumber }}</td>
                        <td class="p-3 font-medium text-white">{{ mnf.courierName }}</td>
                        <td class="p-3 font-mono">{{ mnf.orderCount }} parcels</td>
                        <td class="p-3 font-mono text-emerald-400 font-semibold">{{ mnf.totalCashAmount | number:'1.2-2' }} TND</td>
                        <td class="p-3 font-mono text-slate-300">{{ mnf.expectedFreightCost | number:'1.2-2' }} TND</td>
                        <td class="p-3">
                          <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold font-mono"
                            [class]="mnf.status === 'PendingPickup' ? 'bg-amber-500/20 text-amber-300 border border-amber-500/30' : 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30'">
                            {{ mnf.status }}
                          </span>
                        </td>
                        <td class="p-3 text-slate-400 font-mono">{{ mnf.createdAt }}</td>
                        <td class="p-3 text-right">
                          <button
                            (click)="openManifestSlip(mnf)"
                            class="px-3 py-1 rounded-lg text-xs font-medium bg-slate-800 hover:bg-slate-700 text-cyan-300 border border-cyan-500/30 transition flex items-center gap-1 ml-auto">
                            <span>📄 View & Print Slip</span>
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </section>
        }

        <!-- TAB 3: Cash Remittance Statement CSV Reconciler -->
        @if (activeTab() === 'remittance') {
          <section class="p-8 space-y-6">
            <!-- CSV Ingestion Area -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/70 p-5 space-y-4 shadow-lg">
              <div class="flex flex-wrap items-center justify-between gap-4">
                <div>
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>📥 Courier Cash Remittance Statement Ingestion</span>
                    <span class="text-xs px-2 py-0.5 rounded bg-cyan-500/20 text-cyan-300 font-mono">CSV Diff Engine</span>
                  </h3>
                  <p class="text-xs text-slate-400 mt-0.5">
                    Upload or paste carrier payment statements to cross-reference with SGrowing shipments and audit discrepancies.
                  </p>
                </div>

                <!-- Sample CSV Pre-fills -->
                <div class="flex items-center gap-2 text-xs">
                  <span class="text-slate-400">Preload Sample:</span>
                  <button
                    (click)="loadSampleCsv('livri')"
                    class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition">
                    Livri (Standard + Short Pay)
                  </button>
                  <button
                    (click)="loadSampleCsv('aramex_phantom')"
                    class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 transition">
                    Aramex (Phantom Return Case)
                  </button>
                </div>
              </div>

              <!-- Statement Form -->
              <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Statement Reference #</label>
                  <input
                    type="text"
                    [(ngModel)]="statementRef"
                    placeholder="e.g. BVR-LVR-2026-W44"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white font-mono" />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Carrier Provider</label>
                  <select
                    [(ngModel)]="reconcileCourierCode"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white">
                    <option value="LVR">Livri Express</option>
                    <option value="ARX">Aramex Tunisia</option>
                    <option value="FST">First Delivery</option>
                    <option value="EXP">Express Courrier</option>
                  </select>
                </div>
                <div class="flex items-end">
                  <button
                    (click)="reconcileStatement()"
                    class="w-full px-4 py-2 rounded-lg text-xs font-semibold bg-gradient-to-r from-cyan-600 to-indigo-600 hover:from-cyan-500 hover:to-indigo-500 text-white transition shadow-md shadow-cyan-600/20 flex items-center justify-center gap-1.5">
                    <span>⚡ Audit & Reconcile Statement</span>
                  </button>
                </div>
              </div>

              <!-- Raw CSV Textarea -->
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Carrier CSV Content (TrackingNumber, Status, CashCollected, CourierFee)</label>
                <textarea
                  [(ngModel)]="csvInputText"
                  rows="4"
                  class="w-full bg-slate-950 border border-slate-700 rounded-xl p-3 text-xs text-cyan-300 font-mono leading-relaxed focus:outline-none focus:border-cyan-500"
                  placeholder="AWB-LVR-20261029-0001,Delivered,90.00,7.00&#10;AWB-LVR-20261029-0002,Delivered,120.00,7.00">
                </textarea>
              </div>
            </div>

            <!-- Audit Report Summary -->
            @if (auditSummary()) {
              <!-- Net Cash Settlement Formula Banner -->
              <div class="rounded-2xl border border-cyan-800/60 bg-gradient-to-br from-slate-900 via-cyan-950/20 to-slate-900 p-5 space-y-4 shadow-xl">
                <div class="flex flex-wrap items-center justify-between gap-4 border-b border-slate-800/80 pb-4">
                  <div>
                    <span class="text-xs uppercase tracking-wider text-cyan-400 font-semibold font-mono">Reconciliation Audit Settlement</span>
                    <h4 class="text-base font-bold text-white mt-0.5">
                      {{ auditSummary()!.statementReference }} • {{ auditSummary()!.courierName }}
                    </h4>
                  </div>
                  <div class="text-right">
                    <span class="text-xs text-slate-400">Net Merchant Payout (Approved):</span>
                    <div class="text-2xl font-black text-emerald-400 font-mono">
                      {{ auditSummary()!.netCashSettlement | number:'1.2-2' }} <span class="text-sm">TND</span>
                    </div>
                  </div>
                </div>

                <!-- Settlement Math Equation Grid -->
                <div class="grid grid-cols-2 md:grid-cols-4 gap-3 text-xs">
                  <div class="p-3 bg-slate-950/60 rounded-xl border border-slate-800">
                    <span class="text-slate-400">Gross Cash Collected:</span>
                    <div class="text-sm font-bold text-sky-400 font-mono mt-0.5">
                      +{{ auditSummary()!.totalCollectedCash | number:'1.2-2' }} TND
                    </div>
                  </div>
                  <div class="p-3 bg-slate-950/60 rounded-xl border border-slate-800">
                    <span class="text-slate-400">Approved Delivery Fees:</span>
                    <div class="text-sm font-bold text-slate-300 font-mono mt-0.5">
                      -{{ auditSummary()!.approvedDeliveryFees | number:'1.2-2' }} TND
                    </div>
                  </div>
                  <div class="p-3 bg-slate-950/60 rounded-xl border border-slate-800">
                    <span class="text-slate-400">Return Fee Penalties:</span>
                    <div class="text-sm font-bold text-rose-400 font-mono mt-0.5">
                      -{{ auditSummary()!.returnPenalties | number:'1.2-2' }} TND
                    </div>
                  </div>
                  <div class="p-3 bg-slate-950/60 rounded-xl border border-rose-900/40">
                    <span class="text-rose-300">Discrepancy Holdback:</span>
                    <div class="text-sm font-bold text-rose-400 font-mono mt-0.5">
                      -{{ auditSummary()!.discrepancyHoldback | number:'1.2-2' }} TND
                    </div>
                  </div>
                </div>
              </div>

              <!-- Match Audit Diff Table -->
              <div class="rounded-2xl border border-slate-800 bg-slate-900/60 overflow-hidden shadow-lg">
                <div class="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-900/80">
                  <div class="flex items-center gap-3">
                    <span class="text-xs font-semibold uppercase tracking-wider text-slate-400">Parcel Match Diff Table</span>
                    <span class="text-xs px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 font-mono">
                      {{ auditSummary()!.clearedCount }} Cleared
                    </span>
                    @if (auditSummary()!.discrepancyCount > 0) {
                      <span class="text-xs px-2 py-0.5 rounded-full bg-rose-500/20 text-rose-300 font-mono">
                        {{ auditSummary()!.discrepancyCount }} Discrepancies
                      </span>
                    }
                  </div>
                </div>

                <div class="overflow-x-auto">
                  <table class="w-full text-left text-xs">
                    <thead class="bg-slate-900 text-slate-400 uppercase text-[11px] tracking-wider border-b border-slate-800">
                      <tr>
                        <th class="p-3.5">Tracking #</th>
                        <th class="p-3.5">Order / Customer</th>
                        <th class="p-3.5">Reported Status</th>
                        <th class="p-3.5 text-right">Expected COD</th>
                        <th class="p-3.5 text-right">Reported Cash</th>
                        <th class="p-3.5 text-right">Variance</th>
                        <th class="p-3.5">Audit Match Status</th>
                        <th class="p-3.5 text-right">Dispute Holdback</th>
                        <th class="p-3.5 text-right">Action</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800/60">
                      @for (match of auditSummary()!.matches; track match.matchId) {
                        <tr class="hover:bg-slate-800/30 transition">
                          <!-- Tracking -->
                          <td class="p-3.5 font-mono font-semibold text-cyan-400">{{ match.trackingNumber }}</td>

                          <!-- Order & Customer -->
                          <td class="p-3.5">
                            <div class="font-medium text-white">{{ match.customerName }}</div>
                            <span class="text-[10px] text-slate-400 font-mono">{{ match.orderNumber }}</span>
                          </td>

                          <!-- Reported Status -->
                          <td class="p-3.5">
                            <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-slate-800 text-slate-300">
                              {{ match.reportedStatus }}
                            </span>
                          </td>

                          <!-- Expected COD -->
                          <td class="p-3.5 text-right font-mono text-slate-300">
                            {{ match.systemExpectedAmount | number:'1.2-2' }} TND
                          </td>

                          <!-- Reported Cash -->
                          <td class="p-3.5 text-right font-mono font-bold" [class.text-emerald-400]="match.cashVariance === 0">
                            {{ match.reportedCashCollected | number:'1.2-2' }} TND
                          </td>

                          <!-- Variance -->
                          <td class="p-3.5 text-right font-mono font-bold"
                            [class.text-emerald-400]="match.cashVariance === 0"
                            [class.text-rose-400]="match.cashVariance < 0"
                            [class.text-amber-400]="match.cashVariance > 0">
                            {{ match.cashVariance > 0 ? '+' : '' }}{{ match.cashVariance | number:'1.2-2' }} TND
                          </td>

                          <!-- Audit Status Badge -->
                          <td class="p-3.5">
                            @if (match.discrepancyType === 'ExactMatch' && match.isResolved) {
                              <span class="px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 text-[10px] font-medium">
                                ✓ Fully Cleared
                              </span>
                            } @else if (match.discrepancyType === 'PhantomReturn') {
                              <span class="px-2 py-0.5 rounded-full bg-purple-500/20 text-purple-300 border border-purple-500/30 text-[10px] font-medium animate-pulse">
                                👻 Phantom Return
                              </span>
                            } @else if (match.discrepancyType === 'ShortPayment') {
                              <span class="px-2 py-0.5 rounded-full bg-rose-500/20 text-rose-300 border border-rose-500/30 text-[10px] font-medium">
                                ⚠️ Short Collection
                              </span>
                            } @else if (match.discrepancyType === 'UnaccountedParcel') {
                              <span class="px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-300 border border-amber-500/30 text-[10px] font-medium">
                                ❓ Unaccounted
                              </span>
                            } @else {
                              <span class="px-2 py-0.5 rounded-full bg-slate-800 text-slate-300 text-[10px]">
                                {{ match.discrepancyType }}
                              </span>
                            }
                          </td>

                          <!-- Holdback -->
                          <td class="p-3.5 text-right font-mono text-rose-400 font-semibold">
                            {{ match.holdbackAmount > 0 ? (match.holdbackAmount | number:'1.2-2') + ' TND' : '—' }}
                          </td>

                          <!-- Action -->
                          <td class="p-3.5 text-right">
                            @if (!match.isResolved) {
                              <button
                                (click)="openResolveDiscrepancyModal(match)"
                                class="px-2.5 py-1 rounded bg-rose-500/20 hover:bg-rose-500 text-rose-200 hover:text-white border border-rose-500/30 transition text-[11px] font-medium">
                                Resolve Discrepancy
                              </button>
                            } @else {
                              <span class="text-[11px] text-slate-500 font-mono">Resolved ✓</span>
                            }
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>
            }
          </section>
        }

        <!-- Printable Manifest Slip Modal -->
        @if (selectedManifestForPrint()) {
          <div class="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div class="bg-slate-900 border border-slate-700 rounded-2xl w-full max-w-3xl max-h-[90vh] flex flex-col overflow-hidden shadow-2xl">
              <!-- Modal Header -->
              <div class="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-950">
                <div class="flex items-center gap-2">
                  <span class="text-base">📄</span>
                  <h4 class="text-sm font-bold text-white">Shipping Manifest & Bordereau d'Expédition</h4>
                </div>
                <div class="flex items-center gap-2">
                  <button
                    (click)="printManifest()"
                    class="px-3 py-1 bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5">
                    <span>🖨️ Print Slip</span>
                  </button>
                  <button
                    (click)="selectedManifestForPrint.set(null)"
                    class="text-slate-400 hover:text-white text-xs px-2 py-1">
                    ✕ Close
                  </button>
                </div>
              </div>

              <!-- Printable Area (Formatted as Official Bordereau Slip) -->
              <div id="manifest-print-area" class="p-6 overflow-y-auto space-y-6 bg-white text-slate-900 font-sans">
                <!-- Slip Header -->
                <div class="border-b-2 border-slate-900 pb-4 flex items-start justify-between">
                  <div>
                    <h2 class="text-xl font-black uppercase tracking-tight text-slate-900">SGrowing Commerce Logistics</h2>
                    <p class="text-xs text-slate-600">Bordereau d'Expédition & Shipping Manifest</p>
                    <p class="text-xs font-mono text-slate-500 mt-1">Generated: {{ selectedManifestForPrint()!.createdAt }}</p>
                  </div>
                  <div class="text-right">
                    <!-- Barcode Representation -->
                    <div class="font-mono text-2xl tracking-widest text-slate-900 font-black">
                      {{ selectedManifestForPrint()!.barcodeString }}
                    </div>
                    <span class="text-xs font-mono text-slate-700 font-bold">{{ selectedManifestForPrint()!.manifestNumber }}</span>
                  </div>
                </div>

                <!-- Meta Specs -->
                <div class="grid grid-cols-3 gap-4 text-xs bg-slate-100 p-3 rounded-lg border border-slate-300">
                  <div>
                    <span class="text-slate-500">Designated Carrier:</span>
                    <div class="font-bold text-slate-900">{{ selectedManifestForPrint()!.courierName }}</div>
                  </div>
                  <div>
                    <span class="text-slate-500">Total Parcel Count:</span>
                    <div class="font-bold text-slate-900">{{ selectedManifestForPrint()!.orderCount }} Parcels</div>
                  </div>
                  <div>
                    <span class="text-slate-500">Total Declared COD Value:</span>
                    <div class="font-bold text-slate-900">{{ selectedManifestForPrint()!.totalCashAmount | number:'1.2-2' }} TND</div>
                  </div>
                </div>

                <!-- Parcel Run Sheet Table -->
                <table class="w-full text-left text-xs border border-slate-300">
                  <thead class="bg-slate-200 text-slate-700 uppercase text-[10px] border-b border-slate-300">
                    <tr>
                      <th class="p-2 border-r border-slate-300">#</th>
                      <th class="p-2 border-r border-slate-300">Airway Bill (AWB)</th>
                      <th class="p-2 border-r border-slate-300">Customer & Destination</th>
                      <th class="p-2 border-r border-slate-300">Phone</th>
                      <th class="p-2 text-right">Declared COD</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-300">
                    @for (parcel of selectedManifestForPrint()!.parcels; track parcel.trackingNumber; let idx = $index) {
                      <tr>
                        <td class="p-2 border-r border-slate-300 font-mono">{{ idx + 1 }}</td>
                        <td class="p-2 border-r border-slate-300 font-mono font-bold">
                          {{ parcel.trackingNumber }}
                          <div class="text-[9px] text-slate-500">{{ parcel.awbBarcode }}</div>
                        </td>
                        <td class="p-2 border-r border-slate-300">
                          <div class="font-bold text-slate-900">{{ parcel.customerName }}</div>
                          <div class="text-[10px] text-slate-600">{{ parcel.destination }}</div>
                        </td>
                        <td class="p-2 border-r border-slate-300 font-mono">{{ parcel.phone }}</td>
                        <td class="p-2 text-right font-mono font-bold text-slate-900">
                          {{ parcel.codAmount | number:'1.2-2' }} TND
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>

                <!-- Official Signature & Stamp Lines -->
                <div class="grid grid-cols-2 gap-8 pt-8 border-t border-slate-300 text-xs">
                  <div class="border border-slate-400 rounded-lg p-4 h-28 flex flex-col justify-between">
                    <span class="font-bold text-slate-700">Warehouse Fulfillment Agent Signature:</span>
                    <span class="text-[10px] text-slate-500">I certify all listed parcels are packaged and sealed.</span>
                  </div>
                  <div class="border border-slate-400 rounded-lg p-4 h-28 flex flex-col justify-between">
                    <span class="font-bold text-slate-700">Courier Driver Pickup Signature & Stamp:</span>
                    <span class="text-[10px] text-slate-500">Received above parcels in sound condition.</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        }

        <!-- Discrepancy Resolution Modal -->
        @if (activeDiscrepancyForResolution()) {
          <div class="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
            <div class="bg-slate-900 border border-slate-700 rounded-2xl w-full max-w-lg p-6 space-y-5 shadow-2xl">
              <div>
                <h4 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>⚖️ Resolve Remittance Discrepancy</span>
                </h4>
                <p class="text-xs text-slate-400 mt-1">
                  Tracking: <span class="font-mono text-cyan-400 font-semibold">{{ activeDiscrepancyForResolution()!.trackingNumber }}</span>
                  • Variance: <span class="font-mono text-rose-400 font-semibold">{{ activeDiscrepancyForResolution()!.cashVariance | number:'1.2-2' }} TND</span>
                </p>
              </div>

              <!-- Reason Code Selector -->
              <div class="space-y-1.5">
                <label class="block text-xs font-medium text-slate-300">Resolution Reason Code</label>
                <select
                  [(ngModel)]="resolutionReasonCode"
                  class="w-full bg-slate-950 border border-slate-700 rounded-xl px-3 py-2 text-xs text-white">
                  <option value="Courier clerical error">Courier clerical error (Carrier wire transferred difference)</option>
                  <option value="Parcel lost in transit">Parcel lost in transit (Insurance claim initiated)</option>
                  <option value="Merchant approved partial payment">Merchant approved partial payment / discount</option>
                  <option value="Customer confirmed non-receipt (Phantom Return Dispute)">Phantom Return Conceded by Courier</option>
                  <option value="Packaging damaged on return">Damaged return goods credited to merchant</option>
                </select>
              </div>

              <!-- Resolution Audit Notes -->
              <div class="space-y-1.5">
                <label class="block text-xs font-medium text-slate-300">Audit Notes & Proof Reference</label>
                <textarea
                  [(ngModel)]="resolutionNotes"
                  rows="3"
                  placeholder="Enter bank transfer reference, phone verification log, or courier concession ticket #..."
                  class="w-full bg-slate-950 border border-slate-700 rounded-xl p-3 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-cyan-500">
                </textarea>
              </div>

              <!-- Modal Buttons -->
              <div class="flex items-center justify-end gap-3 pt-2">
                <button
                  (click)="activeDiscrepancyForResolution.set(null)"
                  class="px-3 py-1.5 rounded-lg text-xs text-slate-400 hover:text-white transition">
                  Cancel
                </button>
                <button
                  (click)="confirmResolveDiscrepancy()"
                  class="px-4 py-1.5 rounded-lg text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 text-white transition shadow-md shadow-emerald-600/20">
                  Release Holdback & Confirm
                </button>
              </div>
            </div>
          </div>
        }
      </main>
    </div>
  `
})
export class CarrierReconciliationCockpitComponent implements OnInit {
  private http = inject(HttpClient);

  // Active Tab
  activeTab = signal<'matrix' | 'manifests' | 'remittance'>('matrix');

  // Country & Governorate Signals
  selectedCountry = signal<'TN' | 'EG'>('TN');
  selectedGovernorate = signal<string>('Tunis');
  simulatedCodAmount = signal<number>(85.0);

  // Financial KPIs
  kpis = signal<FinancialKpis>({
    totalCodInTransit: 14250.0,
    totalCashCollectedByCouriers: 18920.0,
    clearedMerchantSettlement: 16450.0,
    unresolvedDiscrepanciesCount: 3,
    unresolvedDiscrepanciesValue: 310.0
  });

  // Toast
  toastMessage = signal<string | null>(null);

  // Auto-Routing Matrix
  matrixCarriers = signal<CourierRoutingScore[]>([]);

  // Manifest Generator
  selectedCourierForManifest = signal<string>('ARX');
  pendingOrders = signal<ConfirmedOrderRow[]>([]);
  manifestBatches = signal<ShippingManifestItem[]>([]);
  selectedManifestForPrint = signal<ShippingManifestItem | null>(null);

  // Remittance Reconciler
  statementRef = signal<string>('STM-LVR-2026-W44');
  reconcileCourierCode = signal<string>('LVR');
  csvInputText = signal<string>(`TrackingNumber,ReportedStatus,CashCollected,CourierFee,Notes
AWB-LVR-20261029-0001,Delivered,90.00,7.00,Full payment collected
AWB-LVR-20261029-0002,Delivered,120.00,7.00,Driver short collected 30 DT
AWB-FST-20261029-7777,Customer Refused,0.00,3.00,Driver claimed customer refused`);

  auditSummary = signal<RemittanceAuditSummary | null>(null);

  // Discrepancy Resolution Modal
  activeDiscrepancyForResolution = signal<RemittanceMatchRow | null>(null);
  resolutionReasonCode = signal<string>('Courier clerical error');
  resolutionNotes = signal<string>('');

  readonly tunisianGovernorates = [
    'Tunis', 'Ariana', 'Ben Arous', 'Manouba', 'Nabeul', 'Zaghouan',
    'Bizerte', 'Beja', 'Jendouba', 'Kef', 'Siliana', 'Sousse',
    'Monastir', 'Mahdia', 'Sfax', 'Kairouan', 'Kasserine', 'Sidi Bouzid',
    'Gabes', 'Medenine', 'Tataouine', 'Gafsa', 'Tozeur', 'Kebili'
  ];

  readonly egyptianGovernorates = [
    'Cairo', 'Giza', 'Alexandria', 'Qalyubia', 'Sharqia', 'Dakahlia',
    'Gharbia', 'Port Said', 'Suez', 'Asyut', 'Sohag', 'Aswan'
  ];

  currentGovernorates = computed(() => {
    return this.selectedCountry() === 'TN' ? this.tunisianGovernorates : this.egyptianGovernorates;
  });

  selectedOrderIds = computed(() => {
    return this.pendingOrders().filter(o => o.selected).map(o => o.id);
  });

  isAllSelected = computed(() => {
    const orders = this.pendingOrders();
    return orders.length > 0 && orders.every(o => o.selected);
  });

  ngOnInit() {
    this.recomputeMatrix();
    this.seedSamplePendingOrders();
    this.seedSampleManifests();
    this.reconcileStatement(); // Run initial reconciliation sample
  }

  switchCountry(country: 'TN' | 'EG') {
    this.selectedCountry.set(country);
    this.selectedGovernorate.set(country === 'TN' ? 'Tunis' : 'Cairo');
    this.simulatedCodAmount.set(country === 'TN' ? 85.0 : 450.0);
    this.recomputeMatrix();
  }

  selectGovernorate(gov: string) {
    this.selectedGovernorate.set(gov);
    this.recomputeMatrix();
  }

  recomputeMatrix() {
    const gov = this.selectedGovernorate();
    const cod = this.simulatedCodAmount();
    const country = this.selectedCountry();

    if (country === 'TN') {
      const isGrandTunis = ['Tunis', 'Ariana', 'Ben Arous', 'Manouba'].includes(gov);
      const isSouth = ['Tataouine', 'Medenine', 'Kebili', 'Tozeur', 'Gafsa'].includes(gov);
      const isSahelOrSfax = ['Sousse', 'Monastir', 'Mahdia', 'Sfax'].includes(gov);

      const fstRate = isGrandTunis ? 0.94 : 0.83;
      const fstFee = isGrandTunis ? 6.5 : 7.5;
      const fstNet = fstRate * cod - fstFee - (1 - fstRate) * 3.0;

      const arxRate = isSouth ? 0.92 : 0.87;
      const arxFee = isSouth ? 8.5 : 8.0;
      const arxNet = arxRate * cod - arxFee - (1 - arxRate) * 4.0;

      const lvrRate = isSahelOrSfax ? 0.93 : 0.86;
      const lvrFee = 7.0;
      const lvrNet = lvrRate * cod - lvrFee - (1 - lvrRate) * 3.5;

      const expRate = 0.88;
      const expFee = 7.5;
      const expNet = expRate * cod - expFee - (1 - expRate) * 3.5;

      const carriers: CourierRoutingScore[] = [
        {
          courierProviderId: 'c1-fst',
          courierName: 'First Delivery',
          apiCode: 'FST',
          destinationGovernorate: gov,
          countryCode: 'TN',
          historicalDeliveryRatePercent: Math.round(fstRate * 100),
          baseDeliveryFee: fstFee,
          returnFeePenalty: 3.0,
          averageTransitDays: 1.2,
          codAmount: cod,
          expectedNetValue: Math.round(fstNet * 100) / 100,
          formulaExpression: `(${fstRate.toFixed(2)} * ${cod}) - ${fstFee} - (${(1 - fstRate).toFixed(2)} * 3.0)`,
          recommendationReason: isGrandTunis ? 'Dominant high-velocity leader in Grand Tunis with lowest base fee.' : 'Standard national coverage.'
        },
        {
          courierProviderId: 'c2-arx',
          courierName: 'Aramex Tunisia',
          apiCode: 'ARX',
          destinationGovernorate: gov,
          countryCode: 'TN',
          historicalDeliveryRatePercent: Math.round(arxRate * 100),
          baseDeliveryFee: arxFee,
          returnFeePenalty: 4.0,
          averageTransitDays: 1.8,
          codAmount: cod,
          expectedNetValue: Math.round(arxNet * 100) / 100,
          formulaExpression: `(${arxRate.toFixed(2)} * ${cod}) - ${arxFee} - (${(1 - arxRate).toFixed(2)} * 4.0)`,
          recommendationReason: isSouth ? 'Highest delivery success in South Tunisia remote areas.' : 'Full national coverage with high return penalty.'
        },
        {
          courierProviderId: 'c3-lvr',
          courierName: 'Livri Express',
          apiCode: 'LVR',
          destinationGovernorate: gov,
          countryCode: 'TN',
          historicalDeliveryRatePercent: Math.round(lvrRate * 100),
          baseDeliveryFee: lvrFee,
          returnFeePenalty: 3.5,
          averageTransitDays: 1.4,
          codAmount: cod,
          expectedNetValue: Math.round(lvrNet * 100) / 100,
          formulaExpression: `(${lvrRate.toFixed(2)} * ${cod}) - ${lvrFee} - (${(1 - lvrRate).toFixed(2)} * 3.5)`,
          recommendationReason: isSahelOrSfax ? 'Highest delivery success in Sahel coastal hubs and Sfax.' : 'Regional courier with fast transit.'
        },
        {
          courierProviderId: 'c4-exp',
          courierName: 'Express Courrier',
          apiCode: 'EXP',
          destinationGovernorate: gov,
          countryCode: 'TN',
          historicalDeliveryRatePercent: Math.round(expRate * 100),
          baseDeliveryFee: expFee,
          returnFeePenalty: 3.5,
          averageTransitDays: 1.5,
          codAmount: cod,
          expectedNetValue: Math.round(expNet * 100) / 100,
          formulaExpression: `(${expRate.toFixed(2)} * ${cod}) - ${expFee} - (${(1 - expRate).toFixed(2)} * 3.5)`,
          recommendationReason: 'Balanced northern corridor logistics with daily warehouse pickups.'
        }
      ];

      this.matrixCarriers.set(carriers.sort((a, b) => b.expectedNetValue - a.expectedNetValue));
    } else {
      // Egypt
      const bstRate = 0.91;
      const bstFee = 45.0;
      const bstNet = bstRate * cod - bstFee - (1 - bstRate) * 20.0;

      const mylRate = 0.88;
      const mylFee = 48.0;
      const mylNet = mylRate * cod - mylFee - (1 - mylRate) * 22.0;

      const carriers: CourierRoutingScore[] = [
        {
          courierProviderId: 'eg1-bst',
          courierName: 'Bosta Egypt',
          apiCode: 'BST',
          destinationGovernorate: gov,
          countryCode: 'EG',
          historicalDeliveryRatePercent: 91,
          baseDeliveryFee: 45.0,
          returnFeePenalty: 20.0,
          averageTransitDays: 1.2,
          codAmount: cod,
          expectedNetValue: Math.round(bstNet * 100) / 100,
          formulaExpression: `(0.91 * ${cod}) - 45 - (0.09 * 20)`,
          recommendationReason: 'Dominant leader in Greater Cairo & Alexandria.'
        },
        {
          courierProviderId: 'eg2-myl',
          courierName: 'Mylerz Delivery',
          apiCode: 'MYL',
          destinationGovernorate: gov,
          countryCode: 'EG',
          historicalDeliveryRatePercent: 88,
          baseDeliveryFee: 48.0,
          returnFeePenalty: 22.0,
          averageTransitDays: 1.5,
          codAmount: cod,
          expectedNetValue: Math.round(mylNet * 100) / 100,
          formulaExpression: `(0.88 * ${cod}) - 48 - (0.12 * 22)`,
          recommendationReason: 'Reliable Delta region delivery with real-time tracking.'
        }
      ];
      this.matrixCarriers.set(carriers.sort((a, b) => b.expectedNetValue - a.expectedNetValue));
    }
  }

  simulateAutoRouteOrder(carrier: CourierRoutingScore) {
    this.toastMessage.set(`Simulated Auto-Dispatch: Order routed to ${carrier.courierName} (E[Net]=+${carrier.expectedNetValue.toFixed(2)} TND, Success=${carrier.historicalDeliveryRatePercent}%).`);
  }

  toggleSelectAll() {
    const targetState = !this.isAllSelected();
    this.pendingOrders.update(orders => orders.map(o => ({ ...o, selected: targetState })));
  }

  generateCourierManifest() {
    const selected = this.pendingOrders().filter(o => o.selected);
    if (selected.length === 0) return;

    const courierCode = this.selectedCourierForManifest();
    const courierNames: Record<string, string> = {
      'ARX': 'Aramex Tunisia',
      'FST': 'First Delivery',
      'LVR': 'Livri Express',
      'EXP': 'Express Courrier'
    };

    const today = new Date().toISOString().slice(0, 10).replace(/-/g, '');
    const randomHex = Math.random().toString(36).substring(2, 7).toUpperCase();
    const manifestNumber = `MNF-${today}-${randomHex}`;

    const parcels: ManifestParcel[] = selected.map((order, index) => {
      const seq = (index + 1).toString().padStart(4, '0');
      const tracking = `AWB-${courierCode}-${today}-${seq}`;
      return {
        orderId: order.id,
        orderNumber: order.orderNumber,
        customerName: order.customerName,
        phone: order.phone,
        destination: `${order.governorate}, ${order.delegation} (${order.address})`,
        trackingNumber: tracking,
        awbBarcode: `*${tracking}*`,
        codAmount: order.codAmount,
        freightFee: 7.0
      };
    });

    const totalCash = parcels.reduce((sum, p) => sum + p.codAmount, 0);
    const totalFreight = parcels.length * 7.0;

    const newManifest: ShippingManifestItem = {
      id: 'mnf-' + Date.now(),
      manifestNumber,
      courierName: courierNames[courierCode] || 'Regional Courier',
      courierCode,
      orderCount: parcels.length,
      totalCashAmount: totalCash,
      expectedFreightCost: totalFreight,
      status: 'PendingPickup',
      createdAt: new Date().toISOString().replace('T', ' ').slice(0, 16),
      barcodeString: `*${manifestNumber}*`,
      parcels
    };

    // Prepend to manifest history
    this.manifestBatches.update(list => [newManifest, ...list]);

    // Remove dispatched orders from pending
    const dispatchedIds = new Set(selected.map(o => o.id));
    this.pendingOrders.update(orders => orders.filter(o => !dispatchedIds.has(o.id)));

    this.toastMessage.set(`Successfully generated locked manifest ${manifestNumber} with ${parcels.length} parcels (${totalCash.toFixed(2)} TND COD).`);
  }

  openManifestSlip(manifest: ShippingManifestItem) {
    this.selectedManifestForPrint.set(manifest);
  }

  printManifest() {
    window.print();
  }

  loadSampleCsv(type: 'livri' | 'aramex_phantom') {
    if (type === 'livri') {
      this.statementRef.set('BVR-LVR-2026-W44');
      this.reconcileCourierCode.set('LVR');
      this.csvInputText.set(`TrackingNumber,ReportedStatus,CashCollected,CourierFee,Notes
AWB-LVR-20261029-0001,Delivered,90.00,7.00,Cash fully cleared
AWB-LVR-20261029-0002,Delivered,120.00,7.00,Short payment 30 DT by driver
AWB-LVR-20261029-0003,Returned,0.00,3.50,Legitimate customer refusal`);
    } else {
      this.statementRef.set('BVR-ARX-2026-W44');
      this.reconcileCourierCode.set('ARX');
      this.csvInputText.set(`TrackingNumber,ReportedStatus,CashCollected,CourierFee,Notes
AWB-ARX-20261029-0001,Delivered,120.00,8.00,Delivered to customer
AWB-FST-20261029-7777,Customer Refused,0.00,4.00,Customer refused package
AWB-UNKNOWN-9999,Delivered,85.00,8.00,Unaccounted parcel`);
    }
    this.reconcileStatement();
  }

  reconcileStatement() {
    const lines = this.csvInputText().trim().split('\n').filter(l => l.trim().length > 0);
    if (lines.length <= 1) return;

    const matches: RemittanceMatchRow[] = [];
    let grossCollected = 0;
    let approvedFees = 0;
    let returnPenalties = 0;
    let holdback = 0;
    let clearedCount = 0;
    let discrepancyCount = 0;

    for (let i = 1; i < lines.length; i++) {
      const parts = lines[i].split(',').map(p => p.trim());
      if (parts.length < 3) continue;

      const tracking = parts[0];
      const status = parts[1];
      const cash = parseFloat(parts[2]) || 0;
      const fee = parseFloat(parts[3]) || 7.0;
      const notes = parts[4] || '';

      grossCollected += cash;

      // Realistic matching simulation
      let expected = 90.0;
      let orderNum = 'ORD-MLT-' + tracking.slice(-4);
      let customer = 'Karim Ben Salem';
      let matchType: 'ExactMatch' | 'ShortPayment' | 'PhantomReturn' | 'UnaccountedParcel' = 'ExactMatch';
      let isResolved = false;
      let rowHoldback = 0;
      let disputeTicket: string | undefined = undefined;

      if (tracking.includes('7777') || notes.toLowerCase().includes('refused')) {
        // Phantom return detection
        matchType = 'PhantomReturn';
        expected = 110.0;
        customer = 'Hamza Bouazizi';
        rowHoldback = 110.0;
        holdback += rowHoldback;
        discrepancyCount++;
        disputeTicket = 'DISP-PHANTOM-' + tracking;
      } else if (tracking.includes('0002') || (cash < expected && status === 'Delivered')) {
        // Short payment variance
        matchType = 'ShortPayment';
        expected = 150.0;
        customer = 'Meriem Chaari';
        rowHoldback = Math.abs(cash - expected);
        holdback += rowHoldback;
        discrepancyCount++;
        disputeTicket = 'DISP-VAR-' + tracking;
      } else if (tracking.includes('UNKNOWN')) {
        // Unaccounted parcel
        matchType = 'UnaccountedParcel';
        expected = 0.0;
        customer = 'Unrecognized Buyer';
        rowHoldback = cash;
        holdback += rowHoldback;
        discrepancyCount++;
        disputeTicket = 'DISP-UNACC-' + tracking;
      } else if (status.toLowerCase().includes('return')) {
        // Legitimate return
        matchType = 'ExactMatch';
        isResolved = true;
        clearedCount++;
        returnPenalties += 3.5;
      } else {
        // Fully Cleared Match
        matchType = 'ExactMatch';
        isResolved = true;
        clearedCount++;
        approvedFees += fee;
      }

      const variance = cash - expected;

      matches.push({
        matchId: 'match-' + i,
        trackingNumber: tracking,
        orderNumber: orderNum,
        customerName: customer,
        reportedStatus: status,
        reportedCashCollected: cash,
        systemExpectedAmount: expected,
        cashVariance: variance,
        courierFeeDeducted: fee,
        discrepancyType: matchType,
        disputeTicketNumber: disputeTicket,
        holdbackAmount: rowHoldback,
        resolutionNotes: notes,
        isResolved
      });
    }

    // Formula: TotalCollectedCash - ApprovedDeliveryFees - ReturnPenalties - DiscrepancyHoldback
    const netPayout = grossCollected - approvedFees - returnPenalties - holdback;

    this.auditSummary.set({
      statementReference: this.statementRef(),
      courierName: this.reconcileCourierCode() === 'LVR' ? 'Livri Express' : 'Aramex Tunisia',
      totalParcels: matches.length,
      totalCollectedCash: grossCollected,
      approvedDeliveryFees: approvedFees,
      returnPenalties,
      discrepancyHoldback: holdback,
      netCashSettlement: netPayout,
      clearedCount,
      discrepancyCount,
      matches
    });
  }

  openResolveDiscrepancyModal(match: RemittanceMatchRow) {
    this.activeDiscrepancyForResolution.set(match);
    this.resolutionReasonCode.set('Courier clerical error');
    this.resolutionNotes.set('');
  }

  confirmResolveDiscrepancy() {
    const active = this.activeDiscrepancyForResolution();
    if (!active) return;

    this.auditSummary.update(summary => {
      if (!summary) return null;

      const updatedMatches = summary.matches.map(m => {
        if (m.matchId === active.matchId) {
          return {
            ...m,
            isResolved: true,
            holdbackAmount: 0,
            resolutionNotes: `Resolved: ${this.resolutionReasonCode()} | ${this.resolutionNotes()}`
          };
        }
        return m;
      });

      // Recalculate holdback and net settlement
      const newHoldback = updatedMatches.reduce((sum, m) => sum + (m.isResolved ? 0 : m.holdbackAmount), 0);
      const newNetPayout = summary.totalCollectedCash - summary.approvedDeliveryFees - summary.returnPenalties - newHoldback;
      const newCleared = updatedMatches.filter(m => m.isResolved).length;
      const newDiscrepancies = updatedMatches.filter(m => !m.isResolved).length;

      return {
        ...summary,
        discrepancyHoldback: newHoldback,
        netCashSettlement: newNetPayout,
        clearedCount: newCleared,
        discrepancyCount: newDiscrepancies,
        matches: updatedMatches
      };
    });

    this.toastMessage.set(`Discrepancy on ${active.trackingNumber} resolved. Holdback released into cleared settlement.`);
    this.activeDiscrepancyForResolution.set(null);
  }

  private seedSamplePendingOrders() {
    this.pendingOrders.set([
      {
        id: 'ord-101',
        orderNumber: 'MLT-TN-88231',
        customerName: 'Karim Ben Salem',
        phone: '+216 98 123 456',
        governorate: 'Tunis',
        delegation: 'La Marsa',
        address: 'Avenue Habib Bourguiba',
        codAmount: 98.0,
        confirmedAt: '2026-10-29 09:15',
        selected: true
      },
      {
        id: 'ord-102',
        orderNumber: 'MLT-TN-88232',
        customerName: 'Sarra Mansour',
        phone: '+216 22 987 654',
        governorate: 'Ariana',
        delegation: 'Ennasr',
        address: 'Résidence Les Jasmins',
        codAmount: 145.0,
        confirmedAt: '2026-10-29 09:40',
        selected: true
      },
      {
        id: 'ord-103',
        orderNumber: 'MLT-TN-88233',
        customerName: 'Youssef Gharbi',
        phone: '+216 50 444 333',
        governorate: 'Sousse',
        delegation: 'Kantaoui',
        address: 'Villa 12, Rue des Palmiers',
        codAmount: 75.0,
        confirmedAt: '2026-10-29 10:05',
        selected: true
      },
      {
        id: 'ord-104',
        orderNumber: 'MLT-TN-88234',
        customerName: 'Mariem Chaari',
        phone: '+216 25 888 777',
        governorate: 'Sfax',
        delegation: 'Sakiet Ezzit',
        address: 'Route de Tunis Km 5',
        codAmount: 110.0,
        confirmedAt: '2026-10-29 10:30',
        selected: false
      },
      {
        id: 'ord-105',
        orderNumber: 'MLT-TN-88235',
        customerName: 'Anis Ben Amor',
        phone: '+216 93 222 111',
        governorate: 'Tataouine',
        delegation: 'Tataouine Sud',
        address: 'Centre Ville',
        codAmount: 130.0,
        confirmedAt: '2026-10-29 11:00',
        selected: false
      }
    ]);
  }

  private seedSampleManifests() {
    this.manifestBatches.set([
      {
        id: 'mnf-prev-1',
        manifestNumber: 'MNF-20261028-A4F1D',
        courierName: 'Aramex Tunisia',
        courierCode: 'ARX',
        orderCount: 18,
        totalCashAmount: 1890.0,
        expectedFreightCost: 144.0,
        status: 'Dispatched',
        createdAt: '2026-10-28 16:30',
        barcodeString: '*MNF-20261028-A4F1D*',
        parcels: []
      },
      {
        id: 'mnf-prev-2',
        manifestNumber: 'MNF-20261029-B92K0',
        courierName: 'Livri Express',
        courierCode: 'LVR',
        orderCount: 12,
        totalCashAmount: 1140.0,
        expectedFreightCost: 84.0,
        status: 'PendingPickup',
        createdAt: '2026-10-29 10:15',
        barcodeString: '*MNF-20261029-B92K0*',
        parcels: []
      }
    ]);
  }
}
