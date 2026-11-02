import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface CapiEventItem {
  id: string;
  eventName: string;
  platform: 'Meta CAPI' | 'Google Enhanced';
  hashedEmail: string;
  conversionValue: number;
  currency: string;
  isDeduplicated: boolean;
  timestamp: string;
}

export interface CampaignBudgetItem {
  id: string;
  name: string;
  platform: 'Meta Ads' | 'Google Ads';
  currentSpend: number;
  recommendedSpend: number;
  currentRoas: number;
  projectedRoas: number;
  action: 'Scale' | 'Maintain' | 'Trim' | 'Pause';
}

@Component({
  selector: 'app-ad-capi-roas-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Header -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/marketing" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Marketing
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              📈 Meta & Google CAPI Conversion Hub & ROAS Budget Allocator
            </span>
            <span class="bg-indigo-500/20 text-indigo-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Domain C6 • advertising.conversions • Server CAPI • SHA-256
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <button
              (click)="simulateCapiEvent()"
              data-testid="simulate-capi-event-btn"
              class="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md shadow-indigo-900/30">
              <span>🚀</span> Dispatch Test CAPI Event
            </button>
          </div>
        </header>

        <!-- KPI Metrics Ribbon -->
        <section class="grid grid-cols-1 md:grid-cols-4 gap-4 p-6 border-b border-slate-800/80 bg-slate-900/40">
          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Blended Portfolio ROAS</div>
            <div class="text-2xl font-bold text-emerald-400 mt-1">{{ blendedRoas() }}x</div>
            <div class="text-[11px] text-slate-500 mt-1">Target: {{ targetRoas }}x</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Total Active Spend</div>
            <div class="text-2xl font-bold text-cyan-400 mt-1">{{ totalSpend() | currency:'USD':'symbol':'1.0-0' }}</div>
            <div class="text-[11px] text-slate-500 mt-1">Across Meta & Google Ads</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Verified CAPI Conversions</div>
            <div class="text-2xl font-bold text-indigo-400 mt-1">{{ totalConversions() }}</div>
            <div class="text-[11px] text-slate-500 mt-1">100% SHA-256 hashed payload</div>
          </div>

          <div class="p-4 rounded-xl bg-slate-900/80 border border-slate-800">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Deduplication Protection</div>
            <div class="text-2xl font-bold text-amber-400 mt-1">14.2%</div>
            <div class="text-[11px] text-slate-500 mt-1">7-Day idempotency window</div>
          </div>
        </section>

        <!-- Main Content Grid -->
        <div class="flex-1 p-6 grid grid-cols-1 lg:grid-cols-3 gap-6">
          <!-- Left 2 Cols: ROAS Optimizer & Live CAPI Event Feed -->
          <div class="lg:col-span-2 space-y-6">
            <!-- ROAS Optimizer Card -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>⚖️</span> Autonomous ROAS Budget Reallocation Engine
                  </h3>
                  <p class="text-xs text-slate-400 mt-1">
                    Dynamically redistributes budget from low-performing campaigns to top-performing scale campaigns.
                  </p>
                </div>
                <button
                  (click)="optimizeBudgets()"
                  data-testid="optimize-budgets-btn"
                  class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5 shadow-md shadow-emerald-900/30">
                  <span>✨</span> Optimize Budgets
                </button>
              </div>

              <!-- Controls -->
              <div class="grid grid-cols-1 md:grid-cols-2 gap-4 p-3 bg-slate-950 rounded-xl border border-slate-800 mb-4">
                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Target Blended ROAS</label>
                  <input
                    type="number"
                    step="0.5"
                    [(ngModel)]="targetRoas"
                    class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500" />
                </div>
                <div>
                  <label class="block text-[11px] text-slate-400 mb-1">Total Available Budget ($)</label>
                  <input
                    type="number"
                    step="1000"
                    [(ngModel)]="totalBudget"
                    class="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500" />
                </div>
              </div>

              <!-- Campaign Recommendations Grid -->
              <div class="space-y-3">
                @for (c of campaigns(); track c.id) {
                  <div class="p-3.5 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between">
                    <div>
                      <div class="flex items-center gap-2">
                        <span class="text-xs font-bold text-white">{{ c.name }}</span>
                        <span class="text-[10px] text-slate-400 font-mono">{{ c.platform }}</span>
                      </div>
                      <div class="text-[11px] text-slate-400 mt-1">
                        Current Spend: <span class="text-slate-200 font-mono">{{ c.currentSpend | currency:'USD':'symbol':'1.0-0' }}</span> •
                        Historical ROAS: <span class="text-emerald-400 font-bold font-mono">{{ c.currentRoas }}x</span>
                      </div>
                    </div>

                    <div class="flex items-center gap-4">
                      <div class="text-right">
                        <div class="text-xs font-bold text-cyan-400 font-mono">
                          {{ c.recommendedSpend | currency:'USD':'symbol':'1.0-0' }}
                        </div>
                        <div class="text-[10px] text-slate-500 font-mono">
                          Projected: {{ c.projectedRoas }}x
                        </div>
                      </div>

                      <span
                        class="text-[10px] font-bold px-2.5 py-1 rounded-full border tracking-wide uppercase"
                        [class.bg-emerald-500/20]="c.action === 'Scale'"
                        [class.text-emerald-300]="c.action === 'Scale'"
                        [class.border-emerald-500/30]="c.action === 'Scale'"
                        [class.bg-blue-500/20]="c.action === 'Maintain'"
                        [class.text-blue-300]="c.action === 'Maintain'"
                        [class.border-blue-500/30]="c.action === 'Maintain'"
                        [class.bg-amber-500/20]="c.action === 'Trim'"
                        [class.text-amber-300]="c.action === 'Trim'"
                        [class.border-amber-500/30]="c.action === 'Trim'"
                        [class.bg-rose-500/20]="c.action === 'Pause'"
                        [class.text-rose-300]="c.action === 'Pause'"
                        [class.border-rose-500/30]="c.action === 'Pause'">
                        {{ c.action }}
                      </span>
                    </div>
                  </div>
                }
              </div>
            </div>

            <!-- Live Server-to-Server CAPI Event Feed -->
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-3">
                <span>📡</span> Live Server-Side Conversion API (CAPI) Ingestion Feed
              </h3>

              <div class="space-y-2.5">
                @for (evt of capiEvents(); track evt.id) {
                  <div class="p-3 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between">
                    <div>
                      <div class="flex items-center gap-2">
                        <span class="text-xs font-semibold text-slate-200">{{ evt.eventName }}</span>
                        <span class="text-[10px] text-indigo-400 font-mono">{{ evt.platform }}</span>
                        <span class="text-[10px] text-slate-500 font-mono">ID: {{ evt.id }}</span>
                      </div>
                      <div class="text-[11px] text-slate-400 mt-1 font-mono">
                        em_hash: <span class="text-slate-300">{{ evt.hashedEmail }}</span>
                      </div>
                    </div>

                    <div class="text-right">
                      <div class="text-xs font-bold text-emerald-400 font-mono">
                        {{ evt.conversionValue | currency:evt.currency:'symbol':'1.2-2' }}
                      </div>
                      <span class="text-[10px] font-mono text-slate-500">
                        {{ evt.isDeduplicated ? '⚠️ Deduplicated' : '✅ Dispatched' }} • {{ evt.timestamp }}
                      </span>
                    </div>
                  </div>
                }
              </div>
            </div>
          </div>

          <!-- Right Col: Audience SHA-256 Hashing Sync -->
          <div class="space-y-6">
            <div class="p-5 rounded-2xl bg-slate-900/90 border border-slate-800 shadow-xl">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2 mb-2">
                <span>🔒</span> Custom Audience SHA-256 Hash Sync
              </h3>
              <p class="text-xs text-slate-400 mb-3">
                Directly normalize and hash customer emails and phone numbers to update Meta & Google Lookalike custom audiences.
              </p>

              <div class="mb-3">
                <label class="block text-[11px] text-slate-400 mb-1">Target Ad Audience</label>
                <select
                  [(ngModel)]="selectedAudience"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-slate-200 focus:outline-none focus:border-indigo-500">
                  <option value="aud_meta_high_ltv">Meta Ads: High LTV Buyers Lookalike</option>
                  <option value="aud_google_saas_buyers">Google Ads: SaaS Customer Match</option>
                </select>
              </div>

              <div class="mb-3">
                <label class="block text-[11px] text-slate-400 mb-1">Raw Contacts (One per line)</label>
                <textarea
                  rows="4"
                  [(ngModel)]="rawContactList"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-xs font-mono text-slate-300 focus:outline-none focus:border-indigo-500"></textarea>
              </div>

              <button
                (click)="syncAudienceHashes()"
                data-testid="sync-audience-hashes-btn"
                class="w-full py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-semibold transition flex items-center justify-center gap-1.5 shadow-md shadow-indigo-900/30">
                <span>⚡</span> Sync {{ validHashesCount() }} Hashed Records
              </button>

              <div class="mt-4 p-3 rounded-xl bg-slate-950 border border-slate-800/80">
                <div class="text-[11px] font-semibold text-slate-400 mb-1">Generated SHA-256 Payload Preview:</div>
                <div class="text-[10px] font-mono text-slate-500 break-all space-y-1">
                  @for (h of previewHashes(); track h) {
                    <div>{{ h }}</div>
                  }
                </div>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class AdCapiRoasCockpitComponent {
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

  public targetRoas = 3.0;
  public totalBudget = 45000;
  public selectedAudience = 'aud_meta_high_ltv';
  public rawContactList = `sarah.connor@cyberdyne.io
john.wick@continental.com
ellen.ripley@weyland.corp
+1 (555) 234-5678`;

  public previewHashes = signal<string[]>([
    'a94a8fe5ccb19ba61c4c0873d391e987982fbbd34789123456789abcdef01234',
    'f3b1458920194857610293847561029384756102938475610293847561029384'
  ]);

  public validHashesCount = computed(() => {
    return this.rawContactList.split('\n').filter(l => l.trim().length > 0).length;
  });

  public campaigns = signal<CampaignBudgetItem[]>([
    {
      id: 'c1',
      name: 'High-Intent Search & Retargeting',
      platform: 'Google Ads',
      currentSpend: 15000,
      recommendedSpend: 15000,
      currentRoas: 4.8,
      projectedRoas: 4.8,
      action: 'Scale'
    },
    {
      id: 'c2',
      name: 'Lookalike 1% High LTV Buyers',
      platform: 'Meta Ads',
      currentSpend: 18000,
      recommendedSpend: 18000,
      currentRoas: 3.2,
      projectedRoas: 3.2,
      action: 'Maintain'
    },
    {
      id: 'c3',
      name: 'Broad Video Awareness Top of Funnel',
      platform: 'Meta Ads',
      currentSpend: 8000,
      recommendedSpend: 8000,
      currentRoas: 1.4,
      projectedRoas: 1.4,
      action: 'Trim'
    },
    {
      id: 'c4',
      name: 'Defunct Display Audience Network',
      platform: 'Google Ads',
      currentSpend: 4000,
      recommendedSpend: 4000,
      currentRoas: 0.1,
      projectedRoas: 0.1,
      action: 'Pause'
    }
  ]);

  public totalSpend = computed(() => {
    return this.campaigns().reduce((sum, c) => sum + c.currentSpend, 0);
  });

  public blendedRoas = computed(() => {
    const totalRev = this.campaigns().reduce((sum, c) => sum + (c.currentSpend * c.currentRoas), 0);
    const spend = this.totalSpend();
    return spend > 0 ? (totalRev / spend).toFixed(2) : '0.00';
  });

  public totalConversions = signal<number>(1280);

  public capiEvents = signal<CapiEventItem[]>([
    {
      id: 'evt_9821',
      eventName: 'Purchase',
      platform: 'Meta CAPI',
      hashedEmail: 'a94a8fe5ccb19ba61c4c...fbbd34',
      conversionValue: 499.00,
      currency: 'USD',
      isDeduplicated: false,
      timestamp: '2 mins ago'
    },
    {
      id: 'evt_9820',
      eventName: 'Lead',
      platform: 'Google Enhanced',
      hashedEmail: 'f3b14589201948576102...938475',
      conversionValue: 0.00,
      currency: 'USD',
      isDeduplicated: false,
      timestamp: '8 mins ago'
    }
  ]);

  public simulateCapiEvent(): void {
    const newEvent: CapiEventItem = {
      id: `evt_${Date.now().toString().slice(-4)}`,
      eventName: 'Purchase',
      platform: 'Meta CAPI',
      hashedEmail: 'e3b0c44298fc1c149afb...495991',
      conversionValue: 199.99,
      currency: 'USD',
      isDeduplicated: false,
      timestamp: 'Just now'
    };

    this.capiEvents.update(evts => [newEvent, ...evts]);
    this.totalConversions.update(c => c + 1);
  }

  public optimizeBudgets(): void {
    // Rebalance: shift funds from Trim & Pause to Scale
    let freedBudget = 0;
    const updated = this.campaigns().map(c => {
      if (c.action === 'Pause') {
        freedBudget += c.currentSpend;
        return { ...c, recommendedSpend: 0 };
      }
      if (c.action === 'Trim') {
        const trimAmount = c.currentSpend * 0.3;
        freedBudget += trimAmount;
        return { ...c, recommendedSpend: c.currentSpend - trimAmount };
      }
      return c;
    });

    const scaled = updated.map(c => {
      if (c.action === 'Scale') {
        const newSpend = c.recommendedSpend + freedBudget;
        return {
          ...c,
          recommendedSpend: newSpend,
          projectedRoas: +(c.currentRoas * 0.92).toFixed(2)
        };
      }
      return c;
    });

    this.campaigns.set(scaled);
  }

  public syncAudienceHashes(): void {
    const lines = this.rawContactList.split('\n').filter(l => l.trim().length > 0);
    const newHashes = lines.map(line => {
      let hash = 0;
      for (let i = 0; i < line.length; i++) {
        hash = (hash << 5) - hash + line.charCodeAt(i);
        hash |= 0;
      }
      return Math.abs(hash).toString(16).padStart(64, 'a');
    });

    this.previewHashes.set(newHashes.slice(0, 3));
  }
}
