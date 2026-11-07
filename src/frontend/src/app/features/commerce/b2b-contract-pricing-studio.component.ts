import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type PriceTierType = 'Standard' | 'VolumeDiscount' | 'CustomerSpecific' | 'ContractFixed';
export type QuoteApprovalStatus = 'Draft' | 'AutoApproved' | 'PendingManagerApproval' | 'PendingVpApproval' | 'Approved' | 'Rejected';

export interface VolumeDiscountTier {
  minQuantity: number;
  maxQuantity: number | null;
  unitPriceSar: number;
  discountPercent: number;
}

export interface ContractPriceListItem {
  sku: string;
  productName: string;
  basePriceSar: number;
  volumeTiers: VolumeDiscountTier[];
  minimumMarginPercent?: number;
}

export interface B2BContractPriceList {
  priceListId: string;
  tenantId: string;
  name: string;
  currency: string;
  targetAccountId: string | null;
  targetAccountName?: string;
  isDefault: boolean;
  items: ContractPriceListItem[];
  validFromUtc: string;
  validToUtc: string;
}

export interface B2BQuoteLineItem {
  sku: string;
  productName: string;
  quantity: number;
  unitPriceSar: number;
  lineTotalSar: number;
  marginPercent: number;
}

export interface B2BQuote {
  quoteId: string;
  tenantId: string;
  quoteNumber: string;
  accountId: string;
  accountName: string;
  contactName: string;
  contactEmail: string;
  totalGrossSar: number;
  totalDiscountSar: number;
  totalNetSar: number;
  status: QuoteApprovalStatus;
  items: B2BQuoteLineItem[];
  requiredApproverRole: string;
  createdAtUtc: string;
  notes?: string;
}

