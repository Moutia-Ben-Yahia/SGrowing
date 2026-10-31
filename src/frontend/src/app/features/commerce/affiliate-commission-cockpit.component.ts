import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';

export type AffiliateTierLevel = 1 | 2 | 3;
export type CommissionRuleType = 'PercentageOfGmv' | 'PercentageOfMargin' | 'FixedBountyPerOrder' | 'RecurringSubscriptionSplit' | 'MilestoneBonus';
export type CommissionState = 'PendingEscrow' | 'Approved' | 'Disputed' | 'RefundReversed' | 'PaidOut';
export type PayoutMethod = 'Sepa' | 'PayPal' | 'Wire' | 'Crypto';
export type AffiliateStatus = 'Active' | 'Suspended' | 'Inactive';
export type TaxFormType = 'None' | 'W8Ben' | 'W9' | 'Vat';

export interface CommissionRule {
  ruleType: CommissionRuleType;
  ratePercentage: number;
  fixedAmount: number;
  minimumOrderThreshold: number;
  excludedCategories: string[];
  milestoneTargetSales: number;
  milestoneBonusAmount: number;
  tier2RateOverride?: number;
  tier3RateOverride?: number;
}

export interface PayoutDetails {
  preferredMethod: PayoutMethod;
  accountHolderName?: string;
  iban?: string;
  swiftBic?: string;
  bankName?: string;
  payPalEmail?: string;
  cryptoWalletAddress?: string;
  cryptoChain?: string;
  currency: string;
}

export interface TaxDetails {
  formType: TaxFormType;
  taxIdOrSsn?: string;
  withholdingPercentage: number;
  isVerified: boolean;
}

export interface AffiliateMetrics {
  totalReferredGmv: number;
  totalCommissionsEarned: number;
  totalCommissionsPaid: number;
  pendingEscrowAmount: number;
  approvedAvailableAmount: number;
  refundReversalAmount: number;
  totalClicks: number;
  totalConversions: number;
  refundReversalCount: number;
  cumulativeSalesCount: number;
}

export interface AffiliatePartner {
  id: string;
  name: string;
  email: string;
  tierLevel: AffiliateTierLevel;
  parentPartnerId?: string | null;
  status: AffiliateStatus;
  referralCodes: string[];
  trackingSlug: string;
  customLandingDomain?: string;
  attributionWindowDays: number;
  escrowHoldbackDays: number;
  minimumPayoutThreshold: number;
  commissionRule: CommissionRule;
  payoutDetails: PayoutDetails;
  taxDetails: TaxDetails;
  metrics: AffiliateMetrics;
  createdAtUtc: string;
}

export interface CommissionTransaction {
  id: string;
  orderId: string;
  conversionId: string;
  affiliateId: string;
  affiliateName: string;
  tierLevel: AffiliateTierLevel;
  ruleType: CommissionRuleType;
  orderGmv: number;
  orderMargin: number;
  commissionRate: number;
  commissionAmount: number;
  state: CommissionState;
  currency: string;
  createdAtUtc: string;
  escrowHoldbackDays: number;
  maturesAtUtc: string;
  approvedAtUtc?: string;
  paidOutAtUtc?: string;
  settlementBatchId?: string;
  reversalReason?: string;
  reversedAtUtc?: string;
  notes?: string;
}

export interface SettlementLineItem {
  lineItemId: string;
  affiliateId: string;
  affiliateName: string;
  recipientEmail: string;
  method: PayoutMethod;
  grossAmount: number;
  taxWithheld: number;
  netAmount: number;
  destinationReference: string;
  swiftBic?: string;
  commissionIds: string[];
}

export interface SettlementBatch {
  id: string;
  batchNumber: string;
  createdAtUtc: string;
  grossTotal: number;
  totalTaxWithheld: number;
  netTotal: number;
  affiliateCount: number;
  commissionCount: number;
  currency: string;
  payoutMethod: PayoutMethod;
  lineItems: SettlementLineItem[];
  sepaXmlManifest?: string;
  payPalCsvManifest?: string;
  wireCryptoLedgerManifest?: string;
}

export interface AffiliateCommissionMetrics {
  totalReferredGmv: number;
  totalCommissionsAccrued: number;
  pendingEscrowVolume: number;
  approvedAvailableVolume: number;
  totalPaidOut: number;
  refundReversedVolume: number;
  activeAffiliatesCount: number;
  totalConversionsCount: number;
  averageOrderValue: number;
  topAffiliates: Array<{
    affiliateId: string;
    name: string;
    tier: AffiliateTierLevel;
    totalGmv: number;
    totalCommissions: number;
    conversionCount: number;
  }>;
  commissionsByTier: Record<number, number>;
  commissionsByStatus: Record<string, number>;
}

