import { Routes } from '@angular/router';
import { ProjectListComponent } from './features/projects/project-list/project-list.component';

export const appRoutes: Routes = [
  {
    path: 'projects',
    component: ProjectListComponent,
  },
  {
    path: 'projects/:projectId',
    loadComponent: () =>
      import(
        './features/projects/project-details/project-details.component'
      ).then((module) => module.ProjectDetailsComponent),
    canDeactivate: [
      (component: { canDeactivate(): boolean }) => component.canDeactivate(),
    ],
  },
  {
    path: 'sites/:siteId',
    loadComponent: () =>
      import('./features/sites/site-details/site-details.component').then(
        (module) => module.SiteDetailsComponent,
      ),
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
