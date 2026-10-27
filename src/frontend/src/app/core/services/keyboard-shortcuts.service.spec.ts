// @vitest-environment jsdom
import '@angular/compiler';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { KeyboardShortcutsService } from './keyboard-shortcuts.service';

describe('KeyboardShortcutsService (Desktop Shortcuts)', () => {
  let service: KeyboardShortcutsService;
  let mockRouter: any;

  beforeEach(() => {
    mockRouter = {
      navigate: vi.fn()
    };
    service = new KeyboardShortcutsService(mockRouter);
  });

  afterEach(() => {
    service.destroy();
  });

  it('should toggle quick macro search on Ctrl+Shift+Space', () => {
    expect(service.isMacroSearchOpen()).toBe(false);

    const event = new KeyboardEvent('keydown', {
      ctrlKey: true,
      shiftKey: true,
      code: 'Space',
      key: ' '
    });

    service.handleKeyDown(event);
    expect(service.isMacroSearchOpen()).toBe(true);
    expect(service.lastShortcutPressed()).toBe('Ctrl+Shift+Space');

    // Press again to close
    service.handleKeyDown(event);
    expect(service.isMacroSearchOpen()).toBe(false);
  });

  it('should navigate to softphone dialer on Ctrl+Alt+D', () => {
    const event = new KeyboardEvent('keydown', {
      ctrlKey: true,
      altKey: true,
      code: 'KeyD',
      key: 'd'
    });

    service.handleKeyDown(event);
    expect(service.lastShortcutPressed()).toBe('Ctrl+Alt+D');
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/softphone']);
  });

  it('should navigate to agent missions on Ctrl+Alt+M', () => {
    const event = new KeyboardEvent('keydown', {
      ctrlKey: true,
      altKey: true,
      code: 'KeyM',
      key: 'm'
    });

    service.handleKeyDown(event);
    expect(service.lastShortcutPressed()).toBe('Ctrl+Alt+M');
    expect(mockRouter.navigate).toHaveBeenCalledWith(['/agents/missions']);
  });

  it('should close macro search when Escape is pressed', () => {
    service.openMacroSearch();
    expect(service.isMacroSearchOpen()).toBe(true);

    const escEvent = new KeyboardEvent('keydown', {
      key: 'Escape'
    });

    service.handleKeyDown(escEvent);
    expect(service.isMacroSearchOpen()).toBe(false);
  });

  it('should not intercept navigation shortcuts when typing inside an input or textarea', () => {
    const inputEl = document.createElement('input');
    document.body.appendChild(inputEl);
    inputEl.focus();

    // Synthetic event with target = inputEl
    const event = new KeyboardEvent('keydown', {
      ctrlKey: true,
      altKey: true,
      code: 'KeyD',
      key: 'd'
    });
    Object.defineProperty(event, 'target', { value: inputEl, enumerable: true });

    service.handleKeyDown(event);
    expect(mockRouter.navigate).not.toHaveBeenCalled();

    // Ctrl+Shift+Space still opens Macro Search even from input
    const macroEvent = new KeyboardEvent('keydown', {
      ctrlKey: true,
      shiftKey: true,
      code: 'Space',
      key: ' '
    });
    Object.defineProperty(macroEvent, 'target', { value: inputEl, enumerable: true });
    service.handleKeyDown(macroEvent);
    expect(service.isMacroSearchOpen()).toBe(true);

    document.body.removeChild(inputEl);
  });

  it('should not trigger agent missions when AltGr produces a special character like µ', () => {
    const event = new KeyboardEvent('keydown', {
      ctrlKey: true,
      altKey: true,
      code: 'KeyM',
      key: 'µ'
    });

    service.handleKeyDown(event);
    expect(mockRouter.navigate).not.toHaveBeenCalled();
    expect(service.lastShortcutPressed()).toBeNull();
  });
});
