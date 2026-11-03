// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of } from 'rxjs';
import { MultipartySigningCockpitComponent } from './multiparty-signing-cockpit.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';

describe('MultipartySigningCockpitComponent', () => {
  let component: MultipartySigningCockpitComponent;
  let mockApi: any;
  let mockAuth: any;

  beforeEach(() => {
    vi.stubGlobal('alert', vi.fn());

    mockApi = {
      createMultiPartyContract: vi.fn().mockReturnValue(of({
        contractId: 'contract-multi-99',
        tenantId: '00000000-0000-0000-0000-000000000001',
        title: 'Enterprise Multi-Signer Agreement',
        documentUrl: 'https://storage.sgrowing.io/contracts/msa.pdf',
        workflowType: 0,
        overallStatus: 'Draft',
        currentSequentialOrderIndex: 1,
        signers: [
          { signerId: 's1', orderIndex: 1, fullName: 'CEO Client Corp', email: 'ceo@client.com', role: 0, status: 1, signingToken: 'tok_ceo', isOtpVerified: false, requiresOtp: true },
          { signerId: 's2', orderIndex: 2, fullName: 'CFO Client Corp', email: 'cfo@client.com', role: 0, status: 0, signingToken: 'tok_cfo', isOtpVerified: false, requiresOtp: false }
        ],
        visualFields: []
      })),
      startSigningWorkflow: vi.fn().mockReturnValue(of({
        contractId: 'contract-multi-99',
        overallStatus: 'InProgress',
        currentSequentialOrderIndex: 1,
        signers: []
      })),
      sendSignerOtp: vi.fn().mockReturnValue(of({ message: 'OTP 829104 dispatched via SMS' })),
      submitSignerSignature: vi.fn().mockReturnValue(of({
        contractId: 'contract-multi-99',
        overallStatus: 'Signed',
        signers: []
      })),
      getDocumentTelemetry: vi.fn().mockReturnValue(of({
        documentId: 'doc-1',
        totalDocumentDwellSeconds: 120,
        totalViews: 6,
        uniqueViewers: 2,
        hasPricingHighInterestAlert: true,
        pageDwellBreakdown: []
      })),
      verifyDocumentSeal: vi.fn().mockReturnValue(of({
        documentId: 'doc-1',
        isSealValid: true,
        integrityIntact: true,
        verificationTokenValid: true,
        certificate: {
          documentId: 'doc-1',
          documentTitle: 'Master Commercial Agreement',
          signerName: 'Chief Executive Officer',
          signerEmail: 'ceo@clientcorp.com'
        }
      }))
    };

    mockAuth = {
      currentUser: vi.fn().mockReturnValue({ tenantId: '00000000-0000-0000-0000-000000000001' })
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi },
        { provide: AuthService, useValue: mockAuth }
      ]
    });

    component = runInInjectionContext(injector, () => new MultipartySigningCockpitComponent());
  });

  it('should initialize and create a multi-signer contract agreement', () => {
    expect(component).toBeTruthy();
    component.createContract();

    expect(mockApi.createMultiPartyContract).toHaveBeenCalled();
    const contract = component.contract();
    expect(contract).toBeDefined();
    expect(contract?.title).toBe('Enterprise Multi-Signer Agreement');
    expect(contract?.signers.length).toBe(2);
  });

  it('should dispatch signing workflow invitations', () => {
    component.createContract();
    component.startWorkflow();

    expect(mockApi.startSigningWorkflow).toHaveBeenCalled();
    expect(component.contract()?.overallStatus).toBe('InProgress');
  });

  it('should send OTP verification challenge to signer', () => {
    component.sendOtp('tok_ceo');

    expect(mockApi.sendSignerOtp).toHaveBeenCalledWith('tok_ceo');
    expect(globalThis.alert).toHaveBeenCalledWith('OTP 829104 dispatched via SMS');
  });

  it('should simulate signer cryptographic signature submission', () => {
    component.simulateSign('tok_ceo', 'CEO Client Corp');

    expect(mockApi.submitSignerSignature).toHaveBeenCalledWith('tok_ceo', expect.any(Object));
    expect(component.contract()?.overallStatus).toBe('Signed');
  });

  it('should open completion certificate and document telemetry modal', () => {
    component.createContract();
    component.openTelemetryModal();

    expect(mockApi.getDocumentTelemetry).toHaveBeenCalled();
    expect(mockApi.verifyDocumentSeal).toHaveBeenCalled();
    expect(component.showTelemetryModal()).toBe(true);

    component.closeTelemetryModal();
    expect(component.showTelemetryModal()).toBe(false);
  });

  it('should map signer status codes to badges and display names', () => {
    expect(component.getSignerStatusName(0)).toBe('Pending');
    expect(component.getSignerStatusName(1)).toBe('Sent');
    expect(component.getSignerStatusName(4)).toBe('Signed');

    expect(component.getSignerStatusBadge(4)).toContain('emerald');
    expect(component.getSignerStatusBadge(1)).toContain('indigo');
    expect(component.getSignerStatusBadge(0)).toContain('slate');
  });
});
