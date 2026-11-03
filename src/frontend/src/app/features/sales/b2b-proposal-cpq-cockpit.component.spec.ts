// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { B2BProposalCpqCockpitComponent } from './b2b-proposal-cpq-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('B2BProposalCpqCockpitComponent', () => {
  let component: B2BProposalCpqCockpitComponent;
  let mockTranslation: TranslationService;

  beforeEach(() => {
    mockTranslation = new TranslationService();
    component = new B2BProposalCpqCockpitComponent(mockTranslation);
  });

  it('calculates initial subtotal, total, and margin above 35%', () => {
    expect(component.lineItems().length).toBe(3);
    expect(component.subtotal()).toBeGreaterThan(5000);
    expect(component.totalAmount()).toBeGreaterThan(5000);
    expect(component.effectiveMarginPercent()).toBeGreaterThanOrEqual(35);
  });

  it('adds and removes line items dynamically', () => {
    const initialCount = component.lineItems().length;
    component.addLineItem();
    expect(component.lineItems().length).toBe(initialCount + 1);

    component.removeLineItem(initialCount);
    expect(component.lineItems().length).toBe(initialCount);
  });

  it('detects margin floor violation when heavy discount is applied', () => {
    // Apply 70% discount to all items
    component.lineItems.update(items =>
      items.map(i => ({ ...i, discountPercent: 70 }))
    );
    expect(component.effectiveMarginPercent()).toBeLessThan(35);
  });

  it('generates SHA-256 digital seal when proposal is signed', () => {
    expect(component.lastSignedHash()).toBeNull();
    component.signerName = 'Chief Revenue Officer';
    component.signAndSealProposal();

    expect(component.lastSignedHash()).not.toBeNull();
    expect(component.lastSignedHash()).toMatch(/^sha256_/);
  });

  it('calculates metered overage charges for API and CAPI consumption', () => {
    const overage = component.calculatedOverage();
    expect(overage).toBeGreaterThan(0);
  });
});
