import { Component, signal, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { QuickMacroModalComponent } from './core/components/quick-macro-modal.component';
import { KeyboardShortcutsService } from './core/services/keyboard-shortcuts.service';
import { TranslationService } from './core/services/translation.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, QuickMacroModalComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly title = signal('marketing-studio-web');
  public readonly shortcuts = inject(KeyboardShortcutsService);
  public readonly translation = inject(TranslationService);
}
