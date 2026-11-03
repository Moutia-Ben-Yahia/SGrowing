// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { FormsStudioComponent } from './forms-studio.component';

describe('FormsStudioComponent', () => {
  let component: FormsStudioComponent;
  let mockApi: any;
  let mockAuth: any;

  const mockForm = {
    id: 'f1',
    tenantId: 'tenant-123',
    title: 'High-Volume COD Merchant Intake',
    slug: 'merchant-intake',
    description: 'Logistics intake survey',
    submissionCount: 4,
    fields: [
      { fieldKey: 'companyName', label: 'Company Name', fieldType: 'text', isRequired: true, options: [] }
    ]
  };

  const mockSubmissions = [
    {
      id: 'sub1',
      tenantId: 'tenant-123',
      formId: 'f1',
      submittedAtUtc: '2026-11-03T10:00:00Z',
      answers: { companyName: 'Atlas Express' },
      completedInSeconds: 42
    }
  ];

  const mockDoc = {
    id: 'doc1',
    tenantId: 'tenant-123',
    title: 'Merchant SLA & Courier Remittance Guarantee',
    content: 'Agreement text',
    isSealed: false,
    signaturePlacementJson: '[]',
    tamperProofHash: 'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855'
  };

  beforeEach(() => {
    mockApi = {
      getForms: vi.fn().mockReturnValue(of([mockForm])),
      createForm: vi.fn().mockReturnValue(of(mockForm)),
      getSubmissions: vi.fn().mockReturnValue(of(mockSubmissions)),
      submitFormAnswers: vi.fn().mockReturnValue(of({
        id: 'sub2',
        tenantId: 'tenant-123',
        formId: 'f1',
        submittedAtUtc: '2026-11-03T11:00:00Z',
        answers: { companyName: 'E-Store Carthage', parcelVolume: '2000-10000' },
        completedInSeconds: 35
      })),
      createDocument: vi.fn().mockReturnValue(of(mockDoc)),
      sealDocument: vi.fn().mockReturnValue(of({ ...mockDoc, isSealed: true })),
      verifyDocumentAuditChain: vi.fn().mockReturnValue(of({
        summary: 'All cryptographic hashes match and signature audit trail is intact.'
      }))
    };

    mockAuth = {
      currentTenant: signal({ id: 'tenant-123', name: 'Test Tenant' })
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi },
        { provide: AuthService, useValue: mockAuth }
      ]
    });

    component = runInInjectionContext(injector, () => new FormsStudioComponent());
    component.ngOnInit();
  });

  it('should initialize and load published forms, submissions, and sample document', () => {
    expect(component).toBeTruthy();
    expect(mockApi.getForms).toHaveBeenCalledWith('tenant-123');
    expect(component.forms().length).toBe(1);
    expect(component.selectedForm()?.id).toBe('f1');
    expect(component.submissions().length).toBe(1);
    expect(component.activeDoc()?.id).toBe('doc1');
  });

  it('should switch navigation tabs between builder, submissions, and documents', () => {
    component.activeTab.set('submissions');
    expect(component.activeTab()).toBe('submissions');

    component.activeTab.set('documents');
    expect(component.activeTab()).toBe('documents');
  });

  it('should submit sample form response and update local submissions count', () => {
    component.sampleMerchantName = 'E-Store Carthage';
    component.submitSampleAnswers();

    expect(mockApi.submitFormAnswers).toHaveBeenCalledWith('merchant-intake', expect.objectContaining({
      tenantId: 'tenant-123',
      formId: 'f1'
    }));
    expect(component.submissions().length).toBe(2);
    expect(component.selectedForm()?.submissionCount).toBe(5);
    expect(component.submitSuccess()).toContain('Form response recorded');
  });

  it('should seal cryptographic document with digital signature', () => {
    component.sealDocumentDemo();

    expect(mockApi.sealDocument).toHaveBeenCalledWith(expect.objectContaining({
      tenantId: 'tenant-123',
      documentId: 'doc1',
      signerEmail: 'director@merchant.tn'
    }));
    expect(component.activeDoc()?.isSealed).toBe(true);
  });

  it('should verify document audit chain and update verification summary', () => {
    component.verifyAuditChain();

    expect(mockApi.verifyDocumentAuditChain).toHaveBeenCalledWith('tenant-123', 'doc1');
    expect(component.verificationResult()).toContain('cryptographic hashes match');
  });
});
