// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { TranslationService } from '../../core/services/translation.service';
import { EnterpriseFleetLicensingStudioComponent } from './enterprise-fleet-licensing-studio.component';

describe('EnterpriseFleetLicensingStudioComponent', () => {
  let component: EnterpriseFleetLicensingStudioComponent;
  let mockHttp: any;
  let mockTranslationService: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue(of({})),
      post: vi.fn().mockReturnValue(of({}))
    };

    const currentLocaleSignal = signal<'en-US' | 'ar-AR'>('en-US');
    mockTranslationService = {
      currentLocale: currentLocaleSignal
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp },
        { provide: TranslationService, useValue: mockTranslationService }
      ]
    });

    component = runInInjectionContext(injector, () => new EnterpriseFleetLicensingStudioComponent());
  });

  it('should initialize with pre-seeded sovereign cluster fleet and active enterprise license', () => {
    expect(component).toBeTruthy();
    expect(component.clusterFleet().overallStatus).toBe('Healthy');
    expect(component.clusterFleet().totalNodes).toBe(2);
    expect(component.clusterFleet().activeNodes).toBe(2);
    expect(component.clusterFleet().totalActiveSeats).toBe(130);
    expect(component.clusterFleet().maxAllowedSeats).toBe(250);
    expect(component.activeLicense()?.organizationName).toBe('Apex Sovereign Enterprise');
    expect(component.daysRemaining()).toBeGreaterThan(300);
  });

  it('should compute seat, CPU, and node utilization percentages correctly', () => {
    // 130 / 250 = 52%
    expect(component.seatUsagePercent()).toBe(52);
    // 40 / 64 = 62.5% -> 63%
    expect(component.cpuUsagePercent()).toBe(63);
    // 2 / 8 = 25%
    expect(component.nodeUsagePercent()).toBe(25);
  });

  it('should respond to RTL locale changes via TranslationService computed signal', () => {
    expect(component.isRtl()).toBe(false);
    mockTranslationService.currentLocale.set('ar-AR');
    expect(component.isRtl()).toBe(true);
    mockTranslationService.currentLocale.set('en-US');
    expect(component.isRtl()).toBe(false);
  });

  it('should switch active studio tabs smoothly', () => {
    expect(component.activeTab()).toBe('fleet');
    component.selectTab('issue');
    expect(component.activeTab()).toBe('issue');
    component.selectTab('verify');
    expect(component.activeTab()).toBe('verify');
    component.selectTab('airgap');
    expect(component.activeTab()).toBe('airgap');
  });

  it('should toggle module entitlements selection for license issuance', () => {
    const initialCount = component.selectedModules().length;
    const testModule = 'Security.AirGappedKeyValidator';

    // Add module
    component.toggleModule(testModule);
    expect(component.selectedModules()).toContain(testModule);
    expect(component.selectedModules().length).toBe(initialCount + 1);

    // Remove module
    component.toggleModule(testModule);
    expect(component.selectedModules()).not.toContain(testModule);
    expect(component.selectedModules().length).toBe(initialCount);
  });

  it('should issue new cryptographic license token and populate artifacts', () => {
    component.issueOrgName = 'Saudi Defense Systems';
    component.issueTier = 'AirGappedGovernment';
    component.issueSeats = 600;
    component.issueCores = 128;
    component.issueNodes = 12;
    component.issueValidityDays = 180;
    component.issueFingerprint = 'HW-SAUDI-NODE-01';

    component.generateLicense();

    const last = component.lastIssuedLicense();
    expect(last).not.toBeNull();
    expect(last?.licenseKey).toContain('SG-ENT-');
    expect(last?.signedToken).toContain('.');
    expect(last?.airGappedActivationCode).toContain('SGA1-');
    expect(last?.payload.organizationName).toBe('Saudi Defense Systems');
    expect(last?.payload.maxSeats).toBe(600);
    expect(component.tokenInput).toBe(last?.signedToken);
    expect(component.airGapCodeInput).toBe(last?.airGappedActivationCode);
  });

  it('should verify valid signed token offline with success outcome', () => {
    component.issueOrgName = 'Valid Scale Org';
    component.issueSeats = 150;
    component.issueFingerprint = '*';
    component.generateLicense();

    component.tokenInput = component.lastIssuedLicense()!.signedToken;
    component.fingerprintInput = 'ANY-FINGERPRINT';
    component.verifyToken();

    const result = component.verificationResult();
    expect(result).not.toBeNull();
    expect(result?.isValid).toBe(true);
    expect(result?.status).toBe('Valid');
    expect(result?.payload?.organizationName).toBe('Valid Scale Org');
  });

  it('should detect tampered token and report TamperedPayload status', () => {
    component.issueOrgName = 'Tamper Detection Target';
    component.issueSeats = 100;
    component.issueFingerprint = '*';
    component.generateLicense();

    const validToken = component.lastIssuedLicense()!.signedToken;
    const parts = validToken.split('.');

    // Tamper with signature
    const tamperedSig = parts[1] + '_FORGED';
    component.tokenInput = `${parts[0]}.${tamperedSig}`;
    component.verifyToken();

    const result = component.verificationResult();
    expect(result?.isValid).toBe(false);
    expect(result?.status).toBe('TamperedPayload');
    expect(result?.message).toContain('signature mismatch');
  });

  it('should detect malformed token format', () => {
    component.tokenInput = 'NotAValidToken';
    component.verifyToken();
    expect(component.verificationResult()?.status).toBe('MalformedToken');

    component.tokenInput = '';
    component.verifyToken();
    expect(component.verificationResult()?.status).toBe('MalformedToken');
  });

  it('should verify hardware fingerprint and reject mismatched machine fingerprints', () => {
    component.issueOrgName = 'Hardware Bound Agency';
    component.issueFingerprint = 'HW-VALID-NODE-01,HW-VALID-NODE-02';
    component.generateLicense();

    component.tokenInput = component.lastIssuedLicense()!.signedToken;

    // Matching fingerprint
    component.fingerprintInput = 'HW-VALID-NODE-01';
    component.verifyToken();
    expect(component.verificationResult()?.isValid).toBe(true);
    expect(component.verificationResult()?.status).toBe('Valid');

    // Mismatched rogue fingerprint
    component.fingerprintInput = 'ROGUE-NODE-999';
    component.verifyToken();
    expect(component.verificationResult()?.isValid).toBe(false);
    expect(component.verificationResult()?.status).toBe('HardwareMismatch');
  });

  it('should activate air-gapped code when checksum and machine fingerprint match', () => {
    component.issueOrgName = 'AirGapped Isolated Mesh';
    component.issueFingerprint = 'HW-OFFLINE-ALPHA';
    component.generateLicense();

    component.airGapCodeInput = component.lastIssuedLicense()!.airGappedActivationCode;
    component.airGapFingerprintInput = 'HW-OFFLINE-ALPHA';
    component.activateAirGap();

    const result = component.airGapResult();
    expect(result?.success).toBe(true);
    expect(result?.message).toContain('Air-gapped enterprise license successfully activated');
    expect(component.activeLicense()?.organizationName).toBe('AirGapped Isolated Mesh');
  });

  it('should reject air-gapped activation when checksum is corrupted', () => {
    component.issueOrgName = 'Checksum Integrity Test';
    component.issueFingerprint = '*';
    component.generateLicense();

    const code = component.lastIssuedLicense()!.airGappedActivationCode;
    const lastHyphen = code.lastIndexOf('-');
    // Mutate checksum
    const corruptedCode = code.substring(0, lastHyphen + 1) + '00000000';

    component.airGapCodeInput = corruptedCode;
    component.airGapFingerprintInput = 'ANY';
    component.activateAirGap();

    const result = component.airGapResult();
    expect(result?.success).toBe(false);
    expect(result?.message).toContain('checksum verification failed');
  });

  it('should ingest new node heartbeat, updating roster and aggregated telemetry', () => {
    const initialCount = component.clusterFleet().nodes.length;
    const initialSeats = component.clusterFleet().totalActiveSeats;

    component.hbNodeId = 'node-new-09';
    component.hbNodeName = 'New Edge Worker';
    component.hbFingerprint = 'FP-EDGE-99';
    component.hbAllocatedCores = 16;
    component.hbActiveSeats = 30;
    component.hbCpuUsage = 52.0;

    component.ingestHeartbeat();

    expect(component.clusterFleet().nodes.length).toBe(initialCount + 1);
    expect(component.clusterFleet().totalActiveSeats).toBe(initialSeats + 30);
    expect(component.clusterFleet().totalNodes).toBe(initialCount + 1);
    expect(component.toastMessage()?.type).toBe('info');
  });

  it('should detect quota overage and transition overall cluster status to QuotaExceeded', () => {
    expect(component.clusterFleet().overallStatus).toBe('Healthy');
    expect(component.clusterFleet().seatQuotaExceeded).toBe(false);

    component.simulateQuotaOverage();

    expect(component.clusterFleet().seatQuotaExceeded).toBe(true);
    expect(component.clusterFleet().overallStatus).toBe('QuotaExceeded');
    expect(component.clusterFleet().activeAlerts.length).toBeGreaterThan(0);
    expect(component.clusterFleet().activeAlerts.some(a => a.includes('Seat quota exceeded'))).toBe(true);
  });

  it('should reset demo cluster fleet state to initial baseline', () => {
    component.simulateQuotaOverage();
    expect(component.clusterFleet().seatQuotaExceeded).toBe(true);

    component.resetDemoCluster();

    expect(component.clusterFleet().seatQuotaExceeded).toBe(false);
    expect(component.clusterFleet().overallStatus).toBe('Healthy');
    expect(component.clusterFleet().nodes.length).toBe(2);
    expect(component.clusterFleet().totalActiveSeats).toBe(130);
  });
});
