import { Routes } from '@angular/router';
import { InboxShellComponent } from './features/inbox/inbox-shell.component';

export const routes: Routes = [
  { path: '', component: InboxShellComponent },
  { path: '**', redirectTo: '' }
];
