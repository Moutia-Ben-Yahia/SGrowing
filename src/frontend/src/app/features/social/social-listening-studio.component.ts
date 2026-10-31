import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface SocialMentionItem {
  id: string;
  platform: 'TwitterX' | 'Trustpilot' | 'GoogleReviews' | 'Instagram' | 'LinkedIn';
  author: string;
  followers: number;
  text: string;
  sentiment: 'Positive' | 'Neutral' | 'Negative' | 'SevereHostile';
  threat: 'NormalGreen' | 'ElevatedYellow' | 'HighAlertOrange' | 'CrisisRedEmergency';
  timeAgo: string;
}

@Component({
  selector: 'app-social-listening-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8 pb-6 border-b border-slate-800">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-rose-500/10 text-rose-400 rounded-lg border border-rose-500/20 text-xl font-bold">📡</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Social Listening & Brand Crisis Sentinel</h1>
              <p class="text-slate-400 text-sm">Real-time omnichannel mention ingestion, sentiment spike detection & automated broadcast deflection</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="toggleBroadcasts()" [class]="isHalted() ? 'bg-amber-600 hover:bg-amber-500' : 'bg-slate-800 hover:bg-slate-700'" class="px-4 py-2 text-white rounded-lg text-sm font-medium border border-slate-700 transition">
            {{ isHalted() ? '▶️ Resume Marketing Broadcasts' : '⏸️ Emergency Pause Broadcasts' }}
          </button>
        </div>
      </div>

      <!-- Threat Level Banner -->
      <div [class]="getThreatBannerClass()" class="p-4 rounded-xl border mb-8 flex flex-col md:flex-row items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <span class="text-2xl">{{ currentThreat() === 'CrisisRedEmergency' ? '🚨' : currentThreat() === 'HighAlertOrange' ? '⚠️' : '🛡️' }}</span>
          <div>
            <div class="text-xs font-bold uppercase tracking-wider">Active Threat Level: {{ currentThreat() }}</div>
            <div class="text-sm font-semibold text-white mt-0.5">
              {{ currentThreat() === 'CrisisRedEmergency' ? 'CRITICAL BRAND CRISIS: High-influence hostile mentions detected.' : 'Brand sentiment stable. Anomaly radar monitoring social streams.' }}
            </div>
          </div>
        </div>
        <div *ngIf="isHalted()" class="px-3 py-1 bg-rose-600 text-white rounded-lg text-xs font-bold animate-pulse">
          🛡️ Outbound Marketing Broadcasts Halted
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Total Mentions (24h)</div>
          <div class="text-3xl font-extrabold text-white">{{ mentions().length + 840 }}</div>
          <div class="text-xs text-slate-400 mt-1">Across 6 social channels</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Negative Sentiment Ratio</div>
          <div class="text-3xl font-extrabold text-rose-400">{{ negativeRatio() }}%</div>
          <div class="text-xs text-rose-500/80 mt-1">Spike threshold: 25.0%</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Brand Health Index</div>
          <div class="text-3xl font-extrabold text-emerald-400">82.4/100</div>
          <div class="text-xs text-emerald-400 mt-1">Positive / Neutral majority</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Active Crisis Alerts</div>
          <div class="text-3xl font-extrabold text-amber-400">{{ activeCrisisCount() }}</div>
          <div class="text-xs text-amber-400 mt-1">Requires PR response</div>
        </div>
      </div>

      <!-- Main Columns -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Live Mentions Feed -->
        <div class="lg:col-span-2 bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-white">Live Mention Ingestion Stream</h2>
            <div class="flex gap-2">
              <button *ngFor="let tab of ['All', 'Negative', 'SevereHostile']"
                (click)="selectedTab.set(tab)"
                [class.bg-slate-700]="selectedTab() === tab"
                [class.text-white]="selectedTab() === tab"
                class="px-3 py-1 text-xs rounded-md text-slate-400 hover:text-slate-200 transition">
                {{ tab }}
              </button>
            </div>
          </div>

          <div class="space-y-3">
            <div *ngFor="let item of filteredMentions()" class="p-4 bg-slate-800/80 border border-slate-700 rounded-lg hover:border-slate-600 transition">
              <div class="flex items-start justify-between gap-4">
                <div class="flex-1">
                  <div class="flex items-center gap-2 mb-1">
                    <span [class]="getPlatformBadge(item.platform)" class="px-2 py-0.5 rounded text-xs font-bold uppercase">
                      {{ item.platform }}
                    </span>
                    <span [class]="getSentimentBadge(item.sentiment)" class="px-2 py-0.5 rounded text-xs font-semibold">
                      {{ item.sentiment }}
                    </span>
                    <span class="text-xs text-slate-400">{{ item.author }} ({{ item.followers | number }} followers)</span>
                  </div>
                  <p class="text-sm text-slate-200 mt-1 font-sans">"{{ item.text }}"</p>
                  <div class="text-xs text-slate-500 mt-2">Ingested {{ item.timeAgo }}</div>
                </div>

                <div *ngIf="item.sentiment === 'SevereHostile' || item.sentiment === 'Negative'">
                  <button (click)="escalateToPr(item)" class="px-3 py-1.5 bg-rose-600 hover:bg-rose-500 text-white rounded text-xs font-medium transition">
                    Escalate to PR
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Sidebar: Crisis Rules & Deflection -->
        <div class="space-y-6">
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-3">Brand Crisis Trigger Keywords</h2>
            <div class="flex flex-wrap gap-2 text-xs">
              <span class="px-2 py-1 rounded bg-rose-500/20 text-rose-300 border border-rose-500/30">scam</span>
              <span class="px-2 py-1 rounded bg-rose-500/20 text-rose-300 border border-rose-500/30">fraud</span>
              <span class="px-2 py-1 rounded bg-rose-500/20 text-rose-300 border border-rose-500/30">boycott</span>
              <span class="px-2 py-1 rounded bg-rose-500/20 text-rose-300 border border-rose-500/30">lawsuit</span>
              <span class="px-2 py-1 rounded bg-rose-500/20 text-rose-300 border border-rose-500/30">outage</span>
              <span class="px-2 py-1 rounded bg-rose-500/20 text-rose-300 border border-rose-500/30">catastrophe</span>
            </div>
            <p class="text-xs text-slate-400 mt-3 leading-relaxed">
              Detection of these keywords immediately elevates the incident threat tier and triggers automated protective actions.
            </p>
          </div>

          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-2">Automated Broadcast Deflection</h2>
            <p class="text-xs text-slate-400 leading-relaxed">
              When a brand crisis is triggered, scheduled social marketing posts and WhatsApp newsletter blasts are automatically frozen to prevent tone-deaf posting during high-stress customer incidents.
            </p>
          </div>
        </div>
      </div>
    </div>
  `
})
export class SocialListeningStudioComponent {
  selectedTab = signal<string>('All');
  isHalted = signal<boolean>(false);
  currentThreat = signal<string>('ElevatedYellow');

  mentions = signal<SocialMentionItem[]>([
    {
      id: 'm-1',
      platform: 'TwitterX',
      author: '@tech_influencer',
      followers: 48000,
      text: 'Major checkout latency today on SGrowing platform, broken payment gateway for 20 mins!',
      sentiment: 'SevereHostile',
      threat: 'HighAlertOrange',
      timeAgo: '4m ago'
    },
    {
      id: 'm-2',
      platform: 'Trustpilot',
      author: 'amira_s',
      followers: 120,
      text: 'Delivery was slightly delayed but customer support on WhatsApp was wonderful and solved it.',
      sentiment: 'Positive',
      threat: 'NormalGreen',
      timeAgo: '18m ago'
    },
    {
      id: 'm-3',
      platform: 'GoogleReviews',
      author: 'mohamed_k',
      followers: 45,
      text: 'Broken package arrived from courier, waiting for replacement.',
      sentiment: 'Negative',
      threat: 'ElevatedYellow',
      timeAgo: '42m ago'
    }
  ]);

  negativeRatio = computed(() => {
    const total = this.mentions().length;
    if (total === 0) return 0;
    const negs = this.mentions().filter(m => m.sentiment === 'Negative' || m.sentiment === 'SevereHostile').length;
    return Math.round((negs / total) * 100);
  });

  activeCrisisCount = computed(() => this.mentions().filter(m => m.threat === 'HighAlertOrange' || m.threat === 'CrisisRedEmergency').length);

  filteredMentions = computed(() => {
    const tab = this.selectedTab();
    if (tab === 'All') return this.mentions();
    return this.mentions().filter(m => m.sentiment === tab);
  });

  getThreatBannerClass(): string {
    switch (this.currentThreat()) {
      case 'CrisisRedEmergency': return 'bg-rose-950/40 border-rose-600/50 text-rose-300';
      case 'HighAlertOrange': return 'bg-amber-950/40 border-amber-600/50 text-amber-300';
      default: return 'bg-slate-800/60 border-slate-700 text-slate-300';
    }
  }

  getPlatformBadge(platform: string): string {
    switch (platform) {
      case 'TwitterX': return 'bg-sky-500/20 text-sky-300 border border-sky-500/30';
      case 'Trustpilot': return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      case 'GoogleReviews': return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
      default: return 'bg-slate-700 text-slate-300';
    }
  }

  getSentimentBadge(st: string): string {
    switch (st) {
      case 'Positive': return 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
      case 'Negative': return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
      case 'SevereHostile': return 'bg-rose-500/20 text-rose-300 border border-rose-500/30 font-bold';
      default: return 'bg-slate-700 text-slate-300';
    }
  }

  toggleBroadcasts(): void {
    this.isHalted.update(v => !v);
  }

  escalateToPr(item: SocialMentionItem): void {
    // Demonstration action
  }
}
