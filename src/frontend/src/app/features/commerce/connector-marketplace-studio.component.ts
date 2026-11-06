import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export type ConnectorTier = 'All' | 'RegionalMENA' | 'GlobalEnterprise' | 'OpenSource';
export type ConnectorAuthType = 'OAuth2 HMAC-SHA256' | 'OAuth 2.0 Webhook Signature' | 'X-Manager-Token' | 'Bearer Token & HMAC' | 'HMAC-SHA256 Consumer Key/Secret';

export interface CommerceConnector {
  id: string;
  name: string;
  tier: 'RegionalMENA' | 'GlobalEnterprise' | 'OpenSource';
  category: string;
  authType: ConnectorAuthType;
  webhookTopicsCount: number;
  status: 'Installed' | 'Available' | 'PendingAuth';
  description: string;
  logoIcon: string;
  supportedTopics: string[];
  activeEndpoints: number;
  averageLatencyMs: number;
}

export interface CanonicalOrderPayload {
  canonicalOrderId: string;
  sourcePlatform: string;
  externalOrderId: string;
  orderNumber: string;
  currency: string;
  totalAmount: number;
  subtotalAmount: number;
  taxAmount: number;
  discountAmount: number;
  paymentMethod: 'CashOnDelivery' | 'Card' | 'BuyNowPayLater' | 'Prepaid' | 'Unknown';
  isCashOnDelivery: boolean;
  customer: {
    name: string;
    email: string;
    phone: string;
    shippingAddress: {
      city: string;
      countryCode: string;
      streetLine?: string;
      district?: string;
    };
  };
  items: Array<{
    sku: string;
    name: string;
    quantity: number;
    unitPrice: number;
    totalPrice: number;
  }>;
  rawEventTimestamp: string;
  normalizedAtUtc: string;
}

const DEFAULT_SALLA_SAMPLE = JSON.stringify({
  event: 'order.created',
  merchant: 102934,
  created_at: '2026-11-06T15:30:00+03:00',
  data: {
    id: 99482711,
    reference_id: 'SALLA-ORD-8812',
    currency: 'SAR',
    total: {
      amount: 450.0,
      currency: 'SAR'
    },
    sub_total: {
      amount: 420.0,
      currency: 'SAR'
    },
    tax: {
      amount: 30.0,
      currency: 'SAR'
    },
    payment_method: 'cod',
    customer: {
      first_name: 'Faisal',
      last_name: 'Al-Otaibi',
      email: 'faisal.otaibi@example.sa',
      mobile: '+966501234567'
    },
    shipping: {
      address: {
        country: 'SA',
        city: 'Riyadh',
        district: 'Al-Malqa',
        street: 'King Salman Road'
      }
    },
    items: [
      {
        id: 54101,
        sku: 'SKU-OUD-ROYAL-50ML',
        name: 'Royal Oud Parfum Intense 50ml',
        quantity: 2,
        price: {
          amount: 210.0,
          currency: 'SAR'
        }
      }
    ]
  }
}, null, 2);

const DEFAULT_SHOPIFY_SAMPLE = JSON.stringify({
  id: 82098273641,
  admin_graphql_api_id: 'gid://shopify/Order/82098273641',
  order_number: 1045,
  name: '#1045',
  created_at: '2026-11-06T12:00:00Z',
  currency: 'USD',
  current_total_price: '185.00',
  current_subtotal_price: '170.00',
  total_tax: '15.00',
  financial_status: 'paid',
  gateway: 'shopify_payments',
  payment_gateway_names: ['shopify_payments'],
  email: 'sarah.connor@example.com',
  customer: {
    first_name: 'Sarah',
    last_name: 'Connor',
    email: 'sarah.connor@example.com',
    phone: '+14155552671'
  },
  shipping_address: {
    first_name: 'Sarah',
    last_name: 'Connor',
    address1: '742 Evergreen Terrace',
    city: 'Springfield',
    country_code: 'US'
  },
  line_items: [
    {
      id: 9283741,
      sku: 'SKU-COFFEE-ROAST-01',
      title: 'Artisan Espresso Beans 1kg',
      quantity: 1,
      price: '35.00'
    },
    {
      id: 9283742,
      sku: 'SKU-DRIP-MACHINE-V2',
      title: 'Precision Drip Brewer Pro',
      quantity: 1,
      price: '135.00'
    }
  ]
}, null, 2);

