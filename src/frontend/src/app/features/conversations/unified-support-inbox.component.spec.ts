// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach } from 'vitest';
import { UnifiedSupportInboxComponent } from './unified-support-inbox.component';
import { TranslationService } from '../../core/services/translation.service';

describe('UnifiedSupportInboxComponent', () => {
  let component: UnifiedSupportInboxComponent;
  let mockTranslation: TranslationService;

  beforeEach(() => {
    mockTranslation = new TranslationService();
    component = new UnifiedSupportInboxComponent(mockTranslation);
  });

  it('initializes with active inquiry threads and urgent count', () => {
    expect(component.threads().length).toBeGreaterThanOrEqual(2);
    expect(component.activeThread()).toBeDefined();
    expect(component.activeThread()?.customerName).toBe('Marcus Vance');
    expect(component.urgentCount()).toBeGreaterThan(0);
  });

  it('selects thread correctly', () => {
    component.selectThread('thr_002');
    expect(component.selectedThreadId()).toBe('thr_002');
    expect(component.activeThread()?.customerName).toBe('Elena Rostova');
  });

  it('applies AI suggested draft into reply box', () => {
    expect(component.replyMessage()).toBe('');
    component.applyAiDraft();
    expect(component.replyMessage()).toBe(component.aiSuggestedDraft());
  });

  it('appends agent reply to message stream and updates status', () => {
    component.replyMessage.set('I have reset your token.');
    const initialCount = component.activeThread()!.messages.length;

    component.sendAgentReply();

    expect(component.activeThread()!.messages.length).toBe(initialCount + 1);
    expect(component.activeThread()!.status).toBe('PendingCustomer');
    expect(component.replyMessage()).toBe('');
  });

  it('escalates active thread to urgent', () => {
    component.escalateActiveThread();
    expect(component.activeThread()?.status).toBe('Escalated');
    expect(component.activeThread()?.priority).toBe('Urgent');
  });
});
