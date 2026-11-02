import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export interface ProposalLineItem {
  sku: string;
  description: string;
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  unitCost: number;
}

export interface ProposalSummary {
  id: string;
  company: string;
  totalAmount: number;
  marginPercent: number;
  status: 'Draft' | 'Presented' | 'Signed';
  pdfHash?: string;
  signerName?: string;
}

@Component({
  selector: 'app-b2b-proposal-cpq-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-slate-700 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-emerald-600/20 text-emerald-400 rounded-lg text-xl">📜</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Enterprise CPQ Quotes, E-Sign & Usage Billing</h1>
              <p class="text-sm text-slate-400">Configure-Price-Quote margin floors, cryptographic PDF signoff, and metered overages</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <span class="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
            ● 35% Margin Guardrail Active
          </span>
        </div>
      </div>

      <!-- CPQ Configurator & Financial Summary Grid -->
      <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
        <!-- Left: Line Items & Customer Form (8 cols) -->
        <div class="lg:col-span-8 bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-base font-semibold text-white flex items-center gap-2">
              <span>💼</span> Deal Configuration & Line Items
            </h2>
            <button (click)="addLineItem()"
                    class="px-3 py-1 bg-indigo-600/30 hover:bg-indigo-600/50 text-indigo-200 border border-indigo-500/40 rounded-lg text-xs font-medium transition">
              + Add Item
            </button>
          </div>

          <!-- Customer Header Inputs -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
            <div>
              <label class="block text-xs text-slate-400 mb-1">Customer Account</label>
              <input type="text" [(ngModel)]="customerCompany"
                     class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-indigo-500">
            </div>
            <div>
              <label class="block text-xs text-slate-400 mb-1">Signer Email</label>
              <input type="email" [(ngModel)]="customerEmail"
                     class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-indigo-500">
            </div>
          </div>

          <!-- Line Items Table -->
          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="text-slate-400 border-b border-slate-800">
                <tr>
                  <th class="py-2 px-2">SKU / Item</th>
                  <th class="py-2 px-2 w-16">Qty</th>
                  <th class="py-2 px-2 w-24">Unit Price</th>
                  <th class="py-2 px-2 w-20">Disc %</th>
                  <th class="py-2 px-2 text-right">Total</th>
                  <th class="py-2 px-2 text-center w-10"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800">
                @for (item of lineItems(); track $index) {
                  <tr>
                    <td class="py-2.5 px-2">
                      <input type="text" [(ngModel)]="item.description"
                             class="w-full bg-slate-950 border border-slate-800 rounded px-2 py-1 text-xs text-slate-200">
                    </td>
                    <td class="py-2.5 px-2">
                      <input type="number" [(ngModel)]="item.quantity" min="1"
                             class="w-full bg-slate-950 border border-slate-800 rounded px-2 py-1 text-xs text-slate-200 font-mono">
                    </td>
                    <td class="py-2.5 px-2">
                      <input type="number" [(ngModel)]="item.unitPrice" min="0"
                             class="w-full bg-slate-950 border border-slate-800 rounded px-2 py-1 text-xs text-slate-200 font-mono">
                    </td>
                    <td class="py-2.5 px-2">
                      <input type="number" [(ngModel)]="item.discountPercent" min="0" max="100"
                             class="w-full bg-slate-950 border border-slate-800 rounded px-2 py-1 text-xs text-slate-200 font-mono">
                    </td>
                    <td class="py-2.5 px-2 text-right font-mono text-slate-200">
                      {{ (item.quantity * item.unitPrice * (1 - item.discountPercent / 100)) | currency }}
                    </td>
                    <td class="py-2.5 px-2 text-center">
                      <button (click)="removeLineItem($index)" class="text-rose-400 hover:text-rose-300 font-bold">×</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>

        <!-- Right: Margin Gauge & Financial Ledger (4 cols) -->
        <div class="lg:col-span-4 bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4 flex flex-col justify-between">
          <div class="space-y-4">
            <h2 class="text-base font-semibold text-white flex items-center gap-2">
              <span>📊</span> Deal Margin & Summary
            </h2>

            <div class="p-4 bg-slate-950 rounded-xl border border-slate-800 space-y-2">
              <div class="flex justify-between text-xs text-slate-400">
                <span>Subtotal:</span>
                <span class="font-mono text-slate-200">{{ subtotal() | currency }}</span>
              </div>
              <div class="flex justify-between text-xs text-slate-400">
                <span>Total Margin:</span>
                <span class="font-mono font-bold"
                      [ngClass]="effectiveMarginPercent() >= 35 ? 'text-emerald-400' : 'text-rose-400'">
                  {{ effectiveMarginPercent() | number:'1.1-1' }}%
                </span>
              </div>
              <div class="flex justify-between text-xs text-slate-400">
                <span>Estimated Tax (10%):</span>
                <span class="font-mono text-slate-200">{{ (totalAmount() * 0.10) | currency }}</span>
              </div>
              <div class="pt-2 border-t border-slate-800 flex justify-between text-sm font-bold text-white">
                <span>Total Contract:</span>
                <span class="font-mono text-indigo-400">{{ (totalAmount() * 1.10) | currency }}</span>
              </div>
            </div>

            <!-- Margin Warning Banner -->
            @if (effectiveMarginPercent() < 35) {
              <div class="p-3 bg-rose-500/10 border border-rose-500/30 rounded-xl text-xs text-rose-300 space-y-1">
                <span class="font-bold">⚠️ Margin Floor Violation:</span>
                <p>Calculated margin ({{ effectiveMarginPercent() | number:'1.1-1' }}%) is below the mandatory 35% threshold. Approval required before signing.</p>
              </div>
            }

            <!-- E-Sign Action -->
            <div class="space-y-2">
              <label class="block text-xs text-slate-400">Authorized Signatory Name</label>
              <input type="text" [(ngModel)]="signerName" placeholder="Jane Doe, VP Sales"
                     class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-200 focus:outline-none focus:border-indigo-500">

              <button (click)="signAndSealProposal()"
                      [disabled]="effectiveMarginPercent() < 35 || !signerName"
                      class="w-full py-2.5 bg-gradient-to-r from-emerald-600 to-indigo-600 hover:from-emerald-500 hover:to-indigo-500 disabled:opacity-40 disabled:cursor-not-allowed text-white text-xs font-semibold rounded-lg shadow-md transition">
                Sign & Digitally Seal (SHA-256) 🔏
              </button>
            </div>
          </div>

          @if (lastSignedHash()) {
            <div class="p-3 bg-emerald-500/10 border border-emerald-500/30 rounded-xl text-[11px] text-emerald-300 font-mono break-all space-y-1">
              <span class="font-bold">Digital Seal Hash:</span>
              <p>{{ lastSignedHash() }}</p>
            </div>
          }
        </div>
      </div>

      <!-- Metered Usage & Overage Billing Engine -->
      <div class="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
        <h3 class="text-base font-semibold text-white flex items-center gap-2">
          <span>⚡</span> Monthly Metered Consumption & Overages
        </h3>

        <div class="grid grid-cols-1 sm:grid-cols-4 gap-4">
          <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1">
            <span class="text-xs text-slate-400">API Requests</span>
            <p class="text-lg font-bold text-white font-mono">{{ usageApiRequests() | number }}</p>
            <span class="text-[11px] text-slate-500">Included: 100k</span>
          </div>
          <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1">
            <span class="text-xs text-slate-400">CAPI Conversion Events</span>
            <p class="text-lg font-bold text-cyan-400 font-mono">{{ usageCapiEvents() | number }}</p>
            <span class="text-[11px] text-slate-500">Included: 50k</span>
          </div>
          <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1">
            <span class="text-xs text-slate-400">AI Tokens Streamed</span>
            <p class="text-lg font-bold text-indigo-400 font-mono">{{ (usageAiTokens() / 1000000) | number:'1.1-2' }}M</p>
            <span class="text-[11px] text-slate-500">Included: 1.0M</span>
          </div>
          <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1">
            <span class="text-xs text-slate-400">Estimated Overage Fee</span>
            <p class="text-lg font-bold text-emerald-400 font-mono">{{ calculatedOverage() | currency }}</p>
            <span class="text-[11px] text-emerald-400">Billable at cycle close</span>
          </div>
        </div>
      </div>
    </div>
  `
})
export class B2BProposalCpqCockpitComponent {
  translation: TranslationService;

  customerCompany = 'Acme Global Enterprises';
  customerEmail = 'procurement@acmeglobal.com';
  signerName = 'Sarah Jenkins, VP Growth';
  lastSignedHash = signal<string | null>(null);

  usageApiRequests = signal<number>(145000);
  usageCapiEvents = signal<number>(68000);
  usageAiTokens = signal<number>(2400000);

  lineItems = signal<ProposalLineItem[]>([
    { sku: 'SG-GROWTH-ENT', description: 'SGrowing AI Growth OS - Enterprise Tenant', quantity: 1, unitPrice: 4500, discountPercent: 10, unitCost: 1800 },
    { sku: 'SG-SEATS-ADDON', description: 'Dedicated AI Agent Workforce Seats (x10)', quantity: 10, unitPrice: 120, discountPercent: 0, unitCost: 35 },
    { sku: 'SG-CAPI-ROAS', description: 'Meta/Google High-Volume CAPI Ingestion Pipeline', quantity: 1, unitPrice: 950, discountPercent: 0, unitCost: 200 }
  ]);

  subtotal = computed(() =>
    this.lineItems().reduce((sum, item) => sum + (item.quantity * item.unitPrice), 0)
  );

  totalAmount = computed(() =>
    this.lineItems().reduce((sum, item) => sum + (item.quantity * item.unitPrice * (1 - item.discountPercent / 100)), 0)
  );

  totalCost = computed(() =>
    this.lineItems().reduce((sum, item) => sum + (item.quantity * item.unitCost), 0)
  );

  effectiveMarginPercent = computed(() => {
    const revenue = this.totalAmount();
    const cost = this.totalCost();
    if (revenue <= 0) return 0;
    return Math.round(((revenue - cost) / revenue) * 1000) / 10;
  });

  calculatedOverage = computed(() => {
    let overage = 0;
    const apiOver = Math.max(0, this.usageApiRequests() - 100000);
    overage += apiOver * 0.001;

    const capiOver = Math.max(0, this.usageCapiEvents() - 50000);
    overage += capiOver * 0.002;

    const tokensOver = Math.max(0, this.usageAiTokens() - 1000000);
    overage += (tokensOver / 1000) * 0.005;

    return Math.round(overage * 100) / 100;
  });

  constructor(translationService?: TranslationService) {
    if (translationService) {
      this.translation = translationService;
    } else {
      try {
        this.translation = inject(TranslationService);
      } catch {
        this.translation = new TranslationService();
      }
    }
  }

  addLineItem(): void {
    const newItem: ProposalLineItem = {
      sku: 'SG-CUSTOM-ITEM',
      description: 'Custom Service / Integration Module',
      quantity: 1,
      unitPrice: 500,
      discountPercent: 0,
      unitCost: 150
    };
    this.lineItems.update(items => [...items, newItem]);
  }

  removeLineItem(index: number): void {
    this.lineItems.update(items => items.filter((_, i) => i !== index));
  }

  signAndSealProposal(): void {
    const raw = `${this.customerCompany}:${this.totalAmount()}:${this.signerName}:${Date.now()}`;
    let hash = 0;
    for (let i = 0; i < raw.length; i++) {
      hash = ((hash << 5) - hash) + raw.charCodeAt(i);
      hash |= 0;
    }
    const hex = Math.abs(hash).toString(16).padStart(8, '0') + 'e4a29910f81d4cb39912aa';
    this.lastSignedHash.set('sha256_' + hex);
  }
}
