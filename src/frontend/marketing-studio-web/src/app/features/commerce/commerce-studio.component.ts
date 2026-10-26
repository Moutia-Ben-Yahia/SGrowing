import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface ProductUi {
  id: string;
  name: string;
  sku: string;
  type: string;
  price: number;
  stock: number;
  isActive: boolean;
}

export interface QuoteUi {
  id: string;
  quoteNumber: string;
  customerName: string;
  total: number;
  status: 'Draft' | 'Sent' | 'Accepted' | 'ConvertedToOrder';
  itemsCount: number;
}

export interface AbandonedCartUi {
  id: string;
  email: string;
  total: number;
  status: 'Detected' | 'RecoveryTriggered' | 'Recovered';
  detectedTime: string;
}

@Component({
  selector: 'app-commerce-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Left Sidebar -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-amber-500 to-orange-500 flex items-center justify-center font-bold text-white shadow-lg shadow-amber-500/20">
              🛒
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-amber-400 font-medium">Commerce & CPQ</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <div class="space-y-1">
              <a routerLink="/" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📥 Omnichannel Inbox</span>
              </a>
              <a routerLink="/commerce" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-amber-500/20 text-amber-300 border border-amber-500/30 font-medium">
                <span>🛒 PIM Catalog & CPQ</span>
              </a>
              <a routerLink="/contracts" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span>✍️ In-Chat E-Signatures</span>
              </a>
              <a routerLink="/accounts" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-blue-400 hover:bg-blue-500/10 border border-blue-500/20 transition">
                <span>🏢 B2B Accounts CRM</span>
              </a>
            </div>
          </div>

          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-amber-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-amber-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-amber-400 animate-pulse"></span>
                In-Chat CPQ
              </span>
              <span class="text-[10px] bg-amber-500/10 text-amber-300 border border-amber-500/20 px-1.5 py-0.5 rounded">Stripe / Shopify</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Assemble quotes directly in customer chats, dispatch one-click checkout cards, and auto-recover abandoned Shopify checkouts.
            </p>
          </div>
        </div>
      </aside>

      <!-- Main Content -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <div>
            <h2 class="text-base font-semibold text-white">E-Commerce Catalog, In-Chat CPQ & Cart Recovery</h2>
            <p class="text-xs text-slate-400">PIM product variants, dynamic quote proposals, and automated abandoned cart recovery.</p>
          </div>
          <div class="flex gap-2">
            <button
              (click)="activeTab.set('catalog')"
              [class.bg-amber-600]="activeTab() === 'catalog'"
              class="px-3 py-1.5 bg-slate-800 text-xs rounded-lg font-medium transition">
              Products Catalog
            </button>
            <button
              (click)="activeTab.set('quotes')"
              [class.bg-amber-600]="activeTab() === 'quotes'"
              class="px-3 py-1.5 bg-slate-800 text-xs rounded-lg font-medium transition">
              CPQ Proposals
            </button>
            <button
              (click)="activeTab.set('carts')"
              [class.bg-amber-600]="activeTab() === 'carts'"
              class="px-3 py-1.5 bg-slate-800 text-xs rounded-lg font-medium transition">
              Abandoned Carts
            </button>
          </div>
        </header>

        <div class="flex-1 overflow-y-auto p-6 space-y-6 max-w-6xl">
          <!-- Catalog Tab -->
          <div *ngIf="activeTab() === 'catalog'" class="space-y-4">
            <div class="flex justify-between items-center">
              <h3 class="text-sm font-semibold text-white">PIM Product & SKU Directory</h3>
              <span class="text-xs text-slate-400 font-mono">{{ products().length }} products active</span>
            </div>
            <div class="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-950 text-slate-400 border-b border-slate-800">
                  <tr>
                    <th class="p-3">Product Name</th>
                    <th class="p-3">SKU</th>
                    <th class="p-3">Type</th>
                    <th class="p-3">Price</th>
                    <th class="p-3">Inventory</th>
                    <th class="p-3">Status</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800 text-slate-200">
                  <tr *ngFor="let p of products()" class="hover:bg-slate-800/40 transition">
                    <td class="p-3 font-medium text-white">{{ p.name }}</td>
                    <td class="p-3 font-mono text-amber-400">{{ p.sku }}</td>
                    <td class="p-3"><span class="px-2 py-0.5 rounded bg-slate-800 text-[10px]">{{ p.type }}</span></td>
                    <td class="p-3 font-mono">\${{ p.price | number:'1.2-2' }}</td>
                    <td class="p-3 font-mono">{{ p.stock }} in stock</td>
                    <td class="p-3"><span class="text-emerald-400">● Active</span></td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Quotes Tab -->
          <div *ngIf="activeTab() === 'quotes'" class="space-y-4">
            <div class="flex justify-between items-center">
              <h3 class="text-sm font-semibold text-white">Active CPQ Proposals</h3>
              <span class="text-xs text-slate-400 font-mono">{{ quotes().length }} quotes</span>
            </div>
            <div class="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-950 text-slate-400 border-b border-slate-800">
                  <tr>
                    <th class="p-3">Quote #</th>
                    <th class="p-3">Account / Contact</th>
                    <th class="p-3">Total Amount</th>
                    <th class="p-3">Status</th>
                    <th class="p-3 text-right">In-Chat Action</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800 text-slate-200">
                  <tr *ngFor="let q of quotes()" class="hover:bg-slate-800/40 transition">
                    <td class="p-3 font-mono font-bold text-white">{{ q.quoteNumber }}</td>
                    <td class="p-3">{{ q.customerName }}</td>
                    <td class="p-3 font-mono font-medium text-emerald-400">\${{ q.total | number:'1.2-2' }}</td>
                    <td class="p-3">
                      <span [ngClass]="{
                        'bg-blue-500/10 text-blue-400 border border-blue-500/20': q.status === 'Sent',
                        'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20': q.status === 'Accepted' || q.status === 'ConvertedToOrder'
                      }" class="px-2 py-0.5 rounded text-[11px] font-medium">{{ q.status }}</span>
                    </td>
                    <td class="p-3 text-right">
                      <button (click)="acceptQuote(q)" class="px-2.5 py-1 bg-amber-600 hover:bg-amber-500 text-white rounded text-[11px] font-medium transition">
                        Dispatch to WhatsApp
                      </button>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <!-- Abandoned Carts Tab -->
          <div *ngIf="activeTab() === 'carts'" class="space-y-4">
            <div class="flex justify-between items-center">
              <h3 class="text-sm font-semibold text-white">Shopify & WooCommerce Abandoned Checkouts</h3>
              <button (click)="triggerCartScan()" class="px-3 py-1.5 bg-orange-600 hover:bg-orange-500 text-white text-xs font-semibold rounded-lg transition shadow-lg shadow-orange-500/20">
                Trigger Automated Recovery Scan
              </button>
            </div>
            <div class="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
              <table class="w-full text-left text-xs">
                <thead class="bg-slate-950 text-slate-400 border-b border-slate-800">
                  <tr>
                    <th class="p-3">Customer Email</th>
                    <th class="p-3">Cart Total</th>
                    <th class="p-3">Detected</th>
                    <th class="p-3">Status</th>
                    <th class="p-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800 text-slate-200">
                  <tr *ngFor="let c of carts()" class="hover:bg-slate-800/40 transition">
                    <td class="p-3 font-medium text-white">{{ c.email }}</td>
                    <td class="p-3 font-mono">\${{ c.total | number:'1.2-2' }}</td>
                    <td class="p-3 text-slate-400">{{ c.detectedTime }}</td>
                    <td class="p-3">
                      <span [ngClass]="{
                        'bg-amber-500/10 text-amber-400 border border-amber-500/20': c.status === 'Detected',
                        'bg-blue-500/10 text-blue-400 border border-blue-500/20': c.status === 'RecoveryTriggered',
                        'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20': c.status === 'Recovered'
                      }" class="px-2 py-0.5 rounded text-[11px] font-medium">{{ c.status }}</span>
                    </td>
                    <td class="p-3 text-right">
                      <span *ngIf="c.status === 'RecoveryTriggered'" class="text-blue-400 text-[11px]">WhatsApp 10% Coupon Sent</span>
                      <span *ngIf="c.status === 'Recovered'" class="text-emerald-400 text-[11px]">Converted to Paid Order</span>
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class CommerceStudioComponent {
  activeTab = signal<'catalog' | 'quotes' | 'carts'>('catalog');

  products = signal<ProductUi[]>([
    {
      id: 'p1',
      name: 'SGrowing Growth Plan (Annual)',
      sku: 'SGR-GRW-01',
      type: 'Subscription',
      price: 2490,
      stock: 999,
      isActive: true
    },
    {
      id: 'p2',
      name: 'Omnichannel Voice Telephony SIP Pack',
      sku: 'SGR-VOICE-10K',
      type: 'Service',
      price: 499,
      stock: 50,
      isActive: true
    },
    {
      id: 'p3',
      name: 'Enterprise Dedicated AI SDR Worker Agent',
      sku: 'SGR-SDR-EXEC',
      type: 'Subscription',
      price: 1200,
      stock: 25,
      isActive: true
    }
  ]);

  quotes = signal<QuoteUi[]>([
    {
      id: 'q1',
      quoteNumber: 'QT-2026-0042',
      customerName: 'Stripe Deployment Lead',
      total: 11988,
      status: 'Sent',
      itemsCount: 3
    },
    {
      id: 'q2',
      quoteNumber: 'QT-2026-0043',
      customerName: 'Uber Logistics Operations',
      total: 24000,
      status: 'ConvertedToOrder',
      itemsCount: 5
    }
  ]);

  carts = signal<AbandonedCartUi[]>([
    {
      id: 'cart_1',
      email: 'alex.shopper@shopify.com',
      total: 349.50,
      status: 'RecoveryTriggered',
      detectedTime: '1 hour ago'
    },
    {
      id: 'cart_2',
      email: 'carol@enterprisecorp.io',
      total: 1250.00,
      status: 'Recovered',
      detectedTime: '3 hours ago'
    }
  ]);

  triggerCartScan() {
    this.carts.update(items =>
      items.map(c => c.status === 'Detected' ? { ...c, status: 'RecoveryTriggered' as const } : c)
    );
  }

  acceptQuote(q: QuoteUi) {
    q.status = 'Accepted';
    this.quotes.update(arr => [...arr]);
  }
}
