// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { of } from 'rxjs';
import { SealingInspectorComponent } from './sealing-inspector.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import {
  CryptographicSealResultDto,
  SealVerificationResultDto,
  DocumentTelemetrySummaryDto
} from '../../core/models/inspiration.models';

describe('SealingInspectorComponent', () => {
  let component: SealingInspectorComponent;
  let apiMock: any;
  let authMock: any;

  const mockSealResult: CryptographicSealResultDto = {
    documentId: '00000000-0000-0000-0000-000000000001',
    sha256Hash: '9a6c71c26b911a3b98e1fa923e1c25f4b0086208bb42b083bda4060824b22c3e',
    signatureFormat: 'ISO 32000-1 PKCS#7 / PAdES-B-LTV',
    certificateIssuer: 'CN=SGrowing Sovereign Root CA, O=SGrowing Security Grid',
    hasRfc3161Timestamp: true,
    timestampUtc: '2026-11-03T18:00:00Z',
    sealedPdfUrl: '/api/v1/documents/00000000-0000-0000-0000-000000000001/download-sealed',
    tamperProofVerificationToken: 'vtok_pkcs7_99341829031_sealed'
  };

  const mockVerifyResult: SealVerificationResultDto = {
    documentId: '00000000-0000-0000-0000-000000000001',
    isSealValid: true,
    integrityIntact: true,
    verificationTokenValid: true,
    documentSha256: '9a6c71c26b911a3b98e1fa923e1c25f4b0086208bb42b083bda4060824b22c3e',
    computedByteRangeHash: '9a6c71c26b911a3b98e1fa923e1c25f4b0086208bb42b083bda4060824b22c3e',
    signatureFormat: 'ISO 32000-1 PKCS#7 / PAdES-B-LTV',
    certificateIssuer: 'CN=SGrowing Sovereign Root CA, O=SGrowing Security Grid',
    hasRfc3161Timestamp: true,
    timestampUtc: '2026-11-03T18:00:00Z',
    status: 'Valid',
    message: 'Seal verified',
    certificate: {
      documentId: '00000000-0000-0000-0000-000000000001',
      documentTitle: 'Commercial Agreement',
      signerName: 'Sarah Jenkins',
      signerEmail: 'director@sgrowing.com',
      signerIpAddress: '197.14.120.45',
      userAgent: 'Chrome',
      signedAtUtc: '2026-11-03T18:00:00Z',
      documentChecksumSha256: '9a6c71c26b911a3b98e1fa923e1c25f4b0086208bb42b083bda4060824b22c3e',
      tsaAuthorityName: 'DigiCert',
      verificationUrl: 'https://verify.sgrowing.io/verify/00000000-0000-0000-0000-000000000001'
    }
  };

  const mockTelemetry: DocumentTelemetrySummaryDto = {
    documentId: '00000000-0000-0000-0000-000000000001',
    totalDocumentDwellSeconds: 95,
    totalViews: 4,
    uniqueViewers: 2,
    hasPricingHighInterestAlert: false,
    lastViewedAtUtc: '2026-11-03T18:00:00Z',
    pageDwellBreakdown: [
      { pageNumber: 1, totalDwellSeconds: 30, viewCount: 2, averageDwellSeconds: 15, attentionPercentage: 31.6, isPricingPage: false, hasHighInterestAlert: false }
    ],
    dropOffHeatmap: [
      { pageNumber: 1, viewersReached: 2, retentionRate: 100, dropOffRate: 0 }
    ],
    highInterestAlerts: []
  };

  beforeEach(() => {
    apiMock = {
      sealDocumentPkcs7: vi.fn().mockReturnValue(of(mockSealResult)),
      verifyDocumentSeal: vi.fn().mockReturnValue(of(mockVerifyResult)),
      getDocumentTelemetry: vi.fn().mockReturnValue(of(mockTelemetry)),
      recordDocumentPageDwell: vi.fn().mockReturnValue(of({ success: true }))
    };

    authMock = {
      currentTenant: vi.fn().mockReturnValue({ id: 'tenant-001', name: 'Acme HQ' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiMock },
      { provide: AuthService, useValue: authMock }
    ]);

    injector.runInContext(() => {
      component = new SealingInspectorComponent();
      component.ngOnInit();
    });
  });

  it('initializes and triggers seal and telemetry fetching on init', () => {
    expect(apiMock.sealDocumentPkcs7).toHaveBeenCalled();
    expect(apiMock.getDocumentTelemetry).toHaveBeenCalled();
    expect(component.sealResult()).toEqual(mockSealResult);
    expect(component.verificationResult()).toEqual(mockVerifyResult);
    expect(component.telemetry()).toEqual(mockTelemetry);
  });

  it('triggers onDocumentChanged and refreshes telemetry and verification', () => {
    apiMock.getDocumentTelemetry.mockClear();
    apiMock.verifyDocumentSeal.mockClear();

    component.documentId = '00000000-0000-0000-0000-000000000002';
    component.onDocumentChanged();

    expect(apiMock.getDocumentTelemetry).toHaveBeenCalledWith('00000000-0000-0000-0000-000000000002', 'tenant-001');
    expect(apiMock.verifyDocumentSeal).toHaveBeenCalled();
  });

  it('simulates pricing dwell and refreshes telemetry', () => {
    apiMock.recordDocumentPageDwell.mockReturnValue(of({}));
    component.simulatePricingDwell(20);

    expect(apiMock.recordDocumentPageDwell).toHaveBeenCalledWith(
      '00000000-0000-0000-0000-000000000001',
      3,
      20,
      '197.14.120.45',
      'director@sgrowing.com',
      100,
      'tenant-001'
    );
  });

  it('manages completion certificate modal visibility', () => {
    expect(component.showCertModal()).toBe(false);

    component.openCertificateModal();
    expect(component.showCertModal()).toBe(true);

    component.closeCertificateModal();
    expect(component.showCertModal()).toBe(false);
  });

  it('should trigger verifySeal when certificate is null on openCertificateModal and handle api error gracefully', () => {
    component.certificate.set(null);
    apiMock.verifyDocumentSeal.mockClear();

    component.openCertificateModal();
    expect(apiMock.verifyDocumentSeal).toHaveBeenCalled();
    expect(component.showCertModal()).toBe(true);

    component.closeCertificateModal();
    expect(component.showCertModal()).toBe(false);
  });
});
