import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The title block every page opens with. The `actions` slot takes the page's primary button.
 */
@Component({
  selector: 'epm-page-header',
  standalone: true,
  template: `
    <header class="page-header">
      <div class="page-header__text">
        <h1>{{ title() }}</h1>
        @if (subtitle()) {
          <p>{{ subtitle() }}</p>
        }
      </div>
      <div class="page-header__actions">
        <ng-content select="[actions]" />
      </div>
    </header>
  `,
  styles: `
    .page-header {
      display: flex;
      flex-wrap: wrap;
      gap: 16px;
      align-items: flex-start;
      justify-content: space-between;
      margin-bottom: 24px;
    }

    .page-header__text h1 {
      font-size: 1.5rem;
    }

    .page-header__text p {
      margin: 4px 0 0;
      color: #64748b;
      font-size: 0.875rem;
    }

    .page-header__actions {
      display: flex;
      gap: 8px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string>();
}
