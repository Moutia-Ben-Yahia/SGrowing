import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface ChannelAttributionUi {
  channel: string;
  campaign: string;
  stage: string;
  spend: number;
  attributedRevenue: number;
  roas: number;
  weight: number;
}

@Component({
  selector: 'app-analytics-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Left Sidebar -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-cyan-500 to-blue-500 flex items-center justify-center font-bold text-white shadow-lg shadow-cyan-500/20">
              📊
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-cyan-400 font-medium">Executive BI & Attribution</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <div class="space-y-1">
              <a routerLink="/" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📥 Omnichannel Inbox</span>
              </a>
              <a routerLink="/analytics" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-cyan-500/20 text-cyan-300 border border-cyan-500/30 font-medium">
                <span>📊 Attribution & Executive BI</span>
              </a>
              <a routerLink="/commerce" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-amber-400 hover:bg-amber-500/10 border border-amber-500/20 transition">
                <span>🛒 Commerce & CPQ</span>
              </a>
              <a routerLink="/contracts" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-emerald-400 hover:bg-emerald-500/10 border border-emerald-500/20 transition">
                <span>✍️ In-Chat E-Signatures</span>
              </a>
              <a routerLink="/accounts" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-blue-400 hover:bg-blue-500/10 border border-blue-500/20 transition">
                <span>🏢 B2B Accounts CRM</span>
              </a>
            </div>
          </div>

          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-cyan-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-cyan-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-cyan-400 animate-pulse"></span>
                AI Prompt Firewall
              </span>
              <span class="text-[10px] bg-emerald-500/10 text-emerald-300 border border-emerald-500/20 px-1.5 py-0.5 rounded">Active</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              Jailbreak prevention & prompt injection defenses active. AI requests dynamically routed by task complexity.
            </p>
          </div>
        </div>
      </aside>

      <!-- Main Content -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <div>
            <h2 class="text-base font-semibold text-white">Multi-Touch Revenue Attribution & Executive BI</h2>
            <p class="text-xs text-slate-400">Connect marketing touchpoints, conversations, deals, and SaaS usage metering.</p>
          </div>
          <div class="flex items-center gap-2">
            <span class="text-xs text-slate-400 mr-2">Attribution Model:</span>
            <select
              [(ngModel)]="selectedModel"
              (ngModelChange)="applyModelChange()"
              class="bg-slate-900 border border-slate-700 text-xs text-cyan-300 rounded-lg px-2.5 py-1.5 focus:outline-none">
              <option value="WShaped">W-Shaped (40/40/20)</option>
              <option value="FirstTouch">First-Touch (100%)</option>
              <option value="LastTouch">Last-Touch (100%)</option>
              <option value="Linear">Linear (Equal Split)</option>
            </select>
          </div>
        </header>

        <div class="flex-1 overflow-y-auto p-6 space-y-6 max-w-6xl">
          <!-- Executive KPI Cards -->
          <div class="grid grid-cols-4 gap-4">
            <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
              <span class="text-xs text-slate-400">Attributed Closed Revenue</span>
              <p class="text-xl font-bold text-emerald-400 mt-1 font-mono">\${{ totalRevenue() | number:'1.0-0' }}</p>
              <span class="text-[10px] text-emerald-500/80 mt-1 inline-block">↑ 34% vs last cycle</span>
            </div>
            <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
              <span class="text-xs text-slate-400">Total Acquisition Spend</span>
              <p class="text-xl font-bold text-white mt-1 font-mono">\${{ totalSpend() | number:'1.0-0' }}</p>
              <span class="text-[10px] text-slate-400 mt-1 inline-block">Google Ads + Meta + SDR</span>
            </div>
            <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
              <span class="text-xs text-slate-400">Blended ROAS</span>
              <p class="text-xl font-bold text-cyan-400 mt-1 font-mono">{{ roas() }}x</p>
              <span class="text-[10px] text-cyan-500/80 mt-1 inline-block">High capital efficiency</span>
            </div>
            <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
              <span class="text-xs text-slate-400">Customer Acquisition Cost</span>
              <p class="text-xl font-bold text-amber-400 mt-1 font-mono">\${{ cac() | number:'1.0-0' }}</p>
              <span class="text-[10px] text-slate-400 mt-1 inline-block">LTV / CAC = 6.8x</span>
            </div>
          </div>

          <!-- SaaS Usage Metering Gauge -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 space-y-4">
            <div class="flex justify-between items-center">
              <h3 class="text-sm font-semibold text-white">SaaS Plan Quotas & Usage Metering</h3>
              <span class="text-xs text-cyan-400 font-mono font-medium">Growth Plan Tier (\$249/mo)</span>
            </div>
            <div class="grid grid-cols-3 gap-4 text-xs">
              <!-- AI Tokens -->
              <div class="p-3 bg-slate-950 border border-slate-800 rounded-lg space-y-2">
                <div class="flex justify-between">
                  <span class="text-slate-400">AI Tokens Meter</span>
                  <span class="font-mono text-cyan-400">1.2M / 5.0M</span>
                </div>
                <div class="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                  <div class="bg-cyan-500 h-1.5 rounded-full" style="width: 24%"></div>
                </div>
                <span class="text-[10px] text-slate-500">24% utilized (Gemini Flash + GPT-4o-mini)</span>
              </div>
              <!-- Voice Minutes -->
              <div class="p-3 bg-slate-950 border border-slate-800 rounded-lg space-y-2">
                <div class="flex justify-between">
                  <span class="text-slate-400">Voice Telephony</span>
                  <span class="font-mono text-emerald-400">320 / 1,000 min</span>
                </div>
                <div class="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                  <div class="bg-emerald-500 h-1.5 rounded-full" style="width: 32%"></div>
                </div>
                <span class="text-[10px] text-slate-500">32% utilized (<400ms WebSocket)</span>
              </div>
              <!-- Outbound Messages -->
              <div class="p-3 bg-slate-950 border border-slate-800 rounded-lg space-y-2">
                <div class="flex justify-between">
                  <span class="text-slate-400">Outbound Messages</span>
                  <span class="font-mono text-purple-400">4,150 / 10,000</span>
                </div>
                <div class="w-full bg-slate-800 rounded-full h-1.5 overflow-hidden">
                  <div class="bg-purple-500 h-1.5 rounded-full" style="width: 41.5%"></div>
                </div>
                <span class="text-[10px] text-slate-500">WhatsApp Cloud + Twilio SMS</span>
              </div>
            </div>
          </div>

          <!-- Channel Attribution Breakdown Table -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
            <div class="p-4 border-b border-slate-800 flex items-center justify-between">
              <h3 class="text-sm font-semibold text-white">Channel & Campaign Attribution Breakdown</h3>
              <span class="text-xs text-slate-400 font-mono">{{ channels().length }} touchpoint channels</span>
            </div>

            <table class="w-full text-left text-xs">
              <thead class="bg-slate-950 text-slate-400 border-b border-slate-800">
                <tr>
                  <th class="p-3">Channel & Campaign</th>
                  <th class="p-3">Touchpoint Role</th>
                  <th class="p-3">Ad Spend</th>
                  <th class="p-3">Weight %</th>
                  <th class="p-3">Attributed Revenue</th>
                  <th class="p-3 text-right">Channel ROAS</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800 text-slate-200">
                <tr *ngFor="let item of channels()" class="hover:bg-slate-800/40 transition">
                  <td class="p-3">
                    <div class="font-medium text-white">{{ item.channel }}</div>
                    <div class="text-[11px] text-slate-400 font-mono">{{ item.campaign }}</div>
                  </td>
                  <td class="p-3">
                    <span class="px-2 py-0.5 rounded bg-slate-800 text-[10px] text-cyan-300">{{ item.stage }}</span>
                  </td>
                  <td class="p-3 font-mono text-slate-300">\${{ item.spend | number:'1.0-0' }}</td>
                  <td class="p-3 font-mono font-medium">{{ item.weight }}%</td>
                  <td class="p-3 font-mono font-bold text-emerald-400">\${{ item.attributedRevenue | number:'1.0-0' }}</td>
                  <td class="p-3 text-right font-mono font-bold text-cyan-400">{{ item.roas | number:'1.1-1' }}x</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </main>
    </div>
  `
})
export class AnalyticsStudioComponent {
  selectedModel = 'WShaped';

  totalRevenue = signal(125000);
  totalSpend = signal(12000);
  roas = signal(10.4);
  cac = signal(1200);

  channels = signal<ChannelAttributionUi[]>([
    {
      channel: 'Google Search Ads',
      campaign: 'Enterprise-Intent-Global',
      stage: 'First Discovery',
      spend: 5000,
      attributedRevenue: 50000,
      roas: 10.0,
      weight: 40
    },
    {
      channel: 'LinkedIn Inbound SDR',
      campaign: 'VP-Sales-Outreach-Q1',
      stage: 'Lead Creation',
      spend: 4000,
      attributedRevenue: 50000,
      roas: 12.5,
      weight: 40
    },
    {
      channel: 'WhatsApp Live Chat',
      campaign: 'Closing-Assisted-Checkout',
      stage: 'Deal Won',
      spend: 3000,
      attributedRevenue: 25000,
      roas: 8.3,
      weight: 20
    }
  ]);

  applyModelChange() {
    if (this.selectedModel === 'FirstTouch') {
      this.channels.set([
        {
          channel: 'Google Search Ads',
          campaign: 'Enterprise-Intent-Global',
          stage: 'First Discovery',
          spend: 5000,
          attributedRevenue: 125000,
          roas: 25.0,
          weight: 100
        }
      ]);
    } else if (this.selectedModel === 'LastTouch') {
      this.channels.set([
        {
          channel: 'WhatsApp Live Chat',
          campaign: 'Closing-Assisted-Checkout',
          stage: 'Deal Won',
          spend: 3000,
          attributedRevenue: 125000,
          roas: 41.6,
          weight: 100
        }
      ]);
    } else {
      this.channels.set([
        {
          channel: 'Google Search Ads',
          campaign: 'Enterprise-Intent-Global',
          stage: 'First Discovery',
          spend: 5000,
          attributedRevenue: 50000,
          roas: 10.0,
          weight: 40
        },
        {
          channel: 'LinkedIn Inbound SDR',
          campaign: 'VP-Sales-Outreach-Q1',
          stage: 'Lead Creation',
          spend: 4000,
          attributedRevenue: 50000,
          roas: 12.5,
          weight: 40
        },
        {
          channel: 'WhatsApp Live Chat',
          campaign: 'Closing-Assisted-Checkout',
          stage: 'Deal Won',
          spend: 3000,
          attributedRevenue: 25000,
          roas: 8.3,
          weight: 20
        }
      ]);
    }
  }
}
