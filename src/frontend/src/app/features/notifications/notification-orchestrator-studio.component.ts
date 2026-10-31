import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface NotificationFeedRow {
  id: string;
  topic: string;
  channel: 'InApp' | 'WhatsApp' | 'Sms' | 'Email';
  status: 'Delivered' | 'Read' | 'FallenBack' | 'Throttled' | 'OptedOut';
  title: string;
  recipient: string;
  timeAgo: string;
}

@Component({
  selector: 'app-notification-orchestrator-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8 pb-6 border-b border-slate-800">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-indigo-500/10 text-indigo-400 rounded-lg border border-indigo-500/20 text-xl font-bold">🔔</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Notification Orchestrator & Preference Center</h1>
              <p class="text-slate-400 text-sm">Cascading multi-channel delivery fallback, anti-spam quiet hours & subscriber preferences</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="runFallbackSweep()" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-sm font-medium border border-slate-700 transition">
            🔄 Trigger Fallback Sweep
          </button>
          <button (click)="openDispatchModal()" class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-sm font-medium shadow-lg shadow-indigo-900/30 transition">
            + Dispatch Notification
          </button>
        </div>
      </div>

      <!-- KPI Summary Cards -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Total Dispatched (24h)</div>
          <div class="text-3xl font-extrabold text-indigo-400">{{ totalDispatched() }}</div>
          <div class="text-xs text-slate-400 mt-1">Across 4 delivery channels</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Read / Engagement Rate</div>
          <div class="text-3xl font-extrabold text-emerald-400">89.4%</div>
          <div class="text-xs text-emerald-400 mt-1">↑ 14% via WhatsApp fallback</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Fallback Cascades Triggered</div>
          <div class="text-3xl font-extrabold text-amber-400">128</div>
          <div class="text-xs text-amber-500/80 mt-1">Unread on primary channel</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Quiet-Hours Throttled</div>
          <div class="text-3xl font-extrabold text-slate-300">42</div>
          <div class="text-xs text-slate-400 mt-1">Queued for 07:00 UTC release</div>
        </div>
      </div>

      <!-- Cascading Route Visualizer -->
      <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6 mb-8">
        <h2 class="text-lg font-semibold text-white mb-4">Priority Cascading Delivery Pipeline</h2>
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div class="p-4 bg-slate-800/80 border border-indigo-500/30 rounded-lg relative">
            <div class="flex items-center justify-between mb-2">
              <span class="text-xs font-bold text-indigo-400 uppercase">Stage 1 (Immediate)</span>
              <span class="text-xs px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300 font-semibold">In-App Push</span>
            </div>
            <div class="text-sm font-semibold text-white">In-App Drawer & Banner</div>
            <div class="text-xs text-slate-400 mt-1">TTL: 3m unread window</div>
          </div>

          <div class="p-4 bg-slate-800/80 border border-emerald-500/30 rounded-lg relative">
            <div class="flex items-center justify-between mb-2">
              <span class="text-xs font-bold text-emerald-400 uppercase">Stage 2 (Fallback +3m)</span>
              <span class="text-xs px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300 font-semibold">WhatsApp</span>
            </div>
            <div class="text-sm font-semibold text-white">WhatsApp Cloud Template</div>
            <div class="text-xs text-slate-400 mt-1">Interactive action button</div>
          </div>

          <div class="p-4 bg-slate-800/80 border border-amber-500/30 rounded-lg relative">
            <div class="flex items-center justify-between mb-2">
              <span class="text-xs font-bold text-amber-400 uppercase">Stage 3 (Fallback +6m)</span>
              <span class="text-xs px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 font-semibold">SMS</span>
            </div>
            <div class="text-sm font-semibold text-white">Direct SMS Alert</div>
            <div class="text-xs text-slate-400 mt-1">High-urgency carrier route</div>
          </div>

          <div class="p-4 bg-slate-800/80 border border-rose-500/30 rounded-lg relative">
            <div class="flex items-center justify-between mb-2">
              <span class="text-xs font-bold text-rose-400 uppercase">Stage 4 (Fallback +9m)</span>
              <span class="text-xs px-2 py-0.5 rounded bg-rose-500/20 text-rose-300 font-semibold">Email</span>
            </div>
            <div class="text-sm font-semibold text-white">Transactional Email Digest</div>
            <div class="text-xs text-slate-400 mt-1">Final audit dispatch</div>
          </div>
        </div>
      </div>

      <!-- Main Columns: Feed & Subscriber Preferences -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Live Notifications Feed -->
        <div class="lg:col-span-2 bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-white">Live Dispatched Feed</h2>
            <span class="text-xs text-slate-400">Auto-refresh active</span>
          </div>

          <div class="space-y-3">
            <div *ngFor="let item of feed()" class="p-4 bg-slate-800/80 border border-slate-700 rounded-lg hover:border-slate-600 transition flex items-center justify-between gap-4">
              <div class="flex-1">
                <div class="flex items-center gap-2 mb-1">
                  <span class="text-xs font-semibold px-2 py-0.5 rounded bg-slate-700 text-slate-200">{{ item.topic }}</span>
                  <span [class]="getChannelBadge(item.channel)" class="text-xs px-2 py-0.5 rounded font-medium">
                    {{ item.channel }}
                  </span>
                  <span [class]="getStatusBadge(item.status)" class="text-xs px-2 py-0.5 rounded font-medium">
                    {{ item.status }}
                  </span>
                </div>
                <div class="text-sm font-semibold text-white">{{ item.title }}</div>
                <div class="text-xs text-slate-400 mt-1">Recipient: {{ item.recipient }} • {{ item.timeAgo }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Subscriber Preference Matrix -->
        <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Preference Center Matrix</h2>
          <div class="space-y-4">
            <div class="p-3 bg-slate-800/70 border border-slate-700/60 rounded-lg">
              <div class="text-sm font-medium text-white mb-1">Security & 2FA Alerts</div>
              <div class="text-xs text-slate-400 mb-2">Bypasses quiet hours automatically</div>
              <div class="flex gap-2 text-xs">
                <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300">In-App ✓</span>
                <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300">WhatsApp ✓</span>
                <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300">SMS ✓</span>
              </div>
            </div>

            <div class="p-3 bg-slate-800/70 border border-slate-700/60 rounded-lg">
              <div class="text-sm font-medium text-white mb-1">Billing & Invoices</div>
              <div class="text-xs text-slate-400 mb-2">Subject to standard quiet hours</div>
              <div class="flex gap-2 text-xs">
                <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300">Email ✓</span>
                <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300">In-App ✓</span>
                <span class="px-2 py-0.5 rounded bg-slate-700 text-slate-400">SMS ✕</span>
              </div>
            </div>

            <div class="p-3 bg-slate-800/70 border border-slate-700/60 rounded-lg">
              <div class="text-sm font-medium text-white mb-1">Marketing & Promotions</div>
              <div class="text-xs text-slate-400 mb-2">Capped at 5/week max</div>
              <div class="flex gap-2 text-xs">
                <span class="px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300">WhatsApp ✓</span>
                <span class="px-2 py-0.5 rounded bg-slate-700 text-slate-400">Email ✕</span>
              </div>
            </div>

            <div class="p-3 bg-indigo-500/10 border border-indigo-500/30 rounded-lg text-xs text-indigo-300">
              🛡️ <strong>Quiet Hours Protection:</strong> Active from 22:00 to 07:00 UTC. Non-urgent notifications are automatically queued.
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class NotificationOrchestratorStudioComponent {
  feed = signal<NotificationFeedRow[]>([
    {
      id: 'notif-1',
      topic: 'TicketSla',
      channel: 'InApp',
      status: 'Read',
      title: 'Ticket #102 breached 15m first-response target',
      recipient: 'Sami Support',
      timeAgo: '2m ago'
    },
    {
      id: 'notif-2',
      topic: 'BillingInvoice',
      channel: 'WhatsApp',
      status: 'FallenBack',
      title: 'Invoice #INV-2026-904 ready for download',
      recipient: 'Client Acme Corp',
      timeAgo: '8m ago'
    },
    {
      id: 'notif-3',
      topic: 'SecurityAlert',
      channel: 'Sms',
      status: 'Delivered',
      title: 'New API Key generated from unknown IP',
      recipient: 'Admin DevOps',
      timeAgo: '22m ago'
    }
  ]);

  totalDispatched = computed(() => this.feed().length + 420);

  getChannelBadge(channel: string): string {
    switch (channel) {
      case 'InApp': return 'bg-indigo-500/20 text-indigo-300 border border-indigo-500/30';
      case 'WhatsApp': return 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30';
      case 'Sms': return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
      case 'Email': return 'bg-rose-500/20 text-rose-300 border border-rose-500/30';
      default: return 'bg-slate-700 text-slate-300';
    }
  }

  getStatusBadge(status: string): string {
    switch (status) {
      case 'Delivered': return 'bg-blue-500/20 text-blue-300 border border-blue-500/30';
      case 'Read': return 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
      case 'FallenBack': return 'bg-amber-500/20 text-amber-300 border border-amber-500/30';
      case 'Throttled': return 'bg-slate-700 text-slate-400';
      default: return 'bg-slate-700 text-slate-300';
    }
  }

  runFallbackSweep(): void {
    // Demonstration action
  }

  openDispatchModal(): void {
    const newRow: NotificationFeedRow = {
      id: `notif-${Date.now().toString().slice(-3)}`,
      topic: 'SecurityAlert',
      channel: 'InApp',
      status: 'Delivered',
      title: 'Live security policy re-evaluation alert',
      recipient: 'Current Operator',
      timeAgo: 'Just now'
    };
    this.feed.update(items => [newRow, ...items]);
  }
}
