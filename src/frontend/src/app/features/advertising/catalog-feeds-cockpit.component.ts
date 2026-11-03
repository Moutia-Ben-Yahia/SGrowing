import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CatalogFeedItem {
  feedId: string;
  channel: string;
  format: string;
  totalItems: number;
  lastGeneratedAt: string;
  isCompliant: boolean;
  endpointUrl: string;
}

export interface CatalogProductRecord {
  productId: string;
  title: string;
  price: number;
  availability: string;
  category: string;
  badges: string[];
}

@Component({
  selector: 'app-catalog-feeds-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="catalog-feeds-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-amber-500/20 text-amber-400 border border-amber-500/30">Horizon 82</span>
            <span class="text-xs text-slate-400">Dynamic Product Ads (DPA) Feeds</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Omnichannel Creative DPA Catalog Feed Engine</h1>
          <p class="text-sm text-slate-400">Automated multi-network catalog syndication, real-time merchant policy compliance, and dynamic badge overlays.</p>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="syncAllFeeds()" data-testid="sync-feeds-btn" class="px-4 py-2 bg-amber-600 hover:bg-amber-500 text-white rounded font-medium text-sm transition-colors shadow">
            Syndicate All Feeds
          </button>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-active-feeds">
          <div class="text-xs font-medium text-slate-400 uppercase">Syndicated Ad Feeds</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ feeds().length }} Channels</div>
          <div class="text-xs text-slate-400 mt-1">Google, Meta, TikTok, Pinterest</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-catalog-items">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Catalog SKUs</div>
          <div class="text-2xl font-bold text-white mt-1">{{ products().length }} Products</div>
          <div class="text-xs text-emerald-400 mt-1">100% In-Stock & Verified</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-compliance-status">
          <div class="text-xs font-medium text-slate-400 uppercase">Merchant Compliance</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">100% Valid</div>
          <div class="text-xs text-slate-400 mt-1">0 Disapproved Products</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-badges">
          <div class="text-xs font-medium text-slate-400 uppercase">Dynamic Badges Active</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ totalBadges() }} Overlays</div>
          <div class="text-xs text-slate-400 mt-1">Boosting CTR by avg +18.4%</div>
        </div>
      </section>

      <!-- Feeds Grid -->
      <div class="grid grid-cols-1 md:grid-cols-3 gap-6 mb-6">
        @for (feed of feeds(); track feed.feedId) {
          <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5" [attr.data-testid]="'feed-card-' + feed.feedId">
            <div class="flex items-center justify-between mb-3">
              <span class="text-sm font-bold text-white">{{ feed.channel }}</span>
              <span class="px-2 py-0.5 text-xs font-medium rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                {{ feed.format }}
              </span>
            </div>

            <div class="text-xs text-slate-400 mb-2 truncate font-mono">{{ feed.endpointUrl }}</div>
            <div class="text-xs text-slate-300 mb-4">Last Generated: {{ feed.lastGeneratedAt | date:'mediumTime' }}</div>

            <div class="flex items-center justify-between pt-3 border-t border-slate-700/40">
              <span class="text-xs text-slate-400">{{ feed.totalItems }} Items Synced</span>
              <span class="text-xs text-emerald-400 font-medium">Auto-Refreshing</span>
            </div>
          </div>
        }
      </div>

      <!-- Products & Badges Table -->
      <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
        <h2 class="text-base font-semibold text-white mb-4">Catalog Products & Dynamic Conversion Badges</h2>
        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="products-table">
            <thead class="text-xs text-slate-400 uppercase border-b border-slate-700/60 bg-slate-800/40">
              <tr>
                <th class="p-3">SKU</th>
                <th class="p-3">Title</th>
                <th class="p-3">Price</th>
                <th class="p-3">Category</th>
                <th class="p-3">Active Badges</th>
                <th class="p-3">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-700/40">
              @for (prod of products(); track prod.productId) {
                <tr class="hover:bg-slate-700/20" [attr.data-testid]="'product-row-' + prod.productId">
                  <td class="p-3 font-mono text-xs text-slate-400">{{ prod.productId }}</td>
                  <td class="p-3 font-medium text-white">{{ prod.title }}</td>
                  <td class="p-3 text-slate-300">\${{ prod.price | number }}</td>
                  <td class="p-3 text-xs text-slate-400">{{ prod.category }}</td>
                  <td class="p-3">
                    <div class="flex flex-wrap gap-1.5">
                      @for (badge of prod.badges; track badge) {
                        <span class="px-2 py-0.5 text-xs rounded bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                          {{ badge }}
                        </span>
                      }
                    </div>
                  </td>
                  <td class="p-3">
                    <button (click)="addPromoBadge(prod)" [attr.data-testid]="'add-badge-btn-' + prod.productId" class="text-xs px-2.5 py-1 rounded bg-slate-800 hover:bg-slate-700 text-slate-200 border border-slate-700">
                      + Add Promo Badge
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class CatalogFeedsCockpitComponent {
  feeds = signal<CatalogFeedItem[]>([
    {
      feedId: 'feed_google_merchant',
      channel: 'Google Merchant Center',
      format: 'XML RSS 2.0',
      totalItems: 3,
      lastGeneratedAt: '2026-11-03T01:00:00Z',
      isCompliant: true,
      endpointUrl: 'https://cdn.sgrowing.internal/feeds/google-merchant.xml'
    },
    {
      feedId: 'feed_meta_advantage',
      channel: 'Meta Advantage+ Catalog',
      format: 'CSV/TSV',
      totalItems: 3,
      lastGeneratedAt: '2026-11-03T01:10:00Z',
      isCompliant: true,
      endpointUrl: 'https://cdn.sgrowing.internal/feeds/meta-dpa-catalog.csv'
    },
    {
      feedId: 'feed_tiktok_shop',
      channel: 'TikTok Shop Commerce',
      format: 'JSON-LD',
      totalItems: 3,
      lastGeneratedAt: '2026-11-03T01:25:00Z',
      isCompliant: true,
      endpointUrl: 'https://cdn.sgrowing.internal/feeds/tiktok-shop.json'
    }
  ]);

  products = signal<CatalogProductRecord[]>([
    {
      productId: 'prod_saas_ent_01',
      title: 'SGrowing Growth OS Enterprise Seat',
      price: 4999,
      availability: 'in_stock',
      category: 'Software > Business',
      badges: ['Free Dedicated Migration', 'Enterprise SLA']
    },
    {
      productId: 'prod_voice_copilot_02',
      title: 'Voice AI Autonomous SDR Seat',
      price: 899,
      availability: 'in_stock',
      category: 'Software > Communications',
      badges: ['Instant Provisioning']
    },
    {
      productId: 'prod_shard_gateway_03',
      title: 'Global Zero-Trust Multi-Region Shard Mesh Add-on',
      price: 1499,
      availability: 'in_stock',
      category: 'Software > Infrastructure',
      badges: ['SOC2 Certified']
    }
  ]);

  totalBadges = computed(() => this.products().reduce((sum, p) => sum + p.badges.length, 0));

  addPromoBadge(product: CatalogProductRecord) {
    this.products.update(list => list.map(p => {
      if (p.productId === product.productId && !p.badges.includes('Q4 Enterprise Special')) {
        return { ...p, badges: [...p.badges, 'Q4 Enterprise Special'] };
      }
      return p;
    }));
  }

  syncAllFeeds() {
    const now = new Date().toISOString();
    this.feeds.update(list => list.map(f => ({
      ...f,
      lastGeneratedAt: now
    })));
  }
}
