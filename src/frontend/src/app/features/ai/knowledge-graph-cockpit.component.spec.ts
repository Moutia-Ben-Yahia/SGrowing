import { ComponentFixture, TestBed } from '@angular/core/testing';
import { KnowledgeGraphCockpitComponent } from './knowledge-graph-cockpit.component';

describe('KnowledgeGraphCockpitComponent', () => {
  let component: KnowledgeGraphCockpitComponent;
  let fixture: ComponentFixture<KnowledgeGraphCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [KnowledgeGraphCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(KnowledgeGraphCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with default knowledge entities and computed average confidence', () => {
    expect(component).toBeTruthy();
    expect(component.entities().length).toBe(4);
    expect(component.triples().length).toBe(3);
    expect(component.avgConfidence()).toBeGreaterThan(90);
  });

  it('should perform hybrid query and display grounded context summary', () => {
    component.searchQuery = 'Voice SDR';
    component.executeSearch();

    expect(component.groundedSummary()).toContain('Grounded by');
    expect(component.groundedSummary()).toContain('Autonomous Voice SDR');
  });

  it('should ingest new knowledge entity and append to graph', () => {
    const initialEntCount = component.entities().length;
    component.newEntityName = 'Neural DSP Module';
    component.newEntityDesc = 'Sub-5ms acoustic echo cancellation.';
    component.ingestKnowledge();

    expect(component.entities().length).toBe(initialEntCount + 1);
    expect(component.entities()[initialEntCount].name).toBe('Neural DSP Module');
  });
});
