import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { getApiErrorMessage, SitesApiService } from '@frontend/api-client';
import { Site } from '@frontend/models';
import {
  LucideArrowLeft,
  LucideCircleAlert,
  LucideTrash2,
} from '@lucide/angular';
import { finalize } from 'rxjs';

@Component({
  selector: 'fg-site-details',
  standalone: true,
  imports: [
    CommonModule,
    LucideArrowLeft,
    LucideCircleAlert,
    LucideTrash2,
    RouterLink,
  ],
  templateUrl: './site-details.component.html',
  styleUrl: './site-details.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SiteDetailsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sitesApi = inject(SitesApiService);

  protected readonly site = signal<Site | null>(null);
  protected readonly loading = signal(true);
  protected readonly deleting = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    this.loadSite();
  }

  protected deleteSite(): void {
    const site = this.site();

    if (
      !site ||
      this.deleting() ||
      !window.confirm(
        `Delete "${site.name}"? This permanently removes its boundary.`,
      )
    ) {
      return;
    }

    this.deleting.set(true);
    this.errorMessage.set(null);

    this.sitesApi
      .deleteSite(site.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          void this.router.navigate(['/projects', site.projectId]);
        },
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(error, 'The site could not be deleted.'),
          );
        },
      });
  }

  protected retry(): void {
    this.loadSite();
  }

  protected formattedBoundary(site: Site): string {
    return JSON.stringify(site.boundary, null, 2);
  }

  private loadSite(): void {
    const siteId = this.route.snapshot.paramMap.get('siteId');

    if (!siteId) {
      this.loading.set(false);
      this.errorMessage.set('No site ID was provided.');
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);
    this.site.set(null);

    this.sitesApi
      .getSite(siteId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (site) => this.site.set(site),
        error: (error: unknown) => {
          this.errorMessage.set(
            getApiErrorMessage(error, 'The site could not be loaded.'),
          );
        },
      });
  }
}
