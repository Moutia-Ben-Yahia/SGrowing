// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { ContractsStudioComponent } from './contracts-studio.component';

describe('ContractsStudioComponent', () => {
  let component: ContractsStudioComponent;

  beforeEach(() => {
    component = new ContractsStudioComponent();
    component.ngOnInit();
  });

  it('should initialize with pre-seeded digital contracts and metrics', () => {
    expect(component).toBeTruthy();
    expect(component.contracts().length).toBeGreaterThanOrEqual(2);
    expect(component.totalValue()).toBe(48000);
    expect(component.signedValue()).toBe(0);
    expect(component.pendingCount()).toBe(1);
  });

  it('should save a new draft contract and recalculate metrics', () => {
    component.showCreateModal.set(true);
    component.newContract = {
      title: 'North Africa Logistics MSA',
      contractType: 'MSA',
      signerName: 'Kamel Riahi',
      signerEmail: 'kamel@hyperlogistics.tn',
      contractValue: 60000
    };

    const countBefore = component.contracts().length;
    component.saveDraft();

    expect(component.contracts().length).toBe(countBefore + 1);
    expect(component.showCreateModal()).toBe(false);
    expect(component.totalValue()).toBe(108000);
    const newest = component.contracts()[0];
    expect(newest.title).toBe('North Africa Logistics MSA');
    expect(newest.status).toBe('Draft');
  });

  it('should dispatch contract invitation to signer and update status to Sent', () => {
    const draft = component.contracts()[0];
    draft.status = 'Draft';

    component.dispatchContract(draft);

    expect(draft.status).toBe('Sent');
    expect(component.pendingCount()).toBeGreaterThan(0);
  });

  it('should simulate customer signing, generate signed PDF URL, and update signed revenue', () => {
    const target = component.contracts()[0];
    component.simulateCustomerSign(target);

    expect(target.status).toBe('Signed');
    expect(target.signedPdfUrl).toContain('_signed.pdf');
    expect(component.signedValue()).toBeGreaterThan(0);
  });
});
