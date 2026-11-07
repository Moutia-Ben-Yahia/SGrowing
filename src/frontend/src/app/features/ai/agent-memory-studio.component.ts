import { Component, ChangeDetectionStrategy, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';

export type AgentMemoryCategory =
  | 'BrandMemory'
  | 'BusinessMemory'
  | 'CampaignMemory'
  | 'CustomerContext'
  | 'OperationalFact';

export type MemoryConfidenceLevel = 'Low' | 'Medium' | 'High' | 'Verified';

export interface AgentMemoryEntry {
  id: string;
  tenantId: string;
  category: AgentMemoryCategory;
  key: string;
  value: string;
  confidence: MemoryConfidenceLevel;
  sourceAgent: string;
  createdAtUtc: string;
  expiresAtUtc?: string | null;
  tags: string[];
  metadata: Record<string, string>;
}

export const INITIAL_MEMORIES: AgentMemoryEntry[] = [
  {
    id: 'mem_seed_brand_01',
    tenantId: '00000000-0000-0000-0000-000000000001',
    category: 'BrandMemory',
    key: 'Brand Voice Guidelines',
    value: 'Direct, culturally resonant, respectful in Arabic & English',
    confidence: 'Verified',
    sourceAgent: 'BrandToneDirectorAgent',
    createdAtUtc: new Date().toISOString(),
    expiresAtUtc: null,
    tags: ['brand', 'voice', 'tone', 'arabic', 'english'],
    metadata: { persona: 'Executive Growth Director', locales: 'ar-SA,en-US' }
  },
  {
    id: 'mem_seed_biz_02',
    tenantId: '00000000-0000-0000-0000-000000000001',
    category: 'BusinessMemory',
    key: 'Saudi Business Regulations',
    value: 'Saudi 15% VAT, 24h COD delivery in Riyadh, 3-day return policy',
    confidence: 'Verified',
    sourceAgent: 'ComplianceOfficerAgent',
    createdAtUtc: new Date().toISOString(),
    expiresAtUtc: null,
    tags: ['saudi', 'vat', 'cod', 'delivery', 'riyadh', 'return-policy'],
    metadata: { jurisdiction: 'KSA', vatRate: '0.15' }
  },
  {
    id: 'mem_seed_camp_03',
    tenantId: '00000000-0000-0000-0000-000000000001',
    category: 'CampaignMemory',
    key: 'Ramadan Campaign Guardrails',
    value: 'Ramadan discount max 25%, ROAS target 3.8x',
    confidence: 'High',
    sourceAgent: 'GrowthCampaignPlannerAgent',
    createdAtUtc: new Date().toISOString(),
    expiresAtUtc: null,
    tags: ['ramadan', 'discount', 'roas', 'campaign'],
    metadata: { maxDiscount: '0.25', targetRoas: '3.8' }
  },
  {
    id: 'mem_seed_cust_04',
    tenantId: '00000000-0000-0000-0000-000000000001',
    category: 'CustomerContext',
    key: 'VIP Customer Engagement SLA',
    value: 'VIP tier requires dedicated WhatsApp support',
    confidence: 'Verified',
    sourceAgent: 'CrmConciergeAgent',
    createdAtUtc: new Date().toISOString(),
    expiresAtUtc: null,
    tags: ['vip', 'whatsapp', 'sla', 'support'],
    metadata: { slaHours: '1', channel: 'WhatsApp' }
  },
  {
    id: 'mem_seed_ops_05',
    tenantId: '00000000-0000-0000-0000-000000000001',
    category: 'OperationalFact',
    key: 'Payment Gateway Failover Routing',
    value: 'Primary: HyperPay (Mada & Apple Pay); Auto-failover to Tap on 5xx or >400ms latency',
    confidence: 'High',
    sourceAgent: 'OpsDirectorAgent',
    createdAtUtc: new Date().toISOString(),
    expiresAtUtc: null,
    tags: ['payments', 'hyperpay', 'tap', 'failover'],
    metadata: { latencyLimit: '400ms', primaryGateway: 'HyperPay' }
  }
];

@Component({
  selector: 'app-agent-memory-studio',
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
            <span class="text-sm font-semibold text-white">Multi-Tier AI Agent Memory Studio</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 font-medium">
              PRD Section 6.5 &bull; Domain J1
            </span>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <!-- Action Buttons -->
          <button
            type="button"
            (click)="openPromptPreview()"
            class="px-3.5 py-1.5 rounded-lg border border-purple-500/30 bg-purple-500/10 hover:bg-purple-500/20 text-purple-300 text-xs font-medium flex items-center gap-1.5 transition shadow-sm"
          >
            <svg class="w-4 h-4 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
            Prompt Injection Preview
          </button>

          <button
            type="button"
            (click)="pruneExpired()"
            class="px-3.5 py-1.5 rounded-lg border border-slate-700 bg-slate-800/80 hover:bg-slate-700 text-slate-300 text-xs font-medium flex items-center gap-1.5 transition"
            title="Prune expired facts with elapsed TTL"
          >
            <svg class="w-4 h-4 text-slate-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
            Prune Expired
          </button>

          <button
            type="button"
            (click)="openStoreModal()"
            class="px-4 py-1.5 rounded-lg bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-400 hover:to-teal-400 text-slate-950 font-bold text-xs flex items-center gap-1.5 shadow-lg shadow-emerald-500/20 transition"
          >
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Store New Agent Fact
          </button>
        </div>
      </header>

      <!-- Subheader / Metrics Ribbon -->
      <section class="border-b border-slate-800/60 bg-slate-900/40 px-6 py-3 flex flex-wrap items-center justify-between gap-4">
        <div class="flex items-center gap-6 text-xs text-slate-400">
          <div class="flex items-center gap-2">
            <span class="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            <span>Memory Engine: <strong class="text-white">Active (Thread-Safe RLS)</strong></span>
          </div>
          <div class="hidden sm:flex items-center gap-1">
            <span>Total Facts:</span>
            <strong class="text-cyan-400 font-semibold">{{ totalFactCount() }}</strong>
          </div>
          <div class="hidden md:flex items-center gap-1">
            <span>Verified Confidence:</span>
            <strong class="text-emerald-400 font-semibold">{{ verifiedCount() }}</strong>
          </div>
          <div class="hidden lg:flex items-center gap-1">
            <span>Tenant Isolation:</span>
            <strong class="text-purple-400 font-semibold">Strict Multi-Tenant</strong>
          </div>
        </div>

        <!-- Notification Banner if action performed -->
        @if (notificationMessage()) {
          <div class="text-xs px-3 py-1 rounded bg-teal-500/20 text-teal-300 border border-teal-500/30 flex items-center gap-2 animate-fade-in">
            <span>{{ notificationMessage() }}</span>
            <button (click)="notificationMessage.set(null)" class="text-teal-400 hover:text-white">&times;</button>
          </div>
        }
      </section>

      <!-- Main Body Container -->
      <main class="flex-1 max-w-7xl w-full mx-auto px-6 py-6 flex flex-col gap-6">
        <!-- Controls & Filter Toolbar -->
        <div class="flex flex-col md:flex-row items-stretch md:items-center justify-between gap-4">
          <!-- Category Tabs -->
          <div class="flex flex-wrap items-center gap-1.5 p-1 bg-slate-900/90 border border-slate-800 rounded-xl">
            <button
              type="button"
              (click)="selectedCategory.set('All')"
              [class]="selectedCategory() === 'All' ? 'bg-cyan-500/20 text-cyan-300 border border-cyan-500/40' : 'text-slate-400 hover:text-slate-200 border border-transparent'"
              class="px-3 py-1.5 rounded-lg text-xs font-medium transition flex items-center gap-1.5"
            >
              All Facts
              <span class="px-1.5 py-0.2 rounded-full text-[10px] bg-slate-800 text-slate-300">{{ totalFactCount() }}</span>
            </button>

            <button
              type="button"
              (click)="selectedCategory.set('BrandMemory')"
              [class]="selectedCategory() === 'BrandMemory' ? 'bg-purple-500/20 text-purple-300 border border-purple-500/40' : 'text-slate-400 hover:text-slate-200 border border-transparent'"
              class="px-3 py-1.5 rounded-lg text-xs font-medium transition flex items-center gap-1.5"
            >
              Brand Voice
              <span class="px-1.5 py-0.2 rounded-full text-[10px] bg-slate-800 text-slate-300">{{ brandCount() }}</span>
            </button>

            <button
              type="button"
              (click)="selectedCategory.set('BusinessMemory')"
              [class]="selectedCategory() === 'BusinessMemory' ? 'bg-blue-500/20 text-blue-300 border border-blue-500/40' : 'text-slate-400 hover:text-slate-200 border border-transparent'"
              class="px-3 py-1.5 rounded-lg text-xs font-medium transition flex items-center gap-1.5"
            >
              Business Rules
              <span class="px-1.5 py-0.2 rounded-full text-[10px] bg-slate-800 text-slate-300">{{ businessCount() }}</span>
            </button>

            <button
              type="button"
              (click)="selectedCategory.set('CampaignMemory')"
              [class]="selectedCategory() === 'CampaignMemory' ? 'bg-amber-500/20 text-amber-300 border border-amber-500/40' : 'text-slate-400 hover:text-slate-200 border border-transparent'"
              class="px-3 py-1.5 rounded-lg text-xs font-medium transition flex items-center gap-1.5"
            >
              Campaign Memory
              <span class="px-1.5 py-0.2 rounded-full text-[10px] bg-slate-800 text-slate-300">{{ campaignCount() }}</span>
            </button>

            <button
              type="button"
              (click)="selectedCategory.set('CustomerContext')"
              [class]="selectedCategory() === 'CustomerContext' ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/40' : 'text-slate-400 hover:text-slate-200 border border-transparent'"
              class="px-3 py-1.5 rounded-lg text-xs font-medium transition flex items-center gap-1.5"
            >
              Customer Context
              <span class="px-1.5 py-0.2 rounded-full text-[10px] bg-slate-800 text-slate-300">{{ customerCount() }}</span>
            </button>

            <button
              type="button"
              (click)="selectedCategory.set('OperationalFact')"
              [class]="selectedCategory() === 'OperationalFact' ? 'bg-rose-500/20 text-rose-300 border border-rose-500/40' : 'text-slate-400 hover:text-slate-200 border border-transparent'"
              class="px-3 py-1.5 rounded-lg text-xs font-medium transition flex items-center gap-1.5"
            >
              Operational Facts
              <span class="px-1.5 py-0.2 rounded-full text-[10px] bg-slate-800 text-slate-300">{{ operationalCount() }}</span>
            </button>
          </div>

          <!-- Search & Confidence Filters -->
          <div class="flex items-center gap-3">
            <div class="relative flex-1 md:w-72">
              <span class="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-500">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
              </span>
              <input
                type="text"
                [ngModel]="searchQuery()"
                (ngModelChange)="searchQuery.set($event)"
                placeholder="Search facts, rules, tags..."
                class="w-full pl-9 pr-7 py-2 bg-slate-900 border border-slate-800 rounded-lg text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500"
              />
              @if (searchQuery()) {
                <button
                  type="button"
                  (click)="searchQuery.set('')"
                  class="absolute inset-y-0 right-0 pr-2.5 flex items-center text-slate-400 hover:text-white"
                >
                  &times;
                </button>
              }
            </div>

            <!-- Confidence Selector -->
            <select
              [ngModel]="selectedConfidence()"
              (ngModelChange)="selectedConfidence.set($event)"
              class="bg-slate-900 border border-slate-800 text-slate-300 text-xs rounded-lg px-2.5 py-2 focus:outline-none focus:border-cyan-500"
            >
              <option value="All">All Confidences</option>
              <option value="Verified">Verified Only</option>
              <option value="High">High & Verified</option>
              <option value="Medium">Medium & Above</option>
              <option value="Low">Low</option>
            </select>
          </div>
        </div>

        <!-- Memory Fact Cards Grid -->
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          @for (item of filteredMemories(); track item.id) {
            <div class="group relative rounded-xl border border-slate-800/80 bg-slate-900/60 hover:bg-slate-900 hover:border-slate-700/80 p-5 flex flex-col justify-between transition duration-200 shadow-md hover:shadow-cyan-900/10">
              <!-- Top Row: Category Pill & Confidence Badge -->
              <div>
                <div class="flex items-center justify-between gap-2 mb-3">
                  <span
                    class="text-[10px] font-semibold uppercase tracking-wider px-2 py-0.5 rounded-full border"
                    [class]="getCategoryPillStyle(item.category)"
                  >
                    {{ formatCategoryLabel(item.category) }}
                  </span>

                  <span
                    class="text-[10px] font-medium px-2 py-0.5 rounded-full border flex items-center gap-1"
                    [class]="getConfidenceBadgeStyle(item.confidence)"
                  >
                    @if (item.confidence === 'Verified') {
                      <svg class="w-3 h-3 text-emerald-400" fill="currentColor" viewBox="0 0 20 20">
                        <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clip-rule="evenodd" />
                      </svg>
                    }
                    {{ item.confidence }}
                  </span>
                </div>

                <!-- Fact Title / Key -->
                <h3 class="text-sm font-bold text-white mb-2 group-hover:text-cyan-300 transition line-clamp-1">
                  {{ item.key }}
                </h3>

                <!-- Fact Value / Rule Constraint -->
                <div class="rounded-lg bg-slate-950/60 border border-slate-800/70 p-3 mb-3 text-xs text-slate-300 leading-relaxed font-mono">
                  {{ item.value }}
                </div>

                <!-- Tags -->
                @if (item.tags.length > 0) {
                  <div class="flex flex-wrap gap-1.5 mb-4">
                    @for (tag of item.tags; track tag) {
                      <span class="text-[10px] px-2 py-0.5 rounded bg-slate-800/90 text-slate-400 border border-slate-700/50">
                        #{{ tag }}
                      </span>
                    }
                  </div>
                }
              </div>

              <!-- Footer: Source Agent, TTL Status, Delete Action -->
              <div class="border-t border-slate-800/80 pt-3 flex items-center justify-between text-[11px] text-slate-400">
                <div class="flex flex-col gap-0.5">
                  <span class="text-slate-300 font-medium flex items-center gap-1">
                    <svg class="w-3 h-3 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                    </svg>
                    {{ item.sourceAgent }}
                  </span>
                  <span class="text-[10px] text-slate-500">
                    {{ getTtlStatus(item) }}
                  </span>
                </div>

                <button
                  type="button"
                  (click)="deleteFact(item.id)"
                  class="p-1.5 rounded-lg text-slate-500 hover:text-rose-400 hover:bg-rose-500/10 transition"
                  title="Delete memory fact"
                >
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                  </svg>
                </button>
              </div>
            </div>
          } @empty {
            <div class="col-span-full py-16 text-center rounded-2xl border border-dashed border-slate-800 bg-slate-900/30">
              <svg class="w-10 h-10 text-slate-600 mx-auto mb-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
              </svg>
              <h4 class="text-sm font-semibold text-slate-300">No agent memories found</h4>
              <p class="text-xs text-slate-500 mt-1 max-w-sm mx-auto">
                No facts match your selected category or search filter. Try clearing filters or store a new agent memory fact.
              </p>
              <button
                type="button"
                (click)="openStoreModal()"
                class="mt-4 px-3.5 py-1.5 rounded-lg bg-cyan-600 hover:bg-cyan-500 text-white font-medium text-xs transition inline-flex items-center gap-1.5"
              >
                + Store First Memory Fact
              </button>
            </div>
          }
        </div>
      </main>

      <!-- MODAL: Store New Agent Fact -->
      @if (showStoreModal()) {
        <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-fade-in">
          <div class="bg-slate-900 border border-slate-700/80 rounded-2xl max-w-lg w-full p-6 shadow-2xl flex flex-col gap-5 text-slate-200">
            <!-- Modal Header -->
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <span class="w-2.5 h-2.5 rounded-full bg-emerald-400"></span>
                <h3 class="text-sm font-bold text-white">Store New Agent Memory Fact</h3>
              </div>
              <button (click)="closeStoreModal()" class="text-slate-400 hover:text-white text-lg">&times;</button>
            </div>

            <!-- Form Content -->
            <div class="flex flex-col gap-4 text-xs">
              <!-- Category Dropdown -->
              <div>
                <label class="block text-slate-400 mb-1 font-medium">Memory Tier & Category *</label>
                <select
                  [(ngModel)]="newCategory"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-cyan-500"
                >
                  <option value="BrandMemory">Brand Voice & Guidelines</option>
                  <option value="BusinessMemory">Business Rules & Compliance</option>
                  <option value="CampaignMemory">Campaign & Growth Playbook</option>
                  <option value="CustomerContext">Customer Context & Tier SLA</option>
                  <option value="OperationalFact">Operational Fact & System Guardrail</option>
                </select>
              </div>

              <!-- Key Input -->
              <div>
                <label class="block text-slate-400 mb-1 font-medium">Fact Key / Title *</label>
                <input
                  type="text"
                  [(ngModel)]="newKey"
                  placeholder="e.g. Saudi VAT & Customs SLA"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-cyan-500"
                />
              </div>

              <!-- Value Textarea -->
              <div>
                <label class="block text-slate-400 mb-1 font-medium">Fact Content / Policy Constraint *</label>
                <textarea
                  [(ngModel)]="newValue"
                  rows="3"
                  placeholder="e.g. All wholesale orders require 15% VAT and SASO certificate prior to customs discharge."
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200 font-mono focus:outline-none focus:border-cyan-500"
                ></textarea>
              </div>

              <!-- Two Column: Confidence & Source Agent -->
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-slate-400 mb-1 font-medium">Confidence Level</label>
                  <select
                    [(ngModel)]="newConfidence"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-cyan-500"
                  >
                    <option value="Verified">Verified (Mandatory)</option>
                    <option value="High">High Confidence</option>
                    <option value="Medium">Medium Confidence</option>
                    <option value="Low">Low / Unverified</option>
                  </select>
                </div>

                <div>
                  <label class="block text-slate-400 mb-1 font-medium">Source Agent</label>
                  <input
                    type="text"
                    [(ngModel)]="newSourceAgent"
                    placeholder="e.g. ComplianceOfficerAgent"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-cyan-500"
                  />
                </div>
              </div>

              <!-- Two Column: TTL & Tags -->
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-slate-400 mb-1 font-medium">TTL in Hours (Blank = Permanent)</label>
                  <input
                    type="number"
                    [(ngModel)]="newTtlHours"
                    placeholder="e.g. 72"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-cyan-500"
                  />
                </div>

                <div>
                  <label class="block text-slate-400 mb-1 font-medium">Tags (Comma-separated)</label>
                  <input
                    type="text"
                    [(ngModel)]="newTags"
                    placeholder="e.g. saudi, vat, customs"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-2 text-slate-200 focus:outline-none focus:border-cyan-500"
                  />
                </div>
              </div>
            </div>

            <!-- Modal Actions -->
            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button
                type="button"
                (click)="closeStoreModal()"
                class="px-4 py-2 rounded-lg text-slate-400 hover:text-white text-xs font-medium transition"
              >
                Cancel
              </button>
              <button
                type="button"
                (click)="storeFact()"
                [disabled]="!newKey.trim() || !newValue.trim()"
                class="px-5 py-2 rounded-lg bg-gradient-to-r from-emerald-500 to-teal-500 disabled:opacity-50 text-slate-950 font-bold text-xs transition shadow-md"
              >
                Save Agent Memory Fact
              </button>
            </div>
          </div>
        </div>
      }

      <!-- DRAWER: Agent Prompt Context Preview -->
      @if (showPromptDrawer()) {
        <div class="fixed inset-0 z-50 flex justify-end bg-slate-950/70 backdrop-blur-sm animate-fade-in">
          <div class="bg-slate-900 border-l border-slate-800 w-full max-w-2xl h-full p-6 shadow-2xl flex flex-col justify-between text-slate-200 overflow-y-auto">
            <div>
              <!-- Drawer Header -->
              <div class="flex items-center justify-between pb-4 border-b border-slate-800 mb-4">
                <div class="flex items-center gap-2">
                  <div class="p-2 rounded-lg bg-purple-500/10 border border-purple-500/20 text-purple-400">
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                    </svg>
                  </div>
                  <div>
                    <h3 class="text-sm font-bold text-white">Agent Prompt Context Injection</h3>
                    <p class="text-[11px] text-slate-400">Exact system prompt memory block injected into autonomous LLMs</p>
                  </div>
                </div>
                <button (click)="closePromptPreview()" class="text-slate-400 hover:text-white text-xl">&times;</button>
              </div>

              <!-- Topic Query Simulator -->
              <div class="mb-4">
                <label class="block text-slate-400 text-xs mb-1 font-medium">Topic Relevance Simulator (Optional Query)</label>
                <div class="relative">
                  <input
                    type="text"
                    [ngModel]="previewTopicQuery()"
                    (ngModelChange)="previewTopicQuery.set($event)"
                    placeholder="e.g. Ramadan, VAT, WhatsApp"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-slate-200 placeholder-slate-600 focus:outline-none focus:border-purple-500"
                  />
                  @if (previewTopicQuery()) {
                    <button
                      type="button"
                      (click)="previewTopicQuery.set('')"
                      class="absolute inset-y-0 right-0 pr-2.5 flex items-center text-slate-400 hover:text-white text-xs"
                    >
                      &times;
                    </button>
                  }
                </div>
              </div>

              <!-- Prompt Injection Box -->
              <div class="relative">
                <div class="flex items-center justify-between bg-slate-950 px-4 py-2 border-t border-x border-slate-800 rounded-t-xl text-[11px] text-slate-400">
                  <span class="font-mono text-purple-400">system_prompt_memory_injection.md</span>
                  <span>{{ promptPreviewContext().length }} chars</span>
                </div>
                <pre class="bg-slate-950 border border-slate-800 rounded-b-xl p-4 font-mono text-xs text-emerald-300 leading-relaxed overflow-x-auto whitespace-pre-wrap select-all max-h-[500px]">{{ promptPreviewContext() }}</pre>
              </div>
            </div>

            <!-- Drawer Footer -->
            <div class="pt-4 border-t border-slate-800 flex items-center justify-between gap-3 mt-4">
              <span class="text-xs text-slate-500">Injects at top of LLM context window</span>
              <div class="flex items-center gap-3">
                <button
                  type="button"
                  (click)="copyPromptContext()"
                  class="px-4 py-2 rounded-lg bg-purple-600 hover:bg-purple-500 text-white font-medium text-xs transition flex items-center gap-1.5 shadow"
                >
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 5H6a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2v-1M8 5a2 2 0 002 2h2a2 2 0 002-2M8 5a2 2 0 012-2h2a2 2 0 012 2m0 0h2a2 2 0 012 2v3m2 4H10m0 0l3-3m-3 3l3 3" />
                  </svg>
                  {{ copyButtonText() }}
                </button>
                <button
                  type="button"
                  (click)="closePromptPreview()"
                  class="px-4 py-2 rounded-lg border border-slate-700 bg-slate-800 hover:bg-slate-700 text-slate-300 font-medium text-xs transition"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AgentMemoryStudioComponent {
  private readonly translationService = inject(TranslationService, { optional: true });

  // State Signals
  readonly memories = signal<AgentMemoryEntry[]>(INITIAL_MEMORIES);
  readonly selectedCategory = signal<string>('All');
  readonly selectedConfidence = signal<string>('All');
  readonly searchQuery = signal<string>('');
  readonly notificationMessage = signal<string | null>(null);

  // Modal State
  readonly showStoreModal = signal<boolean>(false);
  newCategory: AgentMemoryCategory = 'BrandMemory';
  newKey = '';
  newValue = '';
  newConfidence: MemoryConfidenceLevel = 'Verified';
  newSourceAgent = 'BrandToneDirectorAgent';
  newTtlHours: number | null = null;
  newTags = '';

  // Drawer Preview State
  readonly showPromptDrawer = signal<boolean>(false);
  readonly previewTopicQuery = signal<string>('');
  readonly copyButtonText = signal<string>('Copy Context');

  // Computed Metrics
  readonly totalFactCount = computed(() => this.memories().length);
  readonly verifiedCount = computed(() => this.memories().filter(m => m.confidence === 'Verified').length);
  readonly brandCount = computed(() => this.memories().filter(m => m.category === 'BrandMemory').length);
  readonly businessCount = computed(() => this.memories().filter(m => m.category === 'BusinessMemory').length);
  readonly campaignCount = computed(() => this.memories().filter(m => m.category === 'CampaignMemory').length);
  readonly customerCount = computed(() => this.memories().filter(m => m.category === 'CustomerContext').length);
  readonly operationalCount = computed(() => this.memories().filter(m => m.category === 'OperationalFact').length);

  // Filtered Memories
  readonly filteredMemories = computed(() => {
    let list = this.memories();

    // Category Filter
    const cat = this.selectedCategory();
    if (cat !== 'All') {
      list = list.filter(m => m.category === cat);
    }

    // Confidence Filter
    const conf = this.selectedConfidence();
    if (conf === 'Verified') {
      list = list.filter(m => m.confidence === 'Verified');
    } else if (conf === 'High') {
      list = list.filter(m => m.confidence === 'Verified' || m.confidence === 'High');
    } else if (conf === 'Medium') {
      list = list.filter(m => m.confidence === 'Verified' || m.confidence === 'High' || m.confidence === 'Medium');
    } else if (conf === 'Low') {
      list = list.filter(m => m.confidence === 'Low');
    }

    // Keyword Search
    const q = this.searchQuery().trim().toLowerCase();
    if (q) {
      list = list.filter(m =>
        m.key.toLowerCase().includes(q) ||
        m.value.toLowerCase().includes(q) ||
        m.sourceAgent.toLowerCase().includes(q) ||
        m.tags.some(t => t.toLowerCase().includes(q))
      );
    }

    return list;
  });

  // Prompt Preview Context Computed
  readonly promptPreviewContext = computed(() => {
    const query = this.previewTopicQuery().trim().toLowerCase();
    let relevantMemories = this.memories();

    if (query) {
      relevantMemories = relevantMemories.filter(m =>
        m.key.toLowerCase().includes(query) ||
        m.value.toLowerCase().includes(query) ||
        m.tags.some(t => t.toLowerCase().includes(query)) ||
        m.category.toLowerCase().includes(query)
      );
    }

    if (relevantMemories.length === 0) {
      return '[AI AGENT MEMORY CONTEXT: None available for specified query]';
    }

    const lines: string[] = [];
    lines.push('### SYSTEM AGENT MEMORY CONTEXT ###');
    lines.push('The following verified multi-tier organizational memories and constraints MUST guide your responses:');
    lines.push('');

    const categories: AgentMemoryCategory[] = [
      'BrandMemory',
      'BusinessMemory',
      'CampaignMemory',
      'CustomerContext',
      'OperationalFact'
    ];

    for (const cat of categories) {
      const items = relevantMemories.filter(m => m.category === cat);
      if (items.length > 0) {
        lines.push(`[${this.formatCategoryHeader(cat)}]`);
        for (const item of items) {
          lines.push(`- [${item.key}] (Confidence: ${item.confidence} | Source: ${item.sourceAgent})`);
          lines.push(`  ${item.value}`);
          if (item.tags.length > 0) {
            lines.push(`  Tags: #${item.tags.join(' #')}`);
          }
        }
        lines.push('');
      }
    }

    return lines.join('\n').trimEnd();
  });

  t(key: string): string {
    return this.translationService ? this.translationService.t(key) : key;
  }

  // Modal Handlers
  openStoreModal(): void {
    this.newKey = '';
    this.newValue = '';
    this.newCategory = 'BrandMemory';
    this.newConfidence = 'Verified';
    this.newSourceAgent = 'BrandToneDirectorAgent';
    this.newTtlHours = null;
    this.newTags = '';
    this.showStoreModal.set(true);
  }

  closeStoreModal(): void {
    this.showStoreModal.set(false);
  }

  storeFact(): void {
    if (!this.newKey.trim() || !this.newValue.trim()) return;

    const tagsList = this.newTags
      .split(',')
      .map(t => t.trim().toLowerCase())
      .filter(t => t.length > 0);

    const expiresAt = this.newTtlHours && this.newTtlHours > 0
      ? new Date(Date.now() + this.newTtlHours * 3600000).toISOString()
      : null;

    const newEntry: AgentMemoryEntry = {
      id: `mem_${Date.now()}_${Math.random().toString(36).substring(2, 7)}`,
      tenantId: '00000000-0000-0000-0000-000000000001',
      category: this.newCategory,
      key: this.newKey.trim(),
      value: this.newValue.trim(),
      confidence: this.newConfidence,
      sourceAgent: this.newSourceAgent.trim() || 'AdminStudioAgent',
      createdAtUtc: new Date().toISOString(),
      expiresAtUtc: expiresAt,
      tags: tagsList,
      metadata: { createdVia: 'AgentMemoryStudio' }
    };

    this.memories.update(items => [newEntry, ...items]);
    this.notificationMessage.set(`Stored fact '${newEntry.key}' successfully.`);
    this.closeStoreModal();
  }

  deleteFact(id: string): void {
    const item = this.memories().find(m => m.id === id);
    if (!item) return;

    this.memories.update(items => items.filter(m => m.id !== id));
    this.notificationMessage.set(`Deleted fact '${item.key}'.`);
  }

  pruneExpired(): void {
    const now = new Date().toISOString();
    const beforeCount = this.memories().length;
    this.memories.update(items =>
      items.filter(m => !m.expiresAtUtc || m.expiresAtUtc > now)
    );
    const pruned = beforeCount - this.memories().length;
    this.notificationMessage.set(
      pruned > 0
        ? `Pruned ${pruned} expired fact(s).`
        : 'No expired facts found; all memories active.'
    );
  }

  // Drawer Handlers
  openPromptPreview(): void {
    this.previewTopicQuery.set('');
    this.showPromptDrawer.set(true);
  }

  closePromptPreview(): void {
    this.showPromptDrawer.set(false);
  }

  copyPromptContext(): void {
    const text = this.promptPreviewContext();
    if (typeof navigator !== 'undefined' && navigator.clipboard) {
      navigator.clipboard.writeText(text);
    }
    this.copyButtonText.set('Copied!');
    setTimeout(() => this.copyButtonText.set('Copy Context'), 2000);
  }

  // Styling Helpers
  getCategoryPillStyle(category: AgentMemoryCategory): string {
    switch (category) {
      case 'BrandMemory':
        return 'bg-purple-500/10 text-purple-400 border-purple-500/20';
      case 'BusinessMemory':
        return 'bg-blue-500/10 text-blue-400 border-blue-500/20';
      case 'CampaignMemory':
        return 'bg-amber-500/10 text-amber-400 border-amber-500/20';
      case 'CustomerContext':
        return 'bg-emerald-500/10 text-emerald-400 border-emerald-500/20';
      case 'OperationalFact':
        return 'bg-rose-500/10 text-rose-400 border-rose-500/20';
      default:
        return 'bg-slate-500/10 text-slate-400 border-slate-500/20';
    }
  }

  getConfidenceBadgeStyle(confidence: MemoryConfidenceLevel): string {
    switch (confidence) {
      case 'Verified':
        return 'bg-emerald-500/10 text-emerald-300 border-emerald-500/30';
      case 'High':
        return 'bg-cyan-500/10 text-cyan-300 border-cyan-500/30';
      case 'Medium':
        return 'bg-yellow-500/10 text-yellow-300 border-yellow-500/30';
      case 'Low':
        return 'bg-slate-500/10 text-slate-400 border-slate-500/30';
      default:
        return 'bg-slate-500/10 text-slate-400 border-slate-500/30';
    }
  }

  formatCategoryLabel(category: AgentMemoryCategory): string {
    switch (category) {
      case 'BrandMemory':
        return 'Brand Voice';
      case 'BusinessMemory':
        return 'Business Rules';
      case 'CampaignMemory':
        return 'Campaign Memory';
      case 'CustomerContext':
        return 'Customer Context';
      case 'OperationalFact':
        return 'Operational Fact';
      default:
        return category;
    }
  }

  private formatCategoryHeader(category: AgentMemoryCategory): string {
    switch (category) {
      case 'BrandMemory':
        return 'Brand Voice & Tone Memory';
      case 'BusinessMemory':
        return 'Business Rules & Operational Policy Memory';
      case 'CampaignMemory':
        return 'Campaign & Growth Playbook Memory';
      case 'CustomerContext':
        return 'Customer Context & Tier SLA Memory';
      case 'OperationalFact':
        return 'Operational Facts & System Grounding Memory';
      default:
        return `${category} Memory`;
    }
  }

  getTtlStatus(item: AgentMemoryEntry): string {
    if (!item.expiresAtUtc) {
      return 'Permanent Memory';
    }
    const diffMs = new Date(item.expiresAtUtc).getTime() - Date.now();
    if (diffMs <= 0) {
      return 'Expired';
    }
    const diffHours = Math.round(diffMs / 3600000);
    return `Expires in ~${diffHours}h`;
  }
}
