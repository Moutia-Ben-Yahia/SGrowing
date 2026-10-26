import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

export interface DigitalContractUi {
  id: string;
  title: string;
  contractType: string;
  status: 'Draft' | 'Sent' | 'Viewed' | 'Signed' | 'Declined';
  signerName: string;
  signerEmail: string;
  contractValue: number;
  signingToken: string;
  dealTitle?: string;
  signedPdfUrl?: string;
  createdAt: string;
}

@Component({
  selector: 'app-contracts-studio',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <!-- Left Nav Bar -->
      <aside class="w-64 border-r border-slate-800 bg-slate-900/80 flex flex-col justify-between p-4">
        <div>
          <div class="flex items-center gap-2 mb-6">
            <div class="h-8 w-8 rounded-lg bg-gradient-to-tr from-emerald-500 to-teal-400 flex items-center justify-center font-bold text-white shadow-lg shadow-emerald-500/20">
              ✍️
            </div>
            <div>
              <h1 class="text-sm font-semibold tracking-wide text-white">SGrowing</h1>
              <p class="text-xs text-emerald-400 font-medium">In-Chat E-Signatures</p>
            </div>
          </div>

          <div class="space-y-1 mb-6">
            <span class="text-xs font-semibold text-slate-400 uppercase tracking-wider px-2">Navigation</span>
            <div class="space-y-1">
              <a routerLink="/" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-slate-300 hover:bg-slate-800/60 transition">
                <span>📥 Omnichannel Inbox</span>
              </a>
              <a routerLink="/accounts" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-blue-400 hover:bg-blue-500/10 border border-blue-500/20 transition">
                <span>🏢 B2B Accounts CRM</span>
              </a>
              <a routerLink="/contracts" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 font-medium">
                <span>✍️ In-Chat E-Signatures</span>
              </a>
              <a routerLink="/rag" class="w-full flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs text-purple-400 hover:bg-purple-500/10 border border-purple-500/20 transition">
                <span>🧠 Vector RAG Knowledge</span>
              </a>
            </div>
          </div>

          <div class="p-3 bg-gradient-to-b from-slate-800/90 to-slate-900 border border-emerald-500/30 rounded-xl space-y-2">
            <div class="flex items-center justify-between">
              <span class="text-xs font-semibold text-emerald-300 flex items-center gap-1.5">
                <span class="h-2 w-2 rounded-full bg-emerald-400 animate-pulse"></span>
                DocuSeal Engine
              </span>
              <span class="text-[10px] bg-emerald-500/10 text-emerald-300 border border-emerald-500/20 px-1.5 py-0.5 rounded">Auto-Won</span>
            </div>
            <p class="text-[11px] text-slate-300 leading-relaxed">
              When a signer completes their in-chat signature, SGrowing automatically marks the associated CRM deal as WON.
            </p>
          </div>
        </div>
      </aside>

      <!-- Main Content -->
      <main class="flex-1 flex flex-col overflow-hidden bg-slate-950">
        <header class="h-16 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between">
          <div>
            <h2 class="text-base font-semibold text-white">In-Chat Digital Contracts & E-Signatures</h2>
            <p class="text-xs text-slate-400">Generate contracts, dispatch instant WhatsApp/SMS signing links, and auto-close deals.</p>
          </div>
          <button
            (click)="showCreateModal.set(true)"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-xs rounded-lg transition shadow-lg shadow-emerald-500/20 flex items-center gap-1.5">
            <span>+</span> Draft New Contract
          </button>
        </header>

        <div class="flex-1 overflow-y-auto p-6 space-y-6 max-w-6xl">
          <!-- Summary Cards -->
          <div class="grid grid-cols-4 gap-4">
            <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
              <span class="text-xs text-slate-400">Total Contract Value</span>
              <p class="text-xl font-bold text-white mt-1 font-mono">\${{ totalValue() | number:'1.0-0' }}</p>
            </div>
            <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
              <span class="text-xs text-slate-400">Signed & Closed Won</span>
              <p class="text-xl font-bold text-emerald-400 mt-1 font-mono">\${{ signedValue() | number:'1.0-0' }}</p>
            </div>
            <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
              <span class="text-xs text-slate-400">Awaiting Signature</span>
              <p class="text-xl font-bold text-amber-400 mt-1 font-mono">{{ pendingCount() }}</p>
            </div>
            <div class="p-4 bg-slate-900 border border-slate-800 rounded-xl">
              <span class="text-xs text-slate-400">Signature Velocity</span>
              <p class="text-xl font-bold text-teal-400 mt-1 font-mono">&lt; 4 Hours</p>
            </div>
          </div>

          <!-- Active Contracts Table -->
          <div class="bg-slate-900 border border-slate-800 rounded-xl overflow-hidden">
            <div class="p-4 border-b border-slate-800 flex items-center justify-between">
              <h3 class="text-sm font-semibold text-white">Active Digital Contracts</h3>
              <span class="text-xs text-slate-400 font-mono">{{ contracts().length }} items</span>
            </div>

            <table class="w-full text-left text-xs">
              <thead class="bg-slate-950 text-slate-400 border-b border-slate-800">
                <tr>
                  <th class="p-3">Title & Type</th>
                  <th class="p-3">Signer</th>
                  <th class="p-3">Value</th>
                  <th class="p-3">Deal Link</th>
                  <th class="p-3">Status</th>
                  <th class="p-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800 text-slate-200">
                <tr *ngFor="let c of contracts()" class="hover:bg-slate-800/40 transition">
                  <td class="p-3">
                    <div class="font-medium text-white">{{ c.title }}</div>
                    <span class="text-[10px] text-emerald-400 uppercase tracking-wider">{{ c.contractType }}</span>
                  </td>
                  <td class="p-3">
                    <div>{{ c.signerName }}</div>
                    <div class="text-[11px] text-slate-400">{{ c.signerEmail }}</div>
                  </td>
                  <td class="p-3 font-mono font-medium text-slate-100">
                    \${{ c.contractValue | number:'1.0-0' }}
                  </td>
                  <td class="p-3 text-slate-300">
                    {{ c.dealTitle || 'Direct Account Contract' }}
                  </td>
                  <td class="p-3">
                    <span [ngClass]="{
                      'bg-slate-800 text-slate-300': c.status === 'Draft',
                      'bg-blue-500/10 text-blue-400 border border-blue-500/20': c.status === 'Sent',
                      'bg-amber-500/10 text-amber-400 border border-amber-500/20': c.status === 'Viewed',
                      'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20': c.status === 'Signed',
                      'bg-rose-500/10 text-rose-400 border border-rose-500/20': c.status === 'Declined'
                    }" class="px-2 py-0.5 rounded text-[11px] font-medium">
                      {{ c.status }}
                    </span>
                  </td>
                  <td class="p-3 text-right space-x-2">
                    <button
                      *ngIf="c.status === 'Draft'"
                      (click)="dispatchContract(c)"
                      class="px-2.5 py-1 bg-blue-600 hover:bg-blue-500 text-white rounded text-[11px] font-medium transition">
                      Dispatch In-Chat
                    </button>
                    <button
                      *ngIf="c.status === 'Sent' || c.status === 'Viewed'"
                      (click)="simulateCustomerSign(c)"
                      class="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-[11px] font-medium transition">
                      Simulate Sign
                    </button>
                    <a
                      *ngIf="c.status === 'Signed'"
                      [href]="c.signedPdfUrl || '#'"
                      target="_blank"
                      class="px-2.5 py-1 bg-slate-800 hover:bg-slate-700 text-emerald-400 rounded text-[11px] font-medium transition inline-block">
                      Download PDF
                    </a>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </main>

      <!-- Create Contract Modal -->
      <div *ngIf="showCreateModal()" class="fixed inset-0 bg-black/70 flex items-center justify-center p-4 z-50">
        <div class="bg-slate-900 border border-slate-800 rounded-xl p-6 w-full max-w-md space-y-4">
          <h3 class="text-sm font-semibold text-white">Draft Electronic Contract</h3>

          <div class="space-y-3 text-xs">
            <div>
              <label class="block text-slate-400 mb-1">Contract Title</label>
              <input [(ngModel)]="newContract.title" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-slate-200" placeholder="e.g., Enterprise MSA - 100 Seats" />
            </div>
            <div class="grid grid-cols-2 gap-2">
              <div>
                <label class="block text-slate-400 mb-1">Contract Type</label>
                <select [(ngModel)]="newContract.contractType" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-slate-200">
                  <option value="OrderForm">Order Form</option>
                  <option value="MSA">MSA</option>
                  <option value="NDA">NDA</option>
                  <option value="SOW">SOW</option>
                </select>
              </div>
              <div>
                <label class="block text-slate-400 mb-1">Contract Value ($)</label>
                <input type="number" [(ngModel)]="newContract.contractValue" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-slate-200" placeholder="48000" />
              </div>
            </div>
            <div>
              <label class="block text-slate-400 mb-1">Signer Full Name</label>
              <input [(ngModel)]="newContract.signerName" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-slate-200" placeholder="Elena Rostova" />
            </div>
            <div>
              <label class="block text-slate-400 mb-1">Signer Email</label>
              <input [(ngModel)]="newContract.signerEmail" class="w-full bg-slate-950 border border-slate-800 rounded-lg p-2 text-slate-200" placeholder="elena@acme.com" />
            </div>
          </div>

          <div class="flex justify-end gap-2 pt-2">
            <button (click)="showCreateModal.set(false)" class="px-3 py-1.5 bg-slate-800 text-slate-300 rounded-lg text-xs">Cancel</button>
            <button (click)="saveDraft()" class="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white font-medium rounded-lg text-xs">Create Draft</button>
          </div>
        </div>
      </div>
    </div>
  `
})
export class ContractsStudioComponent implements OnInit {
  showCreateModal = signal(false);

  newContract = {
    title: '',
    contractType: 'OrderForm',
    signerName: '',
    signerEmail: '',
    contractValue: 24000
  };

  contracts = signal<DigitalContractUi[]>([
    {
      id: 'c1',
      title: 'Enterprise Annual License - 50 Seats',
      contractType: 'OrderForm',
      status: 'Sent',
      signerName: 'Alexander Hayes',
      signerEmail: 'alex@stripe.com',
      contractValue: 48000,
      signingToken: 'tok_stripe_48000_enterprise',
      dealTitle: 'Stripe Global Deployment',
      createdAt: '2026-10-26T10:15:00Z'
    },
    {
      id: 'c2',
      title: 'Mutual Non-Disclosure Agreement',
      contractType: 'NDA',
      status: 'Signed',
      signerName: 'Sarah Jenkins',
      signerEmail: 'sarah@uber.com',
      contractValue: 0,
      signingToken: 'tok_uber_nda_signed',
      dealTitle: 'Uber Ride-Hail Integration',
      signedPdfUrl: '/storage/contracts/c2_signed.pdf',
      createdAt: '2026-10-25T14:30:00Z'
    }
  ]);

  totalValue = signal(0);
  signedValue = signal(0);
  pendingCount = signal(0);

  ngOnInit() {
    this.refreshMetrics();
  }

  refreshMetrics() {
    let total = 0;
    let signed = 0;
    let pending = 0;

    for (const c of this.contracts()) {
      total += c.contractValue;
      if (c.status === 'Signed') {
        signed += c.contractValue;
      } else if (c.status === 'Sent' || c.status === 'Viewed') {
        pending++;
      }
    }

    this.totalValue.set(total);
    this.signedValue.set(signed);
    this.pendingCount.set(pending);
  }

  saveDraft() {
    if (!this.newContract.title || !this.newContract.signerName) return;

    const created: DigitalContractUi = {
      id: 'c_' + Math.random().toString(36).substring(2, 9),
      title: this.newContract.title,
      contractType: this.newContract.contractType,
      status: 'Draft',
      signerName: this.newContract.signerName,
      signerEmail: this.newContract.signerEmail,
      contractValue: Number(this.newContract.contractValue) || 0,
      signingToken: 'tok_' + Math.random().toString(36).substring(2, 12),
      createdAt: new Date().toISOString()
    };

    this.contracts.update(arr => [created, ...arr]);
    this.showCreateModal.set(false);
    this.refreshMetrics();
  }

  dispatchContract(c: DigitalContractUi) {
    c.status = 'Sent';
    this.contracts.update(arr => [...arr]);
    this.refreshMetrics();
  }

  simulateCustomerSign(c: DigitalContractUi) {
    c.status = 'Signed';
    c.signedPdfUrl = `/storage/contracts/${c.id}_signed.pdf`;
    this.contracts.update(arr => [...arr]);
    this.refreshMetrics();
  }
}
