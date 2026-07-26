import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Project } from '@frontend/models';
import {
  LucideArrowRight,
  LucideEllipsis,
  LucideLandPlot,
} from '@lucide/angular';

@Component({
  selector: 'fg-project-card',
  standalone: true,
  imports: [
    DatePipe,
    LucideArrowRight,
    LucideEllipsis,
    LucideLandPlot,
    RouterLink,
  ],
  templateUrl: './project-card.component.html',
  styleUrl: './project-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectCardComponent {
  readonly project = input<Project | null>(null);
  readonly loading = input(false);
}
