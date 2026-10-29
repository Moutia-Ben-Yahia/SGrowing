import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface VolumeTierUi {
  minQuantity: number;
  maxQuantity: number | null;
  unitPrice: number;
}

export interface PriceBookEntryUi {
  id: string;
  productSku: string;
  productName: string;
  basePrice: number;
  costPrice: number;
  volumeTiers: VolumeTierUi[];
}

export interface PriceBookUi {
  id: string;
  name: string;
  currency: string;
  isDefault: boolean;
  priceBookEntries: PriceBookEntryUi[];
}

export interface CpqLineItemUi {
  id?: string;
  productSku: string;
  productName: string;
  unitPrice: number;
  unitCost: number;
  quantity: number;
  discountPercentage: number;
  lineTotal: number;
  lineMarginPercentage: number;
}

export interface CpqQuoteUi {
  id: string;
  tenantId: string;
  quoteNumber: string;
  customerId: string;
  customerName: string;
  customerEmail?: string;
  dealId?: string;
  status: 'Draft' | 'PendingApproval' | 'SentToClient' | 'Accepted' | 'Rejected' | 'ConvertedToOrder';
  currency: string;
  subtotal: number;
  discountTotal: number;
  taxTotal: number;
  grandTotal: number;
  overallMarginPercentage: number;
  requiresManagerApproval: boolean;
  approvedBy?: string;
  approvedAtUtc?: string;
  approvalNotes?: string;
  lineItems: CpqLineItemUi[];
  expirationDateUtc: string;
  termsAndConditions: string;
  checksumSha256: string;
  convertedOrderId?: string;
  createdAtUtc: string;
}

export interface PdfPreviewUi {
  quoteId: string;
  quoteNumber: string;
  companyName: string;
  companyTaxId: string;
  customerName: string;
  customerEmail: string;
  currency: string;
  lineItems: CpqLineItemUi[];
  subtotal: number;
  discountTotal: number;
  taxTotal: number;
  grandTotal: number;
  overallMarginPercentage: number;
  requiresManagerApproval: boolean;
  approvedBy?: string;
  expirationDateUtc: string;
  daysUntilExpiration: number;
  termsAndConditions: string;
  checksumSha256: string;
  formattedHtmlPreview: string;
}

