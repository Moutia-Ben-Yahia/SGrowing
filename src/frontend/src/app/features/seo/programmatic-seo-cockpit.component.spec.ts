import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProgrammaticSeoCockpitComponent } from './programmatic-seo-cockpit.component';

describe('ProgrammaticSeoCockpitComponent', () => {
  let component: ProgrammaticSeoCockpitComponent;
  let fixture: ComponentFixture<ProgrammaticSeoCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProgrammaticSeoCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ProgrammaticSeoCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with landing pages and backlink opportunities', () => {
    expect(component).toBeTruthy();
    expect(component.pages().length).toBeGreaterThanOrEqual(2);
    expect(component.backlinks().length).toBeGreaterThanOrEqual(3);
  });

  it('should broadcast to IndexNow and show confirmation banner', () => {
    component.broadcastIndexNow();

    expect(component.indexNowMessage()).toContain('IndexNow');
    expect(component.indexNowMessage()).toContain('202 Accepted');
  });

  it('should generate batch pages and update list', () => {
    const initial = component.pages().length;
    component.batchGenerate();

    expect(component.pages().length).toBe(initial + 1);
    expect(component.pages().some(p => p.industry === 'Healthcare')).toBe(true);
  });
});
