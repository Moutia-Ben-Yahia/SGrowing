import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface CourierFloatUi {
  courierName: string;
  inTransitParcelCount: number;
  grossCashValue: number;
  estimatedFees: number;
  netReceivableCash: number;
  averageTurnaroundDays: number;
  remittanceFrequency: string;
  clearanceSchedulePattern: string;
  badgeColor: string;
}

export interface ReceivableItemUi {
  invoiceId: string;
  customerName: string;
  amount: number;
  dueDate: string;
  daysPastDue: number;
  agingBucket: string;
  collectionProbability: number;
  expectedRecovery: number;
}

export interface PayableItemUi {
  purchaseOrderId: string;
  vendorName: string;
  amount: number;
  dueDate: string;
  terms: string;
  status: string;
}

export interface DailyTrajectoryPointUi {
  dayIndex: number;
  dateStr: string;
  openingBalance: number;
  inflowDirectSales: number;
  inflowCourierRemittance: number;
  inflowReceivables: number;
  inflowOther: number;
  totalInflow: number;
  outflowPayables: number;
  outflowPayroll: number;
  outflowRentLease: number;
  outflowSaasOverhead: number;
  outflowOther: number;
  totalOutflow: number;
  netDailyFlow: number;
  closingBalance: number;
  safetyThreshold: number;
  isDeficit: boolean;
  deficitGap: number;
}

export interface ManualAdjustmentUi {
  id: string;
  dateStr: string;
  amount: number;
  category: string;
  description: string;
}

