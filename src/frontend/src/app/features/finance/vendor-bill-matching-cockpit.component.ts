import { Component, ChangeDetectionStrategy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface VendorBillRecord {
  billId: string;
  vendorName: string;
  poNumber: string;
  grnNumber: string;
  invoiceAmount: number;
  poAmount: number;
  earlyDiscountAmount: number;
  discountRatePct: number;
  discountWindowDays: number;
  status: 'PendingOcr' | 'Matched3Way' | 'PriceDiscrepancy' | 'QuantityDiscrepancy' | 'ApprovedEarlyDiscount';
  receivedAt: string;
  discountExpiryDate: string;
  matchAuditReceipt?: string;
}

@Component({
  selector: 'app-vendor-bill-matching-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="p-6 bg-slate-900 text-slate-100 min-h-screen" dir="ltr" data-testid="vendor-bill-matching-cockpit">
      <header class="mb-6 flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-4">
        <div>
          <div class="flex items-center gap-3">
            <span class="px-2.5 py-1 text-xs font-semibold rounded bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">Horizon 104</span>
            <span class="text-xs text-slate-400">Accounts Payable (AP) & Early-Payment Discount Arbitrage</span>
          </div>
          <h1 class="text-2xl font-bold tracking-tight text-white mt-1">Autonomous Vendor Bill OCR Ingestion & 3-Way Reconciliation</h1>
          <p class="text-sm text-slate-400">Zero-touch OCR line-item matching (Invoice vs PO vs GRN) and 2/10 Net 30 early payment discount escrow release.</p>
        </div>
      </header>

      <!-- KPI Summary -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-pending-bills">
          <div class="text-xs font-medium text-slate-400 uppercase">Pending Bills Queue</div>
          <div class="text-2xl font-bold text-amber-400 mt-1">{{ pendingBillsCount() }} Bills</div>
          <div class="text-xs text-slate-400 mt-1">Awaiting 3-way reconciliation audit</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-total-volume">
          <div class="text-xs font-medium text-slate-400 uppercase">Total Bill Volume</div>
          <div class="text-2xl font-bold text-white mt-1">\${{ totalBillVolume() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">Across all enterprise vendors</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-captured-discounts">
          <div class="text-xs font-medium text-slate-400 uppercase">Early Discounts Captured</div>
          <div class="text-2xl font-bold text-emerald-400 mt-1">\${{ capturedDiscounts() | number:'1.0-0' }}</div>
          <div class="text-xs text-slate-400 mt-1">2/10 Net 30 treasury arbitrage savings</div>
        </div>
        <div class="bg-slate-800/80 border border-slate-700/60 p-4 rounded-xl" data-testid="kpi-match-rate">
          <div class="text-xs font-medium text-slate-400 uppercase">3-Way Match Rate</div>
          <div class="text-2xl font-bold text-cyan-400 mt-1">{{ matchRatePct() | number:'1.1-1' }}%</div>
          <div class="text-xs text-slate-400 mt-1">Zero-variance automated reconciliations</div>
        </div>
      </section>

      <!-- Vendor Bills Table -->
      <section class="bg-slate-800/70 border border-slate-700/60 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-4">
          <h2 class="text-lg font-semibold text-white">Vendor Invoices & 3-Way Match Audit Queue</h2>
          <span class="text-xs text-slate-400">Total: {{ bills().length }} Invoices</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-sm" data-testid="bills-table">
            <thead class="text-xs uppercase bg-slate-800 text-slate-400 border-b border-slate-700">
              <tr>
                <th class="py-3 px-4">Bill ID / Vendor</th>
                <th class="py-3 px-4">PO & GRN Ref</th>
                <th class="py-3 px-4">Invoice / PO Amount</th>
                <th class="py-3 px-4">Early Discount</th>
                <th class="py-3 px-4">3-Way Match Status</th>
                <th class="py-3 px-4">Discount Expiry</th>
                <th class="py-3 px-4 text-right">Action</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-750 text-slate-300">
              @for (b of bills(); track b.billId) {
                <tr class="hover:bg-slate-800/50 transition-colors" [attr.data-testid]="'bill-row-' + b.billId">
                  <td class="py-3 px-4">
                    <div class="font-medium text-white">{{ b.vendorName }}</div>
                    <div class="text-xs text-slate-400 font-mono">{{ b.billId }}</div>
                  </td>
                  <td class="py-3 px-4 text-xs font-mono">
                    <div class="text-cyan-400">{{ b.poNumber }}</div>
                    <div class="text-slate-400">{{ b.grnNumber }}</div>
                  </td>
                  <td class="py-3 px-4">
                    <div class="font-semibold text-white">\${{ b.invoiceAmount | number:'1.0-0' }}</div>
                    <div class="text-xs text-slate-400">PO: \${{ b.poAmount | number:'1.0-0' }}</div>
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-bold bg-emerald-500/20 text-emerald-300">
                      -\${{ b.earlyDiscountAmount | number:'1.0-0' }} ({{ b.discountRatePct }}%)
                    </span>
                  </td>
                  <td class="py-3 px-4">
                    <span class="px-2 py-0.5 rounded text-xs font-medium"
                          [ngClass]="{
                            'bg-emerald-500/20 text-emerald-400': b.status === 'ApprovedEarlyDiscount' || b.status === 'Matched3Way',
                            'bg-rose-500/20 text-rose-400': b.status === 'PriceDiscrepancy' || b.status === 'QuantityDiscrepancy',
                            'bg-amber-500/20 text-amber-400': b.status === 'PendingOcr'
                          }">
                      {{ b.status }}
                    </span>
                  </td>
                  <td class="py-3 px-4 text-xs font-mono text-slate-400">
                    {{ b.discountWindowDays }} days window
                  </td>
                  <td class="py-3 px-4 text-right">
                    @if (b.status === 'Matched3Way') {
                      <button class="px-3 py-1 text-xs font-semibold rounded bg-emerald-600 hover:bg-emerald-500 text-white transition-colors"
                              (click)="approveDiscount(b.billId)"
                              [attr.data-testid]="'btn-approve-' + b.billId">
                        Capture Discount
                      </button>
                    } @else if (b.status === 'PriceDiscrepancy') {
                      <span class="text-xs text-rose-400 font-semibold">Flagged Exception</span>
                    } @else {
                      <span class="text-xs text-emerald-400 font-mono">Captured</span>
                    }
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>

      <!-- Action Toast -->
      @if (lastBillNotice()) {
        <div class="p-3 bg-emerald-950/60 border border-emerald-500/40 rounded-xl text-emerald-300 text-xs flex items-center justify-between"
             data-testid="bill-notice">
          <span>{{ lastBillNotice() }}</span>
          <button (click)="lastBillNotice.set(null)" class="text-slate-400 hover:text-white">&times;</button>
        </div>
      }
    </div>
  `
})
export class VendorBillMatchingCockpitComponent {
  readonly bills = signal<VendorBillRecord[]>([
    {
      billId: 'BILL-2026-4401',
      vendorName: 'Amazon Web Services Inc',
      poNumber: 'PO-INFRA-8801',
      grnNumber: 'GRN-CLOUD-9912',
      invoiceAmount: 145000,
      poAmount: 145000,
      earlyDiscountAmount: 2900,
      discountRatePct: 2.0,
      discountWindowDays: 10,
      status: 'Matched3Way',
      receivedAt: '2026-11-01T00:00:00Z',
      discountExpiryDate: '2026-11-11T00:00:00Z',
      matchAuditReceipt: '3WAY-VERIFIED-PO8801'
    },
    {
      billId: 'BILL-2026-4402',
      vendorName: 'Snowflake Computing Inc',
      poNumber: 'PO-DATA-8802',
      grnNumber: 'GRN-DATA-9915',
      invoiceAmount: 82000,
      poAmount: 82000,
      earlyDiscountAmount: 1640,
      discountRatePct: 2.0,
      discountWindowDays: 10,
      status: 'Matched3Way',
      receivedAt: '2026-10-30T00:00:00Z',
      discountExpiryDate: '2026-11-09T00:00:00Z',
      matchAuditReceipt: '3WAY-VERIFIED-PO8802'
    },
    {
      billId: 'BILL-2026-4403',
      vendorName: 'Twilio Telecommunications',
      poNumber: 'PO-VOX-8803',
      grnNumber: 'GRN-VOX-9918',
      invoiceAmount: 64500,
      poAmount: 61000,
      earlyDiscountAmount: 1220,
      discountRatePct: 2.0,
      discountWindowDays: 10,
      status: 'PriceDiscrepancy',
      receivedAt: '2026-10-28T00:00:00Z',
      discountExpiryDate: '2026-11-07T00:00:00Z'
    },
    {
      billId: 'BILL-2026-4400',
      vendorName: 'Stripe Payments Platform',
      poNumber: 'PO-FIN-8800',
      grnNumber: 'GRN-FIN-9910',
      invoiceAmount: 210000,
      poAmount: 210000,
      earlyDiscountAmount: 4200,
      discountRatePct: 2.0,
      discountWindowDays: 10,
      status: 'ApprovedEarlyDiscount',
      receivedAt: '2026-10-22T00:00:00Z',
      discountExpiryDate: '2026-11-01T00:00:00Z',
      matchAuditReceipt: '3WAY-VERIFIED-PO8800-SETTLED'
    }
  ]);

  readonly lastBillNotice = signal<string | null>(null);

  readonly pendingBillsCount = computed(() =>
    this.bills().filter(b => b.status === 'Matched3Way' || b.status === 'PriceDiscrepancy').length
  );

  readonly totalBillVolume = computed(() =>
    this.bills().reduce((sum, b) => sum + b.invoiceAmount, 0)
  );

  readonly capturedDiscounts = computed(() =>
    this.bills().filter(b => b.status === 'ApprovedEarlyDiscount').reduce((sum, b) => sum + b.earlyDiscountAmount, 0)
  );

  readonly matchRatePct = computed(() => {
    const list = this.bills();
    const perfect = list.filter(b => b.status === 'Matched3Way' || b.status === 'ApprovedEarlyDiscount').length;
    return list.length > 0 ? (perfect / list.length) * 100 : 100;
  });

  approveDiscount(billId: string): void {
    const bill = this.bills().find(b => b.billId === billId);
    if (!bill) return;

    this.bills.update(list =>
      list.map(b => b.billId === billId ? {
        ...b,
        status: 'ApprovedEarlyDiscount'
      } : b)
    );

    this.lastBillNotice.set(`Captured \$${bill.earlyDiscountAmount.toLocaleString()} early discount on ${bill.vendorName} invoice (${billId}).`);
  }
}
