// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { OmnichannelCockpitComponent } from './omnichannel-cockpit.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import {
  CannedMacroDto,
  CannedMacroResolutionDto,
  ParsedMimeEmail,
  SlaEscalationEvaluationResult
} from '../../core/models/pillars.models';

describe('OmnichannelCockpitComponent (Omnichannel Power Tools: MIME, Macros & SLA)', () => {
  let component: OmnichannelCockpitComponent;
  let apiServiceMock: {
    getCannedMacros: ReturnType<typeof vi.fn>;
    resolveCannedMacro: ReturnType<typeof vi.fn>;
    parseMimeEmail: ReturnType<typeof vi.fn>;
    evaluateSlaEscalations: ReturnType<typeof vi.fn>;
  };
  let authServiceMock: {
    currentUser: ReturnType<typeof vi.fn>;
  };

  const sampleMacro: CannedMacroDto = {
    id: 'macro-1',
    tenantId: '00000000-0000-0000-0000-000000000001',
    triggerKey: '/refund',
    title: 'Refund Approved Notice',
    category: 'Billing',
    templateBody: 'Hello {{contact.name}}, your refund of {{order.amount}} has been approved.',
    requiredVariables: ['contact.name', 'order.amount'],
    isSystem: true
  };

  const resolvedMock: CannedMacroResolutionDto = {
    rawTemplate: sampleMacro.templateBody,
    resolvedText: 'Hello Karim Ben Ali, your refund of 145.50 TND has been approved.',
    missingVariables: []
  };

  const parsedMimeMock: ParsedMimeEmail = {
    from: 'client@outlook.com',
    to: ['support@sgrowing.io'],
    subject: 'Replacement inquiry',
    dateUtc: '2026-10-27T12:00:00Z',
    cleanBody: 'Hello, I received a wrong size. Can I exchange it?',
    quotedHistory: '> Thank you for choosing SGrowing.',
    signature: 'Best,\nClient User',
    attachmentsCount: 0
  };

  const slaReportMock: SlaEscalationEvaluationResult = {
    evaluatedAtUtc: new Date().toISOString(),
    totalEvaluated: 12,
    escalatedCount: 2,
    escalations: [
      {
        conversationId: 'conv-8910',
        elapsedMinutes: 45,
        targetMinutes: 15,
        escalationTier: 'Tier 2 (Supervisor)',
        recommendedAction: 'Reassign to active shift supervisor immediately'
      }
    ]
  };

  beforeEach(() => {
    apiServiceMock = {
      getCannedMacros: vi.fn().mockReturnValue(of([sampleMacro])),
      resolveCannedMacro: vi.fn().mockReturnValue(of(resolvedMock)),
      parseMimeEmail: vi.fn().mockReturnValue(of(parsedMimeMock)),
      evaluateSlaEscalations: vi.fn().mockReturnValue(of(slaReportMock))
    };

    authServiceMock = {
      currentUser: vi.fn().mockReturnValue({
        id: 'user-1',
        email: 'agent@sgrowing.io',
        tenantId: '00000000-0000-0000-0000-000000000001',
        role: 'Admin'
      })
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: apiServiceMock },
        { provide: AuthService, useValue: authServiceMock }
      ]
    });

    component = runInInjectionContext(injector, () => new OmnichannelCockpitComponent());
    component.ngOnInit();
  });

  it('should initialize and load macros with first macro auto-selected', () => {
    expect(component).toBeTruthy();
    expect(apiServiceMock.getCannedMacros).toHaveBeenCalledWith('00000000-0000-0000-0000-000000000001');
    expect(component.macros().length).toBe(1);
    expect(component.selectedTrigger).toBe('/refund');
    expect(component.resolvedMacro()?.resolvedText).toContain('Karim Ben Ali');
  });

  it('should switch navigation tabs between macros, mime, and sla', () => {
    component.activeTab.set('mime');
    expect(component.activeTab()).toBe('mime');

    component.activeTab.set('sla');
    expect(component.activeTab()).toBe('sla');

    component.activeTab.set('macros');
    expect(component.activeTab()).toBe('macros');
  });

  it('should parse RFC 822 MIME raw text and isolate clean body from quoted history', () => {
    component.parseMime();

    expect(apiServiceMock.parseMimeEmail).toHaveBeenCalledWith(component.rawMimeInput);
    expect(component.isParsingMime()).toBe(false);
    expect(component.parsedMime()).toEqual(parsedMimeMock);
    expect(component.parsedMime()?.cleanBody).toBe('Hello, I received a wrong size. Can I exchange it?');
    expect(component.parsedMime()?.quotedHistory).toContain('Thank you for choosing SGrowing');
  });

  it('should evaluate SLA escalations and render escalation report', () => {
    component.evaluateSla();

    expect(apiServiceMock.evaluateSlaEscalations).toHaveBeenCalledWith('00000000-0000-0000-0000-000000000001');
    expect(component.isEvaluatingSla()).toBe(false);
    expect(component.slaReport()).toEqual(slaReportMock);
    expect(component.slaReport()?.escalatedCount).toBe(2);
    expect(component.slaReport()?.escalations[0].escalationTier).toBe('Tier 2 (Supervisor)');
  });

  it('should fallback to zero GUID when currentUser is null and selectMacro updates selectedTrigger', () => {
    authServiceMock.currentUser.mockReturnValue(null);
    const customMacro: CannedMacroDto = {
      id: 'macro-custom',
      tenantId: '00000000-0000-0000-0000-000000000000',
      triggerKey: '/shipping_status',
      title: 'Tracking Info',
      category: 'Logistics',
      templateBody: 'Order {{order.number}} is in transit.',
      requiredVariables: ['order.number'],
      isSystem: false
    };

    component.selectMacro(customMacro);
    expect(component.selectedTrigger).toBe('/shipping_status');
    expect(apiServiceMock.resolveCannedMacro).toHaveBeenCalledWith(
      '00000000-0000-0000-0000-000000000000',
      '/shipping_status',
      expect.any(Object)
    );
  });
});
