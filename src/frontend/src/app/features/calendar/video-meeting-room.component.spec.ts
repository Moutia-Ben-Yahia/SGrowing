// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { Injector, runInInjectionContext, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { of } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';
import { VideoMeetingRoomComponent } from './video-meeting-room.component';

describe('VideoMeetingRoomComponent', () => {
  let component: VideoMeetingRoomComponent;
  let mockApi: any;
  let mockAuth: any;
  let mockRoute: any;
  let mockRouter: any;

  beforeEach(() => {
    mockApi = {
      joinMeetingRoom: vi.fn().mockReturnValue(of({
        token: { token: 'jwt-token-123' },
        room: {
          id: 'room-1',
          title: 'Client Enterprise Consultation',
          hostName: 'Host Sarah',
          attendeeName: 'Dr. Ben Salem'
        }
      })),
      updateMeetingMediaState: vi.fn().mockReturnValue(of({})),
      evaluateMeetingObjections: vi.fn().mockReturnValue(of({
        sentiment: 'Positive',
        sentimentScore: 0.85,
        matchedBattleCards: [
          {
            objectionKey: 'SecurityGdpr',
            category: 'Security',
            title: 'GDPR Compliance & Local Sovereignty',
            detectedTrigger: 'Data privacy guarantees',
            quickPitch: 'SGrowing stores customer data strictly on dedicated European sovereign clusters.',
            keyTalkingPoints: ['ISO 27001 certified data center', 'Tenant data isolation'],
            recommendedFollowUpQuestion: 'Do you require custom DPA execution?',
            confidenceScore: 0.98,
            severity: 'high'
          }
        ]
      })),
      endMeetingRoom: vi.fn().mockReturnValue(of({}))
    };

    mockAuth = {
      currentTenant: signal({ id: 'tenant-1', name: 'Test Tenant' })
    };

    mockRoute = {
      paramMap: of({
        get: (key: string) => (key === 'roomToken' ? 'ROOM-DEMO-42' : null)
      })
    };

    mockRouter = {
      navigate: vi.fn()
    };

    const injector = Injector.create({
      providers: [
        { provide: ApiService, useValue: mockApi },
        { provide: AuthService, useValue: mockAuth },
        { provide: ActivatedRoute, useValue: mockRoute },
        { provide: Router, useValue: mockRouter }
      ]
    });

    component = runInInjectionContext(injector, () => new VideoMeetingRoomComponent());
    component.ngOnInit();
  });

  afterEach(() => {
    component.ngOnDestroy();
  });

  it('should initialize room token from route parameters and join meeting room', () => {
    expect(component).toBeTruthy();
    expect(component.roomToken()).toBe('ROOM-DEMO-42');
    expect(mockApi.joinMeetingRoom).toHaveBeenCalledWith('ROOM-DEMO-42', expect.any(Object));
    expect(component.room()?.title).toBe('Client Enterprise Consultation');
    expect(component.participantName()).toBe('Host Sarah');
  });

  it('should toggle microphone and synchronize media state', () => {
    expect(component.isMicMuted()).toBe(false);

    component.toggleMic();
    expect(component.isMicMuted()).toBe(true);
    expect(mockApi.updateMeetingMediaState).toHaveBeenCalled();

    component.toggleMic();
    expect(component.isMicMuted()).toBe(false);
  });

  it('should toggle camera state', () => {
    expect(component.isCameraOff()).toBe(false);

    component.toggleCamera();
    expect(component.isCameraOff()).toBe(true);
    expect(mockApi.updateMeetingMediaState).toHaveBeenCalled();
  });

  it('should simulate customer objection speech and surface real-time AI sales battle card', () => {
    component.simulateObjection('security');

    expect(mockApi.evaluateMeetingObjections).toHaveBeenCalledWith(
      'ROOM-DEMO-42',
      expect.stringContaining('data privacy')
    );
    expect(component.overallSentiment()).toBe('Positive');
    expect(component.sentimentScore()).toBe(0.85);
    expect(component.activeBattleCards().some(c => c.objectionKey === 'SecurityGdpr')).toBe(true);
    expect(component.isCopilotOpen()).toBe(true);
  });

  it('should handle call end lifecycle and navigate to calendar', () => {
    component.confirmLeaveCall();

    expect(mockApi.endMeetingRoom).toHaveBeenCalledWith('ROOM-DEMO-42', expect.any(String));
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/calendar']);
  });
});
