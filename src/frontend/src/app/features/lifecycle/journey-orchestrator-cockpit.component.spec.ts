import { ComponentFixture, TestBed } from '@angular/core/testing';
import { JourneyOrchestratorCockpitComponent } from './journey-orchestrator-cockpit.component';

describe('JourneyOrchestratorCockpitComponent', () => {
  let component: JourneyOrchestratorCockpitComponent;
  let fixture: ComponentFixture<JourneyOrchestratorCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [JourneyOrchestratorCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(JourneyOrchestratorCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with active lifecycle journey and stages', () => {
    expect(component).toBeTruthy();
    expect(component.activeJourney().stages.length).toBe(4);
    expect(component.activeJourney().activeEnrollments).toBeGreaterThan(1000);
    expect(component.activeJourney().conversionRate).toBeGreaterThan(40);
  });

  it('should evaluate fatigue policy and enforce cooldown', () => {
    component.testContactEmail = 'cto@acme.corp';
    component.testFatigue();

    expect(component.fatigueCheckResult()?.canSend).toBe(false);
    expect(component.fatigueCheckResult()?.reason).toContain('cooldown');
  });

  it('should enroll new contact and update enrollment counts', () => {
    const initial = component.activeJourney().activeEnrollments;
    component.newContactEmail = 'founder@stealth.ai';
    component.enrollContact();

    expect(component.activeJourney().activeEnrollments).toBe(initial + 1);
    expect(component.enrollments()[0].email).toBe('founder@stealth.ai');
  });
});