@Component({
  selector: 'app-b2b-contract-pricing-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen space-y-6" dir="ltr" data-testid="b2b-contract-pricing-studio">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-amber-400">
            <span class="inline-block w-2 h-2 rounded-full bg-amber-400 animate-pulse"></span>
            PRD G5 & F3 • Enterprise CPQ & Contract Pricing Engine
          </div>
          <h1 class="text-3xl font-extrabold text-white tracking-tight mt-1">
            B2B Contract Pricing Matrix & CPQ Studio
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Account hierarchy inheritance, multi-tier volume pricing curve, gross margin governance & automated multi-tier approval routing.
          </p>
        </div>

        <!-- Action / Fast Switcher -->
        <div class="flex items-center gap-2">
          <button
            (click)="setTab('calculator')"
            [class]="activeTab() === 'calculator' ? 'bg-amber-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-colors flex items-center gap-2"
            data-testid="tab-btn-calculator"
          >
            <span>🧮</span> CPQ Calculator
          </button>
          <button
            (click)="setTab('quotes')"
            [class]="activeTab() === 'quotes' ? 'bg-amber-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-colors flex items-center gap-2"
            data-testid="tab-btn-quotes"
          >
            <span>📋</span> Quotes ({{ quotes().length }})
          </button>
          <button
            (click)="setTab('pricelists')"
            [class]="activeTab() === 'pricelists' ? 'bg-amber-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
            class="px-4 py-2 rounded-lg text-sm font-medium transition-colors flex items-center gap-2"
            data-testid="tab-btn-pricelists"
          >
            <span>📊</span> Price Lists
          </button>
        </div>
      </div>

      <!-- Top KPI Stats -->
      <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4" data-testid="stat-active-pricelists">
          <div class="text-slate-400 text-xs font-medium uppercase tracking-wider">Active Contract Price Lists</div>
          <div class="text-2xl font-bold text-white mt-1">{{ activePriceListsCount() }}</div>
          <div class="text-amber-400 text-xs mt-1">Target Account & Default Wholesale</div>
        </div>

        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4" data-testid="stat-open-quotes">
          <div class="text-slate-400 text-xs font-medium uppercase tracking-wider">Open B2B Quotes</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ openQuotesCount() }}</div>
          <div class="text-slate-400 text-xs mt-1">Draft & In-Flight Deal Cycles</div>
        </div>

        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4" data-testid="stat-pending-approvals">
          <div class="text-slate-400 text-xs font-medium uppercase tracking-wider">Pending Approvals</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ pendingApprovalsCount() }}</div>
          <div class="text-slate-400 text-xs mt-1">Manager (>10%) & VP (>20%) Gates</div>
        </div>

        <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-4" data-testid="stat-pipeline-volume">
          <div class="text-slate-400 text-xs font-medium uppercase tracking-wider">Total B2B Pipeline Volume</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">
            {{ pipelineVolumeSar() | number:'1.0-0' }} SAR
          </div>
          <div class="text-slate-400 text-xs mt-1">Active Deal Contract Net Sum</div>
        </div>
      </div>

      <!-- TAB 1: Live CPQ Calculator -->
      @if (activeTab() === 'calculator') {
        <div class="grid grid-cols-1 lg:grid-cols-12 gap-6" data-testid="calculator-panel">
          <!-- Configuration Form -->
          <div class="lg:col-span-5 bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 space-y-4">
            <h2 class="text-lg font-bold text-white flex items-center gap-2">
              <span>⚙️</span> Quote Configuration
            </h2>

            <!-- Account Selector -->
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Target B2B Account</label>
              <select
                [ngModel]="selectedAccountId()"
                (ngModelChange)="selectedAccountId.set($event)"
                data-testid="select-account"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg p-2.5 text-sm text-white focus:outline-none focus:border-amber-500"
              >
                <option value="enterprise-retail-group">Enterprise Retail Group (Custom Contract - 12-36% Off)</option>
                <option value="standard-wholesale">Standard Wholesale (Default Matrix - 4-28% Off)</option>
              </select>
            </div>

            <!-- SKU Selector -->
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Product SKU</label>
              <select
                [ngModel]="selectedSku()"
                (ngModelChange)="selectedSku.set($event)"
                data-testid="select-sku"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg p-2.5 text-sm text-white focus:outline-none focus:border-amber-500"
              >
                <option value="SKU-PERFUME-OUD">SKU-PERFUME-OUD • Royal Oud Parfum 100ml (Base: 250 SAR)</option>
                <option value="SKU-AMBER-MUSK">SKU-AMBER-MUSK • Velvet Amber Musk 50ml (Base: 180 SAR)</option>
              </select>
            </div>

            <!-- Quantity with Presets -->
            <div>
              <div class="flex justify-between items-center mb-1">
                <label class="text-xs font-medium text-slate-300">Order Quantity (Units)</label>
                <span class="text-xs text-amber-400 font-semibold">{{ quantity() }} units</span>
              </div>
              <input
                type="number"
                min="1"
                max="10000"
                [ngModel]="quantity()"
                (ngModelChange)="setQuantity($event)"
                data-testid="input-quantity"
                class="w-full bg-slate-900 border border-slate-700 rounded-lg p-2.5 text-sm text-white focus:outline-none focus:border-amber-500"
              />

              <!-- Fast Quantity Preset Chips -->
              <div class="flex flex-wrap gap-2 mt-2">
                @for (preset of [25, 50, 100, 200, 300, 500]; track preset) {
                  <button
                    type="button"
                    (click)="setQuantity(preset)"
                    [class]="quantity() === preset ? 'bg-amber-500 text-slate-900 font-bold' : 'bg-slate-700/70 hover:bg-slate-700 text-slate-300'"
                    class="px-2.5 py-1 text-xs rounded transition-colors"
                  >
                    {{ preset }} Units
                  </button>
                }
              </div>
            </div>

            <!-- Contact Information -->
            <div class="pt-3 border-t border-slate-700/60 space-y-3">
              <div class="text-xs font-semibold uppercase text-slate-400 tracking-wider">Contact & Approval Metadata</div>
              <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <label class="block text-xs text-slate-400 mb-1">Contact Name</label>
                  <input
                    type="text"
                    [ngModel]="contactName()"
                    (ngModelChange)="contactName.set($event)"
                    data-testid="input-contact-name"
                    class="w-full bg-slate-900 border border-slate-700 rounded-lg p-2 text-xs text-white"
                  />
                </div>
                <div>
                  <label class="block text-xs text-slate-400 mb-1">Email</label>
                  <input
                    type="email"
                    [ngModel]="contactEmail()"
                    (ngModelChange)="contactEmail.set($event)"
                    data-testid="input-contact-email"
                    class="w-full bg-slate-900 border border-slate-700 rounded-lg p-2 text-xs text-white"
                  />
                </div>
              </div>
              <div>
                <label class="block text-xs text-slate-400 mb-1">Order Notes / Contract Rationale</label>
                <input
                  type="text"
                  [ngModel]="quoteNotes()"
                  (ngModelChange)="quoteNotes.set($event)"
                  data-testid="input-quote-notes"
                  class="w-full bg-slate-900 border border-slate-700 rounded-lg p-2 text-xs text-white"
                />
              </div>
            </div>

            <!-- Create Quote Button -->
            <div class="pt-2">
              <button
                (click)="createQuoteFromCalculator()"
                data-testid="btn-create-quote"
                class="w-full py-3 bg-gradient-to-r from-amber-600 to-amber-500 hover:from-amber-500 hover:to-amber-400 text-white font-bold rounded-lg shadow-lg shadow-amber-600/20 transition-all flex items-center justify-center gap-2"
              >
                <span>🚀</span> Generate Formal B2B Quote
              </button>
            </div>
          </div>

          <!-- Live Pricing Curve & Financial Breakdown -->
          <div class="lg:col-span-7 space-y-5">
            <!-- Applied Price List & Unit Price Card -->
            @let calc = calculation();
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5">
              <div class="flex items-center justify-between border-b border-slate-700/60 pb-3">
                <div>
                  <span class="text-xs uppercase font-medium text-slate-400">Applied Pricing Source</span>
                  <div class="text-base font-bold text-amber-400 mt-0.5" data-testid="applied-price-list-name">
                    {{ calc.appliedPriceListName }}
                  </div>
                </div>
                <div class="text-right">
                  <span class="text-xs uppercase font-medium text-slate-400">Governance Gate</span>
                  <div class="text-xs font-semibold mt-0.5 px-2.5 py-1 rounded-full inline-block"
                       [ngClass]="{
                         'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30': calc.routedStatus === 'AutoApproved',
                         'bg-amber-500/20 text-amber-400 border border-amber-500/30': calc.routedStatus === 'PendingManagerApproval',
                         'bg-purple-500/20 text-purple-400 border border-purple-500/30': calc.routedStatus === 'PendingVpApproval'
                       }"
                       data-testid="calc-approval-badge">
                    {{ calc.routedStatus }} ({{ calc.requiredRole }})
                  </div>
                </div>
              </div>

              <!-- Price Breakdown Metrics -->
              <div class="grid grid-cols-2 sm:grid-cols-4 gap-4 mt-4">
                <div>
                  <div class="text-xs text-slate-400">Effective Unit Price</div>
                  <div class="text-2xl font-extrabold text-white mt-1" data-testid="calc-unit-price">
                    {{ calc.unitPriceSar | number:'1.2-2' }} <span class="text-xs text-slate-400">SAR</span>
                  </div>
                  <div class="text-xs text-slate-500 line-through">Base: {{ calc.basePriceSar | number:'1.0-0' }} SAR</div>
                </div>

                <div>
                  <div class="text-xs text-slate-400">Volume Discount</div>
                  <div class="text-2xl font-extrabold text-emerald-400 mt-1" data-testid="calc-discount-percent">
                    {{ calc.discountPercent | number:'1.1-1' }}%
                  </div>
                  <div class="text-xs text-emerald-400/80">-{{ calc.discountAmountSar | number:'1.0-0' }} SAR total</div>
                </div>

                <div>
                  <div class="text-xs text-slate-400">Gross Total</div>
                  <div class="text-xl font-bold text-slate-300 mt-1" data-testid="calc-gross-total">
                    {{ calc.totalGrossSar | number:'1.0-0' }} <span class="text-xs text-slate-500">SAR</span>
                  </div>
                  <div class="text-xs text-slate-400">{{ quantity() }} × {{ calc.basePriceSar }} SAR</div>
                </div>

                <div>
                  <div class="text-xs text-slate-400">Net Contract Sum</div>
                  <div class="text-2xl font-extrabold text-amber-400 mt-1" data-testid="calc-net-total">
                    {{ calc.totalNetSar | number:'1.0-0' }} <span class="text-xs text-slate-400">SAR</span>
                  </div>
                  <div class="text-xs text-slate-400">Excl. VAT</div>
                </div>
              </div>

              <!-- Margin Guardrail Indicator -->
              <div class="mt-4 pt-4 border-t border-slate-700/60 flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="text-xs text-slate-400">Gross Margin Hurdle (15% Floor):</span>
                  <span class="px-2 py-0.5 rounded text-xs font-bold"
                        [ngClass]="calc.marginPercent >= 20 ? 'bg-emerald-500/20 text-emerald-400' : 'bg-rose-500/20 text-rose-400'"
                        data-testid="calc-margin-indicator">
                    {{ calc.marginPercent | number:'1.1-1' }}% Margin
                  </span>
                </div>
                <div class="text-xs text-slate-400">
                  Est. COGS: {{ calc.unitCostSar }} SAR/unit
                </div>
              </div>
            </div>

            <!-- Volume Discount Matrix Curve Visualization -->
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5">
              <h3 class="text-sm font-bold text-white mb-3 flex items-center justify-between">
                <span>📈 Volume Tier Discount Curve</span>
                <span class="text-xs text-slate-400 font-normal">Active highlight indicates matched band</span>
              </h3>

              <div class="grid grid-cols-1 md:grid-cols-3 gap-3">
                @for (tier of calc.tiers; track tier.minQuantity) {
                  <div
                    class="p-3.5 rounded-lg border transition-all"
                    [class]="tier.isMatched ? 'bg-amber-500/10 border-amber-500 text-white shadow-md shadow-amber-500/10' : 'bg-slate-900/60 border-slate-700/60 text-slate-400'"
                    [attr.data-testid]="'tier-card-' + tier.minQuantity"
                  >
                    <div class="flex justify-between items-center text-xs">
                      <span class="font-semibold" [class]="tier.isMatched ? 'text-amber-400' : 'text-slate-400'">
                        {{ tier.minQuantity }} - {{ tier.maxQuantity ? tier.maxQuantity : '∞' }} Units
                      </span>
                      @if (tier.isMatched) {
                        <span class="px-1.5 py-0.5 rounded text-[10px] bg-amber-500 text-slate-900 font-bold uppercase">Active Tier</span>
                      }
                    </div>
                    <div class="text-lg font-extrabold mt-1" [class]="tier.isMatched ? 'text-white' : 'text-slate-300'">
                      {{ tier.unitPriceSar }} SAR
                    </div>
                    <div class="text-xs mt-0.5 text-emerald-400 font-medium">
                      {{ tier.discountPercent }}% Savings
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>
        </div>
      }

      <!-- TAB 2: B2B Quotes & Approvals -->
      @if (activeTab() === 'quotes') {
        <div class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 space-y-4" data-testid="quotes-panel">
          <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <h2 class="text-lg font-bold text-white">B2B Quotes & Governance Queue</h2>
              <p class="text-xs text-slate-400">Review quotes, discount hurdles and perform Sign-Off approvals.</p>
            </div>

            <!-- Filter Chips -->
            <div class="flex items-center gap-2">
              @for (f of ['ALL', 'PendingManagerApproval', 'PendingVpApproval', 'Approved', 'Rejected']; track f) {
                <button
                  type="button"
                  (click)="setStatusFilter(f)"
                  [class]="statusFilter() === f ? 'bg-amber-600 text-white' : 'bg-slate-800 text-slate-400 hover:bg-slate-700'"
                  class="px-2.5 py-1 text-xs rounded-lg transition-colors font-medium"
                >
                  {{ f === 'ALL' ? 'All Quotes' : f }}
                </button>
              }
            </div>
          </div>

          <!-- Quotes Table -->
          <div class="overflow-x-auto">
            <table class="w-full text-left text-sm" data-testid="quotes-table">
              <thead class="text-xs uppercase bg-slate-900/80 text-slate-400 border-b border-slate-700">
                <tr>
                  <th class="py-3 px-4">Quote Number / Account</th>
                  <th class="py-3 px-4">Net Contract Sum</th>
                  <th class="py-3 px-4">Discount</th>
                  <th class="py-3 px-4">Line Items</th>
                  <th class="py-3 px-4">Status & Role</th>
                  <th class="py-3 px-4 text-right">Manager Sign-Off</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-700/60 text-slate-300">
                @for (q of filteredQuotes(); track q.quoteId) {
                  <tr class="hover:bg-slate-800/50 transition-colors" [attr.data-testid]="'quote-row-' + q.quoteId">
                    <td class="py-3 px-4">
                      <div class="font-bold text-white">{{ q.quoteNumber }}</div>
                      <div class="text-xs text-amber-400">{{ q.accountName }}</div>
                      <div class="text-xs text-slate-400">{{ q.contactName }} ({{ q.contactEmail }})</div>
                    </td>

                    <td class="py-3 px-4">
                      <div class="font-bold text-white">{{ q.totalNetSar | number:'1.0-0' }} SAR</div>
                      <div class="text-xs text-slate-500 line-through">Gross: {{ q.totalGrossSar | number:'1.0-0' }} SAR</div>
                    </td>

                    <td class="py-3 px-4">
                      <div class="font-semibold text-emerald-400">
                        {{ ((q.totalDiscountSar / q.totalGrossSar) * 100) | number:'1.1-1' }}%
                      </div>
                      <div class="text-xs text-slate-400">-{{ q.totalDiscountSar | number:'1.0-0' }} SAR</div>
                    </td>

                    <td class="py-3 px-4">
                      <div class="text-xs space-y-1">
                        @for (item of q.items; track item.sku) {
                          <div>
                            <span class="text-white font-medium">{{ item.quantity }}x</span>
                            {{ item.productName }}
                            <span class="text-slate-400">({{ item.unitPriceSar }} SAR/u, {{ item.marginPercent }}% margin)</span>
                          </div>
                        }
                      </div>
                    </td>

                    <td class="py-3 px-4">
                      <span class="px-2.5 py-1 rounded-full text-xs font-semibold inline-block"
                            [ngClass]="{
                              'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30': q.status === 'AutoApproved' || q.status === 'Approved',
                              'bg-amber-500/20 text-amber-400 border border-amber-500/30': q.status === 'PendingManagerApproval',
                              'bg-purple-500/20 text-purple-400 border border-purple-500/30': q.status === 'PendingVpApproval',
                              'bg-rose-500/20 text-rose-400 border border-rose-500/30': q.status === 'Rejected',
                              'bg-slate-700 text-slate-300': q.status === 'Draft'
                            }"
                            [attr.data-testid]="'quote-status-' + q.quoteId">
                        {{ q.status }}
                      </span>
                      <div class="text-[11px] text-slate-400 mt-0.5">Role: {{ q.requiredApproverRole }}</div>
                    </td>

                    <td class="py-3 px-4 text-right">
                      @if (q.status === 'PendingManagerApproval' || q.status === 'PendingVpApproval') {
                        <div class="flex items-center justify-end gap-2">
                          <button
                            (click)="approveQuote(q.quoteId)"
                            class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold rounded transition-colors"
                            [attr.data-testid]="'btn-approve-' + q.quoteId"
                          >
                            ✓ Approve
                          </button>
                          <button
                            (click)="rejectQuote(q.quoteId)"
                            class="px-3 py-1 bg-rose-600 hover:bg-rose-500 text-white text-xs font-semibold rounded transition-colors"
                            [attr.data-testid]="'btn-reject-' + q.quoteId"
                          >
                            ✕ Reject
                          </button>
                        </div>
                      } @else {
                        <span class="text-xs text-slate-500">Sign-Off Completed</span>
                      }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      }

      <!-- TAB 3: Price Lists & Volume Tiers -->
      @if (activeTab() === 'pricelists') {
        <div class="space-y-5" data-testid="pricelists-panel">
          @for (pl of priceLists(); track pl.priceListId) {
            <div class="bg-slate-800/80 border border-slate-700/60 rounded-xl p-5 space-y-4" [attr.data-testid]="'pricelist-card-' + pl.priceListId">
              <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-slate-700/60 pb-3">
                <div>
                  <div class="flex items-center gap-2">
                    <h3 class="text-base font-bold text-white">{{ pl.name }}</h3>
                    @if (pl.isDefault) {
                      <span class="px-2 py-0.5 rounded text-[10px] bg-blue-500/20 text-blue-400 border border-blue-500/30 uppercase font-bold">Default Wholesale</span>
                    } @else {
                      <span class="px-2 py-0.5 rounded text-[10px] bg-amber-500/20 text-amber-400 border border-amber-500/30 uppercase font-bold">Target Account Specific</span>
                    }
                  </div>
                  <div class="text-xs text-slate-400 mt-1">
                    Currency: <span class="text-white font-medium">{{ pl.currency }}</span> •
                    Target Account: <span class="text-amber-400 font-medium">{{ pl.targetAccountName || 'Universal B2B' }}</span> •
                    Valid Through: <span class="text-slate-300">{{ pl.validToUtc | date:'mediumDate' }}</span>
                  </div>
                </div>
              </div>

              <!-- SKUs & Volume Tiers -->
              @for (item of pl.items; track item.sku) {
                <div class="bg-slate-900/60 border border-slate-800 rounded-lg p-4">
                  <div class="flex justify-between items-center mb-3">
                    <div>
                      <span class="text-white font-bold text-sm">{{ item.productName }}</span>
                      <span class="text-xs text-slate-400 ml-2 font-mono">({{ item.sku }})</span>
                    </div>
                    <div class="text-right text-xs">
                      <span class="text-slate-400">Base Catalog Price: </span>
                      <span class="text-amber-400 font-bold">{{ item.basePriceSar }} SAR</span>
                    </div>
                  </div>

                  <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
                    @for (tier of item.volumeTiers; track tier.minQuantity) {
                      <div class="p-3 bg-slate-800/60 border border-slate-700/50 rounded-lg flex justify-between items-center">
                        <div>
                          <div class="text-xs text-slate-400">
                            {{ tier.minQuantity }} to {{ tier.maxQuantity ? tier.maxQuantity : '∞' }} Units
                          </div>
                          <div class="text-base font-bold text-white mt-0.5">{{ tier.unitPriceSar }} SAR</div>
                        </div>
                        <div class="text-right">
                          <span class="px-2 py-0.5 rounded text-xs font-semibold bg-emerald-500/20 text-emerald-400">
                            -{{ tier.discountPercent }}%
                          </span>
                        </div>
                      </div>
                    }
                  </div>
                </div>
              }
            </div>
          }
        </div>
      }
    </div>
  `
})
export class B2BContractPricingStudioComponent {
  // Navigation
  activeTab = signal<'calculator' | 'quotes' | 'pricelists'>('calculator');

  // Filter
  statusFilter = signal<string>('ALL');

  // Calculator inputs
  selectedAccountId = signal<string>('enterprise-retail-group');
  selectedSku = signal<string>('SKU-PERFUME-OUD');
  quantity = signal<number>(100);
  contactName = signal<string>('Omar Al-Ghamdi');
  contactEmail = signal<string>('omar@retailksa.com');
  quoteNotes = signal<string>('Direct bulk distribution order');

  // Price lists repository
  priceLists = signal<B2BContractPriceList[]>([
    {
      priceListId: 'pl-enterprise-01',
      tenantId: '11111111-1111-1111-1111-111111111111',
      name: 'Enterprise Retail Group Contract',
      currency: 'SAR',
      targetAccountId: 'aa000000-0000-0000-0000-000000000001',
      targetAccountName: 'Enterprise Retail Group',
      isDefault: false,
      items: [
        {
          sku: 'SKU-PERFUME-OUD',
          productName: 'Royal Oud Parfum 100ml',
          basePriceSar: 250,
          minimumMarginPercent: 15,
          volumeTiers: [
            { minQuantity: 1, maxQuantity: 50, unitPriceSar: 220, discountPercent: 12.0 },
            { minQuantity: 51, maxQuantity: 200, unitPriceSar: 190, discountPercent: 24.0 },
            { minQuantity: 201, maxQuantity: null, unitPriceSar: 160, discountPercent: 36.0 }
          ]
        },
        {
          sku: 'SKU-AMBER-MUSK',
          productName: 'Velvet Amber Musk 50ml',
          basePriceSar: 180,
          minimumMarginPercent: 15,
          volumeTiers: [
            { minQuantity: 1, maxQuantity: 50, unitPriceSar: 160, discountPercent: 11.1 },
            { minQuantity: 51, maxQuantity: 200, unitPriceSar: 140, discountPercent: 22.2 },
            { minQuantity: 201, maxQuantity: null, unitPriceSar: 115, discountPercent: 36.1 }
          ]
        }
      ],
      validFromUtc: new Date(Date.now() - 30 * 86400000).toISOString(),
      validToUtc: new Date(Date.now() + 365 * 86400000).toISOString()
    },
    {
      priceListId: 'pl-default-01',
      tenantId: '11111111-1111-1111-1111-111111111111',
      name: 'Standard B2B Wholesale List',
      currency: 'SAR',
      targetAccountId: null,
      targetAccountName: 'Universal B2B Wholesale',
      isDefault: true,
      items: [
        {
          sku: 'SKU-PERFUME-OUD',
          productName: 'Royal Oud Parfum 100ml',
          basePriceSar: 250,
          minimumMarginPercent: 15,
          volumeTiers: [
            { minQuantity: 1, maxQuantity: 50, unitPriceSar: 240, discountPercent: 4.0 },
            { minQuantity: 51, maxQuantity: 200, unitPriceSar: 210, discountPercent: 16.0 },
            { minQuantity: 201, maxQuantity: null, unitPriceSar: 180, discountPercent: 28.0 }
          ]
        },
        {
          sku: 'SKU-AMBER-MUSK',
          productName: 'Velvet Amber Musk 50ml',
          basePriceSar: 180,
          minimumMarginPercent: 15,
          volumeTiers: [
            { minQuantity: 1, maxQuantity: 50, unitPriceSar: 170, discountPercent: 5.6 },
            { minQuantity: 51, maxQuantity: 200, unitPriceSar: 155, discountPercent: 13.9 },
            { minQuantity: 201, maxQuantity: null, unitPriceSar: 135, discountPercent: 25.0 }
          ]
        }
      ],
      validFromUtc: new Date(Date.now() - 30 * 86400000).toISOString(),
      validToUtc: new Date(Date.now() + 365 * 86400000).toISOString()
    }
  ]);

  // Quotes repository
  quotes = signal<B2BQuote[]>([
    {
      quoteId: 'qt-1001',
      tenantId: '11111111-1111-1111-1111-111111111111',
      quoteNumber: 'B2B-QT-1001',
      accountId: 'aa000000-0000-0000-0000-000000000001',
      accountName: 'Enterprise Retail Group',
      contactName: 'Tariq Al-Mansoor',
      contactEmail: 'tariq@retailksa.com',
      totalGrossSar: 25000,
      totalDiscountSar: 6000,
      totalNetSar: 19000,
      status: 'PendingVpApproval',
      requiredApproverRole: 'VP_COMMERCE',
      createdAtUtc: new Date(Date.now() - 2 * 3600000).toISOString(),
      notes: '100 units Oud @ 190 SAR (24% discount)',
      items: [
        {
          sku: 'SKU-PERFUME-OUD',
          productName: 'Royal Oud Parfum 100ml',
          quantity: 100,
          unitPriceSar: 190,
          lineTotalSar: 19000,
          marginPercent: 34.2
        }
      ]
    },
    {
      quoteId: 'qt-1002',
      tenantId: '11111111-1111-1111-1111-111111111111',
      quoteNumber: 'B2B-QT-1002',
      accountId: 'bb000000-0000-0000-0000-000000000002',
      accountName: 'Gulf Wholesale Trading LLC',
      contactName: 'Fatima Zahra',
      contactEmail: 'fatima@gulfwholesale.com',
      totalGrossSar: 25000,
      totalDiscountSar: 4000,
      totalNetSar: 21000,
      status: 'PendingManagerApproval',
      requiredApproverRole: 'SALES_MANAGER',
      createdAtUtc: new Date(Date.now() - 5 * 3600000).toISOString(),
      notes: 'Standard Wholesale Q3 replenishment',
      items: [
        {
          sku: 'SKU-PERFUME-OUD',
          productName: 'Royal Oud Parfum 100ml',
          quantity: 100,
          unitPriceSar: 210,
          lineTotalSar: 21000,
          marginPercent: 40.5
        }
      ]
    },
    {
      quoteId: 'qt-1003',
      tenantId: '11111111-1111-1111-1111-111111111111',
      quoteNumber: 'B2B-QT-1003',
      accountId: 'bb000000-0000-0000-0000-000000000003',
      accountName: 'Najd Boutique Distribution',
      contactName: 'Saad Al-Khuraiji',
      contactEmail: 'saad@najdboutique.sa',
      totalGrossSar: 5000,
      totalDiscountSar: 200,
      totalNetSar: 4800,
      status: 'AutoApproved',
      requiredApproverRole: 'NONE',
      createdAtUtc: new Date(Date.now() - 24 * 3600000).toISOString(),
      notes: 'Initial test batch 20 units Oud',
      items: [
        {
          sku: 'SKU-PERFUME-OUD',
          productName: 'Royal Oud Parfum 100ml',
          quantity: 20,
          unitPriceSar: 240,
          lineTotalSar: 4800,
          marginPercent: 47.9
        }
      ]
    },
    {
      quoteId: 'qt-1004',
      tenantId: '11111111-1111-1111-1111-111111111111',
      quoteNumber: 'B2B-QT-1004',
      accountId: 'aa000000-0000-0000-0000-000000000001',
      accountName: 'Enterprise Retail Group',
      contactName: 'Noura Al-Hassan',
      contactEmail: 'noura@retailksa.com',
      totalGrossSar: 62500,
      totalDiscountSar: 22500,
      totalNetSar: 40000,
      status: 'Approved',
      requiredApproverRole: 'COMPLETED',
      createdAtUtc: new Date(Date.now() - 48 * 3600000).toISOString(),
      notes: 'Signed 250 units batch',
      items: [
        {
          sku: 'SKU-PERFUME-OUD',
          productName: 'Royal Oud Parfum 100ml',
          quantity: 250,
          unitPriceSar: 160,
          lineTotalSar: 40000,
          marginPercent: 21.9
        }
      ]
    }
  ]);

  // Top KPIs computeds
  activePriceListsCount = computed(() => this.priceLists().length);
  openQuotesCount = computed(() => this.quotes().filter(q => q.status !== 'Approved' && q.status !== 'Rejected').length);
  pendingApprovalsCount = computed(() => this.quotes().filter(q => q.status === 'PendingManagerApproval' || q.status === 'PendingVpApproval').length);
  pipelineVolumeSar = computed(() => this.quotes().filter(q => q.status !== 'Rejected').reduce((acc, q) => acc + q.totalNetSar, 0));

  // Live CPQ Calculation computed
  calculation = computed(() => {
    const accountId = this.selectedAccountId();
    const sku = this.selectedSku();
    const qty = Math.max(1, this.quantity() || 1);

    // Pick price list: custom account list first, then default
    let pl = this.priceLists().find(p => p.targetAccountId === 'aa000000-0000-0000-0000-000000000001' && accountId === 'enterprise-retail-group');
    if (!pl) {
      pl = this.priceLists().find(p => p.isDefault) || this.priceLists()[0];
    }

    const item = pl.items.find(i => i.sku === sku) || pl.items[0];
    const basePriceSar = item.basePriceSar;

    // Find volume tier
    const matchedTier = item.volumeTiers.find(t => qty >= t.minQuantity && (t.maxQuantity === null || qty <= t.maxQuantity));
    const unitPriceSar = matchedTier ? matchedTier.unitPriceSar : basePriceSar;

    const totalGrossSar = Math.round(basePriceSar * qty);
    const totalNetSar = Math.round(unitPriceSar * qty);
    const discountAmountSar = Math.max(0, totalGrossSar - totalNetSar);
    const discountPercent = totalGrossSar > 0 ? (discountAmountSar / totalGrossSar) * 100 : 0;

    const unitCostSar = Math.round(basePriceSar * 0.50);
    const marginPercent = unitPriceSar > 0 ? ((unitPriceSar - unitCostSar) / unitPriceSar) * 100 : 0;

    // Approval routing
    let routedStatus: QuoteApprovalStatus = 'AutoApproved';
    let requiredRole = 'NONE';
    if (discountPercent > 20) {
      routedStatus = 'PendingVpApproval';
      requiredRole = 'VP_COMMERCE';
    } else if (discountPercent > 10) {
      routedStatus = 'PendingManagerApproval';
      requiredRole = 'SALES_MANAGER';
    }

    const tiers = item.volumeTiers.map(t => ({
      ...t,
      isMatched: qty >= t.minQuantity && (t.maxQuantity === null || qty <= t.maxQuantity)
    }));

    return {
      appliedPriceListName: pl.name,
      productName: item.productName,
      sku: item.sku,
      basePriceSar,
      unitPriceSar,
      unitCostSar,
      totalGrossSar,
      totalNetSar,
      discountAmountSar,
      discountPercent,
      marginPercent,
      routedStatus,
      requiredRole,
      tiers
    };
  });

  // Filtered quotes computed
  filteredQuotes = computed(() => {
    const filter = this.statusFilter();
    const all = this.quotes();
    if (filter === 'ALL') {
      return all;
    }
    return all.filter(q => q.status === filter);
  });

  // Methods
  setTab(tab: 'calculator' | 'quotes' | 'pricelists') {
    this.activeTab.set(tab);
  }

  setQuantity(qty: number) {
    this.quantity.set(Math.max(1, qty));
  }

  setStatusFilter(filter: string) {
    this.statusFilter.set(filter);
  }

  createQuoteFromCalculator() {
    const calc = this.calculation();
    const qty = this.quantity();
    const accountName = this.selectedAccountId() === 'enterprise-retail-group'
      ? 'Enterprise Retail Group'
      : 'Standard Wholesale Trading Co.';
    const accountId = this.selectedAccountId() === 'enterprise-retail-group'
      ? 'aa000000-0000-0000-0000-000000000001'
      : 'bb000000-0000-0000-0000-000000000002';

    const newQuote: B2BQuote = {
      quoteId: `qt-${Date.now().toString().slice(-4)}`,
      tenantId: '11111111-1111-1111-1111-111111111111',
      quoteNumber: `B2B-QT-${Math.floor(1000 + Math.random() * 9000)}`,
      accountId,
      accountName,
      contactName: this.contactName(),
      contactEmail: this.contactEmail(),
      totalGrossSar: calc.totalGrossSar,
      totalDiscountSar: calc.discountAmountSar,
      totalNetSar: calc.totalNetSar,
      status: calc.routedStatus,
      requiredApproverRole: calc.requiredRole,
      createdAtUtc: new Date().toISOString(),
      notes: this.quoteNotes(),
      items: [
        {
          sku: calc.sku,
          productName: calc.productName,
          quantity: qty,
          unitPriceSar: calc.unitPriceSar,
          lineTotalSar: calc.totalNetSar,
          marginPercent: Math.round(calc.marginPercent * 10) / 10
        }
      ]
    };

    this.quotes.update(curr => [newQuote, ...curr]);
    this.activeTab.set('quotes');
  }

  approveQuote(quoteId: string) {
    this.quotes.update(curr =>
      curr.map(q => q.quoteId === quoteId
        ? { ...q, status: 'Approved' as QuoteApprovalStatus, requiredApproverRole: 'COMPLETED' }
        : q
      )
    );
  }

  rejectQuote(quoteId: string) {
    this.quotes.update(curr =>
      curr.map(q => q.quoteId === quoteId
        ? { ...q, status: 'Rejected' as QuoteApprovalStatus, requiredApproverRole: 'REJECTED' }
        : q
      )
    );
  }
}
