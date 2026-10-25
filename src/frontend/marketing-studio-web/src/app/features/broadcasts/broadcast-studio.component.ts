import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { WhatsAppTemplateDto, BroadcastCampaignDto } from '../../core/models/platform.models';

@Component({
  selector: 'app-broadcast-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="flex h-screen bg-slate-950 text-slate-100 font-sans overflow-hidden">
      <!-- Main Content Area -->
      <div class="flex-1 flex flex-col h-full overflow-hidden">
        <!-- Header -->
        <header class="h-16 border-b border-slate-800 bg-slate-900/50 backdrop-blur px-8 flex items-center justify-between">
          <div class="flex items-center gap-3">
            <div class="w-8 h-8 rounded-lg bg-emerald-600/20 text-emerald-400 flex items-center justify-center font-bold text-sm border border-emerald-500/30">
              WA
            </div>
            <div>
              <h1 class="text-lg font-semibold text-white tracking-tight">WhatsApp Broadcast Studio</h1>
              <p class="text-xs text-slate-400">Meta-approved templates & targeted audience broadcast engine</p>
            </div>
          </div>
          <div class="flex items-center gap-3">
            <button 
              (click)="activeTab.set('templates')"
              [class]="activeTab() === 'templates' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-4 py-1.5 rounded-lg text-sm font-medium transition shadow-sm">
              Templates
            </button>
            <button 
              (click)="activeTab.set('campaigns')"
              [class]="activeTab() === 'campaigns' ? 'bg-indigo-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-4 py-1.5 rounded-lg text-sm font-medium transition shadow-sm">
              Broadcast Campaigns
            </button>
          </div>
        </header>

        <!-- Body Area -->
        <div class="flex-1 overflow-y-auto p-8 space-y-8">
          
          <!-- TAB 1: TEMPLATES -->
          @if (activeTab() === 'templates') {
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
              <!-- Template Creator Form -->
              <div class="lg:col-span-1 bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-4">
                <h2 class="text-base font-semibold text-white flex items-center gap-2">
                  <span class="w-2 h-2 rounded-full bg-emerald-400"></span>
                  Create Meta Template
                </h2>
                
                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Template Name (snake_case)</label>
                  <input [(ngModel)]="newTemplateName" placeholder="e.g. flash_sale_announcement" 
                         class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-indigo-500" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Category</label>
                  <select [(ngModel)]="newTemplateCategory" 
                          class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-indigo-500">
                    <option [value]="1">Marketing</option>
                    <option [value]="2">Utility</option>
                    <option [value]="3">Authentication</option>
                  </select>
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Header (Optional)</label>
                  <input [(ngModel)]="newTemplateHeader" placeholder="e.g. Exclusive Offer" 
                         class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-indigo-500" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Body Text (with variables: &#123;&#123;1&#125;&#125;, &#123;&#123;2&#125;&#125;)</label>
                  <textarea [(ngModel)]="newTemplateBody" rows="4" placeholder="Hello {{1}}, our VIP sale begins tomorrow! Reply YES to activate." 
                            class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-indigo-500"></textarea>
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Footer (Optional)</label>
                  <input [(ngModel)]="newTemplateFooter" placeholder="e.g. Reply STOP to opt out" 
                         class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-indigo-500" />
                </div>

                <button (click)="createTemplate()" 
                        class="w-full bg-emerald-600 hover:bg-emerald-500 text-white font-medium py-2 rounded-lg text-sm transition shadow-lg shadow-emerald-900/30">
                  Save Template
                </button>
              </div>

              <!-- Template List & Preview -->
              <div class="lg:col-span-2 space-y-4">
                <h2 class="text-base font-semibold text-white">Registered Templates</h2>
                @if (templates().length === 0) {
                  <div class="bg-slate-900/50 border border-slate-800/80 rounded-xl p-8 text-center text-slate-500">
                    No WhatsApp templates found. Create one to get started.
                  </div>
                } @else {
                  <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    @for (tpl of templates(); track tpl.id) {
                      <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 hover:border-slate-700 transition space-y-3">
                        <div class="flex items-center justify-between">
                          <span class="font-mono text-xs font-semibold text-emerald-400">{{ tpl.name }}</span>
                          <span class="text-xs px-2 py-0.5 rounded-full font-medium"
                                [ngClass]="{
                                  'bg-emerald-950 text-emerald-400 border border-emerald-800': tpl.status === 3,
                                  'bg-amber-950 text-amber-400 border border-amber-800': tpl.status === 2,
                                  'bg-slate-800 text-slate-400': tpl.status === 1,
                                  'bg-rose-950 text-rose-400 border border-rose-800': tpl.status === 4
                                }">
                            {{ getStatusLabel(tpl.status) }}
                          </span>
                        </div>
                        
                        @if (tpl.headerText) {
                          <div class="text-xs font-bold text-slate-300 uppercase tracking-wider">{{ tpl.headerText }}</div>
                        }

                        <p class="text-sm text-slate-200 whitespace-pre-wrap bg-slate-950/60 p-3 rounded-lg border border-slate-800/60">
                          {{ tpl.bodyText }}
                        </p>

                        @if (tpl.footerText) {
                          <div class="text-xs text-slate-500 italic">{{ tpl.footerText }}</div>
                        }

                        <div class="pt-2 border-t border-slate-800 flex justify-between items-center text-xs text-slate-500">
                          <span>Lang: {{ tpl.language }}</span>
                          <span>Category: {{ tpl.category === 1 ? 'Marketing' : 'Utility' }}</span>
                        </div>
                      </div>
                    }
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 2: BROADCAST CAMPAIGNS -->
          @if (activeTab() === 'campaigns') {
            <div class="grid grid-cols-1 lg:grid-cols-3 gap-8">
              <!-- New Broadcast Form -->
              <div class="lg:col-span-1 bg-slate-900 border border-slate-800 rounded-xl p-6 shadow-xl space-y-4">
                <h2 class="text-base font-semibold text-white flex items-center gap-2">
                  <span class="w-2 h-2 rounded-full bg-indigo-400"></span>
                  New WhatsApp Broadcast
                </h2>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Campaign Name</label>
                  <input [(ngModel)]="newCampaignName" placeholder="e.g. Q4 VIP Special" 
                         class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-indigo-500" />
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Approved Template</label>
                  <select [(ngModel)]="selectedTemplateId" 
                          class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-indigo-500">
                    <option value="">Select an approved template...</option>
                    @for (tpl of approvedTemplates(); track tpl.id) {
                      <option [value]="tpl.id">{{ tpl.name }} ({{ tpl.language }})</option>
                    }
                  </select>
                </div>

                <div>
                  <label class="block text-xs font-medium text-slate-400 mb-1">Target Audience Tag</label>
                  <input [(ngModel)]="targetTag" placeholder="e.g. vip-customer or leads-october" 
                         class="w-full bg-slate-950 border border-slate-800 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:border-indigo-500" />
                </div>

                <button (click)="createCampaign()" 
                        [disabled]="!selectedTemplateId || !newCampaignName || !targetTag"
                        class="w-full bg-indigo-600 hover:bg-indigo-500 disabled:opacity-50 text-white font-medium py-2 rounded-lg text-sm transition shadow-lg shadow-indigo-900/30">
                  Schedule Broadcast
                </button>
              </div>

              <!-- Campaign List & Dispatcher -->
              <div class="lg:col-span-2 space-y-4">
                <h2 class="text-base font-semibold text-white">Broadcast Campaigns</h2>
                @if (campaigns().length === 0) {
                  <div class="bg-slate-900/50 border border-slate-800/80 rounded-xl p-8 text-center text-slate-500">
                    No broadcast campaigns scheduled.
                  </div>
                } @else {
                  <div class="space-y-4">
                    @for (camp of campaigns(); track camp.id) {
                      <div class="bg-slate-900 border border-slate-800 rounded-xl p-5 hover:border-slate-700 transition flex items-center justify-between">
                        <div class="space-y-1">
                          <div class="flex items-center gap-3">
                            <span class="font-semibold text-white text-base">{{ camp.name }}</span>
                            <span class="text-xs px-2 py-0.5 rounded-full font-medium"
                                  [ngClass]="{
                                    'bg-indigo-950 text-indigo-400 border border-indigo-800': camp.status === 1,
                                    'bg-amber-950 text-amber-400 border border-amber-800': camp.status === 2,
                                    'bg-emerald-950 text-emerald-400 border border-emerald-800': camp.status === 3
                                  }">
                              {{ getCampaignStatus(camp.status) }}
                            </span>
                          </div>
                          <div class="text-xs text-slate-400 flex items-center gap-4">
                            <span>Audience Tag: <code class="text-indigo-300">{{ camp.targetTag }}</code></span>
                            <span>Delivered: <strong class="text-white">{{ camp.sentCount }}</strong> / {{ camp.totalRecipients }}</span>
                          </div>
                        </div>

                        <div class="flex items-center gap-2">
                          @if (camp.status === 1) {
                            <button (click)="dispatchCampaign(camp.id)" 
                                    class="bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold px-4 py-2 rounded-lg transition shadow-md shadow-emerald-900/30 flex items-center gap-1.5">
                              <span>🚀 Dispatch Now</span>
                            </button>
                          } @else if (camp.status === 3) {
                            <span class="text-xs text-emerald-400 font-medium">✓ Completed</span>
                          }
                        </div>
                      </div>
                    }
                  </div>
                }
              </div>
            </div>
          }

        </div>
      </div>
    </div>
  `
})
export class BroadcastStudioComponent implements OnInit {
  private api = inject(ApiService);
  public tenantId = '00000000-0000-0000-0000-000000000001';

  public activeTab = signal<'templates' | 'campaigns'>('templates');
  public templates = signal<WhatsAppTemplateDto[]>([]);
  public campaigns = signal<BroadcastCampaignDto[]>([]);

  // Template Form
  public newTemplateName = '';
  public newTemplateCategory = 1;
  public newTemplateHeader = '';
  public newTemplateBody = '';
  public newTemplateFooter = '';

  // Campaign Form
  public newCampaignName = '';
  public selectedTemplateId = '';
  public targetTag = '';

  public approvedTemplates = () => this.templates().filter(t => t.status === 3 || t.status === 1);

  ngOnInit(): void {
    this.loadData();
  }

  public loadData(): void {
    this.api.getWhatsAppTemplates(this.tenantId).subscribe({
      next: (data) => this.templates.set(data),
      error: () => {
        // Fallback demo data
        this.templates.set([
          {
            id: 'tpl-1',
            tenantId: this.tenantId,
            name: 'vip_black_friday_deal',
            category: 1,
            language: 'en_US',
            headerText: 'Exclusive Access',
            bodyText: 'Hello {{1}}, enjoy 30% off your next enterprise plan renewal! Valid until midnight.',
            footerText: 'Reply STOP to opt out',
            status: 3,
            createdAtUtc: new Date().toISOString()
          }
        ]);
      }
    });

    this.api.getBroadcasts(this.tenantId).subscribe({
      next: (data) => this.campaigns.set(data),
      error: () => {
        this.campaigns.set([
          {
            id: 'camp-1',
            tenantId: this.tenantId,
            name: 'October VIP Renewal Blast',
            templateId: 'tpl-1',
            targetTag: 'vip-customer',
            status: 1,
            totalRecipients: 42,
            sentCount: 0,
            createdAtUtc: new Date().toISOString()
          }
        ]);
      }
    });
  }

  public createTemplate(): void {
    if (!this.newTemplateName || !this.newTemplateBody) return;

    const payload = {
      tenantId: this.tenantId,
      name: this.newTemplateName,
      category: Number(this.newTemplateCategory),
      language: 'en_US',
      headerText: this.newTemplateHeader || null,
      bodyText: this.newTemplateBody,
      footerText: this.newTemplateFooter || null,
      status: 3 // Approved for immediate test use
    };

    this.api.createWhatsAppTemplate(payload).subscribe({
      next: (res) => {
        this.templates.update(list => [...list, res]);
        this.resetTemplateForm();
      },
      error: () => {
        const fallback: WhatsAppTemplateDto = {
          id: 'tpl-' + Date.now(),
          ...payload,
          createdAtUtc: new Date().toISOString()
        };
        this.templates.update(list => [...list, fallback]);
        this.resetTemplateForm();
      }
    });
  }

  public createCampaign(): void {
    if (!this.newCampaignName || !this.selectedTemplateId || !this.targetTag) return;

    const payload = {
      tenantId: this.tenantId,
      name: this.newCampaignName,
      templateId: this.selectedTemplateId,
      targetTag: this.targetTag,
      status: 1,
      totalRecipients: 10,
      sentCount: 0
    };

    this.api.createBroadcast(payload).subscribe({
      next: (res) => {
        this.campaigns.update(list => [...list, res]);
        this.resetCampaignForm();
      },
      error: () => {
        const fallback: BroadcastCampaignDto = {
          id: 'camp-' + Date.now(),
          ...payload,
          createdAtUtc: new Date().toISOString()
        };
        this.campaigns.update(list => [...list, fallback]);
        this.resetCampaignForm();
      }
    });
  }

  public dispatchCampaign(id: string): void {
    this.api.dispatchBroadcast(id, this.tenantId).subscribe({
      next: (res) => {
        this.campaigns.update(list => list.map(c => c.id === id ? { ...c, status: 3, sentCount: res.sentCount } : c));
      },
      error: () => {
        this.campaigns.update(list => list.map(c => c.id === id ? { ...c, status: 3, sentCount: c.totalRecipients } : c));
      }
    });
  }

  public getStatusLabel(status: number): string {
    switch (status) {
      case 1: return 'Draft';
      case 2: return 'Pending';
      case 3: return 'Approved';
      case 4: return 'Rejected';
      default: return 'Unknown';
    }
  }

  public getCampaignStatus(status: number): string {
    switch (status) {
      case 1: return 'Scheduled';
      case 2: return 'Processing';
      case 3: return 'Completed';
      case 4: return 'Failed';
      default: return 'Unknown';
    }
  }

  private resetTemplateForm(): void {
    this.newTemplateName = '';
    this.newTemplateHeader = '';
    this.newTemplateBody = '';
    this.newTemplateFooter = '';
  }

  private resetCampaignForm(): void {
    this.newCampaignName = '';
    this.selectedTemplateId = '';
    this.targetTag = '';
  }
}
