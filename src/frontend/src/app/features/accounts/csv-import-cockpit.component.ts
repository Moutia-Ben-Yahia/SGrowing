import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { CsvImportPreviewResult, CsvImportExecutionResult } from '../../core/models/pillars.models';

@Component({
  selector: 'app-csv-import-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/accounts" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← CRM Accounts
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              📊 High-Throughput CSV Importer & Duplicate Resolver
            </span>
            <span class="bg-blue-500/20 text-blue-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-blue-500/30">
              Heuristic Identity • Batch Rollback
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="previewCsv()"
              [disabled]="isPreviewing()"
              class="px-3.5 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded-lg text-xs font-semibold transition">
              {{ isPreviewing() ? 'Inspecting...' : '1. Parse & Preview CSV' }}
            </button>
            <button
              (click)="executeImport()"
              [disabled]="isImporting() || !previewData()"
              class="px-3.5 py-1.5 bg-blue-600 hover:bg-blue-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition flex items-center gap-1.5">
              <span>🚀</span>
              <span>{{ isImporting() ? 'Importing Batch...' : '2. Run Transactional Import' }}</span>
            </button>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- Step 1: Raw CSV Content and Mapping Options -->
          <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- Left: Raw CSV Editor -->
            <div class="lg:col-span-2 p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3">
              <div class="flex items-center justify-between">
                <h3 class="text-sm font-semibold text-white">Paste CSV Data or Sample Leads</h3>
                <span class="text-xs text-slate-400 font-mono">RFC 4180 Format</span>
              </div>

              <textarea
                [(ngModel)]="csvRaw"
                rows="8"
                class="w-full bg-slate-950 border border-slate-800 rounded-lg p-3 text-xs font-mono text-slate-200 focus:outline-none focus:border-blue-500"></textarea>

              <div class="flex gap-4 text-xs text-slate-400">
                <label class="flex items-center gap-1.5 cursor-pointer">
                  <input type="radio" [(ngModel)]="delimiter" value="," class="text-blue-600" /> Comma (,)
                </label>
                <label class="flex items-center gap-1.5 cursor-pointer">
                  <input type="radio" [(ngModel)]="delimiter" value=";" class="text-blue-600" /> Semicolon (;)
                </label>
                <label class="flex items-center gap-1.5 cursor-pointer">
                  <input type="radio" [(ngModel)]="delimiter" value="\t" class="text-blue-600" /> Tab (TSV)
                </label>
              </div>
            </div>

            <!-- Right: Dynamic Column Mapping Config -->
            <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3">
              <h3 class="text-sm font-semibold text-white">Dynamic Column Mapping</h3>

              <div class="space-y-2 text-xs">
                <div>
                  <label class="text-[10px] text-slate-400 block mb-0.5">Email Column (Required)</label>
                  <input type="text" [(ngModel)]="mapEmail" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                </div>
                <div>
                  <label class="text-[10px] text-slate-400 block mb-0.5">First Name Column</label>
                  <input type="text" [(ngModel)]="mapFirst" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                </div>
                <div>
                  <label class="text-[10px] text-slate-400 block mb-0.5">Phone Column</label>
                  <input type="text" [(ngModel)]="mapPhone" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                </div>
                <div>
                  <label class="text-[10px] text-slate-400 block mb-0.5">Company / Org Column</label>
                  <input type="text" [(ngModel)]="mapCompany" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                </div>
                <div>
                  <label class="text-[10px] text-slate-400 block mb-0.5">Duplicate Resolution Rule</label>
                  <select [(ngModel)]="mergeStrategy" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200">
                    <option [value]="0">Skip Duplicate Contact</option>
                    <option [value]="1">Overwrite Existing Fields</option>
                    <option [value]="2">Update Blank Fields Only (Safe Merge)</option>
                  </select>
                </div>
              </div>
            </div>
          </div>

          <!-- Step 2: Preview Results -->
          @if (previewData()) {
            <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
              <div class="flex items-center justify-between">
                <h4 class="text-sm font-semibold text-white">Parsed File Structure: {{ previewData()?.totalRows }} Rows Detected</h4>
                <div class="flex gap-2">
                  @for (col of previewData()?.detectedHeaders; track col) {
                    <span class="bg-slate-800 text-slate-300 text-[10px] font-mono px-2 py-0.5 rounded">{{ col }}</span>
                  }
                </div>
              </div>

              <!-- Sample Rows Table -->
              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs border border-slate-800 rounded-lg overflow-hidden">
                  <thead class="bg-slate-950 text-slate-400 font-mono">
                    <tr>
                      @for (col of previewData()?.detectedHeaders; track col) {
                        <th class="p-2 border-b border-slate-800">{{ col }}</th>
                      }
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60">
                    @for (row of previewData()?.sampleRows; track $index) {
                      <tr class="hover:bg-slate-900/50">
                        @for (col of previewData()?.detectedHeaders; track col) {
                          <td class="p-2 text-slate-300 font-mono text-[11px]">{{ row[col] }}</td>
                        }
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }

          <!-- Step 3: Execution Result -->
          @if (executionResult()) {
            <div class="p-5 rounded-xl border" [class]="executionResult()?.wasRolledBack ? 'bg-rose-950/20 border-rose-800/40' : 'bg-emerald-950/20 border-emerald-800/40'">
              <div class="flex items-center justify-between mb-3">
                <h4 class="text-sm font-semibold" [class]="executionResult()?.wasRolledBack ? 'text-rose-400' : 'text-emerald-400'">
                  {{ executionResult()?.wasRolledBack ? '⚠️ Batch Aborted & Rolled Back' : '✅ Batch Successfully Committed' }}
                </h4>
                <span class="text-xs font-mono text-slate-300">
                  {{ executionResult()?.statusSummary }}
                </span>
              </div>

              <div class="grid grid-cols-4 gap-3 text-xs pt-2 border-t border-slate-800">
                <div><span class="text-slate-500 block text-[10px]">Processed</span><strong class="text-white">{{ executionResult()?.totalProcessed }}</strong></div>
                <div><span class="text-slate-500 block text-[10px]">Inserted</span><strong class="text-emerald-400">{{ executionResult()?.insertedCount }}</strong></div>
                <div><span class="text-slate-500 block text-[10px]">Updated / Merged</span><strong class="text-blue-400">{{ executionResult()?.updatedCount }}</strong></div>
                <div><span class="text-slate-500 block text-[10px]">Errors</span><strong class="text-rose-400">{{ executionResult()?.errorCount }}</strong></div>
              </div>
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class CsvImportCockpitComponent {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public csvRaw = `"Work Email","First Name","Last Name","Phone","Company","TaxId"
"clara@saasventures.io","Clara","Oswald","+447700900123","SaaS Ventures","GB123456789"
"david@stellarscale.net","David","Tennant","+447700900456","Stellar Scale","GB987654321"
"rose@badwolf.org","Rose","Tyler","+447700900789","Bad Wolf Corp","GB554433221"`;

  public delimiter = ',';
  public mapEmail = 'Work Email';
  public mapFirst = 'First Name';
  public mapPhone = 'Phone';
  public mapCompany = 'Company';
  public mergeStrategy = 2; // UpdateBlankFieldsOnly

  public isPreviewing = signal(false);
  public previewData = signal<CsvImportPreviewResult | null>(null);

  public isImporting = signal(false);
  public executionResult = signal<CsvImportExecutionResult | null>(null);

  public previewCsv() {
    this.isPreviewing.set(true);
    this.api.previewCsvImport(this.csvRaw, this.delimiter).subscribe({
      next: res => {
        this.previewData.set(res);
        this.isPreviewing.set(false);
      },
      error: () => this.isPreviewing.set(false)
    });
  }

  public executeImport() {
    this.isImporting.set(true);
    const tenantId = this.auth.currentUser()?.tenantId || '00000000-0000-0000-0000-000000000000';
    this.api.executeCsvImport(tenantId, {
      csvContent: this.csvRaw,
      mapping: {
        emailColumn: this.mapEmail,
        firstNameColumn: this.mapFirst,
        phoneColumn: this.mapPhone,
        companyColumn: this.mapCompany
      },
      options: {
        delimiter: this.delimiter,
        mergeStrategy: Number(this.mergeStrategy),
        rollbackOnError: true,
        maxErrorPercentageTolerance: 15.0
      }
    }).subscribe({
      next: res => {
        this.executionResult.set(res);
        this.isImporting.set(false);
      },
      error: () => this.isImporting.set(false)
    });
  }
}
