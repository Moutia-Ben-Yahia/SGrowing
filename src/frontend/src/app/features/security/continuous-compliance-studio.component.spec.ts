// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { ContinuousComplianceStudioComponent } from './continuous-compliance-studio.component';

describe('ContinuousComplianceStudioComponent (SOC 2 & ISO 27001 Auditor Sentinel)', () => {
  let component: ContinuousComplianceStudioComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockImplementation((url: string) => {
        if (url.includes('/scorecard')) {
          return of({
            overallReadinessScore: 94.2,
            certificationTier: 'Tier 4 - Continuous Verification',
            totalControls: 16,
            compliantControls: 15,
            warningControls: 1,
            nonCompliantControls: 0,
            pendingControls: 0,
            totalEvidenceCount: 84
          });
        }
        if (url.includes('/controls/')) {
          return of({
            evidence: [
              {
                evidenceId: 'ev-test-1',
                controlId: 'ctrl-1',
                category: 'Security',
                title: 'Test Evidence',
                collectedAtUtc: new Date().toISOString(),
                sourceSubsystem: 'AuditSystem',
                status: 'Compliant',
                proofDigest: 'digest-test',
                rawPayloadJson: '{}',
                metadata: {},
                verifiedBy: 'Auto'
              }
            ]
          });
        }
        if (url.includes('/controls')) {
          return of({
            controls: [
              {
                id: 'cc-1.1',
                code: 'CC1.1',
                category: 'Security',
                title: 'COSO Principle 1: Integrity & Ethical Values',
                description: 'Demonstrates commitment to integrity',
                targetMetric: '100% Policy Attestation',
                standardMapping: 'SOC2-CC1.1',
                currentStatus: 'Compliant',
                lastEvaluatedAtUtc: new Date().toISOString()
              },
              {
                id: 'cc-6.1',
                code: 'CC6.1',
                category: 'Security',
                title: 'Logical Access Controls',
                description: 'Enforces MFA and RBAC',
                targetMetric: 'MFA active for 100% users',
                standardMapping: 'SOC2-CC6.1',
                currentStatus: 'Warning',
                lastEvaluatedAtUtc: new Date().toISOString()
              }
            ]
          });
        }
        if (url.includes('/vault')) {
          return of({ packages: [] });
        }
        return of({});
      }),
      post: vi.fn().mockImplementation((url: string, body: any) => {
        if (url.includes('/collect-evidence')) {
          return of({
            collectedEvidenceCount: 5,
            newScore: 96.5
          });
        }
        if (url.includes('/seal-vault')) {
          return of({
            packageId: 'pkg-vault-12345678',
            vaultLabel: 'SOC 2 Q4 Audit Vault',
            auditorOrganization: body?.auditorOrganization || 'AICPA',
            integrityDigest: 'digest-vault-abc',
            sealedAtUtc: new Date().toISOString(),
            validUntilUtc: new Date().toISOString(),
            controlsIncluded: 16,
            evidenceArtifactsIncluded: 84
          });
        }
        if (url.includes('/vault/verify/')) {
          return of({
            isValid: true,
            packageId: 'pkg-vault-12345678',
            calculatedDigest: 'digest-vault-abc',
            expectedDigest: 'digest-vault-abc',
            digestMatch: true,
            signatureValid: true,
            verificationMessage: 'Cryptographic match',
            verifiedAtUtc: new Date().toISOString()
          });
        }
        return of({});
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new ContinuousComplianceStudioComponent());
    component.ngOnInit();
  });

  it('should initialize with scorecard and security controls', () => {
    expect(component).toBeTruthy();
    expect(component.scorecard()).not.toBeNull();
    expect(component.scorecard()?.overallReadinessScore).toBe(94.2);
    expect(component.controls().length).toBe(2);
  });

  it('should filter controls by status and search query', () => {
    component.selectedStatusFilter.set('Warning');
    expect(component.filteredControls().length).toBe(1);
    expect(component.filteredControls()[0].code).toBe('CC6.1');

    component.selectedStatusFilter.set('ALL');
    component.searchQuery = 'ethical';
    expect(component.filteredControls().length).toBe(1);
    expect(component.filteredControls()[0].code).toBe('CC1.1');
  });

  it('should trigger automated compliance harvest sweep', () => {
    component.runComplianceSweep();

    expect(mockHttp.post).toHaveBeenCalled();
    expect(component.isSweeping()).toBe(false);
    expect(component.statusMessage()).toContain('Compliance sweep completed');
  });

  it('should view control evidence for a selected security control', () => {
    const ctrl = component.controls()[0];
    component.viewControlEvidence(ctrl);

    expect(component.inspectControl()).toBe(ctrl);
    expect(component.controlEvidence().length).toBe(1);
    expect(component.controlEvidence()[0].evidenceId).toBe('ev-test-1');
  });

  it('should open modal, submit seal vault, and verify active package', () => {
    component.openSealModal();
    expect(component.showSealModal()).toBe(true);

    component.submitSealVault();
    expect(component.sealedPackageResult()).not.toBeNull();
    expect(component.sealedPackageResult()?.packageId).toContain('pkg-vault');

    component.verifyActivePackage();
    expect(component.verificationResult()).not.toBeNull();
    expect(component.verificationResult()?.isValid).toBe(true);
  });
});
