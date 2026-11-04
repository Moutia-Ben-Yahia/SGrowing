// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { SubscriptionDunningStudioComponent } from './subscription-dunning-studio.component';
import { TranslationService } from '../../core/services/translation.service';

describe('SubscriptionDunningStudioComponent', () => {
  let component: SubscriptionDunningStudioComponent;

  beforeEach(() => {
    component = new SubscriptionDunningStudioComponent(new TranslationService());
  });

  it('should initialize with 2 invoices and 4 tax rules', () => {
    expect(component.invoices().length).toBe(2);
    expect(component.taxRules().length).toBe(4);
    expect(component.pastDueInvoices().length).toBe(1);
    expect(component.recoveredTotal()).toBe(1299);
  });

  it('should add a new past due invoice', () => {
    component.createDemoInvoice();
    expect(component.invoices().length).toBe(3);
    expect(component.pastDueInvoices().length).toBe(2);
  });

  it('should retry delinquent payment and recover invoice', () => {
    component.retryInvoicePayment('inv_4402');
    expect(component.pastDueInvoices().length).toBe(0);
    const recovered = component.invoices().find(i => i.id === 'inv_4402');
    expect(recovered?.status).toBe('Paid');
    expect(recovered?.dunningAttempts).toBe(2);
    expect(component.recoveredTotal()).toBe(4799);
  });

  it('should ignore retryInvoicePayment when invoice id does not match', () => {
    const initialPastDueCount = component.pastDueInvoices().length;
    const initialRecoveredTotal = component.recoveredTotal();

    component.retryInvoicePayment('inv_non_existent');

    expect(component.pastDueInvoices().length).toBe(initialPastDueCount);
    expect(component.recoveredTotal()).toBe(initialRecoveredTotal);
  });

  it('should verify tax rules list and region labels', () => {
    const rules = component.taxRules();
    expect(rules.length).toBe(4);
    expect(rules.some(r => r.region.includes('European Union'))).toBe(true);
    expect(rules.some(r => r.region.includes('United States'))).toBe(true);
  });
});
