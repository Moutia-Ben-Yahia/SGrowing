// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { createEnvironmentInjector } from '@angular/core';
import { of } from 'rxjs';
import { BotSimulatorRunnerComponent } from './bot-simulator-runner.component';
import { ApiService } from '../../core/services/api.service';
import { BotSimulationSession, BotSimulationStepResult } from '../../core/models/pillars.models';

describe('BotSimulatorRunnerComponent', () => {
  let component: BotSimulatorRunnerComponent;
  let apiServiceMock: any;

  const mockSession: BotSimulationSession = {
    sessionId: 'sess-123',
    flowName: 'InteractiveDemoBot',
    currentNodeId: 'node-welcome',
    variables: { userRole: 'Growth Leader' },
    isWaitingForUserInput: true,
    isHandedOverToHuman: false,
    isCompleted: false,
    transcript: [
      { sender: 'Bot', content: 'Hello! I am your AI assistant.' }
    ]
  };

  const mockStepResult: BotSimulationStepResult = {
    currentNodeId: 'node-recommendation',
    newMessages: [
      { sender: 'User', content: 'Tell me about COD' },
      { sender: 'Bot', content: 'Our COD module includes doorstep tracking.' }
    ],
    updatedVariables: { userRole: 'Growth Leader', topic: 'COD' },
    isWaitingForUserInput: false,
    isHandedOverToHuman: false,
    isCompleted: true,
    executionTraceLogs: ['[Trace] Node transition to recommendation']
  };

  beforeEach(() => {
    apiServiceMock = {
      startBotSimulation: vi.fn().mockReturnValue(of(mockSession)),
      stepBotSimulation: vi.fn().mockReturnValue(of(mockStepResult))
    };

    const injector = createEnvironmentInjector([
      { provide: ApiService, useValue: apiServiceMock }
    ]);

    injector.runInContext(() => {
      component = new BotSimulatorRunnerComponent();
    });
  });

  it('should initialize simulation session on construction', () => {
    expect(component).toBeTruthy();
    expect(apiServiceMock.startBotSimulation).toHaveBeenCalled();
    expect(component.session()?.sessionId).toBe('sess-123');
    expect(component.transcript().length).toBe(1);
    expect(component.traceLogs()[0]).toContain('Session ready');
  });

  it('should send user message and process step execution', () => {
    component.userMessageInput = 'Tell me about COD';
    component.sendUserMessage();

    expect(component.userMessageInput).toBe('');
    expect(apiServiceMock.stepBotSimulation).toHaveBeenCalledWith(expect.objectContaining({
      userMessage: 'Tell me about COD'
    }));
    expect(component.session()?.currentNodeId).toBe('node-recommendation');
    expect(component.transcript().length).toBe(3); // 1 initial + 2 new
    expect(component.isStepping()).toBe(false);
  });

  it('should ignore empty message or missing session', () => {
    apiServiceMock.stepBotSimulation.mockClear();
    component.userMessageInput = '   ';
    component.sendUserMessage();

    expect(apiServiceMock.stepBotSimulation).not.toHaveBeenCalled();
  });

  it('should handle card choice selection', () => {
    component.sendCardChoice('outreach');

    expect(apiServiceMock.stepBotSimulation).toHaveBeenCalledWith(expect.objectContaining({
      userMessage: 'outreach'
    }));
  });

  it('should format JSON object correctly', () => {
    const formatted = component.formatJson({ key: 'value' });
    expect(formatted).toContain('"key": "value"');
  });

  it('should reset simulation when requested', () => {
    apiServiceMock.startBotSimulation.mockClear();
    component.resetSimulation();

    expect(apiServiceMock.startBotSimulation).toHaveBeenCalled();
  });
});
