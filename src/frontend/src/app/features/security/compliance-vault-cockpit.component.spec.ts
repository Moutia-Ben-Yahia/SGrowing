import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ComplianceVaultCockpitComponent } from './compliance-vault-cockpit.component';

describe('ComplianceVaultCockpitComponent', () => {
  let component: ComplianceVaultCockpitComponent;
  let fixture: ComponentFixture<ComplianceVaultCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ComplianceVaultCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ComplianceVaultCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
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
});
