import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface MarketplaceApp {
  appId: string;
  name: string;
  description: string;
  category: 'CRM' | 'Commerce' | 'Advertising' | 'Productivity' | 'AI' | 'Communication';
  version: string;
  icon: string;
  author: string;
  requiredPermissions: string[];
  webhookEvents: string[];
  sandboxedIframeUrl?: string;
  isVerified: boolean;
}

export interface InstalledAppRecord {
  installationId: string;
  tenantId: string;
  appId: string;
  status: 'Installed' | 'Disabled' | 'Installing';
  config: Record<string, string>;
  installedAtUtc: string;
}

@Component({
  selector: 'app-app-marketplace-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="marketplace-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">App Marketplace & Extensions</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-indigo-100 text-indigo-800">
              Sandboxed v2.4
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Discover, install, and execute certified partner extensions with zero-trust sandboxed permissions.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="activeTab.set('catalog')"
            class="px-4 py-2 text-sm font-medium rounded-lg transition-colors"
            [ngClass]="activeTab() === 'catalog' ? 'bg-indigo-600 text-white shadow' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'"
            data-testid="tab-catalog">
            Catalog ({{ catalog().length }})
          </button>
          <button 
            (click)="activeTab.set('installed')"
            class="px-4 py-2 text-sm font-medium rounded-lg transition-colors"
            [ngClass]="activeTab() === 'installed' ? 'bg-indigo-600 text-white shadow' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'"
            data-testid="tab-installed">
            Installed ({{ installedApps().length }})
          </button>
        </div>
      </div>

      <!-- Category Filter Pills -->
      @if (activeTab() === 'catalog') {
        <div class="flex flex-wrap items-center gap-2">
          @for (cat of categories; track cat) {
            <button 
              (click)="selectedCategory.set(cat)"
              class="px-3 py-1.5 rounded-full text-xs font-medium transition-colors"
              [ngClass]="selectedCategory() === cat ? 'bg-slate-900 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'"
              [attr.data-testid]="'cat-filter-' + cat">
              {{ cat }}
            </button>
          }
        </div>
      }

      <!-- Main Content Area -->
      @if (activeTab() === 'catalog') {
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6" data-testid="apps-grid">
          @for (app of filteredCatalog(); track app.appId) {
            <div class="bg-white border rounded-xl p-5 shadow-sm hover:shadow-md transition-shadow flex flex-col justify-between">
              <div>
                <div class="flex items-start justify-between gap-3">
                  <div>
                    <span class="text-xs font-medium uppercase tracking-wider text-indigo-600">{{ app.category }}</span>
                    <h3 class="font-semibold text-slate-900 mt-0.5 text-base">{{ app.name }}</h3>
                    <p class="text-xs text-slate-400">by {{ app.author }} &bull; v{{ app.version }}</p>
                  </div>
                  @if (app.isVerified) {
                    <span class="inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold bg-emerald-100 text-emerald-800" title="Verified by SGrowing Security Engine">
                      ✓ Verified
                    </span>
                  }
                </div>
                <p class="text-xs text-slate-600 mt-3 line-clamp-3 leading-relaxed">
                  {{ app.description }}
                </p>

                <div class="mt-4 pt-3 border-t">
                  <span class="text-[11px] font-medium text-slate-400 block mb-1">Permissions:</span>
                  <div class="flex flex-wrap gap-1">
                    @for (perm of app.requiredPermissions; track perm) {
                      <span class="px-1.5 py-0.5 bg-slate-100 text-slate-600 rounded text-[10px] font-mono">
                        {{ perm }}
                      </span>
                    }
                  </div>
                </div>
              </div>

              <div class="mt-5 pt-3 border-t flex items-center justify-between">
                @if (isInstalled(app.appId)) {
                  <span class="text-xs font-semibold text-emerald-600 flex items-center gap-1">
                    ● Active
                  </span>
                  <button 
                    (click)="uninstallApp(app.appId)"
                    class="px-3 py-1.5 text-xs text-rose-600 hover:bg-rose-50 rounded font-medium transition-colors"
                    [attr.data-testid]="'btn-uninstall-' + app.appId">
                    Uninstall
                  </button>
                } @else {
                  <span class="text-xs text-slate-400">Ready to install</span>
                  <button 
                    (click)="openInstallModal(app)"
                    class="px-3 py-1.5 text-xs bg-indigo-600 hover:bg-indigo-700 text-white rounded font-medium transition-colors shadow-sm"
                    [attr.data-testid]="'btn-install-' + app.appId">
                    Install App
                  </button>
                }
              </div>
            </div>
          }
        </div>
      } @else {
        <!-- Installed Apps View -->
        <div class="bg-white border rounded-xl overflow-hidden shadow-sm" data-testid="installed-apps-list">
          @if (installedApps().length === 0) {
            <div class="p-12 text-center text-slate-500">
              <p class="font-medium">No marketplace applications installed yet.</p>
              <p class="text-xs text-slate-400 mt-1">Browse the catalog above to connect HubSpot, Shopify, Stripe, or Custom Extensions.</p>
            </div>
          } @else {
            <div class="divide-y divide-slate-100">
              @for (item of installedApps(); track item.installationId) {
                <div class="p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-4 hover:bg-slate-50 transition-colors">
                  <div class="flex items-center gap-4">
                    <div class="w-10 h-10 rounded-lg bg-indigo-50 border border-indigo-100 flex items-center justify-center font-bold text-indigo-600">
                      {{ item.appId.substring(4, 6).toUpperCase() }}
                    </div>
                    <div>
                      <h4 class="font-medium text-slate-900 text-sm">{{ getAppName(item.appId) }}</h4>
                      <div class="flex items-center gap-2 text-xs text-slate-500 mt-0.5">
                        <span>ID: <code class="font-mono text-[11px]">{{ item.installationId }}</code></span>
                        <span>&bull;</span>
                        <span>Installed {{ item.installedAtUtc | date:'short' }}</span>
                      </div>
                    </div>
                  </div>

                  <div class="flex items-center gap-3">
                    <span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-emerald-100 text-emerald-800">
                      {{ item.status }}
                    </span>
                    <button 
                      (click)="uninstallApp(item.appId)"
                      class="px-3 py-1.5 text-xs font-medium text-rose-600 hover:bg-rose-50 rounded transition-colors"
                      [attr.data-testid]="'btn-manage-uninstall-' + item.appId">
                      Remove
                    </button>
                  </div>
                </div>
              }
            </div>
          }
        </div>
      }

      <!-- Install App Modal Simulation -->
      @if (configuringApp(); as target) {
        <div class="fixed inset-0 bg-slate-900/50 backdrop-blur-sm z-50 flex items-center justify-center p-4" data-testid="install-modal">
          <div class="bg-white rounded-xl shadow-xl border max-w-md w-full p-6 space-y-4">
            <div>
              <h3 class="text-lg font-bold text-slate-900">Install {{ target.name }}</h3>
              <p class="text-xs text-slate-500 mt-1">Configure credentials and grant requested permission scopes.</p>
            </div>

            <div class="p-3 bg-amber-50 rounded-lg border border-amber-200 text-xs text-amber-800 space-y-1">
              <span class="font-bold">Required Permissions:</span>
              <ul class="list-disc pl-4 space-y-0.5">
                @for (perm of target.requiredPermissions; track perm) {
                  <li><code>{{ perm }}</code></li>
                }
              </ul>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-semibold text-slate-700 mb-1">API Key / Token</label>
                <input 
                  type="password" 
                  [(ngModel)]="tempApiKey" 
                  placeholder="Enter API token or credential"
                  class="w-full px-3 py-2 border rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
                  data-testid="input-api-key" />
              </div>
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t">
              <button 
                (click)="configuringApp.set(null)"
                class="px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 rounded-lg">
                Cancel
              </button>
              <button 
                (click)="confirmInstall()"
                class="px-4 py-2 text-xs font-semibold bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg shadow-sm"
                data-testid="btn-confirm-install">
                Authorize & Install
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AppMarketplaceCockpitComponent {
  activeTab = signal<'catalog' | 'installed'>('catalog');
  selectedCategory = signal<string>('All');
  categories = ['All', 'CRM', 'Commerce', 'Advertising', 'Productivity', 'AI', 'Communication'];

  catalog = signal<MarketplaceApp[]>([
    {
      appId: 'app_hubspot_sync',
      name: 'HubSpot Two-Way CRM Connector',
      description: 'Bidirectional sync of Contacts, Companies, and Deals with HubSpot Enterprise.',
      category: 'CRM',
      version: '2.4.0',
      icon: 'hubspot-orange',
      author: 'SGrowing Official',
      requiredPermissions: ['crm:read', 'crm:write'],
      webhookEvents: ['contact.created', 'deal.updated'],
      sandboxedIframeUrl: 'https://marketplace.sgrowing.io/apps/hubspot-sync/sandbox',
      isVerified: true
    },
    {
      appId: 'app_shopify_commerce',
      name: 'Shopify Storefront & Inventory Bridge',
      description: 'Automate cart sync, live order webhooks, and product SKU catalog updates.',
      category: 'Commerce',
      version: '3.1.2',
      icon: 'shopify-green',
      author: 'Shopify Certified',
      requiredPermissions: ['orders:read', 'inventory:write'],
      webhookEvents: ['orders/create', 'inventory_levels/update'],
      sandboxedIframeUrl: 'https://marketplace.sgrowing.io/apps/shopify-bridge/sandbox',
      isVerified: true
    },
    {
      appId: 'app_stripe_billing',
      name: 'Stripe Billing & Payment Gateway',
      description: 'Synchronize customer subscriptions, payment intents, and invoice dunning webhooks.',
      category: 'Commerce',
      version: '4.0.0',
      icon: 'stripe-purple',
      author: 'Stripe Partner',
      requiredPermissions: ['payments:read', 'subscriptions:write'],
      webhookEvents: ['invoice.payment_succeeded', 'charge.refunded'],
      sandboxedIframeUrl: 'https://marketplace.sgrowing.io/apps/stripe/sandbox',
      isVerified: true
    },
    {
      appId: 'app_meta_ads_capi',
      name: 'Meta Conversion API & Pixel Sync',
      description: 'Direct server-side event transmission to Meta Graph API for sub-second ROAS matching.',
      category: 'Advertising',
      version: '1.8.0',
      icon: 'meta-blue',
      author: 'SGrowing Official',
      requiredPermissions: ['ads:write', 'telemetry:read'],
      webhookEvents: ['conversion.attributed'],
      sandboxedIframeUrl: 'https://marketplace.sgrowing.io/apps/meta-capi/sandbox',
      isVerified: true
    },
    {
      appId: 'app_gemini_intelligence',
      name: 'Gemini 2.5 Multi-Modal Agent Copilot',
      description: 'Deep semantic embeddings, live video analysis, and autonomous prompt reasoning.',
      category: 'AI',
      version: '1.0.0',
      icon: 'gemini-sparkle',
      author: 'Google Cloud Ecosystem',
      requiredPermissions: ['ai:execute', 'prompts:read'],
      webhookEvents: ['agent.completed'],
      sandboxedIframeUrl: 'https://marketplace.sgrowing.io/apps/gemini-ai/sandbox',
      isVerified: true
    }
  ]);

  installedApps = signal<InstalledAppRecord[]>([
    {
      installationId: 'inst_seed101',
      tenantId: 'tenant_default',
      appId: 'app_hubspot_sync',
      status: 'Installed',
      config: { apiKey: '••••••••••••' },
      installedAtUtc: new Date().toISOString()
    }
  ]);

  configuringApp = signal<MarketplaceApp | null>(null);
  tempApiKey = '';

  filteredCatalog = computed(() => {
    const cat = this.selectedCategory();
    if (cat === 'All') return this.catalog();
    return this.catalog().filter(a => a.category === cat);
  });

  isInstalled(appId: string): boolean {
    return this.installedApps().some(i => i.appId === appId);
  }

  getAppName(appId: string): string {
    const app = this.catalog().find(a => a.appId === appId);
    return app ? app.name : appId;
  }

  openInstallModal(app: MarketplaceApp): void {
    this.configuringApp.set(app);
    this.tempApiKey = '';
  }

  confirmInstall(): void {
    const target = this.configuringApp();
    if (!target) return;

    const newRecord: InstalledAppRecord = {
      installationId: 'inst_' + Math.random().toString(36).substring(2, 9),
      tenantId: 'tenant_default',
      appId: target.appId,
      status: 'Installed',
      config: { apiKey: this.tempApiKey || 'default_token' },
      installedAtUtc: new Date().toISOString()
    };

    this.installedApps.update(list => [...list, newRecord]);
    this.configuringApp.set(null);
  }

  uninstallApp(appId: string): void {
    this.installedApps.update(list => list.filter(i => i.appId !== appId));
  }
}
