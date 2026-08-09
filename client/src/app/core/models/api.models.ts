/**
 * Mirrors of the API's response contracts.
 *
 * Hand-written rather than generated from the OpenAPI document. At this size a generator is
 * more machinery than it saves, and these types double as documentation of what the frontend
 * actually consumes. If the API grows, `nswag`/`openapi-generator` pointed at
 * /swagger/v1/swagger.json is the natural upgrade.
 */

/** The envelope every endpoint returns, success or failure. */
export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
  /** Stable machine-readable code, e.g. "Employee.EmailExists". */
  code?: string;
  /** Field-keyed validation messages, e.g. { email: ['Email must be valid.'] }. */
  errors?: Record<string, string[]>;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export type ProjectStatus = 'Planning' | 'Active' | 'Completed' | 'Cancelled';

export const PROJECT_STATUSES: readonly ProjectStatus[] = [
  'Planning',
  'Active',
  'Completed',
  'Cancelled',
] as const;

export type UserRole = 'Admin' | 'Manager' | 'User';

export interface Employee {
  id: number;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string;
  phone: string | null;
  jobTitle: string;
  departmentId: number;
  departmentName: string;
  /** ISO date (yyyy-MM-dd) — the API uses DateOnly, so there is no time component. */
  hireDate: string;
  isActive: boolean;
}

export interface EmployeeDetail {
  employee: Employee;
  assignments: EmployeeAssignment[];
}

export interface EmployeeAssignment {
  projectId: number;
  projectName: string;
  projectStatus: ProjectStatus;
  role: string;
  assignedDate: string;
  allocationPercentage: number;
}

export interface SaveEmployeeRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone: string | null;
  jobTitle: string;
  departmentId: number;
  hireDate: string;
}

export interface Department {
  id: number;
  name: string;
  description: string | null;
  employeeCount: number;
  activeEmployeeCount: number;
}

export interface SaveDepartmentRequest {
  name: string;
  description: string | null;
}

export interface Project {
  id: number;
  name: string;
  description: string | null;
  startDate: string;
  endDate: string | null;
  status: ProjectStatus;
  assignedEmployeeCount: number;
}

export interface SaveProjectRequest {
  name: string;
  description: string | null;
  startDate: string;
  endDate: string | null;
  status: ProjectStatus;
}

export interface ProjectAssignment {
  id: number;
  employeeId: number;
  employeeName: string;
  employeeEmail: string;
  departmentName: string;
  employeeIsActive: boolean;
  role: string;
  assignedDate: string;
  allocationPercentage: number;
}

export interface AssignEmployeeRequest {
  employeeId: number;
  role: string;
  assignedDate: string;
  allocationPercentage: number;
}

export interface MyAssignment {
  projectId: number;
  projectName: string;
  projectDescription: string | null;
  projectStatus: ProjectStatus;
  projectStartDate: string;
  projectEndDate: string | null;
  role: string;
  assignedDate: string;
  allocationPercentage: number;
}

export interface Dashboard {
  totalEmployees: number;
  activeEmployees: number;
  inactiveEmployees: number;
  totalDepartments: number;
  totalProjects: number;
  activeProjects: number;
  employeesByDepartment: DepartmentHeadcount[];
  projectsByStatus: ProjectStatusCount[];
}

export interface DepartmentHeadcount {
  departmentId: number;
  departmentName: string;
  employeeCount: number;
  activeEmployeeCount: number;
}

export interface ProjectStatusCount {
  status: ProjectStatus;
  count: number;
}

export interface AuthenticatedUser {
  id: number;
  email: string;
  displayName: string;
  role: UserRole;
  employeeId: number | null;
}

/**
 * What the API reports about itself at `GET /api/auth/sso`.
 *
 * Read at runtime rather than baked into the bundle, so the same build serves a deployment
 * with SSO on and one with it off, and no tenant id is compiled in.
 */
export interface SsoConfiguration {
  enabled: boolean;
  authority: string | null;
  clientId: string | null;
  apiScope: string | null;
}

/** `GET /api/auth/me` — the server's account of who the caller is. */
export interface CurrentUser {
  id: number;
  email: string;
  displayName: string;
  role: UserRole;
  employeeId: number | null;
  isExternalIdentity: boolean;
}

export interface AuthenticationResponse {
  accessToken: string;
  expiresAtUtc: string;
  refreshToken: string;
  user: AuthenticatedUser;
}

/** Query parameters shared by every paged list endpoint. */
export interface PagingQuery {
  page: number;
  pageSize: number;
  search?: string | null;
  sortBy?: string | null;
  sortDescending?: boolean;
}

export interface EmployeeQuery extends PagingQuery {
  departmentId?: number | null;
  isActive?: boolean | null;
}

export interface ProjectQuery extends PagingQuery {
  status?: ProjectStatus | null;
  employeeId?: number | null;
}
