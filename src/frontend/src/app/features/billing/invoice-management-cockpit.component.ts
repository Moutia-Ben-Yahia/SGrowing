import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface InvoiceLineItem {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface InvoiceItem {
  id: string;
  invoiceNumber: string;
  clientName: string;
  clientEmail?: string;
  currency: string;
  exchangeRate: number;
  subTotal: number;
  taxPercentage: number;
  taxAmount: number;
  totalAmount: number;
  totalBaseAmountUsd: number;
  status: 'Draft' | 'Sent' | 'Paid' | 'Overdue';
  dueDateUtc: string;
  paidAtUtc?: string;
  notes?: string;
  lineItems: InvoiceLineItem[];
}

export interface CurrencyRate {
  code: string;
  name: string;
  rateToUsd: number;
  flag: string;
}

@Component({
  selector: 'app-invoice-management-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Top Navigation -->
      <header class="border-b border-slate-800 bg-slate-900/60 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-50">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-slate-200">Enterprise Invoicing & Tax Ledger</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-medium">
              Multi-Currency FX
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <button (click)="openCreateModal.set(true)" class="px-3.5 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-2 shadow-lg shadow-emerald-900/20">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/></svg>
            Generate Invoice
          </button>
          <app-language-switcher />
        </div>
      </header>

      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- Financial Metrics Grid -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 relative overflow-hidden">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Total Invoiced (USD)</div>
            <div class="text-2xl font-bold text-white mt-2">\${{ totalInvoicedUsd().toLocaleString('en-US', { minimumFractionDigits: 2 }) }}</div>
            <div class="text-xs text-slate-500 mt-1">{{ invoices().length }} total enterprise invoices</div>
            <div class="absolute -right-2 -bottom-2 w-16 h-16 bg-emerald-500/5 rounded-full blur-xl pointer-events-none"></div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 relative overflow-hidden">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Collected Revenue</div>
            <div class="text-2xl font-bold text-emerald-400 mt-2">\${{ totalPaidUsd().toLocaleString('en-US', { minimumFractionDigits: 2 }) }}</div>
            <div class="text-xs text-emerald-500/80 mt-1">{{ paidCount() }} settled invoices</div>
            <div class="absolute -right-2 -bottom-2 w-16 h-16 bg-cyan-500/5 rounded-full blur-xl pointer-events-none"></div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 relative overflow-hidden">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Tax & VAT Ledger (19%)</div>
            <div class="text-2xl font-bold text-amber-400 mt-2">\${{ totalTaxUsd().toLocaleString('en-US', { minimumFractionDigits: 2 }) }}</div>
            <div class="text-xs text-amber-500/80 mt-1">Cross-border fiscal liability</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 relative overflow-hidden">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Active Base Currencies</div>
            <div class="flex items-center gap-2 mt-2">
              <span *ngFor="let cur of currencyRates()" class="text-xs px-2 py-0.5 bg-slate-800 rounded text-slate-300 font-mono">
                {{ cur.flag }} {{ cur.code }}
              </span>
            </div>
            <div class="text-xs text-slate-500 mt-2">Live rate updates against USD</div>
          </div>
        </div>

        <!-- Deal to Invoice Quick Scaffolder Card -->
        <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5">
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <h3 class="text-base font-semibold text-slate-200">Autonomous CRM Deal-to-Invoice Engine</h3>
              <p class="text-xs text-slate-400 mt-1">
                Converts won deals into itemized B2B invoices with currency conversion (TND, MAD, EGP, EUR, USD) and fiscal VAT calculations.
              </p>
            </div>
            <div class="flex items-center gap-3">
              <select [(ngModel)]="quickDealCurrency" class="bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-emerald-500">
                <option *ngFor="let c of currencyRates()" [value]="c.code">{{ c.flag }} {{ c.code }} - {{ c.name }}</option>
              </select>
              <button (click)="createDemoInvoiceFromDeal()" class="px-4 py-2 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white rounded-lg text-xs font-medium transition shadow flex items-center gap-2">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
                Generate From Won Deal
              </button>
            </div>
          </div>
        </div>

        <!-- Invoices Ledger Table -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl overflow-hidden shadow-xl">
          <div class="p-4 border-b border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div class="flex items-center gap-3">
              <span class="text-sm font-semibold text-slate-200">Invoice Ledger</span>
              <div class="flex items-center bg-slate-950 border border-slate-800 rounded-lg p-0.5 text-xs">
                <button (click)="statusFilter.set('All')" [class.bg-slate-800]="statusFilter() === 'All'" [class.text-white]="statusFilter() === 'All'" class="px-2.5 py-1 rounded text-slate-400 hover:text-slate-200 transition">All</button>
                <button (click)="statusFilter.set('Draft')" [class.bg-slate-800]="statusFilter() === 'Draft'" [class.text-white]="statusFilter() === 'Draft'" class="px-2.5 py-1 rounded text-slate-400 hover:text-slate-200 transition">Draft</button>
                <button (click)="statusFilter.set('Sent')" [class.bg-slate-800]="statusFilter() === 'Sent'" [class.text-white]="statusFilter() === 'Sent'" class="px-2.5 py-1 rounded text-slate-400 hover:text-slate-200 transition">Sent</button>
                <button (click)="statusFilter.set('Paid')" [class.bg-slate-800]="statusFilter() === 'Paid'" [class.text-white]="statusFilter() === 'Paid'" class="px-2.5 py-1 rounded text-slate-400 hover:text-slate-200 transition">Paid</button>
              </div>
            </div>

            <div class="flex items-center gap-2">
              <input type="text" [(ngModel)]="searchQuery" placeholder="Search by invoice # or client..." class="bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-slate-700 w-64" />
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs border-collapse">
              <thead>
                <tr class="border-b border-slate-800 bg-slate-950/40 text-slate-400 font-semibold uppercase tracking-wider">
                  <th class="p-3.5">Invoice #</th>
                  <th class="p-3.5">Client & Email</th>
                  <th class="p-3.5">Subtotal</th>
                  <th class="p-3.5">Tax (VAT 19%)</th>
                  <th class="p-3.5">Total Amount</th>
                  <th class="p-3.5">Base (USD)</th>
                  <th class="p-3.5">Status</th>
                  <th class="p-3.5">Due Date</th>
                  <th class="p-3.5 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60">
                <tr *ngFor="let inv of filteredInvoices()" class="hover:bg-slate-800/30 transition">
                  <td class="p-3.5 font-mono text-emerald-400 font-semibold">{{ inv.invoiceNumber }}</td>
                  <td class="p-3.5">
                    <div class="font-medium text-slate-200">{{ inv.clientName }}</div>
                    <div class="text-[10px] text-slate-500">{{ inv.clientEmail || 'No email provided' }}</div>
                  </td>
                  <td class="p-3.5 font-mono text-slate-300">{{ inv.subTotal.toFixed(2) }} {{ inv.currency }}</td>
                  <td class="p-3.5 font-mono text-amber-400/90">{{ inv.taxAmount.toFixed(2) }} {{ inv.currency }}</td>
                  <td class="p-3.5 font-mono font-bold text-white">{{ inv.totalAmount.toFixed(2) }} {{ inv.currency }}</td>
                  <td class="p-3.5 font-mono text-slate-400">\${{ inv.totalBaseAmountUsd.toFixed(2) }}</td>
                  <td class="p-3.5">
                    <span [ngClass]="{
                      'bg-emerald-500/10 text-emerald-400 border-emerald-500/20': inv.status === 'Paid',
                      'bg-sky-500/10 text-sky-400 border-sky-500/20': inv.status === 'Sent',
                      'bg-slate-800 text-slate-400 border-slate-700': inv.status === 'Draft',
                      'bg-red-500/10 text-red-400 border-red-500/20': inv.status === 'Overdue'
                    }" class="px-2 py-0.5 rounded-full border text-[11px] font-medium">
                      {{ inv.status }}
                    </span>
                  </td>
                  <td class="p-3.5 text-slate-400">{{ inv.dueDateUtc | date:'shortDate' }}</td>
                  <td class="p-3.5 text-right space-x-2">
                    <button *ngIf="inv.status !== 'Paid'" (click)="markAsPaid(inv)" class="px-2.5 py-1 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/30 rounded text-[11px] font-medium transition">
                      Pay
                    </button>
                    <button (click)="viewDetails(inv)" class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-[11px] font-medium transition">
                      View
                    </button>
                  </td>
                </tr>

                <tr *ngIf="filteredInvoices().length === 0">
                  <td colspan="9" class="p-8 text-center text-slate-500">
                    No invoices found matching criteria.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- Currency Converter & Tax Calculator Section -->
        <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 space-y-4">
            <h4 class="text-sm font-semibold text-slate-200 flex items-center gap-2">
              <svg class="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4"/></svg>
              Multi-Currency FX Converter
            </h4>
            <div class="grid grid-cols-3 gap-3">
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">Amount</label>
                <input type="number" [(ngModel)]="calcAmount" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200" />
              </div>
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">From</label>
                <select [(ngModel)]="calcFrom" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200">
                  <option *ngFor="let c of currencyRates()" [value]="c.code">{{ c.flag }} {{ c.code }}</option>
                </select>
              </div>
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">To</label>
                <select [(ngModel)]="calcTo" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200">
                  <option *ngFor="let c of currencyRates()" [value]="c.code">{{ c.flag }} {{ c.code }}</option>
                </select>
              </div>
            </div>
            <div class="p-3 bg-slate-950/60 rounded-lg border border-slate-800 flex items-center justify-between text-xs">
              <span class="text-slate-400">Converted Value:</span>
              <span class="font-mono text-emerald-400 font-bold text-sm">{{ calculatedConversion() }} {{ calcTo }}</span>
            </div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5 space-y-4">
            <h4 class="text-sm font-semibold text-slate-200 flex items-center gap-2">
              <svg class="w-4 h-4 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 7h6m0 10v-3m-3 3h.01M9 17h.01M9 14h.01M12 14h.01M15 11h.01M12 11h.01M9 11h.01M7 21h10a2 2 0 002-2V5a2 2 0 00-2-2H7a2 2 0 00-2 2v14a2 2 0 002 2z"/></svg>
              Fiscal Tax & VAT Ledger Compliance
            </h4>
            <p class="text-xs text-slate-400">
              Cross-border invoices automatically apply regional value-added tax rates with full transaction traceability for compliance with North African & European fiscal requirements.
            </p>
            <div class="grid grid-cols-2 gap-2 text-xs">
              <div class="p-2.5 bg-slate-950 rounded border border-slate-800 flex justify-between">
                <span class="text-slate-400">Standard VAT Rate:</span>
                <span class="text-slate-200 font-mono font-semibold">19.0%</span>
              </div>
              <div class="p-2.5 bg-slate-950 rounded border border-slate-800 flex justify-between">
                <span class="text-slate-400">Supported Currencies:</span>
                <span class="text-emerald-400 font-mono font-semibold">5 Active</span>
              </div>
            </div>
          </div>
        </div>
      </main>

      <!-- Create Invoice Modal -->
      <div *ngIf="openCreateModal()" class="fixed inset-0 bg-black/70 backdrop-blur-sm z-50 flex items-center justify-center p-4">
        <div class="bg-slate-900 border border-slate-800 rounded-xl max-w-lg w-full p-6 space-y-4 shadow-2xl">
          <div class="flex items-center justify-between pb-3 border-b border-slate-800">
            <h3 class="text-base font-semibold text-white">Create B2B Invoice</h3>
            <button (click)="openCreateModal.set(false)" class="text-slate-400 hover:text-white">✕</button>
          </div>

          <div class="space-y-3 text-xs">
            <div>
              <label class="block text-slate-400 mb-1">Client Name / Business *</label>
              <input type="text" [(ngModel)]="newClientName" placeholder="e.g. Apex Global Logistics SARL" class="w-full bg-slate-950 border border-slate-700 rounded px-3 py-2 text-slate-200" />
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-slate-400 mb-1">Client Email</label>
                <input type="email" [(ngModel)]="newClientEmail" placeholder="billing@apexlogistics.com" class="w-full bg-slate-950 border border-slate-700 rounded px-3 py-2 text-slate-200" />
              </div>
              <div>
                <label class="block text-slate-400 mb-1">Currency</label>
                <select [(ngModel)]="newCurrency" class="w-full bg-slate-950 border border-slate-700 rounded px-3 py-2 text-slate-200">
                  <option *ngFor="let c of currencyRates()" [value]="c.code">{{ c.flag }} {{ c.code }}</option>
                </select>
              </div>
            </div>

            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-slate-400 mb-1">Item Description</label>
                <input type="text" [(ngModel)]="newItemDesc" placeholder="e.g. Enterprise SLA Consulting" class="w-full bg-slate-950 border border-slate-700 rounded px-3 py-2 text-slate-200" />
              </div>
              <div>
                <label class="block text-slate-400 mb-1">Amount ({{ newCurrency }})</label>
                <input type="number" [(ngModel)]="newItemPrice" class="w-full bg-slate-950 border border-slate-700 rounded px-3 py-2 text-slate-200" />
              </div>
            </div>

            <div class="p-3 bg-slate-950 rounded border border-slate-800 space-y-1 text-slate-300">
              <div class="flex justify-between"><span>Subtotal:</span><span class="font-mono">{{ newItemPrice.toFixed(2) }} {{ newCurrency }}</span></div>
              <div class="flex justify-between text-amber-400"><span>VAT (19%):</span><span class="font-mono">{{ (newItemPrice * 0.19).toFixed(2) }} {{ newCurrency }}</span></div>
              <div class="flex justify-between font-bold text-white pt-1 border-t border-slate-800">
                <span>Total:</span>
                <span class="font-mono text-emerald-400">{{ (newItemPrice * 1.19).toFixed(2) }} {{ newCurrency }}</span>
              </div>
            </div>
          </div>

          <div class="flex justify-end gap-3 pt-3 border-t border-slate-800">
            <button (click)="openCreateModal.set(false)" class="px-4 py-2 bg-slate-800 text-slate-300 rounded text-xs hover:bg-slate-700">Cancel</button>
            <button (click)="submitManualInvoice()" class="px-4 py-2 bg-emerald-600 text-white rounded text-xs font-semibold hover:bg-emerald-500">Create & Issue</button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class InvoiceManagementCockpitComponent {
  translation = inject(TranslationService);

  statusFilter = signal<string>('All');
  searchQuery = '';
  openCreateModal = signal<boolean>(false);

  quickDealCurrency = 'USD';
  calcAmount = 1000;
  calcFrom = 'USD';
  calcTo = 'TND';

  newClientName = '';
  newClientEmail = '';
  newCurrency = 'USD';
  newItemDesc = 'Enterprise Growth Retainer';
  newItemPrice = 2500;

  currencyRates = signal<CurrencyRate[]>([
    { code: 'USD', name: 'US Dollar', rateToUsd: 1.00, flag: '🇺🇸' },
    { code: 'EUR', name: 'Euro', rateToUsd: 0.92, flag: '🇪🇺' },
    { code: 'TND', name: 'Tunisian Dinar', rateToUsd: 3.12, flag: '🇹🇳' },
    { code: 'MAD', name: 'Moroccan Dirham', rateToUsd: 10.15, flag: '🇲🇦' },
    { code: 'EGP', name: 'Egyptian Pound', rateToUsd: 48.50, flag: '🇪🇬' }
  ]);

  invoices = signal<InvoiceItem[]>([
    {
      id: 'inv-1',
      invoiceNumber: 'INV-20261028-1001',
      clientName: 'Atlas Freight Solutions',
      clientEmail: 'finance@atlasfreight.ma',
      currency: 'MAD',
      exchangeRate: 10.15,
      subTotal: 45000.00,
      taxPercentage: 19.0,
      taxAmount: 8550.00,
      totalAmount: 53550.00,
      totalBaseAmountUsd: 5275.86,
      status: 'Paid',
      dueDateUtc: new Date(Date.now() - 86400000 * 2).toISOString(),
      paidAtUtc: new Date().toISOString(),
      lineItems: [
        { id: 'l1', description: 'Omnichannel Logistics API License', quantity: 1, unitPrice: 45000.00, totalPrice: 45000.00 }
      ]
    },
    {
      id: 'inv-2',
      invoiceNumber: 'INV-20261028-1002',
      clientName: 'Sahara Retail Ventures',
      clientEmail: 'billing@sahararetail.tn',
      currency: 'TND',
      exchangeRate: 3.12,
      subTotal: 12500.00,
      taxPercentage: 19.0,
      taxAmount: 2375.00,
      totalAmount: 14875.00,
      totalBaseAmountUsd: 4767.63,
      status: 'Sent',
      dueDateUtc: new Date(Date.now() + 86400000 * 14).toISOString(),
      lineItems: [
        { id: 'l2', description: 'Multi-Hub Warehouse Inventory Orchestrator', quantity: 1, unitPrice: 12500.00, totalPrice: 12500.00 }
      ]
    },
    {
      id: 'inv-3',
      invoiceNumber: 'INV-20261028-1003',
      clientName: 'Nile FinTech Ltd',
      clientEmail: 'accounts@nilefintech.eg',
      currency: 'EGP',
      exchangeRate: 48.50,
      subTotal: 180000.00,
      taxPercentage: 19.0,
      taxAmount: 34200.00,
      totalAmount: 214200.00,
      totalBaseAmountUsd: 4416.49,
      status: 'Draft',
      dueDateUtc: new Date(Date.now() + 86400000 * 28).toISOString(),
      lineItems: [
        { id: 'l3', description: 'Compiled WhatsApp Multi-Device Gateway Fleet', quantity: 1, unitPrice: 180000.00, totalPrice: 180000.00 }
      ]
    }
  ]);

  totalInvoicedUsd = computed(() => {
    return this.invoices().reduce((acc, inv) => acc + inv.totalBaseAmountUsd, 0);
  });

  totalPaidUsd = computed(() => {
    return this.invoices()
      .filter(i => i.status === 'Paid')
      .reduce((acc, inv) => acc + inv.totalBaseAmountUsd, 0);
  });

  totalTaxUsd = computed(() => {
    return this.invoices().reduce((acc, inv) => {
      const taxInUsd = inv.exchangeRate > 0 ? inv.taxAmount / inv.exchangeRate : inv.taxAmount;
      return acc + taxInUsd;
    }, 0);
  });

  paidCount = computed(() => {
    return this.invoices().filter(i => i.status === 'Paid').length;
  });

  filteredInvoices = computed(() => {
    const filter = this.statusFilter();
    const query = this.searchQuery.toLowerCase().trim();

    return this.invoices().filter(inv => {
      const matchStatus = filter === 'All' || inv.status === filter;
      const matchQuery = !query || 
        inv.invoiceNumber.toLowerCase().includes(query) ||
        inv.clientName.toLowerCase().includes(query) ||
        (inv.clientEmail && inv.clientEmail.toLowerCase().includes(query));
      return matchStatus && matchQuery;
    });
  });

  calculatedConversion = computed(() => {
    const fromRate = this.currencyRates().find(c => c.code === this.calcFrom)?.rateToUsd || 1.0;
    const toRate = this.currencyRates().find(c => c.code === this.calcTo)?.rateToUsd || 1.0;
    const baseUsd = fromRate > 0 ? this.calcAmount / fromRate : this.calcAmount;
    const target = baseUsd * toRate;
    return target.toFixed(2);
  });

  createDemoInvoiceFromDeal() {
    const cur = this.quickDealCurrency;
    const rateObj = this.currencyRates().find(c => c.code === cur);
    const rate = rateObj?.rateToUsd || 1.0;
    const baseAmountUsd = 4000;
    const subtotal = Math.round(baseAmountUsd * rate);
    const tax = Math.round(subtotal * 0.19);
    const total = subtotal + tax;

    const newInv: InvoiceItem = {
      id: `inv-${Date.now()}`,
      invoiceNumber: `INV-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(1000 + Math.random() * 9000)}`,
      clientName: 'Global Expansion Deal #44',
      clientEmail: 'billing@globalclient.com',
      currency: cur,
      exchangeRate: rate,
      subTotal: subtotal,
      taxPercentage: 19.0,
      taxAmount: tax,
      totalAmount: total,
      totalBaseAmountUsd: Math.round(total / rate),
      status: 'Sent',
      dueDateUtc: new Date(Date.now() + 86400000 * 30).toISOString(),
      lineItems: [
        { id: `l-${Date.now()}`, description: 'Contracted CRM Growth Solution', quantity: 1, unitPrice: subtotal, totalPrice: subtotal }
      ]
    };

    this.invoices.update(list => [newInv, ...list]);
  }

  submitManualInvoice() {
    if (!this.newClientName) return;

    const rateObj = this.currencyRates().find(c => c.code === this.newCurrency);
    const rate = rateObj?.rateToUsd || 1.0;
    const subtotal = this.newItemPrice;
    const tax = Math.round(subtotal * 0.19);
    const total = subtotal + tax;

    const newInv: InvoiceItem = {
      id: `inv-${Date.now()}`,
      invoiceNumber: `INV-${new Date().toISOString().slice(0, 10).replace(/-/g, '')}-${Math.floor(1000 + Math.random() * 9000)}`,
      clientName: this.newClientName,
      clientEmail: this.newClientEmail || undefined,
      currency: this.newCurrency,
      exchangeRate: rate,
      subTotal: subtotal,
      taxPercentage: 19.0,
      taxAmount: tax,
      totalAmount: total,
      totalBaseAmountUsd: Math.round(total / rate),
      status: 'Draft',
      dueDateUtc: new Date(Date.now() + 86400000 * 30).toISOString(),
      lineItems: [
        { id: `l-${Date.now()}`, description: this.newItemDesc, quantity: 1, unitPrice: subtotal, totalPrice: subtotal }
      ]
    };

    this.invoices.update(list => [newInv, ...list]);
    this.openCreateModal.set(false);
    this.newClientName = '';
  }

  markAsPaid(inv: InvoiceItem) {
    this.invoices.update(list =>
      list.map(i => i.id === inv.id ? { ...i, status: 'Paid', paidAtUtc: new Date().toISOString() } : i)
    );
  }

  viewDetails(inv: InvoiceItem) {
    alert(`Invoice Details:\nNumber: ${inv.invoiceNumber}\nClient: ${inv.clientName}\nTotal: ${inv.totalAmount} ${inv.currency} (USD: $${inv.totalBaseAmountUsd})\nStatus: ${inv.status}`);
  }
}
