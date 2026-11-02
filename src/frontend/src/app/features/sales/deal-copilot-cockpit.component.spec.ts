import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DealCopilotCockpitComponent } from './deal-copilot-cockpit.component';

describe('DealCopilotCockpitComponent', () => {
  let component: DealCopilotCockpitComponent;
  let fixture: ComponentFixture<DealCopilotCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [DealCopilotCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(DealCopilotCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with pipeline deals and aggregate health metrics', () => {
    expect(component).toBeTruthy();
    expect(component.deals().length).toBeGreaterThanOrEqual(3);
    expect(component.totalPipeline()).toBeGreaterThan(500000);
    expect(component.aggregateHealth()).toBeGreaterThan(50);
  });

  it('should execute intervention, lift win probability and update risk badge', () => {
    const slippingDeal = component.deals().find(d => d.dealId === 'deal_quantum_corp')!;
    const initialProb = slippingDeal.winProbability;

    component.executeIntervention(slippingDeal);

    const updated = component.deals().find(d => d.dealId === 'deal_quantum_corp')!;
    expect(updated.winProbability).toBeGreaterThan(initialProb);
    expect(updated.riskLevel).toBe('Healthy');
    expect(updated.daysInactive).toBe(0);
    expect(component.actionToast()).toContain('Successfully dispatched AI intervention');
  });
});
