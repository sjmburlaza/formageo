import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ProjectsApiService } from '@frontend/api-client';
import { Project } from '@frontend/models';
import { finalize } from 'rxjs';

@Component({
  selector: 'fg-project-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './project-list.component.html',
  styleUrl: './project-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectListComponent implements OnInit {
  private readonly projectsApi = inject(ProjectsApiService);

  protected readonly projects = signal<Project[]>([]);
  protected readonly loading = signal(false);
  protected readonly submitting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly projectForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(200)],
    }),
  });

  ngOnInit(): void {
    this.loadProjects();
  }

  protected createProject(): void {
    if (this.projectForm.invalid || this.submitting()) {
      this.projectForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    this.projectsApi
      .createProject({
        name: this.projectForm.controls.name.value,
      })
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (project) => {
          this.projects.update((projects) => [project, ...projects]);

          this.projectForm.reset();
        },
        error: () => {
          this.errorMessage.set('The project could not be created.');
        },
      });
  }

  private loadProjects(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.projectsApi
      .getProjects()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (projects) => {
          this.projects.set(projects);
        },
        error: () => {
          this.errorMessage.set('The projects could not be loaded.');
        },
      });
  }
}
