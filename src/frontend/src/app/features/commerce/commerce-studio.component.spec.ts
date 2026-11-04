// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { CommerceStudioComponent } from './commerce-studio.component';

describe('CommerceStudioComponent (Core Commerce Studio)', () => {
  let component: CommerceStudioComponent;

  beforeEach(() => {
    component = new CommerceStudioComponent();
  });

  it('should initialize with catalog tab and default product offerings', () => {
    expect(component.activeTab()).toBe('catalog');
    const prods = component.products();
    expect(prods.length).toBeGreaterThan(0);
    expect(prods.some(p => p.sku === 'SGR-GRW-01')).toBe(true);
  });

  it('should support switching between catalog, quotes, and carts tabs', () => {
    component.activeTab.set('quotes');
    expect(component.activeTab()).toBe('quotes');

    component.activeTab.set('carts');
    expect(component.activeTab()).toBe('carts');

    component.activeTab.set('catalog');
    expect(component.activeTab()).toBe('catalog');
  });

  it('should manage and accept commercial quotations', () => {
    component.activeTab.set('quotes');
    const quotes = component.quotes();
    expect(quotes.length).toBeGreaterThan(0);

    const targetQuote = quotes[0];
    expect(targetQuote.status).toBe('Sent');

    component.acceptQuote(targetQuote);
    expect(targetQuote.status).toBe('Accepted');
  });

  it('should simulate cart recovery scan and update detected carts', () => {
    component.activeTab.set('carts');
    component.carts.update(items => [
      ...items,
      { id: 'cart_test', email: 'test@example.com', total: 99, status: 'Detected', detectedTime: 'Just now' }
    ]);
    expect(component.carts().some(c => c.status === 'Detected')).toBe(true);

    component.triggerCartScan();
    expect(component.carts().some(c => c.id === 'cart_test' && c.status === 'RecoveryTriggered')).toBe(true);
  });

  it('should allow adding new product to products catalog signal', () => {
    component.products.update(list => [
      ...list,
      {
        id: 'p4',
        name: 'Omnichannel Voice Router Gateway',
        sku: 'SGR-VOICE-GW',
        type: 'Addon',
        price: 499,
        stock: 100,
        isActive: true
      }
    ]);
    expect(component.products().length).toBe(4);
    expect(component.products()[3].sku).toBe('SGR-VOICE-GW');
  });
});
