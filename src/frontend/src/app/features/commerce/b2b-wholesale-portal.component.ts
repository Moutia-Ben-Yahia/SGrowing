import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';

export interface CorporateOfficer {
  id: string;
  name: string;
  email: string;
  role: 'ProcurementBuyer' | 'DepartmentDirector' | 'CFO';
  individualSpendingLimit: number;
  isActive: boolean;
}

export interface CorporateBranch {
  id: string;
  branchName: string;
  branchCode: string;
  shippingAddress: string;
  city: string;
  postalCode: string;
  country: string;
  contactPerson: string;
  contactPhone: string;
  isActive: boolean;
}

export interface CorporateAccount {
  id: string;
  companyName: string;
  taxId: string;
  creditLimit: number;
  paymentTermsDays: number;
  requiresApprovalAboveAmount: number;
  currency: string;
  branches: CorporateBranch[];
  officers: CorporateOfficer[];
}

export interface CreditStatus {
  creditLimit: number;
  unpaidInvoicesTotal: number;
  pendingOrderHoldsTotal: number;
  availableCredit: number;
  creditUtilizationPercent: number;
  paymentTermsDays: number;
  paymentTermsLabel: string;
  unpaidInvoicesCount: number;
  pendingHoldsCount: number;
  isCreditExhausted: boolean;
}

export interface BulkPricingTier {
  minQuantity: number;
  maxQuantity: number | null;
  discountPercent: number;
  tierLabel: string;
}

export interface MatrixItem {
  sku: string;
  size: string;
  color: string;
  quantity: number;
  baseUnitPrice: number;
  unitPrice: number;
  lineTotal: number;
}

export interface WholesaleOrder {
  id: string;
  orderNumber: string;
  externalPoNumber: string;
  corporateAccountId: string;
  companyName: string;
  branchId: string;
  branchName: string;
  submittedByOfficerId: string;
  submitterName: string;
  submitterRole: string;
  status: 'Draft' | 'PendingCorporateApproval' | 'Confirmed' | 'Fulfilling' | 'Delivered' | 'Rejected';
  totalUnits: number;
  subtotal: number;
  volumeDiscountPercent: number;
  discountAmount: number;
  taxAmount: number;
  shippingFee: number;
  grandTotal: number;
  creditHoldAmount: number;
  requiresApproval: boolean;
  approvalReason?: string;
  approvedByOfficerId?: string;
  approverName?: string;
  approvedAt?: string;
  rejectionReason?: string;
  warehouseStockReserved: boolean;
  shippingNotes?: string;
  items: MatrixItem[];
  createdAt: string;
}

export interface CreditLedgerEntry {
  id: string;
  entryType: 'HoldPlaced' | 'HoldReleased' | 'InvoiceBilled' | 'InvoicePaid' | 'CreditLimitAdjustment';
  amount: number;
  referenceNumber: string;
  referenceType: string;
  notes: string;
  createdAt: string;
}

export interface CorporateInvoice {
  id: string;
  invoiceNumber: string;
  externalPoNumber?: string;
  totalAmount: number;
  amountPaid: number;
  balanceDue: number;
  status: 'Unpaid' | 'PartiallyPaid' | 'Paid';
  issuedAt: string;
  dueDate: string;
}

