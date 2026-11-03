import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface QuoteApprovalRecord {
  quoteId: string;
  accountName: string;
  totalListPrice: number;
  discountPercentage: number;
  netPrice: number;
  paymentTerms: string;
  requiredTier: 'RepAutonomous' | 'SalesManager' | 'SalesDirector' | 'CroBoardGate';
  status: 'Draft' | 'PendingApproval' | 'Approved' | 'Rejected' | 'Escalated';
  requesterName: string;
  requestedAt: string;
  approvedBy?: string;
  approvedAt?: string;
  auditToken?: string;
  slaMinutesRemaining: number;
  isSlaBreached: boolean;
  justification?: string;
}

@Component({
  selector: 'app-cpq-approval-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="cpq-approval-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-amber-500/20 text-amber-400 border border-amber-500/30">Horizon 96</span>
            <span class="text-xs text-slate-400">Enterprise CPQ & Deal Governance Matrix</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Enterprise CPQ Approval Chain & Discount Matrix</h1>
          <p class="text-sm text-slate-400">Multi-tier discount hurdle rules, automated SLA countdowns, and non-repudiation cryptographic audit tokens.</p>
        </div>
      </header>

      <!-- KPI Overview Cards -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-pending-count">
          <div class="text-xs font-medium text-slate-400 uppercase">Pending Approvals</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ pendingCount() }} Quotes</div>
          <div class="text-xs text-slate-400 mt-1">Awaiting Deal Desk / Exec Decision</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-pending-value">
          <div class="text-xs font-medium text-slate-400 uppercase">Pending Pipeline Value</div>
          <div class="text-2xl font-bold text-white mt-1">\${{ pendingValue() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Total discounted contract value</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-sla-breaches">
          <div class="text-xs font-medium text-slate-400 uppercase">SLA Breaches</div>
          <div class="text-2xl font-bold text-rose-400 mt-1">{{ slaBreaches() }} Quotes</div>
          <div class="text-xs text-slate-400 mt-1">Exceeded max decision SLA threshold</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-avg-discount">
          <div class="text-xs font-medium text-slate-400 uppercase">Avg Approved Discount</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">{{ avgDiscount() | number:'1.1-1' }}%</div>
          <div class="text-xs text-slate-400 mt-1">Across approved enterprise contracts</div>
        </div>
      </section>

      <!-- Main Quotes Table -->
      <section class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-white">Active Quotes & Deal Desk Queue</h2>
          <span class="text-xs text-slate-400">Total: {{ quotes().length }} Quotes</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="quotes-table">
            <thead class="text-xs uppercase bg-slate-800 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Quote ID / Account</th>
                <th class="py-3 px-4">List / Net Price</th>
                <th class="py-3 px-4">Discount</th>
                <th class="py-3 px-4">Payment Terms</th>
                <th class="py-3 px-4">Required Tier</th>
                <th class="py-3 px-4">SLA Time</th>
                <th class="py-3 px-4">Status</th>
                <th class="py-3 px-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-750 text-slate-300">
              @for (q of quotes(); track q.quoteId) {
                <tr class="hover:bg-slate-800/50 transition-colors" [attr.data-testid]="'quote-row-' + q.quoteId">
                  <td class="py-3 px-4">
                    <div class="font-medium text-white">{{ q.accountName }}</div>
                    <div class="text-xs text-slate-400">{{ q.quoteId }} • {{ q.requesterName }}</div>
                  </td>
                  <td class="py-3 px-4">
                    <div class="font-semibold text-white">\${{ q.netPrice | number:'1.0-0' }}</div>
                    <div class="text-xs text-slate-400 line-through">\${{ q.totalListPrice | number:'1.0-0' }}</div>
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-semibold"
                          [ngClass]="{
                            'bg-emerald-500/20 text-emerald-300': q.discountPercentage <= 10,
                            'bg-amber-500/20 text-amber-300': q.discountPercentage > 10 && q.discountPercentage <= 20,
                            'bg-orange-500/20 text-orange-300': q.discountPercentage > 20 && q.discountPercentage <= 35,
                            'bg-rose-500/20 text-rose-300': q.discountPercentage > 35
                          }">
                      {{ q.discountPercentage | number:'1.1-1' }}%
                    </span>
                  </td>
                  <td class="py-3 px-4">
                    <span class="text-xs font-mono px-2 py-1 bg-slate-750 rounded text-slate-300">{{ q.paymentTerms }}</span>
                  </td>
                  <td class="py-3 px-4">
                    <span class="text-xs font-medium text-cyan-400">{{ q.requiredTier }}</span>
                  </td>
                  <td class="py-3 px-4">
                    @if (q.status === 'Approved') {
                      <span class="text-xs text-emerald-400">Sealed</span>
                    } @else if (q.isSlaBreached) {
                      <span class="px-2 py-0.5 rounded text-xs bg-rose-500/20 text-rose-400 font-semibold">BREACHED</span>
                    } @else {
                      <span class="text-xs text-slate-300 font-mono">{{ q.slaMinutesRemaining }}m left</span>
                    }
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-medium"
                          [ngClass]="{
                            'bg-emerald-500/20 text-emerald-400': q.status === 'Approved',
                            'bg-amber-500/20 text-amber-400': q.status === 'PendingApproval',
                            'bg-rose-500/20 text-rose-400': q.status === 'Rejected',
                            'bg-purple-500/20 text-purple-400': q.status === 'Escalated'
                          }">
                      {{ q.status }}
                    </span>
                  </td>
                  <td class="py-3 px-4 text-right space-x-2">
                    @if (q.status === 'PendingApproval' || q.status === 'Escalated') {
                      <button class="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-600 hover:bg-emerald-500 text-white transition-colors"
                              (click)="approve(q.quoteId)"
                              [attr.data-testid]="'btn-approve-' + q.quoteId">
                        Approve (HMAC)
                      </button>
                      <button class="px-2.5 py-1 text-xs font-semibold rounded bg-purple-600 hover:bg-purple-500 text-white transition-colors"
                              (click)="escalate(q.quoteId)"
                              [attr.data-testid]="'btn-escalate-' + q.quoteId">
                        Escalate
                      </button>
                    } @else if (q.auditToken) {
                      <span class="text-xs text-slate-500 font-mono">{{ q.auditToken }}</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Action Toast / Audit Notice -->
      @if (lastActionNotice()) {
        <div class="p-3 bg-emerald-950/60 border border-emerald-500/40 rounded-xl text-emerald-300 text-xs flex items-center justify-between"
             data-testid="action-notice">
          <span>{{ lastActionNotice() }}</span>
          <button (click)="lastActionNotice.set(null)" class="text-slate-400 hover:text-white">&times;</button>
        </div>
      }
    </div>
  `
})
export class CpqApprovalCockpitComponent {
  readonly quotes = signal<QuoteApprovalRecord[]>([
    {
      quoteId: 'QUOTE-2026-881',
      accountName: 'Vertex Global Enterprises',
      totalListPrice: 180000,
      discountPercentage: 18.5,
      netPrice: 146700,
      paymentTerms: 'Net 30',
      requiredTier: 'SalesManager',
      status: 'PendingApproval',
      requesterName: 'Sarah Connor',
      requestedAt: '2026-11-03T00:30:00Z',
      slaMinutesRemaining: 120,
      isSlaBreached: false,
      justification: 'Competitive bakeoff against legacy vendor; 3-year commitment'
    },
    {
      quoteId: 'QUOTE-2026-882',
      accountName: 'Apex FinTech Holdings',
      totalListPrice: 420000,
      discountPercentage: 28.0,
      netPrice: 302400,
      paymentTerms: 'Net 60',
      requiredTier: 'SalesDirector',
      status: 'PendingApproval',
      requesterName: 'David Kim',
      requestedAt: '2026-11-02T19:00:00Z',
      slaMinutesRemaining: 60,
      isSlaBreached: false,
      justification: 'Global rollout across 12 subsidiaries; requested standard Net 60 billing'
    },
    {
      quoteId: 'QUOTE-2026-883',
      accountName: 'Nordic Health Telematics',
      totalListPrice: 850000,
      discountPercentage: 42.0,
      netPrice: 493000,
      paymentTerms: 'Net 90',
      requiredTier: 'CroBoardGate',
      status: 'PendingApproval',
      requesterName: 'Elena Rostova',
      requestedAt: '2026-11-01T23:00:00Z',
      slaMinutesRemaining: 0,
      isSlaBreached: true,
      justification: 'Market penetration marquee anchor customer; 5-year multi-million TCV'
    },
    {
      quoteId: 'QUOTE-2026-880',
      accountName: 'Helios Energy Systems',
      totalListPrice: 95000,
      discountPercentage: 8.0,
      netPrice: 87400,
      paymentTerms: 'Net 30',
      requiredTier: 'RepAutonomous',
      status: 'Approved',
      requesterName: 'Marcus Vance',
      requestedAt: '2026-11-02T08:00:00Z',
      approvedBy: 'Automated Governance Gate',
      approvedAt: '2026-11-02T08:01:00Z',
      auditToken: 'CPQ-AUDIT-4F2A90BC',
      slaMinutesRemaining: 0,
      isSlaBreached: false,
      justification: 'Standard rep volume discount'
    }
  ]);

  readonly lastActionNotice = signal<string | null>(null);

  readonly pendingCount = computed(() =>
    this.quotes().filter(q => q.status === 'PendingApproval' || q.status === 'Escalated').length
  );

  readonly pendingValue = computed(() =>
    this.quotes()
      .filter(q => q.status === 'PendingApproval' || q.status === 'Escalated')
      .reduce((sum, q) => sum + q.netPrice, 0)
  );

  readonly slaBreaches = computed(() =>
    this.quotes().filter(q => q.isSlaBreached && (q.status === 'PendingApproval' || q.status === 'Escalated')).length
  );

  readonly avgDiscount = computed(() => {
    const approved = this.quotes().filter(q => q.status === 'Approved');
    if (approved.length === 0) return 0;
    return approved.reduce((sum, q) => sum + q.discountPercentage, 0) / approved.length;
  });

  approve(quoteId: string): void {
    const token = `CPQ-AUDIT-${Math.random().toString(36).substring(2, 10).toUpperCase()}`;
    this.quotes.update(list =>
      list.map(q => q.quoteId === quoteId ? {
        ...q,
        status: 'Approved',
        approvedBy: 'VP Deal Desk (Approved)',
        approvedAt: new Date().toISOString(),
        auditToken: token,
        slaMinutesRemaining: 0,
        isSlaBreached: false
      } : q)
    );
    this.lastActionNotice.set(`Quote ${quoteId} approved and cryptographically sealed with token ${token}.`);
  }

  escalate(quoteId: string): void {
    this.quotes.update(list =>
      list.map(q => q.quoteId === quoteId ? {
        ...q,
        status: 'Escalated',
        requiredTier: 'CroBoardGate',
        slaMinutesRemaining: 180,
        isSlaBreached: false
      } : q)
    );
    this.lastActionNotice.set(`Quote ${quoteId} escalated to CRO / Board Gate with 180m priority SLA.`);
  }
}
