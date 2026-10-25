import { Routes } from '@angular/router';
import { InboxShellComponent } from './features/inbox/inbox-shell.component';
import { OutboundStudioComponent } from './features/outbound/outbound-studio.component';
import { JourneyStudioComponent } from './features/journeys/journey-studio.component';
import { FeedbackStudioComponent } from './features/feedback/feedback-studio.component';
import { KnowledgeStudioComponent } from './features/knowledge/knowledge-studio.component';
import { BroadcastStudioComponent } from './features/broadcasts/broadcast-studio.component';
import { SlaStudioComponent } from './features/sla/sla-studio.component';

export const routes: Routes = [
  { path: '', component: InboxShellComponent },
  { path: 'outbound', component: OutboundStudioComponent },
  { path: 'broadcasts', component: BroadcastStudioComponent },
  { path: 'sla', component: SlaStudioComponent },
  { path: 'journeys', component: JourneyStudioComponent },
  { path: 'feedback', component: FeedbackStudioComponent },
  { path: 'help', component: KnowledgeStudioComponent },
  { path: '**', redirectTo: '' }
];
