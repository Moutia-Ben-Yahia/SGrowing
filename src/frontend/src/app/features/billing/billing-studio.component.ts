import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SubscriptionDetailsDto, BillingInvoiceRecordDto, PlanTier } from '../../core/models/billing.models';

@Component({
  selector: 'app-billing-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Inbox
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              💳 SaaS Subscription Plans, Quotas & Stripe Portal
            </span>
            <span class="bg-indigo-500/20 text-indigo-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Section 11 • Multi-Tenant Entitlements
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="openStripePortal()"
              class="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-500 text-white rounded-lg text-xs font-bold shadow-lg shadow-indigo-950/20 transition">
              Stripe Customer Portal ↗
            </button>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- Active Plan Header -->
          <div class="p-6 rounded-2xl bg-gradient-to-r from-slate-900 via-indigo-950/30 to-slate-900 border border-slate-800 flex items-center justify-between">
            <div class="space-y-1">
              <span class="text-xs text-indigo-400 uppercase tracking-wider font-semibold font-mono">Current Plan</span>
              <div class="flex items-center gap-3">
                <h2 class="text-2xl font-bold text-white">
                  {{ sub()?.planTier === 1 ? 'Starter Tier' : sub()?.planTier === 3 ? 'Enterprise Tier' : 'Growth Tier' }}
                </h2>
                <span class="px-2 py-0.5 rounded-full text-xs font-bold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                  {{ sub()?.status || 'Active' }}
                </span>
              </div>
              <p class="text-xs text-slate-400">
                Renews on {{ (sub()?.billingCycleEndUtc | date:'mediumDate') || 'in 28 days' }}
              </p>
            </div>
            <div class="text-right">
              <span class="text-3xl font-extrabold text-white">\${{ sub()?.monthlyPrice || 249 }}</span>
              <span class="text-xs text-slate-400 block">per month billed</span>
            </div>
          </div>

          <!-- Usage Quota Meters -->
          <div class="space-y-3">
            <h3 class="text-sm font-semibold text-white">📊 Plan Quotas & Real-Time Usage</h3>
            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
              <!-- Quota 1: AI Tokens -->
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-3">
                <div class="flex justify-between text-xs">
                  <span class="text-slate-400">AI Copilot Tokens</span>
                  <span class="font-bold text-white font-mono">
                    {{ (sub()?.usageSummary?.usedAiTokens || 0) | number }} / {{ (sub()?.usageSummary?.quotaAiTokens || 5000000) | number }}
                  </span>
                </div>
                <div class="w-full bg-slate-950 rounded-full h-2 overflow-hidden border border-slate-800">
                  <div class="bg-indigo-500 h-2 rounded-full" [style.width.%]="sub()?.usageSummary?.tokenUsagePercent || 18"></div>
                </div>
                <span class="text-[10px] text-slate-400 block text-right font-mono">
                  {{ sub()?.usageSummary?.tokenUsagePercent || 18 }}% utilized
                </span>
              </div>

              <!-- Quota 2: Voice Dialing Minutes -->
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-3">
                <div class="flex justify-between text-xs">
                  <span class="text-slate-400">Voice Telephony Minutes</span>
                  <span class="font-bold text-white font-mono">
                    {{ (sub()?.usageSummary?.usedVoiceMinutes || 0) | number }} / {{ (sub()?.usageSummary?.quotaVoiceMinutes || 1000) | number }}
                  </span>
                </div>
                <div class="w-full bg-slate-950 rounded-full h-2 overflow-hidden border border-slate-800">
                  <div class="bg-emerald-500 h-2 rounded-full" [style.width.%]="sub()?.usageSummary?.voiceUsagePercent || 12"></div>
                </div>
                <span class="text-[10px] text-slate-400 block text-right font-mono">
                  {{ sub()?.usageSummary?.voiceUsagePercent || 12 }}% utilized
                </span>
              </div>

              <!-- Quota 3: Outbound Messages -->
              <div class="p-5 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-3">
                <div class="flex justify-between text-xs">
                  <span class="text-slate-400">WhatsApp & Email Outbound</span>
                  <span class="font-bold text-white font-mono">
                    {{ (sub()?.usageSummary?.usedMessages || 0) | number }} / {{ (sub()?.usageSummary?.quotaMessages || 10000) | number }}
                  </span>
                </div>
                <div class="w-full bg-slate-950 rounded-full h-2 overflow-hidden border border-slate-800">
                  <div class="bg-amber-500 h-2 rounded-full" [style.width.%]="sub()?.usageSummary?.messageUsagePercent || 25"></div>
                </div>
                <span class="text-[10px] text-slate-400 block text-right font-mono">
                  {{ sub()?.usageSummary?.messageUsagePercent || 25 }}% utilized
                </span>
              </div>
            </div>
          </div>

          <!-- Tier Comparison & 1-Click Upgrade Options -->
          <div class="space-y-3">
            <h3 class="text-sm font-semibold text-white">🚀 Upgrade Options & Tier Comparison</h3>
            <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
              <!-- Tier 1: Starter -->
              <div class="p-6 rounded-2xl bg-slate-900/40 border border-slate-800 space-y-4">
                <div class="flex justify-between items-start">
                  <div>
                    <h4 class="text-base font-bold text-white">Starter</h4>
                    <p class="text-xs text-slate-400">For boutique merchants</p>
                  </div>
                  <span class="text-lg font-bold text-white">\$99<span class="text-xs text-slate-400 font-normal">/mo</span></span>
                </div>
                <ul class="text-xs text-slate-300 space-y-2">
                  <li>✓ Up to 3 Team Seats</li>
                  <li>✓ 500,000 AI Tokens</li>
                  <li>✓ 1,000 Outbound Messages</li>
                  <li>✓ Standard COD Fraud Scoring</li>
                </ul>
                <button
                  (click)="upgrade(1)"
                  [disabled]="sub()?.planTier === 1"
                  class="w-full py-2 bg-slate-800 hover:bg-slate-700 text-xs font-semibold rounded-lg text-slate-200 transition disabled:opacity-40">
                  {{ sub()?.planTier === 1 ? 'Current Plan' : 'Downgrade to Starter' }}
                </button>
              </div>

              <!-- Tier 2: Growth -->
              <div class="p-6 rounded-2xl bg-slate-900/80 border border-indigo-500/50 space-y-4 shadow-xl shadow-indigo-950/20 relative">
                <span class="absolute -top-2.5 right-6 px-2 py-0.5 rounded-full text-[9px] font-bold bg-indigo-500 text-white uppercase tracking-wider">
                  Popular
                </span>
                <div class="flex justify-between items-start">
                  <div>
                    <h4 class="text-base font-bold text-white">Growth</h4>
                    <p class="text-xs text-slate-400">For scaling regional brands</p>
                  </div>
                  <span class="text-lg font-bold text-white">\$249<span class="text-xs text-slate-400 font-normal">/mo</span></span>
                </div>
                <ul class="text-xs text-slate-300 space-y-2">
                  <li>✓ Up to 10 Team Seats</li>
                  <li>✓ 5,000,000 AI Tokens</li>
                  <li>✓ 10,000 Outbound Messages</li>
                  <li>✓ Defensive Multi-State WMS</li>
                  <li>✓ Doorstep Drop-and-Swap</li>
                </ul>
                <button
                  (click)="upgrade(2)"
                  [disabled]="sub()?.planTier === 2"
                  class="w-full py-2 bg-indigo-600 hover:bg-indigo-500 text-xs font-bold rounded-lg text-white transition disabled:opacity-40">
                  {{ sub()?.planTier === 2 ? 'Current Plan' : 'Select Growth' }}
                </button>
              </div>

              <!-- Tier 3: Enterprise -->
              <div class="p-6 rounded-2xl bg-slate-900/40 border border-slate-800 space-y-4">
                <div class="flex justify-between items-start">
                  <div>
                    <h4 class="text-base font-bold text-white">Enterprise</h4>
                    <p class="text-xs text-slate-400">For high-velocity operations</p>
                  </div>
                  <span class="text-lg font-bold text-white">\$799<span class="text-xs text-slate-400 font-normal">/mo</span></span>
                </div>
                <ul class="text-xs text-slate-300 space-y-2">
                  <li>✓ 100 Team Seats</li>
                  <li>✓ 50,000,000 AI Tokens</li>
                  <li>✓ 100,000 Messages + Unlimited Overage</li>
                  <li>✓ Dedicated Multi-Hub WMS Routing</li>
                  <li>✓ 99.9% Uptime SLA & Custom DDL</li>
                </ul>
                <button
                  (click)="upgrade(3)"
                  [disabled]="sub()?.planTier === 3"
                  class="w-full py-2 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-xs font-bold rounded-lg text-white transition disabled:opacity-40">
                  {{ sub()?.planTier === 3 ? 'Current Plan' : '🚀 Upgrade to Enterprise' }}
                </button>
              </div>
            </div>
          </div>

          <!-- Invoices Table -->
          <div class="rounded-2xl border border-slate-800 bg-slate-900/40 overflow-hidden">
            <div class="px-5 py-3 border-b border-slate-800 bg-slate-900/80 flex items-center justify-between">
              <h3 class="text-sm font-semibold text-white">🧾 Payment History & Invoices</h3>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300">
                <thead class="bg-slate-950/70 text-[11px] uppercase tracking-wider text-slate-400 border-b border-slate-800">
                  <tr>
                    <th class="px-4 py-3">Invoice Number</th>
                    <th class="px-4 py-3">Date</th>
                    <th class="px-4 py-3">Amount</th>
                    <th class="px-4 py-3">Status</th>
                    <th class="px-4 py-3">Action</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @for (inv of invoices(); track inv.id) {
                    <tr class="hover:bg-slate-800/30 transition">
                      <td class="px-4 py-3 font-semibold text-white">{{ inv.invoiceNumber }}</td>
                      <td class="px-4 py-3 text-slate-400">{{ inv.createdAtUtc | date:'mediumDate' }}</td>
                      <td class="px-4 py-3 text-emerald-400 font-bold">\${{ inv.amountDue | number:'1.2-2' }}</td>
                      <td class="px-4 py-3">
                        <span class="px-2 py-0.5 rounded text-[10px] bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                          {{ inv.status }}
                        </span>
                      </td>
                      <td class="px-4 py-3 font-sans">
                        <span class="text-xs text-indigo-400 hover:text-indigo-300 underline cursor-pointer">
                          Download PDF ↗
                        </span>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </main>
    </div>
  `
})
export class BillingStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public sub = signal<SubscriptionDetailsDto | null>(null);
  public invoices = signal<BillingInvoiceRecordDto[]>([]);

  public get tenantId(): string {
    return this.auth.currentTenant()?.id || '00000000-0000-0000-0000-000000000001';
  }

  ngOnInit(): void {
    this.loadData();
  }

  public loadData(): void {
    this.api.getSubscriptionDetails(this.tenantId).subscribe(s => this.sub.set(s));
    this.api.getInvoices(this.tenantId).subscribe(invs => {
      if (invs && invs.length > 0) {
        this.invoices.set(invs);
      } else {
        // Seed default initial invoice
        this.upgrade(2);
      }
    });
  }

  public upgrade(tier: PlanTier): void {
    this.api.upgradePlan({
      tenantId: this.tenantId,
      targetTier: tier
    }).subscribe(() => {
      this.api.getSubscriptionDetails(this.tenantId).subscribe(s => this.sub.set(s));
      this.api.getInvoices(this.tenantId).subscribe(invs => this.invoices.set(invs));
    });
  }

  public openStripePortal(): void {
    this.api.createCheckoutSession({
      tenantId: this.tenantId,
      targetTier: 3,
      successUrl: window.location.href
    }).subscribe(res => {
      alert(`Stripe Checkout Session initialized:\n${res.checkoutUrl}`);
    });
  }
}
