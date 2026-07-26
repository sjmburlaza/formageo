import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
  signal,
} from '@angular/core';
import { Site } from '@frontend/models';
import { EmptyStateComponent } from '@frontend/ui';
import {
  LucideEye,
  LucideEyeOff,
  LucideLandPlot,
  LucideSearch,
} from '@lucide/angular';

@Component({
  selector: 'fg-project-site-list',
  standalone: true,
  imports: [
    EmptyStateComponent,
    LucideEye,
    LucideEyeOff,
    LucideLandPlot,
    LucideSearch,
  ],
  templateUrl: './project-site-list.component.html',
  styleUrl: './project-site-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProjectSiteListComponent {
  readonly sites = input<readonly Site[]>([]);
  readonly selectedSiteId = input<string | null>(null);
  readonly hiddenSiteIds = input<ReadonlySet<string>>(new Set());

  readonly drawRequested = output<void>();
  readonly siteSelected = output<Site>();
  readonly visibilityToggled = output<Site>();

  protected readonly searchQuery = signal('');
  protected readonly filteredSites = computed(() => {
    const query = this.searchQuery().trim().toLocaleLowerCase();
    return query
      ? this.sites().filter((site) =>
          site.name.toLocaleLowerCase().includes(query),
        )
      : this.sites();
  });

  protected updateSearch(event: Event): void {
    this.searchQuery.set((event.target as HTMLInputElement).value);
  }
}
