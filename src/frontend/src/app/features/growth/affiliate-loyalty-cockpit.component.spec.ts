import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AffiliateLoyaltyCockpitComponent } from './affiliate-loyalty-cockpit.component';

describe('AffiliateLoyaltyCockpitComponent', () => {
  let component: AffiliateLoyaltyCockpitComponent;
  let fixture: ComponentFixture<AffiliateLoyaltyCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AffiliateLoyaltyCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AffiliateLoyaltyCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with active affiliate partners and calculated commission totals', () => {
    expect(component).toBeTruthy();
    expect(component.partners().length).toBeGreaterThanOrEqual(2);
    expect(component.totalCommissionsEarned()).toBeGreaterThan(10000);
    expect(component.pendingPayoutTotal()).toBeGreaterThan(1000);
  });

  it('should simulate order conversion and calculate commission and points', () => {
    component.simulatedOrderAmount = 5000;
    component.simulateConversion();

    expect(component.simulatedResult()).toContain('$1000 direct commission');
    expect(component.simulatedResult()).toContain('5000 loyalty pts');
  });

  it('should register new affiliate partner and update network list', () => {
    const initial = component.partners().length;
    component.newPartnerName = 'B2B Influencers Hub';
    component.newPartnerCode = 'INFLUENCE25';
    component.newTier1Rate = 25.0;
    component.newTier2Rate = 7.5;

    component.registerPartner();

    expect(component.partners().length).toBe(initial + 1);
    expect(component.partners()[0].code).toBe('INFLUENCE25');
  });
});
