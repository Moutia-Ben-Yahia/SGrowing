import { Routes } from '@angular/router';
import { InboxShellComponent } from './features/inbox/inbox-shell.component';
import { OutboundStudioComponent } from './features/outbound/outbound-studio.component';

export const routes: Routes = [
  { path: '', component: InboxShellComponent },
  { path: 'outbound', component: OutboundStudioComponent },
  { path: '**', redirectTo: '' }
];
