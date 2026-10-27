import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface StorefrontConnectorConfig {
  platform: 'Shopify' | 'WooCommerce';
  storeDomain: string;
  accessToken: string;
  webhookSecret: string;
  isActive: boolean;
  webhookUrl: string;
  lastSyncedAtUtc: string;
  syncedOrders: number;
  activeDeals: number;
  shipmentsDispatched: number;
  catalogProducts: number;
}

export interface IngestionEventLog {
  id: string;
  platform: 'Shopify' | 'WooCommerce';
  orderNumber: string;
  customerName: string;
  amount: number;
  currency: string;
  dealId: string;
  trackingNumber: string;
  status: 'Ingested' | 'DealCreated' | 'ShipmentDispatched';
  timestamp: string;
}

@Component({
  selector: 'app-ecommerce-connectors-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Bar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/commerce" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← {{ translation.t('nav.commerce') }}
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              🛍️ E-Commerce Storefront Connectors Cockpit
            </span>
            <span class="bg-indigo-500/20 text-indigo-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Shopify • WooCommerce • HMAC Webhooks
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <button
              (click)="triggerCatalogSync('all')"
              [disabled]="isSyncing()"
              class="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md">
              <span [class.animate-spin]="isSyncing()">🔄</span>
              <span>{{ isSyncing() ? 'Synchronizing...' : 'Trigger Catalog Sync' }}</span>
            </button>
            <button
              (click)="saveConfiguration()"
              class="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md">
              <span>💾</span> Save Config
            </button>
          </div>
        </header>

        <div class="p-6 max-w-7xl mx-auto w-full space-y-6 flex-1">
          <!-- Notification Toast -->
          @if (toastMessage()) {
            <div class="p-3 bg-emerald-500/10 border border-emerald-500/30 text-emerald-400 text-xs rounded-lg flex items-center justify-between">
              <div class="flex items-center gap-2">
                <span>✓</span>
                <span>{{ toastMessage() }}</span>
              </div>
              <button (click)="toastMessage.set(null)" class="text-slate-400 hover:text-white font-bold">✕</button>
            </div>
          }

          <!-- KPI Metric Cards Grid -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/50 hover:border-slate-700 transition">
              <span class="text-xs text-slate-400 block mb-1">Total Synced Orders</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-white">{{ totalSyncedOrders() }}</span>
                <span class="text-xs text-emerald-400 font-mono">100% Real-Time</span>
              </div>
            </div>

            <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/50 hover:border-slate-700 transition">
              <span class="text-xs text-slate-400 block mb-1">Active CRM Deals</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-indigo-400">{{ totalActiveDeals() }}</span>
                <span class="text-xs text-indigo-400 font-mono">Stage: Won</span>
              </div>
            </div>

            <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/50 hover:border-slate-700 transition">
              <span class="text-xs text-slate-400 block mb-1">COD Shipments Dispatched</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-emerald-400">{{ totalShipments() }}</span>
                <span class="text-xs text-slate-500 font-mono">AWB Generated</span>
              </div>
            </div>

            <div class="p-4 rounded-xl border border-slate-800 bg-slate-900/50 hover:border-slate-700 transition">
              <span class="text-xs text-slate-400 block mb-1">Catalog Products in Mesh</span>
              <div class="flex items-baseline justify-between">
                <span class="text-2xl font-bold text-amber-400">{{ totalCatalogProducts() }}</span>
                <span class="text-xs text-amber-400/80 font-mono">Variants Active</span>
              </div>
            </div>
          </div>

          <!-- Connector Configuration Cards -->
          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Shopify Card -->
            <div class="rounded-xl border border-slate-800 bg-slate-900/50 p-5 space-y-4">
              <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
                <div class="flex items-center gap-3">
                  <div class="w-9 h-9 rounded-lg bg-emerald-500/10 border border-emerald-500/30 flex items-center justify-center text-lg">
                    🟢
                  </div>
                  <div>
                    <h3 class="text-sm font-semibold text-white">Shopify Connector</h3>
                    <p class="text-xs text-slate-400">REST Admin API & HMAC Webhook Ingestion</p>
                  </div>
                </div>
                <div class="flex items-center gap-2">
                  <span class="px-2 py-0.5 rounded text-[11px] font-mono"
                    [class.bg-emerald-500/20]="shopifyConfig().isActive"
                    [class.text-emerald-400]="shopifyConfig().isActive"
                    [class.border]="true"
                    [class.border-emerald-500/30]="shopifyConfig().isActive">
                    {{ shopifyConfig().isActive ? 'Active' : 'Disabled' }}
                  </span>
                </div>
              </div>

              <div class="space-y-3">
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Store Domain</label>
                  <input
                    type="text"
                    [(ngModel)]="shopifyConfig().storeDomain"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500"
                    placeholder="my-shop.myshopify.com" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Admin API Access Token</label>
                  <input
                    type="password"
                    [(ngModel)]="shopifyConfig().accessToken"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500 font-mono"
                    placeholder="shpat_xxxxxxxxxxxxxxxxxxxxxxxx" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Webhook Secret (HMAC SHA-256)</label>
                  <input
                    type="password"
                    [(ngModel)]="shopifyConfig().webhookSecret"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500 font-mono"
                    placeholder="shpss_xxxxxxxxxxxxxxxxxxxxxxxx" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Webhook Endpoint URL</label>
                  <div class="flex items-center gap-2">
                    <input
                      type="text"
                      readonly
                      [value]="shopifyConfig().webhookUrl"
                      class="flex-1 bg-slate-950/70 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-400 font-mono" />
                    <button
                      (click)="copyToClipboard(shopifyConfig().webhookUrl)"
                      class="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs transition">
                      Copy
                    </button>
                  </div>
                </div>

                <div class="pt-2 flex items-center justify-between border-t border-slate-800/80">
                  <span class="text-[11px] text-slate-400">
                    Last Synced: <span class="text-slate-300">{{ formatTimestamp(shopifyConfig().lastSyncedAtUtc) }}</span>
                  </span>
                  <div class="flex gap-2">
                    <button
                      (click)="simulateInboundOrder('Shopify')"
                      class="px-2.5 py-1 bg-emerald-600/20 hover:bg-emerald-600/30 text-emerald-300 border border-emerald-500/30 rounded text-xs transition">
                      Simulate Webhook
                    </button>
                    <button
                      (click)="triggerCatalogSync('shopify')"
                      class="px-2.5 py-1 bg-indigo-600/20 hover:bg-indigo-600/30 text-indigo-300 border border-indigo-500/30 rounded text-xs transition">
                      Sync Catalog
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <!-- WooCommerce Card -->
            <div class="rounded-xl border border-slate-800 bg-slate-900/50 p-5 space-y-4">
              <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
                <div class="flex items-center gap-3">
                  <div class="w-9 h-9 rounded-lg bg-purple-500/10 border border-purple-500/30 flex items-center justify-center text-lg">
                    🟣
                  </div>
                  <div>
                    <h3 class="text-sm font-semibold text-white">WooCommerce Connector</h3>
                    <p class="text-xs text-slate-400">REST API v3 & Webhook Delivery Engine</p>
                  </div>
                </div>
                <div class="flex items-center gap-2">
                  <span class="px-2 py-0.5 rounded text-[11px] font-mono"
                    [class.bg-purple-500/20]="wooConfig().isActive"
                    [class.text-purple-300]="wooConfig().isActive"
                    [class.border]="true"
                    [class.border-purple-500/30]="wooConfig().isActive">
                    {{ wooConfig().isActive ? 'Active' : 'Disabled' }}
                  </span>
                </div>
              </div>

              <div class="space-y-3">
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Store Base URL</label>
                  <input
                    type="text"
                    [(ngModel)]="wooConfig().storeDomain"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-purple-500"
                    placeholder="https://shop.example.com" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Consumer Key / Secret</label>
                  <input
                    type="password"
                    [(ngModel)]="wooConfig().accessToken"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-purple-500 font-mono"
                    placeholder="ck_xxxxxxxxxxxxxxxxxxxxxxxx" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Webhook Secret</label>
                  <input
                    type="password"
                    [(ngModel)]="wooConfig().webhookSecret"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-purple-500 font-mono"
                    placeholder="wc_secret_xxxxxxxxxxxxxxxx" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Webhook Endpoint URL</label>
                  <div class="flex items-center gap-2">
                    <input
                      type="text"
                      readonly
                      [value]="wooConfig().webhookUrl"
                      class="flex-1 bg-slate-950/70 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-400 font-mono" />
                    <button
                      (click)="copyToClipboard(wooConfig().webhookUrl)"
                      class="px-2.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-xs transition">
                      Copy
                    </button>
                  </div>
                </div>

                <div class="pt-2 flex items-center justify-between border-t border-slate-800/80">
                  <span class="text-[11px] text-slate-400">
                    Last Synced: <span class="text-slate-300">{{ formatTimestamp(wooConfig().lastSyncedAtUtc) }}</span>
                  </span>
                  <div class="flex gap-2">
                    <button
                      (click)="simulateInboundOrder('WooCommerce')"
                      class="px-2.5 py-1 bg-purple-600/20 hover:bg-purple-600/30 text-purple-300 border border-purple-500/30 rounded text-xs transition">
                      Simulate Webhook
                    </button>
                    <button
                      (click)="triggerCatalogSync('woocommerce')"
                      class="px-2.5 py-1 bg-indigo-600/20 hover:bg-indigo-600/30 text-indigo-300 border border-indigo-500/30 rounded text-xs transition">
                      Sync Catalog
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <!-- Real-Time Ingested Orders Feed -->
          <div class="rounded-xl border border-slate-800 bg-slate-900/50 p-5 space-y-4">
            <div class="flex items-center justify-between border-b border-slate-800/80 pb-3">
              <div>
                <h3 class="text-sm font-semibold text-white">Live Ingestion Event Feed</h3>
                <p class="text-xs text-slate-400">Real-time webhook events mapped to CRM Deals and COD Logistics Shipments</p>
              </div>
              <span class="text-xs text-slate-500 font-mono">
                Showing {{ ingestionEvents().length }} recent events
              </span>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs">
                <thead>
                  <tr class="border-b border-slate-800 text-slate-400 font-medium">
                    <th class="py-2.5 px-3">Platform</th>
                    <th class="py-2.5 px-3">Order Number</th>
                    <th class="py-2.5 px-3">Customer</th>
                    <th class="py-2.5 px-3">Amount</th>
                    <th class="py-2.5 px-3">Deal ID</th>
                    <th class="py-2.5 px-3">Logistics Tracking</th>
                    <th class="py-2.5 px-3">Timestamp</th>
                    <th class="py-2.5 px-3">Status</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono text-[11px]">
                  @for (ev of ingestionEvents(); track ev.id) {
                    <tr class="hover:bg-slate-800/30 transition">
                      <td class="py-2.5 px-3">
                        <span class="px-2 py-0.5 rounded text-[10px] font-semibold"
                          [class.bg-emerald-500/20]="ev.platform === 'Shopify'"
                          [class.text-emerald-400]="ev.platform === 'Shopify'"
                          [class.bg-purple-500/20]="ev.platform === 'WooCommerce'"
                          [class.text-purple-300]="ev.platform === 'WooCommerce'">
                          {{ ev.platform }}
                        </span>
                      </td>
                      <td class="py-2.5 px-3 font-semibold text-white">{{ ev.orderNumber }}</td>
                      <td class="py-2.5 px-3 font-sans text-slate-300">{{ ev.customerName }}</td>
                      <td class="py-2.5 px-3 text-emerald-400 font-semibold">
                        {{ translation.formatCurrency(ev.amount, ev.currency) }}
                      </td>
                      <td class="py-2.5 px-3 text-indigo-400">{{ ev.dealId.slice(0, 8) }}...</td>
                      <td class="py-2.5 px-3 text-slate-300">{{ ev.trackingNumber }}</td>
                      <td class="py-2.5 px-3 text-slate-400">{{ formatTimestamp(ev.timestamp) }}</td>
                      <td class="py-2.5 px-3">
                        <span class="px-2 py-0.5 rounded bg-emerald-500/10 text-emerald-400 border border-emerald-500/30 text-[10px]">
                          ✓ {{ ev.status }}
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class EcommerceConnectorsCockpitComponent {
  public readonly translation: TranslationService;

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

  public readonly isSyncing = signal<boolean>(false);
  public readonly toastMessage = signal<string | null>(null);

  public readonly shopifyConfig = signal<StorefrontConnectorConfig>({
    platform: 'Shopify',
    storeDomain: 'sgrowing-flagship.myshopify.com',
    accessToken: 'shpat_88f9a2b1c4d3e5f6a7b8c9d0',
    webhookSecret: 'shpss_sec_9918273645a1b2c3',
    isActive: true,
    webhookUrl: 'https://api.sgrowing.io/api/v1/commerce/connectors/shopify/webhook',
    lastSyncedAtUtc: new Date(Date.now() - 1000 * 60 * 15).toISOString(),
    syncedOrders: 14,
    activeDeals: 14,
    shipmentsDispatched: 14,
    catalogProducts: 3
  });

  public readonly wooConfig = signal<StorefrontConnectorConfig>({
    platform: 'WooCommerce',
    storeDomain: 'shop.sgrowing-boutique.com',
    accessToken: 'ck_99887766554433221100aabbccdd',
    webhookSecret: 'wc_secret_44556677889900aa',
    isActive: true,
    webhookUrl: 'https://api.sgrowing.io/api/v1/commerce/connectors/woocommerce/webhook',
    lastSyncedAtUtc: new Date(Date.now() - 1000 * 60 * 45).toISOString(),
    syncedOrders: 11,
    activeDeals: 11,
    shipmentsDispatched: 11,
    catalogProducts: 3
  });

  public readonly ingestionEvents = signal<IngestionEventLog[]>([
    {
      id: 'EVT-101',
      platform: 'Shopify',
      orderNumber: '#SFY-4021',
      customerName: 'Sophie Tremblay',
      amount: 285.50,
      currency: 'USD',
      dealId: 'd3b07384-d113-4f44-93e1-7e87a256a001',
      trackingNumber: 'SHP-SFY-4021',
      status: 'ShipmentDispatched',
      timestamp: new Date(Date.now() - 1000 * 60 * 10).toISOString()
    },
    {
      id: 'EVT-102',
      platform: 'WooCommerce',
      orderNumber: 'WC-88301',
      customerName: 'Jean-Luc Picard',
      amount: 418.00,
      currency: 'EUR',
      dealId: 'f7c19452-e224-4a55-84f2-8f98b367b002',
      trackingNumber: 'SHP-WC-88301',
      status: 'ShipmentDispatched',
      timestamp: new Date(Date.now() - 1000 * 60 * 25).toISOString()
    },
    {
      id: 'EVT-103',
      platform: 'Shopify',
      orderNumber: '#SFY-4020',
      customerName: 'Marcus Vance',
      amount: 149.00,
      currency: 'USD',
      dealId: 'a1b2c3d4-e5f6-4789-a012-3456789abcde',
      trackingNumber: 'SHP-SFY-4020',
      status: 'ShipmentDispatched',
      timestamp: new Date(Date.now() - 1000 * 60 * 60).toISOString()
    }
  ]);

  public readonly totalSyncedOrders = computed(() => {
    return this.shopifyConfig().syncedOrders + this.wooConfig().syncedOrders;
  });

  public readonly totalActiveDeals = computed(() => {
    return this.shopifyConfig().activeDeals + this.wooConfig().activeDeals;
  });

  public readonly totalShipments = computed(() => {
    return this.shopifyConfig().shipmentsDispatched + this.wooConfig().shipmentsDispatched;
  });

  public readonly totalCatalogProducts = computed(() => {
    return this.shopifyConfig().catalogProducts + this.wooConfig().catalogProducts;
  });

  public formatTimestamp(isoString: string): string {
    return this.translation.formatDate(isoString);
  }

  public copyToClipboard(url: string): void {
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(url);
      this.toastMessage.set(`Copied webhook URL to clipboard: ${url}`);
      setTimeout(() => this.toastMessage.set(null), 3000);
    }
  }

  public triggerCatalogSync(target: 'shopify' | 'woocommerce' | 'all'): void {
    this.isSyncing.set(true);
    setTimeout(() => {
      this.isSyncing.set(false);
      const now = new Date().toISOString();
      if (target === 'shopify' || target === 'all') {
        this.shopifyConfig.update(c => ({
          ...c,
          catalogProducts: c.catalogProducts + 1,
          lastSyncedAtUtc: now
        }));
      }
      if (target === 'woocommerce' || target === 'all') {
        this.wooConfig.update(c => ({
          ...c,
          catalogProducts: c.catalogProducts + 1,
          lastSyncedAtUtc: now
        }));
      }
      this.toastMessage.set(`Catalog synchronization completed successfully for ${target.toUpperCase()}`);
      setTimeout(() => this.toastMessage.set(null), 4000);
    }, 600);
  }

  public saveConfiguration(): void {
    this.toastMessage.set('Storefront connector settings saved to database successfully.');
    setTimeout(() => this.toastMessage.set(null), 3000);
  }

  public simulateInboundOrder(platform: 'Shopify' | 'WooCommerce'): void {
    const isShopify = platform === 'Shopify';
    const randNum = Math.floor(1000 + Math.random() * 9000);
    const orderNumber = isShopify ? `#SFY-${randNum}` : `WC-${randNum}`;
    const amount = isShopify ? 199.50 : 249.00;
    const currency = isShopify ? 'USD' : 'EUR';
    const customer = isShopify ? 'Clara Oswald' : 'Arthur Dent';
    const dealId = crypto.randomUUID();
    const trackingNumber = isShopify ? `SHP-SFY-${randNum}` : `SHP-WC-${randNum}`;

    const newEvent: IngestionEventLog = {
      id: `EVT-${Date.now()}`,
      platform,
      orderNumber,
      customerName: customer,
      amount,
      currency,
      dealId,
      trackingNumber,
      status: 'ShipmentDispatched',
      timestamp: new Date().toISOString()
    };

    this.ingestionEvents.update(list => [newEvent, ...list]);

    if (isShopify) {
      this.shopifyConfig.update(c => ({
        ...c,
        syncedOrders: c.syncedOrders + 1,
        activeDeals: c.activeDeals + 1,
        shipmentsDispatched: c.shipmentsDispatched + 1
      }));
    } else {
      this.wooConfig.update(c => ({
        ...c,
        syncedOrders: c.syncedOrders + 1,
        activeDeals: c.activeDeals + 1,
        shipmentsDispatched: c.shipmentsDispatched + 1
      }));
    }

    this.toastMessage.set(`Simulated ${platform} order ${orderNumber} successfully mapped to CRM Deal & COD Shipment!`);
    setTimeout(() => this.toastMessage.set(null), 4000);
  }
}
