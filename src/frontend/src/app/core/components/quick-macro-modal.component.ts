import { Component, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { KeyboardShortcutsService } from '../services/keyboard-shortcuts.service';

interface MacroAction {
  id: string;
  title: string;
  category: string;
  route: string;
  badge: string;
  shortcut?: string;
  icon: string;
}

const MACRO_ACTIONS: MacroAction[] = [
  { id: 'softphone', title: 'Launch Softphone & Auto Dialer', category: 'Voice & Outreach', route: '/softphone', badge: 'WebRTC', shortcut: 'Ctrl+Alt+D', icon: '📞' },
  { id: 'missions', title: 'Open Autonomous SDR Agent Missions', category: 'AI Agents', route: '/agents/missions', badge: 'Fleet', shortcut: 'Ctrl+Alt+M', icon: '🤖' },
  { id: 'commerce_connectors', title: 'Configure E-Commerce Sync Bridges', category: 'Commerce', route: '/commerce/connectors', badge: 'Shopify & WC', icon: '🔌' },
  { id: 'cpq', title: 'Interactive CPQ Quote & Proposal Builder', category: 'Commerce', route: '/cpq', badge: 'Pricing', icon: '💼' },
  { id: 'inbox', title: 'Omnichannel Message Stream & Macros', category: 'Conversations', route: '/inbox/omnichannel', badge: 'MIME', icon: '💬' },
  { id: 'analytics', title: 'Multi-Touch Attribution Analytics', category: 'BI & Growth', route: '/analytics/attribution', badge: 'Realtime', icon: '📊' },
  { id: 'warmup', title: 'P2P Deliverability Warmup Swarm', category: 'Deliverability', route: '/deliverability/warmup-swarm', badge: 'DNS Radar', icon: '🔥' },
  { id: 'webhooks', title: 'Developer Webhooks Cockpit & SDK', category: 'Developer', route: '/developer/webhooks', badge: 'Outbox', icon: '⚡' }
];

@Component({
  selector: 'app-quick-macro-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    @if (shortcuts.isMacroSearchOpen()) {
      <div 
        (click)="shortcuts.closeMacroSearch()"
        class="fixed inset-0 z-50 bg-black/80 backdrop-blur-sm flex items-start justify-center pt-24 px-4 transition-opacity animate-in fade-in"
        data-testid="quick-macro-backdrop">
        
        <div 
          (click)="$event.stopPropagation()"
          class="w-full max-w-xl bg-slate-900 border border-slate-700/80 rounded-2xl shadow-2xl overflow-hidden flex flex-col"
          data-testid="quick-macro-modal">
          
          <!-- Search Header -->
          <div class="p-4 border-b border-slate-800 flex items-center gap-3">
            <span class="text-xl">⚡</span>
            <input
              type="text"
              [(ngModel)]="searchQuery"
              placeholder="Search platform macros & shortcuts... (e.g. dialer, missions, shopify)"
              autofocus
              class="flex-1 bg-transparent border-0 text-white placeholder-slate-400 text-sm focus:outline-none"
              data-testid="macro-search-input"
            />
            <kbd class="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-800 text-slate-400 border border-slate-700">ESC to close</kbd>
          </div>

          <!-- Shortcuts Hint Row -->
          <div class="px-4 py-2 bg-slate-950/60 border-b border-slate-800/80 flex items-center justify-between text-[11px] text-slate-400 font-mono">
            <span>Global Shortcuts:</span>
            <div class="flex items-center gap-2">
              <span class="px-1.5 py-0.5 rounded bg-slate-800 text-indigo-300">Ctrl+Shift+Space (Macros)</span>
              <span class="px-1.5 py-0.5 rounded bg-slate-800 text-cyan-300">Ctrl+Alt+D (Dialer)</span>
              <span class="px-1.5 py-0.5 rounded bg-slate-800 text-purple-300">Ctrl+Alt+M (Missions)</span>
            </div>
          </div>

          <!-- Actions List -->
          <div class="max-h-80 overflow-y-auto p-2 space-y-1">
            @for (action of filteredActions(); track action.id) {
              <div
                (click)="execute(action)"
                class="w-full flex items-center justify-between p-3 rounded-xl hover:bg-slate-800/80 cursor-pointer transition text-left group">
                <div class="flex items-center gap-3">
                  <span class="text-lg p-2 rounded-lg bg-slate-800 group-hover:bg-indigo-600/20 text-white transition">{{ action.icon }}</span>
                  <div>
                    <h4 class="text-xs font-semibold text-white group-hover:text-indigo-300 transition">{{ action.title }}</h4>
                    <span class="text-[10px] text-slate-400">{{ action.category }}</span>
                  </div>
                </div>

                <div class="flex items-center gap-2">
                  @if (action.shortcut) {
                    <kbd class="text-[10px] font-mono px-1.5 py-0.5 rounded bg-slate-950 text-slate-300 border border-slate-800">
                      {{ action.shortcut }}
                    </kbd>
                  }
                  <span class="text-[10px] font-mono px-2 py-0.5 rounded-full bg-slate-800 text-slate-300 group-hover:bg-indigo-500/20 group-hover:text-indigo-300 transition">
                    {{ action.badge }}
                  </span>
                </div>
              </div>
            } @empty {
              <div class="p-8 text-center text-xs text-slate-500">
                No matching macros found for "{{ searchQuery }}"
              </div>
            }
          </div>
        </div>
      </div>
    }

    <!-- Shortcut Trigger Toast -->
    @if (shortcuts.shortcutToastMessage()) {
      <div 
        class="fixed bottom-6 right-6 z-50 bg-indigo-600/95 border border-indigo-400/40 text-white px-4 py-2.5 rounded-xl shadow-2xl flex items-center gap-2 text-xs font-medium animate-in slide-in-from-bottom"
        data-testid="shortcut-toast">
        <span>⚡</span>
        <span>{{ shortcuts.shortcutToastMessage() }}</span>
      </div>
    }
  `
})
export class QuickMacroModalComponent {
  public readonly shortcuts = inject(KeyboardShortcutsService);
  private readonly router = inject(Router);

  public searchQuery = '';

  public readonly filteredActions = computed(() => {
    const q = this.searchQuery.trim().toLowerCase();
    if (!q) return MACRO_ACTIONS;
    return MACRO_ACTIONS.filter(a => 
      a.title.toLowerCase().includes(q) || 
      a.category.toLowerCase().includes(q) || 
      a.badge.toLowerCase().includes(q)
    );
  });

  public execute(action: MacroAction): void {
    this.shortcuts.closeMacroSearch();
    this.router.navigate([action.route]);
  }
}