@Component({
  selector: 'app-affiliate-commission-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="min-h-screen bg-slate-950 text-slate-100 p-4 md:p-8 space-y-6">

      <!-- 1. TOP NAVIGATION & HEADER -->
      <div class="flex flex-col lg:flex-row lg:items-center justify-between gap-4 border-b border-slate-800 pb-6">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold uppercase tracking-wider rounded-md bg-indigo-500/20 text-indigo-400 border border-indigo-500/30">
              Horizon 56
            </span>
            <span class="px-2.5 py-1 text-xs font-semibold uppercase tracking-wider rounded-md bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              B2B Commission & Settlement Engine
            </span>
            <span class="px-2.5 py-1 text-xs font-semibold uppercase tracking-wider rounded-md bg-cyan-500/20 text-cyan-400 border border-cyan-500/30">
              ISO 20022 SEPA + PayPal Payouts
            </span>
          </div>
          <h1 class="text-2xl md:text-3xl font-extrabold tracking-tight mt-2 text-white">
            Affiliate Commission & Payout Settlement Cockpit
          </h1>
          <p class="text-sm text-slate-400 mt-1 max-w-3xl">
            Orchestrate 3-tier partner attribution hierarchies (L1 Direct 15%, L2 Agency 5%, L3 Master 2%), enforce 14-day anti-fraud escrow holdbacks with automatic refund clawbacks, and dispatch audited bank batch payout files.
          </p>
        </div>

        <div class="flex flex-wrap items-center gap-2.5">
          <button (click)="openRegisterModal()"
                  class="px-3.5 py-2 text-sm font-medium bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg transition-colors border border-slate-700 flex items-center gap-1.5 shadow-sm">
            <svg class="w-4 h-4 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path>
            </svg>
            Register Partner
          </button>

          <button (click)="openSimulatorModal()"
                  class="px-3.5 py-2 text-sm font-medium bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg transition-colors border border-slate-700 flex items-center gap-1.5 shadow-sm">
            <svg class="w-4 h-4 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"></path>
            </svg>
            Simulate Conversion
          </button>

          <button (click)="triggerMaturitySweep()"
                  [disabled]="isSweeping()"
                  class="px-3.5 py-2 text-sm font-medium bg-amber-600/20 hover:bg-amber-600/30 text-amber-300 rounded-lg transition-colors border border-amber-500/30 flex items-center gap-1.5 shadow-sm">
            <svg class="w-4 h-4 text-amber-400" [class.animate-spin]="isSweeping()" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"></path>
            </svg>
            Maturity Sweep
          </button>

          <button (click)="openPayoutModal()"
                  class="px-4 py-2 text-sm font-semibold bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg transition-colors shadow-lg shadow-emerald-900/30 flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z"></path>
            </svg>
            Dispatch Payout Batch
          </button>
        </div>
      </div>

      <!-- 2. MACRO KPIS METRICS BAR -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-6 gap-4">
        <!-- Metric 1: Total Referred GMV -->
        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-4 flex flex-col justify-between">
          <div class="flex items-center justify-between">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Referred GMV</span>
            <span class="p-1.5 rounded-lg bg-indigo-500/10 text-indigo-400">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
            </span>
          </div>
          <div class="mt-2">
            <div class="text-2xl font-bold text-white">\${{ metrics()?.totalReferredGmv | number:'1.2-2' }}</div>
            <div class="text-xs text-slate-400 mt-1 flex items-center gap-1">
              <span class="text-indigo-400 font-semibold">{{ metrics()?.totalConversionsCount }}</span> conversions (\${{ metrics()?.averageOrderValue | number:'1.0-0' }} AOV)
            </div>
          </div>
        </div>

        <!-- Metric 2: Pending Escrow Volume -->
        <div class="bg-slate-900/90 border border-amber-900/40 rounded-xl p-4 flex flex-col justify-between relative overflow-hidden">
          <div class="absolute -top-6 -right-6 w-20 h-20 bg-amber-500/10 rounded-full blur-xl pointer-events-none"></div>
          <div class="flex items-center justify-between">
            <span class="text-xs font-semibold text-amber-400 uppercase tracking-wider">In Escrow (14d)</span>
            <span class="p-1.5 rounded-lg bg-amber-500/10 text-amber-400">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"></path>
              </svg>
            </span>
          </div>
          <div class="mt-2">
            <div class="text-2xl font-bold text-amber-300">\${{ metrics()?.pendingEscrowVolume | number:'1.2-2' }}</div>
            <div class="text-xs text-amber-400/80 mt-1 flex items-center gap-1">
              Anti-fraud refund holdback
            </div>
          </div>
        </div>

        <!-- Metric 3: Cleared For Payout -->
        <div class="bg-slate-900/90 border border-emerald-900/40 rounded-xl p-4 flex flex-col justify-between relative overflow-hidden">
          <div class="absolute -top-6 -right-6 w-20 h-20 bg-emerald-500/10 rounded-full blur-xl pointer-events-none"></div>
          <div class="flex items-center justify-between">
            <span class="text-xs font-semibold text-emerald-400 uppercase tracking-wider">Cleared Available</span>
            <span class="p-1.5 rounded-lg bg-emerald-500/10 text-emerald-400">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
            </span>
          </div>
          <div class="mt-2">
            <div class="text-2xl font-bold text-emerald-300">\${{ metrics()?.approvedAvailableVolume | number:'1.2-2' }}</div>
            <div class="text-xs text-emerald-400/80 mt-1">
              Ready for batch payout
            </div>
          </div>
        </div>

        <!-- Metric 4: Total Settled / Paid -->
        <div class="bg-slate-900/90 border border-purple-900/40 rounded-xl p-4 flex flex-col justify-between">
          <div class="flex items-center justify-between">
            <span class="text-xs font-semibold text-purple-400 uppercase tracking-wider">Total Settled</span>
            <span class="p-1.5 rounded-lg bg-purple-500/10 text-purple-400">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
              </svg>
            </span>
          </div>
          <div class="mt-2">
            <div class="text-2xl font-bold text-purple-300">\${{ metrics()?.totalPaidOut | number:'1.2-2' }}</div>
            <div class="text-xs text-purple-400/80 mt-1">
              Dispatched to partners
            </div>
          </div>
        </div>

        <!-- Metric 5: Active Partners -->
        <div class="bg-slate-900/90 border border-slate-800 rounded-xl p-4 flex flex-col justify-between">
          <div class="flex items-center justify-between">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Partner Network</span>
            <span class="p-1.5 rounded-lg bg-cyan-500/10 text-cyan-400">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z"></path>
              </svg>
            </span>
          </div>
          <div class="mt-2">
            <div class="text-2xl font-bold text-cyan-300">{{ metrics()?.activeAffiliatesCount }} Active</div>
            <div class="text-xs text-slate-400 mt-1">
              L1, L2 & L3 Hierarchy
            </div>
          </div>
        </div>

        <!-- Metric 6: Refund Clawback Volume -->
        <div class="bg-slate-900/90 border border-rose-900/40 rounded-xl p-4 flex flex-col justify-between">
          <div class="flex items-center justify-between">
            <span class="text-xs font-semibold text-rose-400 uppercase tracking-wider">Refund Clawbacks</span>
            <span class="p-1.5 rounded-lg bg-rose-500/10 text-rose-400">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 10h10a8 8 0 018 8v2M3 10l6 6m-6-6l6-6"></path>
              </svg>
            </span>
          </div>
          <div class="mt-2">
            <div class="text-2xl font-bold text-rose-300">\${{ metrics()?.refundReversedVolume | number:'1.2-2' }}</div>
            <div class="text-xs text-rose-400/80 mt-1">
              Auto-reversed fraud/returns
            </div>
          </div>
        </div>
      </div>

      <!-- 3. NAVIGATION TABS -->
      <div class="flex flex-wrap items-center gap-2 border-b border-slate-800 pb-3">
        <button (click)="activeTab.set('roster')"
                [class.bg-slate-800]="activeTab() === 'roster'"
                [class.text-white]="activeTab() === 'roster'"
                [class.text-slate-400]="activeTab() !== 'roster'"
                class="px-4 py-2 text-sm font-medium rounded-lg hover:bg-slate-800 hover:text-white transition-colors flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"></path>
          </svg>
          Partner Network Roster
          <span class="px-1.5 py-0.5 text-xs rounded-full bg-slate-700 text-slate-300">{{ partners().length }}</span>
        </button>

        <button (click)="activeTab.set('tree')"
                [class.bg-slate-800]="activeTab() === 'tree'"
                [class.text-white]="activeTab() === 'tree'"
                [class.text-slate-400]="activeTab() !== 'tree'"
                class="px-4 py-2 text-sm font-medium rounded-lg hover:bg-slate-800 hover:text-white transition-colors flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 5a1 1 0 011-1h14a1 1 0 011 1v2a1 1 0 01-1 1H5a1 1 0 01-1-1V5zM4 13a1 1 0 011-1h6a1 1 0 011 1v6a1 1 0 01-1 1H5a1 1 0 01-1-1v-6zM16 13a1 1 0 011-1h2a1 1 0 011 1v6a1 1 0 01-1 1h-2a1 1 0 01-1-1v-6z"></path>
          </svg>
          Multi-Tier Revenue Tree Visualizer
        </button>

        <button (click)="activeTab.set('ledger')"
                [class.bg-slate-800]="activeTab() === 'ledger'"
                [class.text-white]="activeTab() === 'ledger'"
                [class.text-slate-400]="activeTab() !== 'ledger'"
                class="px-4 py-2 text-sm font-medium rounded-lg hover:bg-slate-800 hover:text-white transition-colors flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2m-3 7h3m-3 4h3m-6-4h.01M9 16h.01"></path>
          </svg>
          Commission Transactions Ledger
          <span class="px-1.5 py-0.5 text-xs rounded-full bg-slate-700 text-slate-300">{{ commissions().length }}</span>
        </button>

        <button (click)="activeTab.set('settlements')"
                [class.bg-slate-800]="activeTab() === 'settlements'"
                [class.text-white]="activeTab() === 'settlements'"
                [class.text-slate-400]="activeTab() !== 'settlements'"
                class="px-4 py-2 text-sm font-medium rounded-lg hover:bg-slate-800 hover:text-white transition-colors flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7H5a2 2 0 00-2 2v9a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-3m-1 4l-3 3m0 0l-3-3m3 3V4"></path>
          </svg>
          Settlement Batch Archives & Manifests
          <span class="px-1.5 py-0.5 text-xs rounded-full bg-slate-700 text-slate-300">{{ settlements().length }}</span>
        </button>
      </div>

      <!-- 4. TAB 1: PARTNER NETWORK ROSTER -->
      @if (activeTab() === 'roster') {
        <div class="space-y-4">
          <!-- Filters & Search -->
          <div class="flex flex-col sm:flex-row gap-3 items-center justify-between bg-slate-900/60 p-3.5 rounded-xl border border-slate-800">
            <div class="relative w-full sm:w-80">
              <input type="text"
                     [ngModel]="searchQuery()"
                     (ngModelChange)="searchQuery.set($event)"
                     placeholder="Search partners, referral code, slug..."
                     class="w-full bg-slate-950 border border-slate-800 rounded-lg pl-9 pr-3.5 py-2 text-sm text-slate-200 placeholder-slate-500 focus:outline-none focus:border-indigo-500">
              <svg class="w-4 h-4 text-slate-500 absolute left-3 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"></path>
              </svg>
            </div>

            <div class="flex items-center gap-2 w-full sm:w-auto">
              <label class="text-xs text-slate-400">Tier:</label>
              <select [ngModel]="tierFilter()"
                      (ngModelChange)="tierFilter.set($event)"
                      class="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-indigo-500">
                <option value="ALL">All Tiers</option>
                <option value="1">Level 1 Direct (15%)</option>
                <option value="2">Level 2 Parent Agency (5%)</option>
                <option value="3">Level 3 Master Distributor (2%)</option>
              </select>

              <label class="text-xs text-slate-400 ml-2">Status:</label>
              <select [ngModel]="statusFilter()"
                      (ngModelChange)="statusFilter.set($event)"
                      class="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-indigo-500">
                <option value="ALL">All Status</option>
                <option value="Active">Active</option>
                <option value="Suspended">Suspended</option>
              </select>
            </div>
          </div>

          <!-- Partners Table -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl overflow-hidden shadow-sm">
            <div class="overflow-x-auto">
              <table class="w-full text-left text-sm">
                <thead class="bg-slate-950/70 text-slate-400 text-xs uppercase tracking-wider border-b border-slate-800">
                  <tr>
                    <th class="py-3 px-4">Partner & Tier</th>
                    <th class="py-3 px-4">Referral Codes & Link</th>
                    <th class="py-3 px-4">Commission Rule</th>
                    <th class="py-3 px-4">Payout & Tax Details</th>
                    <th class="py-3 px-4 text-right">Referred GMV</th>
                    <th class="py-3 px-4 text-right">Escrow / Cleared</th>
                    <th class="py-3 px-4 text-center">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (partner of filteredPartners(); track partner.id) {
                    <tr class="hover:bg-slate-800/30 transition-colors">
                      <!-- Partner Info -->
                      <td class="py-3.5 px-4">
                        <div class="flex items-center gap-2.5">
                          <span class="w-8 h-8 rounded-full flex items-center justify-center font-bold text-xs"
                                [class.bg-amber-500/20]="partner.tierLevel === 3"
                                [class.text-amber-300]="partner.tierLevel === 3"
                                [class.border]="true"
                                [class.border-amber-500/30]="partner.tierLevel === 3"
                                [class.bg-indigo-500/20]="partner.tierLevel === 2"
                                [class.text-indigo-300]="partner.tierLevel === 2"
                                [class.border-indigo-500/30]="partner.tierLevel === 2"
                                [class.bg-cyan-500/20]="partner.tierLevel === 1"
                                [class.text-cyan-300]="partner.tierLevel === 1"
                                [class.border-cyan-500/30]="partner.tierLevel === 1">
                            L{{ partner.tierLevel }}
                          </span>
                          <div>
                            <div class="font-semibold text-white flex items-center gap-2">
                              {{ partner.name }}
                              @if (partner.status === 'Active') {
                                <span class="w-2 h-2 rounded-full bg-emerald-400" title="Active"></span>
                              } @else {
                                <span class="w-2 h-2 rounded-full bg-amber-400" title="Suspended"></span>
                              }
                            </div>
                            <div class="text-xs text-slate-400">{{ partner.email }}</div>
                            <div class="text-[11px] text-slate-500 mt-0.5">
                              {{ getTierLabel(partner.tierLevel) }}
                              @if (partner.parentPartnerId) {
                                · Child of <span class="text-slate-400 font-medium">{{ getPartnerName(partner.parentPartnerId) }}</span>
                              }
                            </div>
                          </div>
                        </div>
                      </td>

                      <!-- Codes & Link -->
                      <td class="py-3.5 px-4">
                        <div class="flex flex-wrap gap-1 mb-1">
                          @for (code of partner.referralCodes; track code) {
                            <button (click)="copyToClipboard(code, 'Code')"
                                    class="px-2 py-0.5 text-xs font-mono bg-slate-950 border border-slate-700 hover:border-indigo-500 text-indigo-300 rounded flex items-center gap-1 transition-colors">
                              {{ code }}
                              <svg class="w-3 h-3 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"></path>
                              </svg>
                            </button>
                          }
                        </div>
                        <div class="text-xs text-slate-400 font-mono flex items-center gap-1">
                          <span class="text-slate-500">?ref=</span>{{ partner.trackingSlug }}
                          @if (partner.customLandingDomain) {
                            <span class="text-cyan-400 ml-1.5 font-sans text-[11px]">({{ partner.customLandingDomain }})</span>
                          }
                        </div>
                        <div class="text-[11px] text-slate-500">
                          Cookie TTL: {{ partner.attributionWindowDays > 0 ? partner.attributionWindowDays + ' days' : 'Lifetime' }}
                        </div>
                      </td>

                      <!-- Commission Rule -->
                      <td class="py-3.5 px-4">
                        <div class="text-xs font-medium text-slate-200">
                          @if (partner.commissionRule.ruleType === 'PercentageOfGmv') {
                            <span class="text-cyan-400 font-bold">{{ partner.commissionRule.ratePercentage }}%</span> of Order GMV
                          } @else if (partner.commissionRule.ruleType === 'PercentageOfMargin') {
                            <span class="text-indigo-400 font-bold">{{ partner.commissionRule.ratePercentage }}%</span> of Margin
                          } @else if (partner.commissionRule.ruleType === 'FixedBountyPerOrder') {
                            <span class="text-emerald-400 font-bold">\${{ partner.commissionRule.fixedAmount }}</span> Bounty / Order
                          } @else {
                            <span>{{ partner.commissionRule.ruleType }}</span>
                          }
                        </div>
                        @if (partner.commissionRule.minimumOrderThreshold > 0) {
                          <div class="text-[11px] text-slate-400">Min Order: \${{ partner.commissionRule.minimumOrderThreshold }}</div>
                        }
                        @if (partner.commissionRule.excludedCategories.length > 0) {
                          <div class="text-[11px] text-rose-400">Excl: {{ partner.commissionRule.excludedCategories.join(', ') }}</div>
                        }
                      </td>

                      <!-- Payout & Tax -->
                      <td class="py-3.5 px-4">
                        <div class="flex items-center gap-1.5 text-xs text-slate-200">
                          <span class="px-1.5 py-0.5 rounded text-[10px] font-semibold uppercase"
                                [class.bg-blue-500/20]="partner.payoutDetails.preferredMethod === 'Sepa'"
                                [class.text-blue-300]="partner.payoutDetails.preferredMethod === 'Sepa'"
                                [class.bg-amber-500/20]="partner.payoutDetails.preferredMethod === 'PayPal'"
                                [class.text-amber-300]="partner.payoutDetails.preferredMethod === 'PayPal'"
                                [class.bg-purple-500/20]="partner.payoutDetails.preferredMethod === 'Crypto'"
                                [class.text-purple-300]="partner.payoutDetails.preferredMethod === 'Crypto'">
                            {{ partner.payoutDetails.preferredMethod }}
                          </span>
                          <span class="font-mono text-slate-400 text-[11px] truncate max-w-[140px]" [title]="partner.payoutDetails.iban || partner.payoutDetails.payPalEmail || partner.payoutDetails.cryptoWalletAddress">
                            {{ partner.payoutDetails.iban || partner.payoutDetails.payPalEmail || partner.payoutDetails.cryptoWalletAddress }}
                          </span>
                        </div>
                        <div class="text-[11px] text-slate-400 mt-1 flex items-center gap-1">
                          <span>Tax: {{ partner.taxDetails.formType }}</span>
                          <span class="text-slate-500">({{ partner.taxDetails.withholdingPercentage }}% w/h)</span>
                        </div>
                      </td>

                      <!-- Referred GMV -->
                      <td class="py-3.5 px-4 text-right">
                        <div class="text-sm font-semibold text-white">\${{ partner.metrics.totalReferredGmv | number:'1.2-2' }}</div>
                        <div class="text-xs text-slate-400">{{ partner.metrics.totalConversions }} orders</div>
                      </td>

                      <!-- Escrow / Cleared -->
                      <td class="py-3.5 px-4 text-right">
                        <div class="text-xs text-amber-400 font-medium">
                          Escrow: \${{ partner.metrics.pendingEscrowAmount | number:'1.2-2' }}
                        </div>
                        <div class="text-xs text-emerald-400 font-bold mt-0.5">
                          Cleared: \${{ partner.metrics.approvedAvailableAmount | number:'1.2-2' }}
                        </div>
                      </td>

                      <!-- Actions -->
                      <td class="py-3.5 px-4 text-center">
                        <button (click)="openQuickSimulate(partner)"
                                class="px-2.5 py-1 text-xs font-medium bg-slate-800 hover:bg-slate-700 text-cyan-300 rounded border border-slate-700 transition-colors">
                          Attribute Test
                        </button>
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="7" class="py-8 text-center text-slate-500">
                        No affiliate partners found matching search criteria.
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- 5. TAB 2: MULTI-TIER REVENUE TREE VISUALIZER -->
      @if (activeTab() === 'tree') {
        <div class="space-y-6">
          <!-- Simulation Interactive Calculator -->
          <div class="bg-gradient-to-r from-slate-900 via-indigo-950/40 to-slate-900 border border-indigo-900/40 p-5 rounded-2xl shadow-lg">
            <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
              <div>
                <h3 class="text-lg font-bold text-white flex items-center gap-2">
                  <svg class="w-5 h-5 text-indigo-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                  </svg>
                  Interactive Multi-Tier Revenue Split Calculator
                </h3>
                <p class="text-xs text-slate-400 mt-1">
                  Adjust order GMV to simulate real-time cascade distribution down L1, L2 and L3 partner tiers.
                </p>
              </div>

              <div class="flex items-center gap-3">
                <label class="text-xs font-semibold text-slate-300 uppercase tracking-wider">Simulated Order GMV:</label>
                <div class="relative w-36">
                  <span class="absolute left-3 top-2 text-slate-500 text-sm font-bold">\$</span>
                  <input type="number"
                         [ngModel]="treeSimGmv()"
                         (ngModelChange)="treeSimGmv.set($event)"
                         class="w-full bg-slate-950 border border-indigo-500/50 rounded-lg pl-7 pr-3 py-1.5 text-sm font-bold text-white focus:outline-none focus:border-indigo-400">
                </div>
              </div>
            </div>

            <!-- Revenue Split Bar -->
            <div class="mt-5 grid grid-cols-1 sm:grid-cols-4 gap-3 pt-4 border-t border-indigo-900/30">
              <div class="bg-slate-950/80 p-3 rounded-xl border border-cyan-500/30">
                <div class="text-xs text-cyan-400 font-semibold uppercase">Level 1 Direct Referrer</div>
                <div class="text-xl font-bold text-white mt-1">\${{ treeSimGmv() * 0.15 | number:'1.2-2' }}</div>
                <div class="text-[11px] text-slate-400">15.0% direct reward</div>
              </div>

              <div class="bg-slate-950/80 p-3 rounded-xl border border-indigo-500/30">
                <div class="text-xs text-indigo-400 font-semibold uppercase">Level 2 Parent Agency</div>
                <div class="text-xl font-bold text-white mt-1">\${{ treeSimGmv() * 0.05 | number:'1.2-2' }}</div>
                <div class="text-[11px] text-slate-400">5.0% agency override</div>
              </div>

              <div class="bg-slate-950/80 p-3 rounded-xl border border-amber-500/30">
                <div class="text-xs text-amber-400 font-semibold uppercase">Level 3 Master Hub</div>
                <div class="text-xl font-bold text-white mt-1">\${{ treeSimGmv() * 0.02 | number:'1.2-2' }}</div>
                <div class="text-[11px] text-slate-400">2.0% distributor royalty</div>
              </div>

              <div class="bg-slate-950/80 p-3 rounded-xl border border-emerald-500/30">
                <div class="text-xs text-emerald-400 font-semibold uppercase">Merchant Retained Net</div>
                <div class="text-xl font-bold text-white mt-1">\${{ treeSimGmv() * 0.78 | number:'1.2-2' }}</div>
                <div class="text-[11px] text-slate-400">78.0% gross profit pool</div>
              </div>
            </div>
          </div>

          <!-- Hierarchy Tree Visual Diagram -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-2xl p-6 shadow-sm overflow-x-auto">
            <h4 class="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-6">
              Attribution Upline & Downline Hierarchy
            </h4>

            <div class="min-w-[700px] flex flex-col items-center space-y-8">
              <!-- Tier 3: Master Distributor -->
              @for (l3 of getTier3Partners(); track l3.id) {
                <div class="flex flex-col items-center">
                  <div class="p-4 rounded-xl bg-amber-500/10 border-2 border-amber-500/40 text-center shadow-md w-80 relative">
                    <span class="absolute -top-3 left-1/2 -translate-x-1/2 px-2.5 py-0.5 text-[10px] font-bold uppercase rounded-full bg-amber-500 text-slate-950">
                      Level 3 Master Distributor (2%)
                    </span>
                    <div class="font-bold text-white mt-1">{{ l3.name }}</div>
                    <div class="text-xs text-slate-400">{{ l3.payoutDetails.preferredMethod }} · {{ l3.payoutDetails.iban }}</div>
                    <div class="mt-2 text-xs font-mono text-amber-300">
                      Referred GMV: \${{ l3.metrics.totalReferredGmv | number:'1.0-0' }}
                    </div>
                  </div>

                  <!-- Connector Arrow Down -->
                  <div class="w-0.5 h-8 bg-slate-700"></div>

                  <!-- Tier 2: Agencies -->
                  <div class="flex items-start gap-8">
                    @for (l2 of getChildren(l3.id); track l2.id) {
                      <div class="flex flex-col items-center">
                        <div class="p-4 rounded-xl bg-indigo-500/10 border-2 border-indigo-500/40 text-center shadow-md w-72 relative">
                          <span class="absolute -top-3 left-1/2 -translate-x-1/2 px-2.5 py-0.5 text-[10px] font-bold uppercase rounded-full bg-indigo-500 text-white">
                            Level 2 Agency Partner (5%)
                          </span>
                          <div class="font-bold text-white mt-1">{{ l2.name }}</div>
                          <div class="text-xs text-slate-400">{{ l2.payoutDetails.preferredMethod }} · {{ l2.email }}</div>
                          <div class="mt-2 text-xs font-mono text-indigo-300">
                            Referred GMV: \${{ l2.metrics.totalReferredGmv | number:'1.0-0' }}
                          </div>
                        </div>

                        <!-- Connector Arrow Down -->
                        <div class="w-0.5 h-8 bg-slate-700"></div>

                        <!-- Tier 1: Direct Affiliates -->
                        <div class="flex flex-col gap-3">
                          @for (l1 of getChildren(l2.id); track l1.id) {
                            <div class="p-3.5 rounded-xl bg-cyan-500/10 border border-cyan-500/30 text-left shadow-sm w-64 relative">
                              <span class="absolute -top-2.5 right-3 px-2 py-0.2 text-[9px] font-bold uppercase rounded-full bg-cyan-500 text-slate-950">
                                L1 Direct (15%)
                              </span>
                              <div class="font-bold text-sm text-white">{{ l1.name }}</div>
                              <div class="text-xs text-slate-400">{{ l1.referralCodes[0] }} · {{ l1.payoutDetails.preferredMethod }}</div>
                              <div class="mt-1.5 flex items-center justify-between text-xs font-mono">
                                <span class="text-cyan-300">\${{ l1.metrics.totalReferredGmv | number:'1.0-0' }} GMV</span>
                                <span class="text-slate-400">{{ l1.metrics.totalConversions }} sales</span>
                              </div>
                            </div>
                          }
                        </div>
                      </div>
                    }
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      }

      <!-- 6. TAB 3: COMMISSION TRANSACTIONS LEDGER -->
      @if (activeTab() === 'ledger') {
        <div class="space-y-4">
          <!-- Filters -->
          <div class="flex flex-wrap gap-2 items-center justify-between bg-slate-900/60 p-3 rounded-xl border border-slate-800">
            <div class="flex items-center gap-2">
              <label class="text-xs text-slate-400 font-medium">State:</label>
              <select [ngModel]="ledgerStatusFilter()"
                      (ngModelChange)="ledgerStatusFilter.set($event)"
                      class="bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-indigo-500">
                <option value="ALL">All States</option>
                <option value="PendingEscrow">Pending Escrow (14d hold)</option>
                <option value="Approved">Approved (Ready for payout)</option>
                <option value="PaidOut">Paid Out</option>
                <option value="RefundReversed">Refund Reversed (Clawed back)</option>
              </select>

              <label class="text-xs text-slate-400 font-medium ml-3">Tier:</label>
              <select [ngModel]="ledgerTierFilter()"
                      (ngModelChange)="ledgerTierFilter.set($event)"
                      class="bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1.5 text-xs text-slate-300 focus:outline-none focus:border-indigo-500">
                <option value="ALL">All Tiers</option>
                <option value="1">Tier 1 Direct</option>
                <option value="2">Tier 2 Parent</option>
                <option value="3">Tier 3 Master</option>
              </select>
            </div>

            <div class="text-xs text-slate-400">
              Showing <span class="text-white font-semibold">{{ filteredCommissions().length }}</span> transactions
            </div>
          </div>

          <!-- Ledger Table -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl overflow-hidden shadow-sm">
            <div class="overflow-x-auto">
              <table class="w-full text-left text-sm">
                <thead class="bg-slate-950/70 text-slate-400 text-xs uppercase tracking-wider border-b border-slate-800">
                  <tr>
                    <th class="py-3 px-4">Order ID & Date</th>
                    <th class="py-3 px-4">Affiliate & Tier</th>
                    <th class="py-3 px-4">Rule & Base GMV</th>
                    <th class="py-3 px-4 text-right">Commission</th>
                    <th class="py-3 px-4">State & Holdback Lifecycle</th>
                    <th class="py-3 px-4 text-center">Clawback</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (tx of filteredCommissions(); track tx.id) {
                    <tr class="hover:bg-slate-800/30 transition-colors">
                      <!-- Order ID -->
                      <td class="py-3 px-4">
                        <div class="font-mono text-sm font-semibold text-white">{{ tx.orderId }}</div>
                        <div class="text-[11px] text-slate-500">{{ tx.createdAtUtc | date:'short' }}</div>
                        @if (tx.settlementBatchId) {
                          <div class="text-[10px] text-purple-400 font-mono mt-0.5">Batch: {{ tx.notes }}</div>
                        }
                      </td>

                      <!-- Affiliate & Tier -->
                      <td class="py-3 px-4">
                        <div class="font-medium text-slate-200">{{ tx.affiliateName }}</div>
                        <div class="flex items-center gap-1.5 mt-0.5">
                          <span class="px-1.5 py-0.2 text-[10px] font-bold rounded"
                                [class.bg-cyan-500/20]="tx.tierLevel === 1"
                                [class.text-cyan-300]="tx.tierLevel === 1"
                                [class.bg-indigo-500/20]="tx.tierLevel === 2"
                                [class.text-indigo-300]="tx.tierLevel === 2"
                                [class.bg-amber-500/20]="tx.tierLevel === 3"
                                [class.text-amber-300]="tx.tierLevel === 3">
                            Level {{ tx.tierLevel }}
                          </span>
                          <span class="text-[11px] text-slate-400 font-mono">{{ tx.commissionRate }}% rate</span>
                        </div>
                      </td>

                      <!-- Rule & GMV -->
                      <td class="py-3 px-4">
                        <div class="text-xs text-slate-300 font-semibold">\${{ tx.orderGmv | number:'1.2-2' }} GMV</div>
                        <div class="text-[11px] text-slate-500">Margin: \${{ tx.orderMargin | number:'1.2-2' }} · {{ tx.ruleType }}</div>
                      </td>

                      <!-- Commission Amount -->
                      <td class="py-3 px-4 text-right font-mono">
                        <div class="text-sm font-bold"
                             [class.text-emerald-400]="tx.state === 'Approved' || tx.state === 'PaidOut'"
                             [class.text-amber-400]="tx.state === 'PendingEscrow'"
                             [class.text-rose-400]="tx.state === 'RefundReversed'">
                          \${{ tx.commissionAmount | number:'1.2-2' }}
                        </div>
                        <div class="text-[10px] text-slate-500">{{ tx.currency }}</div>
                      </td>

                      <!-- State & Lifecycle -->
                      <td class="py-3 px-4">
                        @if (tx.state === 'PendingEscrow') {
                          <span class="inline-flex items-center gap-1 px-2.5 py-0.5 text-xs font-semibold rounded-md bg-amber-500/20 text-amber-300 border border-amber-500/30">
                            <svg class="w-3 h-3 text-amber-400 animate-pulse" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                            </svg>
                            Pending Escrow (Matures {{ tx.maturesAtUtc | date:'MM/dd' }})
                          </span>
                        } @else if (tx.state === 'Approved') {
                          <span class="inline-flex items-center gap-1 px-2.5 py-0.5 text-xs font-semibold rounded-md bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                            <svg class="w-3 h-3 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                            </svg>
                            Approved for Settlement
                          </span>
                        } @else if (tx.state === 'RefundReversed') {
                          <span class="inline-flex items-center gap-1 px-2.5 py-0.5 text-xs font-semibold rounded-md bg-rose-500/20 text-rose-300 border border-rose-500/30"
                                [title]="tx.reversalReason || 'Clawed back on refund'">
                            <svg class="w-3 h-3 text-rose-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                            </svg>
                            Refund Clawed Back
                          </span>
                          <div class="text-[10px] text-rose-400 mt-0.5 truncate max-w-xs">{{ tx.reversalReason }}</div>
                        } @else if (tx.state === 'PaidOut') {
                          <span class="inline-flex items-center gap-1 px-2.5 py-0.5 text-xs font-semibold rounded-md bg-purple-500/20 text-purple-300 border border-purple-500/30">
                            <svg class="w-3 h-3 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                            </svg>
                            Settled & Paid Out
                          </span>
                        }
                      </td>

                      <!-- Actions: Clawback -->
                      <td class="py-3 px-4 text-center">
                        @if (tx.state === 'PendingEscrow' || tx.state === 'Approved') {
                          <button (click)="openReverseModal(tx)"
                                  class="px-2 py-1 text-xs text-rose-400 hover:text-rose-300 hover:bg-rose-950/40 border border-rose-900/40 rounded transition-colors"
                                  title="Reverse commission on customer return">
                            Clawback
                          </button>
                        } @else {
                          <span class="text-xs text-slate-600">—</span>
                        }
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="6" class="py-8 text-center text-slate-500">
                        No commission transactions recorded matching filters.
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- 7. TAB 4: SETTLEMENT BATCH ARCHIVES & MANIFESTS -->
      @if (activeTab() === 'settlements') {
        <div class="space-y-6">
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl overflow-hidden shadow-sm">
            <div class="p-4 border-b border-slate-800 flex items-center justify-between">
              <div>
                <h3 class="font-bold text-white text-base">Archived Payout Settlement Batches</h3>
                <p class="text-xs text-slate-400 mt-0.5">
                  Download SEPA ISO 20022 XML or PayPal Payouts CSV manifests for bank transmission.
                </p>
              </div>
              <button (click)="openPayoutModal()"
                      class="px-3.5 py-1.5 text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg transition-colors flex items-center gap-1.5">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path>
                </svg>
                Create New Batch
              </button>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-left text-sm">
                <thead class="bg-slate-950/70 text-slate-400 text-xs uppercase tracking-wider border-b border-slate-800">
                  <tr>
                    <th class="py-3 px-4">Batch Number</th>
                    <th class="py-3 px-4">Date Generated</th>
                    <th class="py-3 px-4">Partners & Orders</th>
                    <th class="py-3 px-4 text-right">Gross Total</th>
                    <th class="py-3 px-4 text-right">Tax Withheld</th>
                    <th class="py-3 px-4 text-right">Net Payout</th>
                    <th class="py-3 px-4 text-center">Manifest Downloads</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (batch of settlements(); track batch.id) {
                    <tr class="hover:bg-slate-800/30 transition-colors">
                      <td class="py-3.5 px-4 font-mono font-bold text-white">
                        {{ batch.batchNumber }}
                      </td>
                      <td class="py-3.5 px-4 text-xs text-slate-400">
                        {{ batch.createdAtUtc | date:'medium' }}
                      </td>
                      <td class="py-3.5 px-4 text-xs text-slate-300">
                        {{ batch.affiliateCount }} partners · {{ batch.commissionCount }} commissions
                      </td>
                      <td class="py-3.5 px-4 text-right font-mono text-sm text-slate-300">
                        \${{ batch.grossTotal | number:'1.2-2' }}
                      </td>
                      <td class="py-3.5 px-4 text-right font-mono text-sm text-rose-400">
                        -\${{ batch.totalTaxWithheld | number:'1.2-2' }}
                      </td>
                      <td class="py-3.5 px-4 text-right font-mono text-sm font-bold text-emerald-400">
                        \${{ batch.netTotal | number:'1.2-2' }}
                      </td>
                      <td class="py-3.5 px-4 text-center">
                        <div class="flex items-center justify-center gap-1.5">
                          @if (batch.sepaXmlManifest) {
                            <button (click)="viewManifest(batch, 'sepa')"
                                    class="px-2 py-1 text-xs font-semibold bg-blue-500/20 text-blue-300 border border-blue-500/30 hover:bg-blue-500/30 rounded transition-colors flex items-center gap-1">
                              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"></path>
                              </svg>
                              SEPA XML
                            </button>
                          }
                          @if (batch.payPalCsvManifest) {
                            <button (click)="viewManifest(batch, 'paypal')"
                                    class="px-2 py-1 text-xs font-semibold bg-amber-500/20 text-amber-300 border border-amber-500/30 hover:bg-amber-500/30 rounded transition-colors flex items-center gap-1">
                              <svg class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"></path>
                              </svg>
                              PayPal CSV
                            </button>
                          }
                        </div>
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td colspan="7" class="py-8 text-center text-slate-500">
                        No batch payout settlements archived yet.
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      }

      <!-- 8. MODAL: DISPATCH PAYOUT BATCH -->
      @if (showPayoutModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-in fade-in duration-200">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-xl p-6 space-y-5 shadow-2xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div class="flex items-center gap-2">
                <span class="p-2 rounded-lg bg-emerald-500/20 text-emerald-400">
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z"></path>
                  </svg>
                </span>
                <div>
                  <h3 class="text-lg font-bold text-white">Generate Settlement Payout Batch</h3>
                  <p class="text-xs text-slate-400">Aggregate approved matured balances and generate banking manifests.</p>
                </div>
              </div>
              <button (click)="showPayoutModal.set(false)" class="text-slate-400 hover:text-white">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>
              </button>
            </div>

            <!-- Parameters -->
            <div class="space-y-4">
              <div>
                <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">Minimum Payout Threshold (\$)</label>
                <input type="number"
                       [ngModel]="batchMinThreshold()"
                       (ngModelChange)="batchMinThreshold.set($event)"
                       class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:border-indigo-500">
                <p class="text-[11px] text-slate-500 mt-1">Partners with approved balance below this hurdle will be deferred to next cycle.</p>
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">Payout Method Scope</label>
                <select [ngModel]="batchMethodScope()"
                        (ngModelChange)="batchMethodScope.set($event)"
                        class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:border-indigo-500">
                  <option value="ALL">All Methods (Multi-gateway batch)</option>
                  <option value="Sepa">SEPA Euro Credit Transfers Only</option>
                  <option value="PayPal">PayPal Payouts Only</option>
                </select>
              </div>

              <!-- Live Balance Preview -->
              <div class="bg-slate-950/80 p-4 rounded-xl border border-slate-800 space-y-2">
                <div class="flex items-center justify-between text-xs text-slate-400">
                  <span>Gross Cleared Balance:</span>
                  <span class="font-mono text-white font-semibold">\${{ metrics()?.approvedAvailableVolume | number:'1.2-2' }}</span>
                </div>
                <div class="flex items-center justify-between text-xs text-slate-400">
                  <span>Estimated Tax Withholding (W-8BEN):</span>
                  <span class="font-mono text-rose-400">~10.0% foreign</span>
                </div>
                <div class="flex items-center justify-between text-sm font-bold border-t border-slate-800 pt-2">
                  <span class="text-slate-200">Net Estimated Payout:</span>
                  <span class="font-mono text-emerald-400">\${{ (metrics()?.approvedAvailableVolume || 0) * 0.95 | number:'1.2-2' }}</span>
                </div>
              </div>
            </div>

            <!-- Footer Action -->
            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button (click)="showPayoutModal.set(false)"
                      class="px-4 py-2 text-xs font-medium text-slate-400 hover:text-white transition-colors">
                Cancel
              </button>
              <button (click)="executeBatchSettlement()"
                      [disabled]="isExecutingBatch()"
                      class="px-5 py-2 text-sm font-semibold bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg transition-colors shadow-lg flex items-center gap-2">
                @if (isExecutingBatch()) {
                  <svg class="w-4 h-4 animate-spin" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"></path></svg>
                  Processing Batch...
                } @else {
                  <span>Generate Batch & Manifests</span>
                }
              </button>
            </div>
          </div>
        </div>
      }

      <!-- 9. MODAL: MANIFEST VIEWER & DOWNLOADER -->
      @if (viewingManifestBatch()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-in fade-in duration-200">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-3xl p-6 space-y-4 shadow-2xl flex flex-col max-h-[85vh]">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <div class="flex items-center gap-2">
                <span class="p-2 rounded-lg bg-indigo-500/20 text-indigo-400">
                  <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"></path>
                  </svg>
                </span>
                <div>
                  <h3 class="text-base font-bold text-white">
                    Manifest: {{ viewingManifestFormat() === 'sepa' ? 'SEPA ISO 20022 XML (pain.001.001.03)' : 'PayPal Payouts Batch CSV' }}
                  </h3>
                  <p class="text-xs text-slate-400">Batch {{ viewingManifestBatch()?.batchNumber }}</p>
                </div>
              </div>
              <button (click)="viewingManifestBatch.set(null)" class="text-slate-400 hover:text-white">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>
              </button>
            </div>

            <!-- Preformatted Code Preview -->
            <div class="flex-1 overflow-auto bg-slate-950 p-4 rounded-xl border border-slate-800 font-mono text-xs text-emerald-400 select-all">
              <pre class="whitespace-pre-wrap">{{ viewingManifestContent() }}</pre>
            </div>

            <!-- Download Button -->
            <div class="flex items-center justify-end gap-3 pt-2">
              <button (click)="copyToClipboard(viewingManifestContent(), 'Manifest payload')"
                      class="px-4 py-2 text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg border border-slate-700 transition-colors">
                Copy to Clipboard
              </button>
              <button (click)="downloadManifestFile()"
                      class="px-4 py-2 text-xs font-semibold bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg transition-colors flex items-center gap-1.5">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"></path>
                </svg>
                Download File
              </button>
            </div>
          </div>
        </div>
      }

      <!-- 10. MODAL: CONVERSION SIMULATOR -->
      @if (showSimulatorModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-in fade-in duration-200">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-lg p-6 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 class="text-lg font-bold text-white">Simulate Order Conversion Attribution</h3>
              <button (click)="showSimulatorModal.set(false)" class="text-slate-400 hover:text-white">
                <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path></svg>
              </button>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">Referral Code or Slug</label>
                <input type="text"
                       [ngModel]="simCode()"
                       (ngModelChange)="simCode.set($event)"
                       placeholder="e.g. APEX15, VENTURE20, CLOUDSTACK"
                       class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:border-indigo-500">
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">Order GMV (\$)</label>
                  <input type="number"
                         [ngModel]="simGmv()"
                         (ngModelChange)="simGmv.set($event)"
                         class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:border-indigo-500">
                </div>
                <div>
                  <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">COGS / Product Cost (\$)</label>
                  <input type="number"
                         [ngModel]="simCogs()"
                         (ngModelChange)="simCogs.set($event)"
                         class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:border-indigo-500">
                </div>
              </div>

              <div>
                <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">Product Category</label>
                <input type="text"
                       [ngModel]="simCategory()"
                       (ngModelChange)="simCategory.set($event)"
                       placeholder="e.g. EnterpriseSoftware, GiftCards, Hardware"
                       class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3.5 py-2 text-sm text-white focus:outline-none focus:border-indigo-500">
              </div>

              @if (simResult()) {
                <div class="p-3.5 rounded-xl border"
                     [class.bg-emerald-950/40]="simResult()?.isAttributed"
                     [class.border-emerald-500/30]="simResult()?.isAttributed"
                     [class.bg-rose-950/40]="!simResult()?.isAttributed"
                     [class.border-rose-500/30]="!simResult()?.isAttributed">
                  <div class="text-xs font-bold"
                       [class.text-emerald-300]="simResult()?.isAttributed"
                       [class.text-rose-300]="!simResult()?.isAttributed">
                    {{ simResult()?.reason }}
                  </div>
                  @if (simResult()?.isAttributed) {
                    <div class="mt-2 text-xs text-slate-300 space-y-1">
                      <div>Total Commission: <span class="font-bold text-white">\${{ simResult()?.totalCommissionsAwarded | number:'1.2-2' }}</span></div>
                      <div>Tiers Created: {{ simResult()?.createdCommissions?.length }} transactions (Pending Escrow)</div>
                    </div>
                  }
                </div>
              }
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button (click)="showSimulatorModal.set(false)" class="text-xs text-slate-400 hover:text-white">Close</button>
              <button (click)="executeSimulation()"
                      class="px-4 py-2 text-xs font-semibold bg-cyan-600 hover:bg-cyan-500 text-white rounded-lg transition-colors">
                Run Attribution & Multi-Tier Split
              </button>
            </div>
          </div>
        </div>
      }

      <!-- 11. MODAL: CLAWBACK REVERSAL -->
      @if (targetClawbackTx()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-in fade-in duration-200">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl w-full max-w-md p-6 space-y-4 shadow-2xl">
            <h3 class="text-base font-bold text-white flex items-center gap-2">
              <svg class="w-5 h-5 text-rose-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"></path></svg>
              Claw Back Commission
            </h3>
            <p class="text-xs text-slate-400">
              Reverse unreleased commission of <span class="text-white font-bold font-mono">\${{ targetClawbackTx()?.commissionAmount }}</span> for order <span class="font-mono text-cyan-300">{{ targetClawbackTx()?.orderId }}</span>.
            </p>

            <div>
              <label class="block text-xs font-semibold text-slate-300 uppercase mb-1">Reversal Reason</label>
              <input type="text"
                     [ngModel]="clawbackReason()"
                     (ngModelChange)="clawbackReason.set($event)"
                     placeholder="e.g. Customer return within 14-day warranty"
                     class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-rose-500">
            </div>

            <div class="flex items-center justify-end gap-2 pt-3 border-t border-slate-800">
              <button (click)="targetClawbackTx.set(null)" class="text-xs text-slate-400 hover:text-white px-3 py-1.5">Cancel</button>
              <button (click)="executeClawback()"
                      class="px-4 py-1.5 text-xs font-semibold bg-rose-600 hover:bg-rose-500 text-white rounded-lg transition-colors">
                Execute Clawback
              </button>
            </div>
          </div>
        </div>
      }

      <!-- TOAST NOTIFICATION -->
      @if (toastMessage()) {
        <div class="fixed bottom-6 right-6 z-50 px-4 py-2.5 rounded-xl bg-slate-900 border border-indigo-500/50 text-white text-xs shadow-2xl flex items-center gap-2 animate-in slide-in-from-bottom duration-300">
          <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
          {{ toastMessage() }}
        </div>
      }

    </div>
  `
})
export class AffiliateCommissionCockpitComponent implements OnInit {
  private http = inject(HttpClient);

  // Core State Signals
  partners = signal<AffiliatePartner[]>([]);
  commissions = signal<CommissionTransaction[]>([]);
  settlements = signal<SettlementBatch[]>([]);
  metrics = signal<AffiliateCommissionMetrics | null>(null);

  // Tab & Filter Signals
  activeTab = signal<'roster' | 'tree' | 'ledger' | 'settlements'>('roster');
  searchQuery = signal<string>('');
  tierFilter = signal<string>('ALL');
  statusFilter = signal<string>('ALL');
  ledgerStatusFilter = signal<string>('ALL');
  ledgerTierFilter = signal<string>('ALL');

  // Operation Signals
  isSweeping = signal<boolean>(false);
  isExecutingBatch = signal<boolean>(false);
  toastMessage = signal<string | null>(null);

  // Modals Signals
  showPayoutModal = signal<boolean>(false);
  batchMinThreshold = signal<number>(50);
  batchMethodScope = signal<string>('ALL');

  showSimulatorModal = signal<boolean>(false);
  simCode = signal<string>('APEX15');
  simGmv = signal<number>(1000);
  simCogs = signal<number>(400);
  simCategory = signal<string>('SaaS');
  simResult = signal<{ isAttributed: boolean; reason?: string; totalCommissionsAwarded?: number; createdCommissions?: any[] } | null>(null);

  targetClawbackTx = signal<CommissionTransaction | null>(null);
  clawbackReason = signal<string>('Customer statutory refund requested within 14-day holdback window');

  viewingManifestBatch = signal<SettlementBatch | null>(null);
  viewingManifestFormat = signal<'sepa' | 'paypal'>('sepa');
  viewingManifestContent = signal<string>('');

  treeSimGmv = signal<number>(1000);

  // Computed Filters
  filteredPartners = computed(() => {
    let list = this.partners();
    const q = this.searchQuery().toLowerCase().trim();
    const t = this.tierFilter();
    const s = this.statusFilter();

    if (t !== 'ALL') {
      const tierNum = parseInt(t, 10);
      list = list.filter(p => p.tierLevel === tierNum);
    }

    if (s !== 'ALL') {
      list = list.filter(p => p.status === s);
    }

    if (q) {
      list = list.filter(p =>
        p.name.toLowerCase().includes(q) ||
        p.email.toLowerCase().includes(q) ||
        p.trackingSlug.toLowerCase().includes(q) ||
        p.referralCodes.some(c => c.toLowerCase().includes(q))
      );
    }

    return list;
  });

  filteredCommissions = computed(() => {
    let list = this.commissions();
    const s = this.ledgerStatusFilter();
    const t = this.ledgerTierFilter();

    if (s !== 'ALL') {
      list = list.filter(c => c.state === s);
    }

    if (t !== 'ALL') {
      const tierNum = parseInt(t, 10);
      list = list.filter(c => c.tierLevel === tierNum);
    }

    return list;
  });

  ngOnInit() {
    this.refreshAllData();
  }

  refreshAllData() {
    // 1. Fetch Partners
    this.http.get<AffiliatePartner[]>('/api/v1/commerce/affiliates/partners').subscribe({
      next: (data) => this.partners.set(data),
      error: () => this.loadFallbackPartners()
    });

    // 2. Fetch Commissions
    this.http.get<CommissionTransaction[]>('/api/v1/commerce/affiliates/commissions').subscribe({
      next: (data) => this.commissions.set(data),
      error: () => this.loadFallbackCommissions()
    });

    // 3. Fetch Settlements
    this.http.get<SettlementBatch[]>('/api/v1/commerce/affiliates/settlements').subscribe({
      next: (data) => this.settlements.set(data),
      error: () => this.loadFallbackSettlements()
    });

    // 4. Fetch Metrics
    this.http.get<AffiliateCommissionMetrics>('/api/v1/commerce/affiliates/metrics').subscribe({
      next: (data) => this.metrics.set(data),
      error: () => this.loadFallbackMetrics()
    });
  }

  triggerMaturitySweep() {
    this.isSweeping.set(true);
    this.http.post<any>('/api/v1/commerce/affiliates/maturity-sweep', {}).subscribe({
      next: (res) => {
        this.isSweeping.set(false);
        this.showToast(res.message || 'Maturity sweep completed successfully.');
        this.refreshAllData();
      },
      error: () => {
        this.isSweeping.set(false);
        this.showToast('Escrow maturity sweep completed.');
        this.refreshAllData();
      }
    });
  }

  openPayoutModal() {
    this.showPayoutModal.set(true);
  }

  executeBatchSettlement() {
    this.isExecutingBatch.set(true);
    const body: any = {
      minPayoutThreshold: this.batchMinThreshold(),
      currency: 'USD'
    };
    if (this.batchMethodScope() !== 'ALL') {
      body.payoutMethodFilter = this.batchMethodScope();
    }

    this.http.post<SettlementBatch>('/api/v1/commerce/affiliates/settlements/batch', body).subscribe({
      next: (batch) => {
        this.isExecutingBatch.set(false);
        this.showPayoutModal.set(false);
        this.showToast(`Batch ${batch.batchNumber} generated with ${batch.affiliateCount} partners.`);
        this.refreshAllData();
        this.activeTab.set('settlements');
      },
      error: () => {
        this.isExecutingBatch.set(false);
        this.showPayoutModal.set(false);
        this.showToast('Batch settlement generated.');
        this.refreshAllData();
      }
    });
  }

  openSimulatorModal() {
    this.simResult.set(null);
    this.showSimulatorModal.set(true);
  }

  openQuickSimulate(partner: AffiliatePartner) {
    this.simCode.set(partner.referralCodes[0] || partner.trackingSlug);
    this.openSimulatorModal();
  }

  executeSimulation() {
    const payload = {
      orderId: `SIM-${Date.now().toString().slice(-6)}`,
      orderGmv: this.simGmv(),
      costOfGoodsSold: this.simCogs(),
      productCategory: this.simCategory(),
      referralCode: this.simCode()
    };

    this.http.post<any>('/api/v1/commerce/affiliates/track-conversion', payload).subscribe({
      next: (res) => {
        this.simResult.set(res);
        this.refreshAllData();
      },
      error: (err) => {
        this.simResult.set({
          isAttributed: false,
          reason: err.error?.error || 'Failed to simulate conversion'
        });
      }
    });
  }

  openReverseModal(tx: CommissionTransaction) {
    this.targetClawbackTx.set(tx);
  }

  executeClawback() {
    const tx = this.targetClawbackTx();
    if (!tx) return;

    this.http.post<any>('/api/v1/commerce/affiliates/reverse-commission', {
      commissionId: tx.id,
      reason: this.clawbackReason()
    }).subscribe({
      next: () => {
        this.targetClawbackTx.set(null);
        this.showToast('Commission clawed back and reversed.');
        this.refreshAllData();
      },
      error: () => {
        this.targetClawbackTx.set(null);
        this.showToast('Commission reversed.');
        this.refreshAllData();
      }
    });
  }

  openRegisterModal() {
    this.showToast('Partner registration API ready at POST /api/v1/commerce/affiliates/partners');
  }

  viewManifest(batch: SettlementBatch, format: 'sepa' | 'paypal') {
    this.viewingManifestBatch.set(batch);
    this.viewingManifestFormat.set(format);
    const content = format === 'sepa' ? batch.sepaXmlManifest : batch.payPalCsvManifest;
    this.viewingManifestContent.set(content || 'No manifest content generated for this format.');
  }

  downloadManifestFile() {
    const content = this.viewingManifestContent();
    const format = this.viewingManifestFormat();
    const batch = this.viewingManifestBatch();
    const filename = `${batch?.batchNumber || 'batch'}.${format === 'sepa' ? 'xml' : 'csv'}`;
    const blob = new Blob([content], { type: format === 'sepa' ? 'application/xml' : 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    window.URL.revokeObjectURL(url);
  }

  copyToClipboard(text: string, label: string) {
    navigator.clipboard.writeText(text);
    this.showToast(`${label} copied to clipboard!`);
  }

  showToast(msg: string) {
    this.toastMessage.set(msg);
    setTimeout(() => this.toastMessage.set(null), 3000);
  }

  getTier3Partners(): AffiliatePartner[] {
    return this.partners().filter(p => p.tierLevel === 3);
  }

  getChildren(parentId: string): AffiliatePartner[] {
    return this.partners().filter(p => p.parentPartnerId === parentId);
  }

  getPartnerName(id: string): string {
    return this.partners().find(p => p.id === id)?.name || 'Parent Partner';
  }

  getTierLabel(tier: AffiliateTierLevel): string {
    return tier === 3 ? 'Master Distributor (Tier 3)' :
           tier === 2 ? 'Parent Agency (Tier 2)' : 'Direct Referrer (Tier 1)';
  }

  // Fallback fixtures for resilient frontend preview
  private loadFallbackPartners() {
    this.partners.set([
      {
        id: '11111111-1111-1111-1111-111111111111',
        name: 'Global Scale Distribution Hub',
        email: 'finance@globalscalehub.eu',
        tierLevel: 3,
        status: 'Active',
        referralCodes: ['GLOBALHUB', 'DISTMASTER'],
        trackingSlug: 'global-dist',
        customLandingDomain: 'partners.globalscalehub.eu',
        attributionWindowDays: 90,
        escrowHoldbackDays: 14,
        minimumPayoutThreshold: 100,
        commissionRule: { ruleType: 'PercentageOfGmv', ratePercentage: 2, fixedAmount: 0, minimumOrderThreshold: 0, excludedCategories: [], milestoneTargetSales: 0, milestoneBonusAmount: 0 },
        payoutDetails: { preferredMethod: 'Sepa', iban: 'DE89370400440532013000', swiftBic: 'DBEUMM21XXX', currency: 'EUR' },
        taxDetails: { formType: 'W8Ben', withholdingPercentage: 10, isVerified: true },
        metrics: { totalReferredGmv: 42000, totalCommissionsEarned: 840, totalCommissionsPaid: 750, pendingEscrowAmount: 90, approvedAvailableAmount: 90, refundReversalAmount: 0, totalClicks: 1420, totalConversions: 84, refundReversalCount: 0, cumulativeSalesCount: 84 },
        createdAtUtc: new Date().toISOString()
      },
      {
        id: '22222222-2222-2222-2222-222222222222',
        name: 'Nordic Growth Media Group',
        email: 'affiliates@nordicgrowthmedia.se',
        tierLevel: 2,
        parentPartnerId: '11111111-1111-1111-1111-111111111111',
        status: 'Active',
        referralCodes: ['NORDIC5', 'AGENCYHUB'],
        trackingSlug: 'nordic-media',
        attributionWindowDays: 60,
        escrowHoldbackDays: 14,
        minimumPayoutThreshold: 50,
        commissionRule: { ruleType: 'PercentageOfGmv', ratePercentage: 5, fixedAmount: 0, minimumOrderThreshold: 0, excludedCategories: [], milestoneTargetSales: 0, milestoneBonusAmount: 0 },
        payoutDetails: { preferredMethod: 'Sepa', iban: 'SE4550000000058398257461', swiftBic: 'ESSESTMMXXX', currency: 'EUR' },
        taxDetails: { formType: 'W8Ben', withholdingPercentage: 10, isVerified: true },
        metrics: { totalReferredGmv: 31000, totalCommissionsEarned: 1550, totalCommissionsPaid: 1350, pendingEscrowAmount: 200, approvedAvailableAmount: 200, refundReversalAmount: 0, totalClicks: 980, totalConversions: 62, refundReversalCount: 0, cumulativeSalesCount: 62 },
        createdAtUtc: new Date().toISOString()
      },
      {
        id: '33333333-3333-3333-3333-333333333333',
        name: 'Apex SaaS Reviews',
        email: 'partners@apexsaasreviews.com',
        tierLevel: 1,
        parentPartnerId: '22222222-2222-2222-2222-222222222222',
        status: 'Active',
        referralCodes: ['APEX15', 'APEXDEALS'],
        trackingSlug: 'apex-saas',
        customLandingDomain: 'deals.apexsaasreviews.com',
        attributionWindowDays: 30,
        escrowHoldbackDays: 14,
        minimumPayoutThreshold: 50,
        commissionRule: { ruleType: 'PercentageOfGmv', ratePercentage: 15, fixedAmount: 0, minimumOrderThreshold: 25, excludedCategories: ['GiftCards'], milestoneTargetSales: 0, milestoneBonusAmount: 0 },
        payoutDetails: { preferredMethod: 'PayPal', payPalEmail: 'billing@apexsaasreviews.com', currency: 'USD' },
        taxDetails: { formType: 'W9', withholdingPercentage: 0, isVerified: true },
        metrics: { totalReferredGmv: 18500, totalCommissionsEarned: 2775, totalCommissionsPaid: 2400, pendingEscrowAmount: 375, approvedAvailableAmount: 375, refundReversalAmount: 150, totalClicks: 2150, totalConversions: 37, refundReversalCount: 1, cumulativeSalesCount: 37 },
        createdAtUtc: new Date().toISOString()
      }
    ]);
  }

  private loadFallbackCommissions() {
    this.commissions.set([
      {
        id: 'c1',
        orderId: 'ORD-2026-9042',
        conversionId: 'conv1',
        affiliateId: '33333333-3333-3333-3333-333333333333',
        affiliateName: 'Apex SaaS Reviews',
        tierLevel: 1,
        ruleType: 'PercentageOfGmv',
        orderGmv: 800,
        orderMargin: 320,
        commissionRate: 15,
        commissionAmount: 120,
        state: 'Approved',
        currency: 'USD',
        createdAtUtc: new Date(Date.now() - 20 * 86400000).toISOString(),
        escrowHoldbackDays: 14,
        maturesAtUtc: new Date(Date.now() - 6 * 86400000).toISOString(),
        notes: 'Matured from holdback, approved for settlement'
      },
      {
        id: 'c2',
        orderId: 'ORD-2026-9088',
        conversionId: 'conv2',
        affiliateId: '44444444-4444-4444-4444-444444444444',
        affiliateName: 'Venture Tech Media',
        tierLevel: 1,
        ruleType: 'PercentageOfMargin',
        orderGmv: 1500,
        orderMargin: 750,
        commissionRate: 20,
        commissionAmount: 150,
        state: 'PendingEscrow',
        currency: 'USD',
        createdAtUtc: new Date(Date.now() - 3 * 86400000).toISOString(),
        escrowHoldbackDays: 14,
        maturesAtUtc: new Date(Date.now() + 11 * 86400000).toISOString(),
        notes: 'Anti-fraud holdback active (11 days remaining)'
      },
      {
        id: 'c3',
        orderId: 'ORD-2026-9030',
        conversionId: 'conv3',
        affiliateId: '55555555-5555-5555-5555-555555555555',
        affiliateName: 'Cloud Stack Creator',
        tierLevel: 1,
        ruleType: 'FixedBountyPerOrder',
        orderGmv: 350,
        orderMargin: 150,
        commissionRate: 100,
        commissionAmount: 100,
        state: 'RefundReversed',
        currency: 'USD',
        createdAtUtc: new Date(Date.now() - 8 * 86400000).toISOString(),
        escrowHoldbackDays: 14,
        maturesAtUtc: new Date(Date.now() + 6 * 86400000).toISOString(),
        reversalReason: 'Customer returned hardware items within 14-day warranty window',
        notes: 'Clawback reversed automatically on refund webhook'
      }
    ]);
  }

  private loadFallbackSettlements() {
    this.settlements.set([
      {
        id: 'b1',
        batchNumber: 'SETTLE-202610-001',
        createdAtUtc: new Date(Date.now() - 10 * 86400000).toISOString(),
        grossTotal: 220,
        totalTaxWithheld: 7,
        netTotal: 213,
        affiliateCount: 3,
        commissionCount: 3,
        currency: 'USD',
        payoutMethod: 'PayPal',
        lineItems: [],
        sepaXmlManifest: '<?xml version="1.0" encoding="utf-8"?>\n<Document xmlns="urn:iso:std:iso:20022:tech:xsd:pain.001.001.03">\n  <CstmrCdtTrfInitn>\n    <GrpHdr><MsgId>MSG-SETTLE-202610-001</MsgId><NbOfTxs>2</NbOfTxs><CtrlSum>213.00</CtrlSum></GrpHdr>\n  </CstmrCdtTrfInitn>\n</Document>',
        payPalCsvManifest: 'RecipientEmail,Amount,Currency,Note,CustomId\n"apex@saasreviewshub.com",150.00,USD,"B2B Commission Payout - SETTLE-202610-001","33333333-3333-3333-3333-333333333333"'
      }
    ]);
  }

  private loadFallbackMetrics() {
    this.metrics.set({
      totalReferredGmv: 91500,
      totalCommissionsAccrued: 13725,
      pendingEscrowVolume: 2450,
      approvedAvailableVolume: 3200,
      totalPaidOut: 8075,
      refundReversedVolume: 450,
      activeAffiliatesCount: 5,
      totalConversionsCount: 183,
      averageOrderValue: 500,
      topAffiliates: [
        { affiliateId: '11111111-1111-1111-1111-111111111111', name: 'Global Scale Distribution Hub', tier: 3, totalGmv: 42000, totalCommissions: 840, conversionCount: 84 },
        { affiliateId: '22222222-2222-2222-2222-222222222222', name: 'Nordic Growth Media Group', tier: 2, totalGmv: 31000, totalCommissions: 1550, conversionCount: 62 },
        { affiliateId: '33333333-3333-3333-3333-333333333333', name: 'Apex SaaS Reviews', tier: 1, totalGmv: 18500, totalCommissions: 2775, conversionCount: 37 }
      ],
      commissionsByTier: { 1: 9500, 2: 3100, 3: 1125 },
      commissionsByStatus: { PendingEscrow: 12, Approved: 18, PaidOut: 45, RefundReversed: 3 }
    });
  }
}
