import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface CurrencySpotRateDto {
  code: string;
  currencyName: string;
  symbol: string;
  spotRateToUsd: number;
  spreadMarginPct: number;
  effectiveRateToUsd: number;
  lastUpdated: string;
  flag: string;
}

export interface TaxJurisdictionRuleDto {
  countryCode: string;
  countryName: string;
  standardRatePct: number;
  reducedRatePct: number;
  isEuMember: boolean;
  isGccMember: boolean;
  hasViesSupport: boolean;
  hasZatcaPhase2: boolean;
  taxName: string;
  flag: string;
}

export interface CommercialInvoiceLineItemDto {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface CommercialInvoiceDto {
  id: string;
  tenantId: string;
  invoiceNumber: string;
  issueDate: string;
  dueDate: string;
  currency: string;
  lineItems: CommercialInvoiceLineItemDto[];
  subtotal: number;
  taxRatePct: number;
  taxAmount: number;
  totalAmount: number;
  remainingBalance: number;
  creditNotesApplied: number;
  status: 'Draft' | 'Issued' | 'Paid' | 'PartiallyRefunded' | 'Reversed';
  customerName: string;
  customerEmail: string;
  customerCountry: string;
  customerVatNumber?: string | null;
  merchantCountry: string;
  zatcaQrBase64?: string | null;
  facturXXmlPayload?: string | null;
}

export interface CreditNoteDto {
  id: string;
  tenantId: string;
  creditNoteNumber: string;
  originalInvoiceId: string;
  originalInvoiceNumber: string;
  issueDate: string;
  reason: string;
  refundAmount: number;
  taxAdjustment: number;
  totalCredit: number;
  status: string;
}

export interface TaxLiabilitySummaryItemDto {
  jurisdiction: string;
  countryName: string;
  currency: string;
  invoicesCount: number;
  grossSubtotal: number;
  totalTaxCollected: number;
  totalCreditsAdjusted: number;
  netTaxLiability: number;
  flag: string;
}

export interface TaxEvaluationResultDto {
  applicableTaxRatePct: number;
  taxAmount: number;
  taxScheme: string;
  isReverseCharge: boolean;
  exemptionReason?: string | null;
  viesValid: boolean;
}

@Component({
  selector: 'app-tax-invoice-ledger-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Top Navigation Bar -->
      <header class="border-b border-slate-800 bg-slate-900/70 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-40">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-slate-200">Tax & Multi-Currency Invoice Ledger</span>
            <span class="text-[11px] px-2.5 py-0.5 rounded-full bg-indigo-500/10 text-indigo-400 border border-indigo-500/30 font-medium">
              Horizon 31 Enterprise Fiscal Engine
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <div class="hidden lg:flex items-center gap-2 text-xs text-slate-400">
            <span class="px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-mono">ZATCA Phase 2</span>
            <span class="px-2 py-0.5 rounded bg-blue-500/10 text-blue-400 border border-blue-500/20 font-mono">EU Art. 196 VIES</span>
            <span class="px-2 py-0.5 rounded bg-purple-500/10 text-purple-400 border border-purple-500/20 font-mono">Factur-X CII</span>
          </div>
          <button
            (click)="openNewInvoiceModal.set(true)"
            class="px-3.5 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-2 shadow-lg shadow-emerald-900/20">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/></svg>
            Issue Commercial Invoice
          </button>
        </div>
      </header>

      <!-- Main Container -->
      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- KPI Cards Grid -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <!-- Card 1: Total Invoiced -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 relative overflow-hidden group hover:border-slate-700 transition">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider flex items-center justify-between">
              <span>Total Invoiced</span>
              <span class="text-emerald-400 text-[10px] font-mono">USD Eq.</span>
            </div>
            <div class="text-2xl font-black text-white mt-2">
              \${{ totalInvoicedUsd().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
            </div>
            <div class="text-xs text-slate-500 mt-1 flex items-center gap-1.5">
              <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
              {{ invoices().length }} commercial invoices in ledger
            </div>
            <div class="absolute -right-3 -bottom-3 w-16 h-16 bg-emerald-500/10 rounded-full blur-xl pointer-events-none"></div>
          </div>

          <!-- Card 2: Outstanding Receivables (AR) -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 relative overflow-hidden group hover:border-slate-700 transition">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider flex items-center justify-between">
              <span>Outstanding AR</span>
              <span class="text-blue-400 text-[10px] font-mono">Uncollected</span>
            </div>
            <div class="text-2xl font-black text-blue-400 mt-2">
              \${{ totalOutstandingUsd().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
            </div>
            <div class="text-xs text-slate-500 mt-1 flex items-center gap-1.5">
              <span class="w-2 h-2 rounded-full bg-blue-400"></span>
              {{ activeUnpaidCount() }} invoices awaiting settlement
            </div>
            <div class="absolute -right-3 -bottom-3 w-16 h-16 bg-blue-500/10 rounded-full blur-xl pointer-events-none"></div>
          </div>

          <!-- Card 3: Fiscal Tax Collected -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 relative overflow-hidden group hover:border-slate-700 transition">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider flex items-center justify-between">
              <span>Net Tax Collected</span>
              <span class="text-amber-400 text-[10px] font-mono">Net Liability</span>
            </div>
            <div class="text-2xl font-black text-amber-400 mt-2">
              \${{ totalTaxCollectedUsd().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
            </div>
            <div class="text-xs text-slate-500 mt-1 flex items-center gap-1.5">
              <span class="w-2 h-2 rounded-full bg-amber-400"></span>
              Aggregated across 7 jurisdictions
            </div>
            <div class="absolute -right-3 -bottom-3 w-16 h-16 bg-amber-500/10 rounded-full blur-xl pointer-events-none"></div>
          </div>

          <!-- Card 4: Credit Notes Emitted -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 relative overflow-hidden group hover:border-slate-700 transition">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider flex items-center justify-between">
              <span>Credit Notes</span>
              <span class="text-rose-400 text-[10px] font-mono">Emitted</span>
            </div>
            <div class="text-2xl font-black text-rose-400 mt-2">
              {{ creditNotes().length }} <span class="text-sm font-normal text-slate-400">(\${{ totalCreditAmountUsd().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }})</span>
            </div>
            <div class="text-xs text-slate-500 mt-1 flex items-center gap-1.5">
              <span class="w-2 h-2 rounded-full bg-rose-400"></span>
              {{ reversedInvoicesCount() }} invoices fully reversed
            </div>
            <div class="absolute -right-3 -bottom-3 w-16 h-16 bg-rose-500/10 rounded-full blur-xl pointer-events-none"></div>
          </div>
        </div>

        <!-- Navigation Tabs -->
        <div class="border-b border-slate-800 flex items-center gap-2">
          <button
            (click)="activeTab.set('invoices')"
            [class.border-emerald-500]="activeTab() === 'invoices'"
            [class.text-emerald-400]="activeTab() === 'invoices'"
            [class.border-transparent]="activeTab() !== 'invoices'"
            [class.text-slate-400]="activeTab() !== 'invoices'"
            class="py-3 px-4 text-xs font-semibold border-b-2 hover:text-slate-200 transition flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/></svg>
            Commercial Invoices
            <span class="ml-1 px-1.5 py-0.2 bg-slate-800 text-slate-300 rounded text-[10px]">{{ filteredInvoices().length }}</span>
          </button>

          <button
            (click)="activeTab.set('credit-notes')"
            [class.border-emerald-500]="activeTab() === 'credit-notes'"
            [class.text-emerald-400]="activeTab() === 'credit-notes'"
            [class.border-transparent]="activeTab() !== 'credit-notes'"
            [class.text-slate-400]="activeTab() !== 'credit-notes'"
            class="py-3 px-4 text-xs font-semibold border-b-2 hover:text-slate-200 transition flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 14l6-6m0 0l-6-6m6 6H3"/></svg>
            Credit Notes Ledger
            <span class="ml-1 px-1.5 py-0.2 bg-slate-800 text-slate-300 rounded text-[10px]">{{ creditNotes().length }}</span>
          </button>

          <button
            (click)="activeTab.set('tax-liabilities')"
            [class.border-emerald-500]="activeTab() === 'tax-liabilities'"
            [class.text-emerald-400]="activeTab() === 'tax-liabilities'"
            [class.border-transparent]="activeTab() !== 'tax-liabilities'"
            [class.text-slate-400]="activeTab() !== 'tax-liabilities'"
            class="py-3 px-4 text-xs font-semibold border-b-2 hover:text-slate-200 transition flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3"/></svg>
            Tax Liabilities Summary
          </button>

          <button
            (click)="activeTab.set('sandbox')"
            [class.border-emerald-500]="activeTab() === 'sandbox'"
            [class.text-emerald-400]="activeTab() === 'sandbox'"
            [class.border-transparent]="activeTab() !== 'sandbox'"
            [class.text-slate-400]="activeTab() !== 'sandbox'"
            class="py-3 px-4 text-xs font-semibold border-b-2 hover:text-slate-200 transition flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19.428 15.428a2 2 0 00-1.022-.547l-2.387-.477a6 6 0 00-3.86.517l-.318.158a6 6 0 01-3.86.517L6.05 15.21a2 2 0 00-1.806.547M8 4h8l-1 1v5.172a2 2 0 00.586 1.414l5 5c1.26 1.26.367 3.414-1.415 3.414H4.828c-1.782 0-2.674-2.154-1.414-3.414l5-5A2 2 0 009 10.172V5L8 4z"/></svg>
            FX & Tax Rule Sandbox
          </button>
        </div>

        <!-- TAB 1: Invoices Table -->
        <div *ngIf="activeTab() === 'invoices'" class="space-y-4">
          <!-- Filter Controls -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4 flex flex-wrap items-center justify-between gap-4">
            <div class="flex flex-wrap items-center gap-2">
              <span class="text-xs text-slate-400 mr-1">Status:</span>
              <button
                *ngFor="let s of statusFilters"
                (click)="selectedStatusFilter.set(s)"
                [class.bg-emerald-600]="selectedStatusFilter() === s"
                [class.text-white]="selectedStatusFilter() === s"
                [class.bg-slate-800]="selectedStatusFilter() !== s"
                [class.text-slate-300]="selectedStatusFilter() !== s"
                class="px-2.5 py-1 rounded text-xs font-medium transition hover:bg-slate-700">
                {{ s }}
              </button>
            </div>

            <div class="flex items-center gap-3">
              <select
                [(ngModel)]="selectedCurrencyFilter"
                class="bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200">
                <option value="ALL">All Currencies</option>
                <option *ngFor="let c of currencies()" [value]="c.code">{{ c.flag }} {{ c.code }}</option>
              </select>

              <div class="relative">
                <input
                  type="text"
                  [(ngModel)]="searchQuery"
                  placeholder="Search invoice or customer..."
                  class="bg-slate-950 border border-slate-700 rounded-lg pl-8 pr-3 py-1.5 text-xs text-slate-200 w-56 focus:outline-none focus:border-emerald-500" />
                <svg class="w-3.5 h-3.5 text-slate-500 absolute left-2.5 top-2.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/></svg>
              </div>
            </div>
          </div>

          <!-- Invoices Table -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl overflow-hidden shadow-xl">
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300">
                <thead class="bg-slate-950 text-slate-400 font-medium uppercase tracking-wider border-b border-slate-800 text-[11px]">
                  <tr>
                    <th class="py-3 px-4">Invoice #</th>
                    <th class="py-3 px-4">Customer</th>
                    <th class="py-3 px-4">Issue Date</th>
                    <th class="py-3 px-4">Subtotal</th>
                    <th class="py-3 px-4">Tax (VAT)</th>
                    <th class="py-3 px-4">Total Amount</th>
                    <th class="py-3 px-4">Remaining</th>
                    <th class="py-3 px-4">Status</th>
                    <th class="py-3 px-4">Compliance</th>
                    <th class="py-3 px-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  <tr *ngFor="let inv of filteredInvoices()" class="hover:bg-slate-800/40 transition">
                    <td class="py-3.5 px-4 font-mono font-bold text-white">
                      {{ inv.invoiceNumber }}
                    </td>
                    <td class="py-3.5 px-4">
                      <div class="font-medium text-white flex items-center gap-1.5">
                        <span>{{ getCountryFlag(inv.customerCountry) }}</span>
                        {{ inv.customerName }}
                      </div>
                      <div class="text-[11px] text-slate-500 font-mono">
                        {{ inv.customerVatNumber || 'No VAT ID (B2C)' }}
                      </div>
                    </td>
                    <td class="py-3.5 px-4 text-slate-400">
                      {{ inv.issueDate | date:'mediumDate' }}
                    </td>
                    <td class="py-3.5 px-4 font-mono">
                      {{ inv.subtotal.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ inv.currency }}
                    </td>
                    <td class="py-3.5 px-4 font-mono">
                      <span *ngIf="inv.taxRatePct === 0" class="text-slate-400 text-[11px]">0.0% (0.00)</span>
                      <span *ngIf="inv.taxRatePct > 0" class="text-amber-400 font-semibold">
                        {{ inv.taxRatePct }}% ({{ inv.taxAmount.toLocaleString('en-US', { minimumFractionDigits: 2 }) }})
                      </span>
                    </td>
                    <td class="py-3.5 px-4 font-mono font-bold text-white">
                      {{ inv.totalAmount.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ inv.currency }}
                    </td>
                    <td class="py-3.5 px-4 font-mono font-bold" [class.text-emerald-400]="inv.remainingBalance === 0" [class.text-amber-400]="inv.remainingBalance > 0">
                      {{ inv.remainingBalance.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ inv.currency }}
                    </td>
                    <td class="py-3.5 px-4">
                      <span
                        [ngClass]="{
                          'bg-emerald-500/10 text-emerald-400 border-emerald-500/30': inv.status === 'Paid',
                          'bg-blue-500/10 text-blue-400 border-blue-500/30': inv.status === 'Issued',
                          'bg-amber-500/10 text-amber-400 border-amber-500/30': inv.status === 'PartiallyRefunded',
                          'bg-rose-500/10 text-rose-400 border-rose-500/30': inv.status === 'Reversed',
                          'bg-slate-700/30 text-slate-400 border-slate-700': inv.status === 'Draft'
                        }"
                        class="px-2 py-0.5 rounded-full text-[10px] font-bold border">
                        {{ inv.status }}
                      </span>
                    </td>
                    <td class="py-3.5 px-4">
                      <div class="flex items-center gap-1.5 flex-wrap">
                        <span *ngIf="inv.zatcaQrBase64" class="px-1.5 py-0.5 bg-emerald-950 text-emerald-300 border border-emerald-800 rounded text-[9px] font-mono">
                          ZATCA QR
                        </span>
                        <span *ngIf="inv.facturXXmlPayload" class="px-1.5 py-0.5 bg-indigo-950 text-indigo-300 border border-indigo-800 rounded text-[9px] font-mono">
                          Factur-X
                        </span>
                        <span *ngIf="inv.taxRatePct === 0 && inv.customerVatNumber" class="px-1.5 py-0.5 bg-amber-950 text-amber-300 border border-amber-800 rounded text-[9px] font-mono">
                          Art. 196
                        </span>
                      </div>
                    </td>
                    <td class="py-3.5 px-4 text-right">
                      <div class="flex items-center justify-end gap-1.5">
                        <button
                          (click)="openInvoiceDetail(inv)"
                          class="p-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded transition"
                          title="View & Print Invoice">
                          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"/></svg>
                        </button>
                        <button
                          *ngIf="inv.status !== 'Reversed' && inv.remainingBalance > 0"
                          (click)="openCreditNoteDialog(inv)"
                          class="p-1.5 bg-rose-950/60 hover:bg-rose-900 text-rose-300 rounded border border-rose-800/40 transition"
                          title="Issue Credit Note">
                          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 15v-1a4 4 0 00-4-4H8m0 0l3 3m-3-3l3-3m9 14V5a2 2 0 00-2-2H6a2 2 0 00-2 2v16l4-2 4 2 4-2 4 2z"/></svg>
                        </button>
                        <button
                          *ngIf="inv.status === 'Issued' || inv.status === 'PartiallyRefunded'"
                          (click)="settleInvoice(inv)"
                          class="p-1.5 bg-emerald-950/60 hover:bg-emerald-900 text-emerald-300 rounded border border-emerald-800/40 transition"
                          title="Mark Remaining Balance as Paid">
                          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>
                        </button>
                      </div>
                    </td>
                  </tr>

                  <tr *ngIf="filteredInvoices().length === 0">
                    <td colspan="10" class="py-8 text-center text-slate-500">
                      No commercial invoices matching filter criteria.
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>

        <!-- TAB 2: Credit Notes Ledger -->
        <div *ngIf="activeTab() === 'credit-notes'" class="space-y-4">
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4 flex items-center justify-between">
            <div>
              <h3 class="text-sm font-semibold text-white">Credit Notes Journal & Reversals</h3>
              <p class="text-xs text-slate-400">Auditable credit notes adjusting accounts receivable and fiscal tax liability.</p>
            </div>
            <div class="text-xs text-slate-400 font-mono">
              Total Emitted: <span class="text-rose-400 font-bold">{{ creditNotes().length }}</span>
            </div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl overflow-hidden shadow-xl">
            <table class="w-full text-left text-xs text-slate-300">
              <thead class="bg-slate-950 text-slate-400 font-medium uppercase tracking-wider border-b border-slate-800 text-[11px]">
                <tr>
                  <th class="py-3 px-4">Credit Note #</th>
                  <th class="py-3 px-4">Original Invoice</th>
                  <th class="py-3 px-4">Issue Date</th>
                  <th class="py-3 px-4">Reason</th>
                  <th class="py-3 px-4">Net Refund</th>
                  <th class="py-3 px-4">Tax Adjustment</th>
                  <th class="py-3 px-4">Total Credit</th>
                  <th class="py-3 px-4">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60">
                <tr *ngFor="let cn of creditNotes()" class="hover:bg-slate-800/40 transition">
                  <td class="py-3.5 px-4 font-mono font-bold text-rose-400">
                    {{ cn.creditNoteNumber }}
                  </td>
                  <td class="py-3.5 px-4 font-mono text-white">
                    {{ cn.originalInvoiceNumber }}
                  </td>
                  <td class="py-3.5 px-4 text-slate-400">
                    {{ cn.issueDate | date:'mediumDate' }}
                  </td>
                  <td class="py-3.5 px-4 text-slate-300 max-w-xs truncate" [title]="cn.reason">
                    {{ cn.reason }}
                  </td>
                  <td class="py-3.5 px-4 font-mono">
                    {{ cn.refundAmount.toLocaleString('en-US', { minimumFractionDigits: 2 }) }}
                  </td>
                  <td class="py-3.5 px-4 font-mono text-amber-400">
                    {{ cn.taxAdjustment.toLocaleString('en-US', { minimumFractionDigits: 2 }) }}
                  </td>
                  <td class="py-3.5 px-4 font-mono font-bold text-white">
                    {{ cn.totalCredit.toLocaleString('en-US', { minimumFractionDigits: 2 }) }}
                  </td>
                  <td class="py-3.5 px-4">
                    <span class="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/30">
                      {{ cn.status }}
                    </span>
                  </td>
                </tr>

                <tr *ngIf="creditNotes().length === 0">
                  <td colspan="8" class="py-8 text-center text-slate-500">
                    No credit notes emitted yet.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- TAB 3: Tax Liabilities Summary -->
        <div *ngIf="activeTab() === 'tax-liabilities'" class="space-y-4">
          <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div *ngFor="let s of taxSummaries()" class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-3 relative overflow-hidden">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="text-xl">{{ s.flag }}</span>
                  <div>
                    <h4 class="text-sm font-bold text-white">{{ s.countryName }}</h4>
                    <span class="text-[10px] text-slate-500 font-mono">{{ s.jurisdiction }} • {{ s.currency }}</span>
                  </div>
                </div>
                <span class="px-2 py-0.5 rounded bg-amber-500/10 text-amber-400 border border-amber-500/20 text-[10px] font-mono">
                  {{ s.invoicesCount }} Invoices
                </span>
              </div>

              <div class="space-y-1.5 text-xs pt-2 border-t border-slate-800/80">
                <div class="flex justify-between text-slate-400">
                  <span>Gross Taxable Base:</span>
                  <span class="font-mono text-slate-200">{{ s.grossSubtotal.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ s.currency }}</span>
                </div>
                <div class="flex justify-between text-slate-400">
                  <span>Tax Collected:</span>
                  <span class="font-mono text-amber-400">{{ s.totalTaxCollected.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ s.currency }}</span>
                </div>
                <div class="flex justify-between text-slate-400">
                  <span>Credit Note Adjustments:</span>
                  <span class="font-mono text-rose-400">-{{ s.totalCreditsAdjusted.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ s.currency }}</span>
                </div>
                <div class="flex justify-between font-bold text-white pt-1.5 border-t border-slate-800">
                  <span>Net Fiscal Liability:</span>
                  <span class="font-mono text-emerald-400 text-sm">{{ s.netTaxLiability.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ s.currency }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- TAB 4: FX & Tax Rule Sandbox -->
        <div *ngIf="activeTab() === 'sandbox'" class="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <!-- Sandbox Panel 1: Live Tax Determination Engine -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 space-y-4">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <svg class="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>
                Real-Time Tax Determination Sandbox
              </h3>
              <span class="text-[10px] px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 font-mono">Live VIES & Reverse Charge</span>
            </div>

            <div class="space-y-3 text-xs">
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-slate-400 mb-1">Merchant Country</label>
                  <select [(ngModel)]="sandboxMerchantCountry" (change)="runSandboxEvaluation()" class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200">
                    <option *ngFor="let j of jurisdictions()" [value]="j.countryCode">{{ j.flag }} {{ j.countryName }} ({{ j.countryCode }})</option>
                  </select>
                </div>
                <div>
                  <label class="block text-slate-400 mb-1">Customer Country</label>
                  <select [(ngModel)]="sandboxCustomerCountry" (change)="runSandboxEvaluation()" class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200">
                    <option *ngFor="let j of jurisdictions()" [value]="j.countryCode">{{ j.flag }} {{ j.countryName }} ({{ j.countryCode }})</option>
                  </select>
                </div>
              </div>

              <div class="flex items-center justify-between p-3 bg-slate-950/60 rounded-lg border border-slate-800">
                <div>
                  <span class="text-slate-200 font-semibold block">B2B Commercial Sale</span>
                  <span class="text-[11px] text-slate-500">Enables cross-border EU reverse charge under Art. 196</span>
                </div>
                <input type="checkbox" [(ngModel)]="sandboxIsB2B" (change)="runSandboxEvaluation()" class="w-4 h-4 rounded text-emerald-600 bg-slate-900 border-slate-700" />
              </div>

              <div>
                <label class="block text-slate-400 mb-1 flex items-center justify-between">
                  <span>Customer VAT / VIES Number</span>
                  <span *ngIf="sandboxViesValid()" class="text-emerald-400 text-[10px] font-mono">✓ Valid VIES Format</span>
                  <span *ngIf="!sandboxViesValid() && sandboxVatNumber" class="text-rose-400 text-[10px] font-mono">✕ Invalid Format</span>
                </label>
                <input
                  type="text"
                  [(ngModel)]="sandboxVatNumber"
                  (input)="runSandboxEvaluation()"
                  placeholder="e.g. FR32123456789 or DE123456789"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200 font-mono text-xs" />
                <span class="text-[10px] text-slate-500 block mt-1">
                  Format hint: FR (FR+11 chars), DE (DE+9 digits), IT (IT+11 digits), ES (ES+9 chars), SA (15 digits).
                </span>
              </div>

              <div>
                <label class="block text-slate-400 mb-1">Subtotal Amount</label>
                <input
                  type="number"
                  [(ngModel)]="sandboxSubtotal"
                  (input)="runSandboxEvaluation()"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200 font-mono text-xs" />
              </div>

              <!-- Output Evaluation Result -->
              <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2 mt-2">
                <div class="flex justify-between items-center">
                  <span class="text-slate-400">Scheme Applied:</span>
                  <span class="font-semibold text-white">{{ sandboxResult()?.taxScheme || 'Standard' }}</span>
                </div>
                <div class="flex justify-between items-center">
                  <span class="text-slate-400">Tax Rate:</span>
                  <span class="font-mono font-bold text-amber-400 text-sm">{{ sandboxResult()?.applicableTaxRatePct }}%</span>
                </div>
                <div class="flex justify-between items-center">
                  <span class="text-slate-400">Calculated VAT:</span>
                  <span class="font-mono text-amber-400">\${{ (sandboxResult()?.taxAmount || 0).toFixed(2) }}</span>
                </div>
                <div *ngIf="sandboxResult()?.isReverseCharge" class="p-2.5 rounded bg-emerald-950/50 border border-emerald-800/40 text-[11px] text-emerald-300">
                  🛡️ <strong>Reverse Charge Applied:</strong> {{ sandboxResult()?.exemptionReason }}
                </div>
              </div>
            </div>
          </div>

          <!-- Sandbox Panel 2: FX Spot Rate & Margin Spread Calculator -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-6 space-y-4">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                <svg class="w-4 h-4 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>
                Currency Converter & Safety Spread Buffer
              </h3>
              <span class="text-[10px] px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 font-mono">1.2% - 2.0% Spread</span>
            </div>

            <div class="space-y-3 text-xs">
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-slate-400 mb-1">From Currency</label>
                  <select [(ngModel)]="fxFrom" class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200">
                    <option *ngFor="let c of currencies()" [value]="c.code">{{ c.flag }} {{ c.code }} ({{ c.currencyName }})</option>
                  </select>
                </div>
                <div>
                  <label class="block text-slate-400 mb-1">To Currency</label>
                  <select [(ngModel)]="fxTo" class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200">
                    <option *ngFor="let c of currencies()" [value]="c.code">{{ c.flag }} {{ c.code }} ({{ c.currencyName }})</option>
                  </select>
                </div>
              </div>

              <div>
                <label class="block text-slate-400 mb-1">Source Amount</label>
                <input
                  type="number"
                  [(ngModel)]="fxAmount"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200 font-mono text-xs" />
              </div>

              <!-- Converter Results -->
              <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2 mt-2">
                <div class="flex justify-between items-center">
                  <span class="text-slate-400">Interbank Spot Rate:</span>
                  <span class="font-mono text-slate-300">1 USD = {{ getCurrencySpot(fxTo)?.spotRateToUsd }} {{ fxTo }}</span>
                </div>
                <div class="flex justify-between items-center">
                  <span class="text-slate-400">Spread Margin Buffer:</span>
                  <span class="font-mono text-cyan-400">+{{ getCurrencySpot(fxTo)?.spreadMarginPct }}%</span>
                </div>
                <div class="flex justify-between items-center">
                  <span class="text-slate-400">Effective FX Rate:</span>
                  <span class="font-mono text-cyan-400 font-semibold">{{ getEffectiveRate(fxTo) }}</span>
                </div>
                <div class="flex justify-between items-center pt-2 border-t border-slate-800 font-bold">
                  <span class="text-white">Converted Value:</span>
                  <span class="text-base font-mono text-emerald-400">{{ calculatedFxConversion() }} {{ fxTo }}</span>
                </div>
              </div>

              <!-- Currency Grid -->
              <div class="pt-2">
                <span class="text-[11px] font-semibold text-slate-400 block mb-2">Pre-Seeded FX Spot Rate Feeds</span>
                <div class="grid grid-cols-3 gap-2">
                  <div *ngFor="let c of currencies()" class="p-2 bg-slate-950/60 rounded border border-slate-800/80 text-[10px]">
                    <div class="flex items-center justify-between">
                      <span class="font-bold text-white">{{ c.flag }} {{ c.code }}</span>
                      <span class="text-cyan-400 font-mono">+{{ c.spreadMarginPct }}%</span>
                    </div>
                    <div class="text-slate-400 mt-1 font-mono">1 USD = {{ c.spotRateToUsd }}</div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </main>

      <!-- MODAL 1: View / Print Commercial Invoice Modal -->
      <div *ngIf="selectedInvoiceForView()" class="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4 overflow-y-auto">
        <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-3xl w-full p-6 space-y-6 shadow-2xl my-8">
          <!-- Modal Header -->
          <div class="flex items-center justify-between pb-4 border-b border-slate-800">
            <div class="flex items-center gap-3">
              <span class="text-lg font-bold text-white">Commercial Invoice</span>
              <span class="font-mono text-emerald-400 font-semibold">{{ selectedInvoiceForView()?.invoiceNumber }}</span>
            </div>
            <div class="flex items-center gap-2">
              <button
                (click)="printInvoice()"
                class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs font-semibold transition flex items-center gap-1.5">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z"/></svg>
                Print / PDF
              </button>
              <button (click)="selectedInvoiceForView.set(null)" class="text-slate-400 hover:text-white p-1">✕</button>
            </div>
          </div>

          <!-- Invoice Printable Sheet -->
          <div id="printable-invoice" class="bg-slate-950 p-6 rounded-xl border border-slate-800 space-y-6 text-xs text-slate-300">
            <!-- Company & Invoice Header -->
            <div class="flex justify-between items-start">
              <div>
                <h2 class="text-xl font-black bg-gradient-to-r from-emerald-400 to-teal-200 bg-clip-text text-transparent">
                  SGrowing Enterprise Technologies
                </h2>
                <p class="text-slate-400 text-[11px] mt-1">Global AI Omnichannel Commerce & Communications Mesh</p>
                <p class="text-slate-500 text-[10px] font-mono mt-0.5">EU VAT ID: FR88990011223 • SIRET 889 900 112 00014</p>
                <p class="text-slate-500 text-[10px]">10 Boulevard Haussmann, 75009 Paris, France</p>
              </div>

              <div class="text-right space-y-1">
                <div class="text-base font-mono font-bold text-white">{{ selectedInvoiceForView()?.invoiceNumber }}</div>
                <div class="text-[11px] text-slate-400">Issue Date: {{ selectedInvoiceForView()?.issueDate | date:'mediumDate' }}</div>
                <div class="text-[11px] text-slate-400">Due Date: {{ selectedInvoiceForView()?.dueDate | date:'mediumDate' }}</div>
                <span
                  [ngClass]="{
                    'bg-emerald-500/10 text-emerald-400 border-emerald-500/30': selectedInvoiceForView()?.status === 'Paid',
                    'bg-blue-500/10 text-blue-400 border-blue-500/30': selectedInvoiceForView()?.status === 'Issued',
                    'bg-amber-500/10 text-amber-400 border-amber-500/30': selectedInvoiceForView()?.status === 'PartiallyRefunded',
                    'bg-rose-500/10 text-rose-400 border-rose-500/30': selectedInvoiceForView()?.status === 'Reversed'
                  }"
                  class="inline-block px-2 py-0.5 rounded text-[10px] font-bold border mt-1">
                  {{ selectedInvoiceForView()?.status }}
                </span>
              </div>
            </div>

            <!-- Buyer Details -->
            <div class="p-3 bg-slate-900/60 rounded-lg border border-slate-800/80 grid grid-cols-2 gap-4">
              <div>
                <span class="text-[10px] text-slate-500 uppercase tracking-wider font-semibold block">Billed To (Customer):</span>
                <div class="font-bold text-white text-sm mt-0.5">{{ selectedInvoiceForView()?.customerName }}</div>
                <div class="text-slate-400 text-[11px]">{{ selectedInvoiceForView()?.customerEmail }}</div>
                <div class="text-slate-400 text-[11px]">{{ getCountryName(selectedInvoiceForView()?.customerCountry || '') }} ({{ selectedInvoiceForView()?.customerCountry }})</div>
              </div>
              <div class="text-right">
                <span class="text-[10px] text-slate-500 uppercase tracking-wider font-semibold block">Tax Registration / VAT ID:</span>
                <div class="font-mono text-emerald-400 font-semibold mt-0.5">
                  {{ selectedInvoiceForView()?.customerVatNumber || 'Non-Taxable Consumer / No VAT ID' }}
                </div>
                <div class="text-[11px] text-slate-400 mt-1">
                  Currency: <strong class="text-white">{{ selectedInvoiceForView()?.currency }}</strong>
                </div>
              </div>
            </div>

            <!-- Itemized Table -->
            <table class="w-full text-left text-xs">
              <thead class="border-b border-slate-800 text-slate-400 uppercase text-[10px]">
                <tr>
                  <th class="py-2 px-1">Description</th>
                  <th class="py-2 px-1 text-center">Qty</th>
                  <th class="py-2 px-1 text-right">Unit Price</th>
                  <th class="py-2 px-1 text-right">Amount</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60">
                <tr *ngFor="let item of selectedInvoiceForView()?.lineItems">
                  <td class="py-2.5 px-1 font-medium text-white">{{ item.description }}</td>
                  <td class="py-2.5 px-1 text-center text-slate-400">{{ item.quantity }}</td>
                  <td class="py-2.5 px-1 text-right font-mono">{{ item.unitPrice.toLocaleString('en-US', { minimumFractionDigits: 2 }) }}</td>
                  <td class="py-2.5 px-1 text-right font-mono font-bold text-white">{{ item.totalPrice.toLocaleString('en-US', { minimumFractionDigits: 2 }) }}</td>
                </tr>
              </tbody>
            </table>

            <!-- Financial Summation -->
            <div class="flex justify-end pt-2 border-t border-slate-800">
              <div class="w-64 space-y-1.5 text-xs">
                <div class="flex justify-between text-slate-400">
                  <span>Subtotal:</span>
                  <span class="font-mono text-white">{{ selectedInvoiceForView()?.subtotal?.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ selectedInvoiceForView()?.currency }}</span>
                </div>
                <div class="flex justify-between text-slate-400">
                  <span>VAT / Tax ({{ selectedInvoiceForView()?.taxRatePct }}%):</span>
                  <span class="font-mono text-amber-400">{{ selectedInvoiceForView()?.taxAmount?.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ selectedInvoiceForView()?.currency }}</span>
                </div>
                <div class="flex justify-between text-slate-400 font-bold text-white pt-1 border-t border-slate-800">
                  <span>Total Amount:</span>
                  <span class="font-mono text-white">{{ selectedInvoiceForView()?.totalAmount?.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ selectedInvoiceForView()?.currency }}</span>
                </div>
                <div *ngIf="(selectedInvoiceForView()?.creditNotesApplied || 0) > 0" class="flex justify-between text-rose-400">
                  <span>Credit Notes Applied:</span>
                  <span class="font-mono">-{{ selectedInvoiceForView()?.creditNotesApplied?.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ selectedInvoiceForView()?.currency }}</span>
                </div>
                <div class="flex justify-between font-extrabold text-sm text-emerald-400 pt-1 border-t border-slate-800">
                  <span>Remaining Payable:</span>
                  <span class="font-mono">{{ selectedInvoiceForView()?.remainingBalance?.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ selectedInvoiceForView()?.currency }}</span>
                </div>
              </div>
            </div>

            <!-- ZATCA Phase 2 QR Visual Section (if applicable) -->
            <div *ngIf="selectedInvoiceForView()?.zatcaQrBase64" class="p-4 bg-slate-900 rounded-xl border border-emerald-800/40 space-y-3">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="text-emerald-400 font-bold text-xs">🛡️ ZATCA Phase 2 E-Invoice QR Code</span>
                  <span class="text-[10px] text-slate-400 font-mono">Saudi Arabia Tax Authority TLV Compliant</span>
                </div>
                <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300 text-[10px] font-mono">TLV Base64</span>
              </div>

              <div class="flex items-center gap-4">
                <!-- Visual stylized QR matrix simulation -->
                <div class="w-24 h-24 bg-white p-2 rounded-lg flex items-center justify-center shrink-0 shadow-lg">
                  <div class="w-full h-full bg-slate-950 flex flex-col justify-between p-1 rounded">
                    <div class="flex justify-between">
                      <div class="w-4 h-4 bg-white border border-slate-950"></div>
                      <div class="w-4 h-4 bg-white border border-slate-950"></div>
                    </div>
                    <div class="text-[7px] text-center font-mono text-white">ZATCA</div>
                    <div class="flex justify-between">
                      <div class="w-4 h-4 bg-white border border-slate-950"></div>
                      <div class="w-2 h-2 bg-emerald-400"></div>
                    </div>
                  </div>
                </div>

                <div class="flex-1 space-y-1 text-[11px]">
                  <div class="text-slate-400"><strong>Tag 1 (Seller):</strong> SGrowing Saudi Arabia Ltd</div>
                  <div class="text-slate-400"><strong>Tag 2 (VAT No):</strong> 310123456700003</div>
                  <div class="text-slate-400"><strong>Tag 3 (Timestamp):</strong> {{ selectedInvoiceForView()?.issueDate }}</div>
                  <div class="text-slate-400"><strong>Tag 4 (Total):</strong> {{ selectedInvoiceForView()?.totalAmount }} SAR</div>
                  <div class="text-slate-400"><strong>Tag 5 (VAT):</strong> {{ selectedInvoiceForView()?.taxAmount }} SAR</div>
                </div>
              </div>

              <div class="pt-2">
                <span class="text-[10px] text-slate-500 block mb-1">Base64 TLV Payload:</span>
                <input
                  type="text"
                  readonly
                  [value]="selectedInvoiceForView()?.zatcaQrBase64"
                  class="w-full bg-slate-950 border border-slate-800 rounded px-2.5 py-1 text-[10px] font-mono text-emerald-400" />
              </div>
            </div>

            <!-- Factur-X / EN 16931 Electronic XML Section -->
            <div *ngIf="selectedInvoiceForView()?.facturXXmlPayload" class="p-3 bg-slate-900 rounded-lg border border-indigo-900/40 space-y-2">
              <div class="flex items-center justify-between">
                <span class="text-indigo-400 font-semibold text-xs flex items-center gap-1.5">
                  📄 Factur-X / ZUGFeRD CII Electronic Invoice Payload
                </span>
                <button
                  (click)="copyXmlToClipboard(selectedInvoiceForView()?.facturXXmlPayload || '')"
                  class="px-2 py-0.5 bg-indigo-950 hover:bg-indigo-900 text-indigo-300 rounded text-[10px] font-mono transition">
                  Copy XML
                </button>
              </div>
              <pre class="bg-slate-950 p-3 rounded font-mono text-[10px] text-slate-400 overflow-x-auto max-h-36">{{ selectedInvoiceForView()?.facturXXmlPayload }}</pre>
            </div>
          </div>
        </div>
      </div>

      <!-- MODAL 2: Issue Credit Note Dialog -->
      <div *ngIf="invoiceForCreditNote()" class="fixed inset-0 bg-black/75 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-md w-full p-6 space-y-4 shadow-2xl">
          <div class="flex items-center justify-between pb-3 border-b border-slate-800">
            <div>
              <h3 class="text-base font-bold text-white">Issue Credit Note</h3>
              <p class="text-xs text-slate-400">For Invoice <span class="font-mono text-emerald-400">{{ invoiceForCreditNote()?.invoiceNumber }}</span></p>
            </div>
            <button (click)="invoiceForCreditNote.set(null)" class="text-slate-400 hover:text-white">✕</button>
          </div>

          <div class="space-y-3 text-xs">
            <div class="p-3 bg-slate-950 rounded-lg border border-slate-800 flex justify-between">
              <span class="text-slate-400">Current Remaining Balance:</span>
              <span class="font-mono font-bold text-white">
                {{ invoiceForCreditNote()?.remainingBalance?.toLocaleString('en-US', { minimumFractionDigits: 2 }) }} {{ invoiceForCreditNote()?.currency }}
              </span>
            </div>

            <div>
              <label class="block text-slate-400 mb-1">Reason for Adjustment / Credit *</label>
              <select [(ngModel)]="creditNoteReason" class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200">
                <option value="SLA delivery latency credit on monthly cloud nodes">SLA delivery latency credit on monthly cloud nodes</option>
                <option value="Contract scope reduction or downgrade">Contract scope reduction or downgrade</option>
                <option value="Billing discrepancy / incorrect line item">Billing discrepancy / incorrect line item</option>
                <option value="Full contract cancellation within cooling-off period">Full contract cancellation within cooling-off period</option>
              </select>
            </div>

            <div>
              <div class="flex items-center justify-between mb-1">
                <label class="text-slate-400">Refund Amount (Gross Credit) *</label>
                <button
                  (click)="setFullCreditAmount()"
                  class="text-[10px] text-emerald-400 hover:underline font-mono">
                  Credit Full Balance
                </button>
              </div>
              <input
                type="number"
                [(ngModel)]="creditNoteAmount"
                [max]="invoiceForCreditNote()?.remainingBalance || 0"
                class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200 font-mono text-xs" />
              <span *ngIf="creditNoteAmount > (invoiceForCreditNote()?.remainingBalance || 0)" class="text-rose-400 text-[10px] block mt-1">
                ⚠️ Credit amount cannot exceed remaining invoice balance!
              </span>
            </div>

            <!-- Tax Calculation Preview -->
            <div class="p-3 bg-slate-950 rounded border border-slate-800 space-y-1.5 text-slate-300">
              <div class="flex justify-between">
                <span>Gross Credit Applied:</span>
                <span class="font-mono text-white font-bold">{{ creditNoteAmount.toFixed(2) }} {{ invoiceForCreditNote()?.currency }}</span>
              </div>
              <div class="flex justify-between text-amber-400">
                <span>Tax Adjustment Proportion:</span>
                <span class="font-mono">{{ calculateTaxAdjustmentPreview().toFixed(2) }} {{ invoiceForCreditNote()?.currency }}</span>
              </div>
              <div class="flex justify-between text-slate-400">
                <span>Net Subtotal Refund:</span>
                <span class="font-mono">{{ (creditNoteAmount - calculateTaxAdjustmentPreview()).toFixed(2) }} {{ invoiceForCreditNote()?.currency }}</span>
              </div>
            </div>

            <div class="flex items-center gap-3 pt-2">
              <button
                (click)="invoiceForCreditNote.set(null)"
                class="flex-1 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg font-semibold transition">
                Cancel
              </button>
              <button
                (click)="confirmCreditNote()"
                [disabled]="creditNoteAmount <= 0 || creditNoteAmount > (invoiceForCreditNote()?.remainingBalance || 0)"
                class="flex-1 py-2 bg-rose-600 hover:bg-rose-500 disabled:opacity-50 text-white rounded-lg font-semibold transition">
                Emit Credit Note
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- MODAL 3: Issue New Commercial Invoice Dialog -->
      <div *ngIf="openNewInvoiceModal()" class="fixed inset-0 bg-black/75 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-2xl">
          <div class="flex items-center justify-between pb-3 border-b border-slate-800">
            <h3 class="text-base font-bold text-white">Create Compliant Commercial Invoice</h3>
            <button (click)="openNewInvoiceModal.set(false)" class="text-slate-400 hover:text-white">✕</button>
          </div>

          <div class="space-y-3 text-xs">
            <div>
              <label class="block text-slate-400 mb-1">Customer / Organization Name *</label>
              <input
                type="text"
                [(ngModel)]="newInvoiceCustomer"
                placeholder="e.g. Novartis AG or Delivery Hero SE"
                class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200" />
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-slate-400 mb-1">Customer Email</label>
                <input
                  type="email"
                  [(ngModel)]="newInvoiceEmail"
                  placeholder="billing@customer.com"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200" />
              </div>
              <div>
                <label class="block text-slate-400 mb-1">Customer Country</label>
                <select [(ngModel)]="newInvoiceCountry" (change)="recalcNewInvoiceTax()" class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200">
                  <option *ngFor="let j of jurisdictions()" [value]="j.countryCode">{{ j.flag }} {{ j.countryName }}</option>
                </select>
              </div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-slate-400 mb-1">Customer VAT ID</label>
                <input
                  type="text"
                  [(ngModel)]="newInvoiceVat"
                  (input)="recalcNewInvoiceTax()"
                  placeholder="e.g. DE123456789"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200 font-mono" />
              </div>
              <div>
                <label class="block text-slate-400 mb-1">Currency</label>
                <select [(ngModel)]="newInvoiceCurrency" class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200">
                  <option *ngFor="let c of currencies()" [value]="c.code">{{ c.flag }} {{ c.code }}</option>
                </select>
              </div>
            </div>

            <div>
              <label class="block text-slate-400 mb-1">Line Item Description</label>
              <input
                type="text"
                [(ngModel)]="newInvoiceItemDesc"
                placeholder="e.g. Dedicated Enterprise AI Cluster & Telephony SLA"
                class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200" />
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-slate-400 mb-1">Quantity</label>
                <input type="number" [(ngModel)]="newInvoiceQuantity" (input)="recalcNewInvoiceTax()" class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200" />
              </div>
              <div>
                <label class="block text-slate-400 mb-1">Unit Price</label>
                <input type="number" [(ngModel)]="newInvoiceUnitPrice" (input)="recalcNewInvoiceTax()" class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2 text-slate-200 font-mono" />
              </div>
            </div>

            <!-- Tax Determination Preview -->
            <div class="p-3 bg-slate-950 rounded border border-slate-800 space-y-1 text-slate-300">
              <div class="flex justify-between">
                <span>Subtotal:</span>
                <span class="font-mono">{{ (newInvoiceQuantity * newInvoiceUnitPrice).toFixed(2) }} {{ newInvoiceCurrency }}</span>
              </div>
              <div class="flex justify-between text-amber-400">
                <span>Tax Rate & Amount:</span>
                <span class="font-mono">{{ newInvoiceTaxRate }}% ({{ (newInvoiceQuantity * newInvoiceUnitPrice * newInvoiceTaxRate / 100).toFixed(2) }} {{ newInvoiceCurrency }})</span>
              </div>
              <div class="flex justify-between font-bold text-white pt-1 border-t border-slate-800">
                <span>Total Amount:</span>
                <span class="font-mono text-emerald-400">{{ (newInvoiceQuantity * newInvoiceUnitPrice * (1 + newInvoiceTaxRate / 100)).toFixed(2) }} {{ newInvoiceCurrency }}</span>
              </div>
            </div>

            <div class="flex items-center gap-3 pt-2">
              <button
                (click)="openNewInvoiceModal.set(false)"
                class="flex-1 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg font-semibold transition">
                Cancel
              </button>
              <button
                (click)="submitNewInvoice()"
                [disabled]="!newInvoiceCustomer || newInvoiceUnitPrice <= 0"
                class="flex-1 py-2 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded-lg font-semibold transition">
                Issue & Sign Invoice
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class TaxInvoiceLedgerStudioComponent implements OnInit {
  private readonly http = inject(HttpClient);

  // Active state
  activeTab = signal<'invoices' | 'credit-notes' | 'tax-liabilities' | 'sandbox'>('invoices');
  selectedStatusFilter = signal<string>('All');
  selectedCurrencyFilter = 'ALL';
  searchQuery = '';

  // Data signals
  invoices = signal<CommercialInvoiceDto[]>([]);
  creditNotes = signal<CreditNoteDto[]>([]);
  currencies = signal<CurrencySpotRateDto[]>([]);
  jurisdictions = signal<TaxJurisdictionRuleDto[]>([]);
  taxSummaries = signal<TaxLiabilitySummaryItemDto[]>([]);

  // Dialog signals
  selectedInvoiceForView = signal<CommercialInvoiceDto | null>(null);
  invoiceForCreditNote = signal<CommercialInvoiceDto | null>(null);
  openNewInvoiceModal = signal<boolean>(false);

  // Credit Note dialog inputs
  creditNoteReason = 'SLA delivery latency credit on monthly cloud nodes';
  creditNoteAmount = 0;

  // Sandbox inputs & state
  sandboxMerchantCountry = 'FR';
  sandboxCustomerCountry = 'DE';
  sandboxIsB2B = true;
  sandboxVatNumber = 'DE123456789';
  sandboxSubtotal = 10000;
  sandboxResult = signal<TaxEvaluationResultDto | null>(null);
  sandboxViesValid = signal<boolean>(true);

  // FX converter inputs
  fxAmount = 1000;
  fxFrom = 'USD';
  fxTo = 'EUR';

  // New Invoice dialog inputs
  newInvoiceCustomer = '';
  newInvoiceEmail = '';
  newInvoiceCountry = 'DE';
  newInvoiceVat = 'DE123456789';
  newInvoiceCurrency = 'EUR';
  newInvoiceItemDesc = 'Autonomous High-Volume Inbound Voice Mesh SLA';
  newInvoiceQuantity = 1;
  newInvoiceUnitPrice = 7500;
  newInvoiceTaxRate = 0;

  statusFilters = ['All', 'Issued', 'Paid', 'PartiallyRefunded', 'Reversed', 'Draft'];

  // Computed KPIs
  totalInvoicedUsd = computed(() => {
    return this.invoices().reduce((acc, inv) => {
      const spot = this.getCurrencySpot(inv.currency)?.spotRateToUsd || 1.0;
      return acc + (inv.totalAmount / spot);
    }, 0);
  });

  totalOutstandingUsd = computed(() => {
    return this.invoices().reduce((acc, inv) => {
      if (inv.remainingBalance <= 0) return acc;
      const spot = this.getCurrencySpot(inv.currency)?.spotRateToUsd || 1.0;
      return acc + (inv.remainingBalance / spot);
    }, 0);
  });

  totalTaxCollectedUsd = computed(() => {
    return this.invoices().reduce((acc, inv) => {
      const spot = this.getCurrencySpot(inv.currency)?.spotRateToUsd || 1.0;
      return acc + (inv.taxAmount / spot);
    }, 0);
  });

  totalCreditAmountUsd = computed(() => {
    return this.creditNotes().reduce((acc, cn) => {
      const inv = this.invoices().find(i => i.id === cn.originalInvoiceId);
      const spot = this.getCurrencySpot(inv?.currency || 'USD')?.spotRateToUsd || 1.0;
      return acc + (cn.totalCredit / spot);
    }, 0);
  });

  activeUnpaidCount = computed(() => {
    return this.invoices().filter(i => i.remainingBalance > 0).length;
  });

  reversedInvoicesCount = computed(() => {
    return this.invoices().filter(i => i.status === 'Reversed').length;
  });

  filteredInvoices = computed(() => {
    const list = this.invoices();
    const status = this.selectedStatusFilter();
    const cur = this.selectedCurrencyFilter;
    const q = this.searchQuery.trim().toLowerCase();

    return list.filter(inv => {
      if (status !== 'All' && inv.status !== status) return false;
      if (cur !== 'ALL' && inv.currency !== cur) return false;
      if (q) {
        const matchesNum = inv.invoiceNumber.toLowerCase().includes(q);
        const matchesCust = inv.customerName.toLowerCase().includes(q);
        const matchesVat = (inv.customerVatNumber || '').toLowerCase().includes(q);
        if (!matchesNum && !matchesCust && !matchesVat) return false;
      }
      return true;
    });
  });

  ngOnInit() {
    this.initializeData();
    this.runSandboxEvaluation();
    this.recalcNewInvoiceTax();
  }

  private initializeData() {
    // 1. Pre-seed Currencies
    this.currencies.set([
      { code: 'USD', currencyName: 'US Dollar', symbol: '$', spotRateToUsd: 1.0, spreadMarginPct: 0.0, effectiveRateToUsd: 1.0, lastUpdated: new Date().toISOString(), flag: '🇺🇸' },
      { code: 'EUR', currencyName: 'Euro', symbol: '€', spotRateToUsd: 0.92, spreadMarginPct: 1.5, effectiveRateToUsd: 0.9338, lastUpdated: new Date().toISOString(), flag: '🇪🇺' },
      { code: 'GBP', currencyName: 'British Pound', symbol: '£', spotRateToUsd: 0.79, spreadMarginPct: 1.5, effectiveRateToUsd: 0.80185, lastUpdated: new Date().toISOString(), flag: '🇬🇧' },
      { code: 'SAR', currencyName: 'Saudi Riyal', symbol: '﷼', spotRateToUsd: 3.75, spreadMarginPct: 1.2, effectiveRateToUsd: 3.795, lastUpdated: new Date().toISOString(), flag: '🇸🇦' },
      { code: 'AED', currencyName: 'UAE Dirham', symbol: 'د.إ', spotRateToUsd: 3.67, spreadMarginPct: 1.2, effectiveRateToUsd: 3.71404, lastUpdated: new Date().toISOString(), flag: '🇦🇪' },
      { code: 'TND', currencyName: 'Tunisian Dinar', symbol: 'DT', spotRateToUsd: 3.12, spreadMarginPct: 2.0, effectiveRateToUsd: 3.1824, lastUpdated: new Date().toISOString(), flag: '🇹🇳' }
    ]);

    // 2. Pre-seed Jurisdictions
    this.jurisdictions.set([
      { countryCode: 'FR', countryName: 'France', standardRatePct: 20.0, reducedRatePct: 5.5, isEuMember: true, isGccMember: false, hasViesSupport: true, hasZatcaPhase2: false, taxName: 'TVA', flag: '🇫🇷' },
      { countryCode: 'DE', countryName: 'Germany', standardRatePct: 19.0, reducedRatePct: 7.0, isEuMember: true, isGccMember: false, hasViesSupport: true, hasZatcaPhase2: false, taxName: 'MwSt', flag: '🇩🇪' },
      { countryCode: 'IT', countryName: 'Italy', standardRatePct: 22.0, reducedRatePct: 10.0, isEuMember: true, isGccMember: false, hasViesSupport: true, hasZatcaPhase2: false, taxName: 'IVA', flag: '🇮🇹' },
      { countryCode: 'ES', countryName: 'Spain', standardRatePct: 21.0, reducedRatePct: 10.0, isEuMember: true, isGccMember: false, hasViesSupport: true, hasZatcaPhase2: false, taxName: 'IVA', flag: '🇪🇸' },
      { countryCode: 'SA', countryName: 'Saudi Arabia', standardRatePct: 15.0, reducedRatePct: 0.0, isEuMember: false, isGccMember: true, hasViesSupport: false, hasZatcaPhase2: true, taxName: 'ZATCA VAT', flag: '🇸🇦' },
      { countryCode: 'AE', countryName: 'United Arab Emirates', standardRatePct: 5.0, reducedRatePct: 0.0, isEuMember: false, isGccMember: true, hasViesSupport: false, hasZatcaPhase2: false, taxName: 'FTA VAT', flag: '🇦🇪' },
      { countryCode: 'TN', countryName: 'Tunisia', standardRatePct: 19.0, reducedRatePct: 7.0, isEuMember: false, isGccMember: false, hasViesSupport: false, hasZatcaPhase2: false, taxName: 'TVA', flag: '🇹🇳' },
      { countryCode: 'GB', countryName: 'United Kingdom', standardRatePct: 20.0, reducedRatePct: 5.0, isEuMember: false, isGccMember: false, hasViesSupport: false, hasZatcaPhase2: false, taxName: 'VAT', flag: '🇬🇧' },
      { countryCode: 'US', countryName: 'United States', standardRatePct: 0.0, reducedRatePct: 0.0, isEuMember: false, isGccMember: false, hasViesSupport: false, hasZatcaPhase2: false, taxName: 'Sales Tax', flag: '🇺🇸' }
    ]);

    // 3. Pre-seed Invoices
    this.invoices.set([
      {
        id: '11111111-1111-1111-1111-111111110001',
        tenantId: '00000000-0000-0000-0000-000000000001',
        invoiceNumber: 'INV-2026-00001',
        issueDate: '2026-10-15T09:00:00Z',
        dueDate: '2026-11-15T09:00:00Z',
        currency: 'EUR',
        subtotal: 12500,
        taxRatePct: 0,
        taxAmount: 0,
        totalAmount: 12500,
        remainingBalance: 0,
        creditNotesApplied: 0,
        status: 'Paid',
        customerName: 'Doctolib SAS',
        customerEmail: 'billing@doctolib.fr',
        customerCountry: 'FR',
        customerVatNumber: 'FR32123456789',
        merchantCountry: 'DE',
        facturXXmlPayload: '<rsm:CrossIndustryInvoice ...>...</rsm:CrossIndustryInvoice>',
        lineItems: [
          { id: '1', description: 'Enterprise Healthcare Telephony SLA', quantity: 1, unitPrice: 8500, totalPrice: 8500 },
          { id: '2', description: 'Dedicated HIPAA/GDPR Cloud Enclave', quantity: 1, unitPrice: 4000, totalPrice: 4000 }
        ]
      },
      {
        id: '11111111-1111-1111-1111-111111110002',
        tenantId: '00000000-0000-0000-0000-000000000001',
        invoiceNumber: 'INV-2026-00002',
        issueDate: '2026-10-18T11:00:00Z',
        dueDate: '2026-11-18T11:00:00Z',
        currency: 'EUR',
        subtotal: 8400,
        taxRatePct: 0,
        taxAmount: 0,
        totalAmount: 8400,
        remainingBalance: 8400,
        creditNotesApplied: 0,
        status: 'Issued',
        customerName: 'Siemens AG',
        customerEmail: 'ap-invoices@siemens.de',
        customerCountry: 'DE',
        customerVatNumber: 'DE123456789',
        merchantCountry: 'FR',
        facturXXmlPayload: '<rsm:CrossIndustryInvoice ...>...</rsm:CrossIndustryInvoice>',
        lineItems: [
          { id: '1', description: 'Autonomous Inbound Voice AI Pipeline', quantity: 2, unitPrice: 4200, totalPrice: 8400 }
        ]
      },
      {
        id: '11111111-1111-1111-1111-111111110003',
        tenantId: '00000000-0000-0000-0000-000000000001',
        invoiceNumber: 'INV-2026-00003',
        issueDate: '2026-10-20T14:30:00Z',
        dueDate: '2026-11-20T14:30:00Z',
        currency: 'SAR',
        subtotal: 45000,
        taxRatePct: 15,
        taxAmount: 6750,
        totalAmount: 51750,
        remainingBalance: 51750,
        creditNotesApplied: 0,
        status: 'Issued',
        customerName: 'Al Rajhi Retail Logistics',
        customerEmail: 'procurement@alrajhi-logistics.sa',
        customerCountry: 'SA',
        customerVatNumber: '310123456700003',
        merchantCountry: 'SA',
        zatcaQrBase64: 'AQpTR3Jvd2luZyBTYXVkaSBBcmFiaWEgTHRkAg8zMTAxMjM0NTY3MDAwMDMDGDIwMjYtMTAtMjBUMTQ6MzA6MDBaBAY1MTc1MC4wMAYGNjc1MC4wMA==',
        facturXXmlPayload: '<rsm:CrossIndustryInvoice ...>...</rsm:CrossIndustryInvoice>',
        lineItems: [
          { id: '1', description: 'Omnichannel WhatsApp Fulfillment Mesh', quantity: 1, unitPrice: 30000, totalPrice: 30000 },
          { id: '2', description: 'High-Capacity Arabic Voice Agent Cluster', quantity: 1, unitPrice: 15000, totalPrice: 15000 }
        ]
      },
      {
        id: '11111111-1111-1111-1111-111111110004',
        tenantId: '00000000-0000-0000-0000-000000000001',
        invoiceNumber: 'INV-2026-00004',
        issueDate: '2026-10-22T08:15:00Z',
        dueDate: '2026-11-22T08:15:00Z',
        currency: 'AED',
        subtotal: 32000,
        taxRatePct: 5,
        taxAmount: 1600,
        totalAmount: 33600,
        remainingBalance: 0,
        creditNotesApplied: 0,
        status: 'Paid',
        customerName: 'Emaar Properties PJSC',
        customerEmail: 'finance@emaar.ae',
        customerCountry: 'AE',
        customerVatNumber: '100234567800003',
        merchantCountry: 'AE',
        lineItems: [
          { id: '1', description: 'Luxury Real Estate Lead Journey Suite', quantity: 1, unitPrice: 32000, totalPrice: 32000 }
        ]
      },
      {
        id: '11111111-1111-1111-1111-111111110005',
        tenantId: '00000000-0000-0000-0000-000000000001',
        invoiceNumber: 'INV-2026-00005',
        issueDate: '2026-10-24T16:00:00Z',
        dueDate: '2026-11-24T16:00:00Z',
        currency: 'TND',
        subtotal: 18000,
        taxRatePct: 19,
        taxAmount: 3420,
        totalAmount: 21420,
        remainingBalance: 17850,
        creditNotesApplied: 3570,
        status: 'PartiallyRefunded',
        customerName: 'Sousse MedTech SARL',
        customerEmail: 'comptabilite@sousse-medtech.tn',
        customerCountry: 'TN',
        customerVatNumber: '1234567A/P/M/000',
        merchantCountry: 'TN',
        lineItems: [
          { id: '1', description: 'Clinical Telehealth & Inbound SMS Dispatch', quantity: 1, unitPrice: 18000, totalPrice: 18000 }
        ]
      },
      {
        id: '11111111-1111-1111-1111-111111110006',
        tenantId: '00000000-0000-0000-0000-000000000001',
        invoiceNumber: 'INV-2026-00006',
        issueDate: '2026-10-25T17:45:00Z',
        dueDate: '2026-11-25T17:45:00Z',
        currency: 'USD',
        subtotal: 24000,
        taxRatePct: 0,
        taxAmount: 0,
        totalAmount: 24000,
        remainingBalance: 0,
        creditNotesApplied: 0,
        status: 'Paid',
        customerName: 'Anthropic PBC',
        customerEmail: 'finance@anthropic.com',
        customerCountry: 'US',
        customerVatNumber: null,
        merchantCountry: 'FR',
        lineItems: [
          { id: '1', description: 'Multi-Model AI Workflow Mesh Integration', quantity: 2, unitPrice: 12000, totalPrice: 24000 }
        ]
      },
      {
        id: '11111111-1111-1111-1111-111111110007',
        tenantId: '00000000-0000-0000-0000-000000000001',
        invoiceNumber: 'INV-2026-00007',
        issueDate: '2026-10-27T10:00:00Z',
        dueDate: '2026-11-27T10:00:00Z',
        currency: 'GBP',
        subtotal: 9500,
        taxRatePct: 20,
        taxAmount: 1900,
        totalAmount: 11400,
        remainingBalance: 11400,
        creditNotesApplied: 0,
        status: 'Issued',
        customerName: 'London FinTech Partners LLP',
        customerEmail: 'accounts@londonfintech.co.uk',
        customerCountry: 'GB',
        customerVatNumber: 'GB987654321',
        merchantCountry: 'GB',
        lineItems: [
          { id: '1', description: 'FCA Compliant Voice Call Recording & Transcription', quantity: 1, unitPrice: 9500, totalPrice: 9500 }
        ]
      },
      {
        id: '11111111-1111-1111-1111-111111110008',
        tenantId: '00000000-0000-0000-0000-000000000001',
        invoiceNumber: 'INV-2026-00008',
        issueDate: '2026-10-28T12:00:00Z',
        dueDate: '2026-11-28T12:00:00Z',
        currency: 'EUR',
        subtotal: 5000,
        taxRatePct: 20,
        taxAmount: 1000,
        totalAmount: 6000,
        remainingBalance: 0,
        creditNotesApplied: 6000,
        status: 'Reversed',
        customerName: 'Atelier Parisien SARL',
        customerEmail: 'admin@atelier-paris.fr',
        customerCountry: 'FR',
        customerVatNumber: 'FR99988877766',
        merchantCountry: 'FR',
        lineItems: [
          { id: '1', description: 'Custom Workflow Proof of Concept', quantity: 1, unitPrice: 5000, totalPrice: 5000 }
        ]
      }
    ]);

    // 4. Pre-seed Credit Notes
    this.creditNotes.set([
      {
        id: '22222222-2222-2222-2222-222222220001',
        tenantId: '00000000-0000-0000-0000-000000000001',
        creditNoteNumber: 'CN-2026-00001',
        originalInvoiceId: '11111111-1111-1111-1111-111111110005',
        originalInvoiceNumber: 'INV-2026-00005',
        issueDate: '2026-10-26T15:00:00Z',
        reason: 'SLA delivery latency credit on monthly cloud nodes',
        refundAmount: 3000,
        taxAdjustment: 570,
        totalCredit: 3570,
        status: 'AppliedToLedger'
      },
      {
        id: '22222222-2222-2222-2222-222222220002',
        tenantId: '00000000-0000-0000-0000-000000000001',
        creditNoteNumber: 'CN-2026-00002',
        originalInvoiceId: '11111111-1111-1111-1111-111111110008',
        originalInvoiceNumber: 'INV-2026-00008',
        issueDate: '2026-10-29T10:30:00Z',
        reason: 'Full contract cancellation and project rescission',
        refundAmount: 5000,
        taxAdjustment: 1000,
        totalCredit: 6000,
        status: 'AppliedToLedger'
      }
    ]);

    // 5. Pre-seed Tax Summaries
    this.taxSummaries.set([
      { jurisdiction: 'SA', countryName: 'Saudi Arabia', currency: 'SAR', invoicesCount: 1, grossSubtotal: 45000, totalTaxCollected: 6750, totalCreditsAdjusted: 0, netTaxLiability: 6750, flag: '🇸🇦' },
      { jurisdiction: 'TN', countryName: 'Tunisia', currency: 'TND', invoicesCount: 1, grossSubtotal: 18000, totalTaxCollected: 3420, totalCreditsAdjusted: 570, netTaxLiability: 2850, flag: '🇹🇳' },
      { jurisdiction: 'GB', countryName: 'United Kingdom', currency: 'GBP', invoicesCount: 1, grossSubtotal: 9500, totalTaxCollected: 1900, totalCreditsAdjusted: 0, netTaxLiability: 1900, flag: '🇬🇧' },
      { jurisdiction: 'AE', countryName: 'United Arab Emirates', currency: 'AED', invoicesCount: 1, grossSubtotal: 32000, totalTaxCollected: 1600, totalCreditsAdjusted: 0, netTaxLiability: 1600, flag: '🇦🇪' },
      { jurisdiction: 'FR', countryName: 'France', currency: 'EUR', invoicesCount: 2, grossSubtotal: 17500, totalTaxCollected: 1000, totalCreditsAdjusted: 1000, netTaxLiability: 0, flag: '🇫🇷' },
      { jurisdiction: 'DE', countryName: 'Germany', currency: 'EUR', invoicesCount: 1, grossSubtotal: 8400, totalTaxCollected: 0, totalCreditsAdjusted: 0, netTaxLiability: 0, flag: '🇩🇪' },
      { jurisdiction: 'US', countryName: 'United States', currency: 'USD', invoicesCount: 1, grossSubtotal: 24000, totalTaxCollected: 0, totalCreditsAdjusted: 0, netTaxLiability: 0, flag: '🇺🇸' }
    ]);

    // Attempt to load live backend data if available
    this.http.get<CommercialInvoiceDto[]>('/api/v1/billing/tax-invoicing/invoices').subscribe({
      next: (data) => {
        if (data && data.length > 0) this.invoices.set(data);
      },
      error: () => { /* keep pre-seeded data */ }
    });

    this.http.get<CreditNoteDto[]>('/api/v1/billing/tax-invoicing/credit-notes').subscribe({
      next: (data) => {
        if (data && data.length > 0) this.creditNotes.set(data);
      },
      error: () => { /* keep pre-seeded data */ }
    });
  }

  getCountryFlag(code: string): string {
    const j = this.jurisdictions().find(x => x.countryCode.toUpperCase() === (code || '').toUpperCase());
    return j?.flag || '🌐';
  }

  getCountryName(code: string): string {
    const j = this.jurisdictions().find(x => x.countryCode.toUpperCase() === (code || '').toUpperCase());
    return j?.countryName || code;
  }

  getCurrencySpot(code: string): CurrencySpotRateDto | undefined {
    return this.currencies().find(c => c.code.toUpperCase() === (code || '').toUpperCase());
  }

  getEffectiveRate(code: string): number {
    const c = this.getCurrencySpot(code);
    if (!c) return 1.0;
    return parseFloat((c.spotRateToUsd * (1 + c.spreadMarginPct / 100)).toFixed(4));
  }

  calculatedFxConversion(): string {
    const fromCur = this.getCurrencySpot(this.fxFrom);
    const toCur = this.getCurrencySpot(this.fxTo);
    if (!fromCur || !toCur) return (this.fxAmount || 0).toFixed(2);

    const fromEffective = fromCur.spotRateToUsd * (1 + fromCur.spreadMarginPct / 100);
    const toEffective = toCur.spotRateToUsd * (1 + toCur.spreadMarginPct / 100);

    const amountInUsd = this.fxFrom === 'USD' ? this.fxAmount : this.fxAmount / fromEffective;
    const finalAmount = this.fxTo === 'USD' ? amountInUsd : amountInUsd * toEffective;
    return finalAmount.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  runSandboxEvaluation() {
    const m = this.sandboxMerchantCountry;
    const c = this.sandboxCustomerCountry;
    const isB2B = this.sandboxIsB2B;
    const vat = this.sandboxVatNumber;
    const subtotal = this.sandboxSubtotal;

    // Check VIES format
    const isValidVies = this.validateViesFormatLocal(c, vat);
    this.sandboxViesValid.set(isValidVies);

    const mRule = this.jurisdictions().find(j => j.countryCode === m);
    const cRule = this.jurisdictions().find(j => j.countryCode === c);

    if (mRule?.isEuMember && cRule?.isEuMember) {
      if (m === c) {
        const rate = mRule.standardRatePct;
        this.sandboxResult.set({
          applicableTaxRatePct: rate,
          taxAmount: parseFloat((subtotal * rate / 100).toFixed(2)),
          taxScheme: `Domestic EU ${mRule.taxName}`,
          isReverseCharge: false,
          viesValid: isValidVies
        });
      } else {
        if (isB2B && isValidVies) {
          this.sandboxResult.set({
            applicableTaxRatePct: 0,
            taxAmount: 0,
            taxScheme: 'EU Cross-Border B2B Reverse Charge',
            isReverseCharge: true,
            exemptionReason: 'Reverse charge: Art. 196 EU VAT Directive',
            viesValid: true
          });
        } else {
          const rate = cRule.standardRatePct;
          this.sandboxResult.set({
            applicableTaxRatePct: rate,
            taxAmount: parseFloat((subtotal * rate / 100).toFixed(2)),
            taxScheme: `EU B2C Destination ${cRule.taxName}`,
            isReverseCharge: false,
            viesValid: isValidVies
          });
        }
      }
    } else if (c === 'SA') {
      this.sandboxResult.set({
        applicableTaxRatePct: 15.0,
        taxAmount: parseFloat((subtotal * 0.15).toFixed(2)),
        taxScheme: 'KSA ZATCA VAT',
        isReverseCharge: false,
        viesValid: false
      });
    } else if (c === 'AE') {
      this.sandboxResult.set({
        applicableTaxRatePct: 5.0,
        taxAmount: parseFloat((subtotal * 0.05).toFixed(2)),
        taxScheme: 'UAE FTA VAT',
        isReverseCharge: false,
        viesValid: false
      });
    } else if (c === 'TN') {
      const rate = m === 'TN' ? 19.0 : 0.0;
      this.sandboxResult.set({
        applicableTaxRatePct: rate,
        taxAmount: parseFloat((subtotal * rate / 100).toFixed(2)),
        taxScheme: m === 'TN' ? 'Tunisian Domestic VAT' : 'Zero-Rated International Export',
        isReverseCharge: false,
        viesValid: false
      });
    } else {
      this.sandboxResult.set({
        applicableTaxRatePct: 0.0,
        taxAmount: 0.0,
        taxScheme: 'Zero-Rated International Export',
        isReverseCharge: false,
        exemptionReason: 'Export of services outside tax jurisdiction',
        viesValid: false
      });
    }
  }

  private validateViesFormatLocal(countryCode: string, vatNumber?: string | null): boolean {
    if (!vatNumber || !countryCode) return false;
    let clean = vatNumber.trim().toUpperCase().replace(/[\s.-]/g, '');
    const code = countryCode.toUpperCase();
    if (!clean.startsWith(code)) clean = code + clean;

    const regexMap: Record<string, RegExp> = {
      FR: /^FR[A-Z0-9]{2}\d{9}$/i,
      DE: /^DE\d{9}$/i,
      IT: /^IT\d{11}$/i,
      ES: /^ES[A-Z0-9]\d{7}[A-Z0-9]$/i,
      NL: /^NL\d{9}B\d{2}$/i,
      BE: /^BE[01]\d{9}$/i,
      AT: /^ATU\d{8}$/i,
      IE: /^IE\d{7}[A-W][A-I]?$|^IE[0-9A-Z+*]{8,9}$/i,
      SA: /^3\d{13}3$/ // 15 digits starting and ending with 3
    };

    const re = regexMap[code];
    return re ? re.test(clean) : clean.length >= 8;
  }

  openInvoiceDetail(inv: CommercialInvoiceDto) {
    this.selectedInvoiceForView.set(inv);
  }

  openCreditNoteDialog(inv: CommercialInvoiceDto) {
    this.invoiceForCreditNote.set(inv);
    this.creditNoteAmount = inv.remainingBalance;
  }

  setFullCreditAmount() {
    if (this.invoiceForCreditNote()) {
      this.creditNoteAmount = this.invoiceForCreditNote()!.remainingBalance;
    }
  }

  calculateTaxAdjustmentPreview(): number {
    const inv = this.invoiceForCreditNote();
    if (!inv || inv.taxRatePct <= 0 || this.creditNoteAmount <= 0) return 0;
    return parseFloat((this.creditNoteAmount * (inv.taxRatePct / (100 + inv.taxRatePct))).toFixed(2));
  }

  confirmCreditNote() {
    const inv = this.invoiceForCreditNote();
    if (!inv) return;

    const refund = this.creditNoteAmount;
    if (refund <= 0 || refund > inv.remainingBalance) return;

    const taxAdjustment = this.calculateTaxAdjustmentPreview();
    const baseRefund = parseFloat((refund - taxAdjustment).toFixed(2));

    const newSeq = this.creditNotes().length + 1;
    const cnNumber = `CN-2026-${String(newSeq).padStart(5, '0')}`;

    const newCreditNote: CreditNoteDto = {
      id: `cn-${Date.now()}`,
      tenantId: inv.tenantId,
      creditNoteNumber: cnNumber,
      originalInvoiceId: inv.id,
      originalInvoiceNumber: inv.invoiceNumber,
      issueDate: new Date().toISOString(),
      reason: this.creditNoteReason,
      refundAmount: baseRefund,
      taxAdjustment: taxAdjustment,
      totalCredit: refund,
      status: 'AppliedToLedger'
    };

    // Update Invoice
    this.invoices.update(list => list.map(item => {
      if (item.id === inv.id) {
        const remaining = Math.max(0, item.remainingBalance - refund);
        const isReversed = remaining === 0;
        return {
          ...item,
          remainingBalance: remaining,
          creditNotesApplied: item.creditNotesApplied + refund,
          status: isReversed ? 'Reversed' : 'PartiallyRefunded'
        };
      }
      return item;
    }));

    this.creditNotes.update(list => [newCreditNote, ...list]);
    this.invoiceForCreditNote.set(null);
  }

  settleInvoice(inv: CommercialInvoiceDto) {
    this.invoices.update(list => list.map(item => {
      if (item.id === inv.id) {
        return {
          ...item,
          remainingBalance: 0,
          status: 'Paid'
        };
      }
      return item;
    }));
  }

  recalcNewInvoiceTax() {
    const c = this.newInvoiceCountry;
    const vat = this.newInvoiceVat;
    const isValidVies = this.validateViesFormatLocal(c, vat);

    if (c === 'FR') {
      this.newInvoiceTaxRate = 20.0;
    } else if (['DE', 'IT', 'ES', 'NL', 'BE', 'AT', 'IE'].includes(c)) {
      this.newInvoiceTaxRate = isValidVies ? 0.0 : 19.0;
    } else if (c === 'SA') {
      this.newInvoiceTaxRate = 15.0;
    } else if (c === 'AE') {
      this.newInvoiceTaxRate = 5.0;
    } else if (c === 'TN') {
      this.newInvoiceTaxRate = 0.0;
    } else {
      this.newInvoiceTaxRate = 0.0;
    }
  }

  submitNewInvoice() {
    if (!this.newInvoiceCustomer) return;

    const subtotal = this.newInvoiceQuantity * this.newInvoiceUnitPrice;
    const taxAmount = parseFloat((subtotal * (this.newInvoiceTaxRate / 100)).toFixed(2));
    const total = subtotal + taxAmount;
    const seq = this.invoices().length + 1;
    const invNumber = `INV-2026-${String(seq).padStart(5, '0')}`;

    const newInv: CommercialInvoiceDto = {
      id: `inv-${Date.now()}`,
      tenantId: '00000000-0000-0000-0000-000000000001',
      invoiceNumber: invNumber,
      issueDate: new Date().toISOString(),
      dueDate: new Date(Date.now() + 86400000 * 30).toISOString(),
      currency: this.newInvoiceCurrency,
      subtotal: subtotal,
      taxRatePct: this.newInvoiceTaxRate,
      taxAmount: taxAmount,
      totalAmount: total,
      remainingBalance: total,
      creditNotesApplied: 0,
      status: 'Issued',
      customerName: this.newInvoiceCustomer,
      customerEmail: this.newInvoiceEmail,
      customerCountry: this.newInvoiceCountry,
      customerVatNumber: this.newInvoiceVat || null,
      merchantCountry: 'FR',
      lineItems: [
        {
          id: `line-${Date.now()}`,
          description: this.newInvoiceItemDesc,
          quantity: this.newInvoiceQuantity,
          unitPrice: this.newInvoiceUnitPrice,
          totalPrice: subtotal
        }
      ]
    };

    if (this.newInvoiceCountry === 'SA') {
      newInv.zatcaQrBase64 = 'AQpTR3Jvd2luZyBTYXVkaSBBcmFiaWEgTHRkAg8zMTAxMjM0NTY3MDAwMDMDGDIwMjYtMTAtMzBUMTU6MDA6MDBaBAYyMzAwMC4wMAYGMzAwMC4wMA==';
    }

    newInv.facturXXmlPayload = `<rsm:CrossIndustryInvoice xmlns:rsm="urn:un:unece:uncefact:data:standard:CrossIndustryInvoice:100"><rsm:ExchangedDocument><ram:ID>${newInv.invoiceNumber}</ram:ID></rsm:ExchangedDocument></rsm:CrossIndustryInvoice>`;

    this.invoices.update(list => [newInv, ...list]);
    this.openNewInvoiceModal.set(false);
    this.newInvoiceCustomer = '';
  }

  printInvoice() {
    window.print();
  }

  copyXmlToClipboard(xml: string) {
    if (navigator?.clipboard) {
      navigator.clipboard.writeText(xml);
    }
  }
}
