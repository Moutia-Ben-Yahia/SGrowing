// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { AiGovernanceCockpitComponent } from './ai-governance-cockpit.component';
import { TranslationService } from '../../core/services/translation.service';

describe('AiGovernanceCockpitComponent', () => {
  let component: AiGovernanceCockpitComponent;
  let mockTranslation: TranslationService;

  beforeEach(() => {
    mockTranslation = new TranslationService();
    component = new AiGovernanceCockpitComponent(mockTranslation);
  });

  it('initializes with clean prompt and StandardCopywriting route', () => {
    expect(component.selectedComplexity).toBe('StandardCopywriting');
    expect(component.currentRoute().primary).toContain('Gemini 1.5 Pro');
    expect(component.inspectionResult()?.threatLevel).toBe('Clean');
    expect(component.inspectionResult()?.isBlocked).toBe(false);
  });

  it('detects and blocks prompt injection attacks', () => {
    component.loadSample('attack');
    const result = component.inspectionResult();

    expect(result).not.toBeNull();
    expect(result?.isBlocked).toBe(true);
    expect(result?.threatLevel).toBe('Critical');
    expect(result?.threats.length).toBeGreaterThan(0);
  });

  it('detects and redacts PII without blocking legitimate query', () => {
    component.loadSample('pii');
    const result = component.inspectionResult();

    expect(result).not.toBeNull();
    expect(result?.isBlocked).toBe(false);
    expect(result?.redactedTokensCount).toBeGreaterThan(0);
    expect(result?.sanitizedPrompt).toContain('[EMAIL_REDACTED]');
    expect(result?.sanitizedPrompt).toContain('[PHONE_REDACTED]');
  });

  it('routes to Claude 3.5 Sonnet for ComplexReasoning', () => {
    component.selectedComplexity = 'ComplexReasoning';
    expect(component.currentRoute().primary).toContain('Claude 3.5 Sonnet');
    expect(component.currentRoute().latencyMs).toBeGreaterThan(500);
  });

  it('routes to Ollama / Flash for SimpleClassification', () => {
    component.selectedComplexity = 'SimpleClassification';
    expect(component.currentRoute().primary).toContain('OllamaLocal');
    expect(component.currentRoute().costPer1k).toBeLessThan(0.001);
  });
});
