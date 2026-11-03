// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { WhatsAppCatalogCartStudioComponent } from './whatsapp-catalog-cart-studio.component';

describe('WhatsAppCatalogCartStudioComponent', () => {
  let component: WhatsAppCatalogCartStudioComponent;

  beforeEach(() => {
    vi.useFakeTimers();
    component = new WhatsAppCatalogCartStudioComponent();
    component.ngOnInit();
  });

  afterEach(() => {
    component.ngOnDestroy();
    vi.useRealTimers();
  });

  it('should initialize with products, initial cart item, and active status', () => {
    expect(component).toBeTruthy();
    expect(component.products().length).toBeGreaterThan(0);
    expect(component.cartItems().length).toBeGreaterThan(0);
    expect(component.subtotal()).toBeGreaterThan(0);
    expect(component.cartState()).toBe('Active');
  });

  it('should add products to cart and increase subtotal', () => {
    const initialSubtotal = component.subtotal();
    const product = component.products()[1]; // Growth AI Bundle ($149)

    component.addToCart(product);

    expect(component.cartItems().some(i => i.sku === product.sku)).toBe(true);
    expect(component.subtotal()).toBe(initialSubtotal + product.unitPrice);
  });

  it('should apply valid coupon code and calculate proportional discount', () => {
    component.couponInput.set('GROWTH20'); // 20% off
    component.applyCoupon();

    expect(component.appliedCoupon()).toBeTruthy();
    expect(component.appliedCoupon()?.code).toBe('GROWTH20');
    expect(component.appliedCoupon()?.discountAmount).toBeGreaterThan(0);
    expect(component.totalAmount()).toBeLessThan(component.subtotal());
  });

  it('should reject invalid or non-existent coupon codes', () => {
    component.couponInput.set('FAKE-COUPON-999');
    component.applyCoupon();

    expect(component.couponError()).toBeTruthy();
    expect(component.appliedCoupon()).toBeNull();
  });

  it('should format countdown timer correctly and handle expiration', () => {
    expect(component.formatTimer(900)).toBe('15:00');
    expect(component.formatTimer(65)).toBe('1:05');
    expect(component.formatTimer(9)).toBe('0:09');

    component.isReservationActive.set(true);
    component.activeReservationTimer.set(2);

    vi.advanceTimersByTime(2000);

    expect(component.isReservationActive()).toBe(false);
    expect(component.cartState()).toBe('Expired');
  });
});
