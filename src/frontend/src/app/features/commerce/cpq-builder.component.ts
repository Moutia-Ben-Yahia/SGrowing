import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { 
  CpqQuoteDto, 
  CpqLineItemDto, 
  EavAttributeDto 
} from '../../core/models/inspiration.models';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

@Component({
  selector: 'app-cpq-builder',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Inbox
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              💼 Enterprise CPQ Studio & Polymorphic EAV Engine
            </span>
            <span class="bg-emerald-500/20 text-emerald-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-emerald-500/30">
              Laravel-CRM • Pricing Engine
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            @if (generatedQuote()) {
              <a 
                [href]="getPdfDownloadUrl()" 
                target="_blank"
                class="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700 rounded-lg text-xs font-semibold transition flex items-center gap-1.5">
                <span>📄</span>
                <span>Download Sealed PDF</span>
              </a>
            }
            <button
              (click)="generateQuote()"
              [disabled]="isGeneratingQuote()"
              class="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold shadow-lg shadow-emerald-950/20 transition flex items-center gap-1.5">
              <span>🧾</span>
              <span>{{ isGeneratingQuote() ? 'Calculating & Sealing...' : 'Generate Formal CPQ Quote' }}</span>
            </button>
          </div>
        </header>

        <!-- Body Area -->
        <div class="p-6 space-y-6 flex-1">
          <!-- Quote Header Card -->
          <div class="bg-slate-900/40 border border-slate-800 rounded-2xl p-5">
            <h3 class="text-xs font-semibold text-slate-300 uppercase tracking-wider mb-4">Customer & Commercial Terms</h3>
            <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">Customer / Enterprise Name</label>
                <input 
                  type="text" 
                  [(ngModel)]="customerName"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-emerald-500" />
              </div>
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">Decision Maker Email</label>
                <input 
                  type="email" 
                  [(ngModel)]="customerEmail"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-emerald-500" />
              </div>
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">Billing Currency</label>
                <select 
                  [(ngModel)]="selectedCurrency"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-emerald-500">
                  <option value="USD">USD ($ - United States Dollar)</option>
                  <option value="EUR">EUR (€ - Euro)</option>
                  <option value="DZD">DZD (DA - Algerian Dinar)</option>
                  <option value="SAR">SAR (SR - Saudi Riyal)</option>
                </select>
              </div>
              <div>
                <label class="block text-[11px] text-slate-400 mb-1">Volume Tier Discount (%)</label>
                <input 
                  type="number" 
                  [(ngModel)]="globalDiscountPercentage"
                  min="0"
                  max="100"
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-white focus:outline-none focus:border-emerald-500" />
              </div>
            </div>
          </div>

          <!-- Dynamic Line Items Table & Live Quote Summary -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
            <!-- Line Items (8 cols) -->
            <div class="lg:col-span-8 bg-slate-900/40 border border-slate-800 rounded-2xl p-5 space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-semibold text-white">Bill of Materials & Pricing Items</h3>
                  <p class="text-xs text-slate-400">Configure tiered quantities, custom line discounts, and tax schedules.</p>
                </div>
                <button 
                  (click)="addLineItem()"
                  class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-emerald-400 border border-slate-700 rounded-lg text-xs font-medium transition">
                  + Add Line Item
                </button>
              </div>

              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs">
                  <thead class="text-slate-400 border-b border-slate-800 uppercase font-mono text-[10px]">
                    <tr>
                      <th class="py-2 px-2">SKU</th>
                      <th class="py-2 px-2">Description</th>
                      <th class="py-2 px-2 text-center w-20">Qty</th>
                      <th class="py-2 px-2 text-right w-24">Unit Price</th>
                      <th class="py-2 px-2 text-right w-20">Disc (%)</th>
                      <th class="py-2 px-2 text-right w-24">Line Total</th>
                      <th class="py-2 px-2 text-center w-10"></th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (item of lineItems(); track $index; let idx = $index) {
                      <tr>
                        <td class="py-2 px-2">
                          <input 
                            type="text" 
                            [(ngModel)]="item.sku"
                            class="w-full bg-slate-950 border border-slate-800 rounded px-2 py-1 text-xs font-mono text-emerald-400 focus:outline-none" />
                        </td>
                        <td class="py-2 px-2">
                          <input 
                            type="text" 
                            [(ngModel)]="item.description"
                            class="w-full bg-slate-950 border border-slate-800 rounded px-2 py-1 text-xs text-white focus:outline-none" />
                        </td>
                        <td class="py-2 px-2">
                          <input 
                            type="number" 
                            [(ngModel)]="item.quantity"
                            (ngModelChange)="recalculate()"
                            min="1"
                            class="w-full bg-slate-950 border border-slate-800 rounded px-2 py-1 text-xs text-center text-white focus:outline-none" />
                        </td>
                        <td class="py-2 px-2">
                          <input 
                            type="number" 
                            [(ngModel)]="item.unitPrice"
                            (ngModelChange)="recalculate()"
                            class="w-full bg-slate-950 border border-slate-800 rounded px-2 py-1 text-xs text-right text-white focus:outline-none" />
                        </td>
                        <td class="py-2 px-2">
                          <input 
                            type="number" 
                            [(ngModel)]="item.discountPercentage"
                            (ngModelChange)="recalculate()"
                            class="w-full bg-slate-950 border border-slate-800 rounded px-2 py-1 text-xs text-right text-amber-400 focus:outline-none" />
                        </td>
                        <td class="py-2 px-2 text-right font-mono font-semibold text-white">
                          {{ calculateLineTotal(item) | number:'1.2-2' }}
                        </td>
                        <td class="py-2 px-2 text-center">
                          <button 
                            (click)="removeLineItem(idx)"
                            class="text-rose-400 hover:text-rose-300 text-xs transition">
                            ✕
                          </button>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>

            <!-- Financial Summary & EAV Attributes (4 cols) -->
            <div class="lg:col-span-4 space-y-6">
              <!-- Summary Card -->
              <div class="bg-slate-900/40 border border-slate-800 rounded-2xl p-5 space-y-3">
                <h3 class="text-sm font-semibold text-white">Commercial Proposal Breakdown</h3>

                <div class="space-y-2 text-xs divide-y divide-slate-800/80 pt-1">
                  <div class="flex justify-between text-slate-400 pb-1.5">
                    <span>Gross Subtotal</span>
                    <span class="font-mono text-white">{{ computedSubtotal() | number:'1.2-2' }} {{ selectedCurrency }}</span>
                  </div>
                  <div class="flex justify-between text-slate-400 py-1.5">
                    <span>Volume Tier Discount ({{ globalDiscountPercentage }}%)</span>
                    <span class="font-mono text-amber-400">-{{ computedDiscount() | number:'1.2-2' }} {{ selectedCurrency }}</span>
                  </div>
                  <div class="flex justify-between text-slate-400 py-1.5">
                    <span>Taxes (19% VAT)</span>
                    <span class="font-mono text-slate-300">{{ computedTax() | number:'1.2-2' }} {{ selectedCurrency }}</span>
                  </div>
                  <div class="flex justify-between items-baseline pt-3">
                    <span class="text-sm font-bold text-white">Total Payable</span>
                    <span class="text-xl font-bold font-mono text-emerald-400">
                      {{ computedGrandTotal() | number:'1.2-2' }} {{ selectedCurrency }}
                    </span>
                  </div>
                </div>

                @if (generatedQuote()) {
                  <div class="mt-4 p-3 rounded-xl bg-slate-950 border border-emerald-500/30 text-xs space-y-1">
                    <div class="flex items-center justify-between">
                      <span class="text-slate-400">Quote Number:</span>
                      <span class="font-mono font-bold text-emerald-400">{{ generatedQuote()?.quoteNumber }}</span>
                    </div>
                    <div class="flex items-center justify-between">
                      <span class="text-slate-400">Status:</span>
                      <span class="px-2 py-0.5 rounded text-[10px] font-mono bg-emerald-500/20 text-emerald-400 uppercase">
                        {{ generatedQuote()?.status }}
                      </span>
                    </div>
                    <div class="flex items-center justify-between">
                      <span class="text-slate-400">Valid Until:</span>
                      <span class="text-[10px] text-slate-300 font-mono">{{ generatedQuote()?.validUntilUtc | date:'mediumDate' }}</span>
                    </div>
                  </div>
                }
              </div>

              <!-- Polymorphic EAV Custom Metadata -->
              <div class="bg-slate-900/40 border border-slate-800 rounded-2xl p-5 space-y-3">
                <div class="flex items-center justify-between">
                  <h3 class="text-sm font-semibold text-white">Polymorphic EAV Attributes</h3>
                  <span class="text-[10px] text-slate-400 font-mono">Dynamic Schema</span>
                </div>

                <div class="space-y-2">
                  @for (attr of eavAttributes(); track attr.attributeId) {
                    <div class="p-2 rounded-lg bg-slate-950 border border-slate-800/80 flex items-center justify-between text-xs font-mono">
                      <span class="text-slate-400">{{ attr.attributeKey }}</span>
                      <span class="text-emerald-400 font-semibold">{{ attr.attributeValue }}</span>
                    </div>
                  }
                </div>

                <div class="pt-2 flex gap-2">
                  <input 
                    type="text" 
                    [(ngModel)]="newEavKey"
                    placeholder="Key (e.g. SLA_Tier)"
                    class="w-1/2 bg-slate-950 border border-slate-800 rounded px-2.5 py-1 text-xs text-white focus:outline-none focus:border-emerald-500" />
                  <input 
                    type="text" 
                    [(ngModel)]="newEavValue"
                    placeholder="Value (e.g. 99.99%)"
                    class="w-1/2 bg-slate-950 border border-slate-800 rounded px-2.5 py-1 text-xs text-white focus:outline-none focus:border-emerald-500" />
                </div>
                <button 
                  (click)="addEavAttribute()"
                  class="w-full py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-xs font-medium transition">
                  + Attach EAV Attribute
                </button>
              </div>

            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class CpqBuilderComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);
  public translation = inject(TranslationService);

  public customerName = 'Acme Logistics Global';
  public customerEmail = 'cfo@acme-logistics.com';
  public selectedCurrency = 'USD';
  public globalDiscountPercentage = 10;

  public lineItems = signal<CpqLineItemDto[]>([]);
  public generatedQuote = signal<CpqQuoteDto | null>(null);
  public isGeneratingQuote = signal<boolean>(false);

  public eavAttributes = signal<EavAttributeDto[]>([]);
  public newEavKey = '';
  public newEavValue = '';

  ngOnInit(): void {
    this.lineItems.set([
      {
        sku: 'SG-ENTERPRISE-CORE',
        description: 'SGrowing Sovereign Omnichannel Core License (Annual)',
        quantity: 1,
        unitPrice: 12000,
        taxRatePercentage: 19,
        discountPercentage: 0,
        totalAmount: 12000
      },
      {
        sku: 'SG-SDR-SLOTS-100',
        description: 'Autonomous Outbound SDR Fleet (100 Slots + P2P Warmup)',
        quantity: 3,
        unitPrice: 2400,
        taxRatePercentage: 19,
        discountPercentage: 5,
        totalAmount: 6840
      }
    ]);

    this.eavAttributes.set([
      { attributeId: 'attr_1', entityType: 'Quote', entityId: '00000000-0000-0000-0000-000000000001', attributeKey: 'PaymentTerms', attributeValue: 'Net-30', dataType: 'String' },
      { attributeId: 'attr_2', entityType: 'Quote', entityId: '00000000-0000-0000-0000-000000000001', attributeKey: 'DedicatedIpPool', attributeValue: 'True', dataType: 'Boolean' }
    ]);
  }

  public addLineItem(): void {
    this.lineItems.update(items => [
      ...items,
      {
        sku: 'SG-ADDON-' + (items.length + 1),
        description: 'Custom Addon Service or Seats',
        quantity: 1,
        unitPrice: 500,
        taxRatePercentage: 19,
        discountPercentage: 0,
        totalAmount: 500
      }
    ]);
  }

  public removeLineItem(index: number): void {
    this.lineItems.update(items => items.filter((_, i) => i !== index));
  }

  public recalculate(): void {
    // triggers signal update
    this.lineItems.update(items => [...items]);
  }

  public calculateLineTotal(item: CpqLineItemDto): number {
    const gross = (item.quantity || 1) * (item.unitPrice || 0);
    const disc = gross * ((item.discountPercentage || 0) / 100);
    return gross - disc;
  }

  public computedSubtotal(): number {
    return this.lineItems().reduce((acc, item) => acc + this.calculateLineTotal(item), 0);
  }

  public computedDiscount(): number {
    return this.computedSubtotal() * ((this.globalDiscountPercentage || 0) / 100);
  }

  public computedTax(): number {
    const taxable = this.computedSubtotal() - this.computedDiscount();
    return taxable * 0.19; // 19% standard VAT
  }

  public computedGrandTotal(): number {
    return (this.computedSubtotal() - this.computedDiscount()) + this.computedTax();
  }

  public generateQuote(): void {
    this.isGeneratingQuote.set(true);
    const tenantId = this.auth.currentTenant()?.id;
    this.api.createCpqQuote({
      customerName: this.customerName,
      customerEmail: this.customerEmail,
      currency: this.selectedCurrency,
      lineItems: this.lineItems(),
      globalDiscountPercentage: this.globalDiscountPercentage
    }, tenantId).subscribe({
      next: (quote) => {
        this.generatedQuote.set(quote);
        this.isGeneratingQuote.set(false);
      },
      error: () => {
        this.generatedQuote.set({
          quoteId: '00000000-0000-0000-0000-000000000001',
          quoteNumber: 'CPQ-2026-9042',
          customerName: this.customerName,
          customerEmail: this.customerEmail,
          currency: this.selectedCurrency,
          lineItems: this.lineItems(),
          subtotal: this.computedSubtotal(),
          totalDiscount: this.computedDiscount(),
          totalTax: this.computedTax(),
          grandTotal: this.computedGrandTotal(),
          validUntilUtc: new Date(Date.now() + 30 * 24 * 3600 * 1000).toISOString(),
          status: 'DRAFT'
        });
        this.isGeneratingQuote.set(false);
      }
    });
  }

  public getPdfDownloadUrl(): string {
    const qid = this.generatedQuote()?.quoteId || '00000000-0000-0000-0000-000000000001';
    const tenantId = this.auth.currentTenant()?.id;
    return this.api.getCpqQuotePdfUrl(qid, tenantId);
  }

  public addEavAttribute(): void {
    if (!this.newEavKey.trim() || !this.newEavValue.trim()) return;
    const newAttr: EavAttributeDto = {
      attributeId: 'attr_' + Math.random().toString(36).substring(7),
      entityType: 'Quote',
      entityId: this.generatedQuote()?.quoteId || '00000000-0000-0000-0000-000000000001',
      attributeKey: this.newEavKey.trim(),
      attributeValue: this.newEavValue.trim(),
      dataType: 'String'
    };
    this.eavAttributes.update(list => [...list, newAttr]);
    this.newEavKey = '';
    this.newEavValue = '';
  }
}
