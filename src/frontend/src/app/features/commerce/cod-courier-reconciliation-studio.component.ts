import { Component, signal, computed, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type CourierCode = 'SMSA' | 'Aramex' | 'SPL' | 'JTExpress' | 'Other';

export type RemittanceMatchStatus =
  | 'Matched'
  | 'Underpaid'
  | 'Overpaid'
  | 'LostInTransit'
  | 'RtoFeeDisputed'
  | 'UnmatchedWaybill';

export interface CourierRemittanceItem {
  trackingNumber: string;
  orderNumber: string;
  expectedCodAmountSar: number;
  remittedAmountSar: number;
  courierFeeSar: number;
  discrepancyAmountSar: number;
  status: RemittanceMatchStatus;
  discrepancyReason?: string | null;
  isDisputed?: boolean;
  batchId?: string;
  batchReference?: string;
  courier?: CourierCode;
}

export interface CourierRemittanceBatch {
  batchId: string;
  batchReference: string;
  courier: CourierCode;
  settlementDateUtc: string;
  totalParcels: number;
  totalExpectedCodSar: number;
  totalRemittedCodSar: number;
  totalCourierFeesSar: number;
  netDiscrepancySar: number;
  items: CourierRemittanceItem[];
  ingestedAtUtc: string;
}

export interface CourierPerformanceKpi {
  courierCode: CourierCode;
  courierName: string;
  deliveredParcels: number;
  totalCashRemittedSar: number;
  discrepancyRatePercent: number;
  avgSettlementDays: number;
  logoIcon: string;
}

export interface CourierDisputeRecord {
  disputeId: string;
  batchId: string;
  trackingNumber: string;
  courier: CourierCode;
  disputedAmountSar: number;
  reason: string;
  status: 'Open' | 'UnderReview' | 'Resolved' | 'Rejected';
  filedAtUtc: string;
}

export interface RawRemittanceInputItem {
  trackingNumber: string;
  orderNumber: string;
  expectedCodAmountSar?: number;
  remittedAmountSar: number;
  courierFeeSar: number;
  isDelivered: boolean;
  isReturned?: boolean;
}

const PRESET_SMSA_CLEAN: RawRemittanceInputItem[] = [
  { trackingNumber: 'SMSA-911002931', orderNumber: 'ORD-SA-4401', expectedCodAmountSar: 350.00, remittedAmountSar: 350.00, courierFeeSar: 28.00, isDelivered: true, isReturned: false },
  { trackingNumber: 'SMSA-911002932', orderNumber: 'ORD-SA-4402', expectedCodAmountSar: 420.00, remittedAmountSar: 420.00, courierFeeSar: 28.00, isDelivered: true, isReturned: false },
  { trackingNumber: 'SMSA-911002933', orderNumber: 'ORD-SA-4403', expectedCodAmountSar: 190.00, remittedAmountSar: 190.00, courierFeeSar: 28.00, isDelivered: true, isReturned: false },
];

const PRESET_ARAMEX_LOSS: RawRemittanceInputItem[] = [
  { trackingNumber: 'ARX-880192311', orderNumber: 'ORD-SA-5101', expectedCodAmountSar: 550.00, remittedAmountSar: 400.00, courierFeeSar: 32.00, isDelivered: true, isReturned: false },
  { trackingNumber: 'ARX-880192312', orderNumber: 'ORD-SA-5102', expectedCodAmountSar: 680.00, remittedAmountSar: 0.00, courierFeeSar: 0.00, isDelivered: false, isReturned: false },
  { trackingNumber: 'ARX-880192313', orderNumber: 'ORD-SA-5103', expectedCodAmountSar: 310.00, remittedAmountSar: 310.00, courierFeeSar: 32.00, isDelivered: true, isReturned: false },
];

const PRESET_SPL_RTO: RawRemittanceInputItem[] = [
  { trackingNumber: 'SPL-552019481', orderNumber: 'ORD-SA-6201', expectedCodAmountSar: 220.00, remittedAmountSar: 220.00, courierFeeSar: 22.00, isDelivered: true, isReturned: false },
  { trackingNumber: 'SPL-552019482', orderNumber: 'ORD-SA-6202', expectedCodAmountSar: 0.00, remittedAmountSar: 0.00, courierFeeSar: 35.00, isDelivered: false, isReturned: true },
  { trackingNumber: 'SPL-552019483', orderNumber: 'ORD-SA-6203', expectedCodAmountSar: 460.00, remittedAmountSar: 460.00, courierFeeSar: 22.00, isDelivered: true, isReturned: false },
];

const PRESET_JT_MIXED: RawRemittanceInputItem[] = [
  { trackingNumber: 'JT-771029341', orderNumber: 'ORD-SA-7301', expectedCodAmountSar: 380.00, remittedAmountSar: 380.00, courierFeeSar: 24.00, isDelivered: true, isReturned: false },
  { trackingNumber: 'JT-771029342', orderNumber: 'ORD-SA-7302', expectedCodAmountSar: 510.00, remittedAmountSar: 450.00, courierFeeSar: 24.00, isDelivered: true, isReturned: false },
  { trackingNumber: 'JT-771029343', orderNumber: 'UNKNOWN-WAYBILL-09', expectedCodAmountSar: 0.00, remittedAmountSar: 180.00, courierFeeSar: 24.00, isDelivered: true, isReturned: false },
];

@Component({
  selector: 'app-cod-courier-reconciliation-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Top Navigation Header -->
      <header class="border-b border-slate-800 bg-slate-900/90 backdrop-blur px-6 py-4 flex flex-wrap items-center justify-between gap-4 sticky top-0 z-40">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-tr from-emerald-600 via-teal-500 to-cyan-500 flex items-center justify-center shadow-lg shadow-emerald-500/20">
            <svg class="w-5 h-5 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-xl font-bold tracking-tight text-white">COD Courier Remittance Studio</h1>
              <span class="px-2 py-0.5 text-xs font-semibold rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                MENA Cash Settlement Engine
              </span>
            </div>
            <p class="text-xs text-slate-400">Automated COD Bordereau Ingestion, Tariff Audit & Carrier Dispute Resolution</p>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="openUploadDrawer()"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg font-medium text-sm flex items-center gap-2 transition shadow-lg shadow-emerald-600/20">
            <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
            </svg>
            Upload Remittance Batch
          </button>
        </div>
      </header>

      <main class="flex-1 p-6 space-y-6 max-w-7xl mx-auto w-full">
        <!-- Top 4 Financial KPIs -->
        <section class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <!-- Total Cash Remitted -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 relative overflow-hidden group hover:border-slate-700 transition">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Total Cash Remitted</span>
              <span class="w-8 h-8 rounded-lg bg-emerald-500/10 text-emerald-400 flex items-center justify-center">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <div class="text-2xl font-bold text-white tracking-tight" data-testid="kpi-total-remitted">
                {{ totalCashRemitted() | number:'1.2-2' }} <span class="text-sm font-normal text-slate-400">SAR</span>
              </div>
            </div>
            <div class="mt-2 text-xs text-slate-400 flex items-center gap-1.5">
              <span class="text-emerald-400 font-medium">Delivered & Disbursed</span>
              <span>across regional carriers</span>
            </div>
          </div>

          <!-- Discrepancy Revenue Leakage -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 relative overflow-hidden group hover:border-slate-700 transition">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Revenue Leakage</span>
              <span class="w-8 h-8 rounded-lg bg-rose-500/10 text-rose-400 flex items-center justify-center">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <div class="text-2xl font-bold text-rose-400 tracking-tight" data-testid="kpi-discrepancy-leakage">
                {{ discrepancyLeakage() | number:'1.2-2' }} <span class="text-sm font-normal text-slate-400">SAR</span>
              </div>
            </div>
            <div class="mt-2 text-xs text-rose-400/80 flex items-center gap-1.5">
              <span>Shortfalls, transit losses & RTO fees</span>
            </div>
          </div>

          <!-- Clean Match Rate -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 relative overflow-hidden group hover:border-slate-700 transition">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Clean Match Rate</span>
              <span class="w-8 h-8 rounded-lg bg-blue-500/10 text-blue-400 flex items-center justify-center">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
                </svg>
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <div class="text-2xl font-bold text-white tracking-tight" data-testid="kpi-clean-match-rate">
                {{ cleanMatchRate() | number:'1.1-1' }}%
              </div>
            </div>
            <div class="mt-2 text-xs text-slate-400 flex items-center gap-1.5">
              <span>{{ matchedParcelsCount() }} of {{ totalParcelsCount() }} parcels zero-variance</span>
            </div>
          </div>

          <!-- Open Carrier Disputes -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 relative overflow-hidden group hover:border-slate-700 transition">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Carrier Disputes</span>
              <span class="w-8 h-8 rounded-lg bg-amber-500/10 text-amber-400 flex items-center justify-center">
                <svg class="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3" />
                </svg>
              </span>
            </div>
            <div class="mt-3 flex items-baseline gap-2">
              <div class="text-2xl font-bold text-amber-400 tracking-tight" data-testid="kpi-open-disputes">
                {{ openDisputesCount() }}
              </div>
              <span class="text-xs text-slate-400">Claims in review</span>
            </div>
            <div class="mt-2 text-xs text-amber-400/80 flex items-center gap-1.5">
              <span>SLA reimbursement pending</span>
            </div>
          </div>
        </section>

        <!-- Courier Performance Breakdown Cards -->
        <section class="space-y-3">
          <div class="flex items-center justify-between">
            <h2 class="text-base font-semibold text-white flex items-center gap-2">
              <svg class="w-4 h-4 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
              </svg>
              Regional Carrier Performance & Tariffs (KSA / GCC)
            </h2>
            <span class="text-xs text-slate-400">Reconciled settlement velocity and discrepancy metrics</span>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
            @for (carrier of courierPerformance(); track carrier.courierCode) {
              <div class="bg-slate-900 border border-slate-800 rounded-xl p-4 flex flex-col justify-between hover:border-slate-700 transition group">
                <div>
                  <div class="flex items-center justify-between mb-3">
                    <div class="flex items-center gap-2.5">
                      <span class="w-8 h-8 rounded-lg bg-slate-800 border border-slate-700 flex items-center justify-center font-bold text-xs text-emerald-400">
                        {{ carrier.logoIcon }}
                      </span>
                      <div>
                        <h3 class="text-sm font-semibold text-white group-hover:text-emerald-400 transition">{{ carrier.courierName }}</h3>
                        <span class="text-[11px] text-slate-400">{{ carrier.deliveredParcels }} delivered parcels</span>
                      </div>
                    </div>
                  </div>

                  <div class="space-y-2 mt-2 pt-2 border-t border-slate-800/80 text-xs">
                    <div class="flex items-center justify-between">
                      <span class="text-slate-400">Cash Remitted:</span>
                      <span class="font-semibold text-white">{{ carrier.totalCashRemittedSar | number:'1.2-2' }} SAR</span>
                    </div>
                    <div class="flex items-center justify-between">
                      <span class="text-slate-400">Discrepancy Rate:</span>
                      <span [class]="carrier.discrepancyRatePercent > 0 ? 'text-rose-400 font-semibold' : 'text-emerald-400 font-semibold'">
                        {{ carrier.discrepancyRatePercent | number:'1.1-1' }}%
                      </span>
                    </div>
                    <div class="flex items-center justify-between">
                      <span class="text-slate-400">Settlement Velocity:</span>
                      <span class="text-slate-300 font-medium">{{ carrier.avgSettlementDays }} days avg</span>
                    </div>
                  </div>
                </div>

                <div class="mt-4 pt-2 border-t border-slate-800 flex items-center justify-between">
                  <button
                    (click)="setCourierFilter(carrier.courierCode)"
                    class="text-xs text-emerald-400 hover:text-emerald-300 font-medium flex items-center gap-1 transition">
                    View Ledger &rarr;
                  </button>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- Discrepancy Ledger Table Section -->
        <section class="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden space-y-0">
          <!-- Ledger Controls & Filters -->
          <div class="p-4 border-b border-slate-800 flex flex-wrap items-center justify-between gap-4 bg-slate-900/50">
            <div class="flex flex-wrap items-center gap-2">
              <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider mr-1">Status Filter:</span>
              <button
                (click)="setStatusFilter('All')"
                [class]="selectedStatusFilter() === 'All' ? 'bg-emerald-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition">
                All ({{ allLedgerItems().length }})
              </button>
              <button
                (click)="setStatusFilter('DiscrepantOnly')"
                [class]="selectedStatusFilter() === 'DiscrepantOnly' ? 'bg-rose-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition">
                Discrepancies Only ({{ discrepantItemsCount() }})
              </button>
              <button
                (click)="setStatusFilter('Underpaid')"
                [class]="selectedStatusFilter() === 'Underpaid' ? 'bg-amber-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition">
                Underpaid
              </button>
              <button
                (click)="setStatusFilter('LostInTransit')"
                [class]="selectedStatusFilter() === 'LostInTransit' ? 'bg-rose-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition">
                Lost in Transit
              </button>
              <button
                (click)="setStatusFilter('RtoFeeDisputed')"
                [class]="selectedStatusFilter() === 'RtoFeeDisputed' ? 'bg-purple-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition">
                RTO Disputed
              </button>
              <button
                (click)="setStatusFilter('Matched')"
                [class]="selectedStatusFilter() === 'Matched' ? 'bg-emerald-700 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
                class="px-2.5 py-1 rounded-md text-xs font-medium transition">
                Matched
              </button>
            </div>

            <!-- Courier filter and search -->
            <div class="flex items-center gap-3">
              <select
                [ngModel]="selectedCourierFilter()"
                (ngModelChange)="setCourierFilter($event)"
                class="bg-slate-800 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-emerald-500">
                <option value="All">All Carriers</option>
                <option value="SMSA">SMSA Express</option>
                <option value="Aramex">Aramex</option>
                <option value="SPL">SPL (Saudi Post)</option>
                <option value="JTExpress">J&T Express</option>
              </select>

              <input
                type="text"
                placeholder="Search waybill or order..."
                [ngModel]="searchTerm()"
                (ngModelChange)="searchTerm.set($event)"
                class="bg-slate-800 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-emerald-500 w-44" />
            </div>
          </div>

          <!-- Table -->
          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-950 text-slate-400 uppercase text-[11px] font-semibold border-b border-slate-800 tracking-wider">
                <tr>
                  <th class="py-3 px-4">Carrier & Waybill</th>
                  <th class="py-3 px-4">Order Ref</th>
                  <th class="py-3 px-4 text-right">Expected COD</th>
                  <th class="py-3 px-4 text-right">Remitted COD</th>
                  <th class="py-3 px-4 text-right">Courier Fee</th>
                  <th class="py-3 px-4 text-right">Discrepancy</th>
                  <th class="py-3 px-4 text-center">Status</th>
                  <th class="py-3 px-4">Discrepancy Reason</th>
                  <th class="py-3 px-4 text-right">Action</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800 text-slate-300">
                @for (item of filteredLedgerItems(); track item.trackingNumber) {
                  <tr class="hover:bg-slate-800/40 transition">
                    <!-- Carrier & Waybill -->
                    <td class="py-3 px-4">
                      <div class="flex items-center gap-2">
                        <span class="px-1.5 py-0.5 rounded text-[10px] font-bold bg-slate-800 border border-slate-700 text-slate-300">
                          {{ item.courier }}
                        </span>
                        <span class="font-mono font-medium text-white">{{ item.trackingNumber }}</span>
                      </div>
                      <div class="text-[10px] text-slate-500 mt-0.5">{{ item.batchReference }}</div>
                    </td>

                    <!-- Order Ref -->
                    <td class="py-3 px-4 font-mono font-medium text-slate-200">
                      {{ item.orderNumber }}
                    </td>

                    <!-- Expected COD -->
                    <td class="py-3 px-4 text-right font-medium text-slate-200">
                      {{ item.expectedCodAmountSar | number:'1.2-2' }} SAR
                    </td>

                    <!-- Remitted COD -->
                    <td class="py-3 px-4 text-right font-medium" [class.text-rose-400]="item.status === 'Underpaid' || item.status === 'LostInTransit'">
                      {{ item.remittedAmountSar | number:'1.2-2' }} SAR
                    </td>

                    <!-- Courier Fee -->
                    <td class="py-3 px-4 text-right text-slate-400">
                      {{ item.courierFeeSar | number:'1.2-2' }} SAR
                    </td>

                    <!-- Discrepancy Amount -->
                    <td class="py-3 px-4 text-right font-bold" [class.text-rose-400]="item.discrepancyAmountSar > 0" [class.text-emerald-400]="item.discrepancyAmountSar === 0">
                      {{ item.discrepancyAmountSar > 0 ? '+' : '' }}{{ item.discrepancyAmountSar | number:'1.2-2' }} SAR
                    </td>

                    <!-- Status Pill -->
                    <td class="py-3 px-4 text-center">
                      @switch (item.status) {
                        @case ('Matched') {
                          <span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                            Matched
                          </span>
                        }
                        @case ('Underpaid') {
                          <span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20">
                            Underpaid
                          </span>
                        }
                        @case ('LostInTransit') {
                          <span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20">
                            Lost In Transit
                          </span>
                        }
                        @case ('RtoFeeDisputed') {
                          <span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-purple-500/10 text-purple-400 border border-purple-500/20">
                            RTO Disputed
                          </span>
                        }
                        @case ('Overpaid') {
                          <span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
                            Overpaid
                          </span>
                        }
                        @case ('UnmatchedWaybill') {
                          <span class="px-2 py-0.5 rounded-full text-[11px] font-semibold bg-slate-500/10 text-slate-400 border border-slate-500/20">
                            Unmatched Waybill
                          </span>
                        }
                      }
                    </td>

                    <!-- Reason -->
                    <td class="py-3 px-4 max-w-xs truncate text-slate-400" title="{{ item.discrepancyReason || 'Balanced' }}">
                      {{ item.discrepancyReason || 'Zero-variance reconciliation clean' }}
                    </td>

                    <!-- Actions -->
                    <td class="py-3 px-4 text-right">
                      @if (item.status !== 'Matched') {
                        @if (item.isDisputed) {
                          <span class="px-2 py-1 text-[11px] font-semibold text-amber-400 bg-amber-500/10 border border-amber-500/20 rounded-md">
                            Dispute Open
                          </span>
                        } @else {
                          <button
                            (click)="openDisputeModal(item)"
                            class="px-2.5 py-1 text-[11px] font-medium bg-rose-600/20 hover:bg-rose-600/30 text-rose-300 border border-rose-500/30 rounded-md transition"
                            data-testid="file-dispute-btn">
                            File Dispute
                          </button>
                        }
                      } @else {
                        <span class="text-[11px] text-slate-500 font-mono">OK</span>
                      }
                    </td>
                  </tr>
                } @empty {
                  <tr>
                    <td colspan="9" class="py-8 text-center text-slate-500">
                      No remittance items matching your current filters.
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
      </main>

      <!-- Upload Remittance Batch Simulation Drawer -->
      @if (isUploadDrawerOpen()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex justify-end" (click)="closeUploadDrawer()">
          <div class="w-full max-w-lg bg-slate-900 border-l border-slate-800 h-full overflow-y-auto p-6 space-y-6" (click)="$event.stopPropagation()">
            <div class="flex items-center justify-between border-b border-slate-800 pb-4">
              <div>
                <h3 class="text-lg font-bold text-white">Upload Remittance Batch</h3>
                <p class="text-xs text-slate-400">Simulate carrier settlement bordereau ingestion & reconciliation</p>
              </div>
              <button (click)="closeUploadDrawer()" class="text-slate-400 hover:text-white transition">
                <svg class="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>

            <!-- Carrier selector & Batch Reference -->
            <div class="space-y-4">
              <div>
                <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">Carrier</label>
                <select
                  [(ngModel)]="uploadFormCarrier"
                  class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-emerald-500">
                  <option value="SMSA">SMSA Express (Saudi Arabia)</option>
                  <option value="Aramex">Aramex Logistics</option>
                  <option value="SPL">SPL (Saudi Post & Logistics)</option>
                  <option value="JTExpress">J&T Express KSA</option>
                  <option value="Other">Other MENA Courier</option>
                </select>
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">Batch Bordereau Reference</label>
                <input
                  type="text"
                  [(ngModel)]="uploadFormBatchRef"
                  placeholder="e.g. BORD-2026-11-09"
                  class="w-full bg-slate-800 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-100 focus:outline-none focus:border-emerald-500" />
              </div>

              <!-- Presets -->
              <div>
                <label class="block text-xs font-semibold text-slate-300 uppercase mb-1.5">Load Carrier Simulation Preset</label>
                <div class="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    (click)="loadSimulationPreset('smsa')"
                    class="px-2.5 py-1.5 text-xs bg-slate-800 hover:bg-slate-700 border border-slate-700 rounded-lg text-slate-200 transition text-left">
                    <span class="font-semibold block text-emerald-400">SMSA Clean</span>
                    <span class="text-[10px] text-slate-400">100% matched settle</span>
                  </button>
                  <button
                    type="button"
                    (click)="loadSimulationPreset('aramex')"
                    class="px-2.5 py-1.5 text-xs bg-slate-800 hover:bg-slate-700 border border-slate-700 rounded-lg text-slate-200 transition text-left">
                    <span class="font-semibold block text-rose-400">Aramex Loss</span>
                    <span class="text-[10px] text-slate-400">Lost transit + shortfall</span>
                  </button>
                  <button
                    type="button"
                    (click)="loadSimulationPreset('spl')"
                    class="px-2.5 py-1.5 text-xs bg-slate-800 hover:bg-slate-700 border border-slate-700 rounded-lg text-slate-200 transition text-left">
                    <span class="font-semibold block text-purple-400">SPL RTO Dispute</span>
                    <span class="text-[10px] text-slate-400">Unjustified return fee</span>
                  </button>
                  <button
                    type="button"
                    (click)="loadSimulationPreset('jt')"
                    class="px-2.5 py-1.5 text-xs bg-slate-800 hover:bg-slate-700 border border-slate-700 rounded-lg text-slate-200 transition text-left">
                    <span class="font-semibold block text-amber-400">J&T Mixed</span>
                    <span class="text-[10px] text-slate-400">Unmatched waybill</span>
                  </button>
                </div>
              </div>

              <!-- Raw JSON Editor -->
              <div>
                <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">Parsed Items Payload (JSON)</label>
                <textarea
                  [(ngModel)]="uploadFormJson"
                  rows="8"
                  class="w-full bg-slate-950 font-mono text-xs border border-slate-800 rounded-lg p-3 text-emerald-300 focus:outline-none focus:border-emerald-500">
                </textarea>
              </div>

              <!-- Submit button -->
              <div class="pt-4 border-t border-slate-800 flex items-center justify-end gap-3">
                <button
                  type="button"
                  (click)="closeUploadDrawer()"
                  class="px-4 py-2 text-sm text-slate-400 hover:text-white transition">
                  Cancel
                </button>
                <button
                  type="button"
                  (click)="submitBatchUpload()"
                  class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-medium transition shadow-lg shadow-emerald-600/20"
                  data-testid="reconcile-batch-btn">
                  Reconcile Batch Now
                </button>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Dispute Filing Modal -->
      @if (isDisputeModalOpen()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4" (click)="closeDisputeModal()">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-2xl" (click)="$event.stopPropagation()">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div class="flex items-center gap-2">
                <span class="w-8 h-8 rounded-lg bg-rose-500/10 text-rose-400 flex items-center justify-center font-bold">!</span>
                <div>
                  <h3 class="text-base font-bold text-white">File Carrier Remittance Dispute</h3>
                  <p class="text-xs text-slate-400">Waybill: {{ activeDisputeItem()?.trackingNumber }}</p>
                </div>
              </div>
              <button (click)="closeDisputeModal()" class="text-slate-400 hover:text-white transition">&times;</button>
            </div>

            <div class="bg-slate-950 border border-slate-800/80 rounded-xl p-3 space-y-2 text-xs">
              <div class="flex justify-between">
                <span class="text-slate-400">Carrier:</span>
                <span class="font-semibold text-white">{{ activeDisputeItem()?.courier }}</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-400">Expected COD:</span>
                <span class="font-medium text-slate-200">{{ activeDisputeItem()?.expectedCodAmountSar | number:'1.2-2' }} SAR</span>
              </div>
              <div class="flex justify-between">
                <span class="text-slate-400">Carrier Remitted:</span>
                <span class="font-medium text-rose-400">{{ activeDisputeItem()?.remittedAmountSar | number:'1.2-2' }} SAR</span>
              </div>
              <div class="flex justify-between border-t border-slate-800 pt-1.5 font-bold">
                <span class="text-rose-400">Disputed Variance:</span>
                <span class="text-rose-400">{{ activeDisputeItem()?.discrepancyAmountSar | number:'1.2-2' }} SAR</span>
              </div>
            </div>

            <div>
              <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">Dispute Justification & Carrier SLA Claim</label>
              <textarea
                [(ngModel)]="disputeReasonText"
                rows="3"
                class="w-full bg-slate-800 border border-slate-700 rounded-lg p-2.5 text-xs text-slate-100 focus:outline-none focus:border-rose-500">
              </textarea>
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button
                type="button"
                (click)="closeDisputeModal()"
                class="px-4 py-2 text-xs text-slate-400 hover:text-white transition">
                Cancel
              </button>
              <button
                type="button"
                (click)="submitDispute()"
                class="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded-lg text-xs font-semibold transition shadow-lg shadow-rose-600/20"
                data-testid="submit-dispute-btn">
                Submit Formal Dispute Claim
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class CodCourierReconciliationStudioComponent {
  // Batches signal
  batches = signal<CourierRemittanceBatch[]>([]);

  // Disputes signal
  disputes = signal<CourierDisputeRecord[]>([]);

  // Filters
  selectedStatusFilter = signal<string>('All');
  selectedCourierFilter = signal<string>('All');
  searchTerm = signal<string>('');

  // UI state
  isUploadDrawerOpen = signal<boolean>(false);
  isDisputeModalOpen = signal<boolean>(false);
  activeDisputeItem = signal<CourierRemittanceItem | null>(null);
  disputeReasonText = '';

  // Upload Form state
  uploadFormCarrier: CourierCode = 'SMSA';
  uploadFormBatchRef = 'SMSA-BATCH-' + new Date().toISOString().slice(0, 10).replace(/-/g, '');
  uploadFormJson = JSON.stringify(PRESET_SMSA_CLEAN, null, 2);

  constructor() {
    this.seedInitialState();
  }

  // All flattened ledger items with batch reference and courier
  allLedgerItems = computed(() => {
    const list: CourierRemittanceItem[] = [];
    for (const b of this.batches()) {
      for (const item of b.items) {
        list.push({
          ...item,
          batchId: b.batchId,
          batchReference: b.batchReference,
          courier: b.courier
        });
      }
    }
    return list;
  });

  // Filtered ledger items
  filteredLedgerItems = computed(() => {
    let items = this.allLedgerItems();
    const statusFilter = this.selectedStatusFilter();
    const courierFilter = this.selectedCourierFilter();
    const term = this.searchTerm().trim().toLowerCase();

    if (statusFilter === 'DiscrepantOnly') {
      items = items.filter(i => i.status !== 'Matched');
    } else if (statusFilter !== 'All') {
      items = items.filter(i => i.status === statusFilter);
    }

    if (courierFilter !== 'All') {
      items = items.filter(i => i.courier === courierFilter);
    }

    if (term) {
      items = items.filter(
        i =>
          i.trackingNumber.toLowerCase().includes(term) ||
          i.orderNumber.toLowerCase().includes(term) ||
          (i.discrepancyReason && i.discrepancyReason.toLowerCase().includes(term))
      );
    }

    return items;
  });

  // KPI Calculations
  totalCashRemitted = computed(() => {
    return this.allLedgerItems().reduce((acc, i) => acc + i.remittedAmountSar, 0);
  });

  discrepancyLeakage = computed(() => {
    return this.allLedgerItems().reduce((acc, i) => acc + i.discrepancyAmountSar, 0);
  });

  totalParcelsCount = computed(() => {
    return this.allLedgerItems().length;
  });

  matchedParcelsCount = computed(() => {
    return this.allLedgerItems().filter(i => i.status === 'Matched').length;
  });

  discrepantItemsCount = computed(() => {
    return this.allLedgerItems().filter(i => i.status !== 'Matched').length;
  });

  cleanMatchRate = computed(() => {
    const total = this.totalParcelsCount();
    if (total === 0) return 100;
    return (this.matchedParcelsCount() / total) * 100;
  });

  openDisputesCount = computed(() => {
    return this.disputes().filter(d => d.status === 'Open' || d.status === 'UnderReview').length;
  });

  // Courier Performance Cards
  courierPerformance = computed<CourierPerformanceKpi[]>(() => {
    const all = this.allLedgerItems();
    const carriers: { code: CourierCode; name: string; logo: string; avgDays: number }[] = [
      { code: 'SMSA', name: 'SMSA Express', logo: 'SM', avgDays: 3.2 },
      { code: 'Aramex', name: 'Aramex', logo: 'AR', avgDays: 4.5 },
      { code: 'SPL', name: 'SPL (Saudi Post)', logo: 'SP', avgDays: 5.1 },
      { code: 'JTExpress', name: 'J&T Express', logo: 'JT', avgDays: 3.8 }
    ];

    return carriers.map(c => {
      const items = all.filter(i => i.courier === c.code);
      const totalCount = items.length;
      const deliveredCount = items.filter(i => i.status !== 'LostInTransit' && i.status !== 'UnmatchedWaybill').length;
      const cash = items.reduce((acc, i) => acc + i.remittedAmountSar, 0);
      const discrepant = items.filter(i => i.status !== 'Matched').length;
      const discRate = totalCount > 0 ? (discrepant / totalCount) * 100 : 0;

      return {
        courierCode: c.code,
        courierName: c.name,
        deliveredParcels: deliveredCount,
        totalCashRemittedSar: cash,
        discrepancyRatePercent: discRate,
        avgSettlementDays: c.avgDays,
        logoIcon: c.logo
      };
    });
  });

  setStatusFilter(status: string) {
    this.selectedStatusFilter.set(status);
  }

  setCourierFilter(courier: string) {
    this.selectedCourierFilter.set(courier);
  }

  openUploadDrawer() {
    this.isUploadDrawerOpen.set(true);
  }

  closeUploadDrawer() {
    this.isUploadDrawerOpen.set(false);
  }

  openDisputeModal(item: CourierRemittanceItem) {
    this.activeDisputeItem.set(item);
    this.disputeReasonText =
      item.discrepancyReason ||
      `Official SLA dispute claim for ${item.courier} waybill ${item.trackingNumber} discrepancy of ${item.discrepancyAmountSar} SAR.`;
    this.isDisputeModalOpen.set(true);
  }

  closeDisputeModal() {
    this.isDisputeModalOpen.set(false);
    this.activeDisputeItem.set(null);
    this.disputeReasonText = '';
  }

  loadSimulationPreset(presetKey: string) {
    switch (presetKey) {
      case 'smsa':
        this.uploadFormCarrier = 'SMSA';
        this.uploadFormBatchRef = 'SMSA-SETTLE-' + Math.floor(1000 + Math.random() * 9000);
        this.uploadFormJson = JSON.stringify(PRESET_SMSA_CLEAN, null, 2);
        break;
      case 'aramex':
        this.uploadFormCarrier = 'Aramex';
        this.uploadFormBatchRef = 'ARX-SETTLE-' + Math.floor(1000 + Math.random() * 9000);
        this.uploadFormJson = JSON.stringify(PRESET_ARAMEX_LOSS, null, 2);
        break;
      case 'spl':
        this.uploadFormCarrier = 'SPL';
        this.uploadFormBatchRef = 'SPL-CR-' + Math.floor(1000 + Math.random() * 9000);
        this.uploadFormJson = JSON.stringify(PRESET_SPL_RTO, null, 2);
        break;
      case 'jt':
        this.uploadFormCarrier = 'JTExpress';
        this.uploadFormBatchRef = 'JT-DISB-' + Math.floor(1000 + Math.random() * 9000);
        this.uploadFormJson = JSON.stringify(PRESET_JT_MIXED, null, 2);
        break;
    }
  }

  submitBatchUpload() {
    try {
      const parsedItems: RawRemittanceInputItem[] = JSON.parse(this.uploadFormJson);
      this.ingestBatch({
        batchReference: this.uploadFormBatchRef,
        courier: this.uploadFormCarrier,
        items: parsedItems
      });
      this.closeUploadDrawer();
    } catch {
      alert('Invalid JSON in remittance payload editor.');
    }
  }

  ingestBatch(payload: {
    batchReference: string;
    courier: CourierCode;
    items: RawRemittanceInputItem[];
  }): CourierRemittanceBatch {
    const reconciledItems: CourierRemittanceItem[] = payload.items.map(input => {
      let expected = input.expectedCodAmountSar ?? input.remittedAmountSar;
      let status: RemittanceMatchStatus = 'Matched';
      let discrepancy = 0;
      let reason: string | null = null;

      if (input.orderNumber.includes('UNKNOWN') || input.trackingNumber.includes('UNMATCHED')) {
        status = 'UnmatchedWaybill';
        expected = 0;
        discrepancy = input.remittedAmountSar;
        reason = `Waybill ${input.trackingNumber} not recognized in order database.`;
      } else if (input.isReturned) {
        status = 'RtoFeeDisputed';
        expected = 0;
        discrepancy = input.courierFeeSar;
        reason = `Disputed RTO return fee of SAR ${input.courierFeeSar.toFixed(2)} charged for returned order ${input.orderNumber}.`;
      } else if (!input.isDelivered) {
        status = 'LostInTransit';
        discrepancy = expected;
        reason = `Shipment ${input.trackingNumber} marked lost in transit by carrier.`;
      } else {
        const diff = expected - input.remittedAmountSar;
        if (Math.abs(diff) < 0.01) {
          status = 'Matched';
          discrepancy = 0;
          reason = null;
        } else if (diff > 0) {
          status = 'Underpaid';
          discrepancy = diff;
          reason = `Carrier underpaid: expected SAR ${expected.toFixed(2)}, remitted SAR ${input.remittedAmountSar.toFixed(2)}.`;
        } else {
          status = 'Overpaid';
          discrepancy = Math.abs(diff);
          reason = `Carrier overpaid: expected SAR ${expected.toFixed(2)}, remitted SAR ${input.remittedAmountSar.toFixed(2)}.`;
        }
      }

      return {
        trackingNumber: input.trackingNumber,
        orderNumber: input.orderNumber,
        expectedCodAmountSar: expected,
        remittedAmountSar: input.remittedAmountSar,
        courierFeeSar: input.courierFeeSar,
        discrepancyAmountSar: discrepancy,
        status,
        discrepancyReason: reason,
        isDisputed: false,
        courier: payload.courier
      };
    });

    const newBatch: CourierRemittanceBatch = {
      batchId: 'batch_' + Math.random().toString(36).substring(2, 9),
      batchReference: payload.batchReference,
      courier: payload.courier,
      settlementDateUtc: new Date().toISOString(),
      totalParcels: reconciledItems.length,
      totalExpectedCodSar: reconciledItems.reduce((acc, i) => acc + i.expectedCodAmountSar, 0),
      totalRemittedCodSar: reconciledItems.reduce((acc, i) => acc + i.remittedAmountSar, 0),
      totalCourierFeesSar: reconciledItems.reduce((acc, i) => acc + i.courierFeeSar, 0),
      netDiscrepancySar: reconciledItems.reduce((acc, i) => acc + i.discrepancyAmountSar, 0),
      items: reconciledItems,
      ingestedAtUtc: new Date().toISOString()
    };

    this.batches.update(current => [newBatch, ...current]);
    return newBatch;
  }

  submitDispute() {
    const item = this.activeDisputeItem();
    if (!item) return;

    this.fileDispute(item, this.disputeReasonText);
    this.closeDisputeModal();
  }

  fileDispute(item: CourierRemittanceItem, reasonText?: string): CourierDisputeRecord {
    const dispute: CourierDisputeRecord = {
      disputeId: 'disp_' + Math.random().toString(36).substring(2, 9),
      batchId: item.batchId || 'batch_default',
      trackingNumber: item.trackingNumber,
      courier: item.courier || 'Other',
      disputedAmountSar: item.discrepancyAmountSar,
      reason: reasonText || item.discrepancyReason || 'Discrepancy detected in carrier remittance',
      status: 'Open',
      filedAtUtc: new Date().toISOString()
    };

    // Update dispute signal
    this.disputes.update(list => [dispute, ...list]);

    // Update item isDisputed flag in batches
    this.batches.update(batches =>
      batches.map(b => ({
        ...b,
        items: b.items.map(i =>
          i.trackingNumber === item.trackingNumber ? { ...i, isDisputed: true } : i
        )
      }))
    );

    return dispute;
  }

  private seedInitialState() {
    const smsaBatch: CourierRemittanceBatch = {
      batchId: 'batch_smsa_001',
      batchReference: 'SMSA-REMIT-SA-20261101',
      courier: 'SMSA',
      settlementDateUtc: new Date(Date.now() - 5 * 86400000).toISOString(),
      totalParcels: 5,
      totalExpectedCodSar: 1780.00,
      totalRemittedCodSar: 1680.00,
      totalCourierFeesSar: 140.00,
      netDiscrepancySar: 100.00,
      ingestedAtUtc: new Date(Date.now() - 5 * 86400000).toISOString(),
      items: [
        { trackingNumber: 'SMSA-901827101', orderNumber: 'ORD-SA-8812', expectedCodAmountSar: 450.00, remittedAmountSar: 450.00, courierFeeSar: 28.00, discrepancyAmountSar: 0.00, status: 'Matched', discrepancyReason: null, isDisputed: false, courier: 'SMSA' },
        { trackingNumber: 'SMSA-901827102', orderNumber: 'ORD-SA-8813', expectedCodAmountSar: 320.00, remittedAmountSar: 320.00, courierFeeSar: 28.00, discrepancyAmountSar: 0.00, status: 'Matched', discrepancyReason: null, isDisputed: false, courier: 'SMSA' },
        { trackingNumber: 'SMSA-901827103', orderNumber: 'ORD-SA-8814', expectedCodAmountSar: 620.00, remittedAmountSar: 520.00, courierFeeSar: 28.00, discrepancyAmountSar: 100.00, status: 'Underpaid', discrepancyReason: 'Carrier short-remitted COD collection: expected SAR 620.00, received SAR 520.00 (underpaid by SAR 100.00).', isDisputed: false, courier: 'SMSA' },
        { trackingNumber: 'SMSA-901827104', orderNumber: 'ORD-SA-8815', expectedCodAmountSar: 210.00, remittedAmountSar: 210.00, courierFeeSar: 28.00, discrepancyAmountSar: 0.00, status: 'Matched', discrepancyReason: null, isDisputed: false, courier: 'SMSA' },
        { trackingNumber: 'SMSA-901827105', orderNumber: 'ORD-SA-8816', expectedCodAmountSar: 180.00, remittedAmountSar: 180.00, courierFeeSar: 28.00, discrepancyAmountSar: 0.00, status: 'Matched', discrepancyReason: null, isDisputed: false, courier: 'SMSA' }
      ]
    };

    const aramexBatch: CourierRemittanceBatch = {
      batchId: 'batch_arx_002',
      batchReference: 'ARX-SETTLE-KSA-88391',
      courier: 'Aramex',
      settlementDateUtc: new Date(Date.now() - 3 * 86400000).toISOString(),
      totalParcels: 4,
      totalExpectedCodSar: 2000.00,
      totalRemittedCodSar: 1600.00,
      totalCourierFeesSar: 96.00,
      netDiscrepancySar: 440.00,
      ingestedAtUtc: new Date(Date.now() - 3 * 86400000).toISOString(),
      items: [
        { trackingNumber: 'ARX-992014110', orderNumber: 'ORD-SA-9101', expectedCodAmountSar: 540.00, remittedAmountSar: 540.00, courierFeeSar: 32.00, discrepancyAmountSar: 0.00, status: 'Matched', discrepancyReason: null, isDisputed: false, courier: 'Aramex' },
        { trackingNumber: 'ARX-992014111', orderNumber: 'ORD-SA-9102', expectedCodAmountSar: 420.00, remittedAmountSar: 0.00, courierFeeSar: 0.00, discrepancyAmountSar: 420.00, status: 'LostInTransit', discrepancyReason: 'Shipment reported lost in Aramex Riyadh Central Sort Hub; COD value SAR 420.00 unremitted.', isDisputed: true, courier: 'Aramex' },
        { trackingNumber: 'ARX-992014112', orderNumber: 'ORD-SA-9103', expectedCodAmountSar: 290.00, remittedAmountSar: 310.00, courierFeeSar: 32.00, discrepancyAmountSar: 20.00, status: 'Overpaid', discrepancyReason: 'Carrier remitted excess collection: expected SAR 290.00, received SAR 310.00.', isDisputed: false, courier: 'Aramex' },
        { trackingNumber: 'ARX-992014113', orderNumber: 'ORD-SA-9104', expectedCodAmountSar: 750.00, remittedAmountSar: 750.00, courierFeeSar: 32.00, discrepancyAmountSar: 0.00, status: 'Matched', discrepancyReason: null, isDisputed: false, courier: 'Aramex' }
      ]
    };

    const splBatch: CourierRemittanceBatch = {
      batchId: 'batch_spl_003',
      batchReference: 'SPL-CR-20261028-09',
      courier: 'SPL',
      settlementDateUtc: new Date(Date.now() - 1 * 86400000).toISOString(),
      totalParcels: 4,
      totalExpectedCodSar: 1020.00,
      totalRemittedCodSar: 1020.00,
      totalCourierFeesSar: 101.00,
      netDiscrepancySar: 35.00,
      ingestedAtUtc: new Date(Date.now() - 1 * 86400000).toISOString(),
      items: [
        { trackingNumber: 'SPL-330192841', orderNumber: 'ORD-SA-9201', expectedCodAmountSar: 180.00, remittedAmountSar: 180.00, courierFeeSar: 22.00, discrepancyAmountSar: 0.00, status: 'Matched', discrepancyReason: null, isDisputed: false, courier: 'SPL' },
        { trackingNumber: 'SPL-330192842', orderNumber: 'ORD-SA-9202', expectedCodAmountSar: 0.00, remittedAmountSar: 0.00, courierFeeSar: 35.00, discrepancyAmountSar: 35.00, status: 'RtoFeeDisputed', discrepancyReason: 'Unjustified RTO return fee SAR 35.00 billed without delivery attempt proof on returned shipment.', isDisputed: false, courier: 'SPL' },
        { trackingNumber: 'SPL-330192843', orderNumber: 'ORD-SA-9203', expectedCodAmountSar: 350.00, remittedAmountSar: 350.00, courierFeeSar: 22.00, discrepancyAmountSar: 0.00, status: 'Matched', discrepancyReason: null, isDisputed: false, courier: 'SPL' },
        { trackingNumber: 'SPL-330192844', orderNumber: 'ORD-SA-9204', expectedCodAmountSar: 490.00, remittedAmountSar: 490.00, courierFeeSar: 22.00, discrepancyAmountSar: 0.00, status: 'Matched', discrepancyReason: null, isDisputed: false, courier: 'SPL' }
      ]
    };

    this.batches.set([smsaBatch, aramexBatch, splBatch]);

    this.disputes.set([
      {
        disputeId: 'disp_seed_001',
        batchId: 'batch_arx_002',
        trackingNumber: 'ARX-992014111',
        courier: 'Aramex',
        disputedAmountSar: 420.00,
        reason: 'Aramex transit loss claim filed under SLA Section 4.2 for missing shipment in Riyadh Hub',
        status: 'Open',
        filedAtUtc: new Date(Date.now() - 2 * 86400000).toISOString()
      }
    ]);
  }
}
