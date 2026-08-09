import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** Paints the confirm button red, for anything that destroys or deactivates. */
  destructive?: boolean;
}

/**
 * A yes/no dialog, shared by every delete and deactivate in the app.
 *
 * One component rather than a dialog per feature, so the wording, button order and colour of a
 * destructive action stay consistent — inconsistency there is how people click the wrong one.
 */
@Component({
  selector: 'epm-confirm-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title class="confirm__title">
      <mat-icon [class.confirm__icon--danger]="data.destructive">
        {{ data.destructive ? 'warning_amber' : 'help_outline' }}
      </mat-icon>
      {{ data.title }}
    </h2>

    <mat-dialog-content>
      <p class="confirm__message">{{ data.message }}</p>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button type="button" (click)="dialogRef.close(false)">
        {{ data.cancelLabel ?? 'Cancel' }}
      </button>
      <button
        mat-flat-button
        type="button"
        [color]="data.destructive ? 'warn' : 'primary'"
        cdkFocusInitial
        (click)="dialogRef.close(true)"
      >
        {{ data.confirmLabel ?? 'Confirm' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .confirm__title {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .confirm__icon--danger {
      color: #b91c1c;
    }

    .confirm__message {
      margin: 0;
      color: #475569;
      line-height: 1.6;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialogComponent {
  readonly dialogRef = inject(MatDialogRef<ConfirmDialogComponent, boolean>);
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}
