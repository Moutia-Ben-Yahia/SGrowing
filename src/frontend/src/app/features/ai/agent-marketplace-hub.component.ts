import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';

export type MarketplaceItemType = 'AgentSwarm' | 'WorkflowTemplate' | 'ConnectorExtension' | 'PromptPack';
export type MarketplaceTier = 'Official' | 'VerifiedPartner' | 'Community';

export interface MarketplaceListingItem {
  id: string;
  slug: string;
  name: string;
  tagline: string;
  description: string;
  itemType: MarketplaceItemType;
  tier: MarketplaceTier;
  author: string;
  rating: number;
  installCount: number;
  priceSar: number;
  version: string;
  tags: string[];
  iconUrl: string;
  configurationSchemaJson?: string;
  manifestPayloadJson?: string;
  createdAtUtc: string;
}

export interface TenantInstalledItem {
  installationId: string;
  tenantId: string;
  listingId: string;
  listingName?: string;
  itemType?: MarketplaceItemType;
  tier?: MarketplaceTier;
  installedVersion: string;
  customSettings: Record<string, string>;
  installedAtUtc: string;
  isActive: boolean;
}

const INITIAL_CATALOG: MarketplaceListingItem[] = [
  {
    id: 'item-ramadan-cod-swarm',
    slug: 'ksa-ramadan-cod-campaign-swarm',
    name: 'KSA Ramadan COD Campaign Swarm',
    tagline: 'Autonomous multi-agent swarm orchestrating localized Saudi Ramadan COD campaigns across WhatsApp, SMS, and Snapchat.',
    description: 'Coordinates SMS, WhatsApp, and social ad flash campaigns aligned with Iftar/Suhoor peak conversion hours with automated cash-on-delivery risk score verification and anti-RTO address hygiene.',
    itemType: 'AgentSwarm',
    tier: 'Official',
    author: 'SGrowing Core Fleet',
    rating: 5.0,
    installCount: 1250,
    priceSar: 0,
    version: '2.4.0',
    tags: ['Ramadan', 'COD', 'Saudi Arabia', 'WhatsApp', 'Multi-Agent'],
    iconUrl: 'sparkles',
    configurationSchemaJson: JSON.stringify({
      dailyBudgetCapSar: '2500',
      suhoorWindowStartUtc: '01:00',
      maxCodRiskScore: '0.35',
      telephonyGateway: 'Unifonic'
    }),
    manifestPayloadJson: JSON.stringify({
      swarmCoordinator: 'EvoNexus-Swarm-V3',
      agents: ['RamadanTimingPlannerAgent', 'ArabicDialectCopyAgent', 'CodRiskAssessorAgent'],
      slaMinutes: 5
    }),
    createdAtUtc: '2026-08-01T00:00:00Z'
  },
  {
    id: 'item-salla-cart-recovery',
    slug: 'salla-whatsapp-cart-recovery-engine',
    name: 'Salla WhatsApp Cart Recovery Engine',
    tagline: 'Automated event-triggered WhatsApp cart abandonment recovery flow optimized for Saudi Salla merchants.',
    description: 'Delivers hyper-personalized conversational recovery sequences via WhatsApp Official Cloud API, incorporating one-click Mada/Apple Pay links.',
    itemType: 'WorkflowTemplate',
    tier: 'VerifiedPartner',
    author: 'Gulf Commerce Labs',
    rating: 4.9,
    installCount: 890,
    priceSar: 149,
    version: '1.8.2',
    tags: ['Salla', 'WhatsApp', 'Cart Recovery', 'KSA', 'Workflow'],
    iconUrl: 'shopping-bag',
    configurationSchemaJson: JSON.stringify({
      delayMinutes: '15',
      discountPercentage: '10',
      sendMadaDirectLink: 'true'
    }),
    manifestPayloadJson: JSON.stringify({
      engine: 'DurableWorkflowEngine',
      trigger: 'salla.cart.abandoned',
      steps: ['EvaluateCartValue', 'CheckEligibility', 'DispatchWhatsAppMessage']
    }),
    createdAtUtc: '2026-09-01T00:00:00Z'
  },
  {
    id: 'item-b2b-abm-hunter',
    slug: 'b2b-outbound-cold-abm-hunter',
    name: 'B2B Outbound Cold ABM Hunter',
    tagline: 'Autonomous account-based prospecting agent swarm with waterfall enrichment and personalized hyper-targeted outreach.',
    description: 'Orchestrates research, firmographic scoring, decision-maker extraction, and multi-touch email/LinkedIn sequences.',
    itemType: 'AgentSwarm',
    tier: 'Official',
    author: 'SGrowing Enterprise AI',
    rating: 4.8,
    installCount: 640,
    priceSar: 299,
    version: '3.1.0',
    tags: ['B2B', 'ABM', 'Cold Outreach', 'LinkedIn', 'Enrichment'],
    iconUrl: 'crosshair',
    configurationSchemaJson: JSON.stringify({
      targetIndustry: 'Logistics & E-Commerce',
      maxAccountsPerDay: '50',
      enrichmentSource: 'ApolloAndLinkedIn'
    }),
    manifestPayloadJson: JSON.stringify({
      swarmCoordinator: 'EvoNexus-Swarm-V3',
      agents: ['LeadDiscoveryAgent', 'FirmographicVerifierAgent', 'ColdSequenceDraftAgent']
    }),
    createdAtUtc: '2026-07-15T00:00:00Z'
  },
  {
    id: 'item-spintax-generator',
    slug: 'omnichannel-spintax-copy-generator',
    name: 'Omnichannel Spintax Copy Generator',
    tagline: 'High-variance spintax template generator for bulk SMS, email, and social ad copywriting.',
    description: 'Generates thousands of unique high-converting Arabic and English copywriting variations preventing spam filter fatigue.',
    itemType: 'PromptPack',
    tier: 'Community',
    author: 'GrowthHackerDXB',
    rating: 4.7,
    installCount: 420,
    priceSar: 0,
    version: '1.2.0',
    tags: ['Spintax', 'Copywriting', 'Prompt', 'Anti-Spam', 'Community'],
    iconUrl: 'type',
    configurationSchemaJson: JSON.stringify({
      defaultLanguage: 'Arabic-Gulf',
      varianceFactor: '0.85'
    }),
    manifestPayloadJson: JSON.stringify({
      promptTemplates: ['RamadanFlashOffer', 'AbandonedCartSpintax', 'VipReactivation']
    }),
    createdAtUtc: '2026-10-01T00:00:00Z'
  },
  {
    id: 'item-seo-geo-citability',
    slug: 'seo-geo-aeo-citability-booster',
    name: 'SEO GEO/AEO Citability Booster',
    tagline: 'Generative Engine Optimization (GEO) & Answer Engine Optimization (AEO) citation booster swarm.',
    description: 'Analyzes AI search engines (Perplexity, ChatGPT Search, Google AI Overviews) and restructures digital catalog entities for top citability.',
    itemType: 'AgentSwarm',
    tier: 'Official',
    author: 'SGrowing Search Intelligence',
    rating: 5.0,
    installCount: 1500,
    priceSar: 399,
    version: '4.0.1',
    tags: ['SEO', 'GEO', 'AEO', 'Perplexity', 'Knowledge Graph'],
    iconUrl: 'search',
    configurationSchemaJson: JSON.stringify({
      crawlIntervalHours: '24',
      targetEngines: 'Perplexity,ChatGPT,GoogleAI',
      autoUpdateJsonLd: 'true'
    }),
    manifestPayloadJson: JSON.stringify({
      swarmCoordinator: 'EvoNexus-Swarm-V3',
      agents: ['PerplexityAuditAgent', 'SchemaGeneratorAgent', 'EntityGraphPublisherAgent']
    }),
    createdAtUtc: '2026-06-10T00:00:00Z'
  },
  {
    id: 'item-shopify-flash-sale',
    slug: 'shopify-flash-sale-surge-orchestrator',
    name: 'Shopify Flash Sale Surge Orchestrator',
    tagline: 'High-throughput flash sale workflow managing live inventory locks, queue throttling, and abandoned checkouts.',
    description: 'Synchronizes Shopify inventory with edge Redis counters during Black Friday / National Day flash sales to eliminate overselling.',
    itemType: 'WorkflowTemplate',
    tier: 'VerifiedPartner',
    author: 'HyperScale Partners',
    rating: 4.9,
    installCount: 780,
    priceSar: 199,
    version: '2.0.5',
    tags: ['Shopify', 'Flash Sale', 'High Concurrency', 'Workflow', 'Redis'],
    iconUrl: 'zap',
    configurationSchemaJson: JSON.stringify({
      redisTtlSeconds: '300',
      maxConcurrentCheckouts: '10000',
      autoReplenishBuffer: 'false'
    }),
    manifestPayloadJson: JSON.stringify({
      engine: 'DurableWorkflowEngine',
      trigger: 'shopify.campaign.surge_detected',
      steps: ['AcquireRedisLock', 'ValidateInventoryState', 'ThrottleQueue', 'ReleaseLock']
    }),
    createdAtUtc: '2026-09-15T00:00:00Z'
  }
];

