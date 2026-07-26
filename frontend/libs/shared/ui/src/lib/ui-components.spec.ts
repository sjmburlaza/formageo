import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AlertComponent } from './alert/alert.component';
import { EmptyStateComponent } from './empty-state/empty-state.component';
import { MetricCardComponent } from './metric-card/metric-card.component';
import { PageStateComponent } from './page-state/page-state.component';
import { StatusBadgeComponent } from './status-badge/status-badge.component';

describe('shared UI components', () => {
  function createComponent<T>(component: new (...args: never[]) => T) {
    const fixture = TestBed.createComponent(component);
    return fixture as ComponentFixture<T>;
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [
        AlertComponent,
        EmptyStateComponent,
        MetricCardComponent,
        PageStateComponent,
        StatusBadgeComponent,
      ],
    }).compileComponents();
  });

  it('renders an actionable alert and emits its action', () => {
    const fixture = createComponent(AlertComponent);
    fixture.componentRef.setInput('message', 'The request failed.');
    fixture.componentRef.setInput('tone', 'error');
    fixture.componentRef.setInput('actionLabel', 'Try again');
    const action = vi.fn();
    fixture.componentInstance.actionTriggered.subscribe(action);

    fixture.detectChanges();
    fixture.nativeElement.querySelector('button').click();

    expect(fixture.nativeElement.textContent).toContain('The request failed.');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
    expect(action).toHaveBeenCalledOnce();
  });

  it('renders a compact empty state with the requested heading level', () => {
    const fixture = createComponent(EmptyStateComponent);
    fixture.componentRef.setInput('title', 'No matching sites');
    fixture.componentRef.setInput('description', 'Try another search.');
    fixture.componentRef.setInput('headingLevel', 4);
    fixture.componentRef.setInput('compact', true);

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('h4').textContent).toContain(
      'No matching sites',
    );
    expect(
      fixture.nativeElement.querySelector('.empty-state--compact'),
    ).toBeTruthy();
  });

  it('emits when an interactive metric is activated', () => {
    const fixture = createComponent(MetricCardComponent);
    fixture.componentRef.setInput('label', 'Centroid');
    fixture.componentRef.setInput('value', '121.04, 14.62');
    fixture.componentRef.setInput('interactive', true);
    const activated = vi.fn();
    fixture.componentInstance.activated.subscribe(activated);

    fixture.detectChanges();
    fixture.nativeElement.querySelector('button').click();

    expect(activated).toHaveBeenCalledOnce();
  });

  it('uses the correct accessibility semantics for page states', () => {
    const fixture = createComponent(PageStateComponent);
    fixture.componentRef.setInput('title', 'Unable to open project');
    fixture.componentRef.setInput('description', 'Try again later.');
    fixture.componentRef.setInput('tone', 'error');

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('h1').textContent).toContain(
      'Unable to open project',
    );
  });

  it('renders the requested status badge tone', () => {
    const fixture = createComponent(StatusBadgeComponent);
    fixture.componentRef.setInput('label', 'Archived');
    fixture.componentRef.setInput('tone', 'neutral');

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Archived');
    expect(fixture.nativeElement.querySelector('.badge--neutral')).toBeTruthy();
  });
});
