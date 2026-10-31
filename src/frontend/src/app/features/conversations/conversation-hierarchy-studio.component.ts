import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface HierarchyTicketNode {
  id: string;
  title: string;
  status: 'Open' | 'Pending' | 'Resolved' | 'Merged';
  relationship: 'Parent' | 'Child' | 'Split' | 'Merged';
  messagesCount: number;
  isBlocking: boolean;
  children: HierarchyTicketNode[];
}

@Component({
  selector: 'app-conversation-hierarchy-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <!-- Header -->
      <div class="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8 pb-6 border-b border-slate-800">
        <div>
          <div class="flex items-center gap-3">
            <span class="p-2 bg-purple-500/10 text-purple-400 rounded-lg border border-purple-500/20 text-xl font-bold">🔀</span>
            <div>
              <h1 class="text-2xl font-bold text-white tracking-tight">Conversation Split, Merge & Hierarchy Studio</h1>
              <p class="text-slate-400 text-sm">Zammad-style sub-ticket lineages, message extraction, duplicate merging & blocker graphs</p>
            </div>
          </div>
        </div>
        <div class="flex items-center gap-3">
          <button (click)="openMergeModal()" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-lg text-sm font-medium border border-slate-700 transition">
            🔗 Merge Tickets
          </button>
          <button (click)="openSplitModal()" class="px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white rounded-lg text-sm font-medium shadow-lg shadow-purple-900/30 transition">
            ✂️ Split Conversation
          </button>
        </div>
      </div>

      <!-- KPI Summary -->
      <div class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Hierarchy Lineages</div>
          <div class="text-3xl font-extrabold text-purple-400">14</div>
          <div class="text-xs text-slate-400 mt-1">Active Parent-Child ticket trees</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Split Tickets Extracted</div>
          <div class="text-3xl font-extrabold text-indigo-400">38</div>
          <div class="text-xs text-indigo-400 mt-1">Multi-issue tickets separated</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Duplicates Merged</div>
          <div class="text-3xl font-extrabold text-emerald-400">92</div>
          <div class="text-xs text-emerald-400 mt-1">Single timeline reconciliation</div>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/60 rounded-xl p-5">
          <div class="text-slate-400 text-xs font-semibold uppercase tracking-wider mb-1">Active Blockers</div>
          <div class="text-3xl font-extrabold text-rose-400">3</div>
          <div class="text-xs text-rose-500/80 mt-1">Preventing parent ticket close</div>
        </div>
      </div>

      <!-- Main Layout -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Hierarchy Visual Tree -->
        <div class="lg:col-span-2 bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <div class="flex items-center justify-between mb-4">
            <h2 class="text-lg font-semibold text-white">Visual Ticket Hierarchy & Lineage Tree</h2>
            <span class="text-xs px-2.5 py-1 rounded bg-slate-800 text-slate-400 border border-slate-700">Root: #TK-8012</span>
          </div>

          <!-- Tree Root Item -->
          <div class="space-y-4">
            <div class="p-4 bg-slate-800/90 border border-purple-500/40 rounded-xl relative">
              <div class="flex items-start justify-between">
                <div>
                  <div class="flex items-center gap-2 mb-1">
                    <span class="px-2 py-0.5 rounded text-xs font-bold uppercase bg-purple-500/20 text-purple-300 border border-purple-500/30">
                      Parent Ticket
                    </span>
                    <span class="text-xs px-2 py-0.5 rounded font-medium bg-amber-500/20 text-amber-300">
                      Pending Sub-tasks
                    </span>
                  </div>
                  <h3 class="text-base font-bold text-white">#TK-8012: Damaged Commercial Shipment & Invoice Dispute</h3>
                  <p class="text-xs text-slate-400 mt-1">Client: Modern Office Supplies • Assigned: Sami Support</p>
                </div>
                <div class="text-right">
                  <span class="text-xs px-2 py-1 rounded bg-rose-500/10 text-rose-400 border border-rose-500/20 font-bold">
                    🔒 Closure Blocked (1 open child)
                  </span>
                </div>
              </div>

              <!-- Children / Sub-nodes -->
              <div class="mt-4 pl-6 border-l-2 border-purple-500/30 space-y-3">
                <div class="p-3 bg-slate-800/70 border border-slate-700 rounded-lg flex items-center justify-between">
                  <div>
                    <div class="flex items-center gap-2 mb-0.5">
                      <span class="text-xs px-1.5 py-0.5 rounded bg-indigo-500/20 text-indigo-300 font-semibold">✂️ Split Ticket</span>
                      <span class="text-xs px-1.5 py-0.5 rounded bg-rose-500/20 text-rose-300 font-semibold">Open (Blocking)</span>
                    </div>
                    <div class="text-sm font-semibold text-white">#TK-8015: Warehouse Replacement Dispatch</div>
                    <div class="text-xs text-slate-400">Department: Logistics • 4 messages moved</div>
                  </div>
                  <button class="px-2.5 py-1 text-xs bg-slate-700 hover:bg-slate-600 text-slate-200 rounded transition">
                    View
                  </button>
                </div>

                <div class="p-3 bg-slate-800/70 border border-slate-700 rounded-lg flex items-center justify-between">
                  <div>
                    <div class="flex items-center gap-2 mb-0.5">
                      <span class="text-xs px-1.5 py-0.5 rounded bg-emerald-500/20 text-emerald-300 font-semibold">🔗 Merged Ticket</span>
                      <span class="text-xs px-1.5 py-0.5 rounded bg-emerald-500/20 text-emerald-400 font-semibold">Resolved</span>
                    </div>
                    <div class="text-sm font-semibold text-white">#TK-8014: Duplicate WhatsApp: Package arrived broken</div>
                    <div class="text-xs text-slate-400">Merged at 14:22 UTC • Reconciled into #TK-8012</div>
                  </div>
                  <span class="text-xs text-emerald-400">Resolved ✓</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- Split / Merge Operations Panel -->
        <div class="space-y-6">
          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-3">Quick Split Wizard</h2>
            <p class="text-xs text-slate-400 mb-4">Select messages from the active ticket to extract into a dedicated department inquiry.</p>
            <div class="space-y-3">
              <div class="p-3 bg-slate-800/70 border border-slate-700/60 rounded-lg text-xs">
                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" checked class="rounded border-slate-600 bg-slate-700 text-purple-600 focus:ring-0">
                  <span class="text-slate-200">"Also can you resend the corrected invoice with VAT?"</span>
                </label>
              </div>
              <div class="p-3 bg-slate-800/70 border border-slate-700/60 rounded-lg text-xs">
                <label class="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" checked class="rounded border-slate-600 bg-slate-700 text-purple-600 focus:ring-0">
                  <span class="text-slate-200">"The tax identification number is MF: 0987654/A"</span>
                </label>
              </div>
              <input type="text" placeholder="Child Ticket Title (e.g. VAT Invoice Resend)" class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-xs text-slate-100 focus:outline-none focus:border-purple-500">
              <button class="w-full py-2 bg-purple-600 hover:bg-purple-500 text-white rounded-lg text-xs font-semibold transition">
                Extract & Create Linked Child Ticket
              </button>
            </div>
          </div>

          <div class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
            <h2 class="text-lg font-semibold text-white mb-2">Blocker Enforcement Policy</h2>
            <p class="text-xs text-slate-400 leading-relaxed">
              When sub-tickets are created with the <strong>IsBlocking</strong> flag, the parent ticket cannot transition to <code>Closed</code> or <code>Resolved</code> until all blocking child tickets are verified complete.
            </p>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ConversationHierarchyStudioComponent {
  openSplitModal(): void {}
  openMergeModal(): void {}
}
