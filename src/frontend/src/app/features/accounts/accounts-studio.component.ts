import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CompanyUI {
  id: string;
  name: string;
  domain: string;
  industry: string;
  annualRevenue: number;
  contactsCount: number;
  dealsValue: number;
}

@Component({
  selector: 'app-accounts-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex h-screen bg-slate-950 text-slate-100 font-sans overflow-hidden">
      <!-- Main Content -->
      <div class="flex-1 flex flex-col h-full overflow-hidden">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/50 backdrop-blur px-8 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 rounded-lg bg-blue-600/20 text-blue-400 flex items-center justify-center font-bold text-sm border border-blue-500/30">
              🏢
            </div>
            <div>
              <h1 class="text-lg font-semibold text-white tracking-tight">B2B Organizations & Accounts CRM</h1>
              <p class="text-xs text-slate-400">Twenty model: Company hierarchies, domain auto-linking & ARR pipeline rollups</p>
            </div>
          </div>
          <button 
            (click)="showCreateModal = true"
            class="bg-blue-600 hover:bg-blue-500 text-white font-medium px-4 py-2 rounded-lg text-sm transition shadow-lg shadow-blue-900/30">
            + New Company
          </button>
        </header>

        <!-- Body -->
        <div class="flex-1 overflow-y-auto p-8 space-y-8">
          
          <!-- Summary Cards -->
          <div class="grid grid-cols-1 md:grid-cols-4 gap-6">
            <div class="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg">
              <div class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Tracked Companies</div>
              <div class="text-2xl font-bold text-white mt-1">{{ companies().length }}</div>
              <div class="text-xs text-blue-400 mt-2 font-medium">Domain auto-linked</div>
            </div>

            <div class="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg">
              <div class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Total Pipeline ARR</div>
              <div class="text-2xl font-bold text-emerald-400 mt-1">$248,000</div>
              <div class="text-xs text-slate-500 mt-2">Active opportunities</div>
            </div>

            <div class="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg">
              <div class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Linked Contacts</div>
              <div class="text-2xl font-bold text-indigo-400 mt-1">42</div>
              <div class="text-xs text-slate-500 mt-2">Cross-channel profiles</div>
            </div>

            <div class="bg-slate-900 border border-slate-800 p-5 rounded-xl shadow-lg">
              <div class="text-xs text-slate-400 uppercase tracking-wider font-semibold">Win Rate</div>
              <div class="text-2xl font-bold text-white mt-1">64.2%</div>
              <div class="text-xs text-emerald-400 mt-2 font-medium">+8% from AI SDR</div>
            </div>
          </div>

          <!-- Companies Table -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-4">
            <h2 class="text-base font-semibold text-white">Target Accounts & Corporate Entities</h2>
            
            <div class="overflow-x-auto">
              <table class="w-full text-left text-xs text-slate-300">
                <thead class="text-[11px] text-slate-400 uppercase tracking-wider bg-slate-950/60 border-b border-slate-800">
                  <tr>
                    <th class="py-3 px-4">Company Name</th>
                    <th class="py-3 px-4">Domain</th>
                    <th class="py-3 px-4">Industry</th>
                    <th class="py-3 px-4">Annual Revenue</th>
                    <th class="py-3 px-4">Linked Contacts</th>
                    <th class="py-3 px-4">Pipeline Value</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-800/60">
                  @for (c of companies(); track c.id) {
                    <tr class="hover:bg-slate-800/40 transition">
                      <td class="py-3 px-4 font-semibold text-white flex items-center gap-2">
                        <div class="w-6 h-6 rounded bg-blue-950 border border-blue-800 text-blue-300 flex items-center justify-center font-bold text-[11px]">
                          {{ c.name[0] }}
                        </div>
                        {{ c.name }}
                      </td>
                      <td class="py-3 px-4 font-mono text-blue-400">{{ c.domain }}</td>
                      <td class="py-3 px-4 text-slate-400">{{ c.industry }}</td>
                      <td class="py-3 px-4 text-slate-200 font-mono">\${{ c.annualRevenue | number:'1.0-0' }}</td>
                      <td class="py-3 px-4">
                        <span class="bg-slate-800 text-slate-300 px-2 py-0.5 rounded font-mono">{{ c.contactsCount }} members</span>
                      </td>
                      <td class="py-3 px-4 font-semibold text-emerald-400 font-mono">\${{ c.dealsValue | number:'1.0-0' }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </div>

          <!-- Modal: Create Company -->
          @if (showCreateModal) {
            <div class="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center p-4 z-50">
              <div class="bg-slate-900 border border-slate-800 rounded-xl p-6 w-full max-w-md shadow-2xl space-y-4">
                <h3 class="text-base font-semibold text-white">Create B2B Organization</h3>
                
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Company Name</label>
                  <input [(ngModel)]="newCompanyName" placeholder="e.g. Acme Corp" 
                         class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-blue-500" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Domain (for auto-linking)</label>
                  <input [(ngModel)]="newCompanyDomain" placeholder="e.g. acme.corp" 
                         class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-blue-500 font-mono" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Industry</label>
                  <input [(ngModel)]="newCompanyIndustry" placeholder="e.g. Enterprise SaaS" 
                         class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-blue-500" />
                </div>

                <div class="flex items-center gap-3 pt-3">
                  <button (click)="createCompany()" class="flex-1 bg-blue-600 hover:bg-blue-500 text-white font-medium py-2 rounded-lg text-sm transition">
                    Save Company
                  </button>
                  <button (click)="showCreateModal = false" class="bg-slate-800 hover:bg-slate-700 text-slate-300 font-medium px-4 py-2 rounded-lg text-sm transition">
                    Cancel
                  </button>
                </div>
              </div>
            </div>
          }

        </div>
      </div>
    </div>
  `
})
export class AccountsStudioComponent implements OnInit {
  public showCreateModal = false;
  public newCompanyName = '';
  public newCompanyDomain = '';
  public newCompanyIndustry = '';

  public companies = signal<CompanyUI[]>([
    { id: '1', name: 'Themyscira Enterprises', domain: 'themyscira.com', industry: 'Global Security', annualRevenue: 2400000, contactsCount: 5, dealsValue: 12000 },
    { id: '2', name: 'Wayne Enterprises', domain: 'wayne.corp', industry: 'Aerospace & Tech', annualRevenue: 8500000, contactsCount: 12, dealsValue: 48000 },
    { id: '3', name: 'Star Labs Research', domain: 'star-labs.com', industry: 'Biotechnology', annualRevenue: 1200000, contactsCount: 4, dealsValue: 8400 }
  ]);

  ngOnInit(): void {}

  public createCompany(): void {
    if (!this.newCompanyName || !this.newCompanyDomain) return;
    this.companies.update(list => [
      ...list,
      {
        id: 'comp-' + Date.now(),
        name: this.newCompanyName,
        domain: this.newCompanyDomain,
        industry: this.newCompanyIndustry || 'Technology',
        annualRevenue: 500000,
        contactsCount: 1,
        dealsValue: 2400
      }
    ]);
    this.showCreateModal = false;
    this.newCompanyName = '';
    this.newCompanyDomain = '';
    this.newCompanyIndustry = '';
  }
}
