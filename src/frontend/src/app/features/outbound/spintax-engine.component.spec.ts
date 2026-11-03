// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { of } from 'rxjs';
import { SpintaxEngineComponent } from './spintax-engine.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { SpintaxPreviewResponse, EmailVerificationResult, EnrichedContactDto } from '../../core/models/pillars.models';

describe('SpintaxEngineComponent', () => {
  let component: SpintaxEngineComponent;
  let apiMock: any;
  let authMock: any;

  const mockSpintaxResult: SpintaxPreviewResponse = {
    originalTemplate: '{Hi|Hello}, {FirstName}!',
    deterministicSeed: 'ceo@targetcorp.io',
    evaluatedVariant: 'Hello, Sarah! Hope all is well.',
    uniqueVariantPermutations: 36,
    samplePermutations: [
      'Hello, Sarah! Hope all is well.',
      'Hi, Sarah! Hope you are having a productive week.'
    ]
  };

  const mockVerificationResult: EmailVerificationResult = {
    email: 'alex.founder@growth-scale.io',
    domain: 'growth-scale.io',
    mxRecordsFound: true,
    smtpHandshakeSuccess: true,
    isDisposable: false,
    isRoleBased: false,
    isCatchAll: false,
    status: 'Deliverable',
    overallDeliverabilityScore: 97
  };

  const mockEnrichmentResult: EnrichedContactDto = {
    id: 'c-enrich-1',
    firstName: 'Alex',
    lastName: 'Vance',
    email: 'alex@fintech-ventures.com',
    jobTitle: 'VP Engineering & Infrastructure',
    seniorityLevel: 'VP',
    companyName: 'Fintech Ventures',
    industry: 'Financial Technology',
    employeeRange: '100-250',
    techStack: 'Kubernetes, Go, PostgreSQL, React',
    overallDataConfidenceScore: 92
  };

  beforeEach(() => {
    apiMock = {
      previewSpintax: vi.fn().mockReturnValue(of(mockSpintaxResult)),
      verifyEmailDeliverability: vi.fn().mockReturnValue(of(mockVerificationResult)),
      enrichAndVerifyContact: vi.fn().mockReturnValue(of(mockEnrichmentResult))
    };

    authMock = {
      currentUser: vi.fn().mockReturnValue({ id: 'u-1', tenantId: 'tenant-999' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiMock },
      { provide: AuthService, useValue: authMock }
    ]);

    injector.runInContext(() => {
      component = new SpintaxEngineComponent();
    });
  });

  it('switches between spintax, verifier, and enrichment tabs', () => {
    expect(component.activeTab()).toBe('spintax');

    component.activeTab.set('verifier');
    expect(component.activeTab()).toBe('verifier');

    component.activeTab.set('enrichment');
    expect(component.activeTab()).toBe('enrichment');
  });

  it('compiles deterministic spintax variants via previewSpintax', () => {
    component.generateSpintaxPreview();

    expect(apiMock.previewSpintax).toHaveBeenCalledWith(
      component.spintaxTemplate,
      component.prospectSeedEmail,
      { FirstName: 'Sarah' },
      3
    );
    expect(component.spintaxResult()).toEqual(mockSpintaxResult);
    expect(component.isLoadingSpintax()).toBe(false);
  });

  it('verifies email deliverability via verifyEmailDeliverability', () => {
    component.runVerification();

    expect(apiMock.verifyEmailDeliverability).toHaveBeenCalledWith('alex.founder@growth-scale.io');
    expect(component.verificationResult()).toEqual(mockVerificationResult);
    expect(component.isVerifying()).toBe(false);
  });

  it('enriches B2B contact and retrieves corporate profile', () => {
    component.runEnrichment();

    expect(apiMock.enrichAndVerifyContact).toHaveBeenCalledWith('tenant-999', {
      firstName: 'Alex',
      lastName: 'Vance',
      email: 'alex@fintech-ventures.com',
      jobTitle: 'VP Engineering & Infrastructure'
    });
    expect(component.enrichmentResult()).toEqual(mockEnrichmentResult);
    expect(component.isEnriching()).toBe(false);
  });

  it('resolves correct badge classes for verifier statuses', () => {
    expect(component.getBadgeClass('Deliverable')).toContain('text-emerald-400');
    expect(component.getBadgeClass('CatchAll')).toContain('text-amber-400');
    expect(component.getBadgeClass('Disposable')).toContain('text-rose-400');
    expect(component.getBadgeClass('Unknown')).toContain('text-rose-400');
  });
});
