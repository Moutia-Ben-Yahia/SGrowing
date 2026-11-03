import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface EquityStakeholderRecord {
  stakeholderId: string;
  legalName: string;
  stakeholderType: string;
  sharesCount: number;
  ownershipPercentagePct: number;
  investedCapitalUsd: number;
  shareClass: string;
}

export interface StockOptionGrantRecord {
  grantId: string;
  employeeName: string;
  grantedSharesCount: number;
  strikePriceUsd: number;
  vestedSharesCount: number;
  status: string;
}

@Component({
  selector: 'app-cap-table-equity-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div dir="ltr" data-testid="cap-table-equity-cockpit" class="p-6 bg-slate-900 text-slate-100 min-h-screen">
      <header class="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <div>
          <h1 class="text-2xl font-bold tracking-tight text-white flex items-center gap-2">
            <span class="inline-block w-3 h-3 rounded-full bg-indigo-500 animate-pulse"></span>
            Cap Table Equity Dilution & Stock Option Vesting Ledger
          </h1>
          <p class="text-slate-400 text-sm mt-1">
            Statutory 409A Valuation Snapshotting, Waterfall Financing Round Dilution & Employee Option Vesting Schedules
          </p>
        </div>
        <div class="flex gap-3">
          <button (click)="grantNewOption()" class="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white font-medium rounded-lg shadow-sm transition">
            Grant Employee Stock Option
          </button>
        </div>
      </header>

      <!-- KPI Metrics Grid -->
      <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-8">
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Total Shares</span>
          <p class="text-2xl font-extrabold text-white mt-1">10,000,000</p>
          <span class="text-xs text-indigo-400">Fully Diluted Common & Pref</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">409A Valuation</span>
          <p class="text-2xl font-extrabold text-white mt-1">$75.0M</p>
          <span class="text-xs text-emerald-400">$7.50 / Share FMV</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">Stakeholders</span>
          <p class="text-2xl font-extrabold text-white mt-1">{{ stakeholders().length }}</p>
          <span class="text-xs text-cyan-400">Founders, Angels & Series A</span>
        </div>
        <div class="bg-slate-800/60 border border-slate-700/50 p-4 rounded-xl">
          <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider">ESOP Pool</span>
          <p class="text-2xl font-extrabold text-white mt-1">15.0%</p>
          <span class="text-xs text-amber-400">1,500,000 Reserved</span>
        </div>
      </section>

      <!-- Main Panels -->
      <div class="grid grid-cols-1 lg:grid-cols-2 gap-8">
        <!-- Stakeholders Table -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Capitalization Table Registry</h2>
          <div class="space-y-4">
            <div *ngFor="let s of stakeholders()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-white">{{ s.legalName }}</span>
                <span class="text-xs px-2 py-0.5 rounded font-mono bg-indigo-950 text-indigo-300 border border-indigo-800">
                  {{ s.shareClass }}
                </span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Class: {{ s.stakeholderType }} • Invested: {{ s.investedCapitalUsd | currency }}</p>
              <div class="flex items-center gap-2">
                <div class="flex-1 bg-slate-700 h-2 rounded-full overflow-hidden">
                  <div class="bg-indigo-500 h-full rounded-full" [style.width.%]="s.ownershipPercentagePct"></div>
                </div>
                <span class="text-xs font-mono font-bold text-indigo-300">{{ s.ownershipPercentagePct }}%</span>
              </div>
            </div>
          </div>
        </section>

        <!-- Option Grants -->
        <section class="bg-slate-800/40 border border-slate-700/60 rounded-xl p-6">
          <h2 class="text-lg font-semibold text-white mb-4">Employee Option Vesting Ledger</h2>
          <div class="space-y-4">
            <div *ngFor="let g of optionGrants()" class="bg-slate-900/60 border border-slate-700/40 p-4 rounded-lg">
              <div class="flex justify-between items-center mb-1">
                <span class="font-bold text-indigo-300">{{ g.employeeName }}</span>
                <span class="text-xs font-mono text-emerald-400 font-bold">{{ g.strikePriceUsd | currency }}/sh strike</span>
              </div>
              <p class="text-xs text-slate-400 mb-2">Grant ID: {{ g.grantId }} • Status: {{ g.status }}</p>
              <div class="bg-slate-950 p-2.5 rounded text-xs font-mono text-slate-300 flex justify-between">
                <span>Granted: {{ g.grantedSharesCount | number }} shares</span>
                <span class="text-emerald-400">Vested: {{ g.vestedSharesCount | number }} shares</span>
              </div>
            </div>
          </div>
        </section>
      </div>
    </div>
  `
})
export class CapTableEquityCockpitComponent {
  stakeholders = signal<EquityStakeholderRecord[]>([
    { stakeholderId: 'STK-FND-01', legalName: 'Founders Holding Entity LLC', stakeholderType: 'FOUNDER', sharesCount: 6000000, ownershipPercentagePct: 60.0, investedCapitalUsd: 50000, shareClass: 'COMMON' },
    { stakeholderId: 'STK-INV-02', legalName: 'Apex Horizon Ventures Series A', stakeholderType: 'INVESTOR_PREFERRED', sharesCount: 2500000, ownershipPercentagePct: 25.0, investedCapitalUsd: 15000000, shareClass: 'SERIES_A' },
    { stakeholderId: 'STK-OPT-03', legalName: 'Employee Stock Option Pool (ESOP)', stakeholderType: 'EMPLOYEE_COMMON', sharesCount: 1500000, ownershipPercentagePct: 15.0, investedCapitalUsd: 0, shareClass: 'COMMON' }
  ]);

  optionGrants = signal<StockOptionGrantRecord[]>([
    { grantId: 'GRT-2026-001', employeeName: 'Dr. Elena Rostova (Chief AI Architect)', grantedSharesCount: 120000, strikePriceUsd: 1.25, vestedSharesCount: 45000, status: 'VESTING_ACTIVE' }
  ]);

  grantNewOption() {
    const newGrant: StockOptionGrantRecord = {
      grantId: `GRT-${Date.now().toString().slice(-4)}`,
      employeeName: 'Marcus Brody (VP Technology)',
      grantedSharesCount: 80000,
      strikePriceUsd: 1.25,
      vestedSharesCount: 0,
      status: 'VESTING_ACTIVE'
    };
    this.optionGrants.update(list => [newGrant, ...list]);
  }
}
