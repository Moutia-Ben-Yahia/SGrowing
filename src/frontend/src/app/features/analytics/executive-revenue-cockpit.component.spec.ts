import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ExecutiveRevenueCockpitComponent } from './executive-revenue-cockpit.component';

describe('ExecutiveRevenueCockpitComponent', () => {
  let component: ExecutiveRevenueCockpitComponent;
  let fixture: ComponentFixture<ExecutiveRevenueCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ExecutiveRevenueCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ExecutiveRevenueCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize revenue KPIs', () => {
    expect(component).toBeTruthy();
    expect(component.arr()).toBeGreaterThan(1000000);
    expect(component.mrr()).toBeGreaterThan(100000);
    expect(component.nrr()).toBeGreaterThan(100);
  });

  it('should have valid waterfall metrics', () => {
    const wf = component.waterfall();
    expect(wf.netEndingMrr).toBe(wf.startingMrr + wf.newMrr + wf.expansionMrr - wf.contractionMrr - wf.churnedMrr);
    expect(wf.quickRatio).toBeGreaterThan(1.5);
  });

  it('should render cohort retention rows', () => {
    expect(component.cohortRows.length).toBe(6);
    expect(component.cohortRows[0].retention[0]).toBe(100);
  });
});
