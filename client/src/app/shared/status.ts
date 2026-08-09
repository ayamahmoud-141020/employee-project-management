import { ProjectStatus } from '../core/models/api.models';

/**
 * Maps a project status to its badge class.
 *
 * Shared by the list, the detail page and the dashboard so one status never renders in two
 * different colours depending on where you are looking. The classes are defined globally in
 * styles.scss, since badges appear inside dialogs and overlays too.
 */
export function statusBadgeClass(status: ProjectStatus): string {
  return `epm-badge--status-${status.toLowerCase()}`;
}
