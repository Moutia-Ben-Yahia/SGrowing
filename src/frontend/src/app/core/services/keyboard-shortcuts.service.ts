import { Injectable, signal, inject } from '@angular/core';
import { Router } from '@angular/router';

export interface ShortcutDefinition {
  id: string;
  combination: string;
  description: string;
  action: () => void;
}

@Injectable({
  providedIn: 'root'
})
export class KeyboardShortcutsService {
  private readonly router?: Router;

  public readonly isMacroSearchOpen = signal<boolean>(false);
  public readonly lastShortcutPressed = signal<string | null>(null);
  public readonly shortcutToastMessage = signal<string | null>(null);

  private listenerAttached = false;
  private keydownHandler?: (event: KeyboardEvent) => void;

  public readonly shortcuts: ShortcutDefinition[] = [
    {
      id: 'macro-search',
      combination: 'Ctrl+Shift+Space',
      description: 'Quick Macro Search Palette',
      action: () => this.toggleMacroSearch()
    },
    {
      id: 'softphone-dialer',
      combination: 'Ctrl+Alt+D',
      description: 'Open Softphone Dialer',
      action: () => {
        this.showToast('Navigating to Softphone Dialer...');
        this.router?.navigate(['/softphone']);
      }
    },
    {
      id: 'agent-missions',
      combination: 'Ctrl+Alt+M',
      description: 'Open Autonomous Agent Missions',
      action: () => {
        this.showToast('Navigating to Agent Missions...');
        this.router?.navigate(['/agents/missions']);
      }
    }
  ];

  constructor(router?: Router) {
    try {
      this.router = router || inject(Router, { optional: true }) || undefined;
    } catch {
      this.router = router;
    }
    this.init();
  }

  public init(): void {
    if (this.listenerAttached || typeof window === 'undefined') return;

    this.keydownHandler = (event: KeyboardEvent) => this.handleKeyDown(event);
    window.addEventListener('keydown', this.keydownHandler);
    this.listenerAttached = true;
  }

  public destroy(): void {
    if (this.listenerAttached && this.keydownHandler && typeof window !== 'undefined') {
      window.removeEventListener('keydown', this.keydownHandler);
      this.listenerAttached = false;
    }
  }

  public handleKeyDown(event: KeyboardEvent): void {
    const isCtrl = event.ctrlKey || event.metaKey;

    // 1. Quick Macro Search: Ctrl + Shift + Space (Always accessible)
    if (isCtrl && event.shiftKey && (event.code === 'Space' || event.key === ' ')) {
      event.preventDefault();
      event.stopPropagation();
      this.lastShortcutPressed.set('Ctrl+Shift+Space');
      this.toggleMacroSearch();
      return;
    }

    // 2. Escape: Close Macro Search if open
    if (event.key === 'Escape' && this.isMacroSearchOpen()) {
      event.preventDefault();
      this.closeMacroSearch();
      return;
    }

    // Guard: Do not intercept page navigation shortcuts when user is actively editing text in input/textarea
    const target = event.target as HTMLElement | null;
    const isEditingText = !!target && (
      target.tagName === 'INPUT' ||
      target.tagName === 'TEXTAREA' ||
      target.isContentEditable
    );
    if (isEditingText) {
      return;
    }

    // 3. Softphone Dialer: Ctrl + Alt + D
    if (isCtrl && event.altKey && (event.key?.toLowerCase() === 'd')) {
      event.preventDefault();
      event.stopPropagation();
      this.lastShortcutPressed.set('Ctrl+Alt+D');
      this.showToast('📞 Softphone Dialer Activated');
      this.router?.navigate(['/softphone']);
      return;
    }

    // 4. Agent Missions: Ctrl + Alt + M
    if (isCtrl && event.altKey && (event.key?.toLowerCase() === 'm')) {
      event.preventDefault();
      event.stopPropagation();
      this.lastShortcutPressed.set('Ctrl+Alt+M');
      this.showToast('🤖 Agent Missions Cockpit Activated');
      this.router?.navigate(['/agents/missions']);
      return;
    }
  }

  public toggleMacroSearch(): void {
    const newState = !this.isMacroSearchOpen();
    this.isMacroSearchOpen.set(newState);
    if (newState) {
      this.showToast('⚡ Quick Macro Search Opened');
    }
  }

  public openMacroSearch(): void {
    this.isMacroSearchOpen.set(true);
  }

  public closeMacroSearch(): void {
    this.isMacroSearchOpen.set(false);
  }

  private showToast(msg: string): void {
    this.shortcutToastMessage.set(msg);
    setTimeout(() => {
      if (this.shortcutToastMessage() === msg) {
        this.shortcutToastMessage.set(null);
      }
    }, 2500);
  }
}
