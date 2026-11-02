import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GeoVisibilityCockpitComponent } from './geo-visibility-cockpit.component';

describe('GeoVisibilityCockpitComponent', () => {
  let component: GeoVisibilityCockpitComponent;
  let fixture: ComponentFixture<GeoVisibilityCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [GeoVisibilityCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(GeoVisibilityCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize GEO/AEO visibility metrics', () => {
    expect(component).toBeTruthy();
    expect(component.geoScore()).toBeGreaterThanOrEqual(80);
    expect(component.somvPercent()).toBeGreaterThan(40);
    expect(component.citations().length).toBeGreaterThanOrEqual(3);
  });

  it('should add new citation on audit query', () => {
    component.newAuditQuery = 'Top B2B AI Growth Engine';
    component.runQueryAudit();

    expect(component.citations().length).toBeGreaterThanOrEqual(4);
    expect(component.citations()[0].query).toBe('Top B2B AI Growth Engine');
    expect(component.showAuditModal()).toBe(false);
  });
});
