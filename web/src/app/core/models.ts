/**
 * The API contract, as the Angular app sees it. These mirror the records in
 * `RequestDesk.Application/Contracts/Contracts.cs`. Nullable server fields are omitted from the JSON
 * when null, so they are optional here.
 */

export type RequestStatus =
  'New' | 'Triaged' | 'InProgress' | 'Blocked' | 'Resolved' | 'Closed' | 'Cancelled';

export type RequestPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export type UserRole = 'Admin' | 'Agent' | 'Customer';

export interface UserSummary {
  id: string;
  displayName: string;
  role: UserRole;
}

export interface CustomerSummary {
  id: string;
  name: string;
  organization?: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface RequestListItem {
  id: string;
  referenceNumber: string;
  title: string;
  priority: RequestPriority;
  status: RequestStatus;
  customerId: string;
  customerName: string;
  assignedAgentId?: string;
  assignedAgentName?: string;
  commentCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface RequestPermissions {
  canComment: boolean;
  canAttach: boolean;
  canChangeAssignment: boolean;
}

export interface RequestComment {
  id: string;
  body: string;
  author: UserSummary;
  createdAt: string;
}

export interface RequestAttachment {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedBy: UserSummary;
  createdAt: string;
}

export interface HistoryEntry {
  id: string;
  fromStatus?: RequestStatus;
  toStatus: RequestStatus;
  actor: UserSummary;
  reason?: string;
  occurredAt: string;
}

export interface RequestDetail {
  id: string;
  referenceNumber: string;
  title: string;
  description: string;
  priority: RequestPriority;
  status: RequestStatus;
  customer: CustomerSummary;
  assignedAgent?: UserSummary;
  createdBy: UserSummary;
  createdAt: string;
  updatedAt: string;
  /** The legal next statuses for the signed-in user. The UI renders exactly these and nothing else. */
  allowedTransitions: RequestStatus[];
  permissions: RequestPermissions;
  comments: RequestComment[];
  attachments: RequestAttachment[];
  history: HistoryEntry[];
}

export interface StatusChangeResult {
  id: string;
  status: RequestStatus;
  entry: HistoryEntry;
  allowedTransitions: RequestStatus[];
  updatedAt: string;
}

export interface AuthResult {
  accessToken: string;
  accessTokenExpiresAt: string;
  refreshToken: string;
  refreshTokenExpiresAt: string;
  user: UserSummary;
  customerId?: string;
}

export interface StatusCount {
  status: RequestStatus;
  count: number;
}

export interface PriorityCount {
  priority: RequestPriority;
  count: number;
}

export interface AgingBucket {
  label: string;
  minDays: number;
  maxDays?: number;
  count: number;
}

export interface SummaryReport {
  openCount: number;
  inProgressCount: number;
  blockedCount: number;
  resolvedThisWeek: number;
  createdThisWeek: number;
  medianOpenAgeHours?: number;
  byStatus: StatusCount[];
  openByPriority: PriorityCount[];
  aging: AgingBucket[];
  generatedAt: string;
}

export interface DemoAccount {
  role: UserRole;
  email: string;
  password: string;
}

export interface DemoInfo {
  enabled: boolean;
  resetIntervalMinutes: number;
  nextResetAt?: string;
  accounts: DemoAccount[];
}

export interface RequestListParams {
  page: number;
  pageSize: number;
  status: RequestStatus[];
  priority: RequestPriority[];
  assignedAgentId?: string;
  unassigned: boolean;
  search: string;
  sortBy: string;
  sortDescending: boolean;
}

/** RFC 9457 problem details, plus the extensions this API adds. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
  from?: RequestStatus;
  to?: RequestStatus;
  legalTransitions?: RequestStatus[];
}
