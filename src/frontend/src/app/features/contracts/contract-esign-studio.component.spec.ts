// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { ContractEsignStudioComponent } from './contract-esign-studio.component';

describe('ContractEsignStudioComponent (Enterprise E-Sign & Cryptographic Sealing Studio)', () => {
  let component: ContractEsignStudioComponent;

  const mockContracts = [
    {
      id: 'contract-101',
      title: 'Enterprise Master Services Agreement',
      agreementType: 'MSA',
      status: 'AwaitingSigner1',
      pageCount: 3,
      signerCount: 2,
      createdAtUtc: '2026-11-01T09:00:00Z',
      allSigned: false
    }
  ];

  const mockEnvelope = {
    id: 'contract-101',
    title: 'Enterprise Master Services Agreement',
    description: 'B2B Software Development & SaaS Agreement',
    agreementType: 'MSA',
    status: 'AwaitingSigner1',
    currentSignerIndex: 1,
    sha256Hash: 'a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4e5f6a1b2',
    isFullyExecuted: false,
    createdAtUtc: '2026-11-01T09:00:00Z',
    pages: [
      { pageNumber: 1, title: 'Scope of Services', content: 'Detailed terms and deliverables...' },
      { pageNumber: 2, title: 'Commercial Pricing & Payment', content: 'Net-30 payment terms and fees...' },
      { pageNumber: 3, title: 'Execution & Signatures', content: 'Authorized signatures and seals...' }
    ],
    fields: [
      {
        id: 'f-1',
        fieldType: 'Signature' as const,
        pageNumber: 3,
        xNorm: 0.1,
        yNorm: 0.8,
        widthNorm: 0.3,
        heightNorm: 0.1,
        signerIndex: 1,
        label: 'Client Signature',
        isRequired: true
      }
    ],
    signers: [
      {
        id: 's-1',
        orderIndex: 1,
        name: 'Sarah Connor',
        email: 'sarah@cyberdyne.com',
        role: 'ClientSigner' as const,
        token: 'token-sarah-101',
        status: 'ReadyToSign' as const
      },
      {
        id: 's-2',
        orderIndex: 2,
        name: 'John Connor',
        email: 'john@cyberdyne.com',
        role: 'Witness' as const,
        token: 'token-john-102',
        status: 'Pending' as const
      }
    ],
    auditTrail: [
      {
        id: 'a-1',
        timestampUtc: '2026-11-01T09:00:00Z',
        action: 'EnvelopeCreated',
        performedBy: 'System',
        details: 'Envelope dispatched for signature'
      }
    ]
  };

  const mockHttp = {
    get: (url: string) => {
      if (url.endsWith('/e-sign')) return of(mockContracts);
      if (url.includes('/contract-101')) return of(mockEnvelope);
      return of({});
    },
    post: () => of({ success: true })
  };

  beforeEach(() => {
    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new ContractEsignStudioComponent());
    component.ngOnInit();
  });

  it('should initialize with contracts list and auto-select first contract', () => {
    expect(component.contracts().length).toBeGreaterThanOrEqual(1);
    expect(component.selectedContractId()).toBe('contract-101');
    expect(component.selectedContract()).not.toBeNull();
    expect(component.selectedContract()?.title).toBe('Enterprise Master Services Agreement');
  });

  it('should compute totalPages and paginate forward and backward', () => {
    expect(component.totalPages()).toBe(3);
    expect(component.currentPage()).toBe(1);

    component.nextPage();
    expect(component.currentPage()).toBe(2);
    expect(component.currentPageData()?.title).toBe('Commercial Pricing & Payment');

    component.nextPage();
    expect(component.currentPage()).toBe(3);

    // Cannot advance past total pages
    component.nextPage();
    expect(component.currentPage()).toBe(3);

    component.prevPage();
    expect(component.currentPage()).toBe(2);
  });

  it('should filter fields by current page correctly', () => {
    component.currentPage.set(1);
    expect(component.currentPageFields().length).toBe(0);

    component.currentPage.set(3);
    expect(component.currentPageFields().length).toBe(1);
    expect(component.currentPageFields()[0].label).toBe('Client Signature');
  });

  it('should detect active signer ready to sign based on currentSignerIndex', () => {
    const signer = component.activeSigner();
    expect(signer).not.toBeNull();
    expect(signer?.name).toBe('Sarah Connor');
    expect(signer?.role).toBe('ClientSigner');
    expect(signer?.status).toBe('ReadyToSign');
  });

  it('should format status strings with spaces cleanly', () => {
    expect(component.formatStatus('AwaitingSigner1')).toBe('Awaiting Signer1');
    expect(component.formatStatus('FullyExecuted')).toBe('Fully Executed');
  });
});
