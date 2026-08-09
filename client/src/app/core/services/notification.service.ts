import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

/**
 * User-facing messages, in one place so their look and timing stay consistent.
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  success(message: string): void {
    this.snackBar.open(message, 'Dismiss', {
      duration: 4000,
      panelClass: 'epm-snack-success',
      horizontalPosition: 'right',
    });
  }

  // No auto-dismiss: an error the user never read is an error they will hit again.
  error(message: string): void {
    this.snackBar.open(message, 'Dismiss', {
      panelClass: 'epm-snack-error',
      horizontalPosition: 'right',
    });
  }
}
