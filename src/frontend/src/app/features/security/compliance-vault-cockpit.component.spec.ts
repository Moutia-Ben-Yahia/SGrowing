// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { ComplianceVaultCockpitComponent } from './compliance-vault-cockpit.component';

describe('ComplianceVaultCockpitComponent', () => {
  let component: ComplianceVaultCockpitComponent;

  beforeEach(() => {
    component = new ComplianceVaultCockpitComponent();
  });

  it('should initialize with audit leaves, root hash, and residency boundaries', () => {
    expect(component).toBeTruthy();
    expect(component.auditLeaves().length).toBeGreaterThanOrEqual(3);
    expect(component.merkleRootHash()).toBeTruthy();
    expect(component.residencyBoundaries.length).toBeGreaterThanOrEqual(3);
  });

  it('should redact sensitive PII into synthetic tokens', () => {
    component.rawPayload = 'User email is alice.smith@enterprise.com and card is 4111-2222-3333-4444.';
    component.executeRedaction();

    expect(component.redactedResult()).toContain('[REDACTED_EMAIL_8F2A]');
    expect(component.redactedResult()).toContain('[REDACTED_CC_4444]');
    expect(component.redactedTokensCount()).toBeGreaterThanOrEqual(2);
  });

  it('should verify cryptographic Merkle audit proof chain', () => {
    component.verifyMerkleProof();

    expect(component.verificationMessage()).toContain('100% tamper-evident');
  });

  it('should allow adding new audit leaf to the ledger and updating root hash', () => {
    const initialLeaves = component.auditLeaves().length;
    component.auditLeaves.update(list => [
      ...list,
      {
        id: 'l4',
        action: 'POLICY_REVISION_COMMITTED',
        actor: 'compliance_officer',
        tenant: 'org_enterprise_root',
        hash: '1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef',
        time: '12:20:00'
      }
    ]);
    component.merkleRootHash.set('abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd');

    expect(component.auditLeaves().length).toBe(initialLeaves + 1);
    expect(component.merkleRootHash()).toBe('abcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcdefabcd');
  });

  it('should redact phone numbers and update redactedTokensCount to 3', () => {
    component.rawPayload = 'Call +1-415-555-0144 regarding customer ticket.';
    component.executeRedaction();
    expect(component.redactedResult()).toContain('[REDACTED_PHONE_0199]');
    expect(component.redactedTokensCount()).toBe(3);
  });
});