@Component({
  selector: 'app-agent-marketplace-hub',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Top Application Header -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur px-6 py-4 flex flex-wrap items-center justify-between gap-4 sticky top-0 z-40">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-white">Agent & Workflow Marketplace Hub</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-medium">
              PRD Phase 11 &bull; Community Hub
            </span>
          </div>
        </div>

        <!-- Global Search in Header -->
        <div class="flex items-center gap-3">
          <div class="relative w-64 md:w-80">
            <span class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
              <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
              </svg>
            </span>
            <input
              type="text"
              [ngModel]="searchQuery()"
              (ngModelChange)="searchQuery.set($event)"
              placeholder="Search swarms, workflows, prompts..."
              class="w-full pl-9 pr-8 py-2 bg-slate-900 border border-slate-700/80 rounded-lg text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500 transition"
            />
            @if (searchQuery()) {
              <button
                type="button"
                (click)="searchQuery.set('')"
                class="absolute inset-y-0 right-0 pr-2.5 flex items-center text-slate-400 hover:text-white"
                title="Clear search"
              >
                &times;
              </button>
            }
          </div>

          <a
            routerLink="/ai/mission-control"
            class="px-3.5 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-200 border border-slate-700 transition flex items-center gap-1.5"
          >
            <span>🧭</span> Mission DAG
          </a>
        </div>
      </header>

      <!-- Main Container -->
      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- Toast Notification Banner -->
        @if (notificationMessage()) {
          <div class="bg-cyan-950/80 border border-cyan-700/60 text-cyan-200 px-4 py-3 rounded-xl flex items-center justify-between shadow-lg animate-fadeIn">
            <div class="flex items-center gap-2 text-xs font-medium">
              <span class="text-cyan-400 text-base">✨</span>
              <span>{{ notificationMessage() }}</span>
            </div>
            <button (click)="notificationMessage.set(null)" class="text-cyan-400 hover:text-white text-sm font-bold ml-4">
              &times;
            </button>
          </div>
        }

        <!-- Metric Summary Cards -->
        <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
            <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Catalog Solutions</div>
            <div class="text-2xl font-bold text-white mt-1.5">{{ totalCatalogCount() }} Curated</div>
            <div class="text-[11px] text-cyan-400 mt-0.5">Swarms, Flows & Prompts</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
            <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Installed in Workspace</div>
            <div class="text-2xl font-bold text-emerald-400 mt-1.5">{{ activeInstalledCount() }} Active</div>
            <div class="text-[11px] text-emerald-400/80 mt-0.5">Automated DAG Nodes</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
            <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Official AI Swarms</div>
            <div class="text-2xl font-bold text-cyan-400 mt-1.5">{{ officialCount() }} Verified</div>
            <div class="text-[11px] text-slate-400 mt-0.5">SGrowing Core Fleet</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-4">
            <div class="text-[11px] font-semibold text-slate-400 uppercase tracking-wider">Ecosystem Partners</div>
            <div class="text-2xl font-bold text-indigo-400 mt-1.5">{{ partnerCount() + communityCount() }} Listings</div>
            <div class="text-[11px] text-slate-400 mt-0.5">MENA & Global Tech Labs</div>
          </div>
        </div>

        <!-- Navigation Tabs: Explore vs Installed -->
        <div class="border-b border-slate-800 flex items-center justify-between">
          <div class="flex gap-2">
            <button
              type="button"
              (click)="activeTab.set('explore')"
              [class]="activeTab() === 'explore'
                ? 'px-4 py-2.5 text-xs font-bold text-cyan-400 border-b-2 border-cyan-400 flex items-center gap-2 transition'
                : 'px-4 py-2.5 text-xs font-medium text-slate-400 hover:text-slate-200 border-b-2 border-transparent flex items-center gap-2 transition'"
            >
              <span>Explore Catalog</span>
              <span class="px-2 py-0.5 text-[10px] rounded-full bg-slate-800 text-slate-300">
                {{ filteredListings().length }}
              </span>
            </button>

            <button
              type="button"
              (click)="activeTab.set('installed')"
              [class]="activeTab() === 'installed'
                ? 'px-4 py-2.5 text-xs font-bold text-emerald-400 border-b-2 border-emerald-400 flex items-center gap-2 transition'
                : 'px-4 py-2.5 text-xs font-medium text-slate-400 hover:text-slate-200 border-b-2 border-transparent flex items-center gap-2 transition'"
            >
              <span>Installed in Workspace</span>
              <span class="px-2 py-0.5 text-[10px] rounded-full bg-emerald-950 text-emerald-300 border border-emerald-800">
                {{ activeInstalledCount() }}
              </span>
            </button>
          </div>

          <div class="hidden sm:flex items-center gap-2 text-xs text-slate-400 pb-2">
            <span>Workspace:</span>
            <span class="font-mono text-cyan-300 bg-slate-900 px-2 py-0.5 rounded border border-slate-800">00000000-0000-0000-0000-000000000001</span>
          </div>
        </div>

        <!-- TAB 1: EXPLORE CATALOG -->
        @if (activeTab() === 'explore') {
          <!-- Filter Controls Bar -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4 flex flex-col md:flex-row md:items-center justify-between gap-4">
            <!-- Type Filters -->
            <div class="flex flex-wrap items-center gap-2">
              <span class="text-xs font-semibold text-slate-400 mr-1">Type:</span>
              @for (tab of typeFilters; track tab.value) {
                <button
                  type="button"
                  (click)="selectedType.set(tab.value)"
                  [class]="selectedType() === tab.value
                    ? 'px-3 py-1.5 rounded-lg text-xs font-bold bg-cyan-600 text-white shadow transition'
                    : 'px-3 py-1.5 rounded-lg text-xs font-medium bg-slate-800/80 hover:bg-slate-700 text-slate-300 transition'"
                >
                  {{ tab.label }}
                </button>
              }
            </div>

            <!-- Tier Filters -->
            <div class="flex flex-wrap items-center gap-2">
              <span class="text-xs font-semibold text-slate-400 mr-1">Tier:</span>
              @for (tier of tierFilters; track tier.value) {
                <button
                  type="button"
                  (click)="selectedTier.set(tier.value)"
                  [class]="selectedTier() === tier.value
                    ? 'px-3 py-1.5 rounded-lg text-xs font-bold bg-teal-600 text-white shadow transition'
                    : 'px-3 py-1.5 rounded-lg text-xs font-medium bg-slate-800/80 hover:bg-slate-700 text-slate-300 transition'"
                >
                  {{ tier.label }}
                </button>
              }
            </div>
          </div>

          <!-- Listings Responsive Grid -->
          @if (filteredListings().length === 0) {
            <div class="bg-slate-900/40 border border-dashed border-slate-800 rounded-2xl p-12 text-center space-y-3">
              <div class="text-3xl">🔍</div>
              <h3 class="text-base font-bold text-slate-200">No marketplace solutions matched your criteria</h3>
              <p class="text-xs text-slate-400 max-w-md mx-auto">
                Try clearing search terms or resetting filters to browse all official and community offerings.
              </p>
              <button
                type="button"
                (click)="resetFilters()"
                class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-xs font-semibold transition"
              >
                Reset All Filters
              </button>
            </div>
          } @else {
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              @for (item of filteredListings(); track item.id) {
                @let isInstalled = isItemInstalled(item.id);
                @let priceLabel = item.priceSar === 0 ? 'Free' : 'SAR ' + item.priceSar;

                <div class="bg-slate-900/80 border border-slate-800 hover:border-slate-700/80 rounded-2xl p-5 flex flex-col justify-between shadow-lg transition duration-200 hover:shadow-cyan-950/20 group">
                  <div class="space-y-4">
                    <!-- Top Card Header: Icon, Title, Tier -->
                    <div class="flex items-start justify-between gap-3">
                      <div class="flex items-center gap-3">
                        <div [class]="getIconContainerClass(item.itemType)">
                          <span class="text-lg">{{ getIconSymbol(item.iconUrl) }}</span>
                        </div>
                        <div>
                          <h3 class="text-sm font-bold text-white group-hover:text-cyan-300 transition line-clamp-1">
                            {{ item.name }}
                          </h3>
                          <div class="text-[11px] text-slate-400 mt-0.5">
                            by <span class="text-slate-300 font-medium">{{ item.author }}</span>
                          </div>
                        </div>
                      </div>

                      <span [class]="getTierBadgeClass(item.tier)">
                        {{ getTierLabel(item.tier) }}
                      </span>
                    </div>

                    <!-- Tagline & Description -->
                    <p class="text-xs text-slate-300 line-clamp-2 leading-relaxed">
                      {{ item.tagline }}
                    </p>

                    <!-- Rating & Installs -->
                    <div class="flex items-center gap-4 text-xs text-slate-400">
                      <div class="flex items-center gap-1 text-amber-400 font-bold">
                        <span>★</span>
                        <span>{{ item.rating.toFixed(1) }}</span>
                      </div>
                      <div class="flex items-center gap-1">
                        <span>📥</span>
                        <span class="font-medium text-slate-300">{{ item.installCount.toLocaleString() }} installs</span>
                      </div>
                      <div class="text-slate-500 font-mono text-[11px]">
                        v{{ item.version }}
                      </div>
                    </div>

                    <!-- Tags -->
                    <div class="flex flex-wrap gap-1.5 pt-1">
                      @for (tag of item.tags.slice(0, 3); track tag) {
                        <span class="px-2 py-0.5 text-[10px] rounded-md bg-slate-800/80 text-slate-300 border border-slate-700/50">
                          #{{ tag }}
                        </span>
                      }
                      @if (item.tags.length > 3) {
                        <span class="px-1.5 py-0.5 text-[10px] rounded-md bg-slate-800/50 text-slate-400">
                          +{{ item.tags.length - 3 }}
                        </span>
                      }
                    </div>
                  </div>

                  <!-- Card Action Footer -->
                  <div class="border-t border-slate-800/80 pt-4 mt-5 flex items-center justify-between">
                    <div>
                      <span [class]="item.priceSar === 0 ? 'text-xs font-bold text-emerald-400' : 'text-xs font-bold text-white'">
                        {{ priceLabel }}
                      </span>
                      @if (item.priceSar > 0) {
                        <span class="text-[10px] text-slate-500 block">one-time</span>
                      }
                    </div>

                    <div class="flex items-center gap-2">
                      <button
                        type="button"
                        (click)="openDetailModal(item)"
                        class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-200 border border-slate-700 transition"
                      >
                        Inspect
                      </button>

                      @if (isInstalled) {
                        <button
                          type="button"
                          (click)="openDetailModal(item)"
                          class="px-3.5 py-1.5 rounded-lg bg-emerald-600/20 text-emerald-300 border border-emerald-500/30 text-xs font-bold hover:bg-emerald-600/30 transition flex items-center gap-1"
                        >
                          <span>✓</span> Configured
                        </button>
                      } @else {
                        <button
                          type="button"
                          (click)="openDetailModal(item)"
                          class="px-3.5 py-1.5 rounded-lg bg-gradient-to-r from-cyan-600 to-teal-600 hover:from-cyan-500 hover:to-teal-500 text-white text-xs font-bold shadow-md shadow-cyan-950/40 transition flex items-center gap-1"
                        >
                          <span>⚡</span> Install
                        </button>
                      }
                    </div>
                  </div>
                </div>
              }
            </div>
          }
        }

        <!-- TAB 2: INSTALLED IN WORKSPACE -->
        @if (activeTab() === 'installed') {
          @if (installedListings().length === 0) {
            <div class="bg-slate-900/40 border border-dashed border-slate-800 rounded-2xl p-12 text-center space-y-4">
              <div class="text-4xl">📦</div>
              <h3 class="text-base font-bold text-slate-200">No solutions currently installed</h3>
              <p class="text-xs text-slate-400 max-w-md mx-auto">
                Explore the marketplace catalog to install curated Saudi Ramadan swarms, cart recovery flows, or spintax generators into this workspace.
              </p>
              <button
                type="button"
                (click)="activeTab.set('explore')"
                class="px-4 py-2 bg-gradient-to-r from-cyan-600 to-teal-600 text-white rounded-lg text-xs font-bold shadow-lg shadow-cyan-950/40 transition"
              >
                Browse Marketplace Catalog
              </button>
            </div>
          } @else {
            <div class="bg-slate-900/80 border border-slate-800 rounded-2xl overflow-hidden shadow-xl">
              <div class="p-4 border-b border-slate-800 flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-bold text-white">Active Workspace Deployments</h3>
                  <p class="text-[11px] text-slate-400">Manage autonomous configurations and active hooks</p>
                </div>
                <span class="text-xs text-emerald-400 bg-emerald-950/60 border border-emerald-800/80 px-2.5 py-1 rounded-full font-medium">
                  {{ activeInstalledCount() }} Active in Tenant
                </span>
              </div>

              <div class="divide-y divide-slate-800/80">
                @for (installed of installedListings(); track installed.installationId) {
                  @let catalogItem = getCatalogItemById(installed.listingId);

                  <div class="p-5 flex flex-col md:flex-row md:items-center justify-between gap-4 hover:bg-slate-800/30 transition">
                    <div class="flex items-start gap-3.5">
                      <div class="w-10 h-10 rounded-xl bg-cyan-950/80 border border-cyan-800/50 flex items-center justify-center text-lg text-cyan-300">
                        {{ catalogItem ? getIconSymbol(catalogItem.iconUrl) : '⚡' }}
                      </div>
                      <div class="space-y-1">
                        <div class="flex items-center gap-2">
                          <h4 class="text-sm font-bold text-white">{{ catalogItem?.name || installed.listingId }}</h4>
                          <span class="px-2 py-0.5 text-[10px] rounded-full bg-slate-800 text-slate-300 font-mono">
                            v{{ installed.installedVersion }}
                          </span>
                          <span class="px-2 py-0.5 text-[10px] rounded-full bg-emerald-950 text-emerald-400 border border-emerald-800 font-semibold">
                            Active
                          </span>
                        </div>
                        <p class="text-xs text-slate-400 line-clamp-1 max-w-xl">
                          {{ catalogItem?.tagline || 'Custom workflow extension integration' }}
                        </p>
                        <div class="flex items-center gap-4 text-[11px] text-slate-500 pt-1">
                          <span>Installed: {{ installed.installedAtUtc | date:'mediumDate' }}</span>
                          <span>Settings: {{ getSettingsCount(installed.customSettings) }} customized</span>
                          <span class="font-mono text-[10px] text-slate-600">ID: {{ installed.installationId.substring(0, 8) }}...</span>
                        </div>
                      </div>
                    </div>

                    <!-- Action buttons -->
                    <div class="flex items-center gap-2.5 self-end md:self-center">
                      @if (catalogItem) {
                        <button
                          type="button"
                          (click)="openDetailModal(catalogItem)"
                          class="px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-200 border border-slate-700 transition"
                        >
                          Configure
                        </button>
                      }
                      <button
                        type="button"
                        (click)="uninstallItem(installed.installationId)"
                        [disabled]="isUninstalling()"
                        class="px-3 py-1.5 rounded-lg bg-rose-950/60 hover:bg-rose-900/60 text-rose-300 border border-rose-800/60 text-xs font-semibold transition"
                      >
                        Uninstall
                      </button>
                    </div>
                  </div>
                }
              </div>
            </div>
          }
        }
      </main>

      <!-- DETAIL & CONFIGURATION MODAL / DRAWER -->
      @if (isModalOpen() && selectedListing(); as item) {
        @let isCurrentlyInstalled = isItemInstalled(item.id);

        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-2xl w-full max-h-[90vh] flex flex-col shadow-2xl overflow-hidden animate-scaleIn">
            <!-- Modal Header -->
            <div class="p-6 border-b border-slate-800 flex items-center justify-between">
              <div class="flex items-center gap-3">
                <div [class]="getIconContainerClass(item.itemType)">
                  <span class="text-xl">{{ getIconSymbol(item.iconUrl) }}</span>
                </div>
                <div>
                  <div class="flex items-center gap-2">
                    <h2 class="text-base font-bold text-white">{{ item.name }}</h2>
                    <span [class]="getTierBadgeClass(item.tier)">{{ getTierLabel(item.tier) }}</span>
                  </div>
                  <div class="text-xs text-slate-400 mt-0.5">
                    Version <span class="font-mono text-slate-300">v{{ item.version }}</span> &bull; Author: <span class="text-slate-300">{{ item.author }}</span>
                  </div>
                </div>
              </div>

              <button
                type="button"
                (click)="closeModal()"
                class="w-8 h-8 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 flex items-center justify-center text-lg transition"
                title="Close"
              >
                &times;
              </button>
            </div>

            <!-- Modal Content Body -->
            <div class="p-6 overflow-y-auto space-y-5 flex-1">
              <!-- Description -->
              <div class="space-y-1.5">
                <h4 class="text-xs font-bold text-slate-400 uppercase tracking-wider">Solution Overview</h4>
                <p class="text-xs text-slate-300 leading-relaxed bg-slate-950/60 p-3.5 rounded-xl border border-slate-800/80">
                  {{ item.description }}
                </p>
              </div>

              <!-- Configuration Schema Key-Values -->
              <div class="space-y-3">
                <div class="flex items-center justify-between">
                  <h4 class="text-xs font-bold text-slate-400 uppercase tracking-wider">Tenant Custom Settings</h4>
                  <span class="text-[11px] text-cyan-400">Schema validated</span>
                </div>

                <div class="bg-slate-950/60 p-4 rounded-xl border border-slate-800/80 space-y-3">
                  @for (key of objectKeys(customConfig()); track key) {
                    <div>
                      <label class="block text-xs font-medium text-slate-300 mb-1 font-mono">
                        {{ key }}
                      </label>
                      <input
                        type="text"
                        [ngModel]="customConfig()[key]"
                        (ngModelChange)="updateConfigField(key, $event)"
                        class="w-full px-3 py-2 bg-slate-900 border border-slate-700/80 rounded-lg text-xs text-white focus:outline-none focus:border-cyan-500 font-mono"
                      />
                    </div>
                  }

                  <div class="pt-2 flex items-center justify-between text-[11px] text-slate-500">
                    <span>Variables are injected into the agent swarm runtime DAG environment.</span>
                  </div>
                </div>
              </div>

              <!-- Manifest Payload Inspection -->
              @if (item.manifestPayloadJson) {
                <div class="space-y-1.5">
                  <h4 class="text-xs font-bold text-slate-400 uppercase tracking-wider">Orchestration Manifest Spec</h4>
                  <pre class="bg-slate-950 p-3 rounded-xl border border-slate-800 text-[11px] text-cyan-300 font-mono overflow-x-auto max-h-36">{{ item.manifestPayloadJson }}</pre>
                </div>
              }
            </div>

            <!-- Modal Footer -->
            <div class="p-5 border-t border-slate-800 bg-slate-900/60 flex items-center justify-between">
              <div class="text-xs text-slate-400">
                Price: <span class="font-bold text-white">{{ item.priceSar === 0 ? 'Free' : 'SAR ' + item.priceSar }}</span>
              </div>

              <div class="flex items-center gap-3">
                <button
                  type="button"
                  (click)="closeModal()"
                  class="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-semibold text-slate-300 transition"
                >
                  Cancel
                </button>

                @if (isCurrentlyInstalled) {
                  <button
                    type="button"
                    (click)="saveConfiguration()"
                    class="px-4 py-2 rounded-lg bg-teal-600 hover:bg-teal-500 text-white text-xs font-bold shadow-lg shadow-teal-950/40 transition flex items-center gap-1.5"
                  >
                    <span>✓</span> Update Settings
                  </button>
                } @else {
                  <button
                    type="button"
                    (click)="confirmInstall(item)"
                    [disabled]="isInstalling()"
                    class="px-4 py-2 rounded-lg bg-gradient-to-r from-cyan-600 to-teal-600 hover:from-cyan-500 hover:to-teal-500 text-white text-xs font-bold shadow-lg shadow-cyan-950/40 transition flex items-center gap-1.5"
                  >
                    @if (isInstalling()) {
                      <span class="animate-spin inline-block w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full"></span>
                      <span>Installing Swarm...</span>
                    } @else {
                      <span>⚡</span> Confirm & Install
                    }
                  </button>
                }
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AgentMarketplaceHubComponent {
  private readonly translation = inject(TranslationService, { optional: true });

  // Tab & Filters State
  readonly activeTab = signal<'explore' | 'installed'>('explore');
  readonly searchQuery = signal<string>('');
  readonly selectedType = signal<string>('All');
  readonly selectedTier = signal<string>('All');

  // Catalog & Workspace Installed State
  readonly listings = signal<MarketplaceListingItem[]>(INITIAL_CATALOG);
  readonly installedItems = signal<TenantInstalledItem[]>([
    {
      installationId: '11111111-1111-1111-1111-111111111111',
      tenantId: '00000000-0000-0000-0000-000000000001',
      listingId: 'item-ramadan-cod-swarm',
      listingName: 'KSA Ramadan COD Campaign Swarm',
      itemType: 'AgentSwarm',
      tier: 'Official',
      installedVersion: '2.4.0',
      customSettings: {
        dailyBudgetCapSar: '2500',
        telephonyGateway: 'Unifonic'
      },
      installedAtUtc: '2026-11-05T10:00:00Z',
      isActive: true
    }
  ]);

  // Modal State
  readonly isModalOpen = signal<boolean>(false);
  readonly selectedListing = signal<MarketplaceListingItem | null>(null);
  readonly customConfig = signal<Record<string, string>>({});
  readonly isInstalling = signal<boolean>(false);
  readonly isUninstalling = signal<boolean>(false);
  readonly notificationMessage = signal<string | null>(null);

  readonly typeFilters = [
    { label: 'All Solutions', value: 'All' },
    { label: 'Agent Swarms', value: 'AgentSwarm' },
    { label: 'Workflows', value: 'WorkflowTemplate' },
    { label: 'Connectors', value: 'ConnectorExtension' },
    { label: 'Prompt Packs', value: 'PromptPack' }
  ];

  readonly tierFilters = [
    { label: 'All Tiers', value: 'All' },
    { label: 'Official SGrowing', value: 'Official' },
    { label: 'Verified Partners', value: 'VerifiedPartner' },
    { label: 'Community', value: 'Community' }
  ];

  // Computed Values
  readonly totalCatalogCount = computed(() => this.listings().length);

  readonly activeInstalledCount = computed(() =>
    this.installedItems().filter(i => i.isActive).length
  );

  readonly installedListings = computed(() =>
    this.installedItems().filter(i => i.isActive)
  );

  readonly officialCount = computed(() =>
    this.listings().filter(l => l.tier === 'Official').length
  );

  readonly partnerCount = computed(() =>
    this.listings().filter(l => l.tier === 'VerifiedPartner').length
  );

  readonly communityCount = computed(() =>
    this.listings().filter(l => l.tier === 'Community').length
  );

  readonly filteredListings = computed(() => {
    let list = this.listings();
    const type = this.selectedType();
    const tier = this.selectedTier();
    const query = this.searchQuery().trim().toLowerCase();

    if (type !== 'All') {
      list = list.filter(item => item.itemType === type);
    }

    if (tier !== 'All') {
      list = list.filter(item => item.tier === tier);
    }

    if (query) {
      list = list.filter(item =>
        item.name.toLowerCase().includes(query) ||
        item.tagline.toLowerCase().includes(query) ||
        item.description.toLowerCase().includes(query) ||
        item.author.toLowerCase().includes(query) ||
        item.tags.some(t => t.toLowerCase().includes(query))
      );
    }

    return list;
  });

  resetFilters(): void {
    this.searchQuery.set('');
    this.selectedType.set('All');
    this.selectedTier.set('All');
  }

  isItemInstalled(listingId: string): boolean {
    return this.installedItems().some(
      i => i.listingId === listingId && i.isActive
    );
  }

  getCatalogItemById(listingId: string): MarketplaceListingItem | undefined {
    return this.listings().find(l => l.id === listingId);
  }

  openDetailModal(item: MarketplaceListingItem): void {
    this.selectedListing.set(item);

    // Parse config schema defaults or existing install settings
    const existing = this.installedItems().find(i => i.listingId === item.id && i.isActive);
    if (existing) {
      this.customConfig.set({ ...existing.customSettings });
    } else if (item.configurationSchemaJson) {
      try {
        const parsed = JSON.parse(item.configurationSchemaJson);
        this.customConfig.set(parsed);
      } catch {
        this.customConfig.set({});
      }
    } else {
      this.customConfig.set({});
    }

    this.isModalOpen.set(true);
  }

  closeModal(): void {
    this.isModalOpen.set(false);
    this.selectedListing.set(null);
    this.customConfig.set({});
  }

  updateConfigField(key: string, value: string): void {
    const current = { ...this.customConfig() };
    current[key] = value;
    this.customConfig.set(current);
  }

  confirmInstall(item: MarketplaceListingItem): void {
    this.isInstalling.set(true);

    const newInstallation: TenantInstalledItem = {
      installationId: 'inst-' + Math.random().toString(36).substring(2, 10),
      tenantId: '00000000-0000-0000-0000-000000000001',
      listingId: item.id,
      listingName: item.name,
      itemType: item.itemType,
      tier: item.tier,
      installedVersion: item.version,
      customSettings: { ...this.customConfig() },
      installedAtUtc: new Date().toISOString(),
      isActive: true
    };

    // Update listings install count
    this.listings.update(items =>
      items.map(l => (l.id === item.id ? { ...l, installCount: l.installCount + 1 } : l))
    );

    // Update installed items
    this.installedItems.update(items => [newInstallation, ...items]);

    this.isInstalling.set(false);
    this.closeModal();
    this.notificationMessage.set(`Successfully deployed "${item.name}" into workspace.`);
  }

  saveConfiguration(): void {
    const listing = this.selectedListing();
    if (!listing) return;

    this.installedItems.update(items =>
      items.map(item =>
        item.listingId === listing.id
          ? { ...item, customSettings: { ...this.customConfig() } }
          : item
      )
    );

    this.closeModal();
    this.notificationMessage.set(`Updated configuration settings for "${listing.name}".`);
  }

  uninstallItem(installationId: string): void {
    this.isUninstalling.set(true);

    const target = this.installedItems().find(i => i.installationId === installationId);
    const targetName = target ? (this.getCatalogItemById(target.listingId)?.name || 'Solution') : 'Solution';

    this.installedItems.update(items =>
      items.map(item =>
        item.installationId === installationId
          ? { ...item, isActive: false }
          : item
      )
    );

    this.isUninstalling.set(false);
    this.notificationMessage.set(`Uninstalled "${targetName}" from workspace.`);
  }

  objectKeys(obj: Record<string, string>): string[] {
    return Object.keys(obj || {});
  }

  getSettingsCount(settings: Record<string, string>): number {
    return Object.keys(settings || {}).length;
  }

  getTierLabel(tier: MarketplaceTier): string {
    switch (tier) {
      case 'Official':
        return 'Official';
      case 'VerifiedPartner':
        return 'Verified Partner';
      case 'Community':
        return 'Community';
      default:
        return tier;
    }
  }

  getTierBadgeClass(tier: MarketplaceTier): string {
    switch (tier) {
      case 'Official':
        return 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20';
      case 'VerifiedPartner':
        return 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-cyan-500/10 text-cyan-400 border border-cyan-500/20';
      case 'Community':
        return 'px-2 py-0.5 rounded-full text-[10px] font-bold bg-purple-500/10 text-purple-400 border border-purple-500/20';
      default:
        return 'px-2 py-0.5 rounded-full text-[10px] font-medium bg-slate-800 text-slate-300';
    }
  }

  getIconContainerClass(type: MarketplaceItemType): string {
    switch (type) {
      case 'AgentSwarm':
        return 'w-10 h-10 rounded-xl bg-cyan-950/80 border border-cyan-800/50 flex items-center justify-center text-cyan-300';
      case 'WorkflowTemplate':
        return 'w-10 h-10 rounded-xl bg-indigo-950/80 border border-indigo-800/50 flex items-center justify-center text-indigo-300';
      case 'PromptPack':
        return 'w-10 h-10 rounded-xl bg-amber-950/80 border border-amber-800/50 flex items-center justify-center text-amber-300';
      default:
        return 'w-10 h-10 rounded-xl bg-slate-800 border border-slate-700 flex items-center justify-center text-slate-300';
    }
  }

  getIconSymbol(icon: string): string {
    switch (icon) {
      case 'sparkles':
        return '✨';
      case 'shopping-bag':
        return '🛍️';
      case 'crosshair':
        return '🎯';
      case 'type':
        return '✍️';
      case 'search':
        return '🔍';
      case 'zap':
        return '⚡';
      default:
        return '🧩';
    }
  }
}