@Component({
  selector: 'app-cashflow-forecasting-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div dir="ltr" class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Left Sidebar -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4 flex-shrink-0">
        <div>
          <!-- Brand & Horizon Header -->
          <div class="flex items-center gap-2.5 mb-6">
            <div class="h-9 w-9 rounded-xl bg-gradient-to-tr from-emerald-500 to-teal-400 flex items-center justify-center font-bold text-white shadow-lg shadow-emerald-500/25">
              💎
            </div>
            <div>
              <h1 class="text-sm font-bold tracking-tight text-white">SGrowing Treasury</h1>
              <p class="text-[11px] text-emerald-400 font-medium">Horizon 26 Cash & Float</p>
            </div>
          </div>

          <!-- Navigation Links -->
          <div class="space-y-1 mb-6">
            <span class="text-[10px] font-semibold text-slate-400 uppercase tracking-wider px-2">Finance & Treasury</span>
            <div class="space-y-1 mt-1">
              <a class="w-full flex items-center gap-2 px-3 py-2 rounded-lg text-xs bg-emerald-500/15 text-emerald-300 border border-emerald-500/30 font-medium shadow-sm">
                <span>🌊 90-Day Float Engine</span>
              </a>
              <a routerLink="/forecasting" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📈 Sales Forecast & Quotas</span>
              </a>
              <a routerLink="/contracts" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📜 Courier SLA Contracts</span>
              </a>
              <a routerLink="/checkout" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>🛒 COD Checkout & Gateway</span>
              </a>
            </div>
          </div>

          <!-- Autonomous Float Guardian Badge -->
          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-emerald-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-emerald-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-emerald-400 animate-pulse"></span>
                Weibull Clearance
              </span>
              <span class="text-[10px] bg-emerald-500/10 text-emerald-300 border border-emerald-500/20 px-1.5 py-0.5 rounded">Active</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Predictive carrier remittance schedules model COD in-transit lag, supplier Net 30/60 PO obligations, and instant factoring liquidity injection.
            </p>
          </div>
        </div>

        <!-- Simulation Horizon & Safe Threshold Controls -->
        <div class="p-3 bg-slate-900/90 border border-slate-800 rounded-xl space-y-3">
          <div class="flex items-center justify-between">
            <span class="text-[11px] text-slate-400 font-medium">Safe Cash Buffer</span>
            <span class="text-xs font-semibold text-amber-400">\${{ safetyThreshold().toLocaleString() }}</span>
          </div>
          <input
            type="range"
            min="5000"
            max="30000"
            step="1000"
            [ngModel]="safetyThreshold()"
            (ngModelChange)="safetyThreshold.set($event)"
            class="w-full accent-emerald-500 cursor-pointer h-1.5 bg-slate-700 rounded-lg"
          />
          <div class="flex items-center justify-between text-[10px] text-slate-500">
            <span>\$5,000</span>
            <span>\$15,000</span>
            <span>\$30,000</span>
          </div>
        </div>
      </aside>

      <!-- Main Stage Viewport -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <!-- Header Bar -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between flex-shrink-0">
          <div>
            <div class="flex items-center gap-3">
              <h2 class="text-base font-bold text-white tracking-tight">Autonomous Cash Flow Forecasting & Working Capital Float Engine</h2>
              <span class="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-500/10 text-emerald-300 border border-emerald-500/20">
                PRD §4.16 Engine
              </span>
            </div>
            <p class="text-xs text-slate-400">90-day multi-carrier float trajectory, Weibull delay clearance, CCC optimization, and liquidity drawdown simulator.</p>
          </div>

          <div class="flex items-center gap-3">
            <!-- Horizon Selector Toggle -->
            <div class="flex items-center bg-slate-900 p-0.5 rounded-lg border border-slate-800 text-xs">
              <button
                (click)="selectedHorizon.set(30)"
                [class.bg-emerald-600]="selectedHorizon() === 30"
                [class.text-white]="selectedHorizon() === 30"
                [class.text-slate-400]="selectedHorizon() !== 30"
                class="px-2.5 py-1 rounded-md font-medium transition">
                30 Days
              </button>
              <button
                (click)="selectedHorizon.set(60)"
                [class.bg-emerald-600]="selectedHorizon() === 60"
                [class.text-white]="selectedHorizon() === 60"
                [class.text-slate-400]="selectedHorizon() !== 60"
                class="px-2.5 py-1 rounded-md font-medium transition">
                60 Days
              </button>
              <button
                (click)="selectedHorizon.set(90)"
                [class.bg-emerald-600]="selectedHorizon() === 90"
                [class.text-white]="selectedHorizon() === 90"
                [class.text-slate-400]="selectedHorizon() !== 90"
                class="px-2.5 py-1 rounded-md font-medium transition">
                90 Days
              </button>
            </div>

            <!-- Action Buttons -->
            <button
              (click)="openAdjustmentModal.set(true)"
              class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-sm">
              <span>➕ Record Adjustment</span>
            </button>
            <button
              (click)="openFactoringDrawer.set(true)"
              class="px-3 py-1.5 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-emerald-500/20 transition flex items-center gap-1.5">
              <span>⚡ Factor Float Now</span>
            </button>
          </div>
        </header>

        <!-- Body Scrollable Content -->
        <div class="flex-1 overflow-y-auto p-6 space-y-6">
          <!-- KPI Cards Grid -->
          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            <!-- 1. Liquid Cash Reserve -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4 relative overflow-hidden group hover:border-emerald-500/40 transition">
              <div class="flex items-center justify-between mb-1">
                <span class="text-xs text-slate-400 font-medium">Liquid Cash Reserve</span>
                <span class="text-[10px] bg-emerald-500/10 text-emerald-300 border border-emerald-500/20 px-1.5 py-0.5 rounded font-mono">Current</span>
              </div>
              <div class="text-2xl font-bold text-white tracking-tight">\${{ startingReserve().toLocaleString() }}</div>
              <div class="mt-2 flex items-center gap-2 text-xs">
                <span class="text-emerald-400 font-medium flex items-center gap-1">
                  <span>▲</span> +12.4%
                </span>
                <span class="text-slate-500">vs. last month</span>
              </div>
            </div>

            <!-- 2. In-Transit Courier Float -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4 relative overflow-hidden group hover:border-teal-500/40 transition">
              <div class="flex items-center justify-between mb-1">
                <span class="text-xs text-slate-400 font-medium">In-Transit Courier Float</span>
                <span class="text-[10px] bg-teal-500/10 text-teal-300 border border-teal-500/20 px-1.5 py-0.5 rounded font-mono">450 parcels</span>
              </div>
              <div class="text-2xl font-bold text-teal-300 tracking-tight">\${{ totalCourierFloat().toLocaleString() }}</div>
              <div class="mt-2 flex items-center justify-between text-xs text-slate-400">
                <span>Trapped Liquidity: <strong class="text-amber-400">{{ trappedLiquidityRatio() }}%</strong></span>
                <span class="text-[11px] text-slate-500">Avg: 5.2d lag</span>
              </div>
            </div>

            <!-- 3. Cash Conversion Cycle (CCC) -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4 relative overflow-hidden group hover:border-cyan-500/40 transition">
              <div class="flex items-center justify-between mb-1">
                <span class="text-xs text-slate-400 font-medium">Cash Conversion Cycle (CCC)</span>
                <span class="text-[10px] bg-cyan-500/10 text-cyan-300 border border-cyan-500/20 px-1.5 py-0.5 rounded font-mono">Quarterly</span>
              </div>
              <div class="text-2xl font-bold text-cyan-300 tracking-tight">{{ cccDays() }} <span class="text-xs font-normal text-slate-400">Days</span></div>
              <div class="mt-2 text-[11px] text-slate-400 flex items-center gap-2">
                <span>DIO: <strong class="text-slate-200">44.3d</strong></span>
                <span>+ DSO: <strong class="text-slate-200">26.5d</strong></span>
                <span>- DPO: <strong class="text-slate-200">38.2d</strong></span>
              </div>
            </div>

            <!-- 4. Projected 90-Day Low Point (Trough) -->
            <div class="bg-slate-900/80 border rounded-xl p-4 relative overflow-hidden transition"
              [class.border-red-500-30]="deficitDaysCount() > 0"
              [class.border-emerald-500-30]="deficitDaysCount() === 0">
              <div class="flex items-center justify-between mb-1">
                <span class="text-xs text-slate-400 font-medium">90-Day Lowest Balance</span>
                @if (deficitDaysCount() > 0) {
                  <span class="text-[10px] bg-amber-500/10 text-amber-400 border border-amber-500/20 px-1.5 py-0.5 rounded font-semibold animate-pulse">
                    ⚠️ {{ deficitDaysCount() }} Deficit Days
                  </span>
                } @else {
                  <span class="text-[10px] bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 px-1.5 py-0.5 rounded font-semibold">
                    ✓ Above Safe Buffer
                  </span>
                }
              </div>
              <div class="text-2xl font-bold tracking-tight"
                [class.text-amber-400]="deficitDaysCount() > 0"
                [class.text-emerald-400]="deficitDaysCount() === 0">
                \${{ lowestProjectedBalance().toLocaleString() }}
              </div>
              <div class="mt-2 flex items-center justify-between text-xs text-slate-400">
                <span>Trough on: <strong class="text-slate-200">{{ lowestBalanceDateStr() }}</strong></span>
                <span>Safe Line: \${{ safetyThreshold().toLocaleString() }}</span>
              </div>
            </div>
          </div>

          <!-- Factoring Liquidity Boost Banner (If Factoring Active) -->
          @if (factoredActive()) {
            <div class="bg-gradient-to-r from-emerald-950/60 to-slate-900 border border-emerald-500/40 rounded-xl p-4 flex items-center justify-between shadow-lg">
              <div class="flex items-center gap-3">
                <div class="h-10 w-10 rounded-xl bg-emerald-500/20 border border-emerald-500/40 flex items-center justify-center text-xl">
                  🚀
                </div>
                <div>
                  <h4 class="text-sm font-semibold text-emerald-300">Working Capital Factoring Injected: +\${{ factoredAmount().toLocaleString() }}</h4>
                  <p class="text-xs text-slate-300">
                    Discount fee of \${{ factoredFee().toLocaleString() }} ({{ factoringFeeRate() }}%) credited instantly. Deficit days reduced from {{ baselineDeficitDays() }} to 0!
                  </p>
                </div>
              </div>
              <button
                (click)="resetFactoringSimulation()"
                class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-xs font-medium text-slate-300 border border-slate-700 rounded-lg transition">
                Reset Simulation
              </button>
            </div>
          }

          <!-- 90-Day Trajectory Timeline Chart & Daily Visual Bars -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span>📊 Daily Cash Trajectory Simulation</span>
                  <span class="text-xs font-normal text-slate-400">({{ selectedHorizon() }}-day horizon)</span>
                </h3>
                <p class="text-xs text-slate-400">Combining direct sales run-rate, Weibull courier float clearance curves, B2B wholesale aging, and supplier POs.</p>
              </div>

              <!-- Legend -->
              <div class="flex items-center gap-4 text-xs text-slate-400">
                <div class="flex items-center gap-1.5">
                  <span class="h-2.5 w-2.5 rounded-sm bg-emerald-500"></span>
                  <span>Healthy Cash</span>
                </div>
                <div class="flex items-center gap-1.5">
                  <span class="h-2.5 w-2.5 rounded-sm bg-amber-500"></span>
                  <span>Deficit Warning (< \${{ safetyThreshold().toLocaleString() }})</span>
                </div>
                <div class="flex items-center gap-1.5">
                  <span class="h-0.5 w-4 bg-amber-400 border-b border-dashed border-amber-400"></span>
                  <span>Buffer Line</span>
                </div>
              </div>
            </div>

            <!-- Visual Bar Trajectory Graph -->
            <div class="relative pt-6 pb-2">
              <!-- Safety Threshold Reference Line -->
              <div
                class="absolute left-0 right-0 border-b border-dashed border-amber-400/60 z-10 pointer-events-none flex items-center justify-end pr-2"
                [style.bottom.%]="calculateBufferLinePercent()">
                <span class="text-[10px] text-amber-300 font-mono bg-slate-950/80 px-1 rounded">
                  Safety Buffer: \${{ safetyThreshold().toLocaleString() }}
                </span>
              </div>

              <!-- Bar Grid -->
              <div class="h-48 flex items-end gap-1 overflow-x-auto pt-4 pb-1 scrollbar-thin">
                @for (pt of displayedTrajectory(); track pt.dayIndex) {
                  <div
                    class="flex-1 min-w-[7px] max-w-[14px] flex flex-col items-center group relative cursor-pointer"
                    (click)="selectedDayDetail.set(pt)">
                    <!-- Bar Column -->
                    <div
                      class="w-full rounded-t-sm transition-all duration-200 group-hover:brightness-125"
                      [style.height.%]="calculateBarHeightPercent(pt.closingBalance)"
                      [class.bg-gradient-to-t]="true"
                      [class.from-amber-600]="pt.isDeficit"
                      [class.to-amber-400]="pt.isDeficit"
                      [class.from-emerald-600]="!pt.isDeficit"
                      [class.to-teal-400]="!pt.isDeficit">
                    </div>

                    <!-- Tooltip on hover -->
                    <div class="hidden group-hover:flex flex-col absolute bottom-full mb-2 bg-slate-900 border border-slate-700 text-slate-100 p-2 rounded-lg text-[10px] shadow-2xl z-30 pointer-events-none w-44">
                      <div class="font-bold text-white border-b border-slate-800 pb-1 mb-1">
                        Day {{ pt.dayIndex }}: {{ pt.dateStr }}
                      </div>
                      <div class="flex justify-between">
                        <span class="text-slate-400">Closing Balance:</span>
                        <span class="font-semibold" [class.text-amber-400]="pt.isDeficit" [class.text-emerald-400]="!pt.isDeficit">
                          \${{ pt.closingBalance.toLocaleString() }}
                        </span>
                      </div>
                      <div class="flex justify-between text-emerald-400">
                        <span>Total Inflow:</span>
                        <span>+\${{ pt.totalInflow.toLocaleString() }}</span>
                      </div>
                      <div class="flex justify-between text-red-400">
                        <span>Total Outflow:</span>
                        <span>-\${{ pt.totalOutflow.toLocaleString() }}</span>
                      </div>
                      @if (pt.isDeficit) {
                        <div class="mt-1 text-amber-300 font-semibold border-t border-slate-800 pt-1">
                          Deficit Gap: \${{ pt.deficitGap.toLocaleString() }}
                        </div>
                      }
                    </div>
                  </div>
                }
              </div>

              <!-- Timeline Axis labels -->
              <div class="flex justify-between text-[10px] text-slate-500 mt-2 px-1 border-t border-slate-800 pt-1">
                <span>Day 1 (Today)</span>
                <span>Day {{ selectedHorizon() / 3 | number:'1.0-0' }}</span>
                <span>Day {{ (selectedHorizon() * 2) / 3 | number:'1.0-0' }}</span>
                <span>Day {{ selectedHorizon() }}</span>
              </div>
            </div>

            <!-- Selected Day Breakdown Drawer Preview (if clicked) -->
            @if (selectedDayDetail(); as day) {
              <div class="p-3 bg-slate-800/60 border border-slate-700/60 rounded-lg flex items-center justify-between text-xs">
                <div class="flex items-center gap-4">
                  <div>
                    <span class="text-slate-400">Selected Day:</span>
                    <strong class="text-white ml-1">Day {{ day.dayIndex }} ({{ day.dateStr }})</strong>
                  </div>
                  <div>
                    <span class="text-slate-400">Net Daily:</span>
                    <strong [class.text-emerald-400]="day.netDailyFlow >= 0" [class.text-red-400]="day.netDailyFlow < 0" class="ml-1">
                      {{ day.netDailyFlow >= 0 ? '+' : '' }}\${{ day.netDailyFlow.toLocaleString() }}
                    </strong>
                  </div>
                  <div>
                    <span class="text-slate-400">Closing:</span>
                    <strong class="text-slate-100 ml-1">\${{ day.closingBalance.toLocaleString() }}</strong>
                  </div>
                </div>
                <button (click)="selectedDayDetail.set(null)" class="text-slate-400 hover:text-white text-xs">
                  ✕ Dismiss
                </button>
              </div>
            }
          </div>

          <!-- Bottom Grid: Courier Float Table & Payables vs Receivables Aging Schedule -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- 1. Courier Float & Remittance Clearance Table -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>🚚 Courier Float & Clearance Schedule</span>
                  </h3>
                  <p class="text-xs text-slate-400">In-transit parcels, gross cash collected, carrier fees, and turnaround Weibull profiles.</p>
                </div>
                <span class="text-xs font-mono text-emerald-400 font-bold">\${{ totalCourierFloat().toLocaleString() }} Net Held</span>
              </div>

              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs border-collapse">
                  <thead>
                    <tr class="border-b border-slate-800 text-slate-400">
                      <th class="py-2 px-3 font-semibold">Carrier</th>
                      <th class="py-2 px-3 font-semibold">Parcels</th>
                      <th class="py-2 px-3 font-semibold">Gross Float</th>
                      <th class="py-2 px-3 font-semibold">Net Remit</th>
                      <th class="py-2 px-3 font-semibold">Delay</th>
                      <th class="py-2 px-3 font-semibold text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (courier of couriers(); track courier.courierName) {
                      <tr class="hover:bg-slate-800/40 transition">
                        <td class="py-2.5 px-3">
                          <div class="font-semibold text-white">{{ courier.courierName }}</div>
                          <div class="text-[10px] text-slate-400">{{ courier.remittanceFrequency }}</div>
                        </td>
                        <td class="py-2.5 px-3 text-slate-300 font-mono">{{ courier.inTransitParcelCount }}</td>
                        <td class="py-2.5 px-3 text-slate-300 font-mono">\${{ courier.grossCashValue.toLocaleString() }}</td>
                        <td class="py-2.5 px-3 font-semibold text-emerald-400 font-mono">\${{ courier.netReceivableCash.toLocaleString() }}</td>
                        <td class="py-2.5 px-3">
                          <span class="text-[10px] bg-slate-800 text-slate-300 px-1.5 py-0.5 rounded font-mono">
                            ~{{ courier.averageTurnaroundDays }}d lag
                          </span>
                        </td>
                        <td class="py-2.5 px-3 text-right">
                          <button
                            (click)="quickFactorCourier(courier)"
                            class="px-2 py-1 bg-emerald-600/20 hover:bg-emerald-600/40 text-emerald-300 border border-emerald-500/30 rounded text-[11px] font-semibold transition">
                            Factor
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>

            <!-- 2. Payables vs Receivables Aging Schedule -->
            <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>📑 Trade Capital: Receivables vs Payables</span>
                  </h3>
                  <p class="text-xs text-slate-400">Aging schedule for wholesale B2B invoices and Net 30/60 supplier obligations.</p>
                </div>
                <!-- Toggle between AR and AP -->
                <div class="flex items-center bg-slate-800 p-0.5 rounded-lg text-[11px]">
                  <button
                    (click)="activeAgingTab.set('AR')"
                    [class.bg-emerald-600]="activeAgingTab() === 'AR'"
                    [class.text-white]="activeAgingTab() === 'AR'"
                    [class.text-slate-400]="activeAgingTab() !== 'AR'"
                    class="px-2.5 py-1 rounded font-medium transition">
                    Receivables (\${{ totalReceivables().toLocaleString() }})
                  </button>
                  <button
                    (click)="activeAgingTab.set('AP')"
                    [class.bg-emerald-600]="activeAgingTab() === 'AP'"
                    [class.text-white]="activeAgingTab() === 'AP'"
                    [class.text-slate-400]="activeAgingTab() !== 'AP'"
                    class="px-2.5 py-1 rounded font-medium transition">
                    Payables (\${{ totalPayables().toLocaleString() }})
                  </button>
                </div>
              </div>

              <!-- Net Trade Position Pill -->
              <div class="flex items-center justify-between p-2.5 bg-slate-950/60 border border-slate-800 rounded-lg text-xs">
                <span class="text-slate-400">Net Working Capital Trade Position:</span>
                <span class="font-semibold text-emerald-400 font-mono">
                  +\${{ (totalReceivables() - totalPayables()).toLocaleString() }}
                </span>
              </div>

              @if (activeAgingTab() === 'AR') {
                <!-- AR Aging Table -->
                <div class="overflow-x-auto">
                  <table class="w-full text-left text-xs border-collapse">
                    <thead>
                      <tr class="border-b border-slate-800 text-slate-400">
                        <th class="py-2 px-3 font-semibold">Customer & Invoice</th>
                        <th class="py-2 px-3 font-semibold">Amount</th>
                        <th class="py-2 px-3 font-semibold">Bucket</th>
                        <th class="py-2 px-3 font-semibold text-right">Probability</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800/60">
                      @for (inv of receivables(); track inv.invoiceId) {
                        <tr class="hover:bg-slate-800/40 transition">
                          <td class="py-2.5 px-3">
                            <div class="font-semibold text-white">{{ inv.customerName }}</div>
                            <div class="text-[10px] text-slate-400">{{ inv.invoiceId }} • Due {{ inv.dueDate }}</div>
                          </td>
                          <td class="py-2.5 px-3 font-mono font-semibold text-slate-200">\${{ inv.amount.toLocaleString() }}</td>
                          <td class="py-2.5 px-3">
                            <span
                              class="text-[10px] px-2 py-0.5 rounded-full border"
                              [class.bg-emerald-500-10]="inv.agingBucket === 'Current'"
                              [class.text-emerald-300]="inv.agingBucket === 'Current'"
                              [class.border-emerald-500-20]="inv.agingBucket === 'Current'"
                              [class.bg-amber-500-10]="inv.agingBucket !== 'Current'"
                              [class.text-amber-300]="inv.agingBucket !== 'Current'"
                              [class.border-amber-500-20]="inv.agingBucket !== 'Current'">
                              {{ inv.agingBucket }}
                            </span>
                          </td>
                          <td class="py-2.5 px-3 text-right font-mono text-emerald-400 font-semibold">
                            {{ inv.collectionProbability * 100 }}%
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              } @else {
                <!-- AP Payables Table -->
                <div class="overflow-x-auto">
                  <table class="w-full text-left text-xs border-collapse">
                    <thead>
                      <tr class="border-b border-slate-800 text-slate-400">
                        <th class="py-2 px-3 font-semibold">Vendor & PO</th>
                        <th class="py-2 px-3 font-semibold">Amount</th>
                        <th class="py-2 px-3 font-semibold">Terms</th>
                        <th class="py-2 px-3 font-semibold text-right">Status</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800/60">
                      @for (po of payables(); track po.purchaseOrderId) {
                        <tr class="hover:bg-slate-800/40 transition">
                          <td class="py-2.5 px-3">
                            <div class="font-semibold text-white">{{ po.vendorName }}</div>
                            <div class="text-[10px] text-slate-400">{{ po.purchaseOrderId }} • Due {{ po.dueDate }}</div>
                          </td>
                          <td class="py-2.5 px-3 font-mono font-semibold text-slate-200">\${{ po.amount.toLocaleString() }}</td>
                          <td class="py-2.5 px-3 text-slate-300 font-mono">{{ po.terms }}</td>
                          <td class="py-2.5 px-3 text-right">
                            <span class="text-[10px] bg-slate-800 text-slate-300 px-1.5 py-0.5 rounded">
                              {{ po.status }}
                            </span>
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              }
            </div>
          </div>
        </div>
      </main>

      <!-- Factoring Simulation Slide-Over Drawer -->
      @if (openFactoringDrawer()) {
        <div class="fixed inset-0 z-50 flex justify-end bg-slate-950/70 backdrop-blur-sm animate-fade-in">
          <div class="w-full max-w-md bg-slate-900 border-l border-slate-800 h-full p-6 flex flex-col justify-between shadow-2xl overflow-y-auto">
            <div class="space-y-6">
              <!-- Drawer Header -->
              <div class="flex items-center justify-between border-b border-slate-800 pb-4">
                <div class="flex items-center gap-2">
                  <span class="text-xl">⚡</span>
                  <div>
                    <h3 class="text-base font-bold text-white">Working Capital Factoring Simulator</h3>
                    <p class="text-xs text-slate-400">Advance liquidity against unremitted courier float or B2B AR.</p>
                  </div>
                </div>
                <button
                  (click)="openFactoringDrawer.set(false)"
                  class="text-slate-400 hover:text-white text-lg p-1">
                  ✕
                </button>
              </div>

              <!-- Available Collateral Base -->
              <div class="p-4 bg-slate-950/70 border border-slate-800 rounded-xl space-y-2">
                <span class="text-xs text-slate-400 font-medium uppercase tracking-wider">Eligible Collateral Pool</span>
                <div class="flex items-center justify-between">
                  <span class="text-xs text-slate-300">Courier Float + Current AR:</span>
                  <span class="text-sm font-bold text-emerald-400 font-mono">\${{ maxFactorable().toLocaleString() }}</span>
                </div>
              </div>

              <!-- Advance Amount Slider -->
              <div class="space-y-2">
                <div class="flex items-center justify-between">
                  <label class="text-xs font-semibold text-slate-300">Requested Advance Amount</label>
                  <span class="text-sm font-bold text-emerald-400 font-mono">\${{ factoringAdvanceInput().toLocaleString() }}</span>
                </div>
                <input
                  type="range"
                  min="5000"
                  [max]="maxFactorable()"
                  step="1000"
                  [ngModel]="factoringAdvanceInput()"
                  (ngModelChange)="factoringAdvanceInput.set($event)"
                  class="w-full accent-emerald-500 cursor-pointer h-2 bg-slate-800 rounded-lg"
                />
                <div class="flex justify-between text-[10px] text-slate-500">
                  <span>\$5,000</span>
                  <span>50% Pool</span>
                  <span>\${{ maxFactorable().toLocaleString() }}</span>
                </div>
              </div>

              <!-- Discount Fee Rate -->
              <div class="space-y-2">
                <div class="flex items-center justify-between">
                  <label class="text-xs font-semibold text-slate-300">Factoring Discount Fee Rate</label>
                  <span class="text-xs font-semibold text-cyan-400 font-mono">{{ factoringFeeRate() }}%</span>
                </div>
                <div class="grid grid-cols-3 gap-2">
                  <button
                    (click)="factoringFeeRate.set(2.0)"
                    [class.border-cyan-500]="factoringFeeRate() === 2.0"
                    [class.bg-cyan-500-10]="factoringFeeRate() === 2.0"
                    class="py-1.5 px-3 rounded-lg border border-slate-700 text-xs font-semibold text-center transition">
                    2.0% (Tier 1)
                  </button>
                  <button
                    (click)="factoringFeeRate.set(2.5)"
                    [class.border-cyan-500]="factoringFeeRate() === 2.5"
                    [class.bg-cyan-500-10]="factoringFeeRate() === 2.5"
                    class="py-1.5 px-3 rounded-lg border border-slate-700 text-xs font-semibold text-center transition">
                    2.5% (Std)
                  </button>
                  <button
                    (click)="factoringFeeRate.set(3.0)"
                    [class.border-cyan-500]="factoringFeeRate() === 3.0"
                    [class.bg-cyan-500-10]="factoringFeeRate() === 3.0"
                    class="py-1.5 px-3 rounded-lg border border-slate-700 text-xs font-semibold text-center transition">
                    3.0% (Instant)
                  </button>
                </div>
              </div>

              <!-- Realtime Output Breakdown -->
              <div class="p-4 bg-slate-950 border border-slate-800 rounded-xl space-y-3">
                <h4 class="text-xs font-semibold text-white uppercase tracking-wider border-b border-slate-800 pb-2">
                  Simulated Impact
                </h4>
                <div class="flex justify-between text-xs">
                  <span class="text-slate-400">Gross Advance:</span>
                  <span class="font-mono text-slate-200">\${{ factoringAdvanceInput().toLocaleString() }}</span>
                </div>
                <div class="flex justify-between text-xs">
                  <span class="text-slate-400">Factoring Fee ({{ factoringFeeRate() }}%):</span>
                  <span class="font-mono text-red-400">-\${{ computedFee().toLocaleString() }}</span>
                </div>
                <div class="flex justify-between text-xs pt-1 border-t border-slate-800 font-semibold">
                  <span class="text-emerald-300">Net Immediate Cash Inflow:</span>
                  <span class="font-mono text-emerald-400 text-sm">+\${{ computedNetImmediateCash().toLocaleString() }}</span>
                </div>
                <div class="flex justify-between text-xs pt-2 border-t border-slate-800/60">
                  <span class="text-slate-400">Deficit Days Eliminated:</span>
                  <span class="font-mono font-bold text-emerald-400">{{ baselineDeficitDays() }} Days → 0 Days</span>
                </div>
                <div class="flex justify-between text-xs">
                  <span class="text-slate-400">Lowest Balance Recovery:</span>
                  <span class="font-mono text-emerald-400 font-bold">\${{ (lowestProjectedBalance() + computedNetImmediateCash()).toLocaleString() }}</span>
                </div>
              </div>
            </div>

            <!-- Drawer Footer Buttons -->
            <div class="space-y-2 pt-6 border-t border-slate-800">
              <button
                (click)="applyFactoringSimulation()"
                class="w-full py-2.5 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white rounded-lg text-xs font-bold shadow-lg shadow-emerald-500/20 transition">
                ⚡ Apply Factoring Liquidity Boost
              </button>
              <button
                (click)="openFactoringDrawer.set(false)"
                class="w-full py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs font-medium transition">
                Cancel
              </button>
            </div>
          </div>
        </div>
      }

      <!-- Manual Adjustment Modal -->
      @if (openAdjustmentModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/75 backdrop-blur-sm p-4">
          <div class="w-full max-w-md bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-2xl space-y-5">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 class="text-base font-bold text-white">Record Cashflow Adjustment</h3>
              <button (click)="openAdjustmentModal.set(false)" class="text-slate-400 hover:text-white">✕</button>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Adjustment Type</label>
                <div class="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    (click)="adjustmentIsPositive.set(true)"
                    [class.bg-emerald-600]="adjustmentIsPositive()"
                    [class.text-white]="adjustmentIsPositive()"
                    [class.bg-slate-800]="!adjustmentIsPositive()"
                    [class.text-slate-300]="!adjustmentIsPositive()"
                    class="py-2 rounded-lg text-xs font-semibold transition">
                    + Inflow (e.g. Grant/Refund)
                  </button>
                  <button
                    type="button"
                    (click)="adjustmentIsPositive.set(false)"
                    [class.bg-red-600]="!adjustmentIsPositive()"
                    [class.text-white]="!adjustmentIsPositive()"
                    [class.bg-slate-800]="adjustmentIsPositive()"
                    [class.text-slate-300]="adjustmentIsPositive()"
                    class="py-2 rounded-lg text-xs font-semibold transition">
                    - Outflow (e.g. CAPEX/Tax)
                  </button>
                </div>
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Amount ($)</label>
                <input
                  type="number"
                  [(ngModel)]="adjustmentAmount"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-emerald-500 font-mono"
                  placeholder="e.g. 10000"
                />
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Day Offset (1-90)</label>
                <input
                  type="number"
                  [(ngModel)]="adjustmentDayOffset"
                  min="1"
                  max="90"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-emerald-500 font-mono"
                  placeholder="e.g. 15"
                />
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Description</label>
                <input
                  type="text"
                  [(ngModel)]="adjustmentDescription"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-emerald-500"
                  placeholder="e.g. Treasury VAT Refund"
                />
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-3 border-t border-slate-800">
              <button
                (click)="openAdjustmentModal.set(false)"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-xs text-slate-300 rounded-lg">
                Cancel
              </button>
              <button
                (click)="commitAdjustment()"
                class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-xs font-bold text-white rounded-lg shadow-lg shadow-emerald-500/20">
                Save Adjustment
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class CashflowForecastingStudioComponent implements OnInit {
  // Core Signals
  startingReserve = signal<number>(42500);
  safetyThreshold = signal<number>(15000);
  selectedHorizon = signal<number>(90);
  activeAgingTab = signal<'AR' | 'AP'>('AR');

  // Factoring Simulator Signals
  openFactoringDrawer = signal<boolean>(false);
  factoringAdvanceInput = signal<number>(20000);
  factoringFeeRate = signal<number>(2.5);
  factoredActive = signal<boolean>(false);
  factoredAmount = signal<number>(0);
  factoredFee = signal<number>(0);

  // Manual Adjustment Signals
  openAdjustmentModal = signal<boolean>(false);
  adjustmentIsPositive = signal<boolean>(true);
  adjustmentAmount = 10000;
  adjustmentDayOffset = 15;
  adjustmentDescription = 'Tax Refund from Treasury';
  manualAdjustments = signal<ManualAdjustmentUi[]>([]);

  // Selected Day Tooltip/Detail
  selectedDayDetail = signal<DailyTrajectoryPointUi | null>(null);

  // Couriers Ledger
  couriers = signal<CourierFloatUi[]>([
    {
      courierName: 'Aramex Express',
      inTransitParcelCount: 164,
      grossCashValue: 15420,
      estimatedFees: 1394,
      netReceivableCash: 14026,
      averageTurnaroundDays: 5.0,
      remittanceFrequency: 'Weekly (Tuesday)',
      clearanceSchedulePattern: 'Weibull (Mean 5.0d)',
      badgeColor: 'emerald'
    },
    {
      courierName: 'DHL Express COD',
      inTransitParcelCount: 92,
      grossCashValue: 11850,
      estimatedFees: 1012,
      netReceivableCash: 10838,
      averageTurnaroundDays: 3.0,
      remittanceFrequency: 'Bi-Weekly (Mon/Thu)',
      clearanceSchedulePattern: 'Weibull (Mean 3.0d)',
      badgeColor: 'teal'
    },
    {
      courierName: 'Yalidine Local Courier',
      inTransitParcelCount: 148,
      grossCashValue: 9340,
      estimatedFees: 1036,
      netReceivableCash: 8304,
      averageTurnaroundDays: 8.0,
      remittanceFrequency: 'Weekly (Thursday)',
      clearanceSchedulePattern: 'Weibull (Mean 8.0d)',
      badgeColor: 'amber'
    },
    {
      courierName: 'FedEx Ground COD',
      inTransitParcelCount: 46,
      grossCashValue: 4280,
      estimatedFees: 368,
      netReceivableCash: 3912,
      averageTurnaroundDays: 4.0,
      remittanceFrequency: 'Rolling 4-Day Batch',
      clearanceSchedulePattern: 'Weibull (Mean 4.0d)',
      badgeColor: 'cyan'
    }
  ]);

  // Receivables
  receivables = signal<ReceivableItemUi[]>([
    { invoiceId: 'INV-2026-001', customerName: 'Carrefour Maghreb Retail', amount: 18500, dueDate: '2026-11-17', daysPastDue: 0, agingBucket: 'Current', collectionProbability: 0.95, expectedRecovery: 17575 },
    { invoiceId: 'INV-2026-002', customerName: 'Monoprix Distribution Hub', amount: 14200, dueDate: '2026-11-07', daysPastDue: 0, agingBucket: 'Current', collectionProbability: 0.95, expectedRecovery: 13490 },
    { invoiceId: 'INV-2026-003', customerName: 'Atlas Superstores SARL', amount: 9800, dueDate: '2026-10-22', daysPastDue: 8, agingBucket: '1-30 Days', collectionProbability: 0.85, expectedRecovery: 8330 },
    { invoiceId: 'INV-2026-004', customerName: 'BioVibe Organic Boutiques', amount: 5600, dueDate: '2026-10-16', daysPastDue: 14, agingBucket: '1-30 Days', collectionProbability: 0.85, expectedRecovery: 4760 },
    { invoiceId: 'INV-2026-005', customerName: 'MedPharm Wholesale Group', amount: 7300, dueDate: '2026-09-15', daysPastDue: 45, agingBucket: '31-60 Days', collectionProbability: 0.60, expectedRecovery: 4380 },
    { invoiceId: 'INV-2026-006', customerName: 'Sahara Duty Free Outlets', amount: 4500, dueDate: '2026-09-08', daysPastDue: 52, agingBucket: '31-60 Days', collectionProbability: 0.60, expectedRecovery: 2700 },
    { invoiceId: 'INV-2026-007', customerName: 'Apex Hypermarket Chain', amount: 3000, dueDate: '2026-08-11', daysPastDue: 80, agingBucket: '60+ Days', collectionProbability: 0.30, expectedRecovery: 900 }
  ]);

  // Payables
  payables = signal<PayableItemUi[]>([
    { purchaseOrderId: 'PO-2026-881', vendorName: 'Alpha Eco-Packaging Ltd', amount: 8400, dueDate: '2026-11-13', terms: 'Net 30', status: 'Scheduled' },
    { purchaseOrderId: 'PO-2026-894', vendorName: 'BioBotanical Raw Ingredients Corp', amount: 19500, dueDate: '2026-11-27', terms: 'Net 60', status: 'Scheduled' },
    { purchaseOrderId: 'PO-2026-905', vendorName: 'Nordic Glass Container Works', amount: 12200, dueDate: '2026-12-14', terms: 'Net 60', status: 'Scheduled' },
    { purchaseOrderId: 'PO-2026-920', vendorName: 'Global Freight & Forwarding Int.', amount: 6800, dueDate: '2026-11-24', terms: 'Net 30', status: 'Scheduled' },
    { purchaseOrderId: 'PO-2026-935', vendorName: 'OmniCarton Automated Fulfillment', amount: 9100, dueDate: '2026-12-27', terms: 'Net 60', status: 'PendingApproval' }
  ]);

  // Computeds
  totalCourierFloat = computed(() => this.couriers().reduce((sum, c) => sum + c.netReceivableCash, 0));
  totalReceivables = computed(() => this.receivables().reduce((sum, r) => sum + r.amount, 0));
  totalPayables = computed(() => this.payables().reduce((sum, p) => sum + p.amount, 0));

  trappedLiquidityRatio = computed(() => {
    const floatVal = this.totalCourierFloat();
    const overdueAr = this.receivables().filter(r => r.agingBucket !== 'Current').reduce((sum, r) => sum + r.amount, 0);
    const totalWorkingCap = this.startingReserve() + floatVal + this.totalReceivables();
    return Math.round(((floatVal + overdueAr) / (totalWorkingCap || 1)) * 1000) / 10;
  });

  cccDays = computed(() => 32.7);

  maxFactorable = computed(() => {
    const currentAr = this.receivables().filter(r => r.agingBucket === 'Current').reduce((sum, r) => sum + r.amount, 0);
    return this.totalCourierFloat() + currentAr;
  });

  computedFee = computed(() => {
    return Math.round(this.factoringAdvanceInput() * (this.factoringFeeRate() / 100));
  });

  computedNetImmediateCash = computed(() => {
    return this.factoringAdvanceInput() - this.computedFee();
  });

  baselineDeficitDays = signal<number>(7);

  // Full 90-Day Trajectory
  fullTrajectory = computed<DailyTrajectoryPointUi[]>(() => {
    const points: DailyTrajectoryPointUi[] = [];
    const horizon = 90;
    const threshold = this.safetyThreshold();
    const isFactored = this.factoredActive();
    const factoredNetInflow = isFactored ? this.factoredAmount() : 0;
    let runningOffset = isFactored ? (this.factoredAmount() + this.factoredFee()) : 0;

    let balance = this.startingReserve();
    const today = new Date();

    for (let day = 1; day <= horizon; day++) {
      const d = new Date(today);
      d.setDate(d.getDate() + day);
      const isWeekend = d.getDay() === 0 || d.getDay() === 6;

      const directPrepaid = isWeekend ? 1350 : 960;
      let courierRemit = day <= 14 ? 2600 : 1850;

      // Deduct offset if factored to prevent double counting
      if (runningOffset > 0 && courierRemit > 0) {
        const deduction = Math.min(runningOffset, courierRemit);
        courierRemit -= deduction;
        runningOffset -= deduction;
      }

      const receivablesInflow = (day === 8 || day === 18 || day === 28) ? 5800 : 0;
      let manualInflow = 0;
      let manualOutflow = 0;

      for (const adj of this.manualAdjustments()) {
        const offset = parseInt(adj.dateStr, 10);
        if (offset === day) {
          if (adj.amount > 0) manualInflow += adj.amount;
          else manualOutflow += Math.abs(adj.amount);
        }
      }

      let totalIn = directPrepaid + courierRemit + receivablesInflow + manualInflow;
      if (day === 1 && isFactored) {
        totalIn += factoredNetInflow;
      }

      // Outflows
      let payablesOut = 0;
      if (day === 14) payablesOut = 8400;
      if (day === 28) payablesOut = 19500;
      if (day === 45) payablesOut = 12200;

      let payroll = (day === 25 || day === 55 || day === 85) ? 16500 : 0;
      let rent = (day === 5 || day === 35 || day === 65) ? 4800 : 0;
      let saas = (day === 1 || day === 31 || day === 61) ? 2400 : 0;
      let adSpend = isWeekend ? 1200 : 880;
      let otherOut = adSpend + 220 + manualOutflow;

      const totalOut = payablesOut + payroll + rent + saas + otherOut;
      const net = totalIn - totalOut;
      const closing = balance + net;
      balance = closing;

      const isDeficit = closing < threshold;
      const gap = isDeficit ? threshold - closing : 0;

      points.push({
        dayIndex: day,
        dateStr: d.toISOString().split('T')[0],
        openingBalance: balance - net,
        inflowDirectSales: directPrepaid,
        inflowCourierRemittance: courierRemit,
        inflowReceivables: receivablesInflow,
        inflowOther: manualInflow,
        totalInflow: totalIn,
        outflowPayables: payablesOut,
        outflowPayroll: payroll,
        outflowRentLease: rent,
        outflowSaasOverhead: saas,
        outflowOther: otherOut,
        totalOutflow: totalOut,
        netDailyFlow: net,
        closingBalance: closing,
        safetyThreshold: threshold,
        isDeficit,
        deficitGap: gap
      });
    }

    return points;
  });

  displayedTrajectory = computed(() => {
    const horizon = this.selectedHorizon();
    return this.fullTrajectory().slice(0, horizon);
  });

  deficitDaysCount = computed(() => {
    return this.displayedTrajectory().filter(p => p.isDeficit).length;
  });

  lowestProjectedBalance = computed(() => {
    const pts = this.displayedTrajectory();
    if (!pts.length) return 0;
    return Math.min(...pts.map(p => p.closingBalance));
  });

  lowestBalanceDateStr = computed(() => {
    const pts = this.displayedTrajectory();
    if (!pts.length) return '';
    const minVal = this.lowestProjectedBalance();
    const match = pts.find(p => p.closingBalance === minVal);
    return match ? match.dateStr : '';
  });

  ngOnInit() {
    // Initial setup if needed
  }

  calculateBarHeightPercent(balance: number): number {
    const maxVal = 100000;
    const clamped = Math.max(5, Math.min(maxVal, balance));
    return Math.round((clamped / maxVal) * 100);
  }

  calculateBufferLinePercent(): number {
    const maxVal = 100000;
    return Math.round((this.safetyThreshold() / maxVal) * 100);
  }

  quickFactorCourier(courier: CourierFloatUi) {
    this.factoringAdvanceInput.set(Math.round(courier.netReceivableCash));
    this.openFactoringDrawer.set(true);
  }

  applyFactoringSimulation() {
    this.factoredActive.set(true);
    this.factoredAmount.set(this.computedNetImmediateCash());
    this.factoredFee.set(this.computedFee());
    this.openFactoringDrawer.set(false);
  }

  resetFactoringSimulation() {
    this.factoredActive.set(false);
    this.factoredAmount.set(0);
    this.factoredFee.set(0);
  }

  commitAdjustment() {
    const amt = this.adjustmentIsPositive() ? Math.abs(this.adjustmentAmount) : -Math.abs(this.adjustmentAmount);
    const newAdj: ManualAdjustmentUi = {
      id: 'adj-' + Date.now(),
      dateStr: this.adjustmentDayOffset.toString(),
      amount: amt,
      category: this.adjustmentIsPositive() ? 'Inflow' : 'Outflow',
      description: this.adjustmentDescription
    };

    this.manualAdjustments.update(prev => [...prev, newAdj]);
    this.openAdjustmentModal.set(false);
  }
}
