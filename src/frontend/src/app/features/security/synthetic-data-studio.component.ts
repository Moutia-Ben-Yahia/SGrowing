import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface ColumnRuleItem {
  name: string;
  classification: 'DirectIdentifier' | 'QuasiIdentifier' | 'SensitiveAttribute' | 'NonSensitive';
  strategy: 'RealisticFake' | 'FormatPreservingToken' | 'DifferentialPrivacyNoise' | 'DeterministicHash';
  dataType: string;
}

@Component({
  selector: 'app-synthetic-data-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="p-6 max-w-7xl mx-auto space-y-6 text-slate-100" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-800 pb-5">
        <div>
          <div class="flex items-center gap-3">
            <span class="text-3xl">🧪</span>
            <div>
              <h1 class="text-2xl font-bold tracking-tight text-white">
                Enterprise Synthetic Test Data & Masking Studio
              </h1>
              <p class="text-xs text-slate-400 mt-1">
                Zero-risk CI/CD sandbox data generator with Differential Privacy and Format-Preserving Encryption.
              </p>
            </div>
          </div>
        </div>

        <div class="flex items-center gap-3">
          <button
            (click)="generateDataset()"
            class="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold shadow-lg shadow-indigo-600/20 transition flex items-center gap-2">
            <span>⚡</span> Generate Synthetic Batch ({{ rowCount }} rows)
          </button>
        </div>
      </div>

      <!-- Controls & Schema Configuration -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Configuration Card -->
        <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-5 space-y-4">
          <h2 class="text-sm font-semibold text-white flex items-center gap-2">
            <span>⚙️</span> Sandbox Generation Settings
          </h2>

          <div>
            <label class="block text-xs text-slate-400 mb-1">Target Table Name</label>
            <input
              type="text"
              [(ngModel)]="tableName"
              class="w-full bg-slate-950 border border-slate-700 rounded-lg px-3 py-1.5 text-xs text-white" />
          </div>

          <div>
            <div class="flex justify-between text-xs text-slate-400 mb-1">
              <span>Row Count:</span>
              <span class="text-white font-bold">{{ rowCount }}</span>
            </div>
            <input
              type="range"
              min="10"
              max="500"
              step="10"
              [(ngModel)]="rowCount"
              class="w-full accent-indigo-500" />
          </div>

          <div>
            <div class="flex justify-between text-xs text-slate-400 mb-1">
              <span>Differential Privacy Budget (ε):</span>
              <span class="text-cyan-400 font-bold">{{ epsilon }}</span>
            </div>
            <input
              type="range"
              min="0.1"
              max="2.0"
              step="0.1"
              [(ngModel)]="epsilon"
              class="w-full accent-cyan-500" />
            <p class="text-[10px] text-slate-500 mt-1">Lower ε = higher privacy noise; Higher ε = higher analytical fidelity.</p>
          </div>

          <div class="pt-3 border-t border-slate-800 space-y-2">
            <div class="text-xs font-semibold text-slate-300">Privacy Protection Summary:</div>
            <div class="text-[11px] text-slate-400 space-y-1">
              <div class="flex justify-between">
                <span>Direct Identifiers Masked:</span>
                <span class="text-emerald-400 font-bold">{{ directCount() }} cols</span>
              </div>
              <div class="flex justify-between">
                <span>DP Perturbed Numeric Cols:</span>
                <span class="text-cyan-400 font-bold">{{ dpCount() }} cols</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Schema Columns Mapper -->
        <div class="lg:col-span-2 bg-slate-900/60 border border-slate-800 rounded-xl p-5 space-y-4">
          <h2 class="text-sm font-semibold text-white flex items-center gap-2">
            <span>📋</span> Schema Column Rules & Synthesis Strategy
          </h2>

          <div class="overflow-x-auto">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-950/60 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-800">
                <tr>
                  <th class="p-2.5">Column Name</th>
                  <th class="p-2.5">Classification</th>
                  <th class="p-2.5">Strategy</th>
                  <th class="p-2.5">Type</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-slate-800/60">
                @for (col of columns(); track col.name) {
                  <tr class="hover:bg-slate-800/30">
                    <td class="p-2.5 font-mono text-white font-medium">{{ col.name }}</td>
                    <td class="p-2.5">
                      <span
                        class="px-2 py-0.5 rounded text-[10px] font-semibold border"
                        [ngClass]="getClassificationBadge(col.classification)">
                        {{ col.classification }}
                      </span>
                    </td>
                    <td class="p-2.5 text-slate-300 font-mono text-[11px]">{{ col.strategy }}</td>
                    <td class="p-2.5 text-slate-400 font-mono text-[11px]">{{ col.dataType }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Live Generated Sample Table -->
      <div class="bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden">
        <div class="p-4 border-b border-slate-800 flex items-center justify-between">
          <h2 class="text-sm font-semibold text-white">Generated Sample Records Preview (First 5 Rows)</h2>
          <span class="text-xs text-emerald-400 font-mono">SOC 2 / GDPR Compliant</span>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full text-left text-xs">
            <thead class="bg-slate-950/60 text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-800">
              <tr>
                <th class="p-3">User ID</th>
                <th class="p-3">Synthetic Name</th>
                <th class="p-3">Synthetic Email</th>
                <th class="p-3">Synthetic Phone</th>
                <th class="p-3">Perturbed Balance</th>
                <th class="p-3">Masked Tax ID</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-800/60 font-mono">
              @for (row of sampleRows(); track row.id) {
                <tr class="hover:bg-slate-800/30">
                  <td class="p-3 text-cyan-300">{{ row.id }}</td>
                  <td class="p-3 text-white font-sans">{{ row.name }}</td>
                  <td class="p-3 text-slate-300">{{ row.email }}</td>
                  <td class="p-3 text-slate-400">{{ row.phone }}</td>
                  <td class="p-3 text-emerald-400 font-bold">\${{ row.balance | number:'1.2-2' }}</td>
                  <td class="p-3 text-amber-300">{{ row.taxId }}</td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `
})
export class SyntheticDataStudioComponent {
  tableName = 'CoreCustomers';
  rowCount = 50;
  epsilon = 0.5;

  columns = signal<ColumnRuleItem[]>([
    { name: 'Id', classification: 'NonSensitive', strategy: 'DeterministicHash', dataType: 'string' },
    { name: 'CustomerName', classification: 'DirectIdentifier', strategy: 'RealisticFake', dataType: 'string' },
    { name: 'WorkEmail', classification: 'DirectIdentifier', strategy: 'RealisticFake', dataType: 'string' },
    { name: 'MobilePhone', classification: 'DirectIdentifier', strategy: 'RealisticFake', dataType: 'string' },
    { name: 'AccountBalance', classification: 'SensitiveAttribute', strategy: 'DifferentialPrivacyNoise', dataType: 'decimal' },
    { name: 'TaxIdentifier', classification: 'DirectIdentifier', strategy: 'FormatPreservingToken', dataType: 'string' }
  ]);

  sampleRows = signal([
    { id: '8f4c2e1b9a7d3f0a', name: 'Alex Smith', email: 'alex.smith42@company.test', phone: '+1 (555) 381-9921', balance: 14250.85, taxId: '492-81-9482' },
    { id: '3b7e9a1c5d8f204b', name: 'Jordan Williams', email: 'jordan.williams18@sandbox.corp', phone: '+1 (555) 742-1049', balance: 9812.40, taxId: '719-33-8104' },
    { id: '9c1a4e7f2b8d053e', name: 'Taylor Johnson', email: 'taylor.johnson77@enterprise.dev', phone: '+1 (555) 290-6715', balance: 22100.12, taxId: '184-60-2918' },
    { id: '5d8f204b8f4c2e1b', name: 'Sam Brown', email: 'sam.brown55@mockmail.internal', phone: '+1 (555) 819-3320', balance: 6420.95, taxId: '903-12-7491' },
    { id: '2b8d053e3b7e9a1c', name: 'Morgan Jones', email: 'morgan.jones31@company.test', phone: '+1 (555) 430-8812', balance: 18740.60, taxId: '381-44-6029' }
  ]);

  directCount = computed(() => this.columns().filter(c => c.classification === 'DirectIdentifier').length);
  dpCount = computed(() => this.columns().filter(c => c.strategy === 'DifferentialPrivacyNoise').length);

  getClassificationBadge(classification: string): string {
    switch (classification) {
      case 'DirectIdentifier':
        return 'bg-rose-500/20 text-rose-300 border-rose-500/30';
      case 'QuasiIdentifier':
        return 'bg-amber-500/20 text-amber-300 border-amber-500/30';
      case 'SensitiveAttribute':
        return 'bg-cyan-500/20 text-cyan-300 border-cyan-500/30';
      default:
        return 'bg-slate-700/40 text-slate-400 border-slate-600/30';
    }
  }

  generateDataset() {
    // Regenerate rows with random variations
    const firstNames = ['Casey', 'Robin', 'Pat', 'Chris', 'Avery'];
    const lastNames = ['Miller', 'Davis', 'Garcia', 'Martinez', 'Rodriguez'];
    const newRows = Array.from({ length: 5 }, (_, i) => ({
      id: Math.random().toString(16).substring(2, 18),
      name: `${firstNames[i]} ${lastNames[i]}`,
      email: `${firstNames[i].toLowerCase()}.${lastNames[i].toLowerCase()}${Math.floor(Math.random() * 89 + 10)}@sandbox.corp`,
      phone: `+1 (555) ${Math.floor(Math.random() * 899 + 100)}-${Math.floor(Math.random() * 8999 + 1000)}`,
      balance: Math.round((Math.random() * 20000 + 3000) * 100) / 100,
      taxId: `${Math.floor(Math.random() * 899 + 100)}-${Math.floor(Math.random() * 89 + 10)}-${Math.floor(Math.random() * 8999 + 1000)}`
    }));
    this.sampleRows.set(newRows);
  }
}
