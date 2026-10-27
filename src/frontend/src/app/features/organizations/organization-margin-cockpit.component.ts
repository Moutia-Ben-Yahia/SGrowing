import { Component, signal, computed, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslationService } from '../../core/services/translation.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

export interface OrgNode {
  id: string;
  name: string;
  type: 'Tenant' | 'Organization' | 'Branch' | 'Team' | 'TeamMember';
  headcount: number;
  totalRevenue: number;
  totalMargin: number;
  expanded?: boolean;
  meta?: string;
  children?: OrgNode[];
}

export interface DealMarginItem {
  id: string;
  dealTitle: string;
  organizationName: string;
  revenue: number;
  cogs: number;
  courierFees: number;
  laborHours: number;
  hourlyRate: number;
  laborCost: number;
  trueGrossMargin: number;
  marginPercentage: number;
  calculatedAt: string;
}

@Component({
  selector: 'app-organization-margin-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LanguageSwitcherComponent],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans" dir="ltr">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Top Navigation Header -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/80 backdrop-blur-md px-6 flex items-center justify-between shrink-0 sticky top-0 z-20">
          <div class="flex items-center gap-3">
            <a routerLink="/" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← {{ translation.t('nav.inbox') }}
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-bold text-white flex items-center gap-2">
              🏢 Multi-Organization Hierarchy & Margin Tracking
            </span>
            <span class="bg-indigo-500/20 text-indigo-300 text-[10px] font-mono px-2 py-0.5 rounded-full border border-indigo-500/30">
              Inspired by Ever Gauzy • True Gross Margin
            </span>
          </div>

          <div class="flex items-center gap-3">
            <app-language-switcher></app-language-switcher>
            <button
              (click)="recalculateDealMargin()"
              data-testid="recalc-margin-btn"
              class="px-3 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold flex items-center gap-1.5 transition shadow-sm">
              ⚡ Compute True Gross Margin
            </button>
          </div>
        </header>

        <!-- Notification Toast -->
        @if (toastMessage()) {
          <div class="mx-6 mt-4 px-4 py-2 bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 text-xs rounded-lg flex items-center justify-between">
            <span>{{ toastMessage() }}</span>
            <button (click)="toastMessage.set('')" class="text-emerald-400 hover:text-white">✕</button>
          </div>
        }

        <div class="p-6 space-y-6">
          <!-- 1. Executive Rollup Metrics -->
          <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <div class="text-[11px] font-mono text-slate-400 uppercase tracking-wider mb-1">Consolidated Revenue</div>
              <div class="text-xl font-bold font-mono text-white" data-testid="consolidated-revenue">&dollar;{{ consolidatedRevenue().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}</div>
              <div class="text-[10px] text-emerald-400 mt-1">Across {{ dealLedger().length }} Recorded Deals</div>
            </div>

            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <div class="text-[11px] font-mono text-slate-400 uppercase tracking-wider mb-1">Cost Base (COGS + Ship + Labor)</div>
              <div class="text-xl font-bold font-mono text-amber-400" data-testid="consolidated-cost-base">&dollar;{{ consolidatedCostBase().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}</div>
              <div class="text-[10px] text-slate-400 mt-1">Direct COGS, Courier & Labor Base</div>
            </div>

            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <div class="text-[11px] font-mono text-slate-400 uppercase tracking-wider mb-1">True Gross Margin ($)</div>
              <div class="text-xl font-bold font-mono text-emerald-400" data-testid="consolidated-true-margin">&dollar;{{ consolidatedTrueMargin().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}</div>
              <div class="text-[10px] text-emerald-400 mt-1">Net of operator labor hours</div>
            </div>

            <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
              <div class="text-[11px] font-mono text-slate-400 uppercase tracking-wider mb-1">Gross Margin Percentage</div>
              <div class="text-xl font-bold font-mono text-indigo-300" data-testid="consolidated-margin-pct">{{ consolidatedMarginPct().toFixed(2) }}%</div>
              <div class="text-[10px] text-emerald-400 mt-1">✓ Healthy Unit Economics (>40%)</div>
            </div>
          </div>

          <!-- 2. Two-Column Layout: Organization Tree Hierarchy vs True Margin Calculator -->
          <div class="grid grid-cols-1 lg:grid-cols-12 gap-6">
            <!-- Organization Tree Hierarchy (Tenant -> Org -> Branch -> Team) -->
            <div class="lg:col-span-6 bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm">
              <div class="flex items-center justify-between pb-3 mb-4 border-b border-slate-800/80">
                <div class="flex items-center gap-2">
                  <span class="text-lg">🌳</span>
                  <h3 class="text-sm font-bold text-white">
                    Multi-Tenant Organization Tree (Tenant → Org → Branch → Team)
                  </h3>
                </div>
                <span class="text-xs text-slate-500 font-mono">{{ totalHeadcount() }} Members</span>
              </div>

              <!-- Tree Hierarchy Accordion Nodes -->
              <div class="space-y-2.5 font-mono text-xs" data-testid="org-hierarchy-tree">
                <!-- Root Tenant -->
                <div class="border border-indigo-500/30 bg-indigo-950/20 rounded-lg p-3">
                  <div class="flex items-center justify-between font-bold text-indigo-200">
                    <span class="flex items-center gap-2">
                      <span>🏛️</span>
                      <span>{{ treeData().name }}</span>
                    </span>
                    <span class="text-[10px] px-2 py-0.5 rounded bg-indigo-500/20 text-indigo-300 border border-indigo-500/30">
                      Tenant Root
                    </span>
                  </div>

                  <!-- Organizations -->
                  <div class="mt-3 pl-4 space-y-2.5 border-l-2 border-indigo-500/20">
                    @for (org of treeData().children; track org.id) {
                      <div class="border border-slate-800 bg-slate-900/80 rounded-lg p-2.5">
                        <div class="flex items-center justify-between text-slate-200 font-semibold">
                          <span class="flex items-center gap-2">
                            <span>🏢</span>
                            <span>{{ org.name }}</span>
                          </span>
                          <span class="text-[10px] px-1.5 py-0.5 rounded bg-slate-800 text-slate-400">
                            {{ org.headcount }} headcount
                          </span>
                        </div>

                        <!-- Branches -->
                        <div class="mt-2 pl-3 space-y-2 border-l border-slate-800">
                          @for (branch of org.children; track branch.id) {
                            <div class="bg-slate-950/70 border border-slate-800/80 rounded p-2">
                              <div class="flex items-center justify-between text-slate-300">
                                <span class="flex items-center gap-1.5 text-[11px] font-bold">
                                  <span>📍</span>
                                  <span>{{ branch.name }}</span>
                                </span>
                                <span class="text-[10px] text-slate-500">{{ branch.headcount }} members</span>
                              </div>

                              <!-- Teams -->
                              <div class="mt-2 pl-3 space-y-1.5 border-l border-slate-900">
                                @for (team of branch.children; track team.id) {
                                  <div class="text-[11px] bg-slate-900/50 p-1.5 rounded">
                                    <div class="flex items-center justify-between text-slate-300 font-semibold">
                                      <span class="flex items-center gap-1.5">
                                        <span>👥</span>
                                        <span>{{ team.name }}</span>
                                      </span>
                                      <span class="text-[10px] text-indigo-400">{{ team.children?.length || 0 }} members</span>
                                    </div>

                                    <!-- Team Members -->
                                    <div class="mt-1 pl-3 space-y-1 text-[10px] text-slate-400">
                                      @for (member of team.children; track member.id) {
                                        <div class="flex items-center justify-between">
                                          <span>• {{ member.name }}</span>
                                          <span class="text-slate-500">{{ member.meta }}</span>
                                        </div>
                                      }
                                    </div>
                                  </div>
                                }
                              </div>
                            </div>
                          }
                        </div>
                      </div>
                    }
                  </div>
                </div>
              </div>
            </div>

            <!-- True Gross Margin & COGS Tracker Studio -->
            <div class="lg:col-span-6 bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm">
              <div class="flex items-center justify-between pb-3 mb-4 border-b border-slate-800/80">
                <div class="flex items-center gap-2">
                  <span class="text-lg">⚖️</span>
                  <h3 class="text-sm font-bold text-white">
                    Deal Cost & True Gross Margin Engine
                  </h3>
                </div>
                <span class="text-xs text-indigo-300 font-mono">Revenue - COGS - Courier - Labor</span>
              </div>

              <!-- Deal Calculator Form -->
              <div class="space-y-4">
                <div>
                  <label class="block text-[11px] font-medium text-slate-300 mb-1">Deal Title</label>
                  <input
                    type="text"
                    data-testid="deal-title-input"
                    [ngModel]="calcDealTitle()"
                    (ngModelChange)="calcDealTitle.set($event)"
                    class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white focus:outline-none focus:border-indigo-500 font-sans" />
                </div>

                <div class="grid grid-cols-2 gap-3">
                  <div>
                    <label class="block text-[11px] font-medium text-slate-300 mb-1">Gross Contract Revenue ($)</label>
                    <input
                      type="number"
                      data-testid="calc-revenue-input"
                      [ngModel]="calcRevenue()"
                      (ngModelChange)="calcRevenue.set($event)"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:outline-none focus:border-indigo-500" />
                  </div>

                  <div>
                    <label class="block text-[11px] font-medium text-slate-300 mb-1">Cost of Goods Sold (COGS) ($)</label>
                    <input
                      type="number"
                      data-testid="calc-cogs-input"
                      [ngModel]="calcCogs()"
                      (ngModelChange)="calcCogs.set($event)"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:outline-none focus:border-indigo-500" />
                  </div>
                </div>

                <div class="grid grid-cols-3 gap-3">
                  <div>
                    <label class="block text-[11px] font-medium text-slate-300 mb-1">Courier Shipping ($)</label>
                    <input
                      type="number"
                      data-testid="calc-courier-input"
                      [ngModel]="calcCourierFees()"
                      (ngModelChange)="calcCourierFees.set($event)"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:outline-none focus:border-indigo-500" />
                  </div>

                  <div>
                    <label class="block text-[11px] font-medium text-slate-300 mb-1">Operator Labor (Hrs)</label>
                    <input
                      type="number"
                      data-testid="calc-labor-hours-input"
                      [ngModel]="calcLaborHours()"
                      (ngModelChange)="calcLaborHours.set($event)"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:outline-none focus:border-indigo-500" />
                  </div>

                  <div>
                    <label class="block text-[11px] font-medium text-slate-300 mb-1">Hourly Rate ($/hr)</label>
                    <input
                      type="number"
                      [ngModel]="calcHourlyRate()"
                      (ngModelChange)="calcHourlyRate.set($event)"
                      class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-xs text-white font-mono focus:outline-none focus:border-indigo-500" />
                  </div>
                </div>

                <!-- True Margin Output Card -->
                <div class="bg-slate-950 border border-slate-800 rounded-xl p-4 mt-2">
                  <div class="flex items-center justify-between pb-3 mb-3 border-b border-slate-900">
                    <div>
                      <div class="text-[10px] text-slate-500 uppercase tracking-wider font-mono">True Gross Margin Result</div>
                      <div class="text-xl font-bold font-mono" [ngClass]="marginHealthColor()">
                        &dollar;{{ computedTrueMargin().toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) }}
                      </div>
                    </div>

                    <div class="text-right">
                      <div class="text-[10px] text-slate-500 uppercase tracking-wider font-mono">Margin Percentage</div>
                      <div class="text-xl font-bold font-mono" [ngClass]="marginHealthColor()" data-testid="margin-percentage-display">
                        {{ computedMarginPct() }}%
                      </div>
                    </div>
                  </div>

                  <!-- Margin Health Indicator -->
                  <div class="flex items-center justify-between text-xs mb-2">
                    <span class="text-slate-400">Profitability Health Grade:</span>
                    <span class="font-bold uppercase text-[11px] px-2 py-0.5 rounded border" [ngClass]="marginHealthBadge()">
                      {{ marginHealthLabel() }}
                    </span>
                  </div>

                  <!-- Cost Stack Share Breakdown Bar -->
                  <div class="space-y-1.5 pt-2 border-t border-slate-900 text-[10px] text-slate-400 font-mono">
                    <div class="flex justify-between">
                      <span>COGS: &dollar;{{ calcCogs() }}</span>
                      <span>Courier: &dollar;{{ calcCourierFees() }}</span>
                      <span>Labor: &dollar;{{ computedLaborCost() }}</span>
                    </div>
                    <div class="w-full h-2 rounded-full overflow-hidden flex bg-slate-800">
                      <div class="bg-red-500/80 h-full" [style.width.%]="cogsPctShare()" title="COGS"></div>
                      <div class="bg-amber-500/80 h-full" [style.width.%]="courierPctShare()" title="Shipping"></div>
                      <div class="bg-purple-500/80 h-full" [style.width.%]="laborPctShare()" title="Labor"></div>
                      <div class="bg-emerald-500/80 h-full" [style.width.%]="netMarginPctShare()" title="Net Margin"></div>
                    </div>
                  </div>
                </div>

                <!-- Record Deal Button -->
                <button
                  (click)="recordCurrentDealMargin()"
                  data-testid="record-deal-margin-btn"
                  class="w-full py-2.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold flex items-center justify-center gap-2 transition shadow-sm">
                  💾 Record Deal to Margin Ledger
                </button>
              </div>
            </div>
          </div>

          <!-- 3. Historical Deals True Margin Ledger Table -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 shadow-sm">
            <div class="flex items-center justify-between pb-3 mb-4 border-b border-slate-800/80">
              <h3 class="text-sm font-bold text-white flex items-center gap-2">
                📋 Historical Deals True Margin Ledger
              </h3>
              <span class="text-xs text-slate-500">{{ dealLedger().length }} analyzed deals</span>
            </div>

            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300 font-sans" data-testid="margin-ledger-table">
                <thead class="bg-slate-950 text-slate-400 uppercase text-[10px] tracking-wider font-mono border-b border-slate-800">
                  <tr>
                    <th class="py-2.5 px-3">Deal Title</th>
                    <th class="py-2.5 px-3">Operating Entity</th>
                    <th class="py-2.5 px-3 text-right">Revenue</th>
                    <th class="py-2.5 px-3 text-right">COGS</th>
                    <th class="py-2.5 px-3 text-right">Shipping</th>
                    <th class="py-2.5 px-3 text-right">Labor Cost</th>
                    <th class="py-2.5 px-3 text-right">True Margin</th>
                    <th class="py-2.5 px-3 text-right">Margin %</th>
                    <th class="py-2.5 px-3 text-center">Status</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60 font-mono">
                  @for (deal of dealLedger(); track deal.id) {
                    <tr class="hover:bg-slate-800/30 transition">
                      <td class="py-3 px-3 font-bold text-white font-sans">{{ deal.dealTitle }}</td>
                      <td class="py-3 px-3 font-sans text-slate-400">{{ deal.organizationName }}</td>
                      <td class="py-3 px-3 text-right font-bold text-slate-200">&dollar;{{ deal.revenue.toFixed(2) }}</td>
                      <td class="py-3 px-3 text-right text-red-400">&dollar;{{ deal.cogs.toFixed(2) }}</td>
                      <td class="py-3 px-3 text-right text-amber-400">&dollar;{{ deal.courierFees.toFixed(2) }}</td>
                      <td class="py-3 px-3 text-right text-purple-400">&dollar;{{ deal.laborCost.toFixed(2) }}</td>
                      <td class="py-3 px-3 text-right font-bold text-emerald-400">&dollar;{{ deal.trueGrossMargin.toFixed(2) }}</td>
                      <td class="py-3 px-3 text-right font-bold" [ngClass]="deal.marginPercentage >= 40 ? 'text-emerald-400' : 'text-amber-400'">
                        {{ deal.marginPercentage.toFixed(2) }}%
                      </td>
                      <td class="py-3 px-3 text-center">
                        <span
                          class="px-2 py-0.5 rounded text-[10px] font-mono border"
                          [ngClass]="deal.marginPercentage >= 40 ? 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30' : 'bg-amber-500/20 text-amber-300 border-amber-500/30'">
                          {{ deal.marginPercentage >= 40 ? 'Profitable' : 'Moderate' }}
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
export class OrganizationMarginCockpitComponent {
  translation = inject(TranslationService);

  toastMessage = signal<string>('');

  // Deal Calculator Inputs
  calcDealTitle = signal<string>('Enterprise Multi-Store Omnichannel Rollout');
  calcRevenue = signal<number>(15000);
  calcCogs = signal<number>(4500);
  calcCourierFees = signal<number>(1200);
  calcLaborHours = signal<number>(35);
  calcHourlyRate = signal<number>(30);

  // Tree Hierarchy
  treeData = signal<OrgNode>({
    id: 'tenant-root',
    name: 'SGrowing Global Enterprise Tenant',
    type: 'Tenant',
    headcount: 14,
    totalRevenue: 214800,
    totalMargin: 106450,
    children: [
      {
        id: 'org-holding-1',
        name: 'SGrowing Global Holding (SG-HOLDING)',
        type: 'Organization',
        headcount: 9,
        totalRevenue: 142000,
        totalMargin: 74200,
        children: [
          {
            id: 'branch-tunis',
            name: 'North Africa Regional Hub (Tunis)',
            type: 'Branch',
            headcount: 5,
            totalRevenue: 85000,
            totalMargin: 46000,
            children: [
              {
                id: 'team-sdr-tunis',
                name: 'High-Velocity COD Outbound SDRs',
                type: 'Team',
                headcount: 3,
                totalRevenue: 0,
                totalMargin: 0,
                children: [
                  { id: 'm-1', name: 'Sarah Al-Mansoor', type: 'TeamMember', headcount: 1, totalRevenue: 0, totalMargin: 0, meta: 'Lead SDR • $35/hr' },
                  { id: 'm-2', name: 'Omar Ben Salem', type: 'TeamMember', headcount: 1, totalRevenue: 0, totalMargin: 0, meta: 'SDR Rep • $30/hr' },
                  { id: 'm-3', name: 'Yassine Khelil', type: 'TeamMember', headcount: 1, totalRevenue: 0, totalMargin: 0, meta: 'SDR Rep • $28/hr' }
                ]
              },
              {
                id: 'team-logistics-tunis',
                name: 'Logistics & Warehouse Operations',
                type: 'Team',
                headcount: 2,
                totalRevenue: 0,
                totalMargin: 0,
                children: [
                  { id: 'm-4', name: 'Karim Trabelsi', type: 'TeamMember', headcount: 1, totalRevenue: 0, totalMargin: 0, meta: 'Logistics Lead • $32/hr' },
                  { id: 'm-5', name: 'Sami Bouazizi', type: 'TeamMember', headcount: 1, totalRevenue: 0, totalMargin: 0, meta: 'Packer • $22/hr' }
                ]
              }
            ]
          },
          {
            id: 'branch-dubai',
            name: 'Middle East Regional Hub (Dubai)',
            type: 'Branch',
            headcount: 4,
            totalRevenue: 57000,
            totalMargin: 28200,
            children: [
              {
                id: 'team-sales-dubai',
                name: 'Enterprise B2B Accounts Team',
                type: 'Team',
                headcount: 4,
                totalRevenue: 0,
                totalMargin: 0,
                children: [
                  { id: 'm-6', name: 'Tariq Mansour', type: 'TeamMember', headcount: 1, totalRevenue: 0, totalMargin: 0, meta: 'VP Sales • $45/hr' },
                  { id: 'm-7', name: 'Layla Qasim', type: 'TeamMember', headcount: 1, totalRevenue: 0, totalMargin: 0, meta: 'Account Exec • $35/hr' }
                ]
              }
            ]
          }
        ]
      },
      {
        id: 'org-apex-2',
        name: 'Apex Omnichannel Commerce Ltd (APEX-COMM)',
        type: 'Organization',
        headcount: 5,
        totalRevenue: 72800,
        totalMargin: 32250,
        children: [
          {
            id: 'branch-paris',
            name: 'European Retail Hub (Paris)',
            type: 'Branch',
            headcount: 5,
            totalRevenue: 72800,
            totalMargin: 32250,
            children: [
              {
                id: 'team-eu-crossborder',
                name: 'Cross-Border Customs & Trade Team',
                type: 'Team',
                headcount: 5,
                totalRevenue: 0,
                totalMargin: 0,
                children: [
                  { id: 'm-8', name: 'Jean-Luc Moreau', type: 'TeamMember', headcount: 1, totalRevenue: 0, totalMargin: 0, meta: 'Trade Lead • $40/hr' },
                  { id: 'm-9', name: 'Claire Dubois', type: 'TeamMember', headcount: 1, totalRevenue: 0, totalMargin: 0, meta: 'Compliance • $35/hr' }
                ]
              }
            ]
          }
        ]
      }
    ]
  });

  totalHeadcount = computed(() => {
    return this.treeData().headcount;
  });

  dealLedger = signal<DealMarginItem[]>([
    {
      id: 'deal-1',
      dealTitle: 'Enterprise Multi-Store Omnichannel Rollout',
      organizationName: 'SGrowing Global Holding',
      revenue: 12500,
      cogs: 3800,
      courierFees: 950,
      laborHours: 40,
      hourlyRate: 30,
      laborCost: 1200,
      trueGrossMargin: 6550,
      marginPercentage: 52.40,
      calculatedAt: 'Yesterday'
    },
    {
      id: 'deal-2',
      dealTitle: 'MENA Regional COD Fulfillment Contract',
      organizationName: 'SGrowing Global Holding',
      revenue: 8400,
      cogs: 3100,
      courierFees: 1100,
      laborHours: 25,
      hourlyRate: 28,
      laborCost: 700,
      trueGrossMargin: 3500,
      marginPercentage: 41.67,
      calculatedAt: 'Oct 25, 2026'
    },
    {
      id: 'deal-3',
      dealTitle: 'European Cross-Border Customs Pilot',
      organizationName: 'Apex Omnichannel Commerce Ltd',
      revenue: 19800,
      cogs: 8200,
      courierFees: 2400,
      laborHours: 50,
      hourlyRate: 35,
      laborCost: 1750,
      trueGrossMargin: 7450,
      marginPercentage: 37.63,
      calculatedAt: 'Oct 24, 2026'
    }
  ]);

  consolidatedRevenue = computed(() => {
    return this.dealLedger().reduce((sum, d) => sum + d.revenue, 0);
  });

  consolidatedCostBase = computed(() => {
    return this.dealLedger().reduce((sum, d) => sum + d.cogs + d.courierFees + d.laborCost, 0);
  });

  consolidatedTrueMargin = computed(() => {
    return this.dealLedger().reduce((sum, d) => sum + d.trueGrossMargin, 0);
  });

  consolidatedMarginPct = computed(() => {
    const rev = this.consolidatedRevenue();
    return rev > 0 ? (this.consolidatedTrueMargin() / rev) * 100 : 0;
  });

  computedLaborCost = computed(() => {
    return Math.round(Number(this.calcLaborHours()) * Number(this.calcHourlyRate()));
  });

  computedTrueMargin = computed(() => {
    const rev = Number(this.calcRevenue());
    const cogs = Number(this.calcCogs());
    const courier = Number(this.calcCourierFees());
    const labor = this.computedLaborCost();
    return Math.round((rev - cogs - courier - labor) * 100) / 100;
  });

  computedMarginPct = computed(() => {
    const rev = Number(this.calcRevenue());
    if (rev <= 0) return 0;
    const margin = this.computedTrueMargin();
    return Math.round((margin / rev) * 10000) / 100;
  });

  cogsPctShare = computed(() => {
    const rev = Number(this.calcRevenue());
    return rev > 0 ? (Number(this.calcCogs()) / rev) * 100 : 0;
  });

  courierPctShare = computed(() => {
    const rev = Number(this.calcRevenue());
    return rev > 0 ? (Number(this.calcCourierFees()) / rev) * 100 : 0;
  });

  laborPctShare = computed(() => {
    const rev = Number(this.calcRevenue());
    return rev > 0 ? (this.computedLaborCost() / rev) * 100 : 0;
  });

  netMarginPctShare = computed(() => {
    return Math.max(0, this.computedMarginPct());
  });

  marginHealthColor(): string {
    const pct = this.computedMarginPct();
    if (pct >= 40) return 'text-emerald-400';
    if (pct >= 20) return 'text-amber-400';
    return 'text-red-400';
  }

  marginHealthBadge(): string {
    const pct = this.computedMarginPct();
    if (pct >= 40) return 'bg-emerald-500/20 text-emerald-300 border-emerald-500/30';
    if (pct >= 20) return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
    return 'bg-red-500/20 text-red-300 border-red-500/30';
  }

  marginHealthLabel(): string {
    const pct = this.computedMarginPct();
    if (pct >= 40) return 'Healthy Unit Economics (>40%)';
    if (pct >= 20) return 'Moderate Margin (20-40%)';
    return 'Low Margin Risk (<20%)';
  }

  recalculateDealMargin(): void {
    // Computed signals handle this reactively
  }

  recordCurrentDealMargin(): void {
    const newDeal: DealMarginItem = {
      id: `deal-${Date.now()}`,
      dealTitle: this.calcDealTitle().trim() || 'Custom Deal Calculation',
      organizationName: 'SGrowing Global Holding',
      revenue: Number(this.calcRevenue()),
      cogs: Number(this.calcCogs()),
      courierFees: Number(this.calcCourierFees()),
      laborHours: Number(this.calcLaborHours()),
      hourlyRate: Number(this.calcHourlyRate()),
      laborCost: this.computedLaborCost(),
      trueGrossMargin: this.computedTrueMargin(),
      marginPercentage: this.computedMarginPct(),
      calculatedAt: 'Just now'
    };

    this.dealLedger.update(current => [newDeal, ...current]);
    this.toastMessage.set(`Deal "${newDeal.dealTitle}" recorded with True Gross Margin of $${newDeal.trueGrossMargin.toFixed(2)} (${newDeal.marginPercentage}%)!`);
  }
}
