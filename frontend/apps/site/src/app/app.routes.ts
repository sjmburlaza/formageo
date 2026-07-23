import { Routes } from '@angular/router';
import { ProjectListComponent } from './features/projects/project-list/project-list.component';

export const appRoutes: Routes = [
  {
    path: 'projects',
    component: ProjectListComponent,
  },
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'projects',
  },
  {
    path: '**',
    redirectTo: 'projects',
  },
];
