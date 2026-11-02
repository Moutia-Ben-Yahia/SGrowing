import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BiReportBuilderComponent } from './bi-report-builder.component';

describe('BiReportBuilderComponent', () => {
  let component: BiReportBuilderComponent;
  let fixture: ComponentFixture<BiReportBuilderComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BiReportBuilderComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(BiReportBuilderComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with Monte Carlo scenario distribution and base ARR', () => {
    expect(component).toBeTruthy();
    expect(component.baseArr()).toBe(10_000_000);
    expect(component.scenarios().length).toBe(5);
    expect(component.savedReports().length).toBe(2);
  });

  it('should toggle between Monte Carlo and Pivot Builder tabs', () => {
    expect(component.activeTab()).toBe('monte-carlo');

    component.activeTab.set('pivot-builder');
    expect(component.activeTab()).toBe('pivot-builder');
    expect(component.pivotRows().length).toBe(3);
  });

  it('should recalculate Monte Carlo distribution when hyperparameters change', () => {
    component.growthMean = 40;
    component.targetArrInput = 14_000_000;
    component.runMonteCarloSimulation();

    expect(component.simP50Arr()).toBe(14_000_000);
    expect(component.simP90Arr()).toBe(Math.round(14_000_000 * 1.2));
    expect(component.probTarget()).toBe(78.5);
  });
});
