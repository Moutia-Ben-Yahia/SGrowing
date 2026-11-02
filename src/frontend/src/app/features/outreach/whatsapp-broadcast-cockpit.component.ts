import { Component, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface WhatsAppTemplateItem {
  templateId: string;
  name: string;
  category: 'MARKETING' | 'UTILITY' | 'AUTHENTICATION';
  headerText: string;
  bodyText: string;
  buttons: string[];
  status: 'Approved' | 'Pending' | 'Rejected';
}

export interface BroadcastSummary {
  campaignId: string;
  name: string;
  total: number;
  sent: number;
  delivered: number;
  read: number;
  status: 'Draft' | 'Broadcasting' | 'Completed';
}

@Component({
  selector: 'app-whatsapp-broadcast-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="whatsapp-broadcast-cockpit p-6 max-w-7xl mx-auto space-y-6" dir="ltr">
      <!-- Header -->
      <div class="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b pb-6">
        <div>
          <div class="flex items-center gap-3">
            <h1 class="text-2xl font-bold tracking-tight text-slate-900">WhatsApp Cloud Broadcast Mesh</h1>
            <span class="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
              Meta Cloud API &bull; Tier 3 (100k/day)
            </span>
          </div>
          <p class="text-sm text-slate-500 mt-1">
            High-throughput transactional & marketing broadcasts with interactive mobile template previews and real-time read receipts.
          </p>
        </div>

        <div class="flex items-center gap-3">
          <button 
            (click)="dispatchCampaign()"
            [disabled]="campaign().status === 'Completed'"
            class="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 disabled:bg-slate-300 text-white rounded-lg font-medium text-xs transition-colors shadow-sm"
            data-testid="btn-dispatch-broadcast">
            ⚡ Launch Broadcast ({{ campaign().total | number }} Recipients)
          </button>
        </div>
      </div>

      <!-- Main Layout: Simulator Preview + Broadcast Metrics -->
      <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <!-- Mobile WhatsApp Chat Preview -->
        <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
          <div class="flex items-center justify-between pb-2 border-b">
            <h3 class="font-bold text-slate-900 text-sm">Interactive Phone Preview</h3>
            <span class="text-xs text-emerald-600 font-semibold flex items-center gap-1">● Online</span>
          </div>

          <!-- Phone Mockup Frame -->
          <div class="bg-[#e5ddd5] p-4 rounded-xl shadow-inner min-h-[380px] flex flex-col justify-between border-4 border-slate-700">
            <!-- Simulated WhatsApp Message Bubble -->
            <div class="bg-white rounded-lg p-3 shadow text-xs space-y-2 max-w-[90%] self-start border-l-4 border-l-emerald-500">
              <div class="font-bold text-slate-800 text-[13px]">
                {{ selectedTemplate().headerText }}
              </div>
              <p class="text-slate-600 leading-relaxed">
                {{ selectedTemplate().bodyText }}
              </p>
              <div class="text-[10px] text-slate-400 text-right">
                10:42 AM ✓✓
              </div>

              <!-- Interactive Buttons -->
              <div class="pt-2 border-t space-y-1.5">
                @for (btn of selectedTemplate().buttons; track btn) {
                  <button class="w-full py-1.5 text-center text-xs font-semibold text-emerald-700 bg-slate-50 hover:bg-slate-100 rounded border transition-colors">
                    {{ btn }}
                  </button>
                }
              </div>
            </div>

            <!-- Simulated Input bar -->
            <div class="bg-white/90 p-2 rounded-lg flex items-center justify-between text-xs text-slate-400">
              <span>Type a message...</span>
              <span>🎙️</span>
            </div>
          </div>
        </div>

        <!-- Real-Time Delivery Funnel & Campaign Telemetry -->
        <div class="lg:col-span-2 space-y-6">
          <!-- Delivery KPI Cards -->
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-4" data-testid="delivery-kpis">
            <div class="bg-white border rounded-xl p-4 shadow-sm">
              <span class="text-xs font-medium text-slate-400">Total Enqueued</span>
              <div class="text-xl font-bold text-slate-900 mt-1 font-mono">{{ campaign().total | number }}</div>
              <span class="text-[11px] text-slate-500 mt-1 block">Meta Verified</span>
            </div>

            <div class="bg-white border rounded-xl p-4 shadow-sm">
              <span class="text-xs font-medium text-slate-400">Dispatched (Sent)</span>
              <div class="text-xl font-bold text-indigo-600 mt-1 font-mono">{{ campaign().sent | number }}</div>
              <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">100% Rate Limit Safe</span>
            </div>

            <div class="bg-white border rounded-xl p-4 shadow-sm">
              <span class="text-xs font-medium text-slate-400">Delivered</span>
              <div class="text-xl font-bold text-emerald-600 mt-1 font-mono">{{ campaign().delivered | number }}</div>
              <span class="text-[11px] text-emerald-600 font-semibold mt-1 block">{{ deliveredPercent() }}% Success</span>
            </div>

            <div class="bg-white border rounded-xl p-4 shadow-sm">
              <span class="text-xs font-medium text-slate-400">Read (Opened)</span>
              <div class="text-xl font-bold text-slate-900 mt-1 font-mono">{{ campaign().read | number }}</div>
              <span class="text-[11px] text-indigo-600 font-semibold mt-1 block">{{ readPercent() }}% Open Rate</span>
            </div>
          </div>

          <!-- Funnel Progress Bars -->
          <div class="bg-white border rounded-xl p-5 shadow-sm space-y-4">
            <div class="flex items-center justify-between">
              <h3 class="font-bold text-slate-900 text-sm">Campaign Funnel Conversion</h3>
              <span class="text-xs font-semibold px-2 py-0.5 rounded"
                [ngClass]="campaign().status === 'Completed' ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-700'">
                {{ campaign().status }}
              </span>
            </div>

            <div class="space-y-3">
              <div>
                <div class="flex justify-between text-xs text-slate-600 mb-1">
                  <span>Delivered to Handset</span>
                  <span class="font-mono font-bold">{{ deliveredPercent() }}%</span>
                </div>
                <div class="w-full bg-slate-100 rounded-full h-2 overflow-hidden">
                  <div class="bg-emerald-500 h-2 rounded-full transition-all duration-500" [style.width.%]="deliveredPercent()"></div>
                </div>
              </div>

              <div>
                <div class="flex justify-between text-xs text-slate-600 mb-1">
                  <span>Read Receipts Received</span>
                  <span class="font-mono font-bold">{{ readPercent() }}%</span>
                </div>
                <div class="w-full bg-slate-100 rounded-full h-2 overflow-hidden">
                  <div class="bg-indigo-600 h-2 rounded-full transition-all duration-500" [style.width.%]="readPercent()"></div>
                </div>
              </div>
            </div>
          </div>

          <!-- Synchronized Message Templates -->
          <div class="bg-white border rounded-xl p-5 shadow-sm space-y-3">
            <h3 class="font-bold text-slate-900 text-sm">Synchronized Meta Templates</h3>
            <div class="divide-y divide-slate-100">
              @for (tmpl of templates(); track tmpl.templateId) {
                <div class="py-2.5 flex items-center justify-between hover:bg-slate-50 transition-colors px-2 rounded-lg">
                  <div>
                    <span class="font-mono text-xs font-semibold text-slate-900">{{ tmpl.name }}</span>
                    <span class="text-[11px] text-slate-400 block">{{ tmpl.category }} &bull; {{ tmpl.buttons.length }} Buttons</span>
                  </div>
                  <div class="flex items-center gap-3">
                    <span class="px-2 py-0.5 rounded text-[11px] font-semibold bg-emerald-100 text-emerald-800">
                      {{ tmpl.status }}
                    </span>
                    <button 
                      (click)="selectedTemplate.set(tmpl)"
                      class="px-2.5 py-1 text-xs text-indigo-600 hover:bg-indigo-50 rounded font-medium transition-colors">
                      Preview
                    </button>
                  </div>
                </div>
              }
            </div>
          </div>
        </div>
      </div>
    </div>
  `
})
export class WhatsAppBroadcastCockpitComponent {
  templates = signal<WhatsAppTemplateItem[]>([
    {
      templateId: 'tmpl_q4_flash_offer',
      name: 'q4_enterprise_flash_discount',
      category: 'MARKETING',
      headerText: '⚡ Exclusive Growth OS Enterprise Upgrade',
      bodyText: 'Hi Sarah, upgrade your team\'s autonomous growth fleet before Friday to unlock 25% ARR credit and dedicated SDR agents.',
      buttons: ['Claim Offer', 'Book Demo', 'Unsubscribe'],
      status: 'Approved'
    },
    {
      templateId: 'tmpl_order_delivery_otp',
      name: 'order_dispatch_verification',
      category: 'UTILITY',
      headerText: '📦 Your SGrowing Shipment is In Transit',
      bodyText: 'Hello Michael, order #SG-9821 has been dispatched. Track your delivery or confirm receiving PIN 4821.',
      buttons: ['Track Package', 'Contact Courier'],
      status: 'Approved'
    }
  ]);

  selectedTemplate = signal<WhatsAppTemplateItem>(this.templates()[0]);

  campaign = signal<BroadcastSummary>({
    campaignId: 'wabc_vip_launch_01',
    name: 'Q4 Enterprise Growth Upgrade',
    total: 25000,
    sent: 0,
    delivered: 0,
    read: 0,
    status: 'Draft'
  });

  deliveredPercent = computed(() => {
    const c = this.campaign();
    return c.total > 0 ? Math.round((c.delivered / c.total) * 100) : 0;
  });

  readPercent = computed(() => {
    const c = this.campaign();
    return c.total > 0 ? Math.round((c.read / c.total) * 100) : 0;
  });

  dispatchCampaign(): void {
    const total = this.campaign().total;
    const delivered = Math.round(total * 0.98);
    const read = Math.round(delivered * 0.82);

    this.campaign.set({
      campaignId: this.campaign().campaignId,
      name: this.campaign().name,
      total: total,
      sent: total,
      delivered: delivered,
      read: read,
      status: 'Completed'
    });
  }
}
