import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AppGeneratorCockpitComponent } from './app-generator-cockpit.component';

describe('AppGeneratorCockpitComponent', () => {
  let component: AppGeneratorCockpitComponent;
  let fixture: ComponentFixture<AppGeneratorCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppGeneratorCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AppGeneratorCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with 1 generated app and default records', () => {
    expect(component).toBeTruthy();
    expect(component.apps().length).toBe(1);
    expect(component.records().length).toBe(2);
    expect(component.totalPipelineVal()).toBe(120000 + 85000);
  });

  it('should generate a new micro-app and append to directory', () => {
    const initialCount = component.apps().length;
    component.newAppName = 'Security Compliance Checklist';
    component.newAppDesc = 'Tracks SOC2 Type II controls';
    component.newAppPermissions = 'InternalEmployees';
    component.generateApp();

    expect(component.apps().length).toBe(initialCount + 1);
    expect(component.apps()[initialCount].name).toBe('Security Compliance Checklist');
    expect(component.apps()[initialCount].slug).toBe('security-compliance-checklist');
  });

  it('should insert quick record and update pipeline value', () => {
    const initialCount = component.records().length;
    const initialPipeline = component.totalPipelineVal();
    component.insertQuickRecord();

    expect(component.records().length).toBe(initialCount + 1);
    expect(component.records()[0].company).toBe('Snowflake Inc');
    expect(component.totalPipelineVal()).toBe(initialPipeline + 240000);
  });
});
