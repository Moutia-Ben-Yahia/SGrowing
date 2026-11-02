import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HeadlessCmsCockpitComponent } from './headless-cms-cockpit.component';

describe('HeadlessCmsCockpitComponent', () => {
  let component: HeadlessCmsCockpitComponent;
  let fixture: ComponentFixture<HeadlessCmsCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [HeadlessCmsCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(HeadlessCmsCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with published articles and average voice score', () => {
    expect(component).toBeTruthy();
    expect(component.articles().length).toBeGreaterThanOrEqual(2);
    expect(component.averageVoiceScore()).toBeGreaterThanOrEqual(90);
  });

  it('should evaluate brand voice copy', () => {
    component.sampleTextToAudit = 'Comprehensive enterprise revenue intelligence and predictive attribution.';
    component.auditText();

    expect(component.auditResult()?.score).toBeGreaterThanOrEqual(90);
    expect(component.auditResult()?.tone).toBe('Enterprise Precision');
  });

  it('should create new modular content article and update article count', () => {
    const initial = component.articles().length;
    component.newTitle = 'Zero-Trust Security Whitepaper';
    component.newSlug = 'zero-trust-whitepaper';
    component.newDescription = 'Cryptographic audit trails and Merkle verification.';

    component.createArticle();

    expect(component.articles().length).toBe(initial + 1);
    expect(component.articles()[0].slug).toBe('zero-trust-whitepaper');
  });
});