@Component({
  selector: 'app-b2b-wholesale-portal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <!-- Sidebar Navigation -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/90 flex flex-col justify-between p-4 flex-shrink-0">
        <div>
          <!-- Brand Badge -->
          <div class="flex items-center gap-2 mb-6">
            <div class="h-9 w-9 rounded-xl bg-gradient-to-tr from-amber-500 to-cyan-500 flex items-center justify-center font-bold text-slate-950 shadow-lg shadow-amber-500/20 text-lg">
              🏭
            </div>
            <div>
              <h1 class="text-sm font-bold tracking-wide text-white">SGrowing B2B Wholesale</h1>
              <p class="text-[11px] text-amber-400 font-medium">Revolving Credit & Bulk Portal</p>
            </div>
          </div>

          <!-- Enterprise Account Switcher -->
          <div class="mb-5 p-3 rounded-xl bg-slate-800/80 border border-slate-700/80">
            <label class="block text-[10px] font-semibold uppercase tracking-wider text-slate-400 mb-1.5">Corporate Account</label>
            <select
              [(ngModel)]="selectedAccountId"
              (change)="onAccountChange()"
              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-amber-400 font-medium">
              @for (acc of accounts(); track acc.id) {
                <option [value]="acc.id">{{ acc.companyName }}</option>
              }
            </select>
            <div class="flex items-center justify-between mt-2 pt-2 border-t border-slate-700/60 text-[11px]">
              <span class="text-slate-400">Tax ID:</span>
              <span class="font-mono text-cyan-400">{{ selectedAccount().taxId }}</span>
            </div>
          </div>

          <!-- Active Officer Profile Pill -->
          <div class="mb-5 p-3 rounded-xl bg-slate-800/50 border border-slate-700/50">
            <label class="block text-[10px] font-semibold uppercase tracking-wider text-slate-400 mb-1.5">Authorized Officer (You)</label>
            <select
              [(ngModel)]="selectedOfficerId"
              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1.5 text-xs text-white focus:outline-none focus:border-amber-400">
              @for (off of selectedAccount().officers; track off.id) {
                <option [value]="off.id">{{ off.name }} ({{ off.role }})</option>
              }
            </select>
            <div class="mt-2 text-[11px] flex items-center justify-between">
              <span class="text-slate-400">Spending Cap:</span>
              <span class="font-bold text-amber-400 font-mono">{{ formatCurrency(selectedOfficer() ? selectedOfficer()!.individualSpendingLimit : 0) }}</span>
            </div>
          </div>

          <!-- Navigation Tabs -->
          <nav class="space-y-1">
            <button
              (click)="activeTab.set('matrix')"
              [class]="activeTab() === 'matrix' ? 'bg-gradient-to-r from-amber-500/20 to-transparent border-l-2 border-amber-500 text-amber-300 font-semibold' : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'"
              class="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs transition">
              <span class="text-base">📐</span>
              <span>Bulk Matrix SKU Builder</span>
            </button>

            <button
              (click)="activeTab.set('orders')"
              [class]="activeTab() === 'orders' ? 'bg-gradient-to-r from-amber-500/20 to-transparent border-l-2 border-amber-500 text-amber-300 font-semibold' : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'"
              class="w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-xs transition">
              <div class="flex items-center gap-3">
                <span class="text-base">📦</span>
                <span>Purchase Orders</span>
              </div>
              <span class="text-[10px] px-1.5 py-0.5 rounded-full bg-slate-800 text-slate-300 font-mono">{{ purchaseOrders().length }}</span>
            </button>

            <button
              (click)="activeTab.set('approvals')"
              [class]="activeTab() === 'approvals' ? 'bg-gradient-to-r from-amber-500/20 to-transparent border-l-2 border-amber-500 text-amber-300 font-semibold' : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'"
              class="w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-xs transition">
              <div class="flex items-center gap-3">
                <span class="text-base">🛡️</span>
                <span>Manager Approvals</span>
              </div>
              @if (pendingApprovals().length > 0) {
                <span class="text-[10px] px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-400 border border-amber-500/30 font-bold animate-pulse">
                  {{ pendingApprovals().length }}
                </span>
              }
            </button>

            <button
              (click)="activeTab.set('ledger')"
              [class]="activeTab() === 'ledger' ? 'bg-gradient-to-r from-amber-500/20 to-transparent border-l-2 border-amber-500 text-amber-300 font-semibold' : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/50'"
              class="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs transition">
              <span class="text-base">💳</span>
              <span>Credit & Invoice Ledger</span>
            </button>
          </nav>
        </div>

        <!-- Footer System Info -->
        <div class="p-3 bg-slate-950/60 rounded-xl border border-slate-800/80 text-[11px] text-slate-500">
          <div class="flex items-center justify-between mb-1">
            <span>Revolving Ledger:</span>
            <span class="text-emerald-400 font-medium">Synced</span>
          </div>
          <div class="flex items-center justify-between">
            <span>Payment Terms:</span>
            <span class="text-cyan-400 font-mono">{{ creditStatus().paymentTermsLabel }}</span>
          </div>
        </div>
      </aside>

      <!-- Main Application Body -->
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Cockpit Header: Revolving Credit Gauge & Branch Selector -->
        <header class="border-b border-slate-800 bg-slate-900/60 px-8 py-5">
          <div class="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
            <!-- Left Header: Account & Branch Info -->
            <div>
              <div class="flex items-center gap-3">
                <h2 class="text-xl font-bold text-white tracking-tight">{{ selectedAccount().companyName }}</h2>
                <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-cyan-500/10 text-cyan-400 border border-cyan-500/30">
                  {{ creditStatus().paymentTermsLabel }} Terms
                </span>
                <span class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-mono bg-slate-800 text-slate-300">
                  Approval Cap: >{{ formatCurrency(selectedAccount().requiresApprovalAboveAmount) }}
                </span>
              </div>
              <p class="text-xs text-slate-400 mt-1">Multi-tier corporate procurement portal with automated revolving credit holds & matrix calculation</p>
            </div>

            <!-- Right Header: Branch Selector & Circular Revolving Credit Gauge -->
            <div class="flex items-center gap-6">
              <!-- Branch Selector -->
              <div class="bg-slate-800/70 p-2.5 rounded-xl border border-slate-700/70 min-w-[220px]">
                <label class="block text-[10px] font-semibold uppercase tracking-wider text-slate-400 mb-1">Delivery Destination Branch</label>
                <select
                  [(ngModel)]="selectedBranchId"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-2.5 py-1 text-xs text-cyan-300 focus:outline-none focus:border-cyan-400 font-medium">
                  @for (br of selectedAccount().branches; track br.id) {
                    <option [value]="br.id">{{ br.branchName }} ({{ br.city }})</option>
                  }
                </select>
                <p class="text-[10px] text-slate-400 mt-1 truncate">
                  📍 {{ selectedBranch() ? selectedBranch()!.shippingAddress : '' }}, {{ selectedBranch() ? selectedBranch()!.city : '' }}
                </p>
              </div>

              <!-- Circular SVG Credit Gauge -->
              <div class="flex items-center gap-4 bg-slate-800/50 p-2.5 pr-4 rounded-xl border border-slate-700/60">
                <div class="relative w-20 h-20 flex items-center justify-center">
                  <svg class="w-20 h-20 transform -rotate-90" viewBox="0 0 100 100">
                    <circle cx="50" cy="50" r="40" stroke="#1e293b" stroke-width="8" fill="transparent" />
                    <circle
                      cx="50" cy="50" r="40"
                      [attr.stroke]="creditGaugeStrokeColor()"
                      stroke-width="8"
                      stroke-linecap="round"
                      fill="transparent"
                      stroke-dasharray="251.32"
                      [attr.stroke-dashoffset]="creditGaugeDashOffset()"
                      class="transition-all duration-700 ease-out" />
                  </svg>
                  <div class="absolute inset-0 flex flex-col items-center justify-center text-center">
                    <span class="text-xs font-bold text-white font-mono">{{ creditStatus().creditUtilizationPercent }}%</span>
                    <span class="text-[8px] text-slate-400 uppercase tracking-tight">Utilized</span>
                  </div>
                </div>

                <div class="space-y-1 text-xs">
                  <div>
                    <span class="text-slate-400 text-[11px]">Available Credit:</span>
                    <p class="text-base font-extrabold text-emerald-400 font-mono">{{ formatCurrency(creditStatus().availableCredit) }}</p>
                  </div>
                  <div class="flex items-center gap-3 text-[11px] text-slate-400">
                    <span>Limit: <strong class="text-slate-200 font-mono">{{ formatCurrency(creditStatus().creditLimit) }}</strong></span>
                    <span>Holds: <strong class="text-amber-400 font-mono">{{ formatCurrency(creditStatus().pendingOrderHoldsTotal) }}</strong></span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </header>

        <!-- Main Content Area based on Active Tab -->
        <div class="p-8 space-y-6 flex-1">
          <!-- Notification Toast -->
          @if (toastMessage()) {
            <div class="p-3.5 rounded-xl bg-cyan-950/80 border border-cyan-500/40 text-cyan-200 text-xs flex items-center justify-between shadow-xl animate-fade-in">
              <div class="flex items-center gap-2">
                <span>ℹ️</span>
                <span>{{ toastMessage() }}</span>
              </div>
              <button (click)="toastMessage.set(null)" class="text-cyan-400 hover:text-white text-xs font-bold px-2 py-0.5">✕</button>
            </div>
          }

          <!-- TAB 1: Bulk Matrix SKU Order Builder -->
          @if (activeTab() === 'matrix') {
            <div class="space-y-6">
              <!-- Matrix Configuration Banner -->
              <div class="rounded-2xl border border-slate-800 bg-slate-900/60 p-5">
                <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-4 border-b border-slate-800/80">
                  <div>
                    <h3 class="text-base font-bold text-white flex items-center gap-2">
                      <span>📐 Multi-Variant Matrix SKU Grid</span>
                      <span class="text-xs px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 border border-amber-500/30 font-normal">
                        20 Combinations (4 Colors × 5 Sizes)
                      </span>
                    </h3>
                    <p class="text-xs text-slate-400 mt-1">Specify unit quantities per cell. Pricing automatically scales with overall volume tier discounts.</p>
                  </div>

                  <!-- Quick Presets -->
                  <div class="flex items-center gap-2 flex-wrap">
                    <span class="text-[11px] text-slate-400">Quick Fill:</span>
                    <button (click)="applyQuickFill(10)" class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-xs text-slate-200 transition font-medium">
                      +10 All
                    </button>
                    <button (click)="applyQuickFill(25)" class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-xs text-slate-200 transition font-medium">
                      +25 All
                    </button>
                    <button (click)="fillCoreSizes()" class="px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-xs text-cyan-300 transition font-medium">
                      Core Sizes (M/L/XL: 30)
                    </button>
                    <button (click)="clearMatrix()" class="px-2.5 py-1 rounded bg-slate-800 hover:bg-rose-950/60 text-xs text-rose-300 transition">
                      Clear
                    </button>
                  </div>
                </div>

                <!-- Product Style & PO Input Controls -->
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4 pt-4">
                  <div>
                    <label class="block text-[11px] font-semibold text-slate-400 mb-1">Product Style / Catalog Line</label>
                    <select
                      [(ngModel)]="selectedProductStyle"
                      (change)="onProductStyleChange()"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-amber-400">
                      @for (style of productStyles(); track style.name) {
                        <option [value]="style.name">{{ style.name }} (Base: {{ formatCurrency(style.basePrice) }})</option>
                      }
                    </select>
                  </div>

                  <div>
                    <label class="block text-[11px] font-semibold text-slate-400 mb-1">External Purchase Order (PO) Number</label>
                    <input
                      type="text"
                      [(ngModel)]="externalPoNumber"
                      placeholder="e.g. PO-CORP-2026-991"
                      class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-xs text-amber-300 font-mono focus:outline-none focus:border-amber-400" />
                  </div>

                  <div>
                    <label class="block text-[11px] font-semibold text-slate-400 mb-1">PO Document Upload (Optional)</label>
                    <div class="flex items-center gap-2">
                      <input
                        type="file"
                        #fileInput
                        (change)="onFileSelected($event)"
                        class="hidden" />
                      <button
                        (click)="fileInput.click()"
                        class="flex-1 px-3 py-2 bg-slate-800 hover:bg-slate-700 text-xs text-slate-300 rounded-lg border border-slate-700 flex items-center justify-center gap-2 transition">
                        <span>📎</span>
                        <span>{{ uploadedPoFileName() ?? 'Attach Signed PO File (PDF/DOC)' }}</span>
                      </button>
                      @if (uploadedPoFileName()) {
                        <button (click)="uploadedPoFileName.set(null)" class="text-rose-400 hover:text-white text-xs px-2">✕</button>
                      }
                    </div>
                  </div>
                </div>
              </div>

              <!-- 2D Interactive Matrix Grid Table -->
              <div class="rounded-2xl border border-slate-800 bg-slate-900/60 overflow-hidden shadow-xl">
                <div class="overflow-x-auto">
                  <table class="w-full text-left text-xs border-collapse">
                    <thead class="bg-slate-900/90 text-slate-400 uppercase text-[11px] tracking-wider border-b border-slate-800">
                      <tr>
                        <th class="p-4 bg-slate-950/80 sticky left-0 z-10">Color Variant</th>
                        @for (size of sizes(); track size) {
                          <th class="p-4 text-center font-bold text-slate-200 min-w-[100px]">Size {{ size }}</th>
                        }
                        <th class="p-4 text-right bg-slate-950/60 font-semibold min-w-[120px]">Color Total</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-800/60">
                      @for (color of colors(); track color) {
                        <tr class="hover:bg-slate-800/30 transition">
                          <!-- Row Header: Color -->
                          <td class="p-4 font-bold text-white bg-slate-950/40 sticky left-0 z-10 flex items-center gap-2">
                            <span class="w-3.5 h-3.5 rounded-full border border-slate-600 inline-block" [style.background-color]="getColorHex(color)"></span>
                            <span>{{ color }}</span>
                          </td>

                          <!-- Matrix Cells for Sizes -->
                          @for (size of sizes(); track size) {
                            <td class="p-3 text-center">
                              <div class="inline-flex items-center justify-center">
                                <input
                                  type="number"
                                  min="0"
                                  step="1"
                                  [ngModel]="getCellQty(color, size)"
                                  (ngModelChange)="setCellQty(color, size, $event)"
                                  class="w-16 h-9 bg-slate-950 border border-slate-700 focus:border-amber-400 focus:ring-1 focus:ring-amber-400 rounded-lg text-center font-mono text-sm text-white font-medium" />
                              </div>
                            </td>
                          }

                          <!-- Row Total for Color -->
                          <td class="p-4 text-right font-mono font-bold text-cyan-300 bg-slate-950/30">
                            {{ getColorTotal(color) }} units
                          </td>
                        </tr>
                      }
                    </tbody>
                    <!-- Table Footer: Column Totals -->
                    <tfoot class="bg-slate-950/80 border-t-2 border-slate-700/80 font-mono font-bold text-xs">
                      <tr>
                        <td class="p-4 text-slate-300 uppercase tracking-wider sticky left-0 z-10 bg-slate-950/90">Size Subtotals</td>
                        @for (size of sizes(); track size) {
                          <td class="p-4 text-center text-cyan-300">
                            {{ getSizeTotal(size) }}
                          </td>
                        }
                        <td class="p-4 text-right text-amber-400 text-sm">
                          {{ totalMatrixUnits() }} Units
                        </td>
                      </tr>
                    </tfoot>
                  </table>
                </div>
              </div>

              <!-- Real-Time Calculation Cards & Submit Section -->
              <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
                <!-- Volume Discount Tier Summary -->
                <div class="rounded-2xl border border-slate-800 bg-slate-900/60 p-5 space-y-3">
                  <h4 class="text-xs font-bold uppercase tracking-wider text-slate-400">Volume Discount Scaling</h4>
                  <div class="p-3 rounded-xl bg-slate-950/80 border border-slate-800">
                    <div class="flex items-center justify-between mb-1.5">
                      <span class="text-xs text-slate-300">Active Tier:</span>
                      <span class="text-xs font-bold text-amber-400">{{ appliedPricingTier().tierLabel }}</span>
                    </div>
                    <div class="flex items-center justify-between text-xs">
                      <span class="text-slate-400">Base Price:</span>
                      <span class="font-mono line-through text-slate-500">{{ formatCurrency(baseUnitPrice()) }}/unit</span>
                    </div>
                    <div class="flex items-center justify-between text-xs">
                      <span class="text-slate-400">Discounted Unit Price:</span>
                      <span class="font-mono font-bold text-emerald-400">{{ formatCurrency(discountedUnitPrice()) }}/unit</span>
                    </div>
                    <div class="flex items-center justify-between text-xs pt-1 border-t border-slate-800 mt-1">
                      <span class="text-slate-400">Total Volume Discount:</span>
                      <span class="font-mono font-bold text-cyan-400">-{{ appliedPricingTier().discountPercent }}% ({{ formatCurrency(matrixDiscountAmount()) }})</span>
                    </div>
                  </div>

                  <!-- Tier Breakdown Progression -->
                  <div class="space-y-1 text-[11px] text-slate-400">
                    <p class="font-semibold text-slate-300">Pricing Schedule:</p>
                    <div class="flex justify-between"><span>1 - 49 units:</span> <span>0% discount</span></div>
                    <div class="flex justify-between"><span>50 - 199 units:</span> <span class="text-cyan-400">10% discount</span></div>
                    <div class="flex justify-between"><span>200 - 499 units:</span> <span class="text-cyan-400">18% discount</span></div>
                    <div class="flex justify-between"><span>500 - 999 units:</span> <span class="text-emerald-400 font-bold">25% discount + Free Freight</span></div>
                    <div class="flex justify-between"><span>1,000+ units:</span> <span class="text-emerald-400 font-bold">32% discount + Free Freight</span></div>
                  </div>
                </div>

                <!-- Financial Calculation Breakdown -->
                <div class="rounded-2xl border border-slate-800 bg-slate-900/60 p-5 space-y-3">
                  <h4 class="text-xs font-bold uppercase tracking-wider text-slate-400">Order Financial Summary</h4>
                  <div class="space-y-2 text-xs">
                    <div class="flex justify-between">
                      <span class="text-slate-400">Total Units:</span>
                      <span class="font-mono text-white font-bold">{{ totalMatrixUnits() }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-slate-400">Subtotal Before Discount:</span>
                      <span class="font-mono text-slate-300">{{ formatCurrency(matrixSubtotalBeforeDiscount()) }}</span>
                    </div>
                    <div class="flex justify-between text-amber-400">
                      <span>Volume Savings:</span>
                      <span class="font-mono font-bold">-{{ formatCurrency(matrixDiscountAmount()) }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-slate-400">Net Merchandise Subtotal:</span>
                      <span class="font-mono text-white font-bold">{{ formatCurrency(matrixSubtotal()) }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-slate-400">Standard B2B VAT (19%):</span>
                      <span class="font-mono text-slate-300">{{ formatCurrency(matrixTaxAmount()) }}</span>
                    </div>
                    <div class="flex justify-between">
                      <span class="text-slate-400">Freight & Pallet Shipping:</span>
                      <span class="font-mono" [class]="matrixShippingFee() === 0 ? 'text-emerald-400 font-bold' : 'text-slate-300'">
                        {{ matrixShippingFee() === 0 ? 'FREE FREIGHT' : formatCurrency(matrixShippingFee()) }}
                      </span>
                    </div>
                    <div class="pt-2 border-t border-slate-800 flex justify-between items-baseline">
                      <span class="text-sm font-bold text-white">Grand Total Value:</span>
                      <span class="text-lg font-extrabold text-amber-400 font-mono">{{ formatCurrency(matrixGrandTotal()) }}</span>
                    </div>
                  </div>
                </div>

                <!-- Revolving Credit Check & Submit Button -->
                <div class="rounded-2xl border border-slate-800 bg-slate-900/60 p-5 flex flex-col justify-between space-y-4">
                  <div>
                    <h4 class="text-xs font-bold uppercase tracking-wider text-slate-400 mb-2">Corporate Authorization Status</h4>

                    <!-- Credit Check Indicator -->
                    @if (isExceedingCredit()) {
                      <div class="p-3 rounded-xl bg-rose-950/60 border border-rose-500/40 text-xs text-rose-300 mb-3">
                        <strong class="font-bold flex items-center gap-1.5">
                          <span>⛔</span> Credit Limit Exhaustion Warning
                        </strong>
                        <p class="mt-1 text-[11px]">
                          Order of {{ formatCurrency(matrixGrandTotal()) }} exceeds available credit ({{ formatCurrency(creditStatus().availableCredit) }}).
                          Submission will be blocked unless authorized for manual override.
                        </p>
                      </div>
                    } @else {
                      <div class="p-3 rounded-xl bg-emerald-950/40 border border-emerald-500/30 text-xs text-emerald-300 mb-3">
                        <strong class="font-bold flex items-center gap-1.5">
                          <span>✓</span> Revolving Credit Verified
                        </strong>
                        <p class="mt-1 text-[11px]">
                          Leaves {{ formatCurrency(creditStatus().availableCredit - matrixGrandTotal()) }} available credit after this order hold.
                        </p>
                      </div>
                    }

                    <!-- Spending Approval Indicator -->
                    @if (requiresApproval()) {
                      <div class="p-3 rounded-xl bg-amber-950/40 border border-amber-500/40 text-xs text-amber-300">
                        <strong class="font-bold flex items-center gap-1.5">
                          <span>⚠️</span> Routing to Manager Approval Queue
                        </strong>
                        <p class="mt-1 text-[11px]">
                          Total exceeds your limit ({{ formatCurrency(selectedOfficer() ? selectedOfficer()!.individualSpendingLimit : 0) }}) or account threshold ({{ formatCurrency(selectedAccount().requiresApprovalAboveAmount) }}).
                          Will be placed in <em>PendingCorporateApproval</em> status. Stock reserved upon manager approval.
                        </p>
                      </div>
                    } @else {
                      <div class="p-3 rounded-xl bg-cyan-950/40 border border-cyan-500/30 text-xs text-cyan-300">
                        <strong class="font-bold flex items-center gap-1.5">
                          <span>⚡</span> Instant Confirmation & Stock Reservation
                        </strong>
                        <p class="mt-1 text-[11px]">
                          Order is within officer individual spending cap. Warehouse stock will be reserved immediately upon submission.
                        </p>
                      </div>
                    }
                  </div>

                  <!-- Submit Action Button -->
                  <div class="space-y-2">
                    <button
                      (click)="submitMatrixOrder()"
                      [disabled]="totalMatrixUnits() === 0 || (isExceedingCredit() && !allowCreditOverride)"
                      [class]="isExceedingCredit() && !allowCreditOverride
                        ? 'bg-slate-800 text-slate-500 cursor-not-allowed border border-slate-700'
                        : requiresApproval()
                          ? 'bg-amber-600 hover:bg-amber-500 text-slate-950 font-bold shadow-lg shadow-amber-600/30'
                          : 'bg-emerald-600 hover:bg-emerald-500 text-white font-bold shadow-lg shadow-emerald-600/30'"
                      class="w-full py-3.5 px-4 rounded-xl text-sm transition flex items-center justify-center gap-2">
                      @if (requiresApproval()) {
                        <span>Submit for Corporate Approval ({{ formatCurrency(matrixGrandTotal()) }})</span>
                      } @else {
                        <span>Confirm & Reserve Stock ({{ formatCurrency(matrixGrandTotal()) }})</span>
                      }
                    </button>

                    @if (isExceedingCredit()) {
                      <label class="flex items-center gap-2 text-[11px] text-slate-400 cursor-pointer pt-1">
                        <input type="checkbox" [(ngModel)]="allowCreditOverride" class="rounded bg-slate-950 border-slate-700 text-amber-500" />
                        <span>Executive override: Allow order to exceed credit facility</span>
                      </label>
                    }
                  </div>
                </div>
              </div>
            </div>
          }

          <!-- TAB 2: Purchase Orders History -->
          @if (activeTab() === 'orders') {
            <div class="space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-base font-bold text-white">Wholesale Purchase Orders</h3>
                  <p class="text-xs text-slate-400">Complete corporate purchase order history across all branch locations</p>
                </div>
                <div class="flex items-center gap-3">
                  <select
                    [(ngModel)]="orderStatusFilter"
                    class="bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-300">
                    <option value="">All Statuses</option>
                    <option value="Confirmed">Confirmed</option>
                    <option value="PendingCorporateApproval">Pending Approval</option>
                    <option value="Fulfilling">Fulfilling</option>
                    <option value="Delivered">Delivered</option>
                    <option value="Rejected">Rejected</option>
                  </select>
                </div>
              </div>

              <!-- Orders Table -->
              <div class="rounded-2xl border border-slate-800 bg-slate-900/60 overflow-hidden shadow-xl">
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-900/90 text-slate-400 uppercase text-[11px] tracking-wider border-b border-slate-800">
                    <tr>
                      <th class="p-3.5">Order / PO #</th>
                      <th class="p-3.5">Branch</th>
                      <th class="p-3.5">Submitted By</th>
                      <th class="p-3.5 text-center">Units</th>
                      <th class="p-3.5 text-right">Order Value</th>
                      <th class="p-3.5 text-center">Status</th>
                      <th class="p-3.5 text-center">Warehouse Stock</th>
                      <th class="p-3.5 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (order of filteredOrders(); track order.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="p-3.5">
                          <div class="font-bold text-white font-mono">{{ order.orderNumber }}</div>
                          <div class="text-[11px] text-amber-400 font-mono">{{ order.externalPoNumber }}</div>
                          <div class="text-[10px] text-slate-500">{{ order.createdAt | date:'short' }}</div>
                        </td>
                        <td class="p-3.5">
                          <div class="font-medium text-slate-200">{{ order.branchName }}</div>
                        </td>
                        <td class="p-3.5">
                          <div class="text-slate-200 font-medium">{{ order.submitterName }}</div>
                          <span class="inline-block text-[10px] px-1.5 py-0.2 rounded bg-slate-800 text-slate-400">
                            {{ order.submitterRole }}
                          </span>
                        </td>
                        <td class="p-3.5 text-center font-mono font-bold text-cyan-300">
                          {{ order.totalUnits }}
                        </td>
                        <td class="p-3.5 text-right font-mono font-bold text-white">
                          {{ formatCurrency(order.grandTotal) }}
                        </td>
                        <td class="p-3.5 text-center">
                          <span [class]="getOrderStatusBadgeClass(order.status)" class="inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-semibold border">
                            {{ order.status }}
                          </span>
                        </td>
                        <td class="p-3.5 text-center">
                          @if (order.warehouseStockReserved) {
                            <span class="text-emerald-400 text-xs font-semibold flex items-center justify-center gap-1">
                              <span>✓</span> Reserved
                            </span>
                          } @else {
                            <span class="text-slate-500 text-xs">Unreserved</span>
                          }
                        </td>
                        <td class="p-3.5 text-right space-x-2">
                          <button
                            (click)="reorderPrevious(order)"
                            class="px-2.5 py-1 rounded bg-slate-800 hover:bg-cyan-950 text-cyan-400 hover:text-cyan-300 border border-slate-700 text-xs font-medium transition inline-flex items-center gap-1">
                            <span>🔁</span> 1-Click Reorder
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- TAB 3: Manager Approval Queue -->
          @if (activeTab() === 'approvals') {
            <div class="space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-base font-bold text-white flex items-center gap-2">
                    <span>🛡️ Corporate Manager Approval Queue</span>
                    <span class="text-xs px-2.5 py-0.5 rounded-full bg-amber-500/20 text-amber-300 border border-amber-500/30 font-bold">
                      {{ pendingApprovals().length }} Orders Pending
                    </span>
                  </h3>
                  <p class="text-xs text-slate-400">Orders exceeding individual spending limits or company threshold require review by a Department Director or CFO</p>
                </div>
              </div>

              @if (pendingApprovals().length === 0) {
                <div class="p-12 text-center rounded-2xl border border-slate-800 bg-slate-900/40 space-y-3">
                  <div class="text-4xl">✨</div>
                  <h4 class="text-base font-bold text-white">Approval Queue Clear</h4>
                  <p class="text-xs text-slate-400">All wholesale orders have been approved or fulfilled. No pending escalation requests.</p>
                </div>
              } @else {
                <div class="grid grid-cols-1 gap-4">
                  @for (order of pendingApprovals(); track order.id) {
                    <div class="rounded-2xl border border-amber-500/30 bg-slate-900/80 p-5 space-y-4 shadow-xl">
                      <div class="flex flex-col md:flex-row md:items-center justify-between gap-3 pb-3 border-b border-slate-800">
                        <div>
                          <div class="flex items-center gap-3">
                            <span class="text-base font-bold text-white font-mono">{{ order.orderNumber }}</span>
                            <span class="text-xs font-mono text-amber-400">{{ order.externalPoNumber }}</span>
                            <span class="px-2 py-0.5 rounded text-[11px] font-bold bg-amber-500/20 text-amber-300 border border-amber-500/30">
                              Requires Corporate Sign-off
                            </span>
                          </div>
                          <p class="text-xs text-slate-400 mt-1">
                            Destination: <strong class="text-slate-200">{{ order.branchName }}</strong> • Submitted by: <strong class="text-cyan-400">{{ order.submitterName }}</strong> ({{ order.submitterRole }})
                          </p>
                        </div>

                        <div class="text-right">
                          <span class="text-xs text-slate-400">Commitment Total:</span>
                          <p class="text-xl font-extrabold text-amber-400 font-mono">{{ formatCurrency(order.grandTotal) }}</p>
                        </div>
                      </div>

                      <!-- Escalation Reason Card -->
                      <div class="p-3 rounded-xl bg-amber-950/30 border border-amber-500/20 text-xs text-amber-200">
                        <strong class="font-bold">Escalation Trigger:</strong> {{ order.approvalReason }}
                      </div>

                      <!-- Order Items Preview -->
                      <div class="bg-slate-950/60 p-3 rounded-xl border border-slate-800 text-xs">
                        <div class="text-[11px] font-semibold uppercase tracking-wider text-slate-400 mb-2">Matrix Items Breakdown ({{ order.totalUnits }} units total):</div>
                        <div class="flex flex-wrap gap-2">
                          @for (item of order.items; track item.sku) {
                            <div class="px-2.5 py-1 rounded bg-slate-900 border border-slate-700/80 text-[11px]">
                              <span class="font-bold text-white">{{ item.color }} / {{ item.size }}:</span>
                              <span class="font-mono text-cyan-300 ml-1">{{ item.quantity }} units</span>
                              <span class="text-slate-500 ml-1">({{ formatCurrency(item.lineTotal) }})</span>
                            </div>
                          }
                        </div>
                      </div>

                      <!-- Approval Actions -->
                      <div class="flex items-center justify-end gap-3 pt-2">
                        <button
                          (click)="openRejectionModal(order.id)"
                          class="px-4 py-2 rounded-xl bg-slate-800 hover:bg-rose-950/80 text-rose-300 border border-rose-500/30 text-xs font-bold transition flex items-center gap-1.5">
                          <span>✕</span> Reject Order & Release Hold
                        </button>
                        <button
                          (click)="approveOrder(order.id)"
                          class="px-5 py-2 rounded-xl bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-bold shadow-lg shadow-emerald-600/30 transition flex items-center gap-1.5">
                          <span>✓</span> Approve Order & Reserve Stock
                        </button>
                      </div>
                    </div>
                  }
                </div>
              }
            </div>
          }

          <!-- TAB 4: Revolving Credit & Invoice Ledger -->
          @if (activeTab() === 'ledger') {
            <div class="space-y-6">
              <!-- Revolving Credit Math Breakdown Card -->
              <div class="rounded-2xl border border-slate-800 bg-slate-900/60 p-5">
                <h3 class="text-sm font-bold uppercase tracking-wider text-slate-400 mb-3">Revolving Credit Ledger Formula</h3>
                <div class="grid grid-cols-1 md:grid-cols-4 gap-4 text-center">
                  <div class="p-4 rounded-xl bg-slate-950/80 border border-slate-800">
                    <span class="text-[11px] text-slate-400 uppercase">Total Facility Limit</span>
                    <p class="text-xl font-extrabold text-white font-mono mt-1">{{ formatCurrency(creditStatus().creditLimit) }}</p>
                  </div>
                  <div class="p-4 rounded-xl bg-slate-950/80 border border-slate-800">
                    <span class="text-[11px] text-slate-400 uppercase">Unpaid Invoices Total</span>
                    <p class="text-xl font-extrabold text-rose-400 font-mono mt-1">-{{ formatCurrency(creditStatus().unpaidInvoicesTotal) }}</p>
                  </div>
                  <div class="p-4 rounded-xl bg-slate-950/80 border border-slate-800">
                    <span class="text-[11px] text-slate-400 uppercase">Pending Order Holds</span>
                    <p class="text-xl font-extrabold text-amber-400 font-mono mt-1">-{{ formatCurrency(creditStatus().pendingOrderHoldsTotal) }}</p>
                  </div>
                  <div class="p-4 rounded-xl bg-emerald-950/40 border border-emerald-500/40">
                    <span class="text-[11px] text-emerald-300 uppercase font-bold">= Available Revolving Credit</span>
                    <p class="text-xl font-extrabold text-emerald-400 font-mono mt-1">{{ formatCurrency(creditStatus().availableCredit) }}</p>
                  </div>
                </div>
              </div>

              <!-- Unpaid Corporate Invoices Table -->
              <div class="rounded-2xl border border-slate-800 bg-slate-900/60 overflow-hidden shadow-xl">
                <div class="p-4 border-b border-slate-800 flex items-center justify-between bg-slate-900/90">
                  <h4 class="text-xs font-bold uppercase tracking-wider text-white">Billed Invoices & Settlement</h4>
                  <span class="text-xs text-slate-400">Paying invoices immediately replenishes available revolving credit</span>
                </div>
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-900/90 text-slate-400 uppercase text-[11px] tracking-wider border-b border-slate-800">
                    <tr>
                      <th class="p-3.5">Invoice #</th>
                      <th class="p-3.5">PO Ref</th>
                      <th class="p-3.5">Issued Date</th>
                      <th class="p-3.5">Due Date</th>
                      <th class="p-3.5 text-right">Invoice Amount</th>
                      <th class="p-3.5 text-right">Balance Due</th>
                      <th class="p-3.5 text-center">Status</th>
                      <th class="p-3.5 text-right">Action</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (inv of invoices(); track inv.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="p-3.5 font-bold font-mono text-cyan-300">{{ inv.invoiceNumber }}</td>
                        <td class="p-3.5 font-mono text-slate-300">{{ inv.externalPoNumber ?? '—' }}</td>
                        <td class="p-3.5 text-slate-400">{{ inv.issuedAt | date:'mediumDate' }}</td>
                        <td class="p-3.5 text-slate-300 font-medium">{{ inv.dueDate | date:'mediumDate' }}</td>
                        <td class="p-3.5 text-right font-mono text-white">{{ formatCurrency(inv.totalAmount) }}</td>
                        <td class="p-3.5 text-right font-mono font-bold" [class]="inv.balanceDue > 0 ? 'text-rose-400' : 'text-emerald-400'">
                          {{ formatCurrency(inv.balanceDue) }}
                        </td>
                        <td class="p-3.5 text-center">
                          <span [class]="inv.status === 'Paid' ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30' : 'bg-rose-500/20 text-rose-300 border-rose-500/30'" class="px-2 py-0.5 rounded text-[11px] font-semibold border">
                            {{ inv.status }}
                          </span>
                        </td>
                        <td class="p-3.5 text-right">
                          @if (inv.status !== 'Paid') {
                            <button
                              (click)="payInvoice(inv.id, inv.balanceDue)"
                              class="px-2.5 py-1 rounded bg-emerald-600 hover:bg-emerald-500 text-white font-bold text-xs transition">
                              Pay {{ formatCurrency(inv.balanceDue) }}
                            </button>
                          } @else {
                            <span class="text-emerald-400 text-xs font-semibold">Settled</span>
                          }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>

              <!-- Transaction Ledger Audit Trail -->
              <div class="rounded-2xl border border-slate-800 bg-slate-900/60 overflow-hidden shadow-xl">
                <div class="p-4 border-b border-slate-800 bg-slate-900/90">
                  <h4 class="text-xs font-bold uppercase tracking-wider text-white">Credit Facility Transaction Audit Trail</h4>
                </div>
                <table class="w-full text-left text-xs">
                  <thead class="bg-slate-900/90 text-slate-400 uppercase text-[11px] tracking-wider border-b border-slate-800">
                    <tr>
                      <th class="p-3.5">Timestamp</th>
                      <th class="p-3.5">Event Type</th>
                      <th class="p-3.5">Reference #</th>
                      <th class="p-3.5 text-right">Amount</th>
                      <th class="p-3.5">Audit Notes</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-mono text-[11px]">
                    @for (entry of creditLedger(); track entry.id) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="p-3.5 text-slate-400">{{ entry.createdAt | date:'short' }}</td>
                        <td class="p-3.5">
                          <span [class]="getLedgerTypeBadgeClass(entry.entryType)" class="px-2 py-0.5 rounded font-bold text-[10px]">
                            {{ entry.entryType }}
                          </span>
                        </td>
                        <td class="p-3.5 text-cyan-300">{{ entry.referenceNumber }}</td>
                        <td class="p-3.5 text-right font-bold" [class]="entry.entryType === 'HoldPlaced' || entry.entryType === 'InvoiceBilled' ? 'text-amber-400' : 'text-emerald-400'">
                          {{ formatCurrency(entry.amount) }}
                        </td>
                        <td class="p-3.5 text-slate-300 font-sans text-xs">{{ entry.notes }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        </div>
      </main>

      <!-- Rejection Modal -->
      @if (rejectionModalOpen()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-md w-full p-6 space-y-4 shadow-2xl">
            <h4 class="text-base font-bold text-white">Reject Wholesale Purchase Order</h4>
            <p class="text-xs text-slate-400">Rejecting this order will release the revolving credit hold and notify the procurement buyer.</p>

            <div>
              <label class="block text-xs font-semibold text-slate-300 mb-1">Rejection Reason</label>
              <textarea
                [(ngModel)]="rejectionReasonInput"
                rows="3"
                placeholder="e.g. Budget ceiling reached for this quarter. Resubmit in Q1."
                class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-xs text-white focus:outline-none focus:border-rose-400"></textarea>
            </div>

            <div class="flex items-center justify-end gap-3 pt-2">
              <button
                (click)="rejectionModalOpen.set(false)"
                class="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs text-slate-300 font-medium">
                Cancel
              </button>
              <button
                (click)="confirmRejectOrder()"
                class="px-4 py-2 rounded-lg bg-rose-600 hover:bg-rose-500 text-xs text-white font-bold">
                Confirm Rejection & Release Hold
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
  styles: [`
    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
    .animate-fade-in {
      animation: fadeIn 0.2s ease-out forwards;
    }
  `]
})
export class B2BWholesalePortalComponent implements OnInit {
  private http = inject(HttpClient);

  // Active Tab
  activeTab = signal<'matrix' | 'orders' | 'approvals' | 'ledger'>('matrix');

  // Corporate Accounts
  accounts = signal<CorporateAccount[]>([]);
  selectedAccountId = signal<string>('');
  selectedBranchId = signal<string>('');
  selectedOfficerId = signal<string>('');

  // Matrix configuration
  productStyles = signal([
    { name: 'Workwear Industrial Heavy Duty Polo', basePrice: 28.0, baseSku: 'WWR-POLO' },
    { name: 'Tactical High-Durability Cargo Trousers', basePrice: 42.0, baseSku: 'TAC-PANTS' },
    { name: 'All-Weather Waterproof Softshell Jacket', basePrice: 65.0, baseSku: 'ALL-SOFT' },
    { name: 'High-Visibility Thermal Reflective Vest', basePrice: 22.0, baseSku: 'HIV-VEST' }
  ]);
  selectedProductStyle = signal('Workwear Industrial Heavy Duty Polo');
  baseUnitPrice = signal(28.0);
  colors = signal(['Navy', 'Charcoal', 'Black', 'Royal Blue']);
  sizes = signal(['S', 'M', 'L', 'XL', '2XL']);

  // Matrix quantities store: key = `${color}_${size}`
  matrixCellQuantities = signal<Record<string, number>>({});

  // Order Details
  externalPoNumber = signal('PO-ACME-2026-981');
  uploadedPoFileName = signal<string | null>(null);
  allowCreditOverride = false;
  orderStatusFilter = '';

  // Data Collections
  purchaseOrders = signal<WholesaleOrder[]>([]);
  creditStatus = signal<CreditStatus>({
    creditLimit: 100000,
    unpaidInvoicesTotal: 21400,
    pendingOrderHoldsTotal: 20301.88,
    availableCredit: 58298.12,
    creditUtilizationPercent: 41.7,
    paymentTermsDays: 30,
    paymentTermsLabel: 'Net 30',
    unpaidInvoicesCount: 1,
    pendingHoldsCount: 2,
    isCreditExhausted: false
  });
  creditLedger = signal<CreditLedgerEntry[]>([]);
  invoices = signal<CorporateInvoice[]>([]);

  // Modals & UI State
  rejectionModalOpen = signal(false);
  rejectingOrderId = signal<string | null>(null);
  rejectionReasonInput = signal('');
  toastMessage = signal<string | null>(null);

  // Computed Entities
  selectedAccount = computed(() =>
    this.accounts().find(a => a.id === this.selectedAccountId()) ?? this.accounts()[0]
  );

  selectedBranch = computed(() =>
    this.selectedAccount()?.branches.find(b => b.id === this.selectedBranchId()) ??
    this.selectedAccount()?.branches[0]
  );

  selectedOfficer = computed(() =>
    this.selectedAccount()?.officers.find(o => o.id === this.selectedOfficerId()) ??
    this.selectedAccount()?.officers[0]
  );

  // Matrix Mathematical Computations
  totalMatrixUnits = computed(() => {
    const map = this.matrixCellQuantities();
    return Object.values(map).reduce((sum, qty) => sum + (qty || 0), 0);
  });

  appliedPricingTier = computed<BulkPricingTier>(() => {
    const units = this.totalMatrixUnits();
    if (units >= 1000) return { minQuantity: 1000, maxQuantity: null, discountPercent: 32, tierLabel: 'Tier 5: Master Distributor (32% OFF)' };
    if (units >= 500) return { minQuantity: 500, maxQuantity: 999, discountPercent: 25, tierLabel: 'Tier 4: Enterprise Volume (25% OFF)' };
    if (units >= 200) return { minQuantity: 200, maxQuantity: 499, discountPercent: 18, tierLabel: 'Tier 3: Wholesale Gold (18% OFF)' };
    if (units >= 50) return { minQuantity: 50, maxQuantity: 199, discountPercent: 10, tierLabel: 'Tier 2: Wholesale Silver (10% OFF)' };
    return { minQuantity: 1, maxQuantity: 49, discountPercent: 0, tierLabel: 'Tier 1: Standard Bulk (Base Rate)' };
  });

  discountedUnitPrice = computed(() => {
    const base = this.baseUnitPrice();
    const discount = this.appliedPricingTier().discountPercent;
    return Math.round(base * (1 - discount / 100) * 100) / 100;
  });

  matrixSubtotalBeforeDiscount = computed(() => {
    return Math.round(this.totalMatrixUnits() * this.baseUnitPrice() * 100) / 100;
  });

  matrixSubtotal = computed(() => {
    return Math.round(this.totalMatrixUnits() * this.discountedUnitPrice() * 100) / 100;
  });

  matrixDiscountAmount = computed(() => {
    return Math.round((this.matrixSubtotalBeforeDiscount() - this.matrixSubtotal()) * 100) / 100;
  });

  matrixTaxAmount = computed(() => {
    return Math.round(this.matrixSubtotal() * 0.19 * 100) / 100; // 19% B2B VAT
  });

  matrixShippingFee = computed(() => {
    const units = this.totalMatrixUnits();
    return units >= 500 || units === 0 ? 0 : 45.0; // Free freight above 500 units
  });

  matrixGrandTotal = computed(() => {
    return Math.round((this.matrixSubtotal() + this.matrixTaxAmount() + this.matrixShippingFee()) * 100) / 100;
  });

  isExceedingCredit = computed(() => {
    return this.matrixGrandTotal() > this.creditStatus().availableCredit;
  });

  isExceedingSpendingLimit = computed(() => {
    const limit = this.selectedOfficer()?.individualSpendingLimit ?? 0;
    return this.matrixGrandTotal() > limit;
  });

  isExceedingAccountThreshold = computed(() => {
    const threshold = this.selectedAccount()?.requiresApprovalAboveAmount ?? 0;
    return this.matrixGrandTotal() > threshold;
  });

  requiresApproval = computed(() => {
    return this.isExceedingSpendingLimit() || this.isExceedingAccountThreshold();
  });

  pendingApprovals = computed(() => {
    return this.purchaseOrders().filter(o => o.status === 'PendingCorporateApproval');
  });

  filteredOrders = computed(() => {
    const filter = this.orderStatusFilter;
    if (!filter) return this.purchaseOrders();
    return this.purchaseOrders().filter(o => o.status === filter);
  });

  // Credit SVG Gauge Dashoffset
  creditGaugeDashOffset = computed(() => {
    const percent = Math.min(100, Math.max(0, this.creditStatus().creditUtilizationPercent));
    const circumference = 2 * Math.PI * 40; // 251.32
    return circumference - (percent / 100) * circumference;
  });

  creditGaugeStrokeColor = computed(() => {
    const percent = this.creditStatus().creditUtilizationPercent;
    if (percent >= 90) return '#f43f5e'; // rose-500
    if (percent >= 75) return '#f59e0b'; // amber-500
    return '#06b6d4';                   // cyan-500
  });

  ngOnInit() {
    this.seedDemoData();
    this.fillCoreSizes();
  }

  seedDemoData() {
    const acmeBranches: CorporateBranch[] = [
      {
        id: 'br-par-01',
        branchName: 'Paris Hub & Fulfillment Depot',
        branchCode: 'DEPOT-PAR-01',
        shippingAddress: '45 Rue de la Logistique',
        city: 'Paris',
        postalCode: '75012',
        country: 'France',
        contactPerson: 'Jean Dupont',
        contactPhone: '+33 1 42 68 00 11',
        isActive: true
      },
      {
        id: 'br-lyn-02',
        branchName: 'Lyon Distribution Center',
        branchCode: 'HUB-LYN-02',
        shippingAddress: '12 Avenue des Entrepôts',
        city: 'Lyon',
        postalCode: '69007',
        country: 'France',
        contactPerson: 'Claire Moreau',
        contactPhone: '+33 4 78 50 12 34',
        isActive: true
      },
      {
        id: 'br-mrs-03',
        branchName: 'Marseille Port Terminal',
        branchCode: 'PORT-MRS-03',
        shippingAddress: '8 Quai Maritime',
        city: 'Marseille',
        postalCode: '13002',
        country: 'France',
        contactPerson: 'Henri Blanc',
        contactPhone: '+33 4 91 14 25 80',
        isActive: true
      }
    ];

    const acmeOfficers: CorporateOfficer[] = [
      {
        id: 'off-sarah-1',
        name: 'Sarah Jenkins',
        email: 's.jenkins@acme-industrial.com',
        role: 'ProcurementBuyer',
        individualSpendingLimit: 5000,
        isActive: true
      },
      {
        id: 'off-marcus-2',
        name: 'Marcus Vance',
        email: 'm.vance@acme-industrial.com',
        role: 'DepartmentDirector',
        individualSpendingLimit: 25000,
        isActive: true
      },
      {
        id: 'off-elena-3',
        name: 'Elena Rostova',
        email: 'e.rostova@acme-industrial.com',
        role: 'CFO',
        individualSpendingLimit: 150000,
        isActive: true
      }
    ];

    const acmeAccount: CorporateAccount = {
      id: 'acc-acme-01',
      companyName: 'Acme Industrial Logistics Corp',
      taxId: 'FR-889102934',
      creditLimit: 100000,
      paymentTermsDays: 30,
      requiresApprovalAboveAmount: 7500,
      currency: 'USD',
      branches: acmeBranches,
      officers: acmeOfficers
    };

    const apexBranches: CorporateBranch[] = [
      {
        id: 'br-casa-01',
        branchName: 'Casablanca Gateway Logistics',
        branchCode: 'CASA-MAIN-01',
        shippingAddress: 'Boulevard Abdelmoumen',
        city: 'Casablanca',
        postalCode: '20100',
        country: 'Morocco',
        contactPerson: 'Yassine Bennani',
        contactPhone: '+212 522 99 88 77',
        isActive: true
      },
      {
        id: 'br-tun-02',
        branchName: 'Tunis Carthage Hub',
        branchCode: 'TUN-CARTH-01',
        shippingAddress: 'Zone Industrielle Charguia II',
        city: 'Tunis',
        postalCode: '2035',
        country: 'Tunisia',
        contactPerson: 'Mehdi Zribi',
        contactPhone: '+216 71 80 90 00',
        isActive: true
      }
    ];

    const apexOfficers: CorporateOfficer[] = [
      {
        id: 'off-amine-1',
        name: 'Amine Khelil',
        email: 'a.khelil@globalapex.com',
        role: 'ProcurementBuyer',
        individualSpendingLimit: 10000,
        isActive: true
      },
      {
        id: 'off-nadia-2',
        name: 'Nadia Bouzid',
        email: 'n.bouzid@globalapex.com',
        role: 'DepartmentDirector',
        individualSpendingLimit: 50000,
        isActive: true
      }
    ];

    const apexAccount: CorporateAccount = {
      id: 'acc-apex-02',
      companyName: 'Global Apex Retailers Group',
      taxId: 'TN-9988776B',
      creditLimit: 250000,
      paymentTermsDays: 60,
      requiresApprovalAboveAmount: 15000,
      currency: 'USD',
      branches: apexBranches,
      officers: apexOfficers
    };

    this.accounts.set([acmeAccount, apexAccount]);
    this.selectedAccountId.set(acmeAccount.id);
    this.selectedBranchId.set(acmeBranches[0].id);
    this.selectedOfficerId.set(acmeOfficers[0].id);

    // Initial Orders
    this.purchaseOrders.set([
      {
        id: 'ord-101',
        orderNumber: 'WPO-2026-0104',
        externalPoNumber: 'PO-ACME-9011',
        corporateAccountId: acmeAccount.id,
        companyName: acmeAccount.companyName,
        branchId: acmeBranches[0].id,
        branchName: acmeBranches[0].branchName,
        submittedByOfficerId: acmeOfficers[0].id,
        submitterName: acmeOfficers[0].name,
        submitterRole: acmeOfficers[0].role,
        status: 'Fulfilling',
        totalUnits: 240,
        subtotal: 5510.40,
        volumeDiscountPercent: 18,
        discountAmount: 1209.60,
        taxAmount: 1046.98,
        shippingFee: 0,
        grandTotal: 6557.38,
        creditHoldAmount: 6557.38,
        requiresApproval: false,
        warehouseStockReserved: true,
        items: [
          { sku: 'WWR-POLO-NAV-M', size: 'M', color: 'Navy', quantity: 80, baseUnitPrice: 28, unitPrice: 22.96, lineTotal: 1836.80 },
          { sku: 'WWR-POLO-NAV-L', size: 'L', color: 'Navy', quantity: 80, baseUnitPrice: 28, unitPrice: 22.96, lineTotal: 1836.80 },
          { sku: 'WWR-POLO-BLK-XL', size: 'XL', color: 'Black', quantity: 80, baseUnitPrice: 28, unitPrice: 22.96, lineTotal: 1836.80 }
        ],
        createdAt: new Date(Date.now() - 3 * 86400000).toISOString()
      },
      {
        id: 'ord-102',
        orderNumber: 'WPO-2026-0105',
        externalPoNumber: 'PO-ACME-9045-PEND',
        corporateAccountId: acmeAccount.id,
        companyName: acmeAccount.companyName,
        branchId: acmeBranches[1].id,
        branchName: acmeBranches[1].branchName,
        submittedByOfficerId: acmeOfficers[0].id,
        submitterName: acmeOfficers[0].name,
        submitterRole: acmeOfficers[0].role,
        status: 'PendingCorporateApproval',
        totalUnits: 550,
        subtotal: 11550.00,
        volumeDiscountPercent: 25,
        discountAmount: 3850.00,
        taxAmount: 2194.50,
        shippingFee: 0,
        grandTotal: 13744.50,
        creditHoldAmount: 13744.50,
        requiresApproval: true,
        approvalReason: 'Order total of $13,744.50 exceeds officer limit ($5,000.00) and company threshold ($7,500.00). Requires Department Director or CFO approval.',
        warehouseStockReserved: false,
        items: [
          { sku: 'WWR-POLO-NAV-S', size: 'S', color: 'Navy', quantity: 50, baseUnitPrice: 28, unitPrice: 21.0, lineTotal: 1050.00 },
          { sku: 'WWR-POLO-NAV-M', size: 'M', color: 'Navy', quantity: 100, baseUnitPrice: 28, unitPrice: 21.0, lineTotal: 2100.00 },
          { sku: 'WWR-POLO-NAV-L', size: 'L', color: 'Navy', quantity: 150, baseUnitPrice: 28, unitPrice: 21.0, lineTotal: 3150.00 },
          { sku: 'WWR-POLO-CHA-M', size: 'M', color: 'Charcoal', quantity: 100, baseUnitPrice: 28, unitPrice: 21.0, lineTotal: 2100.00 },
          { sku: 'WWR-POLO-CHA-L', size: 'L', color: 'Charcoal', quantity: 150, baseUnitPrice: 28, unitPrice: 21.0, lineTotal: 3150.00 }
        ],
        createdAt: new Date(Date.now() - 5 * 3600000).toISOString()
      }
    ]);

    // Initial Invoices
    this.invoices.set([
      {
        id: 'inv-412',
        invoiceNumber: 'INV-2026-00412',
        externalPoNumber: 'PO-ACME-8921',
        totalAmount: 21400.00,
        amountPaid: 0,
        balanceDue: 21400.00,
        status: 'Unpaid',
        issuedAt: new Date(Date.now() - 14 * 86400000).toISOString(),
        dueDate: new Date(Date.now() + 16 * 86400000).toISOString()
      }
    ]);

    // Initial Ledger
    this.creditLedger.set([
      {
        id: 'led-1',
        entryType: 'CreditLimitAdjustment',
        amount: 100000,
        referenceNumber: 'FACILITY-INITIAL',
        referenceType: 'CreditFacility',
        notes: 'Initial revolving credit facility activated with Net-30 payment terms',
        createdAt: new Date(Date.now() - 180 * 86400000).toISOString()
      },
      {
        id: 'led-2',
        entryType: 'InvoiceBilled',
        amount: 21400,
        referenceNumber: 'INV-2026-00412',
        referenceType: 'Invoice',
        notes: 'Billed for previous shipment PO-ACME-8921',
        createdAt: new Date(Date.now() - 14 * 86400000).toISOString()
      },
      {
        id: 'led-3',
        entryType: 'HoldPlaced',
        amount: 6557.38,
        referenceNumber: 'WPO-2026-0104',
        referenceType: 'PurchaseOrder',
        notes: 'Revolving credit hold placed on confirmed wholesale order WPO-2026-0104',
        createdAt: new Date(Date.now() - 3 * 86400000).toISOString()
      },
      {
        id: 'led-4',
        entryType: 'HoldPlaced',
        amount: 13744.50,
        referenceNumber: 'WPO-2026-0105',
        referenceType: 'PurchaseOrder',
        notes: 'Pre-approval revolving credit hold placed on wholesale order WPO-2026-0105',
        createdAt: new Date(Date.now() - 5 * 3600000).toISOString()
      }
    ]);
  }

  onAccountChange() {
    const acc = this.selectedAccount();
    if (acc) {
      this.selectedBranchId.set(acc.branches[0]?.id ?? '');
      this.selectedOfficerId.set(acc.officers[0]?.id ?? '');
      this.recalculateCreditStatus();
    }
  }

  onProductStyleChange() {
    const found = this.productStyles().find(p => p.name === this.selectedProductStyle());
    if (found) {
      this.baseUnitPrice.set(found.basePrice);
    }
  }

  getCellKey(color: string, size: string): string {
    return `${color}_${size}`;
  }

  getCellQty(color: string, size: string): number {
    return this.matrixCellQuantities()[this.getCellKey(color, size)] || 0;
  }

  setCellQty(color: string, size: string, qty: number) {
    const safeQty = Math.max(0, Math.floor(qty || 0));
    this.matrixCellQuantities.update(map => ({
      ...map,
      [this.getCellKey(color, size)]: safeQty
    }));
  }

  getColorTotal(color: string): number {
    let sum = 0;
    for (const size of this.sizes()) {
      sum += this.getCellQty(color, size);
    }
    return sum;
  }

  getSizeTotal(size: string): number {
    let sum = 0;
    for (const color of this.colors()) {
      sum += this.getCellQty(color, size);
    }
    return sum;
  }

  applyQuickFill(amount: number) {
    const newMap: Record<string, number> = {};
    for (const color of this.colors()) {
      for (const size of this.sizes()) {
        newMap[this.getCellKey(color, size)] = amount;
      }
    }
    this.matrixCellQuantities.set(newMap);
    this.toastMessage.set(`Applied +${amount} units across all 20 matrix SKU combinations (${amount * 20} total units).`);
  }

  fillCoreSizes() {
    const newMap: Record<string, number> = {};
    for (const color of this.colors()) {
      newMap[this.getCellKey(color, 'S')] = 10;
      newMap[this.getCellKey(color, 'M')] = 30;
      newMap[this.getCellKey(color, 'L')] = 35;
      newMap[this.getCellKey(color, 'XL')] = 20;
      newMap[this.getCellKey(color, '2XL')] = 5;
    }
    this.matrixCellQuantities.set(newMap);
    this.toastMessage.set('Loaded standard core size distribution (400 total units, Gold Wholesale Tier 3).');
  }

  clearMatrix() {
    this.matrixCellQuantities.set({});
    this.toastMessage.set('Matrix quantities cleared.');
  }

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.uploadedPoFileName.set(input.files[0].name);
      this.toastMessage.set(`Attached PO file: ${input.files[0].name}`);
    }
  }

  submitMatrixOrder() {
    if (this.totalMatrixUnits() === 0) return;

    const grandTotal = this.matrixGrandTotal();
    const available = this.creditStatus().availableCredit;

    if (grandTotal > available && !this.allowCreditOverride) {
      this.toastMessage.set(`❌ Order of ${this.formatCurrency(grandTotal)} exceeds available credit of ${this.formatCurrency(available)}. Submission blocked.`);
      return;
    }

    const orderNum = `WPO-2026-${Math.floor(1000 + Math.random() * 9000)}`;
    const poNum = this.externalPoNumber() || `PO-${Date.now().toString().slice(-6)}`;
    const reqApproval = this.requiresApproval();

    // Construct matrix items
    const items: MatrixItem[] = [];
    const discountedPrice = this.discountedUnitPrice();
    for (const color of this.colors()) {
      for (const size of this.sizes()) {
        const qty = this.getCellQty(color, size);
        if (qty > 0) {
          items.push({
            sku: `WWR-POLO-${color.slice(0, 3).toUpperCase()}-${size}`,
            size,
            color,
            quantity: qty,
            baseUnitPrice: this.baseUnitPrice(),
            unitPrice: discountedPrice,
            lineTotal: Math.round(qty * discountedPrice * 100) / 100
          });
        }
      }
    }

    const newOrder: WholesaleOrder = {
      id: `ord-${Date.now()}`,
      orderNumber: orderNum,
      externalPoNumber: poNum,
      corporateAccountId: this.selectedAccount()!.id,
      companyName: this.selectedAccount()!.companyName,
      branchId: this.selectedBranch()!.id,
      branchName: this.selectedBranch()!.branchName,
      submittedByOfficerId: this.selectedOfficer()!.id,
      submitterName: this.selectedOfficer()!.name,
      submitterRole: this.selectedOfficer()!.role,
      status: reqApproval ? 'PendingCorporateApproval' : 'Confirmed',
      totalUnits: this.totalMatrixUnits(),
      subtotal: this.matrixSubtotal(),
      volumeDiscountPercent: this.appliedPricingTier().discountPercent,
      discountAmount: this.matrixDiscountAmount(),
      taxAmount: this.matrixTaxAmount(),
      shippingFee: this.matrixShippingFee(),
      grandTotal,
      creditHoldAmount: grandTotal,
      requiresApproval: reqApproval,
      approvalReason: reqApproval
        ? `Order of ${this.formatCurrency(grandTotal)} exceeds spending limit or company threshold.`
        : undefined,
      warehouseStockReserved: !reqApproval,
      items,
      createdAt: new Date().toISOString()
    };

    this.purchaseOrders.update(orders => [newOrder, ...orders]);

    // Record hold in ledger
    this.creditLedger.update(ledger => [
      {
        id: `led-${Date.now()}`,
        entryType: 'HoldPlaced',
        amount: grandTotal,
        referenceNumber: orderNum,
        referenceType: 'PurchaseOrder',
        notes: `Revolving credit hold placed on wholesale order ${orderNum} (${newOrder.status})`,
        createdAt: new Date().toISOString()
      },
      ...ledger
    ]);

    this.recalculateCreditStatus();

    if (reqApproval) {
      this.toastMessage.set(`Order ${orderNum} placed in PENDING APPROVAL queue. Routed to Department Director.`);
      this.activeTab.set('approvals');
    } else {
      this.toastMessage.set(`Order ${orderNum} CONFIRMED! Stock reserved in warehouse.`);
      this.activeTab.set('orders');
    }
  }

  approveOrder(orderId: string) {
    this.purchaseOrders.update(orders =>
      orders.map(o => {
        if (o.id === orderId) {
          return {
            ...o,
            status: 'Confirmed' as const,
            warehouseStockReserved: true,
            approvedByOfficerId: this.selectedOfficer()?.id,
            approverName: this.selectedOfficer()?.name ?? 'Department Director',
            approvedAt: new Date().toISOString()
          };
        }
        return o;
      })
    );
    this.toastMessage.set(`Order approved! Status updated to Confirmed and warehouse stock reserved.`);
  }

  openRejectionModal(orderId: string) {
    this.rejectingOrderId.set(orderId);
    this.rejectionReasonInput.set('');
    this.rejectionModalOpen.set(true);
  }

  confirmRejectOrder() {
    const id = this.rejectingOrderId();
    if (!id) return;

    const reason = this.rejectionReasonInput() || 'Rejected by corporate director.';
    let releasedAmount = 0;
    let orderNum = '';

    this.purchaseOrders.update(orders =>
      orders.map(o => {
        if (o.id === id) {
          releasedAmount = o.creditHoldAmount;
          orderNum = o.orderNumber;
          return {
            ...o,
            status: 'Rejected' as const,
            rejectionReason: reason,
            creditHoldAmount: 0,
            warehouseStockReserved: false
          };
        }
        return o;
      })
    );

    // Record hold release in ledger
    this.creditLedger.update(ledger => [
      {
        id: `led-${Date.now()}`,
        entryType: 'HoldReleased',
        amount: releasedAmount,
        referenceNumber: orderNum,
        referenceType: 'PurchaseOrder',
        notes: `Credit hold released for rejected order ${orderNum}: ${reason}`,
        createdAt: new Date().toISOString()
      },
      ...ledger
    ]);

    this.recalculateCreditStatus();
    this.rejectionModalOpen.set(false);
    this.toastMessage.set(`Order ${orderNum} rejected and ${this.formatCurrency(releasedAmount)} credit hold released.`);
  }

  reorderPrevious(order: WholesaleOrder) {
    // Populate matrix from previous order items
    const newMap: Record<string, number> = {};
    for (const item of order.items) {
      newMap[this.getCellKey(item.color, item.size)] = item.quantity;
    }
    this.matrixCellQuantities.set(newMap);
    this.externalPoNumber.set(`REORDER-${order.externalPoNumber}`);
    this.activeTab.set('matrix');
    this.toastMessage.set(`Loaded previous items from ${order.orderNumber} into matrix builder.`);
  }

  payInvoice(invoiceId: string, amount: number) {
    this.invoices.update(invs =>
      invs.map(i => {
        if (i.id === invoiceId) {
          return {
            ...i,
            amountPaid: i.totalAmount,
            balanceDue: 0,
            status: 'Paid' as const
          };
        }
        return i;
      })
    );

    const inv = this.invoices().find(i => i.id === invoiceId);
    if (inv) {
      this.creditLedger.update(ledger => [
        {
          id: `led-${Date.now()}`,
          entryType: 'InvoicePaid',
          amount,
          referenceNumber: inv.invoiceNumber,
          referenceType: 'Payment',
          notes: `Full payment received for invoice ${inv.invoiceNumber}. Revolving credit replenished.`,
          createdAt: new Date().toISOString()
        },
        ...ledger
      ]);
    }

    this.recalculateCreditStatus();
    this.toastMessage.set(`Invoice payment of ${this.formatCurrency(amount)} recorded! Available credit replenished.`);
  }

  recalculateCreditStatus() {
    const acc = this.selectedAccount();
    if (!acc) return;

    const unpaid = this.invoices()
      .filter(i => i.status !== 'Paid')
      .reduce((sum, i) => sum + i.balanceDue, 0);

    const holds = this.purchaseOrders()
      .filter(o => o.status === 'PendingCorporateApproval' || o.status === 'Confirmed' || o.status === 'Fulfilling')
      .reduce((sum, o) => sum + o.creditHoldAmount, 0);

    const totalCommitted = unpaid + holds;
    const available = Math.max(0, acc.creditLimit - totalCommitted);
    const limit = acc.creditLimit;
    const utilization = limit > 0 ? Math.min(100, Math.round((totalCommitted / limit) * 1000) / 10) : 0;

    this.creditStatus.set({
      creditLimit: limit,
      unpaidInvoicesTotal: Math.round(unpaid * 100) / 100,
      pendingOrderHoldsTotal: Math.round(holds * 100) / 100,
      availableCredit: Math.round(available * 100) / 100,
      creditUtilizationPercent: utilization,
      paymentTermsDays: acc.paymentTermsDays,
      paymentTermsLabel: `Net ${acc.paymentTermsDays}`,
      unpaidInvoicesCount: this.invoices().filter(i => i.status !== 'Paid').length,
      pendingHoldsCount: this.purchaseOrders().filter(o => o.status === 'PendingCorporateApproval' || o.status === 'Confirmed').length,
      isCreditExhausted: available <= 0.01
    });
  }

  getColorHex(color: string): string {
    const map: Record<string, string> = {
      'Navy': '#1e3a8a',
      'Charcoal': '#374151',
      'Black': '#0f172a',
      'Royal Blue': '#2563eb'
    };
    return map[color] || '#64748b';
  }

  formatCurrency(val: number): string {
    return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(val || 0);
  }

  getOrderStatusBadgeClass(status: string): string {
    switch (status) {
      case 'Confirmed':
        return 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30';
      case 'PendingCorporateApproval':
        return 'bg-amber-500/10 text-amber-400 border-amber-500/30';
      case 'Fulfilling':
        return 'bg-cyan-500/10 text-cyan-400 border-cyan-500/30';
      case 'Delivered':
        return 'bg-indigo-500/10 text-indigo-400 border-indigo-500/30';
      case 'Rejected':
        return 'bg-rose-500/10 text-rose-400 border-rose-500/30';
      default:
        return 'bg-slate-800 text-slate-400 border-slate-700';
    }
  }

  getLedgerTypeBadgeClass(type: string): string {
    switch (type) {
      case 'HoldPlaced':
        return 'bg-amber-500/20 text-amber-300';
      case 'HoldReleased':
        return 'bg-cyan-500/20 text-cyan-300';
      case 'InvoiceBilled':
        return 'bg-rose-500/20 text-rose-300';
      case 'InvoicePaid':
        return 'bg-emerald-500/20 text-emerald-300';
      default:
        return 'bg-slate-800 text-slate-300';
    }
  }
}
