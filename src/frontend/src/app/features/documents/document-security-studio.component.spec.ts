// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { DocumentSecurityStudioComponent } from './document-security-studio.component';

describe('DocumentSecurityStudioComponent', () => {
  let component: DocumentSecurityStudioComponent;

  beforeEach(() => {
    const injector = createEnvironmentInjector([]);
    injector.runInContext(() => {
      component = new DocumentSecurityStudioComponent();
    });
  });

  it('initializes with sample raw text and audits list', () => {
    expect(component.sampleRawText).toContain('CONTRACT AGREEMENT');
    expect(component.sampleRawText).toContain('Tax Registration');
    expect(component.audits().length).toBe(3);
  });

  it('computes redacted preview replacing PII with forensic redactions', () => {
    const preview = component.redactedPreview();
    expect(preview).toContain('[REDACTED:NATIONALID]');
    expect(preview).toContain('[REDACTED:TAXID]');
    expect(preview).toContain('[REDACTED:BANKACCOUNTRID]');
    expect(preview).toContain('[REDACTED:CREDITCARD]');
    expect(preview).toContain('[REDACTED:EMAILADDRESS]');
  });

  it('contains expected audit log actions', () => {
    const auditEntries = component.audits();
    expect(auditEntries[0].viewer).toBe('auditor@kpmg.com');
    expect(auditEntries[0].action).toBe('ViewedAndBurned');
    expect(auditEntries[1].viewer).toBe('investor@sequoia.com');
  });

  it('executes createShareLink without errors', () => {
    expect(() => component.createShareLink()).not.toThrow();
  });

  it('should support dynamic audits updates and identify burned views', () => {
    const burnedCountBefore = component.audits().filter(a => a.action === 'ViewedAndBurned').length;
    expect(burnedCountBefore).toBe(2);

    component.audits.update(list => [
      { id: '4', viewer: 'compliance@sgrowing.io', ip: '10.0.0.1', action: 'ViewedAndBurned', dwell: '15s', time: 'Just now' },
      ...list
    ]);

    expect(component.audits().length).toBe(4);
    expect(component.audits()[0].viewer).toBe('compliance@sgrowing.io');
    expect(component.audits().filter(a => a.action === 'ViewedAndBurned').length).toBe(3);
  });
});