@Component({
  selector: 'app-connector-marketplace-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Top Navigation Header -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-40">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-white">Universal Connector Marketplace</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-indigo-500/10 text-indigo-400 border border-indigo-500/20 font-medium">
              Canonical Ingestion Studio
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <span class="text-xs text-slate-400 font-mono">Installed:</span>
          <span class="text-xs px-2 py-0.5 rounded-md bg-emerald-500/20 text-emerald-300 font-bold border border-emerald-500/30">
            {{ installedCount() }} Active
          </span>
          <button
            type="button"
            (click)="scrollToPlayground()"
            class="px-3.5 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-xs font-semibold text-white shadow-lg shadow-indigo-950/40 transition flex items-center gap-1.5"
          >
            <span>🔄</span> Transformation Playground
          </button>
        </div>
      </header>

      <!-- Main Container -->
      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-8">
        <!-- Hero & Filter Bar -->
        <section class="bg-slate-900/60 border border-slate-800 rounded-2xl p-6 shadow-xl">
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div>
              <div class="flex items-center gap-2">
                <span class="text-2xl">🛍️</span>
                <h1 class="text-lg font-bold text-white">E-Commerce Storefront Connectors & Webhook Mesh</h1>
              </div>
              <p class="text-xs text-slate-400 mt-1">
                Zero-code integration for regional MENA marketplaces and global enterprise commerce platforms with automated canonical normalization.
              </p>
            </div>

            <!-- Tier Filter Pills -->
            <div class="flex items-center bg-slate-950/80 p-1.5 rounded-xl border border-slate-800 self-start md:self-auto">
              @for (tier of tiers; track tier) {
                <button
                  type="button"
                  (click)="setTier(tier)"
                  [class]="selectedTier() === tier
                    ? 'px-3 py-1.5 rounded-lg text-xs font-bold bg-indigo-600 text-white shadow'
                    : 'px-3 py-1.5 rounded-lg text-xs font-medium text-slate-400 hover:text-slate-200'"
                >
                  {{ tierLabels[tier] }}
                </button>
              }
            </div>
          </div>
        </section>

        <!-- Connector Cards Grid -->
        <section class="space-y-4">
          <div class="flex items-center justify-between">
            <h2 class="text-xs font-bold uppercase tracking-wider text-slate-400">Available E-Commerce Connectors ({{ filteredConnectors().length }})</h2>
            <span class="text-xs text-slate-500 font-mono">HMAC-SHA256 & OAuth 2.0 Ready</span>
          </div>

          <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
            @for (conn of filteredConnectors(); track conn.id) {
              <div class="bg-slate-900/60 border border-slate-800 hover:border-slate-700 rounded-2xl p-5 flex flex-col justify-between transition-all hover:shadow-xl group">
                <div>
                  <div class="flex items-start justify-between gap-3 mb-3">
                    <div class="flex items-center gap-3">
                      <div class="w-10 h-10 rounded-xl bg-slate-800 flex items-center justify-center text-xl shrink-0 group-hover:scale-105 transition">
                        {{ conn.logoIcon }}
                      </div>
                      <div>
                        <h3 class="text-sm font-bold text-white">{{ conn.name }}</h3>
                        <p class="text-[11px] text-slate-400">{{ conn.category }}</p>
                      </div>
                    </div>

                    <!-- Status Pill -->
                    <span [class]="getStatusClass(conn.status)">
                      {{ conn.status }}
                    </span>
                  </div>

                  <p class="text-xs text-slate-300 mb-4 line-clamp-2 leading-relaxed">
                    {{ conn.description }}
                  </p>

                  <div class="space-y-2 py-3 border-t border-slate-800/80 text-[11px]">
                    <div class="flex items-center justify-between">
                      <span class="text-slate-500">Tier Category:</span>
                      <span [class]="getTierBadgeClass(conn.tier)">{{ conn.tier }}</span>
                    </div>

                    <div class="flex items-center justify-between">
                      <span class="text-slate-500">Authentication:</span>
                      <span class="text-indigo-300 font-mono font-medium">{{ conn.authType }}</span>
                    </div>

                    <div class="flex items-center justify-between">
                      <span class="text-slate-500">Webhook Topics:</span>
                      <span class="text-slate-200 font-mono font-semibold">{{ conn.webhookTopicsCount }} events</span>
                    </div>
                  </div>
                </div>

                <!-- Action Button -->
                <div class="pt-4 border-t border-slate-800/80 flex items-center gap-2">
                  <button
                    type="button"
                    (click)="openConfigureModal(conn)"
                    class="flex-1 py-2 px-3 rounded-xl bg-slate-800 hover:bg-indigo-600 text-xs font-semibold text-slate-200 hover:text-white transition flex items-center justify-center gap-1.5"
                  >
                    <span>⚙️</span> Configure / Install
                  </button>
                  <button
                    type="button"
                    (click)="loadConnectorSample(conn)"
                    title="Load sample JSON in playground"
                    class="p-2 rounded-xl bg-slate-950 border border-slate-800 hover:border-slate-700 text-xs text-slate-400 hover:text-white transition"
                  >
                    📋 Test
                  </button>
                </div>
              </div>
            }
          </div>
        </section>

        <!-- Dynamic Field Transformation Playground -->
        <section id="playground-section" class="bg-slate-900/60 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-6">
          <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-4 border-b border-slate-800">
            <div>
              <div class="flex items-center gap-2">
                <span class="text-xl">⚡</span>
                <h2 class="text-base font-bold text-white">Dynamic Field Transformation Playground</h2>
              </div>
              <p class="text-xs text-slate-400 mt-1">
                Paste any raw store webhook payload (Salla, Shopify, WooCommerce, Zid) to verify real-time canonical order normalization.
              </p>
            </div>

            <!-- Preset Load Buttons -->
            <div class="flex items-center gap-2 flex-wrap">
              <span class="text-xs text-slate-500">Quick Samples:</span>
              <button
                type="button"
                (click)="loadPresetSample('salla')"
                class="px-2.5 py-1 rounded-lg bg-emerald-950/40 hover:bg-emerald-900/60 border border-emerald-700/50 text-[11px] font-semibold text-emerald-300 transition"
              >
                Salla COD (SAR)
              </button>
              <button
                type="button"
                (click)="loadPresetSample('shopify')"
                class="px-2.5 py-1 rounded-lg bg-cyan-950/40 hover:bg-cyan-900/60 border border-cyan-700/50 text-[11px] font-semibold text-cyan-300 transition"
              >
                Shopify Paid (USD)
              </button>
              <button
                type="button"
                (click)="loadPresetSample('woocommerce')"
                class="px-2.5 py-1 rounded-lg bg-purple-950/40 hover:bg-purple-900/60 border border-purple-700/50 text-[11px] font-semibold text-purple-300 transition"
              >
                WooCommerce
              </button>
            </div>
          </div>

          <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Left: Raw JSON Input -->
            <div class="space-y-3 flex flex-col">
              <div class="flex items-center justify-between">
                <label class="text-xs font-bold uppercase tracking-wider text-slate-400 flex items-center gap-1.5">
                  <span>📥</span> Raw Incoming Webhook JSON
                </label>
                <button
                  type="button"
                  (click)="transformOrderPayload()"
                  class="px-3 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-xs font-bold text-white shadow transition flex items-center gap-1.5"
                >
                  <span>🚀</span> Transform to Canonical Order
                </button>
              </div>

              <textarea
                rows="16"
                [ngModel]="testJsonPayload()"
                (ngModelChange)="testJsonPayload.set($event)"
                class="w-full flex-1 bg-slate-950 border border-slate-800 rounded-xl p-3 text-[11px] font-mono text-cyan-300/90 focus:outline-none focus:border-indigo-500 select-text whitespace-pre"
                placeholder="Paste store JSON here..."
              ></textarea>
            </div>

            <!-- Right: Canonical Normalized Output -->
            <div class="space-y-3 flex flex-col">
              <div class="flex items-center justify-between">
                <label class="text-xs font-bold uppercase tracking-wider text-slate-400 flex items-center gap-1.5">
                  <span>📤</span> Normalized Canonical Order Payload
                </label>
                @if (transformedOrder(); as norm) {
                  <div class="flex items-center gap-2">
                    <span [class]="norm.isCashOnDelivery ? 'bg-amber-500/20 text-amber-300 border-amber-500/30' : 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30'"
                          class="text-[10px] font-mono px-2 py-0.5 rounded border font-semibold">
                      {{ norm.isCashOnDelivery ? 'Cash on Delivery (COD)' : 'Prepaid/Digital' }}
                    </span>
                    <span class="text-xs text-emerald-400 font-mono font-bold">{{ norm.totalAmount }} {{ norm.currency }}</span>
                  </div>
                }
              </div>

              <div class="flex-1 min-h-[360px] bg-slate-950 border border-slate-800 rounded-xl p-3 flex flex-col justify-between overflow-hidden">
                @if (transformedOrder(); as norm) {
                  <div class="overflow-y-auto max-h-[380px] space-y-3 pr-1">
                    <!-- Quick KPI Header -->
                    <div class="grid grid-cols-3 gap-2 bg-slate-900/80 p-2.5 rounded-lg border border-slate-800 text-[11px]">
                      <div>
                        <div class="text-slate-500">Order ID:</div>
                        <div class="font-mono text-white font-bold truncate">{{ norm.orderNumber }}</div>
                      </div>
                      <div>
                        <div class="text-slate-500">Source:</div>
                        <div class="font-mono text-indigo-300 font-bold">{{ norm.sourcePlatform }}</div>
                      </div>
                      <div>
                        <div class="text-slate-500">Customer:</div>
                        <div class="text-white font-bold truncate">{{ norm.customer.name }}</div>
                      </div>
                    </div>

                    <!-- Formatted Canonical JSON -->
                    <pre class="text-[11px] font-mono text-emerald-300 select-text whitespace-pre-wrap">{{ norm | json }}</pre>
                  </div>
                } @else {
                  <div class="h-full flex flex-col items-center justify-center text-center p-8 text-slate-500 text-xs">
                    <span class="text-3xl mb-2">📦</span>
                    <p>Click "Transform to Canonical Order" to parse and normalize the input webhook payload.</p>
                  </div>
                }
              </div>
            </div>
          </div>
        </section>
      </main>

      <!-- Install & Configure Modal -->
      @if (showInstallModal()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-5">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <span class="text-xl">{{ selectedConnector()?.logoIcon }}</span>
                <h3 class="text-sm font-bold text-white">Configure {{ selectedConnector()?.name }}</h3>
              </div>
              <button
                type="button"
                (click)="closeConfigureModal()"
                class="text-slate-400 hover:text-white text-lg font-mono"
              >
                ✕
              </button>
            </div>

            <div class="space-y-4 text-xs">
              <div>
                <label class="block text-slate-400 mb-1 font-medium">Store Domain / API Endpoint</label>
                <input
                  type="text"
                  [(ngModel)]="modalStoreDomain"
                  placeholder="e.g. store.salla.sa or your-store.myshopify.com"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white focus:outline-none focus:border-indigo-500"
                />
              </div>

              <div>
                <label class="block text-slate-400 mb-1 font-medium">Webhook Secret / API Token</label>
                <input
                  type="password"
                  [(ngModel)]="modalWebhookSecret"
                  placeholder="whsec_..."
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white focus:outline-none focus:border-indigo-500 font-mono"
                />
              </div>

              <div>
                <label class="block text-slate-400 mb-1 font-medium">Selected Environment</label>
                <select
                  [(ngModel)]="modalEnvironment"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-white focus:outline-none focus:border-indigo-500"
                >
                  <option value="production">Production Storefront</option>
                  <option value="sandbox">Sandbox / Staging</option>
                </select>
              </div>

              <div class="p-3 bg-slate-950 rounded-xl border border-slate-800 text-[11px] text-slate-400">
                <span class="font-bold text-slate-300">Security Note:</span> Inbound events are verified via {{ selectedConnector()?.authType }} before reaching the canonical ingestion queue.
              </div>
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button
                type="button"
                (click)="closeConfigureModal()"
                class="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-300 transition"
              >
                Cancel
              </button>
              <button
                type="button"
                (click)="saveConnectorConfiguration()"
                class="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-xs font-bold text-white shadow transition"
              >
                Save & Enable Connector
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class ConnectorMarketplaceStudioComponent {
  // Required signals matching spec
  readonly selectedTier = signal<ConnectorTier>('All');
  readonly connectors = signal<CommerceConnector[]>([
    {
      id: 'shopify',
      name: 'Shopify',
      tier: 'GlobalEnterprise',
      category: 'Global Enterprise Storefront',
      authType: 'OAuth2 HMAC-SHA256',
      webhookTopicsCount: 18,
      status: 'Installed',
      description: 'Connect Shopify and Shopify Plus storefronts with automatic inventory sync and real-time order webhooks.',
      logoIcon: '🛍️',
      supportedTopics: ['orders/create', 'orders/paid', 'orders/updated', 'inventory_levels/update'],
      activeEndpoints: 3,
      averageLatencyMs: 34
    },
    {
      id: 'salla',
      name: 'Salla',
      tier: 'RegionalMENA',
      category: 'KSA & GCC E-Commerce Leader',
      authType: 'OAuth 2.0 Webhook Signature',
      webhookTopicsCount: 24,
      status: 'Available',
      description: 'Leading Saudi Arabian commerce engine with native Cash-On-Delivery, Tabby/Tamara split, and Mada support.',
      logoIcon: '🇸🇦',
      supportedTopics: ['order.created', 'order.status.updated', 'product.created', 'shipment.creating'],
      activeEndpoints: 1,
      averageLatencyMs: 42
    },
    {
      id: 'zid',
      name: 'Zid',
      tier: 'RegionalMENA',
      category: 'Saudi Retail Modernization',
      authType: 'X-Manager-Token',
      webhookTopicsCount: 16,
      status: 'Available',
      description: 'Saudi retail omnichannel platform connecting in-store POS with digital storefront orders and logistics.',
      logoIcon: '📦',
      supportedTopics: ['order.create', 'order.payment_status.update', 'product.inventory_update'],
      activeEndpoints: 0,
      averageLatencyMs: 48
    },
    {
      id: 'youcan',
      name: 'YouCan',
      tier: 'RegionalMENA',
      category: 'North Africa & Maghreb Commerce',
      authType: 'Bearer Token & HMAC',
      webhookTopicsCount: 12,
      status: 'Available',
      description: 'High-conversion COD-optimized platform powering fast-growing merchants across Morocco, Egypt, and GCC.',
      logoIcon: '⚡',
      supportedTopics: ['order.create', 'order.paid', 'inventory.update'],
      activeEndpoints: 0,
      averageLatencyMs: 55
    },
    {
      id: 'woocommerce',
      name: 'WooCommerce',
      tier: 'OpenSource',
      category: 'Self-Hosted WordPress Mesh',
      authType: 'HMAC-SHA256 Consumer Key/Secret',
      webhookTopicsCount: 14,
      status: 'Installed',
      description: 'Open-source WordPress e-commerce engine with full sovereign control and custom REST API hooks.',
      logoIcon: '🔌',
      supportedTopics: ['woocommerce_order_status_completed', 'woocommerce_order_status_processing'],
      activeEndpoints: 2,
      averageLatencyMs: 62
    }
  ]);
  readonly selectedConnector = signal<CommerceConnector | null>(null);
  readonly showInstallModal = signal(false);
  readonly testJsonPayload = signal<string>(DEFAULT_SALLA_SAMPLE);
  readonly transformedOrder = signal<CanonicalOrderPayload | null>(null);

  // Modal form bindings
  modalStoreDomain = '';
  modalWebhookSecret = '';
  modalEnvironment = 'production';

  readonly tiers: ConnectorTier[] = ['All', 'RegionalMENA', 'GlobalEnterprise', 'OpenSource'];
  readonly tierLabels: Record<ConnectorTier, string> = {
    All: 'All Platforms',
    RegionalMENA: 'Regional MENA / KSA',
    GlobalEnterprise: 'Global Enterprise',
    OpenSource: 'Open-Source Self-Hosted'
  };

  // Computed signals
  readonly filteredConnectors = computed<CommerceConnector[]>(() => {
    const tier = this.selectedTier();
    const all = this.connectors();
    if (tier === 'All') return all;
    return all.filter(c => c.tier === tier);
  });

  readonly installedCount = computed<number>(() => {
    return this.connectors().filter(c => c.status === 'Installed').length;
  });

  constructor() {
    // Run initial transformation with default sample so output is visible immediately
    this.transformOrderPayload();
  }

  setTier(tier: ConnectorTier): void {
    this.selectedTier.set(tier);
  }

  openConfigureModal(connector: CommerceConnector): void {
    this.selectedConnector.set(connector);
    this.modalStoreDomain = connector.id === 'shopify' ? 'my-brand.myshopify.com' : `${connector.id}-store.example.com`;
    this.modalWebhookSecret = 'whsec_' + Math.random().toString(36).substring(2, 12);
    this.modalEnvironment = 'production';
    this.showInstallModal.set(true);
  }

  closeConfigureModal(): void {
    this.showInstallModal.set(false);
  }

  saveConnectorConfiguration(): void {
    const connector = this.selectedConnector();
    if (connector) {
      const updated = this.connectors().map(c => {
        if (c.id === connector.id) {
          return { ...c, status: 'Installed' as const, activeEndpoints: Math.max(c.activeEndpoints, 1) };
        }
        return c;
      });
      this.connectors.set(updated);
    }
    this.showInstallModal.set(false);
  }

  loadPresetSample(platform: 'salla' | 'shopify' | 'woocommerce'): void {
    if (platform === 'salla') {
      this.testJsonPayload.set(DEFAULT_SALLA_SAMPLE);
    } else if (platform === 'shopify') {
      this.testJsonPayload.set(DEFAULT_SHOPIFY_SAMPLE);
    } else {
      const wooSample = {
        id: 7721,
        number: 'WOO-901',
        status: 'processing',
        currency: 'AED',
        total: '310.00',
        total_tax: '15.50',
        payment_method: 'cod',
        payment_method_title: 'Cash on delivery',
        billing: {
          first_name: 'Tariq',
          last_name: 'Mansoor',
          email: 'tariq.m@example.ae',
          phone: '+971501234567',
          city: 'Dubai',
          country: 'AE'
        },
        line_items: [
          {
            id: 101,
            name: 'Leather Weekend Travel Bag',
            product_id: 882,
            quantity: 1,
            price: 310.0,
            total: '310.00',
            sku: 'SKU-LEATHER-BAG-BRN'
          }
        ]
      };
      this.testJsonPayload.set(JSON.stringify(wooSample, null, 2));
    }
    this.transformOrderPayload();
  }

  loadConnectorSample(connector: CommerceConnector): void {
    if (connector.id === 'shopify') {
      this.loadPresetSample('shopify');
    } else if (connector.id === 'woocommerce') {
      this.loadPresetSample('woocommerce');
    } else {
      this.loadPresetSample('salla');
    }
    this.scrollToPlayground();
  }

  transformOrderPayload(): void {
    try {
      const raw = JSON.parse(this.testJsonPayload());
      const normalized = this.normalizeToCanonical(raw);
      this.transformedOrder.set(normalized);
    } catch {
      this.transformedOrder.set(null);
    }
  }

  private normalizeToCanonical(raw: any): CanonicalOrderPayload {
    // Salla schema
    if (raw.data && (raw.merchant || raw.data.reference_id || raw.event?.startsWith('order.'))) {
      const d = raw.data;
      const isCod = (d.payment_method ?? '').toLowerCase().includes('cod') ||
                    (d.payment_method ?? '').toLowerCase().includes('cash');
      const items = (d.items ?? []).map((it: any) => ({
        sku: it.sku || `SKU-${it.id}`,
        name: it.name || 'Unnamed Product',
        quantity: it.quantity ?? 1,
        unitPrice: typeof it.price?.amount === 'number' ? it.price.amount : (parseFloat(it.price?.amount) || 0),
        totalPrice: (it.quantity ?? 1) * (typeof it.price?.amount === 'number' ? it.price.amount : (parseFloat(it.price?.amount) || 0))
      }));

      return {
        canonicalOrderId: `canon-salla-${d.id}`,
        sourcePlatform: 'Salla',
        externalOrderId: String(d.id),
        orderNumber: d.reference_id || `#${d.id}`,
        currency: d.currency || d.total?.currency || 'SAR',
        totalAmount: parseFloat(d.total?.amount ?? '0'),
        subtotalAmount: parseFloat(d.sub_total?.amount ?? '0'),
        taxAmount: parseFloat(d.tax?.amount ?? '0'),
        discountAmount: 0,
        paymentMethod: isCod ? 'CashOnDelivery' : 'Card',
        isCashOnDelivery: isCod,
        customer: {
          name: `${d.customer?.first_name ?? ''} ${d.customer?.last_name ?? ''}`.trim() || 'Valued Customer',
          email: d.customer?.email || 'unspecified@customer.sa',
          phone: d.customer?.mobile || '',
          shippingAddress: {
            city: d.shipping?.address?.city || 'Riyadh',
            countryCode: d.shipping?.address?.country || 'SA',
            streetLine: d.shipping?.address?.street || '',
            district: d.shipping?.address?.district || ''
          }
        },
        items,
        rawEventTimestamp: raw.created_at || new Date().toISOString(),
        normalizedAtUtc: new Date().toISOString()
      };
    }

    // Shopify schema
    if (raw.admin_graphql_api_id || raw.order_number || raw.line_items) {
      const isCod = (raw.gateway ?? '').toLowerCase().includes('cash') ||
                    (raw.payment_gateway_names ?? []).some((g: string) => g.toLowerCase().includes('cash') || g.toLowerCase().includes('cod'));
      const items = (raw.line_items ?? []).map((li: any) => ({
        sku: li.sku || `SKU-${li.id}`,
        name: li.title || li.name || 'Unnamed Item',
        quantity: li.quantity ?? 1,
        unitPrice: parseFloat(li.price || '0'),
        totalPrice: (li.quantity ?? 1) * parseFloat(li.price || '0')
      }));

      return {
        canonicalOrderId: `canon-shopify-${raw.id}`,
        sourcePlatform: 'Shopify',
        externalOrderId: String(raw.id),
        orderNumber: raw.name || `#${raw.order_number || raw.id}`,
        currency: raw.currency || 'USD',
        totalAmount: parseFloat(raw.current_total_price || raw.total_price || '0'),
        subtotalAmount: parseFloat(raw.current_subtotal_price || raw.subtotal_price || '0'),
        taxAmount: parseFloat(raw.total_tax || '0'),
        discountAmount: parseFloat(raw.total_discounts || '0'),
        paymentMethod: isCod ? 'CashOnDelivery' : 'Card',
        isCashOnDelivery: isCod,
        customer: {
          name: `${raw.customer?.first_name ?? raw.shipping_address?.first_name ?? ''} ${raw.customer?.last_name ?? raw.shipping_address?.last_name ?? ''}`.trim() || 'Valued Customer',
          email: raw.email || raw.customer?.email || 'unspecified@customer.com',
          phone: raw.customer?.phone || raw.shipping_address?.phone || '',
          shippingAddress: {
            city: raw.shipping_address?.city || 'Default City',
            countryCode: raw.shipping_address?.country_code || raw.shipping_address?.country || 'US',
            streetLine: raw.shipping_address?.address1 || ''
          }
        },
        items,
        rawEventTimestamp: raw.created_at || new Date().toISOString(),
        normalizedAtUtc: new Date().toISOString()
      };
    }

    // WooCommerce or Generic Fallback
    const isCod = (raw.payment_method ?? '').toLowerCase().includes('cod') ||
                  (raw.payment_method_title ?? '').toLowerCase().includes('cash');
    const items = (raw.line_items ?? raw.items ?? []).map((it: any) => ({
      sku: it.sku || `SKU-${it.id}`,
      name: it.name || it.title || 'Product',
      quantity: it.quantity ?? 1,
      unitPrice: typeof it.price === 'number' ? it.price : (parseFloat(it.price) || 0),
      totalPrice: (it.quantity ?? 1) * (typeof it.price === 'number' ? it.price : (parseFloat(it.price) || 0))
    }));

    return {
      canonicalOrderId: `canon-generic-${raw.id || Date.now()}`,
      sourcePlatform: raw.payment_method_title ? 'WooCommerce' : 'GenericCommerce',
      externalOrderId: String(raw.id || raw.order_id || 'UNKNOWN'),
      orderNumber: String(raw.number || raw.order_number || raw.id || '#GEN-001'),
      currency: raw.currency || raw.currency_code || 'SAR',
      totalAmount: parseFloat(raw.total || raw.total_amount || raw.amount || '0'),
      subtotalAmount: parseFloat(raw.subtotal || raw.sub_total || raw.total || '0'),
      taxAmount: parseFloat(raw.total_tax || raw.tax || '0'),
      discountAmount: 0,
      paymentMethod: isCod ? 'CashOnDelivery' : 'Card',
      isCashOnDelivery: isCod,
      customer: {
        name: `${raw.billing?.first_name ?? raw.customer?.name ?? ''} ${raw.billing?.last_name ?? ''}`.trim() || 'Customer',
        email: raw.billing?.email || raw.customer?.email || '',
        phone: raw.billing?.phone || raw.customer?.phone || '',
        shippingAddress: {
          city: raw.billing?.city || raw.shipping?.city || 'Riyadh',
          countryCode: raw.billing?.country || raw.shipping?.country || 'SA'
        }
      },
      items,
      rawEventTimestamp: new Date().toISOString(),
      normalizedAtUtc: new Date().toISOString()
    };
  }

  scrollToPlayground(): void {
    const el = document.getElementById('playground-section');
    if (el) {
      el.scrollIntoView({ behavior: 'smooth' });
    }
  }

  getTierBadgeClass(tier: string): string {
    switch (tier) {
      case 'RegionalMENA':
        return 'text-emerald-300 bg-emerald-500/10 border border-emerald-500/20 px-2 py-0.5 rounded font-mono font-bold';
      case 'GlobalEnterprise':
        return 'text-cyan-300 bg-cyan-500/10 border border-cyan-500/20 px-2 py-0.5 rounded font-mono font-bold';
      case 'OpenSource':
        return 'text-purple-300 bg-purple-500/10 border border-purple-500/20 px-2 py-0.5 rounded font-mono font-bold';
      default:
        return 'text-slate-300 bg-slate-800 px-2 py-0.5 rounded font-mono';
    }
  }

  getStatusClass(status: string): string {
    if (status === 'Installed') {
      return 'text-[10px] font-mono px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-semibold';
    }
    return 'text-[10px] font-mono px-2 py-0.5 rounded-full bg-slate-800 text-slate-400 border border-slate-700';
  }
}
