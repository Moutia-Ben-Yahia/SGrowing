import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SemanticRagCockpitComponent } from './semantic-rag-cockpit.component';

describe('SemanticRagCockpitComponent', () => {
  let component: SemanticRagCockpitComponent;
  let fixture: ComponentFixture<SemanticRagCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SemanticRagCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(SemanticRagCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with vector metrics and default grounded answer', () => {
    expect(component).toBeTruthy();
    expect(component.totalChunks()).toBeGreaterThan(1000);
    expect(component.chunks().length).toBeGreaterThanOrEqual(2);
    expect(component.groundedAnswer()).toContain('SGrowing Enterprise');
  });

  it('should execute search and update grounded answer', () => {
    component.searchQuery = 'How does offline licensing work?';
    component.executeSearch();

    expect(component.groundedAnswer()).toContain('How does offline licensing work?');
  });

  it('should ingest new chunk and increment count', () => {
    const initial = component.totalChunks();
    component.newDocTitle = 'API Webhook Security';
    component.newDocContent = 'HMAC SHA-256 signatures are evaluated on all incoming payloads.';
    component.ingestChunk();

    expect(component.totalChunks()).toBe(initial + 1);
    expect(component.chunks()[0].title).toBe('API Webhook Security');
    expect(component.showIngestModal()).toBe(false);
  });
});
