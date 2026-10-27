import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { WaybillGenerationResult, CodFraudEvaluationResult } from '../../core/models/pillars.models';

@Component({
  selector: 'app-cod-logistics-fraud-cockpit',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="flex h-screen w-screen overflow-hidden bg-slate-950 text-slate-100 font-sans">
      <main class="flex-1 flex flex-col overflow-y-auto">
        <!-- Topbar -->
        <header class="h-14 border-b border-slate-800 bg-slate-900/60 px-6 flex items-center justify-between shrink-0">
          <div class="flex items-center gap-3">
            <a routerLink="/courier" class="text-xs text-slate-400 hover:text-white flex items-center gap-1 transition">
              ← Courier Hub
            </a>
            <span class="text-slate-700">|</span>
            <span class="text-base font-semibold text-white flex items-center gap-2">
              📦 Regional Courier Gateway & COD Anti-Fraud Radar
            </span>
            <span class="bg-orange-500/20 text-orange-400 text-[10px] font-mono px-2 py-0.5 rounded-full border border-orange-500/30">
              Yalidine • Aramex • Fake Order Shield
            </span>
          </div>

          <div class="flex items-center gap-3">
            <button
              (click)="activeTab.set('shipping')"
              [class]="activeTab() === 'shipping' ? 'bg-orange-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
              Carrier Shipping Gateway
            </button>
            <button
              (click)="activeTab.set('antifraud')"
              [class]="activeTab() === 'antifraud' ? 'bg-orange-600 text-white' : 'bg-slate-800 text-slate-300 hover:bg-slate-700'"
              class="px-3 py-1.5 rounded-lg text-xs font-semibold transition">
              COD Anti-Fraud Radar
            </button>
          </div>
        </header>

        <!-- Body -->
        <div class="p-6 space-y-6 flex-1">
          <!-- TAB 1: SHIPPING GATEWAY -->
          @if (activeTab() === 'shipping') {
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
              <!-- Waybill Generator Form -->
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-3">
                <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                  <span>🏷️</span> Generate Waybill & Manifest
                </h3>

                <div class="grid grid-cols-2 gap-3 text-xs">
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-0.5">Carrier Service</label>
                    <select [(ngModel)]="courierCode" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200">
                      <option value="yalidine">Yalidine Express Logistics</option>
                      <option value="aramex">Aramex Global COD</option>
                      <option value="chronopost">Chronopost / La Poste Express</option>
                      <option value="colisprive">Colis Privé Doorstep</option>
                      <option value="bosta">Bosta Logistics</option>
                      <option value="oto">OTO Multi-Carrier Hub</option>
                    </select>
                  </div>
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-0.5">Order Number</label>
                    <input type="text" [(ngModel)]="orderNum" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                  </div>
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-0.5">Recipient Full Name</label>
                    <input type="text" [(ngModel)]="customerName" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                  </div>
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-0.5">Recipient Phone</label>
                    <input type="text" [(ngModel)]="customerPhone" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                  </div>
                  <div class="col-span-2">
                    <label class="text-[10px] text-slate-400 block mb-0.5">Destination Street Address</label>
                    <input type="text" [(ngModel)]="deliveryAddress" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                  </div>
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-0.5">Cash to Collect</label>
                    <input type="number" [(ngModel)]="cashToCollect" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                  </div>
                  <div>
                    <label class="text-[10px] text-slate-400 block mb-0.5">Destination City</label>
                    <input type="text" [(ngModel)]="deliveryCity" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                  </div>
                </div>

                <button
                  (click)="generateWaybill()"
                  [disabled]="isGeneratingWaybill()"
                  class="w-full py-2 bg-orange-600 hover:bg-orange-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                  {{ isGeneratingWaybill() ? 'Dispatching...' : 'Dispatch Waybill & Generate AWB Barcode' }}
                </button>
              </div>

              <!-- Waybill & Label Output -->
              <div class="p-5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
                <h3 class="text-sm font-semibold text-white">Carrier Dispatch & Thermal Label Card</h3>

                @if (waybillResult()) {
                  <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-3">
                    <div class="flex items-center justify-between">
                      <span class="text-xs font-bold text-orange-400">{{ waybillResult()?.courierName }}</span>
                      <span class="text-[10px] bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 px-2 py-0.5 rounded font-mono">
                        Dispatched
                      </span>
                    </div>

                    <div class="p-3 bg-slate-900 rounded-lg text-center space-y-1">
                      <div class="text-sm font-mono tracking-widest text-white font-bold">{{ waybillResult()?.trackingNumber }}</div>
                      <div class="text-xs font-mono text-slate-400 tracking-wider select-all">{{ waybillResult()?.awbBarcode }}</div>
                    </div>

                    <div class="grid grid-cols-2 gap-2 text-xs pt-1 border-t border-slate-800">
                      <div><span class="text-slate-500 text-[10px] block">Manifest ID</span><span class="text-white font-mono">{{ waybillResult()?.manifestId }}</span></div>
                      <div><span class="text-slate-500 text-[10px] block">COD Cash Collection</span><span class="text-emerald-400 font-bold font-mono">{{ waybillResult()?.declaredCashToCollect | currency }}</span></div>
                    </div>

                    <div class="pt-2">
                      <a [href]="waybillResult()?.labelUrl" target="_blank" class="w-full py-1.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded text-xs flex items-center justify-center gap-1 transition">
                        <span>🖨️</span> Download Thermal PDF Label
                      </a>
                    </div>
                  </div>
                } @else {
                  <div class="h-64 flex flex-col items-center justify-center text-slate-500 text-xs gap-2">
                    <span>💡</span> Click Dispatch to issue compliant courier manifests and barcodes.
                  </div>
                }
              </div>
            </div>
          }

          <!-- TAB 2: ANTI-FRAUD RADAR -->
          @if (activeTab() === 'antifraud') {
            <div class="max-w-2xl mx-auto p-6 rounded-xl bg-slate-900/60 border border-slate-800 space-y-4">
              <h3 class="text-sm font-semibold text-white flex items-center gap-2">
                <span>🛡️</span> Fake Order Detection & COD Risk Scoring
              </h3>
              <p class="text-xs text-slate-400">
                Multi-factor risk analysis evaluating telephone syntax, repetitive digits, address gibberish, order size, and delivery refusal history.
              </p>

              <div class="grid grid-cols-2 gap-3 text-xs">
                <div>
                  <label class="text-[10px] text-slate-400 block mb-0.5">Customer Name</label>
                  <input type="text" [(ngModel)]="fraudName" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                </div>
                <div>
                  <label class="text-[10px] text-slate-400 block mb-0.5">Customer Phone</label>
                  <input type="text" [(ngModel)]="fraudPhone" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                </div>
                <div class="col-span-2">
                  <label class="text-[10px] text-slate-400 block mb-0.5">Delivery Address</label>
                  <input type="text" [(ngModel)]="fraudAddress" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                </div>
                <div>
                  <label class="text-[10px] text-slate-400 block mb-0.5">Order Total Amount ($)</label>
                  <input type="number" [(ngModel)]="fraudTotal" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                </div>
                <div>
                  <label class="text-[10px] text-slate-400 block mb-0.5">Item Quantity</label>
                  <input type="number" [(ngModel)]="fraudItems" class="w-full bg-slate-950 border border-slate-800 rounded p-1.5 text-xs text-slate-200" />
                </div>
              </div>

              <button
                (click)="evaluateFraud()"
                [disabled]="isEvaluatingFraud()"
                class="w-full py-2 bg-orange-600 hover:bg-orange-500 disabled:opacity-50 text-white rounded-lg text-xs font-semibold transition">
                {{ isEvaluatingFraud() ? 'Analyzing...' : 'Execute Anti-Fraud Evaluation' }}
              </button>

              @if (fraudResult()) {
                <div class="p-4 rounded-xl bg-slate-950 border border-slate-800 space-y-3">
                  <div class="flex items-center justify-between">
                    <div>
                      <span class="text-xs text-slate-400">Risk Score:</span>
                      <span class="text-2xl font-bold font-mono ml-2" [class]="getRiskColor(fraudResult()?.riskScore)">
                        {{ fraudResult()?.riskScore }}/100
                      </span>
                    </div>

                    <span [class]="getTierBadge(fraudResult()?.tier)" class="px-3 py-1 rounded-full text-xs font-bold uppercase font-mono">
                      {{ getTierName(fraudResult()?.tier) }}
                    </span>
                  </div>

                  <div class="p-3 bg-slate-900 rounded border border-slate-800 text-xs">
                    <strong class="text-slate-300">Action Recommended:</strong> {{ getActionName(fraudResult()?.recommendedAction) }}
                    <p class="text-slate-400 mt-1">{{ fraudResult()?.assessmentSummary }}</p>
                  </div>

                  @if (fraudResult()?.triggeredRules?.length) {
                    <div class="space-y-1.5 pt-2 border-t border-slate-800 text-xs">
                      <span class="text-[10px] font-semibold text-rose-400 uppercase tracking-wider block">Triggered Fraud Vectors</span>
                      @for (rule of fraudResult()?.triggeredRules; track rule.ruleId) {
                        <div class="text-[11px] text-slate-300 flex items-center justify-between">
                          <span>⚠️ {{ rule.description }}</span>
                          <span class="text-rose-400 font-mono">+{{ rule.pointsAdded }} pts</span>
                        </div>
                      }
                    </div>
                  }
                </div>
              }
            </div>
          }
        </div>
      </main>
    </div>
  `
})
export class CodLogisticsFraudCockpitComponent {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public activeTab = signal<'shipping' | 'antifraud'>('shipping');

  // Waybill
  public courierCode = 'yalidine';
  public orderNum = 'ORD-2026-8812';
  public customerName = 'Mourad Trabelsi';
  public customerPhone = '+21698123456';
  public deliveryAddress = '14 Avenue Habib Bourguiba, Centre Ville';
  public deliveryCity = 'Tunis';
  public cashToCollect = 165.0;
  public isGeneratingWaybill = signal(false);
  public waybillResult = signal<WaybillGenerationResult | null>(null);

  // Anti-fraud
  public fraudName = 'asdf test';
  public fraudPhone = '+21699999999';
  public fraudAddress = 'fake test street none';
  public fraudTotal = 650;
  public fraudItems = 12;
  public isEvaluatingFraud = signal(false);
  public fraudResult = signal<CodFraudEvaluationResult | null>(null);

  public generateWaybill() {
    this.isGeneratingWaybill.set(true);
    const tenantId = this.auth.currentUser()?.tenantId || '00000000-0000-0000-0000-000000000000';
    this.api.generateCarrierWaybill(tenantId, {
      courierCode: this.courierCode,
      orderId: '00000000-0000-0000-0000-000000000000',
      orderNumber: this.orderNum,
      customerName: this.customerName,
      customerPhone: this.customerPhone,
      deliveryAddress: this.deliveryAddress,
      deliveryCity: this.deliveryCity,
      deliveryGovernorateOrState: 'Tunis',
      cashToCollect: this.cashToCollect,
      currency: 'TND',
      weightKg: 1.5,
      packageCount: 1,
      allowCustomerToOpenBeforePaying: true
    }).subscribe({
      next: res => {
        this.waybillResult.set(res);
        this.isGeneratingWaybill.set(false);
      },
      error: () => this.isGeneratingWaybill.set(false)
    });
  }

  public evaluateFraud() {
    this.isEvaluatingFraud.set(true);
    const tenantId = this.auth.currentUser()?.tenantId || '00000000-0000-0000-0000-000000000000';
    this.api.evaluateCodFraud(tenantId, {
      customerName: this.fraudName,
      customerPhone: this.fraudPhone,
      deliveryAddress: this.fraudAddress,
      deliveryCity: '',
      orderTotal: this.fraudTotal,
      itemsCount: this.fraudItems
    }).subscribe({
      next: res => {
        this.fraudResult.set(res);
        this.isEvaluatingFraud.set(false);
      },
      error: () => this.isEvaluatingFraud.set(false)
    });
  }

  public getRiskColor(score?: number): string {
    if (!score) return 'text-emerald-400';
    if (score >= 70) return 'text-rose-500';
    if (score >= 45) return 'text-orange-400';
    if (score >= 25) return 'text-amber-400';
    return 'text-emerald-400';
  }

  public getTierName(tier?: number): string {
    switch (tier) {
      case 0: return 'Safe';
      case 1: return 'Moderate';
      case 2: return 'High Risk';
      case 3: return 'Fraudulent / Fake';
      default: return 'Safe';
    }
  }

  public getTierBadge(tier?: number): string {
    switch (tier) {
      case 3: return 'bg-rose-500/20 text-rose-400 border border-rose-500/30';
      case 2: return 'bg-orange-500/20 text-orange-400 border border-orange-500/30';
      case 1: return 'bg-amber-500/20 text-amber-400 border border-amber-500/30';
      default: return 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30';
    }
  }

  public getActionName(action?: number): string {
    switch (action) {
      case 0: return 'Auto-Approve for Packing';
      case 1: return 'Require SMS OTP / WhatsApp Confirmation';
      case 2: return 'Require Advance Shipping Deposit';
      case 3: return 'Auto-Reject Order to Prevent Return Shipping Loss';
      default: return 'Auto-Approve';
    }
  }
}
