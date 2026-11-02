import { Component, inject, signal, computed, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslationService } from '../../core/services/translation.service';

export interface InvoiceItem {
  id: string;
  customerName: string;
  amount: number;
  tax: number;
  total: number;
  currency: string;
  status: 'Paid' | 'PastDue' | 'Uncollectible';
  dunningAttempts: number;
  sealHash: string;
}

export interface TaxRateItem {
  region: string;
  rate: string;
  rule: string;
}

@Component({
  selector: 'app-subscription-dunning-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" class="p-6 max-w-7xl mx-auto space-y-6 text-slate-100 font-sans">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-2">
            <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
              Horizon 19 • Domain G1 & G2
            </span>
            <span class="text-xs text-slate-400">Global Tax & Smart Dunning Recovery</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">
            Subscription Billing, Tax & Smart Dunning Studio
          </h1>
          <p class="text-sm text-slate-400">
            Automated EU VAT reverse-charge compliance, SHA-256 sealed invoicing, and progressive multi-channel dunning recovery.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button (click)="createDemoInvoice()"
                  class="px-3.5 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold shadow-lg shadow-indigo-600/30 transition">
            + Generate Test Invoice
          </button>
        </div>
      </div>

      <!-- Top Metrics -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Monthly Recovered MRR</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">\${{ recoveredTotal().toLocaleString() }}</div>
          <div class="text-xs text-emerald-400 mt-0.5">84.2% Dunning Recovery Rate</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Delinquent Invoices</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ pastDueInvoices().length }}</div>
          <div class="text-xs text-slate-500 mt-0.5">Awaiting Gateway Retry</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">EU VIES Exemption Total</div>
          <div class="text-2xl font-bold text-white mt-1">€18,400</div>
          <div class="text-xs text-slate-500 mt-0.5">0% Reverse Charge Applied</div>
        </div>

        <div class="bg-slate-900/60 border border-slate-800 p-4 rounded-2xl">
          <div class="text-xs text-slate-400 font-medium">Cryptographic Invoices</div>
          <div class="text-2xl font-bold text-indigo-400 mt-1">{{ invoices().length }}</div>
          <div class="text-xs text-slate-500 mt-0.5">SHA-256 Digital Seals</div>
        </div>
      </div>

      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Invoices & Dunning Queue (2 Cols) -->
        <div class="lg:col-span-2 space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">Invoices & Dunning Queue</h2>
            <span class="text-xs text-slate-500">Real-Time Invoicing Vault</span>
          </div>

          <div class="space-y-3">
            @for (inv of invoices(); track inv.id) {
              <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 flex flex-col md:flex-row md:items-center justify-between gap-4">
                <div class="space-y-1">
                  <div class="flex items-center gap-2">
                    <span class="font-bold text-sm text-white">{{ inv.customerName }}</span>
                    <span class="font-mono text-xs text-slate-400">({{ inv.id }})</span>
                    <span class="text-xs px-2 py-0.5 rounded-full font-semibold"
                          [ngClass]="inv.status === 'Paid' ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30' : 'bg-amber-500/20 text-amber-300 border border-amber-500/30'">
                      {{ inv.status }}
                    </span>
                  </div>
                  <div class="text-xs text-slate-400 flex items-center gap-3 font-mono">
                    <span>Subtotal: \${{ inv.amount }}</span>
                    <span>•</span>
                    <span>Tax: \${{ inv.tax }}</span>
                    <span>•</span>
                    <span>Total: \${{ inv.total }} {{ inv.currency }}</span>
                  </div>
                  <div class="text-[11px] text-slate-500 font-mono truncate max-w-sm">
                    Seal: {{ inv.sealHash }}
                  </div>
                </div>

                <div class="flex items-center gap-3">
                  @if (inv.status === 'PastDue') {
                    <div class="text-right">
                      <div class="text-xs text-slate-400">Dunning Attempt</div>
                      <div class="text-xs font-bold text-amber-400">#{{ inv.dunningAttempts }} / 3</div>
                    </div>

                    <button (click)="retryInvoicePayment(inv.id)"
                            class="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold rounded-xl transition shadow-md shadow-emerald-600/30">
                      Retry Gateway 💳
                    </button>
                  } @else {
                    <span class="text-xs text-emerald-400 font-semibold px-3 py-1.5 bg-emerald-500/10 rounded-xl">
                      Settled & Reconciled ✓
                    </span>
                  }
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Global Tax Engine Rules (1 Col) -->
        <div class="space-y-4">
          <h2 class="text-sm font-semibold text-slate-300 uppercase tracking-wider">Global Tax Jurisdictions</h2>

          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 space-y-3">
            @for (tax of taxRules(); track tax.region) {
              <div class="p-3 bg-slate-950/80 rounded-xl border border-slate-800/80 space-y-1 text-xs">
                <div class="flex items-center justify-between font-bold text-white">
                  <span>{{ tax.region }}</span>
                  <span class="text-indigo-400 font-mono">{{ tax.rate }}</span>
                </div>
                <p class="text-slate-400 text-[11px]">{{ tax.rule }}</p>
              </div>
            }

            <div class="p-3 bg-slate-950 rounded-xl text-xs text-slate-400 space-y-1">
              <div class="font-semibold text-slate-200">Autonomous Dunning Schedule:</div>
              <p>Day 1: Invisible payment retry • Day 3: Gentle email notification • Day 7: Urgent SMS notice & graceful plan suspension.</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class SubscriptionDunningStudioComponent {
  translation: TranslationService;
  private cdr?: ChangeDetectorRef;

  invoices = signal<InvoiceItem[]>([
    {
      id: 'inv_4401',
      customerName: 'SaaS Metrics Labs',
      amount: 1200,
      tax: 99,
      total: 1299,
      currency: 'USD',
      status: 'Paid',
      dunningAttempts: 0,
      sealHash: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'
    },
    {
      id: 'inv_4402',
      customerName: 'Quant Growth Partners',
      amount: 3500,
      tax: 0,
      total: 3500,
      currency: 'EUR',
      status: 'PastDue',
      dunningAttempts: 1,
      sealHash: 'a591a6d40bf420404a011733cfb7b190d62c65bf0bcda32b57b277d9ad9f146e'
    }
  ]);

  taxRules = signal<TaxRateItem[]>([
    { region: 'European Union (B2B)', rate: '0.0% (Reverse Charge)', rule: 'Requires validated VIES VAT registration number.' },
    { region: 'United States', rate: '8.25% Average', rule: 'State-level economic nexus thresholds applied automatically.' },
    { region: 'United Kingdom', rate: '20.0% Standard VAT', rule: 'HMRC digital tax reporting integration active.' },
    { region: 'Global / Export', rate: '0.0% Exempt', rule: 'International B2B software services exemption applied.' }
  ]);

  pastDueInvoices = computed(() => this.invoices().filter(i => i.status === 'PastDue'));
  recoveredTotal = computed(() => {
    return this.invoices().filter(i => i.status === 'Paid').reduce((sum, i) => sum + i.total, 0);
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
    try {
      this.cdr = inject(ChangeDetectorRef);
    } catch {
      // Direct unit test instantiation
    }
  }

  createDemoInvoice(): void {
    const newInv: InvoiceItem = {
      id: 'inv_' + Math.random().toString(36).substring(2, 6),
      customerName: 'Nova Retail Corp',
      amount: 2500,
      tax: 206.25,
      total: 2706.25,
      currency: 'USD',
      status: 'PastDue',
      dunningAttempts: 1,
      sealHash: '7f83b1657ff1fc53b92dc18148a1d65dfc2d4b1fa3d677284addd200126d9069'
    };

    this.invoices.update(list => [newInv, ...list]);
    this.cdr?.markForCheck();
  }

  retryInvoicePayment(id: string): void {
    const updated = this.invoices().map(inv => {
      if (inv.id === id) {
        return {
          ...inv,
          status: 'Paid' as const,
          dunningAttempts: inv.dunningAttempts + 1
        };
      }
      return inv;
    });

    this.invoices.set(updated);
    this.cdr?.markForCheck();
  }
}
