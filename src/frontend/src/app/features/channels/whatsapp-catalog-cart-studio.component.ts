import { Component, signal, computed, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface CatalogProduct {
  sku: string;
  title: string;
  description: string;
  category: string;
  unitPrice: number;
  currency: string;
  stockAvailable: number;
  imageUrl: string;
  isActive: boolean;
}

export interface CartItem {
  sku: string;
  title: string;
  quantity: number;
  unitPrice: number;
  currency: string;
  imageUrl: string;
}

export interface AppliedCoupon {
  code: string;
  type: 'Percentage' | 'Fixed';
  value: number;
  discountAmount: number;
  description: string;
}

export interface AvailableCoupon {
  code: string;
  type: 'Percentage' | 'Fixed';
  value: number;
  minimumOrder: number;
  description: string;
}

@Component({
  selector: 'app-whatsapp-catalog-cart-studio',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans selection:bg-emerald-500/30">
      <!-- Top Navigation & Header -->
      <header class="border-b border-slate-800/80 bg-slate-900/80 backdrop-blur-md px-6 py-4 sticky top-0 z-40 flex flex-wrap items-center justify-between gap-4">
        <div class="flex items-center gap-3">
          <div class="w-10 h-10 rounded-xl bg-gradient-to-tr from-emerald-600 via-teal-500 to-green-500 flex items-center justify-center shadow-lg shadow-emerald-500/20">
            <svg class="w-5 h-5 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z"/>
            </svg>
          </div>
          <div>
            <div class="flex items-center gap-2">
              <h1 class="text-lg font-bold text-slate-100 tracking-tight">WhatsApp Interactive Catalog & In-Chat Cart Studio</h1>
              <span class="px-2.5 py-0.5 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-400 border border-emerald-500/20">
                Horizon 46
              </span>
            </div>
            <p class="text-xs text-slate-400">Meta Multi-Product MPM Stanzas • Stock Locking TTL • Conversational Checkout State Engine</p>
          </div>
        </div>

        <!-- Header Quick Info -->
        <div class="flex items-center gap-3">
          <div class="bg-slate-900 border border-slate-800 rounded-lg px-3 py-1.5 flex items-center gap-2.5">
            <span class="w-2.5 h-2.5 rounded-full" [ngClass]="isReservationActive() ? 'bg-amber-400 animate-pulse' : (cartItems().length > 0 ? 'bg-emerald-400' : 'bg-slate-500')"></span>
            <div class="text-xs">
              <span class="text-slate-400 font-mono">{{ customerPhone() }}</span>
              <span class="text-slate-600 mx-1.5">|</span>
              <span class="font-medium" [ngClass]="isReservationActive() ? 'text-amber-300' : (cartState() === 'OrderPlaced' ? 'text-emerald-400' : 'text-slate-300')">
                {{ cartState() }}
              </span>
              @if (isReservationActive()) {
                <span class="text-amber-400 font-mono ml-1 font-bold">({{ formatTimer(activeReservationTimer()) }})</span>
              }
            </div>
          </div>

          <button (click)="openStanzaGenerator(null, 'product_list')" class="px-3.5 py-1.5 rounded-lg text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 text-white transition flex items-center gap-1.5 shadow-md shadow-emerald-600/20">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4"/>
            </svg>
            Generate Catalog Stanza
          </button>
        </div>
      </header>

      <!-- Main Layout Grid -->
      <main class="flex-1 max-w-7xl w-full mx-auto p-6 grid grid-cols-1 lg:grid-cols-12 gap-6">

        <!-- Left Column: Catalog Manager & Products (7 Cols) -->
        <div class="lg:col-span-7 space-y-6">

          <!-- Filter & Search Bar -->
          <div class="bg-slate-900/70 border border-slate-800 rounded-xl p-4 flex flex-wrap items-center justify-between gap-4">
            <div class="flex items-center gap-2 flex-1 min-w-[220px]">
              <div class="relative w-full">
                <input
                  type="text"
                  [ngModel]="searchQuery()"
                  (ngModelChange)="searchQuery.set($event)"
                  placeholder="Search products by title, SKU, or keywords..."
                  class="w-full bg-slate-950 border border-slate-800 rounded-lg pl-9 pr-3 py-1.5 text-xs text-slate-200 placeholder-slate-500 focus:outline-none focus:border-emerald-500 transition"
                />
                <svg class="w-4 h-4 text-slate-500 absolute left-3 top-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"/>
                </svg>
              </div>
            </div>

            <!-- Category Pills -->
            <div class="flex items-center gap-1.5 overflow-x-auto">
              @for (cat of categories; track cat) {
                <button
                  (click)="selectedCategory.set(cat)"
                  class="px-2.5 py-1 rounded-md text-xs font-medium transition"
                  [ngClass]="selectedCategory() === cat ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' : 'bg-slate-800/60 text-slate-400 hover:text-slate-200 border border-transparent'"
                >
                  {{ cat }}
                </button>
              }
            </div>
          </div>

          <!-- Product Catalog Grid -->
          <div class="grid grid-cols-1 sm:grid-cols-2 gap-4">
            @for (product of filteredProducts(); track product.sku) {
              <div class="bg-slate-900/60 border border-slate-800 rounded-xl overflow-hidden hover:border-slate-700 transition flex flex-col group">
                <div class="h-36 relative overflow-hidden bg-slate-950">
                  <img [src]="product.imageUrl" [alt]="product.title" class="w-full h-full object-cover group-hover:scale-105 transition duration-300" />
                  <div class="absolute inset-0 bg-gradient-to-t from-slate-950 via-transparent to-transparent"></div>
                  <div class="absolute top-2.5 left-2.5 flex items-center gap-1.5">
                    <span class="px-2 py-0.5 rounded text-[10px] font-semibold bg-slate-900/90 text-slate-300 border border-slate-700 backdrop-blur-sm">
                      {{ product.category }}
                    </span>
                    <span class="px-2 py-0.5 rounded text-[10px] font-mono font-medium bg-emerald-500/20 text-emerald-300 border border-emerald-500/30">
                      {{ product.sku }}
                    </span>
                  </div>
                  <div class="absolute top-2.5 right-2.5">
                    <span
                      class="px-2 py-0.5 rounded text-[10px] font-semibold border backdrop-blur-sm"
                      [ngClass]="product.stockAvailable > 20 ? 'bg-emerald-950/80 text-emerald-400 border-emerald-800' : (product.stockAvailable > 0 ? 'bg-amber-950/80 text-amber-400 border-amber-800' : 'bg-rose-950/80 text-rose-400 border-rose-800')"
                    >
                      {{ product.stockAvailable > 0 ? product.stockAvailable + ' in stock' : 'Out of stock' }}
                    </span>
                  </div>
                </div>

                <div class="p-4 flex-1 flex flex-col justify-between space-y-3">
                  <div>
                    <h3 class="text-sm font-semibold text-slate-100">{{ product.title }}</h3>
                    <p class="text-xs text-slate-400 mt-1 line-clamp-2">{{ product.description }}</p>
                  </div>

                  <div class="pt-2 border-t border-slate-800/80 flex items-center justify-between">
                    <div>
                      <span class="text-xs text-slate-400">Unit Price</span>
                      <div class="text-base font-bold text-emerald-400">\${{ product.unitPrice.toFixed(2) }} <span class="text-[10px] text-slate-400">{{ product.currency }}</span></div>
                    </div>

                    <div class="flex items-center gap-1.5">
                      <button
                        (click)="openStanzaGenerator(product, 'single_product')"
                        title="View WhatsApp JSON Stanza"
                        class="p-1.5 rounded-lg text-slate-400 hover:text-slate-200 bg-slate-800 hover:bg-slate-700 transition"
                      >
                        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4"/>
                        </svg>
                      </button>

                      <button
                        (click)="addToCart(product)"
                        [disabled]="product.stockAvailable <= 0"
                        class="px-2.5 py-1.5 rounded-lg text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 disabled:pointer-events-none text-white transition flex items-center gap-1 shadow-sm"
                      >
                        <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"/>
                        </svg>
                        Add to Cart
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            }
          </div>

          <!-- Coupon Ledger Strip -->
          <div class="bg-slate-900/60 border border-slate-800 rounded-xl p-4">
            <div class="flex items-center justify-between mb-3">
              <div class="flex items-center gap-2">
                <svg class="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M7 7h.01M7 3h5c.512 0 1.024.195 1.414.586l7 7a2 2 0 010 2.828l-7 7a2 2 0 01-2.828 0l-7-7A1.994 1.994 0 013 12V7a4 4 0 014-4z"/>
                </svg>
                <h4 class="text-xs font-semibold text-slate-200 uppercase tracking-wider">Active Promotional Coupons</h4>
              </div>
              <span class="text-[10px] text-slate-400">Click any code to autofill into cart</span>
            </div>

            <div class="grid grid-cols-2 sm:grid-cols-4 gap-2.5">
              @for (c of availableCoupons; track c.code) {
                <button
                  (click)="quickApplyCoupon(c.code)"
                  class="bg-slate-950 border border-slate-800 hover:border-emerald-500/40 rounded-lg p-2.5 text-left transition group"
                >
                  <div class="flex items-center justify-between">
                    <span class="text-xs font-mono font-bold text-emerald-400 group-hover:text-emerald-300">{{ c.code }}</span>
                    <span class="text-[10px] px-1.5 py-0.2 rounded bg-slate-800 text-slate-300">
                      {{ c.type === 'Percentage' ? c.value + '%' : '\$' + c.value }}
                    </span>
                  </div>
                  <p class="text-[10px] text-slate-400 mt-1 truncate">{{ c.description }}</p>
                  <p class="text-[9px] text-slate-500 mt-0.5">Min: \${{ c.minimumOrder }}</p>
                </button>
              }
            </div>
          </div>
        </div>

        <!-- Right Column: WhatsApp Simulator & Cart Session Drawer (5 Cols) -->
        <div class="lg:col-span-5 space-y-6">

          <!-- WhatsApp Smartphone Simulator Card -->
          <div class="bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-2xl flex flex-col">
            <!-- Simulated Phone Top Bar -->
            <div class="bg-emerald-800 px-4 py-3 flex items-center justify-between text-white border-b border-emerald-700/60">
              <div class="flex items-center gap-2.5">
                <div class="w-8 h-8 rounded-full bg-emerald-700 flex items-center justify-center font-bold text-xs border border-emerald-500/40">
                  SG
                </div>
                <div>
                  <div class="flex items-center gap-1.5">
                    <span class="text-xs font-bold leading-tight">SGrowing Official Store</span>
                    <svg class="w-3.5 h-3.5 text-emerald-300" fill="currentColor" viewBox="0 0 20 20">
                      <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/>
                    </svg>
                  </div>
                  <div class="text-[10px] text-emerald-200">Verified WhatsApp Business Account</div>
                </div>
              </div>

              <div class="flex items-center gap-2.5 text-emerald-200">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 5a2 2 0 012-2h3.28a1 1 0 01.948.684l1.498 4.493a1 1 0 01-.502 1.21l-2.257 1.13a11.042 11.042 0 005.516 5.516l1.13-2.257a1 1 0 011.21-.502l4.493 1.498a1 1 0 01.684.949V19a2 2 0 01-2 2h-1C9.716 21 3 14.284 3 6V5z"/>
                </svg>
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 5v.01M12 12v.01M12 19v.01M12 6a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2zm0 7a1 1 0 110-2 1 1 0 010 2z"/>
                </svg>
              </div>
            </div>

            <!-- Simulated Chat Messages Area (WhatsApp Patterned Background) -->
            <div class="p-4 bg-slate-950/90 min-h-[380px] max-h-[460px] overflow-y-auto space-y-3.5 relative">

              <!-- Date Badge -->
              <div class="flex justify-center">
                <span class="px-2.5 py-0.5 rounded-full text-[10px] bg-slate-800 text-slate-400 font-medium shadow-sm">
                  Today
                </span>
              </div>

              <!-- Inbound Catalog Stanza Bubble -->
              <div class="max-w-[88%] bg-emerald-950/80 border border-emerald-800/60 rounded-2xl rounded-tl-sm p-3 text-slate-100 shadow-md">
                <div class="flex items-center gap-1.5 text-emerald-400 text-xs font-semibold pb-1.5 border-b border-emerald-900/60 mb-2">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M16 11V7a4 4 0 00-8 0v4M5 9h14l1 12H4L5 9z"/>
                  </svg>
                  <span>⚡ Official Product Catalog</span>
                </div>
                <p class="text-xs text-slate-200">
                  Welcome to our interactive catalog! Select products directly in-chat to add them to your cart.
                </p>
                <div class="mt-2.5 pt-2 border-t border-emerald-900/40 flex justify-between items-center text-[10px] text-slate-400">
                  <span>{{ products().length }} Products Available</span>
                  <span class="text-emerald-300 font-medium">Meta Verified</span>
                </div>
              </div>

              <!-- Cart Status Bubble -->
              @if (cartItems().length > 0) {
                <div class="max-w-[88%] bg-slate-900 border border-slate-800 rounded-2xl rounded-tl-sm p-3 shadow-md space-y-2">
                  <div class="flex items-center justify-between text-xs font-bold text-slate-200 pb-1.5 border-b border-slate-800">
                    <span class="flex items-center gap-1.5 text-emerald-400">
                      <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M3 3h2l.4 2M7 13h10l4-8H5.4M7 13L5.4 5M7 13l-2.293 2.293c-.63.63-.184 1.707.707 1.707H17m0 0a2 2 0 100 4 2 2 0 000-4zm-8 2a2 2 0 11-4 0 2 2 0 014 0z"/>
                      </svg>
                      In-Chat Cart
                    </span>
                    <span class="text-[10px] px-2 py-0.5 rounded bg-slate-800 text-slate-300 font-mono">
                      {{ cartState() }}
                    </span>
                  </div>

                  <!-- Cart Items Mini List -->
                  <div class="space-y-1.5 text-xs">
                    @for (item of cartItems(); track item.sku) {
                      <div class="flex items-center justify-between text-slate-300">
                        <span class="truncate max-w-[150px]">• {{ item.quantity }}x {{ item.title }}</span>
                        <span class="font-mono text-emerald-400">\${{ (item.quantity * item.unitPrice).toFixed(2) }}</span>
                      </div>
                    }
                  </div>

                  <!-- Subtotals & Discounts -->
                  <div class="pt-2 border-t border-slate-800/80 space-y-1 text-xs">
                    <div class="flex justify-between text-slate-400">
                      <span>Subtotal</span>
                      <span class="font-mono">\${{ subtotal().toFixed(2) }}</span>
                    </div>

                    @if (appliedCoupon(); as cp) {
                      <div class="flex justify-between text-emerald-400">
                        <span>Coupon ({{ cp.code }})</span>
                        <span class="font-mono">-\${{ cp.discountAmount.toFixed(2) }}</span>
                      </div>
                    }

                    <div class="flex justify-between font-bold text-slate-100 pt-1 border-t border-slate-800">
                      <span>Total Payable</span>
                      <span class="font-mono text-emerald-400">\${{ totalAmount().toFixed(2) }}</span>
                    </div>
                  </div>

                  <!-- Checkout State Banner -->
                  @if (cartState() === 'CheckoutPending') {
                    <div class="mt-2 p-2 rounded-lg bg-amber-950/60 border border-amber-800 text-[11px] text-amber-300 flex items-center justify-between">
                      <span>⏱️ Stock Reserved for {{ formatTimer(activeReservationTimer()) }}</span>
                      <span class="font-bold font-mono">{{ reservationOrderId() }}</span>
                    </div>
                  }

                  @if (cartState() === 'OrderPlaced') {
                    <div class="mt-2 p-2 rounded-lg bg-emerald-950/80 border border-emerald-700 text-[11px] text-emerald-300 space-y-1">
                      <div class="font-bold flex items-center gap-1">
                        <svg class="w-3.5 h-3.5 text-emerald-400" fill="currentColor" viewBox="0 0 20 20">
                          <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/>
                        </svg>
                        Order Confirmed! ({{ reservationOrderId() }})
                      </div>
                      <p class="text-[10px] text-slate-300">
                        {{ selectedPaymentMethod() === 'Stripe' ? 'Payment link sent. Invoice generated.' : 'Cash on delivery registered. Awaiting courier dispatch.' }}
                      </p>
                    </div>
                  }
                </div>
              } @else {
                <div class="text-center py-6 text-slate-500 text-xs italic">
                  In-chat cart is currently empty. Add items from the catalog grid to test the checkout flow.
                </div>
              }
            </div>

            <!-- Simulator Action Strip -->
            <div class="p-3 bg-slate-900 border-t border-slate-800 flex items-center gap-2">
              <input
                type="text"
                readonly
                value="💬 Simulated Customer Session: {{ customerPhone() }}"
                class="flex-1 bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-400 select-none cursor-default"
              />
              <button
                (click)="clearCart()"
                [disabled]="cartItems().length === 0"
                class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-rose-950/80 hover:bg-rose-900 disabled:opacity-40 disabled:pointer-events-none text-rose-300 border border-rose-800 transition"
              >
                Clear Cart
              </button>
            </div>
          </div>

          <!-- Live Cart Session Controls & Checkout Drawer -->
          <div class="bg-slate-900/80 border border-slate-800 rounded-2xl p-5 space-y-5">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <svg class="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"/>
                </svg>
                <h3 class="text-sm font-bold text-slate-200">Active In-Chat Session Control</h3>
              </div>
              <span class="text-xs font-mono text-emerald-400 bg-emerald-500/10 px-2 py-0.5 rounded border border-emerald-500/20">
                {{ cartItems().length }} SKU(s)
              </span>
            </div>

            <!-- Customer Phone Selector -->
            <div>
              <label class="block text-xs font-medium text-slate-400 mb-1.5">Customer WhatsApp Phone Number</label>
              <div class="flex gap-2">
                <input
                  type="text"
                  [ngModel]="customerPhone()"
                  (ngModelChange)="customerPhone.set($event)"
                  class="flex-1 bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 font-mono focus:outline-none focus:border-emerald-500"
                />
                <button
                  (click)="resetPhoneSession()"
                  class="px-2.5 py-1.5 rounded-lg text-xs bg-slate-800 hover:bg-slate-700 text-slate-300 transition"
                  title="Switch to new customer phone"
                >
                  New
                </button>
              </div>
            </div>

            <!-- Cart Items Management -->
            @if (cartItems().length > 0) {
              <div class="space-y-2.5">
                <label class="block text-xs font-medium text-slate-400">Cart Line Items & Quantities</label>
                <div class="space-y-2 max-h-48 overflow-y-auto pr-1">
                  @for (item of cartItems(); track item.sku) {
                    <div class="bg-slate-950 border border-slate-800 rounded-lg p-2.5 flex items-center justify-between">
                      <div class="min-w-0 flex-1 pr-2">
                        <div class="text-xs font-medium text-slate-200 truncate">{{ item.title }}</div>
                        <div class="text-[10px] text-slate-400 font-mono">\${{ item.unitPrice.toFixed(2) }} each</div>
                      </div>

                      <div class="flex items-center gap-2">
                        <!-- Quantity Modifiers -->
                        <div class="flex items-center border border-slate-800 rounded bg-slate-900">
                          <button
                            (click)="adjustQuantity(item.sku, -1)"
                            class="px-2 py-0.5 text-slate-400 hover:text-slate-100 hover:bg-slate-800 text-xs font-bold rounded-l transition"
                          >
                            -
                          </button>
                          <span class="px-2 py-0.5 text-xs font-mono font-medium text-slate-200">{{ item.quantity }}</span>
                          <button
                            (click)="adjustQuantity(item.sku, 1)"
                            class="px-2 py-0.5 text-slate-400 hover:text-slate-100 hover:bg-slate-800 text-xs font-bold rounded-r transition"
                          >
                            +
                          </button>
                        </div>

                        <span class="text-xs font-mono font-bold text-emerald-400 w-16 text-right">
                          \${{ (item.quantity * item.unitPrice).toFixed(2) }}
                        </span>

                        <button
                          (click)="removeItem(item.sku)"
                          class="p-1 rounded text-slate-500 hover:text-rose-400 transition"
                          title="Remove item"
                        >
                          <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16"/>
                          </svg>
                        </button>
                      </div>
                    </div>
                  }
                </div>
              </div>
            }

            <!-- Coupon Code Section -->
            <div class="space-y-1.5 pt-2 border-t border-slate-800">
              <label class="block text-xs font-medium text-slate-400">Coupon Discount Engine</label>
              <div class="flex gap-2">
                <input
                  type="text"
                  [ngModel]="couponInput()"
                  (ngModelChange)="couponInput.set($event)"
                  placeholder="e.g. GROWTH20"
                  class="flex-1 bg-slate-950 border border-slate-800 rounded-lg px-3 py-1.5 text-xs text-slate-200 font-mono uppercase focus:outline-none focus:border-emerald-500"
                />
                <button
                  (click)="applyCoupon()"
                  [disabled]="cartItems().length === 0"
                  class="px-3.5 py-1.5 rounded-lg text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 disabled:opacity-40 disabled:pointer-events-none text-white transition shadow-sm"
                >
                  Apply
                </button>
              </div>

              @if (couponSuccess()) {
                <p class="text-[11px] text-emerald-400 mt-1 flex items-center gap-1">
                  <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 20 20">
                    <path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd"/>
                  </svg>
                  {{ couponSuccess() }}
                </p>
              }
              @if (couponError()) {
                <p class="text-[11px] text-rose-400 mt-1 flex items-center gap-1">
                  <svg class="w-3.5 h-3.5" fill="currentColor" viewBox="0 0 20 20">
                    <path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clip-rule="evenodd"/>
                  </svg>
                  {{ couponError() }}
                </p>
              }
            </div>

            <!-- Reservation TTL Countdown Bar -->
            @if (isReservationActive()) {
              <div class="p-3 rounded-xl bg-amber-950/40 border border-amber-800/80 space-y-1.5">
                <div class="flex items-center justify-between text-xs text-amber-300 font-medium">
                  <span>Atomic Stock Hold Active (15m TTL)</span>
                  <span class="font-mono font-bold">{{ formatTimer(activeReservationTimer()) }}</span>
                </div>
                <!-- Progress bar -->
                <div class="w-full bg-amber-950 rounded-full h-1.5 overflow-hidden">
                  <div
                    class="bg-amber-400 h-1.5 rounded-full transition-all duration-1000"
                    [style.width.%]="(activeReservationTimer() / 900) * 100"
                  ></div>
                </div>
              </div>
            }

            <!-- Payment Method Selector -->
            <div class="space-y-1.5">
              <label class="block text-xs font-medium text-slate-400">Payment & Invoicing Method</label>
              <div class="grid grid-cols-2 gap-2">
                <button
                  (click)="selectedPaymentMethod.set('Stripe')"
                  class="p-2.5 rounded-lg border text-left text-xs transition"
                  [ngClass]="selectedPaymentMethod() === 'Stripe' ? 'bg-emerald-500/10 border-emerald-500 text-emerald-300' : 'bg-slate-950 border-slate-800 text-slate-400 hover:text-slate-200'"
                >
                  <div class="font-semibold">Stripe Checkout</div>
                  <div class="text-[10px] text-slate-400 mt-0.5">Online card payment link</div>
                </button>

                <button
                  (click)="selectedPaymentMethod.set('COD')"
                  class="p-2.5 rounded-lg border text-left text-xs transition"
                  [ngClass]="selectedPaymentMethod() === 'COD' ? 'bg-emerald-500/10 border-emerald-500 text-emerald-300' : 'bg-slate-950 border-slate-800 text-slate-400 hover:text-slate-200'"
                >
                  <div class="font-semibold">Cash On Delivery</div>
                  <div class="text-[10px] text-slate-400 mt-0.5">Pay upon delivery</div>
                </button>
              </div>
            </div>

            <!-- Order Total & Checkout Action -->
            <div class="pt-3 border-t border-slate-800 space-y-3">
              <div class="flex items-baseline justify-between">
                <div>
                  <span class="text-xs text-slate-400">Total Payable</span>
                  <div class="text-xl font-black text-emerald-400 font-mono">\${{ totalAmount().toFixed(2) }} <span class="text-xs font-normal text-slate-400">USD</span></div>
                </div>

                @if (cartState() === 'CheckoutPending') {
                  <button
                    (click)="confirmOrder()"
                    class="px-4 py-2 rounded-xl text-xs font-bold bg-emerald-600 hover:bg-emerald-500 text-white transition flex items-center gap-1.5 shadow-lg shadow-emerald-600/30"
                  >
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/>
                    </svg>
                    Confirm & Place Order
                  </button>
                } @else {
                  <button
                    (click)="simulateReserveCheckout()"
                    [disabled]="cartItems().length === 0 || cartState() === 'OrderPlaced'"
                    class="px-4 py-2 rounded-xl text-xs font-bold bg-gradient-to-r from-emerald-600 to-teal-500 hover:from-emerald-500 hover:to-teal-400 disabled:opacity-40 disabled:pointer-events-none text-white transition flex items-center gap-1.5 shadow-lg shadow-emerald-500/20"
                  >
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z"/>
                    </svg>
                    Lock Stock & Checkout
                  </button>
                }
              </div>
            </div>
          </div>
        </div>
      </main>

      <!-- Meta WhatsApp Interactive Stanza Modal -->
      @if (isStanzaModalOpen()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-2xl w-full p-6 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <div class="w-7 h-7 rounded-lg bg-emerald-500/20 text-emerald-400 flex items-center justify-center font-mono text-xs font-bold">
                  JSON
                </div>
                <div>
                  <h3 class="text-sm font-bold text-slate-100">Meta WhatsApp Interactive Stanza</h3>
                  <p class="text-[11px] text-slate-400">Valid payload structure ready to send through WhatsApp Cloud API</p>
                </div>
              </div>
              <button (click)="isStanzaModalOpen.set(false)" class="text-slate-400 hover:text-slate-200 text-lg font-bold">
                ✕
              </button>
            </div>

            <!-- Interactive Type Selector -->
            <div class="flex items-center gap-2">
              <button
                (click)="switchStanzaType('product_list')"
                class="px-3 py-1 rounded-lg text-xs font-semibold transition"
                [ngClass]="stanzaModalType() === 'product_list' ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30' : 'bg-slate-800 text-slate-400'"
              >
                Multi-Product List (MPM)
              </button>
              <button
                (click)="switchStanzaType('single_product')"
                class="px-3 py-1 rounded-lg text-xs font-semibold transition"
                [ngClass]="stanzaModalType() === 'single_product' ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30' : 'bg-slate-800 text-slate-400'"
              >
                Single Product Stanza
              </button>
            </div>

            <!-- Code block -->
            <div class="relative bg-slate-950 border border-slate-800 rounded-xl p-4 overflow-x-auto max-h-96">
              <pre class="text-xs text-emerald-300 font-mono">{{ generatedStanzaJson() }}</pre>
            </div>

            <div class="flex items-center justify-between pt-2">
              <span class="text-xs text-slate-500">Target Phone: {{ customerPhone() }}</span>
              <div class="flex items-center gap-2">
                <button
                  (click)="copyStanza()"
                  class="px-3 py-1.5 rounded-lg text-xs font-semibold bg-slate-800 hover:bg-slate-700 text-slate-200 transition flex items-center gap-1.5"
                >
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z"/>
                  </svg>
                  {{ copiedStanza() ? 'Copied to Clipboard!' : 'Copy JSON Payload' }}
                </button>
                <button
                  (click)="isStanzaModalOpen.set(false)"
                  class="px-4 py-1.5 rounded-lg text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 text-white transition"
                >
                  Done
                </button>
              </div>
            </div>
          </div>
        </div>
      }

      <!-- Order Invoice Confirmation Modal -->
      @if (isCheckoutModalOpen()) {
        <div class="fixed inset-0 z-50 bg-slate-950/80 backdrop-blur-sm flex items-center justify-center p-4">
          <div class="bg-slate-900 border border-slate-800 rounded-2xl max-w-xl w-full p-6 space-y-4 shadow-2xl">
            <div class="flex items-center justify-between pb-3 border-b border-slate-800">
              <div class="flex items-center gap-2">
                <div class="w-8 h-8 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center">
                  <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/>
                  </svg>
                </div>
                <div>
                  <h3 class="text-sm font-bold text-slate-100">WhatsApp COD & Stripe Invoice Stanza</h3>
                  <p class="text-[11px] text-slate-400">Order {{ reservationOrderId() }} locked & formatted</p>
                </div>
              </div>
              <button (click)="isCheckoutModalOpen.set(false)" class="text-slate-400 hover:text-slate-200 text-lg font-bold">
                ✕
              </button>
            </div>

            <!-- Stanza Text Area -->
            <div class="bg-slate-950 border border-slate-800 rounded-xl p-4 overflow-y-auto max-h-72">
              <pre class="text-xs text-slate-200 font-mono whitespace-pre-wrap">{{ reservationInvoiceStanza() }}</pre>
            </div>

            <div class="flex items-center justify-between pt-2">
              <span class="text-xs text-slate-400">Method: {{ selectedPaymentMethod() }}</span>
              <div class="flex items-center gap-2">
                <button
                  (click)="confirmOrder()"
                  class="px-4 py-1.5 rounded-lg text-xs font-semibold bg-emerald-600 hover:bg-emerald-500 text-white transition flex items-center gap-1.5 shadow-md shadow-emerald-600/30"
                >
                  Confirm Customer Order
                </button>
              </div>
            </div>
          </div>
        </div>
      }
    </div>
  `
})
export class WhatsAppCatalogCartStudioComponent implements OnInit, OnDestroy {
  // Products Store
  readonly products = signal<CatalogProduct[]>([
    {
      sku: 'PRO-SAAS',
      title: 'Pro SaaS Plan',
      description: 'Monthly multi-tenant workspace with advanced automated workflows and omnichannel orchestration',
      category: 'Subscription',
      unitPrice: 99.00,
      currency: 'USD',
      stockAvailable: 100,
      imageUrl: 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=600',
      isActive: true
    },
    {
      sku: 'GROWTH-AI',
      title: 'Growth AI Bundle',
      description: 'AI autonomous copilot, voice synthesis & predictive customer intent engine with 1M tokens',
      category: 'Add-on',
      unitPrice: 149.00,
      currency: 'USD',
      stockAvailable: 50,
      imageUrl: 'https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=600',
      isActive: true
    },
    {
      sku: 'ACCEL-ONB',
      title: 'Onboarding Accelerator',
      description: 'White-glove CRM data migration, custom domain setup and SLA onboarding session with engineers',
      category: 'Services',
      unitPrice: 299.00,
      currency: 'USD',
      stockAvailable: 20,
      imageUrl: 'https://images.unsplash.com/photo-1552664730-d307ca884978?w=600',
      isActive: true
    },
    {
      sku: 'ENT-SLA',
      title: 'Enterprise SLA Pack',
      description: '24/7 dedicated telephone routing, 15-minute response SLA & priority dispatch guarantee',
      category: 'Support',
      unitPrice: 499.00,
      currency: 'USD',
      stockAvailable: 15,
      imageUrl: 'https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?w=600',
      isActive: true
    }
  ]);

  readonly categories = ['All', 'Subscription', 'Add-on', 'Services', 'Support'];
  readonly selectedCategory = signal<string>('All');
  readonly searchQuery = signal<string>('');

  readonly filteredProducts = computed(() => {
    let list = this.products().filter(p => p.isActive);

    const cat = this.selectedCategory();
    if (cat !== 'All') {
      list = list.filter(p => p.category.toLowerCase() === cat.toLowerCase());
    }

    const q = this.searchQuery().trim().toLowerCase();
    if (q) {
      list = list.filter(p =>
        p.title.toLowerCase().includes(q) ||
        p.sku.toLowerCase().includes(q) ||
        p.description.toLowerCase().includes(q)
      );
    }

    return list;
  });

  // Coupons
  readonly availableCoupons: AvailableCoupon[] = [
    { code: 'GROWTH20', type: 'Percentage', value: 20, minimumOrder: 50, description: '20% off orders > $50' },
    { code: 'WELCOME50', type: 'Fixed', value: 50, minimumOrder: 100, description: '$50 off orders > $100' },
    { code: 'FLASH10', type: 'Percentage', value: 10, minimumOrder: 0, description: '10% flash discount' },
    { code: 'ENTERPRISE100', type: 'Fixed', value: 100, minimumOrder: 300, description: '$100 off orders > $300' }
  ];

  // Cart Session State
  readonly customerPhone = signal<string>('+1 (555) 382-9014');
  readonly cartItems = signal<CartItem[]>([
    {
      sku: 'PRO-SAAS',
      title: 'Pro SaaS Plan',
      quantity: 1,
      unitPrice: 99.00,
      currency: 'USD',
      imageUrl: 'https://images.unsplash.com/photo-1460925895917-afdab827c52f?w=600'
    }
  ]);

  readonly cartState = signal<string>('Active');
  readonly appliedCoupon = signal<AppliedCoupon | null>(null);
  readonly couponInput = signal<string>('GROWTH20');
  readonly couponSuccess = signal<string>('');
  readonly couponError = signal<string>('');

  readonly subtotal = computed(() => {
    return this.cartItems().reduce((sum, item) => sum + (item.quantity * item.unitPrice), 0);
  });

  readonly totalAmount = computed(() => {
    const sub = this.subtotal();
    const discount = this.appliedCoupon()?.discountAmount ?? 0;
    return Math.max(0, sub - discount);
  });

  // Reservation & Checkout State
  readonly selectedPaymentMethod = signal<'Stripe' | 'COD'>('Stripe');
  readonly isReservationActive = signal<boolean>(false);
  readonly activeReservationTimer = signal<number>(900); // 15 minutes = 900s
  readonly reservationOrderId = signal<string>('ORD-WA-20261031-7B8A1');
  readonly reservationPaymentLink = signal<string>('');
  readonly reservationInvoiceStanza = signal<string>('');
  readonly isCheckoutModalOpen = signal<boolean>(false);

  // Stanza Generator Modal
  readonly isStanzaModalOpen = signal<boolean>(false);
  readonly stanzaModalType = signal<'product_list' | 'single_product'>('product_list');
  readonly selectedProductForStanza = signal<CatalogProduct | null>(null);
  readonly generatedStanzaJson = signal<string>('');
  readonly copiedStanza = signal<boolean>(false);

  private timerInterval: any = null;

  ngOnInit() {
    this.startCountdownTimer();
  }

  ngOnDestroy() {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
    }
  }

  private startCountdownTimer() {
    this.timerInterval = setInterval(() => {
      if (this.isReservationActive() && this.activeReservationTimer() > 0) {
        this.activeReservationTimer.update(t => t - 1);
        if (this.activeReservationTimer() <= 0) {
          this.isReservationActive.set(false);
          this.cartState.set('Expired');
        }
      }
    }, 1000);
  }

  formatTimer(seconds: number): string {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m}:${s < 10 ? '0' : ''}${s}`;
  }

  addToCart(product: CatalogProduct) {
    if (product.stockAvailable <= 0) return;

    this.cartItems.update(items => {
      const idx = items.findIndex(i => i.sku === product.sku);
      if (idx >= 0) {
        const copy = [...items];
        copy[idx] = { ...copy[idx], quantity: copy[idx].quantity + 1 };
        return copy;
      } else {
        return [...items, {
          sku: product.sku,
          title: product.title,
          quantity: 1,
          unitPrice: product.unitPrice,
          currency: product.currency,
          imageUrl: product.imageUrl
        }];
      }
    });

    this.cartState.set('Active');
    this.recalculateCouponIfPresent();
  }

  adjustQuantity(sku: string, delta: number) {
    this.cartItems.update(items => {
      return items.map(item => {
        if (item.sku === sku) {
          const newQty = item.quantity + delta;
          return newQty > 0 ? { ...item, quantity: newQty } : null;
        }
        return item;
      }).filter((item): item is CartItem => item !== null);
    });

    if (this.cartItems().length === 0) {
      this.cartState.set('Empty');
      this.appliedCoupon.set(null);
    } else {
      this.cartState.set('Active');
      this.recalculateCouponIfPresent();
    }
  }

  removeItem(sku: string) {
    this.cartItems.update(items => items.filter(i => i.sku !== sku));
    if (this.cartItems().length === 0) {
      this.cartState.set('Empty');
      this.appliedCoupon.set(null);
    } else {
      this.recalculateCouponIfPresent();
    }
  }

  clearCart() {
    this.cartItems.set([]);
    this.appliedCoupon.set(null);
    this.cartState.set('Cancelled');
    this.isReservationActive.set(false);
    this.couponSuccess.set('');
    this.couponError.set('');
  }

  resetPhoneSession() {
    const randomSuffix = Math.floor(1000 + Math.random() * 9000);
    this.customerPhone.set(`+1 (555) 382-${randomSuffix}`);
    this.clearCart();
    this.cartState.set('Empty');
  }

  quickApplyCoupon(code: string) {
    this.couponInput.set(code);
    this.applyCoupon();
  }

  applyCoupon() {
    this.couponSuccess.set('');
    this.couponError.set('');

    const code = this.couponInput().trim().toUpperCase();
    if (!code) {
      this.couponError.set('Please enter a coupon code.');
      return;
    }

    if (this.cartItems().length === 0) {
      this.couponError.set('Cannot apply coupon to an empty cart.');
      return;
    }

    const coupon = this.availableCoupons.find(c => c.code === code);
    if (!coupon) {
      this.couponError.set(`Coupon '${code}' is invalid or expired.`);
      return;
    }

    const sub = this.subtotal();
    if (sub < coupon.minimumOrder) {
      this.couponError.set(`Order subtotal of \$${sub.toFixed(2)} does not meet minimum requirement of \$${coupon.minimumOrder}.`);
      return;
    }

    let discount = 0;
    if (coupon.type === 'Percentage') {
      discount = Math.round((sub * (coupon.value / 100)) * 100) / 100;
    } else {
      discount = Math.min(coupon.value, sub);
    }

    this.appliedCoupon.set({
      code: coupon.code,
      type: coupon.type,
      value: coupon.value,
      discountAmount: discount,
      description: coupon.description
    });

    this.cartState.set('DiscountApplied');
    this.couponSuccess.set(`Coupon '${coupon.code}' applied! Saved \$${discount.toFixed(2)}.`);
  }

  private recalculateCouponIfPresent() {
    const current = this.appliedCoupon();
    if (!current) return;

    const coupon = this.availableCoupons.find(c => c.code === current.code);
    const sub = this.subtotal();
    if (coupon && sub >= coupon.minimumOrder) {
      let discount = 0;
      if (coupon.type === 'Percentage') {
        discount = Math.round((sub * (coupon.value / 100)) * 100) / 100;
      } else {
        discount = Math.min(coupon.value, sub);
      }
      this.appliedCoupon.set({ ...current, discountAmount: discount });
      this.cartState.set('DiscountApplied');
    } else {
      this.appliedCoupon.set(null);
      this.couponError.set(`Coupon '${current.code}' removed: minimum order requirement not met.`);
    }
  }

  simulateReserveCheckout() {
    if (this.cartItems().length === 0) return;

    const dateStr = new Date().toISOString().slice(0, 10).replace(/-/g, '');
    const rand = Math.random().toString(36).substring(2, 7).toUpperCase();
    const orderId = `ORD-WA-${dateStr}-${rand}`;
    const paymentLink = `https://checkout.stripe.com/c/pay/cs_live_${Math.random().toString(36).substring(2, 12)}?order=${orderId}`;

    this.reservationOrderId.set(orderId);
    this.reservationPaymentLink.set(paymentLink);
    this.cartState.set('CheckoutPending');
    this.isReservationActive.set(true);
    this.activeReservationTimer.set(900); // 15 mins

    // Build itemized invoice stanza
    const itemsText = this.cartItems()
      .map(i => `• ${i.quantity}x *${i.title}* (${i.sku}) - \$${(i.quantity * i.unitPrice).toFixed(2)}`)
      .join('\n');

    const discountText = this.appliedCoupon()
      ? `\nDiscount (${this.appliedCoupon()?.code}): -\$${this.appliedCoupon()?.discountAmount.toFixed(2)}`
      : '';

    const methodText = this.selectedPaymentMethod() === 'Stripe'
      ? `🔗 *Instant Stripe Payment Link:*\n${paymentLink}\n_Complete payment before stock hold expires._`
      : `🚚 *Cash on Delivery Selected.*\nPlease prepare exact cash upon delivery. Reply *CONFIRM* to verify.`;

    const stanza = `📦 *ORDER INVOICE & CONFIRMATION*\n──────────────────────────────\n🔖 *Order ID:* \`${orderId}\`\n👤 *Customer:* ${this.customerPhone()}\n💳 *Payment:* ${this.selectedPaymentMethod() === 'Stripe' ? 'Instant Online Card (Stripe)' : 'Cash on Delivery (COD)'}\n⏱️ *Stock Hold:* 15 Minutes TTL\n──────────────────────────────\n*Items Ordered:*\n${itemsText}\n──────────────────────────────\nSubtotal: \$${this.subtotal().toFixed(2)}${discountText}\n*Total Payable:* *\$${this.totalAmount().toFixed(2)} USD*\n──────────────────────────────\n${methodText}`;

    this.reservationInvoiceStanza.set(stanza);
    this.isCheckoutModalOpen.set(true);
  }

  confirmOrder() {
    this.cartState.set('OrderPlaced');
    this.isReservationActive.set(false);
    this.isCheckoutModalOpen.set(false);
  }

  openStanzaGenerator(product: CatalogProduct | null, type: 'product_list' | 'single_product') {
    this.selectedProductForStanza.set(product);
    this.stanzaModalType.set(type);
    this.copiedStanza.set(false);
    this.generateStanza(type, product);
    this.isStanzaModalOpen.set(true);
  }

  switchStanzaType(type: 'product_list' | 'single_product') {
    this.stanzaModalType.set(type);
    this.copiedStanza.set(false);
    this.generateStanza(type, this.selectedProductForStanza());
  }

  private generateStanza(type: 'product_list' | 'single_product', product: CatalogProduct | null) {
    if (type === 'single_product') {
      const p = product ?? this.products()[0];
      const stanzaObj = {
        messaging_product: 'whatsapp',
        recipient_type: 'individual',
        to: this.customerPhone(),
        type: 'interactive',
        interactive: {
          type: 'single_product',
          body: {
            text: `Explore ${p.title} directly from our verified WhatsApp catalog.`
          },
          footer: {
            text: 'SGrowing • Instant In-Chat Checkout'
          },
          action: {
            catalog_id: 'CATALOG_DEFAULT',
            product_retailer_id: p.sku
          }
        }
      };
      this.generatedStanzaJson.set(JSON.stringify(stanzaObj, null, 2));
    } else {
      const stanzaObj = {
        messaging_product: 'whatsapp',
        recipient_type: 'individual',
        to: this.customerPhone(),
        type: 'interactive',
        interactive: {
          type: 'product_list',
          header: {
            type: 'text',
            text: '⚡ Featured Business Catalog'
          },
          body: {
            text: 'Select an item below to review features and add it to your in-chat cart.'
          },
          footer: {
            text: 'SGrowing • Fast Delivery Available'
          },
          action: {
            catalog_id: 'CATALOG_DEFAULT',
            sections: [
              {
                title: 'Top Solutions',
                product_items: this.products().map(p => ({ product_retailer_id: p.sku }))
              }
            ]
          }
        }
      };
      this.generatedStanzaJson.set(JSON.stringify(stanzaObj, null, 2));
    }
  }

  copyStanza() {
    navigator.clipboard.writeText(this.generatedStanzaJson());
    this.copiedStanza.set(true);
    setTimeout(() => this.copiedStanza.set(false), 2000);
  }
}
