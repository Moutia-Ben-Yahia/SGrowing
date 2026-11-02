import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface GeneratedAppUi {
  id: string;
  name: string;
  slug: string;
  description: string;
  permissions: string;
  fieldCount: number;
}

export interface AppRecordUi {
  id: string;
  company: string;
  tier: string;
  contractValue: number;
  status: string;
}

@Component({
  selector: 'app-app-generator-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="app-generator-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">Autonomous Micro-SaaS App Generator & Schema Studio</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800">
              Instant Schema-to-UI Active
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            Generate custom data schemas, automated REST CRUD endpoints, and responsive portals without writing frontend code.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="showCreateAppModal.set(true)"
            class="px-4 py-2 bg-amber-600 hover:bg-amber-700 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-open-create-app">
            + Generate Micro-App
          </button>
        </div>
      </div>

      <!-- Generator KPIs -->
      <div class="grid grid-cols-1 sm:grid-cols-4 gap-4" data-testid="app-builder-kpis">
        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Published Micro-Apps</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-apps-count">
            {{ apps().length }}
          </div>
          <div class="text-[11px] text-amber-600 mt-1 font-semibold">Self-Hosted Schemas</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Total Live Records</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-records-count">
            {{ records().length }}
          </div>
          <div class="text-[11px] text-emerald-600 mt-1 font-semibold">Indexed in Real-Time</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Auto-Synthesized APIs</div>
          <div class="text-2xl font-bold text-emerald-600 mt-1 font-mono" data-testid="kpi-api-coverage">100%</div>
          <div class="text-[11px] text-slate-400 mt-1">Full OpenAPI Contracts</div>
        </div>

        <div class="bg-white border rounded-xl p-4 shadow-sm">
          <div class="text-xs text-slate-500 font-medium">Total Contract Volume</div>
          <div class="text-2xl font-bold text-slate-900 mt-1 font-mono" data-testid="kpi-total-val">
            \${{ (totalPipelineVal() / 1000) | number:'1.0-0' }}k
          </div>
          <div class="text-[11px] text-indigo-600 mt-1 font-semibold">Managed Portfolio</div>
        </div>
      </div>

      <!-- Main Columns -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Apps Directory -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">Active Generated Micro-Apps</h3>
            <span class="text-xs text-slate-400 font-mono">{{ apps().length }} apps</span>
          </div>

          <div class="space-y-3" data-testid="apps-list">
            @for (appItem of apps(); track appItem.id) {
              <div class="p-3 border rounded-lg hover:border-amber-400 transition-colors space-y-1.5 cursor-pointer">
                <div class="flex justify-between items-center text-xs">
                  <span class="font-bold text-slate-800">{{ appItem.name }}</span>
                  <span class="px-2 py-0.5 rounded text-[10px] font-mono font-bold bg-amber-50 text-amber-700">
                    {{ appItem.permissions }}
                  </span>
                </div>
                <p class="text-[11px] text-slate-500">{{ appItem.description }}</p>
                <div class="flex justify-between items-center text-[10px] text-slate-400 pt-1 border-t font-mono">
                  <span>/portal/{{ appItem.slug }}</span>
                  <span>{{ appItem.fieldCount }} Schema Fields</span>
                </div>
              </div>
            }
          </div>
        </div>

        <!-- Live Generated Grid -->
        <div class="lg:col-span-2 bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between">
            <h3 class="font-bold text-slate-900 text-sm">VIP Client Onboarding Tracker (Auto-Generated UI)</h3>
            <button 
              (click)="insertQuickRecord()"
              class="px-3 py-1.5 bg-slate-900 hover:bg-slate-800 text-white rounded text-xs font-medium transition-colors shadow-sm"
              data-testid="btn-insert-record">
              + New Record
            </button>
          </div>

          <div class="overflow-x-auto" data-testid="records-table">
            <table class="w-full text-left text-xs">
              <thead class="bg-slate-50 border-b text-slate-500">
                <tr>
                  <th class="p-3">Company</th>
                  <th class="p-3">Tier</th>
                  <th class="p-3">Annual Contract Value</th>
                  <th class="p-3">Provisioning Status</th>
                </tr>
              </thead>
              <tbody class="divide-y">
                @for (rec of records(); track rec.id) {
                  <tr class="hover:bg-slate-50">
                    <td class="p-3 font-bold text-slate-900">{{ rec.company }}</td>
                    <td class="p-3">
                      <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-indigo-50 text-indigo-700 font-mono">
                        {{ rec.tier }}
                      </span>
                    </td>
                    <td class="p-3 font-mono font-bold text-emerald-700">
                      \${{ rec.contractValue | number:'1.0-0' }}
                    </td>
                    <td class="p-3">
                      <span class="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800">
                        {{ rec.status }}
                      </span>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Create App Modal -->
      @if (showCreateAppModal()) {
        <div class="fixed inset-0 bg-slate-900/40 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-white rounded-2xl max-w-md w-full p-6 space-y-4 shadow-xl border">
            <div class="flex items-center justify-between border-b pb-3">
              <h3 class="font-bold text-slate-900 text-base">Generate New Micro-SaaS App</h3>
              <button (click)="showCreateAppModal.set(false)" class="text-slate-400 hover:text-slate-600">&times;</button>
            </div>

            <div class="space-y-3 text-xs">
              <div>
                <label class="block text-slate-700 font-semibold mb-1">Application Name</label>
                <input 
                  type="text" 
                  [(ngModel)]="newAppName" 
                  class="w-full px-3 py-2 border rounded-lg font-mono focus:ring-2 focus:ring-amber-500 focus:outline-none"
                  data-testid="input-app-name" />
              </div>

              <div>
                <label class="block text-slate-700 font-semibold mb-1">Description</label>
                <textarea 
                  [(ngModel)]="newAppDesc" 
                  rows="2"
                  class="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-amber-500 focus:outline-none"></textarea>
              </div>

              <div>
                <label class="block text-slate-700 font-semibold mb-1">Access Permissions</label>
                <select 
                  [(ngModel)]="newAppPermissions" 
                  class="w-full px-3 py-2 border rounded-lg focus:ring-2 focus:ring-amber-500 focus:outline-none">
                  <option value="InternalEmployees">Internal Employees</option>
                  <option value="ClientPortal">Client Portal</option>
                  <option value="Public">Public Directory</option>
                </select>
              </div>
            </div>

            <div class="flex justify-end gap-2 pt-3 border-t">
              <button 
                (click)="showCreateAppModal.set(false)"
                class="px-3 py-1.5 border rounded-lg text-xs font-medium text-slate-600 hover:bg-slate-50">
                Cancel
              </button>
              <button 
                (click)="generateApp()"
                class="px-4 py-1.5 bg-amber-600 hover:bg-amber-700 text-white rounded-lg text-xs font-medium shadow-sm"
                data-testid="btn-submit-app">
                Generate & Deploy
              </button>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class AppGeneratorCockpitComponent {
  showCreateAppModal = signal(false);

  newAppName = 'Partner Referral Directory';
  newAppDesc = 'Directory of certified agency implementation partners';
  newAppPermissions = 'Public';

  apps = signal<GeneratedAppUi[]>([
    { id: 'app_1', name: 'VIP Client Onboarding Tracker', slug: 'vip-onboarding', description: 'Tracks high-touch enterprise customer provisioning milestones', permissions: 'InternalEmployees', fieldCount: 4 }
  ]);

  records = signal<AppRecordUi[]>([
    { id: 'rec_1', company: 'Stripe Inc', tier: 'Enterprise', contractValue: 120000, status: 'Live' },
    { id: 'rec_2', company: 'Datadog Corp', tier: 'Enterprise', contractValue: 85000, status: 'TechnicalIntegration' }
  ]);

  totalPipelineVal = computed(() => {
    return this.records().reduce((sum, r) => sum + r.contractValue, 0);
  });

  generateApp() {
    if (!this.newAppName) return;

    const newApp: GeneratedAppUi = {
      id: 'app_' + Date.now(),
      name: this.newAppName,
      slug: this.newAppName.toLowerCase().replace(/ /g, '-'),
      description: this.newAppDesc,
      permissions: this.newAppPermissions,
      fieldCount: 3
    };

    this.apps.update(list => [...list, newApp]);
    this.showCreateAppModal.set(false);
  }

  insertQuickRecord() {
    const newRecord: AppRecordUi = {
      id: 'rec_' + Date.now(),
      company: 'Snowflake Inc',
      tier: 'Enterprise',
      contractValue: 240000,
      status: 'Live'
    };

    this.records.update(list => [newRecord, ...list]);
  }
}
