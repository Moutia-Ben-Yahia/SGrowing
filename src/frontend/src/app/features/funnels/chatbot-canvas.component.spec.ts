// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector, signal } from '@angular/core';
import { of } from 'rxjs';
import { ChatbotCanvasComponent } from './chatbot-canvas.component';
import { ApiService } from '../../core/services/api.service';
import { AuthService } from '../../core/services/auth.service';

describe('ChatbotCanvasComponent', () => {
  let component: ChatbotCanvasComponent;
  let apiServiceMock: any;
  let authServiceMock: any;

  beforeEach(() => {
    apiServiceMock = {
      registerChatbotFlow: vi.fn().mockReturnValue(of({ flowId: 'flow-999' })),
      processSocialCommentWebhook: vi.fn().mockReturnValue(of({
        publicReplyComment: 'Thanks! Check your DM.',
        initiatedPrivateDm: true,
        privateDmInitialMessage: 'Here is your catalog link.'
      })),
      executeChatbotFlowStep: vi.fn().mockReturnValue(of({
        currentNodeId: 'node-3',
        variables: { choice: 'pricing' },
        dispatchedMessages: ['Here is our price guide'],
        waitingInputPrompt: 'Any questions?'
      }))
    };

    authServiceMock = {
      currentTenant: signal({ id: 't-1', name: 'Demo Tenant' })
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiServiceMock },
      { provide: AuthService, useValue: authServiceMock }
    ]);

    injector.runInContext(() => {
      component = new ChatbotCanvasComponent();
    });
  });

  it('should create component and populate default nodes on init', () => {
    component.ngOnInit();
    expect(component).toBeTruthy();
    expect(component.activeTab()).toBe('canvas');
    expect(component.nodes().length).toBeGreaterThan(0);
  });

  it('should switch between tabs', () => {
    component.activeTab.set('comment-trigger');
    expect(component.activeTab()).toBe('comment-trigger');

    component.activeTab.set('simulator');
    expect(component.activeTab()).toBe('simulator');
  });

  it('should save and deploy chatbot flow', () => {
    component.flowName = 'Custom Test Flow';
    component.saveFlow();

    expect(apiServiceMock.registerChatbotFlow).toHaveBeenCalledWith(
      expect.objectContaining({
        name: 'Custom Test Flow',
        channel: 'WhatsApp'
      }),
      't-1'
    );
    expect(component.flowId()).toBe('flow-999');
    expect(component.isSaving()).toBe(false);
  });

  it('should test social comment webhook trigger', () => {
    component.commentTriggerKeywords = 'price, cost';
    component.testCommentTrigger();

    expect(apiServiceMock.processSocialCommentWebhook).toHaveBeenCalledWith(
      expect.objectContaining({
        commentAuthor: 'sarah_growth',
        platform: 'Instagram'
      }),
      't-1'
    );
    expect(component.commentReactionResult()?.initiatedPrivateDm).toBe(true);
    expect(component.isTestingComment()).toBe(false);
  });

  it('should restart simulation with initial messages', () => {
    component.ngOnInit();
    component.restartSimulation();

    expect(component.simulationMessages().length).toBe(2);
    expect(component.currentSimulatorNodeId()).toBe('node-2');
  });

  it('should send simulator message and process response', () => {
    component.userInput = 'show pricing';
    component.sendSimulatorMessage();

    expect(component.userInput).toBe('');
    expect(apiServiceMock.executeChatbotFlowStep).toHaveBeenCalled();
    expect(component.currentSimulatorNodeId()).toBe('node-3');
    expect(component.simulationMessages().some(m => m.text === 'Here is our price guide')).toBe(true);
    expect(component.simulationMessages().some(m => m.text === 'Any questions?')).toBe(true);
  });

  it('should resolve node type labels correctly', () => {
    expect(component.getNodeTypeLabel(1)).toBe('Message');
    expect(component.getNodeTypeLabel(2)).toBe('Capture Input');
    expect(component.getNodeTypeLabel(3)).toBe('Decision Split');
    expect(component.getNodeTypeLabel(4)).toBe('Delay Timer');
    expect(component.getNodeTypeLabel(5)).toBe('API Webhook');
    expect(component.getNodeTypeLabel(6)).toBe('Custom Script');
    expect(component.getNodeTypeLabel(99)).toBe('Step');
  });
});
