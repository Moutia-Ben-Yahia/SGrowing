import { ComponentFixture, TestBed } from '@angular/core/testing';
import { describe, it, expect, beforeEach } from 'vitest';
import { VideoAvatarCockpitComponent } from './video-avatar-cockpit.component';

describe('VideoAvatarCockpitComponent', () => {
  let component: VideoAvatarCockpitComponent;
  let fixture: ComponentFixture<VideoAvatarCockpitComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VideoAvatarCockpitComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(VideoAvatarCockpitComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create component and render initial personas and stream', () => {
    expect(component).toBeTruthy();
    expect(component.personas().length).toBeGreaterThanOrEqual(3);
    expect(component.selectedPersona().name).toBe('Elena Vance');
    expect(component.isStreaming()).toBe(true);
  });

  it('should switch persona and update state', () => {
    const marcus = component.personas()[1];
    component.selectPersona(marcus);
    expect(component.selectedPersona().id).toBe('persona_marcus_architect');
  });

  it('should synthesize speech and generate visemes', () => {
    component.speechPromptInput = 'Testing speech synthesis for AI avatar.';
    component.synthesizeSpeech();

    expect(component.isSpeaking()).toBe(true);
    expect(component.lastSpokenUtterance()).toBe('Testing speech synthesis for AI avatar.');
    expect(component.activeVisemes().length).toBeGreaterThan(0);
  });

  it('should switch active projection slide', () => {
    expect(component.activeSlideIndex()).toBe(1);
    component.projectSlide(2);
    expect(component.activeSlideIndex()).toBe(2);
  });
});