@Component({
  selector: 'app-cpq-quotation-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <!-- Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/90 flex flex-col justify-between p-4 shrink-0">
        <div>
          <div class="flex items-center gap-3 mb-6">
            <div class="h-9 w-9 rounded-xl bg-gradient-to-tr from-blue-600 to-emerald-500 flex items-center justify-center font-black text-white shadow-lg shadow-blue-500/20">
              $
            </div>
            <div>
              <h1 class="text-sm font-bold tracking-wide text-white">SGrowing CPQ</h1>
              <p class="text-[11px] text-emerald-400 font-medium">Margin-Guarded Engine</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-[10px] font-bold text-slate-500 uppercase tracking-wider px-2">Sales Modules</span>
            <div class="space-y-1 mt-1">
              <a routerLink="/" class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs text-slate-400 hover:text-white hover:bg-slate-800/60 transition">
                <span>← CRM & Inbox</span>
              </a>
              <button class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs bg-blue-600/20 text-blue-300 border border-blue-500/30 font-semibold shadow-inner">
                <span>💼 Quotation Studio</span>
              </button>
              <a routerLink="/commerce/cart-reservations" class="w-full flex items-center gap-2.5 px-3 py-2 rounded-lg text-xs text-slate-400 hover:text-white hover:bg-slate-800/60 transition">
                <span>🛒 Cart Reservations</span>
              </a>
            </div>
          </div>

          <!-- Active Margin Policy Indicator -->
          <div class="p-3 rounded-xl bg-slate-950/60 border border-slate-800/80 mb-4 text-xs space-y-2">
            <div class="flex items-center justify-between text-[11px] font-semibold text-slate-400">
              <span>Guard Policy</span>
              <span class="text-emerald-400">Enforced</span>
            </div>
            <div class="flex justify-between text-slate-300">
              <span class="text-slate-500">Min Margin:</span>
              <span class="font-mono text-amber-400 font-semibold">25.0%</span>
            </div>
            <div class="flex justify-between text-slate-300">
              <span class="text-slate-500">Max Discount:</span>
              <span class="font-mono text-blue-400 font-semibold">20.0%</span>
            </div>
            <div class="flex justify-between text-slate-300">
              <span class="text-slate-500">Tamper Seal:</span>
              <span class="font-mono text-emerald-400 font-semibold">SHA-256</span>
            </div>
          </div>
        </div>

        <div class="border-t border-slate-800 pt-3 text-[11px] text-slate-500 flex items-center justify-between">
          <span>CPQ v2.4 Active</span>
          <span class="h-2 w-2 rounded-full bg-emerald-500 animate-pulse"></span>
        </div>
      </aside>

      <!-- Main Studio Body -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top App Bar -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0 backdrop-blur-sm">
          <div class="flex items-center gap-3">
            <span class="text-lg font-bold text-white flex items-center gap-2">
              Configure, Price, Quote (CPQ) Studio
            </span>
            <span class="bg-blue-500/10 text-blue-400 text-[11px] font-medium px-2.5 py-0.5 rounded-full border border-blue-500/20">
              Horizon 18 • Enterprise
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button 
              (click)="createNewQuoteDraft()" 
              class="px-3.5 py-1.5 bg-blue-600 hover:bg-blue-500 text-white rounded-lg text-xs font-semibold shadow-md shadow-blue-600/30 transition flex items-center gap-1.5">
              <span>+ New Quote</span>
            </button>
            <button 
              (click)="loadQuotes()" 
              class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 border border-slate-700 rounded-lg text-xs font-medium transition">
              Refresh
            </button>
          </div>
        </header>

        <!-- Main Workspace Grid -->
        <div class="p-6 space-y-6 max-w-7xl w-full mx-auto">
          <!-- TOP ROW: Visual Quote Editor & Real-Time Margin Meter -->
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- Left 2 Cols: Quote Builder Form & Line Items -->
            <div class="lg:col-span-2 bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-6">
              <div class="flex items-center justify-between border-b border-slate-800/80 pb-4">
                <div>
                  <h2 class="text-base font-bold text-white flex items-center gap-2">
                    <span>📝 Quote Configuration</span>
                    @if (currentQuote()) {
                      <span class="font-mono text-xs bg-slate-800 text-blue-400 px-2 py-0.5 rounded border border-slate-700">
                        {{ currentQuote()?.quoteNumber }}
                      </span>
                    }
                  </h2>
                  <p class="text-xs text-slate-400 mt-0.5">Configure items, customer info, and evaluate automated volume break pricing</p>
                </div>
                <div class="flex items-center gap-2">
                  <span class="text-xs text-slate-400">Price Book:</span>
                  <select 
                    [ngModel]="selectedPriceBookId()" 
                    (ngModelChange)="onPriceBookChanged($event)"
                    class="bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1 text-xs text-slate-200 focus:outline-none focus:border-blue-500">
                    @for (pb of priceBooks(); track pb.id) {
                      <option [value]="pb.id">{{ pb.name }} ({{ pb.currency }})</option>
                    }
                  </select>
                </div>
              </div>

              <!-- Customer & Quote Metadata -->
              <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div>
                  <label class="block text-[11px] font-medium text-slate-400 mb-1">Customer / Account</label>
                  <input 
                    type="text" 
                    [(ngModel)]="customerName"
                    placeholder="e.g. Acme Corporation" 
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-blue-500" />
                </div>
                <div>
                  <label class="block text-[11px] font-medium text-slate-400 mb-1">Contact Email</label>
                  <input 
                    type="email" 
                    [(ngModel)]="customerEmail"
                    placeholder="procurement@acme.com" 
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-blue-500" />
                </div>
                <div>
                  <label class="block text-[11px] font-medium text-slate-400 mb-1">Terms & Conditions</label>
                  <input 
                    type="text" 
                    [(ngModel)]="terms"
                    placeholder="Net 30 Days" 
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-blue-500" />
                </div>
              </div>

              <!-- Line Items Table -->
              <div class="space-y-3">
                <div class="flex items-center justify-between">
                  <h3 class="text-xs font-semibold text-slate-300 uppercase tracking-wider">Line Items & Volume Breaks</h3>
                  <button 
                    (click)="addLineItem()" 
                    class="px-2.5 py-1 bg-blue-600/20 hover:bg-blue-600/30 text-blue-400 border border-blue-500/30 rounded-lg text-xs font-medium transition flex items-center gap-1">
                    <span>+ Add Product Line</span>
                  </button>
                </div>

                <div class="overflow-x-auto border border-slate-800/80 rounded-xl">
                  <table class="w-full text-left text-xs">
                    <thead class="bg-slate-950/80 text-slate-400 uppercase text-[10px] font-semibold border-b border-slate-800">
                      <tr>
                        <th class="px-3 py-2.5">SKU / Product</th>
                        <th class="px-3 py-2.5 w-24 text-center">Quantity</th>
                        <th class="px-3 py-2.5 w-28 text-right">Unit Price</th>
                        <th class="px-3 py-2.5 w-20 text-right">Disc%</th>
                        <th class="px-3 py-2.5 w-28 text-right">Net Total</th>
                        <th class="px-3 py-2.5 w-24 text-center">Margin%</th>
                        <th class="px-2 py-2.5 w-10 text-center"></th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800/60 bg-slate-950/40">
                      @for (item of draftLines(); track $index) {
                        <tr class="hover:bg-slate-800/30 transition">
                          <!-- SKU Selection -->
                          <td class="px-3 py-2.5">
                            <select 
                              [ngModel]="item.productSku" 
                              (ngModelChange)="onLineSkuChanged($index, $event)"
                              class="w-full bg-slate-900 border border-slate-700 rounded px-2 py-1 text-xs text-slate-100 focus:outline-none focus:border-blue-500">
                              @for (entry of currentPriceBookEntries(); track entry.productSku) {
                                <option [value]="entry.productSku">{{ entry.productName }} ({{ entry.productSku }})</option>
                              }
                            </select>
                          </td>
                          <!-- Quantity with Volume Break Info -->
                          <td class="px-3 py-2.5 text-center">
                            <input 
                              type="number" 
                              min="1" 
                              [ngModel]="item.quantity" 
                              (ngModelChange)="onQuantityChanged($index, $event)"
                              class="w-16 bg-slate-900 border border-slate-700 rounded px-2 py-1 text-xs text-center text-white focus:outline-none focus:border-blue-500" />
                          </td>
                          <!-- Evaluated Unit Price -->
                          <td class="px-3 py-2.5 text-right font-mono text-slate-200">
                            {{ item.unitPrice | number:'1.2-2' }} {{ currentCurrency() }}
                          </td>
                          <!-- Discount % -->
                          <td class="px-3 py-2.5 text-right">
                            <input 
                              type="number" 
                              min="0" 
                              max="100" 
                              [ngModel]="item.discountPercentage" 
                              (ngModelChange)="onDiscountChanged($index, $event)"
                              class="w-14 bg-slate-900 border border-slate-700 rounded px-1.5 py-1 text-xs text-right text-amber-300 focus:outline-none focus:border-blue-500" />
                          </td>
                          <!-- Net Line Total -->
                          <td class="px-3 py-2.5 text-right font-mono font-semibold text-white">
                            {{ item.lineTotal | number:'1.2-2' }} {{ currentCurrency() }}
                          </td>
                          <!-- Line Margin -->
                          <td class="px-3 py-2.5 text-center">
                            <span 
                              [class]="item.lineMarginPercentage < 25 ? 'text-rose-400 bg-rose-500/10 border-rose-500/30' : 'text-emerald-400 bg-emerald-500/10 border-emerald-500/30'"
                              class="px-2 py-0.5 rounded text-[11px] font-mono border font-medium">
                              {{ item.lineMarginPercentage | number:'1.1-1' }}%
                            </span>
                          </td>
                          <!-- Remove Line -->
                          <td class="px-2 py-2.5 text-center">
                            <button 
                              (click)="removeLineItem($index)" 
                              class="text-slate-500 hover:text-rose-400 transition text-sm">
                              ✕
                            </button>
                          </td>
                        </tr>
                      }
                      @if (draftLines().length === 0) {
                        <tr>
                          <td colspan="7" class="px-4 py-8 text-center text-xs text-slate-500">
                            No product lines added yet. Click "+ Add Product Line" to begin configuring quote.
                          </td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>

              <!-- Action Bar -->
              <div class="flex items-center justify-between pt-2">
                <div class="flex items-center gap-2">
                  <button 
                    (click)="saveOrUpdateQuote()" 
                    [disabled]="draftLines().length === 0 || isSaving()"
                    class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded-xl text-xs font-semibold shadow-lg shadow-emerald-600/30 transition flex items-center gap-1.5">
                    <span>💾 Save & Calculate Quote</span>
                  </button>
                  @if (currentQuote()) {
                    <button 
                      (click)="openPdfPreview()" 
                      class="px-3.5 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-xl text-xs font-medium transition flex items-center gap-1.5">
                      <span>📄 View Sealed PDF</span>
                    </button>
                  }
                </div>

                <!-- Dispatch to Client -->
                @if (currentQuote()) {
                  <div>
                    <button 
                      (click)="sendQuoteToClient()" 
                      [disabled]="currentQuote()?.requiresManagerApproval"
                      [class]="currentQuote()?.requiresManagerApproval ? 'bg-slate-800 text-slate-500 cursor-not-allowed border border-slate-800' : 'bg-blue-600 hover:bg-blue-500 text-white shadow-lg shadow-blue-600/30'"
                      class="px-4 py-2 rounded-xl text-xs font-semibold transition flex items-center gap-1.5">
                      <span>🚀 Dispatch Quote to Client</span>
                    </button>
                  </div>
                }
              </div>
            </div>

            <!-- Right 1 Col: Real-Time Gross Margin Meter & Manager Approval Panel -->
            <div class="space-y-6">
              <!-- Margin Meter Card -->
              <div class="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-5">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-bold text-white flex items-center gap-2">
                    <span>📊 Real-Time Margin Meter</span>
                  </h3>
                  <span 
                    [class]="computedMarginPct() < 25 ? 'bg-rose-500/20 text-rose-400 border-rose-500/30' : 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30'"
                    class="text-[11px] font-mono px-2 py-0.5 rounded-full border font-semibold">
                    {{ computedMarginPct() | number:'1.1-1' }}%
                  </span>
                </div>

                <!-- Visual Meter Gauge Bar -->
                <div class="space-y-2">
                  <div class="h-4 w-full bg-slate-950 rounded-full overflow-hidden border border-slate-800 relative">
                    <!-- Policy Threshold Marker (25%) -->
                    <div class="absolute top-0 bottom-0 left-1/4 w-0.5 bg-amber-400 z-10" title="25% Policy Threshold"></div>
                    <!-- Fill Bar -->
                    <div 
                      [style.width.%]="clampedMargin()"
                      [class]="computedMarginPct() < 25 ? 'bg-gradient-to-r from-rose-600 to-amber-500' : 'bg-gradient-to-r from-emerald-500 to-blue-500'"
                      class="h-full transition-all duration-500">
                    </div>
                  </div>
                  <div class="flex justify-between text-[10px] text-slate-500 font-mono">
                    <span>0%</span>
                    <span class="text-amber-400">25% Policy Floor</span>
                    <span>100%</span>
                  </div>
                </div>

                <!-- Margin Policy Guard Warning or Clean Signal -->
                @if (computedMarginPct() < 25 || discountExceedsPolicy()) {
                  <div class="p-3.5 rounded-xl bg-rose-500/10 border border-rose-500/30 space-y-2">
                    <div class="flex items-center gap-2 text-xs font-bold text-rose-400">
                      <span>⚠️ Manager Approval Required</span>
                    </div>
                    <p class="text-[11px] text-rose-300 leading-relaxed">
                      Aggregate gross margin ({{ computedMarginPct() | number:'1.1-1' }}%) falls below the 25% policy threshold. 
                      Quote sending is locked until sales manager sign-off.
                    </p>
                    @if (currentQuote() && currentQuote()?.status !== 'PendingApproval' && !currentQuote()?.approvedBy) {
                      <button 
                        (click)="requestManagerApproval()" 
                        class="w-full py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded-lg text-xs font-semibold shadow transition">
                        Submit to Manager Queue
                      </button>
                    }
                  </div>
                } @else {
                  <div class="p-3 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-xs text-emerald-300 flex items-center gap-2">
                    <span>✅ Healthy Gross Margin. Complies with auto-approval policy.</span>
                  </div>
                }

                <!-- Financial Summary Box -->
                <div class="p-4 rounded-xl bg-slate-950/80 border border-slate-800/80 space-y-2.5 text-xs">
                  <div class="flex justify-between text-slate-400">
                    <span>Gross Subtotal:</span>
                    <span class="font-mono text-slate-200">{{ computedSubtotal() | number:'1.2-2' }} {{ currentCurrency() }}</span>
                  </div>
                  <div class="flex justify-between text-rose-400">
                    <span>Total Discount:</span>
                    <span class="font-mono">-{{ computedDiscountTotal() | number:'1.2-2' }} {{ currentCurrency() }}</span>
                  </div>
                  <div class="flex justify-between text-slate-400">
                    <span>Estimated COGS:</span>
                    <span class="font-mono text-slate-300">{{ computedCogsTotal() | number:'1.2-2' }} {{ currentCurrency() }}</span>
                  </div>
                  <div class="border-t border-slate-800 pt-2 flex justify-between font-bold text-sm text-blue-400">
                    <span>Grand Total:</span>
                    <span class="font-mono text-base text-white">{{ computedGrandTotal() | number:'1.2-2' }} {{ currentCurrency() }}</span>
                  </div>
                </div>
              </div>

              <!-- Manager Approval Sign-Off Panel -->
              @if (currentQuote()?.requiresManagerApproval || currentQuote()?.status === 'PendingApproval') {
                <div class="bg-gradient-to-b from-slate-900 to-slate-950 border border-amber-500/30 rounded-2xl p-5 shadow-xl space-y-4">
                  <div class="flex items-center gap-2 text-xs font-bold text-amber-400">
                    <span>🛡️ Manager Approval Override</span>
                  </div>
                  <div class="space-y-2">
                    <label class="block text-[11px] text-slate-400">Manager Email / Name</label>
                    <input 
                      type="text" 
                      [(ngModel)]="managerSignoffName" 
                      placeholder="sarah.vp@sgrowing.io" 
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-amber-500" />
                  </div>
                  <div class="space-y-2">
                    <label class="block text-[11px] text-slate-400">Approval Justification</label>
                    <input 
                      type="text" 
                      [(ngModel)]="managerSignoffNotes" 
                      placeholder="Approved for enterprise anchor client" 
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-amber-500" />
                  </div>
                  <div class="flex gap-2">
                    <button 
                      (click)="approveQuoteAsManager()" 
                      class="flex-1 py-2 bg-amber-500 hover:bg-amber-400 text-slate-950 font-bold rounded-xl text-xs shadow-lg shadow-amber-500/20 transition">
                      Sign-Off & Approve
                    </button>
                    <button 
                      (click)="rejectQuoteAsManager()" 
                      class="px-3 py-2 bg-slate-800 hover:bg-rose-900/40 text-rose-300 border border-slate-700 rounded-xl text-xs font-medium transition">
                      Reject
                    </button>
                  </div>
                </div>
              }
            </div>
          </div>

          <!-- BOTTOM SECTION: Recent Quotes Table -->
          <div class="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
            <div class="flex items-center justify-between">
              <div>
                <h3 class="text-sm font-bold text-white flex items-center gap-2">
                  <span>📑 Commercial Quotes Repository</span>
                </h3>
                <p class="text-xs text-slate-400 mt-0.5">Track quote lifecycle, manager reviews, customer acceptance, and order conversions</p>
              </div>
              <div class="flex items-center gap-2">
                <span class="text-xs text-slate-400">Filter Status:</span>
                <select 
                  [ngModel]="selectedFilterStatus()" 
                  (ngModelChange)="onFilterStatusChanged($event)"
                  class="bg-slate-950 border border-slate-800 rounded-lg px-2.5 py-1 text-xs text-slate-200 focus:outline-none">
                  <option value="">All Statuses</option>
                  <option value="Draft">Draft</option>
                  <option value="PendingApproval">Pending Approval</option>
                  <option value="SentToClient">Sent to Client</option>
                  <option value="Accepted">Accepted</option>
                  <option value="ConvertedToOrder">Converted to Order</option>
                </select>
              </div>
            </div>

            <div class="overflow-x-auto border border-slate-800/80 rounded-xl">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-950/80 text-slate-400 uppercase text-[10px] font-semibold border-b border-slate-800">
                  <tr>
                    <th class="px-4 py-3">Quote Number</th>
                    <th class="px-4 py-3">Customer</th>
                    <th class="px-4 py-3 text-right">Grand Total</th>
                    <th class="px-4 py-3 text-center">Gross Margin</th>
                    <th class="px-4 py-3 text-center">Status</th>
                    <th class="px-4 py-3">Valid Until</th>
                    <th class="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 bg-slate-950/40">
                  @for (q of quotesList(); track q.id) {
                    <tr class="hover:bg-slate-800/30 transition">
                      <td class="px-4 py-3 font-mono font-medium text-blue-400">
                        {{ q.quoteNumber }}
                      </td>
                      <td class="px-4 py-3">
                        <div class="font-semibold text-white">{{ q.customerName }}</div>
                        <div class="text-[11px] text-slate-400">{{ q.customerEmail || 'No email' }}</div>
                      </td>
                      <td class="px-4 py-3 text-right font-mono font-bold text-white">
                        {{ q.grandTotal | number:'1.2-2' }} {{ q.currency }}
                      </td>
                      <td class="px-4 py-3 text-center">
                        <span 
                          [class]="q.overallMarginPercentage < 25 ? 'bg-rose-500/10 text-rose-400 border-rose-500/30' : 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30'"
                          class="px-2 py-0.5 rounded text-[11px] font-mono border font-medium">
                          {{ q.overallMarginPercentage | number:'1.1-1' }}%
                        </span>
                      </td>
                      <td class="px-4 py-3 text-center">
                        <span [ngClass]="getStatusBadgeClass(q.status)" class="px-2.5 py-0.5 rounded-full text-[10px] font-semibold border">
                          {{ q.status }}
                        </span>
                      </td>
                      <td class="px-4 py-3 text-slate-400 text-[11px]">
                        {{ q.expirationDateUtc | date:'yyyy-MM-dd' }}
                      </td>
                      <td class="px-4 py-3 text-right space-x-1.5">
                        <button 
                          (click)="loadQuoteIntoEditor(q)" 
                          class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-[11px] transition">
                          Edit
                        </button>
                        @if (q.status === 'SentToClient' || q.status === 'Draft') {
                          <button 
                            (click)="acceptAndConvertQuote(q)" 
                            class="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-[11px] font-semibold transition">
                            Accept & Order
                          </button>
                        }
                      </td>
                    </tr>
                  }
                  @if (quotesList().length === 0) {
                    <tr>
                      <td colspan="7" class="px-4 py-10 text-center text-xs text-slate-500">
                        No quotes found in this view.
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </main>

      <!-- PDF Preview Modal -->
      @if (showPdfModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
          <div class="bg-slate-900 border border-slate-700 rounded-2xl max-w-4xl w-full max-h-[90vh] flex flex-col shadow-2xl overflow-hidden">
            <div class="px-6 py-4 border-b border-slate-800 flex items-center justify-between bg-slate-950/60">
              <div class="flex items-center gap-2">
                <span class="text-sm font-bold text-white">Quotation PDF Preview</span>
                <span class="font-mono text-xs text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20">
                  Cryptographically Sealed
                </span>
              </div>
              <button (click)="showPdfModal.set(false)" class="text-slate-400 hover:text-white text-lg">✕</button>
            </div>
            
            <div class="p-6 overflow-y-auto flex-1 bg-slate-950">
              @if (pdfPreviewData()) {
                <div [innerHTML]="pdfPreviewData()?.formattedHtmlPreview"></div>
              }
            </div>

            <div class="px-6 py-3.5 border-t border-slate-800 bg-slate-900 flex items-center justify-between text-xs">
              <span class="font-mono text-[11px] text-slate-400">
                SHA-256: <span class="text-emerald-400">{{ pdfPreviewData()?.checksumSha256 }}</span>
              </span>
              <div class="flex gap-2">
                <button 
                  (click)="showPdfModal.set(false)" 
                  class="px-4 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg">
                  Close
                </button>
                @if (currentQuote() && currentQuote()?.status !== 'ConvertedToOrder') {
                  <button 
                    (click)="acceptAndConvertFromModal()" 
                    class="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold rounded-lg shadow-md shadow-emerald-600/30">
                    Accept & Convert to Order
                  </button>
                }
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class CpqQuotationStudioComponent implements OnInit {
  private http = inject(HttpClient);

  // Reactive State Signals
  priceBooks = signal<PriceBookUi[]>([]);
  selectedPriceBookId = signal<string>('');
  currentQuote = signal<CpqQuoteUi | null>(null);
  quotesList = signal<CpqQuoteUi[]>([]);
  draftLines = signal<CpqLineItemUi[]>([]);
  selectedFilterStatus = signal<string>('');

  // Form Inputs
  customerName = 'Acme Corporation';
  customerEmail = 'procurement@acme.com';
  terms = 'Payment due within 30 days. Standard enterprise SLA applies.';
  managerSignoffName = 'sarah.vp@sgrowing.io';
  managerSignoffNotes = 'Strategic account margin override approved';

  // Status & Modal Signals
  isSaving = signal<boolean>(false);
  showPdfModal = signal<boolean>(false);
  pdfPreviewData = signal<PdfPreviewUi | null>(null);

  // Computed state
  currentPriceBook = computed(() => {
    return this.priceBooks().find(p => p.id === this.selectedPriceBookId()) ?? this.priceBooks()[0];
  });

  currentPriceBookEntries = computed(() => {
    return this.currentPriceBook()?.priceBookEntries ?? [];
  });

  currentCurrency = computed(() => {
    return this.currentPriceBook()?.currency ?? 'USD';
  });

  computedSubtotal = computed(() => {
    return this.draftLines().reduce((acc, l) => acc + (l.quantity * l.unitPrice), 0);
  });

  computedDiscountTotal = computed(() => {
    return this.draftLines().reduce((acc, l) => acc + ((l.quantity * l.unitPrice) * (l.discountPercentage / 100)), 0);
  });

  computedGrandTotal = computed(() => {
    return this.draftLines().reduce((acc, l) => acc + l.lineTotal, 0);
  });

  computedCogsTotal = computed(() => {
    return this.draftLines().reduce((acc, l) => acc + (l.quantity * l.unitCost), 0);
  });

  computedMarginPct = computed(() => {
    const net = this.computedGrandTotal();
    const cogs = this.computedCogsTotal();
    if (net <= 0) return 0;
    return Math.round(((net - cogs) / net) * 1000) / 10;
  });

  clampedMargin = computed(() => {
    return Math.max(0, Math.min(100, this.computedMarginPct()));
  });

  discountExceedsPolicy = computed(() => {
    return this.draftLines().some(l => l.discountPercentage > 20);
  });

  ngOnInit() {
    this.loadPriceBooks();
    this.loadQuotes();
  }

  loadPriceBooks() {
    this.http.get<PriceBookUi[]>('/api/v1/sales/cpq/price-books').subscribe({
      next: (books) => {
        this.priceBooks.set(books);
        if (books.length > 0 && !this.selectedPriceBookId()) {
          const defaultBook = books.find(b => b.isDefault) ?? books[0];
          this.selectedPriceBookId.set(defaultBook.id);
          this.initDraftLinesFromPriceBook(defaultBook);
        }
      },
      error: () => {
        // Fallback demo data if backend is offline in local preview
        const fallbackBook: PriceBookUi = {
          id: 'pb-enterprise-2026',
          name: 'Standard Enterprise 2026 Price Book',
          currency: 'USD',
          isDefault: true,
          priceBookEntries: [
            {
              id: 'e1',
              productSku: 'ENTERPRISE-CRM-LIC',
              productName: 'Enterprise Growth CRM Seat',
              basePrice: 120,
              costPrice: 30,
              volumeTiers: [
                { minQuantity: 1, maxQuantity: 9, unitPrice: 120 },
                { minQuantity: 10, maxQuantity: 49, unitPrice: 100 },
                { minQuantity: 50, maxQuantity: 99, unitPrice: 85 },
                { minQuantity: 100, maxQuantity: null, unitPrice: 70 }
              ]
            },
            {
              id: 'e2',
              productSku: 'AUTONOMOUS-AI-SEAT',
              productName: 'Autonomous AI Agent Fleet Seat',
              basePrice: 250,
              costPrice: 60,
              volumeTiers: [
                { minQuantity: 1, maxQuantity: 4, unitPrice: 250 },
                { minQuantity: 5, maxQuantity: 19, unitPrice: 210 },
                { minQuantity: 20, maxQuantity: null, unitPrice: 175 }
              ]
            },
            {
              id: 'e3',
              productSku: 'CUSTOM-INTEGRATION-HR',
              productName: 'Custom ERP/WMS Integration Hours',
              basePrice: 150,
              costPrice: 120,
              volumeTiers: [
                { minQuantity: 1, maxQuantity: 19, unitPrice: 150 },
                { minQuantity: 20, maxQuantity: null, unitPrice: 140 }
              ]
            }
          ]
        };
        this.priceBooks.set([fallbackBook]);
        this.selectedPriceBookId.set(fallbackBook.id);
        this.initDraftLinesFromPriceBook(fallbackBook);
      }
    });
  }

  initDraftLinesFromPriceBook(pb: PriceBookUi) {
    if (this.draftLines().length === 0 && pb.priceBookEntries.length > 0) {
      const entry = pb.priceBookEntries[0];
      const initialQty = 15;
      const tierPrice = this.evaluateTierPrice(entry, initialQty);
      const gross = initialQty * tierPrice;
      const net = gross;
      const cogs = initialQty * entry.costPrice;
      const margin = Math.round(((net - cogs) / net) * 1000) / 10;

      this.draftLines.set([
        {
          productSku: entry.productSku,
          productName: entry.productName,
          unitPrice: tierPrice,
          unitCost: entry.costPrice,
          quantity: initialQty,
          discountPercentage: 0,
          lineTotal: net,
          lineMarginPercentage: margin
        }
      ]);
    }
  }

  loadQuotes() {
    let url = '/api/v1/sales/cpq/quotes';
    const status = this.selectedFilterStatus();
    if (status) {
      url += `?status=${status}`;
    }

    this.http.get<CpqQuoteUi[]>(url).subscribe({
      next: (quotes) => this.quotesList.set(quotes),
      error: () => {}
    });
  }

  onFilterStatusChanged(status: string) {
    this.selectedFilterStatus.set(status);
    this.loadQuotes();
  }

  onPriceBookChanged(pbId: string) {
    this.selectedPriceBookId.set(pbId);
    this.recalculateAllLines();
  }

  addLineItem() {
    const entries = this.currentPriceBookEntries();
    if (entries.length === 0) return;

    const entry = entries[0];
    const qty = 5;
    const unitPrice = this.evaluateTierPrice(entry, qty);
    const lineGross = qty * unitPrice;
    const lineTotal = lineGross;
    const lineCogs = qty * entry.costPrice;
    const margin = lineTotal > 0 ? Math.round(((lineTotal - lineCogs) / lineTotal) * 1000) / 10 : 0;

    const current = [...this.draftLines()];
    current.push({
      productSku: entry.productSku,
      productName: entry.productName,
      unitPrice: unitPrice,
      unitCost: entry.costPrice,
      quantity: qty,
      discountPercentage: 0,
      lineTotal: lineTotal,
      lineMarginPercentage: margin
    });
    this.draftLines.set(current);
  }

  removeLineItem(index: number) {
    const current = [...this.draftLines()];
    current.splice(index, 1);
    this.draftLines.set(current);
  }

  onLineSkuChanged(index: number, newSku: string) {
    const entry = this.currentPriceBookEntries().find(e => e.productSku === newSku);
    if (!entry) return;

    const current = [...this.draftLines()];
    const item = current[index];
    const qty = item.quantity;
    const unitPrice = this.evaluateTierPrice(entry, qty);
    const lineGross = qty * unitPrice;
    const discount = lineGross * (item.discountPercentage / 100);
    const lineTotal = Math.round((lineGross - discount) * 100) / 100;
    const lineCogs = qty * entry.costPrice;
    const margin = lineTotal > 0 ? Math.round(((lineTotal - lineCogs) / lineTotal) * 1000) / 10 : 0;

    current[index] = {
      ...item,
      productSku: entry.productSku,
      productName: entry.productName,
      unitPrice: unitPrice,
      unitCost: entry.costPrice,
      lineTotal: lineTotal,
      lineMarginPercentage: margin
    };
    this.draftLines.set(current);
  }

  onQuantityChanged(index: number, newQty: number) {
    const qty = Math.max(1, newQty || 1);
    const current = [...this.draftLines()];
    const item = current[index];
    const entry = this.currentPriceBookEntries().find(e => e.productSku === item.productSku);

    const unitPrice = entry ? this.evaluateTierPrice(entry, qty) : item.unitPrice;
    const lineGross = qty * unitPrice;
    const discount = lineGross * (item.discountPercentage / 100);
    const lineTotal = Math.round((lineGross - discount) * 100) / 100;
    const lineCogs = qty * item.unitCost;
    const margin = lineTotal > 0 ? Math.round(((lineTotal - lineCogs) / lineTotal) * 1000) / 10 : 0;

    current[index] = {
      ...item,
      quantity: qty,
      unitPrice: unitPrice,
      lineTotal: lineTotal,
      lineMarginPercentage: margin
    };
    this.draftLines.set(current);
  }

  onDiscountChanged(index: number, newDiscount: number) {
    const disc = Math.max(0, Math.min(100, newDiscount || 0));
    const current = [...this.draftLines()];
    const item = current[index];

    const lineGross = item.quantity * item.unitPrice;
    const discountAmt = lineGross * (disc / 100);
    const lineTotal = Math.round((lineGross - discountAmt) * 100) / 100;
    const lineCogs = item.quantity * item.unitCost;
    const margin = lineTotal > 0 ? Math.round(((lineTotal - lineCogs) / lineTotal) * 1000) / 10 : 0;

    current[index] = {
      ...item,
      discountPercentage: disc,
      lineTotal: lineTotal,
      lineMarginPercentage: margin
    };
    this.draftLines.set(current);
  }

  evaluateTierPrice(entry: PriceBookEntryUi, qty: number): number {
    if (!entry.volumeTiers || entry.volumeTiers.length === 0) {
      return entry.basePrice;
    }
    const matched = entry.volumeTiers
      .filter(t => qty >= t.minQuantity && (t.maxQuantity === null || qty <= t.maxQuantity))
      .sort((a, b) => b.minQuantity - a.minQuantity)[0];

    return matched ? matched.unitPrice : entry.basePrice;
  }

  recalculateAllLines() {
    const current = [...this.draftLines()];
    current.forEach((item, index) => {
      const entry = this.currentPriceBookEntries().find(e => e.productSku === item.productSku);
      if (entry) {
        const unitPrice = this.evaluateTierPrice(entry, item.quantity);
        const lineGross = item.quantity * unitPrice;
        const discountAmt = lineGross * (item.discountPercentage / 100);
        const lineTotal = Math.round((lineGross - discountAmt) * 100) / 100;
        const lineCogs = item.quantity * entry.costPrice;
        const margin = lineTotal > 0 ? Math.round(((lineTotal - lineCogs) / lineTotal) * 1000) / 10 : 0;

        current[index] = {
          ...item,
          productName: entry.productName,
          unitPrice: unitPrice,
          unitCost: entry.costPrice,
          lineTotal: lineTotal,
          lineMarginPercentage: margin
        };
      }
    });
    this.draftLines.set(current);
  }

  createNewQuoteDraft() {
    this.currentQuote.set(null);
    this.customerName = 'Acme Corporation';
    this.customerEmail = 'procurement@acme.com';
    const pb = this.currentPriceBook();
    if (pb) {
      this.initDraftLinesFromPriceBook(pb);
    }
  }

  saveOrUpdateQuote() {
    this.isSaving.set(true);
    const existing = this.currentQuote();

    const lineInputs = this.draftLines().map(l => ({
      productSku: l.productSku,
      quantity: l.quantity,
      overrideUnitPrice: l.unitPrice,
      discountPercentage: l.discountPercentage,
      productName: l.productName
    }));

    if (existing) {
      // Update existing quote lines
      this.http.post<CpqQuoteUi>(`/api/v1/sales/cpq/quotes/${existing.id}/lines`, {
        lines: lineInputs,
        priceBookId: this.selectedPriceBookId()
      }).subscribe({
        next: (updated) => {
          this.currentQuote.set(updated);
          this.isSaving.set(false);
          this.loadQuotes();
        },
        error: () => this.isSaving.set(false)
      });
    } else {
      // Create new draft quote
      this.http.post<CpqQuoteUi>('/api/v1/sales/cpq/quotes', {
        customerId: '22222222-2222-2222-2222-222222222222',
        customerName: this.customerName,
        customerEmail: this.customerEmail,
        currency: this.currentCurrency(),
        termsAndConditions: this.terms,
        priceBookId: this.selectedPriceBookId(),
        lineItems: lineInputs
      }).subscribe({
        next: (created) => {
          this.currentQuote.set(created);
          this.isSaving.set(false);
          this.loadQuotes();
        },
        error: () => this.isSaving.set(false)
      });
    }
  }

  loadQuoteIntoEditor(q: CpqQuoteUi) {
    this.currentQuote.set(q);
    this.customerName = q.customerName;
    this.customerEmail = q.customerEmail || '';
    this.terms = q.termsAndConditions;
    this.draftLines.set(q.lineItems || []);
  }

  requestManagerApproval() {
    const q = this.currentQuote();
    if (!q) return;

    this.http.post<CpqQuoteUi>(`/api/v1/sales/cpq/quotes/${q.id}/request-approval`, {
      notes: 'Strategic account margin override requested by account executive'
    }).subscribe({
      next: (res) => {
        this.currentQuote.set(res);
        this.loadQuotes();
      }
    });
  }

  approveQuoteAsManager() {
    const q = this.currentQuote();
    if (!q) return;

    this.http.post<CpqQuoteUi>(`/api/v1/sales/cpq/quotes/${q.id}/approve`, {
      managerNameOrEmail: this.managerSignoffName,
      notes: this.managerSignoffNotes
    }).subscribe({
      next: (res) => {
        this.currentQuote.set(res);
        this.loadQuotes();
      }
    });
  }

  rejectQuoteAsManager() {
    const q = this.currentQuote();
    if (!q) return;

    this.http.post<CpqQuoteUi>(`/api/v1/sales/cpq/quotes/${q.id}/reject`, {
      managerNameOrEmail: this.managerSignoffName,
      reason: 'Margin too thin for standard warranty support'
    }).subscribe({
      next: (res) => {
        this.currentQuote.set(res);
        this.loadQuotes();
      }
    });
  }

  sendQuoteToClient() {
    const q = this.currentQuote();
    if (!q) return;

    this.http.post<CpqQuoteUi>(`/api/v1/sales/cpq/quotes/${q.id}/send`, {}).subscribe({
      next: (res) => {
        this.currentQuote.set(res);
        this.loadQuotes();
      },
      error: (err) => {
        alert(err.error?.error || 'Quote blocked by gross margin guard!');
      }
    });
  }

  acceptAndConvertQuote(q: CpqQuoteUi) {
    this.http.post<any>(`/api/v1/sales/cpq/quotes/${q.id}/accept`, {}).subscribe({
      next: (res) => {
        alert(`Quote accepted! Converted to Commerce Order #${res.orderNumber} with Deal marked Won.`);
        this.loadQuotes();
      },
      error: (err) => {
        alert(err.error?.error || 'Failed to accept quote.');
      }
    });
  }

  acceptAndConvertFromModal() {
    const q = this.currentQuote();
    if (!q) return;
    this.acceptAndConvertQuote(q);
    this.showPdfModal.set(false);
  }

  openPdfPreview() {
    const q = this.currentQuote();
    if (!q) return;

    this.http.get<PdfPreviewUi>(`/api/v1/sales/cpq/quotes/${q.id}/pdf-preview`).subscribe({
      next: (preview) => {
        this.pdfPreviewData.set(preview);
        this.showPdfModal.set(true);
      }
    });
  }

  getStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Draft':
        return 'bg-slate-800 text-slate-300 border-slate-700';
      case 'PendingApproval':
        return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
      case 'SentToClient':
        return 'bg-blue-500/20 text-blue-300 border-blue-500/30';
      case 'Accepted':
        return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30';
      case 'ConvertedToOrder':
        return 'bg-indigo-500/20 text-indigo-300 border-indigo-500/30 font-bold';
      case 'Rejected':
        return 'bg-rose-500/20 text-rose-400 border-rose-500/30';
      default:
        return 'bg-slate-800 text-slate-300 border-slate-700';
    }
  }
}
