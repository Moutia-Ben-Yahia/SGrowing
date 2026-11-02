import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FraudFirewallCockpitComponent } from './fraud-firewall-cockpit.component';

describe('FraudFirewallCockpitComponent', () => {
  let component: FraudFirewallCockpitComponent;
  let fixture: ComponentFixture<FraudFirewallCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FraudFirewallCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(FraudFirewallCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with rules and default evaluations', () => {
    expect(component).toBeTruthy();
    expect(component.rules().length).toBe(4);
    expect(component.evaluations().length).toBe(3);
    expect(component.avgRiskScore()).toBeGreaterThan(40);
  });

  it('should toggle heuristic rule status', () => {
    const initialStatus = component.rules()[0].isActive;
    component.toggleRule('rule_geo_mismatch');

    expect(component.rules()[0].isActive).toBe(!initialStatus);
  });

  it('should simulate high-risk transaction and append Block decision', () => {
    const initialCount = component.evaluations().length;
    component.simulateEvaluation();

    expect(component.evaluations().length).toBe(initialCount + 1);
    expect(component.evaluations()[0].decision).toBe('Block');
    expect(component.evaluations()[0].riskScore).toBe(85);
  });
});
