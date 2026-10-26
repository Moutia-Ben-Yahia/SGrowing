import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { WilayaZoneDto, CodCheckoutSessionDto } from '../../core/models/horizons-advanced.models';

interface DelegationOption {
  name: string;
}

@Component({
  selector: 'app-checkout-widget',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 font-sans flex flex-col justify-between">
      <!-- Checkout Navigation Header -->
      <header class="border-b border-slate-800 bg-slate-900/80 backdrop-blur sticky top-0 z-20 px-6 py-4 flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="h-9 w-9 rounded-xl bg-emerald-500/20 text-emerald-400 flex items-center justify-center font-bold text-lg border border-emerald-500/30">
            🛒
          </div>
          <div>
            <h1 class="text-base font-bold text-white leading-tight">1-Page Cash on Delivery Checkout</h1>
            <p class="text-xs text-slate-400">Headless High-Converting COD Funnel with Wilaya Cascade & Phone OTP</p>
          </div>
        </div>

        <a routerLink="/commerce" class="text-xs text-slate-400 hover:text-white px-3 py-1.5 rounded-lg bg-slate-800 transition">
          ← Back to Commerce
        </a>
      </header>

      <!-- Main Checkout Body -->
      <main class="max-w-5xl mx-auto w-full p-4 md:p-8 grid grid-cols-1 lg:grid-cols-12 gap-8">
        <!-- Left: Customer Form & Wilaya Cascade (7 cols) -->
        <div class="lg:col-span-7 space-y-6">
          @if (!orderCompleted()) {
            <!-- Step 1: Customer Contact -->
            <section class="p-6 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-4 shadow-xl">
              <div class="flex items-center justify-between">
                <div class="flex items-center gap-2">
                  <span class="h-6 w-6 rounded-full bg-emerald-500/20 text-emerald-400 text-xs font-mono font-bold flex items-center justify-center">1</span>
                  <h2 class="text-sm font-bold text-white uppercase tracking-wider">Contact & Verification</h2>
                </div>
                @if (isOtpVerified()) {
                  <span class="px-2.5 py-0.5 rounded-full text-xs font-mono bg-emerald-500/20 text-emerald-400 border border-emerald-500/30 flex items-center gap-1">
                    <span>✓</span> Phone Verified
                  </span>
                }
              </div>

              <div class="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
                <div>
                  <label class="text-slate-400 block mb-1 font-medium">Full Name *</label>
                  <input
                    type="text"
                    [(ngModel)]="customerName"
                    placeholder="e.g. Yassine Ben Salah"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-white focus:border-emerald-500 focus:outline-none" />
                </div>
                <div>
                  <label class="text-slate-400 block mb-1 font-medium">Phone Number (COD confirmation) *</label>
                  <div class="flex gap-2">
                    <input
                      type="tel"
                      [(ngModel)]="customerPhone"
                      placeholder="e.g. +216 98 123 456"
                      class="flex-1 bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-white font-mono focus:border-emerald-500 focus:outline-none" />
                    <button
                      type="button"
                      (click)="triggerSendOtp()"
                      [disabled]="!customerPhone || isOtpVerified()"
                      class="px-3 py-2 bg-slate-800 hover:bg-slate-700 disabled:opacity-40 text-emerald-400 text-xs font-bold rounded-lg border border-slate-700 transition">
                      Verify OTP
                    </button>
                  </div>
                </div>
              </div>

              @if (otpSimulatedCode()) {
                <div class="p-3 bg-emerald-500/10 border border-emerald-500/30 rounded-xl flex items-center justify-between text-xs">
                  <div class="space-y-0.5">
                    <span class="text-emerald-400 font-bold">📲 SMS Gateway Simulation</span>
                    <p class="text-slate-300">Your verification OTP code is: <strong class="text-white font-mono text-sm tracking-widest">{{ otpSimulatedCode() }}</strong></p>
                  </div>
                  <div class="flex items-center gap-2">
                    <input
                      type="text"
                      [(ngModel)]="enteredOtp"
                      placeholder="4 digits"
                      maxlength="4"
                      class="w-20 bg-slate-950 border border-emerald-500 rounded-lg p-1.5 text-center font-mono font-bold text-white text-sm" />
                    <button
                      type="button"
                      (click)="triggerVerifyOtp()"
                      class="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg font-bold text-xs">
                      Confirm
                    </button>
                  </div>
                </div>
              }
            </section>

            <!-- Step 2: Shipping Destination & Wilaya Cascade -->
            <section class="p-6 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-4 shadow-xl">
              <div class="flex items-center gap-2">
                <span class="h-6 w-6 rounded-full bg-emerald-500/20 text-emerald-400 text-xs font-mono font-bold flex items-center justify-center">2</span>
                <h2 class="text-sm font-bold text-white uppercase tracking-wider">Delivery Destination (Maghreb COD Mesh)</h2>
              </div>

              <div class="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
                <div>
                  <label class="text-slate-400 block mb-1 font-medium">Wilaya / Governorate *</label>
                  <select
                    [ngModel]="selectedWilayaCode()"
                    (ngModelChange)="onWilayaChange($event)"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-white focus:border-emerald-500 focus:outline-none">
                    <option value="" disabled>Select Wilaya...</option>
                    @for (w of wilayas(); track w.code) {
                      <option [value]="w.code">{{ w.nameFr }} ({{ w.nameAr }}) — {{ w.standardShippingFee }} TND</option>
                    }
                  </select>
                </div>

                <div>
                  <label class="text-slate-400 block mb-1 font-medium">Delegation / Commune *</label>
                  <select
                    [(ngModel)]="selectedDelegation"
                    [disabled]="delegations().length === 0"
                    class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-white focus:border-emerald-500 focus:outline-none disabled:opacity-50">
                    <option value="" disabled>Select Delegation...</option>
                    @for (d of delegations(); track d.name) {
                      <option [value]="d.name">{{ d.name }}</option>
                    }
                  </select>
                </div>
              </div>

              <div class="text-xs">
                <label class="text-slate-400 block mb-1 font-medium">Exact Street Address / Landmark *</label>
                <input
                  type="text"
                  [(ngModel)]="streetAddress"
                  placeholder="e.g. Imm El Manar 2, Apt 4, en face Carrefour Market"
                  class="w-full bg-slate-950 border border-slate-700 rounded-lg p-2.5 text-white focus:border-emerald-500 focus:outline-none" />
              </div>

              <!-- Wilaya Dispatch Guarantee Badge -->
              @if (currentWilaya()) {
                <div class="p-3 bg-slate-950/80 rounded-xl border border-slate-800 flex items-center justify-between text-xs">
                  <div class="flex items-center gap-2 text-slate-300">
                    <span>⚡</span>
                    <span>Guaranteed Courier Delivery: <strong class="text-emerald-400">{{ currentWilaya()!.estimatedDeliveryDays }} business days</strong></span>
                  </div>
                  <span class="font-mono text-cyan-400 font-bold">Shipping: {{ currentWilaya()!.standardShippingFee | number:'1.2-2' }} TND</span>
                </div>
              }
            </section>

            <!-- Step 3: Payment Method -->
            <section class="p-6 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-3 shadow-xl">
              <div class="flex items-center gap-2">
                <span class="h-6 w-6 rounded-full bg-emerald-500/20 text-emerald-400 text-xs font-mono font-bold flex items-center justify-center">3</span>
                <h2 class="text-sm font-bold text-white uppercase tracking-wider">Payment Method</h2>
              </div>

              <div class="p-4 rounded-xl border border-emerald-500/40 bg-emerald-950/10 flex items-center justify-between">
                <div class="flex items-center gap-3">
                  <div class="h-8 w-8 rounded-lg bg-emerald-500/20 text-emerald-400 flex items-center justify-center font-bold text-base">
                    💵
                  </div>
                  <div>
                    <h4 class="text-xs font-bold text-white">Cash on Delivery (COD)</h4>
                    <p class="text-[11px] text-slate-400">Pay the courier directly in cash when you receive and inspect your parcel.</p>
                  </div>
                </div>
                <span class="text-emerald-400 font-bold text-xs font-mono">100% Risk Free</span>
              </div>
            </section>
          } @else {
            <!-- Post-Order Confirmation & 1-Click Upsell Screen -->
            <section class="p-8 rounded-2xl bg-slate-900/80 border border-emerald-500/40 space-y-6 shadow-2xl text-center">
              <div class="h-16 w-16 mx-auto rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center text-3xl border border-emerald-500/40 animate-bounce">
                ✓
              </div>

              <div class="space-y-1">
                <h2 class="text-xl font-bold text-white">Order Confirmed Successfully!</h2>
                <p class="text-xs text-slate-400">Reference Number: <span class="font-mono text-emerald-400 font-bold">{{ confirmedOrderNumber() }}</span></p>
                <p class="text-xs text-slate-300">A confirmation SMS has been dispatched to {{ customerPhone }}. Courier will contact you before delivery.</p>
              </div>

              <!-- 1-Click Upsell Drawer -->
              @if (!upsellAccepted()) {
                <div class="p-5 rounded-2xl bg-gradient-to-r from-amber-500/10 via-amber-600/10 to-amber-500/10 border border-amber-500/30 text-left space-y-3">
                  <div class="flex items-center justify-between">
                    <span class="px-2 py-0.5 rounded-full bg-amber-500/20 text-amber-400 text-[10px] font-mono uppercase font-bold border border-amber-500/30">
                      ⚡ Special 1-Click Post-Purchase Deal
                    </span>
                    <span class="text-xs font-mono text-amber-400 font-bold">50% OFF TODAY</span>
                  </div>

                  <div class="flex items-center gap-4">
                    <div class="h-12 w-12 rounded-xl bg-slate-800 border border-slate-700 flex items-center justify-center text-xl shrink-0">
                      🎁
                    </div>
                    <div class="flex-1">
                      <h4 class="text-xs font-bold text-white">Add Premium Protective Hardcase & Fast VIP Dispatch</h4>
                      <p class="text-[11px] text-slate-400">Only 24.50 TND instead of regular 49.00 TND. No extra shipping fees!</p>
                    </div>
                    <div class="text-right">
                      <span class="text-sm font-bold font-mono text-amber-400">+24.50 TND</span>
                    </div>
                  </div>

                  <button
                    (click)="acceptUpsell()"
                    class="w-full py-2.5 rounded-xl bg-amber-500 hover:bg-amber-400 text-slate-950 text-xs font-bold transition shadow-lg shadow-amber-500/20">
                    Yes! Add to my order with 1 click (+24.50 TND)
                  </button>
                </div>
              } @else {
                <div class="p-4 rounded-xl bg-emerald-500/10 border border-emerald-500/30 text-xs text-emerald-400 font-medium">
                  ✓ 1-Click Upsell Added to Order! Total Cash to prepare at doorstep: <strong class="font-mono text-white">{{ finalTotalAmount() | number:'1.2-2' }} TND</strong>
                </div>
              }

              <button
                (click)="resetCheckout()"
                class="px-5 py-2.5 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium transition">
                Start New Order / Return
              </button>
            </section>
          }
        </div>

        <!-- Right: Cart Summary & Place Order Button (5 cols) -->
        <div class="lg:col-span-5 space-y-4">
          <div class="p-6 rounded-2xl bg-slate-900/60 border border-slate-800 space-y-5 sticky top-24 shadow-xl">
            <h3 class="text-sm font-bold text-white uppercase tracking-wider border-b border-slate-800 pb-3">
              Order Summary
            </h3>

            <!-- Cart Items List -->
            <div class="space-y-3">
              <div class="flex items-center gap-3">
                <div class="h-12 w-12 rounded-xl bg-slate-800 border border-slate-700 flex items-center justify-center text-xl shrink-0">
                  📦
                </div>
                <div class="flex-1 text-xs">
                  <h4 class="font-bold text-white">Wireless ANC Earbuds Pro</h4>
                  <span class="text-slate-400 text-[11px]">SKU: EAR-PRO-ANC • Qty: 1</span>
                </div>
                <span class="font-mono font-bold text-white text-xs">89.00 TND</span>
              </div>
            </div>

            <!-- Price Breakdown -->
            <div class="border-t border-slate-800 pt-3 space-y-2 text-xs">
              <div class="flex justify-between text-slate-400">
                <span>Subtotal</span>
                <span class="font-mono text-white">{{ subtotal() | number:'1.2-2' }} TND</span>
              </div>
              <div class="flex justify-between text-slate-400">
                <span>Shipping ({{ currentWilaya()?.nameFr || 'Standard' }})</span>
                <span class="font-mono text-white">{{ shippingFee() | number:'1.2-2' }} TND</span>
              </div>
              @if (upsellAccepted()) {
                <div class="flex justify-between text-amber-400 font-medium">
                  <span>VIP Hardcase Upsell</span>
                  <span class="font-mono">+24.50 TND</span>
                </div>
              }
              <div class="border-t border-slate-800 pt-2 flex justify-between items-center text-sm font-bold text-white">
                <span>Total Due at Doorstep</span>
                <span class="text-lg font-mono text-emerald-400">{{ finalTotalAmount() | number:'1.2-2' }} TND</span>
              </div>
            </div>

            <!-- Submit Button -->
            @if (!orderCompleted()) {
              <button
                type="button"
                (click)="submitCodOrder()"
                [disabled]="!isFormValid()"
                class="w-full py-3.5 rounded-xl bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 text-white font-bold text-sm shadow-xl shadow-emerald-600/20 transition flex items-center justify-center gap-2">
                <span>📦</span>
                <span>Confirm Cash on Delivery Order</span>
              </button>
              <p class="text-[11px] text-slate-400 text-center">
                Free inspection upon delivery. No card required.
              </p>
            }
          </div>
        </div>
      </main>

      <!-- Footer -->
      <footer class="border-t border-slate-800 p-4 text-center text-xs text-slate-500">
        SGrowing Omnichannel COD Mesh • Ultra-Fast 1-Page Checkout
      </footer>
    </div>
  `
})
export class CheckoutWidgetComponent implements OnInit {
  private api = inject(ApiService);
  private auth = inject(AuthService);

  public tenantId = this.auth.currentTenant()?.id || 'a0000000-0000-0000-0000-000000000001';

  public wilayas = signal<WilayaZoneDto[]>([]);
  public delegations = signal<DelegationOption[]>([]);
  public currentWilaya = signal<WilayaZoneDto | null>(null);

  public customerName = 'Karim Trabelsi';
  public customerPhone = '+21698765432';
  public selectedWilayaCode = signal<string>('TUN');
  public selectedDelegation = 'La Marsa';
  public streetAddress = '14 Avenue de la République, La Marsa Plage';

  public subtotal = signal<number>(89.0);
  public shippingFee = signal<number>(7.0);
  public upsellAccepted = signal<boolean>(false);
  public upsellAmount = signal<number>(24.5);

  public isOtpVerified = signal<boolean>(false);
  public otpSimulatedCode = signal<string | null>(null);
  public enteredOtp = '';

  public orderCompleted = signal<boolean>(false);
  public confirmedOrderNumber = signal<string>('');

  ngOnInit(): void {
    this.loadWilayas();
  }

  public loadWilayas(): void {
    this.api.getCheckoutWilayas('TN').subscribe({
      next: data => {
        if (data && data.length > 0) {
          this.wilayas.set(data);
          this.onWilayaChange(data[0].code);
        } else {
          this.fallbackWilayas();
        }
      },
      error: () => this.fallbackWilayas()
    });
  }

  private fallbackWilayas(): void {
    const defaultList: WilayaZoneDto[] = [
      {
        id: 'w-tun',
        code: 'TUN',
        nameFr: 'Tunis',
        nameAr: 'تونس',
        countryCode: 'TN',
        standardShippingFee: 7.0,
        estimatedDeliveryDays: 1,
        delegationsJson: JSON.stringify([{ name: 'La Marsa' }, { name: 'Carthage' }, { name: 'Sidi Bou Said' }, { name: 'El Menzah' }])
      },
      {
        id: 'w-ari',
        code: 'ARI',
        nameFr: 'Ariana',
        nameAr: 'أريانة',
        countryCode: 'TN',
        standardShippingFee: 7.0,
        estimatedDeliveryDays: 1,
        delegationsJson: JSON.stringify([{ name: 'Ennasr' }, { name: 'Ariana Ville' }, { name: 'Raoued' }])
      },
      {
        id: 'w-sou',
        code: 'SOU',
        nameFr: 'Sousse',
        nameAr: 'سوسة',
        countryCode: 'TN',
        standardShippingFee: 8.0,
        estimatedDeliveryDays: 2,
        delegationsJson: JSON.stringify([{ name: 'Hammam Sousse' }, { name: 'Sousse Ville' }, { name: 'Kantaoui' }])
      },
      {
        id: 'w-sfx',
        code: 'SFX',
        nameFr: 'Sfax',
        nameAr: 'صفاقس',
        countryCode: 'TN',
        standardShippingFee: 8.5,
        estimatedDeliveryDays: 2,
        delegationsJson: JSON.stringify([{ name: 'Sfax Ville' }, { name: 'Sakiet Ezzit' }, { name: 'Sakiet Eddaier' }])
      }
    ];

    this.wilayas.set(defaultList);
    this.onWilayaChange('TUN');
  }

  public onWilayaChange(code: string): void {
    this.selectedWilayaCode.set(code);
    const found = this.wilayas().find(w => w.code === code);
    if (found) {
      this.currentWilaya.set(found);
      this.shippingFee.set(found.standardShippingFee);

      try {
        const parsed = JSON.parse(found.delegationsJson);
        this.delegations.set(parsed);
        if (parsed.length > 0) {
          this.selectedDelegation = parsed[0].name;
        }
      } catch {
        this.delegations.set([]);
      }
    }
  }

  public triggerSendOtp(): void {
    this.api.sendCheckoutOtp({ tenantId: this.tenantId, sessionKey: 'sess-ui-' + Date.now() }).subscribe({
      next: res => {
        this.otpSimulatedCode.set(res.simulatedOtp || '4829');
      },
      error: () => {
        this.otpSimulatedCode.set('4829');
      }
    });
  }

  public triggerVerifyOtp(): void {
    if (this.enteredOtp === this.otpSimulatedCode()) {
      this.isOtpVerified.set(true);
      this.otpSimulatedCode.set(null);
    } else {
      alert('Invalid OTP code. Please check your simulated code.');
    }
  }

  public isFormValid(): boolean {
    return !!(this.customerName && this.customerPhone && this.selectedWilayaCode() && this.streetAddress);
  }

  public finalTotalAmount(): number {
    return this.subtotal() + this.shippingFee() + (this.upsellAccepted() ? this.upsellAmount() : 0);
  }

  public submitCodOrder(): void {
    const ordNum = 'ORD-' + Math.floor(100000 + Math.random() * 900000);
    this.confirmedOrderNumber.set(ordNum);
    this.orderCompleted.set(true);
  }

  public acceptUpsell(): void {
    this.upsellAccepted.set(true);
  }

  public resetCheckout(): void {
    this.orderCompleted.set(false);
    this.upsellAccepted.set(false);
    this.isOtpVerified.set(false);
    this.enteredOtp = '';
  }
}
