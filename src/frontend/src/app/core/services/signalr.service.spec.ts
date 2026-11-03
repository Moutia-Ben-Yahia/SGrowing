import '@angular/compiler';
import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest';
import { SignalRService, BufferedMessage } from './signalr.service';

describe('SignalRService', () => {
  let service: SignalRService;

  beforeEach(() => {
    service = new SignalRService();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should initialize with disconnected state and empty offline queue', () => {
    expect(service.isConnected()).toBe(false);
    expect(service.connectionState()).toBe('Disconnected');
    expect(service.offlineQueue()).toEqual([]);
  });

  it('should enqueue messages to offline queue', () => {
    const msg: BufferedMessage = {
      clientUuid: 'uuid-1',
      conversationId: 'conv-101',
      content: 'Hello offline world',
      queuedAt: new Date().toISOString()
    };

    service.enqueueOfflineMessage(msg);

    expect(service.offlineQueue().length).toBe(1);
    expect(service.offlineQueue()[0]).toEqual(msg);

    const msg2: BufferedMessage = {
      clientUuid: 'uuid-2',
      conversationId: 'conv-102',
      content: 'Second offline msg',
      queuedAt: new Date().toISOString()
    };

    service.enqueueOfflineMessage(msg2);
    expect(service.offlineQueue().length).toBe(2);
  });

  it('should emit events via subjects when received', () => {
    let receivedMessage: any;
    const sub = service.messageReceived$.subscribe(m => receivedMessage = m);

    service.messageReceived$.next({
      id: 'msg-1',
      conversationId: 'conv-1',
      content: 'Incoming chat',
      senderType: 'Customer',
      sentAt: new Date().toISOString()
    } as any);

    expect(receivedMessage).toBeDefined();
    expect(receivedMessage.id).toBe('msg-1');
    sub.unsubscribe();
  });

  it('should emit typing indicators and sla alerts', () => {
    let typingData: any;
    let alertData: any;

    const sub1 = service.typingIndicator$.subscribe(t => typingData = t);
    const sub2 = service.slaBreachAlert$.subscribe(a => alertData = a);

    service.typingIndicator$.next({ conversationId: 'c-1', senderName: 'Alice', isTyping: true });
    service.slaBreachAlert$.next({ conversationId: 'c-1', priority: 'High', minutesRemaining: 5, isBreached: false });

    expect(typingData).toEqual({ conversationId: 'c-1', senderName: 'Alice', isTyping: true });
    expect(alertData).toEqual({ conversationId: 'c-1', priority: 'High', minutesRemaining: 5, isBreached: false });

    sub1.unsubscribe();
    sub2.unsubscribe();
  });

  it('should safely do nothing when invoke methods are called without active connection', async () => {
    // Should not throw
    await expect(service.joinConversation('conv-1')).resolves.toBeUndefined();
    await expect(service.leaveConversation('conv-1')).resolves.toBeUndefined();
    await expect(service.broadcastTyping('conv-1', 'Agent', true)).resolves.toBeUndefined();
    await expect(service.lockConversation('conv-1', 'user-1', 'Agent')).resolves.toBeUndefined();
    await expect(service.unlockConversation('conv-1')).resolves.toBeUndefined();
  });
});
