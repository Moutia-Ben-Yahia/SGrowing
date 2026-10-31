// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Injector, runInInjectionContext } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  AgentPresenceCockpitComponent,
  AgentPresenceState,
  CollisionLockState
} from './agent-presence-cockpit.component';

describe('AgentPresenceCockpitComponent (Collision Prevention & Presence Mesh)', () => {
  let component: AgentPresenceCockpitComponent;
  let mockHttp: any;

  beforeEach(() => {
    mockHttp = {
      get: vi.fn().mockReturnValue({ subscribe: () => {} }),
      post: vi.fn().mockImplementation(() => ({
        subscribe: (handler: any) => {
          if (typeof handler === 'function') handler({ success: false });
          else if (handler?.next) handler.next({ success: false });
        }
      })),
      put: vi.fn().mockReturnValue({ subscribe: () => {} }),
      delete: vi.fn().mockReturnValue({ subscribe: () => {} })
    };

    const injector = Injector.create({
      providers: [
        { provide: HttpClient, useValue: mockHttp }
      ]
    });

    component = runInInjectionContext(injector, () => new AgentPresenceCockpitComponent());
  });

  it('should initialize with default operators and selected simulation ticket', () => {
    expect(component.selectedTicketId()).toBe('aaaa0102-0000-0000-0000-000000000002');
    expect(component.op1AgentId).toBe('11111111-1111-1111-1111-111111111111');
    expect(component.op2AgentId).toBe('22222222-2222-2222-2222-222222222222');
    expect(component.isOp1HoldingLock()).toBe(true);
    expect(component.op1Draft()).toContain('checking your webhook');
  });

  it('should compute lockedTickets reactively when tickets are loaded', () => {
    expect(component.lockedTickets().length).toBe(0);

    component.tickets.set([
      {
        ticketId: 't-1',
        ticketTitle: 'Payment Webhook Failure',
        customerName: 'Starlight Retail',
        priority: 'Urgent',
        customerMessagePending: true,
        lockState: CollisionLockState.HardComposingLock,
        lockedByAgentId: component.op1AgentId,
        lockedByAgentName: 'Alex Vance',
        lockDraftSnippet: 'Investigating webhook...'
      },
      {
        ticketId: 't-2',
        ticketTitle: 'SSO SAML Timeout',
        customerName: 'Acme Corp',
        priority: 'Normal',
        customerMessagePending: true,
        lockState: CollisionLockState.SoftViewing
      },
      {
        ticketId: 't-3',
        ticketTitle: 'General Inquiry',
        customerName: 'Nordic Co',
        priority: 'Low',
        customerMessagePending: false,
        lockState: CollisionLockState.Unlocked
      }
    ]);

    const locked = component.lockedTickets();
    expect(locked.length).toBe(1);
    expect(locked[0].ticketId).toBe('t-1');
    expect(locked[0].lockedByAgentName).toBe('Alex Vance');
  });

  it('should trigger typing heartbeat on Operator 1 input update', () => {
    component.onOp1Typing('Drafting refund response...');

    expect(component.op1Draft()).toBe('Drafting refund response...');
    expect(mockHttp.post).toHaveBeenCalledWith(
      '/api/v1/conversations/presence/heartbeat',
      expect.objectContaining({
        agentId: component.op1AgentId,
        state: AgentPresenceState.ComposingReply,
        isTyping: true,
        draftSnippet: 'Drafting refund response...'
      })
    );
  });

  it('should block Operator 2 write attempt when Operator 1 holds the active lock', () => {
    component.isOp1HoldingLock.set(true);
    component.op2Draft.set('Elena attempting to reply...');

    component.op2AttemptWrite();

    // Verify blocked warning flag is set
    expect(component.op2BlockedWarning()).toBe(true);
    // Should attempt to acquire lock via HTTP
    expect(mockHttp.post).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/conversations/presence/tickets/'),
      expect.objectContaining({
        agentId: component.op2AgentId,
        agentName: 'Elena Rostova'
      })
    );
  });

  it('should execute supervisor override lock release', () => {
    component.isOp1HoldingLock.set(true);

    component.op2ForceAdminOverride();

    expect(mockHttp.post).toHaveBeenCalledWith(
      expect.stringContaining('/unlock'),
      expect.objectContaining({
        agentId: component.op2AgentId,
        reason: 3, // AdminOverride
        isAdminOverride: true
      })
    );
  });

  it('should manage auto-handoff sweep loading state', () => {
    expect(component.isSweepLoading()).toBe(false);

    component.triggerAutoHandoffSweep();

    // Verify sweep post called with default threshold
    expect(mockHttp.post).toHaveBeenCalledWith(
      expect.stringContaining('/auto-handoff/sweep'),
      { abandonmentThresholdMinutes: 5 }
    );
  });
});
