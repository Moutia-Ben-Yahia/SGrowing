import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface TimesheetEntry {
  id: string;
  teamMemberId: string;
  teamMemberName: string;
  taskTitle: string;
  dealTitle: string;
  description: string;
  hoursWorked: number;
  hourlyRate: number;
  totalLaborCost: number;
  status: 'Submitted' | 'Approved' | 'Rejected';
  workDate: string;
  approvedBy?: string;
}

@Component({
  selector: 'app-timesheet-labor-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans" dir="ltr">
      <!-- Top Navigation Bar -->
      <header class="border-b border-slate-800 bg-slate-900/60 backdrop-blur px-6 py-4 flex items-center justify-between sticky top-0 z-50">
        <div class="flex items-center gap-4">
          <a routerLink="/" class="text-xl font-black bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400 bg-clip-text text-transparent hover:opacity-90">
            SGrowing
          </a>
          <span class="text-slate-600">/</span>
          <div class="flex items-center gap-2">
            <span class="text-sm font-semibold text-slate-200">Workforce Timesheets & Labor Margin Ledger</span>
            <span class="text-xs px-2.5 py-0.5 rounded-full bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 font-medium">
              Ever-Gauzy ERP
            </span>
          </div>
        </div>

        <div class="flex items-center gap-4">
          <button (click)="openLogModal.set(true)" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-medium transition shadow-lg shadow-emerald-950 flex items-center gap-2">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/></svg>
            Log Work Hours
          </button>
          <app-language-switcher />
        </div>
      </header>

      <!-- Main Workspace -->
      <main class="flex-1 p-6 max-w-7xl w-full mx-auto space-y-6">
        <!-- Metric Cards -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Total Logged Hours</div>
            <div class="text-2xl font-bold text-white mt-2">{{ totalHours() }} hrs</div>
            <div class="text-xs text-slate-500 mt-1">Across {{ entries().length }} timecard entries</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Approved Hours</div>
            <div class="text-2xl font-bold text-emerald-400 mt-2">{{ approvedHours() }} hrs</div>
            <div class="text-xs text-emerald-500/80 mt-1">Ready for payroll & client billing</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">True Margin Labor Cost</div>
            <div class="text-2xl font-bold text-cyan-400 mt-2">\${{ totalLaborCost().toFixed(2) }}</div>
            <div class="text-xs text-cyan-500/80 mt-1">Auto-deducted from Deal Margin</div>
          </div>

          <div class="bg-slate-900/80 border border-slate-800 rounded-xl p-5">
            <div class="text-xs font-medium text-slate-400 uppercase tracking-wider">Pending Approval</div>
            <div class="text-2xl font-bold text-amber-400 mt-2">{{ pendingCount() }} entries</div>
            <div class="text-xs text-amber-500/80 mt-1">Requires manager review</div>
          </div>
        </div>

        <!-- Filter & Header -->
        <div class="flex items-center justify-between border-b border-slate-800 pb-4">
          <div class="flex items-center gap-2">
            <button (click)="filterStatus.set('ALL')" [class.bg-slate-800]="filterStatus() === 'ALL'" class="px-3 py-1.5 rounded-lg text-xs font-medium text-slate-300 hover:bg-slate-800 transition">
              All Entries ({{ entries().length }})
            </button>
            <button (click)="filterStatus.set('Submitted')" [class.bg-slate-800]="filterStatus() === 'Submitted'" class="px-3 py-1.5 rounded-lg text-xs font-medium text-amber-400 hover:bg-slate-800 transition">
              Submitted ({{ pendingCount() }})
            </button>
            <button (click)="filterStatus.set('Approved')" [class.bg-slate-800]="filterStatus() === 'Approved'" class="px-3 py-1.5 rounded-lg text-xs font-medium text-emerald-400 hover:bg-slate-800 transition">
              Approved ({{ approvedCount() }})
            </button>
          </div>

          <a routerLink="/organizations/margin" class="text-xs text-emerald-400 hover:underline flex items-center gap-1 font-medium">
            Inspect Deal True Margin Cockpit &rarr;
          </a>
        </div>

        <!-- Table of Timecards -->
        <div class="bg-slate-900/80 border border-slate-800 rounded-xl overflow-hidden shadow-xl">
          <table class="w-full text-left text-sm">
            <thead class="bg-slate-950/60 text-slate-400 text-xs uppercase tracking-wider border-b border-slate-800">
              <tr>
                <th class="px-6 py-4">Specialist / Operator</th>
                <th class="px-6 py-4">CRM Deal / Client</th>
                <th class="px-6 py-4">Task Milestone</th>
                <th class="px-6 py-4">Hours</th>
                <th class="px-6 py-4">Hourly Rate</th>
                <th class="px-6 py-4">Labor Cost</th>
                <th class="px-6 py-4">Status</th>
                <th class="px-6 py-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800/60">
              @for (entry of filteredEntries(); track entry.id) {
                <tr class="hover:bg-slate-800/30 transition">
                  <td class="px-6 py-4">
                    <div class="font-medium text-white">{{ entry.teamMemberName }}</div>
                    <div class="text-xs text-slate-500">{{ entry.description }}</div>
                  </td>
                  <td class="px-6 py-4 font-mono text-xs text-slate-300">{{ entry.dealTitle }}</td>
                  <td class="px-6 py-4 text-xs text-slate-400">{{ entry.taskTitle }}</td>
                  <td class="px-6 py-4 font-semibold text-white">{{ entry.hoursWorked }} hrs</td>
                  <td class="px-6 py-4 text-slate-400">\${{ entry.hourlyRate.toFixed(2) }}/hr</td>
                  <td class="px-6 py-4 font-semibold text-cyan-400">\${{ entry.totalLaborCost.toFixed(2) }}</td>
                  <td class="px-6 py-4">
                    <span [class]="getStatusClass(entry.status)">
                      {{ entry.status }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-right">
                    @if (entry.status === 'Submitted') {
                      <button (click)="approveTimecard(entry.id)" class="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-semibold transition shadow">
                        Approve & Roll Cost
                      </button>
                    } @else {
                      <span class="text-xs text-slate-500 font-mono">Approved</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </main>

      <!-- Log Timecard Modal -->
      @if (openLogModal()) {
        <div class="fixed inset-0 bg-slate-950/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between border-b border-slate-800 pb-3">
              <h3 class="text-base font-bold text-white">Log Work Hours & Labor Allocation</h3>
              <button (click)="openLogModal.set(false)" class="text-slate-400 hover:text-white">&times;</button>
            </div>

            <div class="space-y-3">
              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Operator / Team Member</label>
                <input [(ngModel)]="newOperator" type="text" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-emerald-500" placeholder="e.g. Sarah Jenkins">
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Associated CRM Deal</label>
                <input [(ngModel)]="newDeal" type="text" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-emerald-500" placeholder="e.g. Carrefour Hypermarket Wholesale Deal">
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Task Milestone</label>
                <input [(ngModel)]="newTask" type="text" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-emerald-500" placeholder="e.g. Catalog Ingestion & ERP Data Migration">
              </div>

              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Hours Worked</label>
                  <input [(ngModel)]="newHours" type="number" step="0.5" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-emerald-500">
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Hourly Rate (\$/hr)</label>
                  <input [(ngModel)]="newRate" type="number" step="5" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-emerald-500">
                </div>
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-400 mb-1">Work Description / Deliverable</label>
                <textarea [(ngModel)]="newDescription" rows="2" class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-white focus:outline-none focus:border-emerald-500" placeholder="Completed product catalog CSV schema mapping and validation"></textarea>
              </div>
            </div>

            <div class="flex items-center justify-end gap-3 pt-3 border-t border-slate-800">
              <button (click)="openLogModal.set(false)" class="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-lg text-sm font-medium transition">
                Cancel
              </button>
              <button (click)="submitTimecard()" class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg text-sm font-semibold transition">
                Save & Submit Timecard
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class TimesheetLaborCockpitComponent {
  private translationService = inject(TranslationService);

  openLogModal = signal<boolean>(false);
  filterStatus = signal<'ALL' | 'Submitted' | 'Approved'>('ALL');

  newOperator = 'Karim Bouazizi';
  newDeal = 'Carrefour Hypermarket Wholesale Deal';
  newTask = 'Phase 3: Catalog Ingestion & ERP Data Migration';
  newHours = 6.5;
  newRate = 35.00;
  newDescription = 'Migrated 4,200 SKU records from legacy CSV into SGrowing PIM catalog.';

  entries = signal<TimesheetEntry[]>([
    {
      id: 'ts-01',
      teamMemberId: 'tm-01',
      teamMemberName: 'Yasmine Mansour',
      taskTitle: 'Phase 1: Kickoff & Technical Architecture Scoping',
      dealTitle: 'Carrefour Hypermarket Wholesale Deal',
      description: 'Conducted kickoff technical audit and scoped multi-warehouse hubs.',
      hoursWorked: 8.0,
      hourlyRate: 45.00,
      totalLaborCost: 360.00,
      status: 'Approved',
      workDate: '2026-10-25',
      approvedBy: 'Operations Director'
    },
    {
      id: 'ts-02',
      teamMemberId: 'tm-02',
      teamMemberName: 'Amine Trabelsi',
      taskTitle: 'Phase 2: Multi-Tenant Workspace & Secrets Setup',
      dealTitle: 'Carrefour Hypermarket Wholesale Deal',
      description: 'Provisioned tenant DB shards and configured AES-256 scraper vault keys.',
      hoursWorked: 5.5,
      hourlyRate: 35.00,
      totalLaborCost: 192.50,
      status: 'Approved',
      workDate: '2026-10-26',
      approvedBy: 'Operations Director'
    },
    {
      id: 'ts-03',
      teamMemberId: 'tm-03',
      teamMemberName: 'Sarah Jenkins',
      taskTitle: 'Phase 3: Catalog Ingestion & ERP Data Migration',
      dealTitle: 'Monoprix Retail COD Fulfillment Deal',
      description: 'Validated barcode scanner workflows and safety stock thresholds.',
      hoursWorked: 12.0,
      hourlyRate: 30.00,
      totalLaborCost: 360.00,
      status: 'Submitted',
      workDate: '2026-10-27'
    },
    {
      id: 'ts-04',
      teamMemberId: 'tm-04',
      teamMemberName: 'Karim Bouazizi',
      taskTitle: 'Phase 4: WhatsApp Cutover & Multi-Channel Bots',
      dealTitle: 'Monoprix Retail COD Fulfillment Deal',
      description: 'Paired primary WhatsApp gateway number using 8-digit code protocol.',
      hoursWorked: 4.0,
      hourlyRate: 40.00,
      totalLaborCost: 160.00,
      status: 'Submitted',
      workDate: '2026-10-28'
    }
  ]);

  filteredEntries = computed(() => {
    const s = this.filterStatus();
    if (s === 'ALL') return this.entries();
    return this.entries().filter(e => e.status === s);
  });

  totalHours = computed(() => this.entries().reduce((acc, e) => acc + e.hoursWorked, 0));
  approvedHours = computed(() => this.entries().filter(e => e.status === 'Approved').reduce((acc, e) => acc + e.hoursWorked, 0));
  totalLaborCost = computed(() => this.entries().filter(e => e.status === 'Approved').reduce((acc, e) => acc + e.totalLaborCost, 0));
  pendingCount = computed(() => this.entries().filter(e => e.status === 'Submitted').length);
  approvedCount = computed(() => this.entries().filter(e => e.status === 'Approved').length);

  getStatusClass(status: string): string {
    switch (status) {
      case 'Approved':
        return 'inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20';
      case 'Submitted':
        return 'inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-amber-500/10 text-amber-400 border border-amber-500/20';
      default:
        return 'inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-rose-500/10 text-rose-400 border border-rose-500/20';
    }
  }

  approveTimecard(id: string) {
    this.entries.update(list =>
      list.map(e => e.id === id ? { ...e, status: 'Approved', approvedBy: 'Lead Operations Director' } : e)
    );
  }

  submitTimecard() {
    const hours = Number(this.newHours) || 1;
    const rate = Number(this.newRate) || 25;
    const cost = Math.round(hours * rate * 100) / 100;

    const newEntry: TimesheetEntry = {
      id: 'ts-' + Math.random().toString(36).substring(2, 7),
      teamMemberId: 'tm-custom',
      teamMemberName: this.newOperator,
      dealTitle: this.newDeal,
      taskTitle: this.newTask,
      description: this.newDescription,
      hoursWorked: hours,
      hourlyRate: rate,
      totalLaborCost: cost,
      status: 'Submitted',
      workDate: new Date().toISOString().split('T')[0]
    };

    this.entries.update(list => [newEntry, ...list]);
    this.openLogModal.set(false);
  }
}
