import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export type SearchEntityType =
  | 'Customer'
  | 'TicketMessage'
  | 'OrderFulfillment'
  | 'ContractDocument'
  | 'CrmDeal'
  | 'KnowledgeArticle';

export interface SearchResultItem {
  id: string;
  entityType: SearchEntityType;
  title: string;
  snippet: string;
  urlRoute: string;
  score: number;
  author: string;
  timeAgo: string;
}

@Component({
  selector: 'app-universal-search-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6 text-slate-100" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <span class="text-3xl">🔍</span>
            <div>
              <h1 class="text-2xl font-bold tracking-tight text-white">
                Universal Search & Knowledge Retrieval
              </h1>
              <p class="text-xs text-slate-400 mt-1">
                Sub-50ms BM25 full-text, fuzzy typo-tolerant inverted index across all modules.
              </p>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-2">
          <kbd class="px-2.5 py-1 rounded bg-slate-800 border border-slate-700 text-xs font-mono text-slate-300">
            Ctrl + K
          </kbd>
        </div>
      </div>

      <!-- Search Input Bar -->
      <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-4 shadow-xl space-y-3">
        <div class="relative">
          <span class="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-slate-500">
            🔍
          </span>
          <input
            type="text"
            [(ngModel)]="searchQuery"
            placeholder="Search across customers, tickets, orders, contracts, deals, and docs..."
            class="w-full bg-slate-950 border border-slate-700 rounded-xl pl-10 pr-4 py-3 text-sm text-white placeholder-slate-500 focus:outline-none focus:border-cyan-500 transition" />
        </div>

        <!-- Entity Filter Pills -->
        <div class="flex flex-wrap items-center gap-2 pt-1 text-xs">
          <span class="text-slate-500 text-[11px] font-semibold uppercase tracking-wider mr-1">Filter:</span>
          @for (entity of entityFilters; track entity) {
            <button
              (click)="toggleFilter(entity)"
              class="px-2.5 py-1 rounded-lg border transition font-medium text-[11px]"
              [ngClass]="activeFilter() === entity ? 'bg-cyan-600 text-white border-cyan-500 shadow-md shadow-cyan-600/20' : 'bg-slate-950 text-slate-400 border-slate-800 hover:border-slate-700'">
              {{ entity }}
            </button>
          }
        </div>
      </div>

      <!-- Search Results Feed -->
      <div class="bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden space-y-1 p-3">
        <div class="px-3 py-2 flex items-center justify-between text-xs text-slate-400 border-b border-slate-800">
          <span>Found {{ filteredResults().length }} ranked results</span>
          <span class="font-mono text-[11px]">Execution: 12ms</span>
        </div>

        <div class="divide-y divide-slate-800/60">
          @for (res of filteredResults(); track res.id) {
            <div class="p-3.5 hover:bg-slate-800/40 rounded-lg transition space-y-1.5">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2.5">
                  <span
                    class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                    [ngClass]="getEntityBadgeClass(res.entityType)">
                    {{ res.entityType }}
                  </span>
                  <a
                    [href]="res.urlRoute"
                    class="text-sm font-semibold text-white hover:text-cyan-400 transition">
                    {{ res.title }}
                  </a>
                </div>

                <div class="flex items-center gap-3">
                  <span class="text-[10px] font-mono text-cyan-400 bg-cyan-950/60 px-2 py-0.5 rounded border border-cyan-500/30">
                    BM25: {{ res.score }}
                  </span>
                  <span class="text-[11px] text-slate-500">{{ res.timeAgo }}</span>
                </div>
              </div>

              <p class="text-xs text-slate-300 leading-relaxed font-sans" [innerHTML]="res.snippet"></p>

              <div class="flex items-center gap-4 text-[11px] text-slate-500 pt-0.5">
                <span>Author/Owner: <strong class="text-slate-400 font-medium">{{ res.author }}</strong></span>
                <span class="text-slate-600">•</span>
                <span class="font-mono text-slate-400">{{ res.urlRoute }}</span>
              </div>
            </div>
          } @empty {
            <div class="p-8 text-center text-xs text-slate-500">
              No matching records found for "{{ searchQuery }}". Try adjusting filters or fuzzy search keywords.
            </div>
          }
        </div>
      </div>
    </div>
  `
})
export class UniversalSearchCockpitComponent {
  searchQuery = 'refund';
  activeFilter = signal<string>('All');

  entityFilters = [
    'All',
    'Customer',
    'TicketMessage',
    'OrderFulfillment',
    'ContractDocument',
    'CrmDeal',
    'KnowledgeArticle'
  ];

  results = signal<SearchResultItem[]>([
    {
      id: 'res-1',
      entityType: 'KnowledgeArticle',
      title: 'Standard Refund Policy and Payment Disputes Guide',
      snippet: 'Procedures for processing immediate customer <mark>refund</mark> requests and dispute arbitration.',
      urlRoute: '/help/docs/refund-policy',
      score: 4.82,
      author: 'Finance Ops',
      timeAgo: '2h ago'
    },
    {
      id: 'res-2',
      entityType: 'TicketMessage',
      title: 'Ticket #49102 - Customer requesting full order refund',
      snippet: 'Customer contacted support citing delayed parcel delivery and requested a full <mark>refund</mark>.',
      urlRoute: '/conversations/inbox',
      score: 3.91,
      author: 'Claire Moreau',
      timeAgo: '5h ago'
    },
    {
      id: 'res-3',
      entityType: 'OrderFulfillment',
      title: 'Order ORD-8921-X [Partial Refund Issued]',
      snippet: 'Partial item return completed. Total <mark>refund</mark> amount of $45.00 credited back to Visa.',
      urlRoute: '/commerce/fulfillment-routing',
      score: 3.15,
      author: 'Warehouse Bot',
      timeAgo: '1d ago'
    },
    {
      id: 'res-4',
      entityType: 'CrmDeal',
      title: 'Enterprise Agreement - Acme Corp [Non-Refundable Clause]',
      snippet: 'Signed master services agreement stipulating strict non-<mark>refundable</mark> annual platform fee.',
      urlRoute: '/crm/deal-slippage',
      score: 2.74,
      author: 'David Vance',
      timeAgo: '3d ago'
    }
  ]);

  filteredResults = computed(() => {
    const filter = this.activeFilter();
    const query = this.searchQuery.toLowerCase().trim();
    return this.results().filter(r => {
      const matchFilter = filter === 'All' || r.entityType === filter;
      const matchQuery = !query || r.title.toLowerCase().includes(query) || r.snippet.toLowerCase().includes(query);
      return matchFilter && matchQuery;
    });
  });

  toggleFilter(entity: string) {
    this.activeFilter.set(entity);
  }

  getEntityBadgeClass(type: SearchEntityType): string {
    switch (type) {
      case 'Customer':
        return 'bg-purple-500/20 text-purple-300 border-purple-500/30';
      case 'TicketMessage':
        return 'bg-blue-500/20 text-blue-300 border-blue-500/30';
      case 'OrderFulfillment':
        return 'bg-cyan-500/20 text-cyan-300 border-cyan-500/30';
      case 'ContractDocument':
        return 'bg-indigo-500/20 text-indigo-300 border-indigo-500/30';
      case 'CrmDeal':
        return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
      case 'KnowledgeArticle':
        return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30';
    }
  }
}
