// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { PrivacyGovernanceStudioComponent } from './privacy-governance-studio.component';

describe('PrivacyGovernanceStudioComponent', () => {
  let component: PrivacyGovernanceStudioComponent;

  beforeEach(() => {
    component = new PrivacyGovernanceStudioComponent();
  });

  it('should create the component successfully', () => {
    expect(component).toBeTruthy();
    expect(component.translation).toBeDefined();
  });

  it('should initialize with accurate sovereign report and regulation certifications', () => {
    const report = component.sovereignReport();
    expect(report.sovereignRegion).toContain('sa-central-1');
    expect(report.isSovereignEnclaveEnforced).toBe(true);
    expect(report.certifiedRegulations).toContain('Saudi Personal Data Protection Law (PDPL - Royal Decree M/19)');
    expect(report.certifiedRegulations).toContain('NCA Essential Cybersecurity Controls (ECC-1:2018)');
    expect(report.certifiedRegulations).toContain('GCC Cross-Border Data Privacy Framework');
  });

  it('should calculate initial KPIs accurately', () => {
    expect(component.consents().length).toBe(5);
    expect(component.activeConsentsCount()).toBe(3);
    expect(component.pendingRequestsCount()).toBe(2);
    expect(component.completedErasuresCount()).toBe(1);
  });

  it('should filter privacy requests by status correctly', () => {
    component.statusFilter.set('ALL');
    expect(component.filteredRequests().length).toBe(3);

    component.statusFilter.set('PendingDpoReview');
    expect(component.filteredRequests().length).toBe(2);
    expect(component.filteredRequests().every(r => r.status === 'PendingDpoReview')).toBe(true);

    component.statusFilter.set('Completed');
    expect(component.filteredRequests().length).toBe(1);
    expect(component.filteredRequests()[0].status).toBe('Completed');
  });

  it('should record granted consent via simulator with SHA-256 digital signature', () => {
    const initialActive = component.activeConsentsCount();
    const initialTotal = component.consents().length;

    component.simContactEmail = 'reem.alsaud@gov.sa';
    component.simContactPhone = '+966500001122';
    component.simConsentType = 'MarketingSms';
    component.simLegalBasis = 'PDPL Art. 5: Explicit Digital Portal Consent';
    component.simSourceIp = '10.50.1.20';

    component.grantConsent();

    expect(component.consents().length).toBe(initialTotal + 1);
    expect(component.activeConsentsCount()).toBe(initialActive + 1);

    const newest = component.consents()[0];
    expect(newest.contactEmail).toBe('reem.alsaud@gov.sa');
    expect(newest.status).toBe('Granted');
    expect(newest.digitalSignatureSha256).toBeTruthy();
    expect(newest.digitalSignatureSha256.length).toBe(64);
    expect(component.actionFeedback()).toContain('Consent Granted for MarketingSms');
  });

  it('should revoke consent and update active consents KPI', () => {
    component.simContactEmail = 'khalid.alghamdi@riyadh-retail.sa';
    component.simConsentType = 'MarketingEmail';
    component.simLegalBasis = 'Withdrawn via Customer Privacy Preference Center';

    const prevActive = component.activeConsentsCount();
    component.revokeConsent();

    expect(component.actionFeedback()).toContain('Consent Revoked for MarketingEmail');
    expect(component.activeConsentsCount()).toBeLessThan(prevActive + 1);

    const emailConsents = component.consents().filter(
      c => c.contactEmail === 'khalid.alghamdi@riyadh-retail.sa' && c.type === 'MarketingEmail'
    );
    expect(emailConsents.some(c => c.status === 'Revoked')).toBe(true);
  });

  it('should execute RTBF erasure, sanitize entities and revoke active subject consents', () => {
    const pendingErasure = component.requests().find(
      r => r.requestType === 'RightToBeForgotten_Erasure' && r.status === 'PendingDpoReview'
    )!;
    expect(pendingErasure).toBeDefined();

    component.executeRtbfErasure(pendingErasure);

    const updated = component.requests().find(r => r.requestId === pendingErasure.requestId)!;
    expect(updated.status).toBe('Completed');
    expect(updated.redactedEntitiesCount).toBe(42);
    expect(updated.resolvedAtUtc).toBeTruthy();
    expect(updated.dpoNotes).toContain('RTBF erasure executed per Saudi PDPL Art. 24');

    // Verify khalid's active consents are now revoked
    const khalidConsents = component.consents().filter(c => c.contactEmail === pendingErasure.requesterEmail);
    expect(khalidConsents.every(c => c.status === 'Revoked')).toBe(true);

    expect(component.completedErasuresCount()).toBe(2);
  });

  it('should generate cryptographic SAR export download link when resolving SAR request', () => {
    const sarReq = component.requests().find(
      r => r.requestType === 'SubjectAccessRequest_Export' && r.status === 'PendingDpoReview'
    )!;
    expect(sarReq).toBeDefined();

    component.generateSarExport(sarReq);

    const updated = component.requests().find(r => r.requestId === sarReq.requestId)!;
    expect(updated.status).toBe('Completed');
    expect(updated.exportDownloadUrl).toBeTruthy();
    expect(updated.exportDownloadUrl).toContain('storage.sa-central-1.cloud.sgrowing.net');
    expect(updated.exportDownloadUrl).toContain('_sar.enc');
    expect(updated.resolvedAtUtc).toBeTruthy();
    expect(component.actionFeedback()).toContain('Subject Access Request export package compiled');
  });

  it('should support generic resolve and reject actions', () => {
    const req = component.requests()[0];
    component.resolveGeneric(req, 'Rejected');

    const updated = component.requests().find(r => r.requestId === req.requestId)!;
    expect(updated.status).toBe('Rejected');
    expect(updated.dpoNotes).toContain('Rejected per regulatory criteria');
  });
});
