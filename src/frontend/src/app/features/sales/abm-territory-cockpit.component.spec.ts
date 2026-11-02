import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AbmTerritoryCockpitComponent } from './abm-territory-cockpit.component';

describe('AbmTerritoryCockpitComponent', () => {
  let component: AbmTerritoryCockpitComponent;
  let fixture: ComponentFixture<AbmTerritoryCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AbmTerritoryCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(AbmTerritoryCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with target accounts and calculated average surge index', () => {
    expect(component).toBeTruthy();
    expect(component.accounts().length).toBeGreaterThanOrEqual(3);
    expect(component.averageSurge()).toBeGreaterThanOrEqual(80);
    expect(component.reps().length).toBeGreaterThanOrEqual(3);
  });

  it('should route unassigned account to rep with lowest load', () => {
    component.autoRouteAccount('acc_3');

    const grab = component.accounts().find(a => a.id === 'acc_3');
    expect(grab?.assignedRep).toBe('Kenji Sato');
    expect(grab?.territory).toBe('APAC & Global Tech');
  });

  it('should ingest intent surge signal and add new surging account', () => {
    const initial = component.accounts().length;
    component.newSurgeDomain = 'snowflake.com';
    component.newSurgeWeight = 15;

    component.ingestSignal();

    expect(component.accounts().length).toBe(initial + 1);
    expect(component.accounts()[0].domain).toBe('snowflake.com');
  });
});
