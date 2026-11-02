import { ComponentFixture, TestBed } from '@angular/core/testing';
import { LiveCallCopilotCockpitComponent } from './live-call-copilot-cockpit.component';

describe('LiveCallCopilotCockpitComponent', () => {
  let component: LiveCallCopilotCockpitComponent;
  let fixture: ComponentFixture<LiveCallCopilotCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LiveCallCopilotCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(LiveCallCopilotCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should initialize with diarized utterances and calculate average sentiment', () => {
    expect(component).toBeTruthy();
    expect(component.utterances().length).toBeGreaterThanOrEqual(3);
    expect(component.averageSentiment()).toBeDefined();
    expect(component.activeBattlecards().length).toBeGreaterThanOrEqual(1);
  });

  it('should stream simulated utterance and surface competitor battlecard', () => {
    component.simulatedUtterance = 'We are testing HubSpot right now.';
    component.sendUtterance();

    expect(component.utterances().length).toBe(4);
    expect(component.activeBattlecards().some(c => c.keyword === 'hubspot')).toBe(true);
  });

  it('should end call and generate executive summary with action items', () => {
    component.endAndSummarizeCall();

    expect(component.callSummary()).toBeTruthy();
    expect(component.callSummary()?.actionItems.length).toBeGreaterThanOrEqual(2);
    expect(component.callSummary()?.summary).toContain('Nexus Innovations');
  });
});
