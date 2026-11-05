// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect } from 'vitest';
import { CallComplianceCockpitComponent } from './call-compliance-cockpit.component';

describe('CallComplianceCockpitComponent', () => {
  it('should initialize with audit corpus and calculate metrics', () => {
    const comp = new CallComplianceCockpitComponent();
    expect(comp.audits().length).toBe(3);
    expect(comp.flaggedCount()).toBe(1);
    expect(comp.averageAdherence()).toBeGreaterThan(80);
  });

  it('should select an audit record', () => {
    const comp = new CallComplianceCockpitComponent();
    const first = comp.audits()[0];
    comp.selectAudit(first);
    expect(comp.selectedAudit()?.agentName).toContain('Atlas SDR');
  });

  it('should approve compliance override, clearing flags and updating status', () => {
    const comp = new CallComplianceCockpitComponent();
    comp.approveCompliance('audit_rec_103');

    expect(comp.flaggedCount()).toBe(0);
    const updated = comp.audits().find(a => a.auditId === 'audit_rec_103');
    expect(updated?.status).toBe('CompliantPassed');
    expect(updated?.flaggedSnippets.length).toBe(0);
  });

  it('should return 0 averageAdherence and 0 flaggedCount when audits list is empty', () => {
    const comp = new CallComplianceCockpitComponent();
    comp.audits.set([]);

    expect(comp.averageAdherence()).toBe(0);
    expect(comp.flaggedCount()).toBe(0);
  });

  it('should update selectedAudit status and clear flags when approved while selected', () => {
    const comp = new CallComplianceCockpitComponent();
    expect(comp.selectedAudit()?.auditId).toBe('audit_rec_103');
    expect(comp.selectedAudit()?.status).toBe('FlaggedForReview');

    comp.approveCompliance('audit_rec_103');
    expect(comp.selectedAudit()?.status).toBe('CompliantPassed');
    expect(comp.selectedAudit()?.flaggedSnippets.length).toBe(0);
  });
});
