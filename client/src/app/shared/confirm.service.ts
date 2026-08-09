import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, filter, map } from 'rxjs';
import { ConfirmDialogComponent, ConfirmDialogData } from './confirm-dialog.component';

@Injectable({ providedIn: 'root' })
export class ConfirmService {
  private readonly dialog = inject(MatDialog);

  /**
   * Opens the confirm dialog and emits only when the user actually confirms.
   *
   * Filtering out the cancel case means callers read as
   * `confirm(...).subscribe(() => doTheThing())` rather than wrapping the whole action in an
   * `if (result)` — there is one code path, and it is the one that does the work.
   */
  confirm(data: ConfirmDialogData): Observable<void> {
    return this.dialog
      .open(ConfirmDialogComponent, { data, width: '420px', autoFocus: 'dialog' })
      .afterClosed()
      .pipe(
        filter((confirmed): confirmed is true => confirmed === true),
        map(() => undefined),
      );
  }
}
