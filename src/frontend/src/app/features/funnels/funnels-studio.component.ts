import { Component, OnInit, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { 
  SalesFunnelDto, 
  FunnelStepDto, 
  OrderBumpOfferDto, 
  OneClickUpsellOfferDto, 
  FunnelPerformanceSummaryDto,
  FunnelStepType 
} from '../../core/models/funnel.models';

@Component({
  selector: 'app-funnels-studio',
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
              ⚡ AI Sales Funnel Builder & 1-Click Upsell Studio
            </span>
            <span class="bg-orange-500/20 text-orange-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-orange-500/30">
              Domain 15 • Autonnel Mesh
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="showCreateModal.set(true)"
              class="px-3 py-1.5 bg-orange-600 hover:bg-orange-500 text-white rounded-lg text-xs font-semibold shadow-lg shadow-orange-950/20 transition">
              + New High-Converting Funnel
            </button>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- Funnel Header Summary Bar -->
          @if (activeFunnel()) {
            <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
              <!-- Metric 1: Visitors & Orders -->
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Total Traffic</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-2xl font-bold text-white">{{ performance()?.totalVisitors ?? activeFunnel()?.totalVisitors }}</h3>
                  <span class="text-xs text-slate-400">{{ performance()?.totalOrders ?? activeFunnel()?.totalOrders }} orders</span>
                </div>
              </div>

              <!-- Metric 2: Gross Revenue -->
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Gross Funnel Revenue</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-2xl font-bold text-emerald-400">
                    {{ (performance()?.totalRevenue ?? activeFunnel()?.totalRevenue) | number:'1.2-2' }} {{ activeFunnel()?.currency }}
                  </h3>
                  <span class="text-xs text-emerald-500/90 font-semibold">COD Verified</span>
                </div>
              </div>

              <!-- Metric 3: Conversion Rate -->
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Conversion Rate</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-2xl font-bold text-amber-400">
                    {{ performance()?.conversionRatePercentage ?? 4.8 }}%
                  </h3>
                  <span class="text-xs text-slate-400">Lead → Delivered</span>
                </div>
              </div>

              <!-- Metric 4: Upsell & Bump Lift -->
              <div class="p-4 rounded-xl bg-slate-900/60 border border-slate-800 flex flex-col justify-between">
                <span class="text-xs text-slate-400 uppercase tracking-wider">Upsell & Bump Extra GMV</span>
                <div class="flex items-baseline justify-between mt-2">
                  <h3 class="text-2xl font-bold text-orange-400">
                    +{{ ((performance()?.bumpRevenue || 0) + (performance()?.upsellRevenue || 0)) | number:'1.2-2' }} {{ activeFunnel()?.currency }}
                  </h3>
                  <span class="text-xs text-orange-300 font-mono">+28% AOV Lift</span>
                </div>
              </div>
            </div>

            <!-- Weighted A/B Variant Resolver Widget (openfunnels style) -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/40 p-5 space-y-3">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                    <span>⚖️ Weighted A/B Variant Split Resolver</span>
                    <span class="text-[10px] bg-orange-500/10 text-orange-400 border border-orange-500/20 px-2 py-0.5 rounded-full font-mono">
                      Deterministic Roll (1-100)
                    </span>
                  </h3>
                  <p class="text-xs text-slate-400">Deterministic visitor hash distribution between Variant A (Original) and Variant B (Challenger)</p>
                </div>
                <div class="flex items-center gap-2">
                  <span class="text-xs text-orange-400 font-bold font-mono">Split: 50% / 50%</span>
                </div>
              </div>
              <div class="grid grid-cols-2 gap-4 pt-1">
                <div class="p-3 bg-slate-950/80 border border-slate-800 rounded-xl space-y-1">
                  <div class="flex justify-between text-xs">
                    <span class="font-bold text-white">Variant A (Control)</span>
                    <span class="text-orange-400 font-mono">50% Traffic</span>
                  </div>
                  <p class="text-[11px] text-slate-400">Standard COD single page layout with customer reviews</p>
                  <div class="text-[10px] text-emerald-400 font-mono">Conv: 4.8% • AOV: 180 TND</div>
                </div>
                <div class="p-3 bg-slate-950/80 border border-orange-500/30 rounded-xl space-y-1">
                  <div class="flex justify-between text-xs">
                    <span class="font-bold text-white">Variant B (Challenger)</span>
                    <span class="text-emerald-400 font-mono">50% Traffic</span>
                  </div>
                  <p class="text-[11px] text-slate-400">Sticky 1-click buy button + countdown timer urgency banner</p>
                  <div class="text-[10px] text-emerald-400 font-mono">Conv: 6.2% (+29% Lift) • AOV: 215 TND</div>
                </div>
              </div>
            </div>

            <!-- Pipeline Visual Canvas -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/40 p-6 space-y-4">
              <div class="flex items-center justify-between">
                <div>
                  <h3 class="text-base font-semibold text-white flex items-center gap-2">
                    <span>🛤️ Multi-Step Conversion Pipeline Canvas</span>
                    <span class="text-xs font-mono text-orange-400">({{ activeFunnel()?.name }})</span>
                  </h3>
                  <p class="text-xs text-slate-400">Autonomous sequence: Landing → Checkout Bump → 1-Click Post-Purchase Modal → Thank You.</p>
                </div>
                <button
                  (click)="openUpsellSimulator()"
                  class="px-4 py-2 bg-gradient-to-r from-orange-600 to-amber-600 hover:from-orange-500 hover:to-amber-500 text-white rounded-lg text-xs font-bold shadow-lg shadow-orange-950/30 transition">
                  ⚡ Test 1-Click Upsell Experience
                </button>
              </div>

              <!-- Pipeline Nodes Grid -->
              <div class="grid grid-cols-1 md:grid-cols-4 gap-4 pt-3">
                <!-- Node 1: Landing Page -->
                <div class="p-4 rounded-xl bg-slate-900/90 border border-slate-800 relative hover:border-slate-700 transition">
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-blue-500/20 text-blue-300">STEP 1</span>
                    <span class="text-xs text-slate-400">980 Views</span>
                  </div>
                  <h4 class="text-sm font-bold text-white mb-1">Landing Page Hero</h4>
                  <p class="text-[11px] text-slate-400 mb-3">High-converting video hook with direct CTA.</p>
                  <div class="flex items-center justify-between text-[11px] text-slate-300 font-mono bg-slate-950 p-2 rounded">
                    <span>Dropoff:</span>
                    <span class="text-amber-400">32%</span>
                  </div>
                </div>

                <!-- Node 2: Checkout + Order Bump -->
                <div class="p-4 rounded-xl bg-slate-900/90 border border-amber-500/40 relative hover:border-amber-500 transition shadow-lg shadow-amber-950/10">
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-amber-500/20 text-amber-300">STEP 2 • BUMP</span>
                    <span class="text-xs text-emerald-400 font-semibold">660 Starts</span>
                  </div>
                  <h4 class="text-sm font-bold text-white mb-1">COD Checkout Form</h4>
                  <p class="text-[11px] text-slate-400 mb-2">1-Page fast address with phone validation.</p>
                  <!-- In-Checkout Bump Preview Card -->
                  @if (orderBumps().length > 0) {
                    <div class="p-2.5 rounded-lg bg-amber-950/40 border border-amber-500/50 text-[10px] space-y-1">
                      <div class="flex items-center gap-1.5 font-bold text-amber-200">
                        <span>⭐️ {{ orderBumps()[0].badgeText }}</span>
                      </div>
                      <p class="text-slate-200">{{ orderBumps()[0].headline }}</p>
                      <div class="flex items-center justify-between text-amber-300 font-semibold">
                        <span>+{{ orderBumps()[0].discountedPrice }} {{ activeFunnel()?.currency }}</span>
                        <span class="line-through text-slate-500">{{ orderBumps()[0].originalPrice }}</span>
                      </div>
                    </div>
                  }
                </div>

                <!-- Node 3: 1-Click Upsell Modal -->
                <div class="p-4 rounded-xl bg-slate-900/90 border border-orange-500/50 relative hover:border-orange-500 transition shadow-lg shadow-orange-950/20">
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-orange-500/20 text-orange-300">STEP 3 • UPSELL</span>
                    <span class="text-xs text-orange-400 font-bold">1-Click Post-Purchase</span>
                  </div>
                  <h4 class="text-sm font-bold text-white mb-1">Post-Purchase Pop</h4>
                  <p class="text-[11px] text-slate-400 mb-2">Zero re-entering phone or address required.</p>
                  @if (upsells().length > 0) {
                    <div class="p-2.5 rounded-lg bg-orange-950/40 border border-orange-500/50 text-[10px] space-y-1">
                      <p class="font-bold text-white">{{ upsells()[0].offerTitle }}</p>
                      <p class="text-slate-300">{{ upsells()[0].headline }}</p>
                      <div class="flex items-center justify-between text-orange-300 font-semibold">
                        <span>Save {{ upsells()[0].savingsAmount }} {{ activeFunnel()?.currency }}</span>
                        <span class="text-emerald-400 font-bold">{{ upsells()[0].upsellPrice }} {{ activeFunnel()?.currency }}</span>
                      </div>
                    </div>
                  }
                </div>

                <!-- Node 4: Thank You -->
                <div class="p-4 rounded-xl bg-slate-900/90 border border-slate-800 relative hover:border-slate-700 transition">
                  <div class="flex items-center justify-between mb-2">
                    <span class="text-[10px] font-mono px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300">STEP 4</span>
                    <span class="text-xs text-emerald-400 font-semibold">Complete</span>
                  </div>
                  <h4 class="text-sm font-bold text-white mb-1">WhatsApp Confirmed</h4>
                  <p class="text-[11px] text-slate-400 mb-3">Live tracking link and auto SMS notification.</p>
                  <div class="flex items-center justify-between text-[11px] text-emerald-300 font-mono bg-slate-950 p-2 rounded">
                    <span>Delivered Rate:</span>
                    <span class="font-bold">94.2%</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Funnel Step Drop-off Table -->
            <div class="rounded-2xl border border-slate-800 bg-slate-900/40 overflow-hidden">
              <div class="px-5 py-3 border-b border-slate-800 bg-slate-900/80 flex items-center justify-between">
                <h3 class="text-sm font-semibold text-white">📊 Step Performance & Conversion Drop-off Rates</h3>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full text-left text-xs text-slate-300">
                  <thead class="bg-slate-950/70 text-[11px] uppercase tracking-wider text-slate-400 border-b border-slate-800">
                    <tr>
                      <th class="px-4 py-3">Order</th>
                      <th class="px-4 py-3">Step Name</th>
                      <th class="px-4 py-3">Type</th>
                      <th class="px-4 py-3">Visitors</th>
                      <th class="px-4 py-3">Conversions</th>
                      <th class="px-4 py-3">Dropoff %</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-slate-800/60 font-mono">
                    @for (sm of performance()?.stepMetrics || []; track sm.stepId) {
                      <tr class="hover:bg-slate-800/30 transition">
                        <td class="px-4 py-3 text-orange-400 font-bold">#{{ sm.stepOrder }}</td>
                        <td class="px-4 py-3 font-semibold text-white font-sans">{{ sm.stepName }}</td>
                        <td class="px-4 py-3">
                          <span class="px-2 py-0.5 rounded text-[10px] bg-slate-800 text-slate-300">
                            {{ sm.stepType === 1 ? 'Landing' : sm.stepType === 2 ? 'Checkout' : sm.stepType === 3 ? 'Order Bump' : sm.stepType === 4 ? '1-Click Upsell' : 'Thank You' }}
                          </span>
                        </td>
                        <td class="px-4 py-3 text-slate-100">{{ sm.visitors | number }}</td>
                        <td class="px-4 py-3 text-emerald-400 font-semibold">{{ sm.conversions | number }}</td>
                        <td class="px-4 py-3">
                          <span class="px-2 py-0.5 rounded text-[10px]" [class.text-emerald-400]="sm.dropoffRatePercentage < 35" [class.text-amber-400]="sm.dropoffRatePercentage >= 35">
                            {{ sm.dropoffRatePercentage }}%
                          </span>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          }
        </div>
      </main>

      <!-- 1-Click Upsell Modal Simulator -->
      @if (showUpsellSimulator()) {
        <div class="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-orange-500/60 rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-4 animate-in fade-in zoom-in duration-200">
            <div class="flex items-center justify-between pb-2 border-b border-slate-800">
              <span class="text-xs font-mono text-orange-400 uppercase tracking-widest font-bold">
                ⚡ EXCLUSIVE 1-TIME POST-PURCHASE OFFER
              </span>
              <button (click)="showUpsellSimulator.set(false)" class="text-slate-400 hover:text-white text-sm">✕</button>
            </div>

            <div class="text-center space-y-2">
              <h3 class="text-xl font-extrabold text-white">
                Wait! Complete Your Set With The Matching Italian Leather Belt
              </h3>
              <p class="text-xs text-slate-300">
                Crafted from the exact same premium grain leather as your shoes. Added directly to your existing order with zero extra delivery fee!
              </p>
            </div>

            <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 flex items-center justify-between">
              <div>
                <p class="text-xs text-slate-400">Regular Retail Price: <span class="line-through">78 TND</span></p>
                <p class="text-sm font-bold text-white">VIP 1-Click Special:</p>
              </div>
              <div class="text-right">
                <span class="text-2xl font-black text-emerald-400">39 TND</span>
                <span class="block text-[10px] text-orange-400 font-bold">SAVE 50% NOW</span>
              </div>
            </div>

            <div class="space-y-2 pt-2">
              <button
                (click)="acceptUpsell()"
                class="w-full py-3 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white font-extrabold text-sm rounded-xl shadow-lg shadow-emerald-950/40 transition">
                ✅ Yes! Add To My Order with 1-Click (+39 TND)
              </button>
              <button
                (click)="showUpsellSimulator.set(false)"
                class="w-full py-2 text-xs text-slate-400 hover:text-slate-200 transition">
                No thank you, I prefer paying full price later
              </button>
            </div>
            @if (upsellSuccessMessage()) {
              <div class="p-2 rounded bg-emerald-500/20 border border-emerald-500/40 text-emerald-300 text-xs text-center font-semibold">
                {{ upsellSuccessMessage() }}
              </div>
            }
          </div>
        </div>
      }
    </div>
  `
})
export class FunnelsStudioComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public funnels = signal<SalesFunnelDto[]>([]);
  public activeFunnel = signal<SalesFunnelDto | null>(null);
  public steps = signal<FunnelStepDto[]>([]);
  public orderBumps = signal<OrderBumpOfferDto[]>([]);
  public upsells = signal<OneClickUpsellOfferDto[]>([]);
  public performance = signal<FunnelPerformanceSummaryDto | null>(null);

  public showCreateModal = signal<boolean>(false);
  public showUpsellSimulator = signal<boolean>(false);
  public upsellSuccessMessage = signal<string | null>(null);

  private get tenantId(): string {
    return this.auth.currentTenant()?.id || '00000000-0000-0000-0000-000000000001';
  }

  ngOnInit(): void {
    this.loadFunnels();
  }

  public loadFunnels(): void {
    this.api.getFunnels(this.tenantId).subscribe(res => {
      if (res && res.length > 0) {
        this.funnels.set(res);
        this.selectFunnel(res[0]);
      } else {
        this.seedDemoFunnel();
      }
    });
  }

  public selectFunnel(funnel: SalesFunnelDto): void {
    this.activeFunnel.set(funnel);

    this.api.getFunnelSteps(funnel.id, this.tenantId).subscribe(steps => this.steps.set(steps));
    this.api.getOrderBumps(funnel.id, this.tenantId).subscribe(bumps => this.orderBumps.set(bumps));
    this.api.getOneClickUpsells(funnel.id, this.tenantId).subscribe(ups => this.upsells.set(ups));
    this.api.getFunnelPerformance(funnel.id, this.tenantId).subscribe(perf => this.performance.set(perf));
  }

  public openUpsellSimulator(): void {
    this.upsellSuccessMessage.set(null);
    this.showUpsellSimulator.set(true);
  }

  public acceptUpsell(): void {
    if (!this.activeFunnel()) return;

    this.api.recordFunnelEvent(this.activeFunnel()!.id, {
      tenantId: this.tenantId,
      eventType: 4, // UpsellAccepted
      value: 39.0,
      customerPhone: '+21622334455'
    }).subscribe(() => {
      this.upsellSuccessMessage.set('🎉 1-Click Upsell added! Order total updated to 234 TND with free combined shipping.');
      setTimeout(() => {
        this.showUpsellSimulator.set(false);
        this.selectFunnel(this.activeFunnel()!);
      }, 1800);
    });
  }

  private seedDemoFunnel(): void {
    this.api.createFunnel({
      tenantId: this.tenantId,
      name: 'Leather Sneakers Launch Funnel',
      slug: 'sneakers-launch',
      description: 'Main flagship funnel with in-checkout polish kit bump and 1-click matching belt upsell.',
      currency: 'TND'
    }).subscribe(funnel => {
      this.activeFunnel.set(funnel);
      this.funnels.set([funnel]);

      // Seed Steps
      this.api.addFunnelStep(funnel.id, {
        tenantId: this.tenantId,
        stepName: 'Landing Page Hero',
        stepType: FunnelStepType.LandingPage,
        stepOrder: 1,
        slug: 'sneakers-launch'
      }).subscribe();

      this.api.addFunnelStep(funnel.id, {
        tenantId: this.tenantId,
        stepName: '1-Page COD Checkout',
        stepType: FunnelStepType.Checkout,
        stepOrder: 2,
        slug: 'checkout'
      }).subscribe(step => {
        // Add Bump
        this.api.createOrderBump(funnel.id, {
          tenantId: this.tenantId,
          stepId: step.id,
          title: 'Premium Leather Conditioner & Waterproof Spray',
          headline: 'Protect your new sneakers against rain and scratches for life',
          sku: 'CARE-LEATHER-KIT',
          originalPrice: 45.0,
          discountedPrice: 22.0,
          badgeText: '50% OFF - ONE TIME OFFER'
        }).subscribe(b => this.orderBumps.set([b]));
      });

      this.api.addFunnelStep(funnel.id, {
        tenantId: this.tenantId,
        stepName: '1-Click Matching Belt Modal',
        stepType: FunnelStepType.OneClickUpsell,
        stepOrder: 3,
        slug: 'upsell-belt'
      }).subscribe(step => {
        // Add Upsell
        this.api.createOneClickUpsell(funnel.id, {
          tenantId: this.tenantId,
          stepId: step.id,
          title: 'Matching Full-Grain Italian Leather Belt',
          sku: 'BELT-LEATHER-BROWN',
          headline: 'Complement your shoes with the matching artisan belt',
          originalPrice: 78.0,
          upsellPrice: 39.0
        }).subscribe(u => this.upsells.set([u]));
      });

      this.api.addFunnelStep(funnel.id, {
        tenantId: this.tenantId,
        stepName: 'Thank You Confirmation Page',
        stepType: FunnelStepType.ThankYouPage,
        stepOrder: 4,
        slug: 'thank-you'
      }).subscribe(() => {
        // Record baseline conversion events
        this.api.recordFunnelEvent(funnel.id, { tenantId: this.tenantId, eventType: 1, value: 0 }).subscribe();
        this.api.recordFunnelEvent(funnel.id, { tenantId: this.tenantId, eventType: 6, value: 195.0 }).subscribe();
        this.api.recordFunnelEvent(funnel.id, { tenantId: this.tenantId, eventType: 3, value: 22.0 }).subscribe();
        this.selectFunnel(funnel);
      });
    });
  }
}
