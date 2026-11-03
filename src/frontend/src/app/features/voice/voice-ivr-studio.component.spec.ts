// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { VoiceIvrStudioComponent } from './voice-ivr-studio.component';

describe('VoiceIvrStudioComponent', () => {
  let component: VoiceIvrStudioComponent;

  beforeEach(() => {
    component = new VoiceIvrStudioComponent();
  });

  it('should initialize with MainMenu root node and default call log entry', () => {
    expect(component).toBeTruthy();
    expect(component.activeSimNode()?.name).toBe('MainMenu');
    expect(component.activeSimNode()?.type).toBe('CollectDtmfDigits');
    expect(component.isTerminalNode()).toBe(false);
    expect(component.callLog().length).toBe(1);
    expect(component.callLog()[0]).toContain('MainMenu');
  });

  it('should transition to SalesRoute queue node when pressing key 1', () => {
    component.pressKey('1');

    const active = component.activeSimNode();
    expect(active?.name).toBe('SalesRoute');
    expect(active?.type).toBe('RouteToQueue');
    expect(active?.targetQueue).toBe('SalesEnterprise');
    expect(component.isTerminalNode()).toBe(true);
    expect(component.callLog().some(l => l.includes('[Key 1] -> SalesRoute'))).toBe(true);
  });

  it('should transition to SupportRoute queue node when pressing key 2', () => {
    component.pressKey('2');

    const active = component.activeSimNode();
    expect(active?.name).toBe('SupportRoute');
    expect(active?.targetQueue).toBe('TechSupportTier1');
    expect(component.isTerminalNode()).toBe(true);
  });

  it('should log invalid selection warning and remain on current node when an unmapped key is pressed', () => {
    component.pressKey('9');

    expect(component.activeSimNode()?.name).toBe('MainMenu');
    expect(component.callLog().some(l => l.includes('[Key 9] Invalid selection for MainMenu'))).toBe(true);
    expect(component.isTerminalNode()).toBe(false);
  });

  it('should reset simulation to root node on restartSimulation', () => {
    component.pressKey('1');
    expect(component.activeSimNode()?.name).toBe('SalesRoute');

    component.restartSimulation();
    expect(component.activeSimNode()?.name).toBe('MainMenu');
    expect(component.callLog().length).toBe(1);
    expect(component.callLog()[0]).toContain('Call reconnected -> MainMenu');
  });

  it('should return appropriate CSS badge classes for each IVR node type', () => {
    expect(component.getNodeBadgeClass('CollectDtmfDigits')).toContain('indigo');
    expect(component.getNodeBadgeClass('RouteToQueue')).toContain('emerald');
    expect(component.getNodeBadgeClass('VoicemailDrop')).toContain('amber');
    expect(component.getNodeBadgeClass('Hangup')).toContain('rose');
    expect(component.getNodeBadgeClass('PlayAudioPrompt')).toContain('blue');
  });
});
