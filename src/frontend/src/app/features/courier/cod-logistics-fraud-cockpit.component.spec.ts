// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { of } from 'rxjs';
import { CodLogisticsFraudCockpitComponent } from './cod-logistics-fraud-cockpit.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';

describe('CodLogisticsFraudCockpitComponent (COD Logistics & Anti-Fraud Radar)', () => {
  let component: CodLogisticsFraudCockpitComponent;

  const mockApiService = {
    generateCarrierWaybill: () => of({
      courierCode: 'yalidine',
      courierName: 'Yalidine Express',
      trackingNumber: 'WB-YAL-2026-9901',
      awbBarcode: 'AWB9901882',
      labelUrl: 'https://yalidine.com/track/WB-YAL-2026-9901',
      manifestId: 'MAN-2026-001',
      declaredCashToCollect: 165.0,
      status: 1,
      dispatchedAtUtc: new Date().toISOString()
    }),
    evaluateCodFraud: () => of({
      riskScore: 78,
      tier: 2,
      recommendedAction: 1,
      assessmentSummary: 'High probability of refusal due to address entropy and repeat returns',
      triggeredRules: [
        { ruleId: 'R1', description: 'Address entropy indicates gibberish', pointsAdded: 35, severity: 'High' },
        { ruleId: 'R2', description: 'Customer phone has >2 prior refusals', pointsAdded: 43, severity: 'High' }
      ],
      evaluatedAtUtc: new Date().toISOString()
    })
  };

  const mockAuthService = {
    currentUser: () => ({ tenantId: '11111111-2222-3333-4444-555555555555', email: 'admin@sgrowing.com' })
  };

  beforeEach(() => {
    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApiService },
        { provide: AuthService, useValue: mockAuthService }
      ]
    });

    component = runInInjectionContext(injector, () => new CodLogisticsFraudCockpitComponent());
  });

  it('should initialize with shipping tab active and default form values', () => {
    expect(component.activeTab()).toBe('shipping');
    expect(component.courierCode).toBe('yalidine');
    expect(component.orderNum).toBe('ORD-2026-8812');
    expect(component.cashToCollect).toBe(165.0);
    expect(component.isGeneratingWaybill()).toBe(false);
    expect(component.waybillResult()).toBeNull();
  });

  it('should switch tabs between shipping gateway and antifraud radar', () => {
    component.activeTab.set('antifraud');
    expect(component.activeTab()).toBe('antifraud');

    component.activeTab.set('shipping');
    expect(component.activeTab()).toBe('shipping');
  });

  it('should dispatch waybill generation and update waybillResult signal', () => {
    component.generateWaybill();

    expect(component.isGeneratingWaybill()).toBe(false);
    const result = component.waybillResult();
    expect(result).not.toBeNull();
    expect(result?.trackingNumber).toBe('WB-YAL-2026-9901');
    expect(result?.courierCode).toBe('yalidine');
    expect(result?.declaredCashToCollect).toBe(165.0);
  });

  it('should evaluate COD fraud and populate fraudResult signal with triggered rules', () => {
    component.evaluateFraud();

    expect(component.isEvaluatingFraud()).toBe(false);
    const result = component.fraudResult();
    expect(result).not.toBeNull();
    expect(result?.riskScore).toBe(78);
    expect(result?.tier).toBe(2);
    expect(result?.triggeredRules?.length).toBe(2);
  });

  it('should resolve risk colors and tier badges correctly based on score and tier', () => {
    expect(component.getRiskColor(85)).toBe('text-rose-500');
    expect(component.getRiskColor(50)).toBe('text-orange-400');
    expect(component.getRiskColor(30)).toBe('text-amber-400');
    expect(component.getRiskColor(15)).toBe('text-emerald-400');

    expect(component.getTierName(0)).toBe('Safe');
    expect(component.getTierName(2)).toBe('High Risk');
    expect(component.getTierName(3)).toBe('Fraudulent / Fake');

    expect(component.getActionName(1)).toBe('Require SMS OTP / WhatsApp Confirmation');
    expect(component.getActionName(3)).toBe('Auto-Reject Order to Prevent Return Shipping Loss');
  });
});
