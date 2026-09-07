import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  CustomerSummary,
  DemoInfo,
  PagedResult,
  RequestAttachment,
  RequestComment,
  RequestDetail,
  RequestListItem,
  RequestListParams,
  RequestPriority,
  RequestStatus,
  StatusChangeResult,
  SummaryReport,
  UserSummary,
} from './models';

/** Every call the app makes, typed, in one place. Paths are relative: the API is same-origin behind a proxy. */
@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  demo(): Observable<DemoInfo> {
    return this.http.get<DemoInfo>('/api/demo');
  }

  listRequests(params: RequestListParams): Observable<PagedResult<RequestListItem>> {
    let query = new HttpParams()
      .set('page', params.page)
      .set('pageSize', params.pageSize)
      .set('sortBy', params.sortBy)
      .set('sortDescending', params.sortDescending);

    for (const status of params.status) query = query.append('status', status);
    for (const priority of params.priority) query = query.append('priority', priority);
    if (params.assignedAgentId) query = query.set('assignedAgentId', params.assignedAgentId);
    if (params.unassigned) query = query.set('unassigned', true);
    if (params.search.trim()) query = query.set('search', params.search.trim());

    return this.http.get<PagedResult<RequestListItem>>('/api/requests', { params: query });
  }

  getRequest(id: string): Observable<RequestDetail> {
    return this.http.get<RequestDetail>(`/api/requests/${id}`);
  }

  createRequest(body: {
    title: string;
    description: string;
    priority: RequestPriority;
    customerId?: string;
  }): Observable<RequestDetail> {
    return this.http.post<RequestDetail>('/api/requests', body);
  }

  changeStatus(id: string, to: RequestStatus, reason?: string): Observable<StatusChangeResult> {
    return this.http.patch<StatusChangeResult>(`/api/requests/${id}/status`, {
      to,
      reason: reason?.trim() || undefined,
    });
  }

  assign(id: string, agentId: string | null): Observable<RequestDetail> {
    return this.http.patch<RequestDetail>(`/api/requests/${id}/assignment`, { agentId });
  }

  addComment(id: string, body: string): Observable<RequestComment> {
    return this.http.post<RequestComment>(`/api/requests/${id}/comments`, { body });
  }

  uploadAttachment(id: string, file: File): Observable<RequestAttachment> {
    const form = new FormData();
    form.append('file', file, file.name);
    return this.http.post<RequestAttachment>(`/api/requests/${id}/attachments`, form);
  }

  /** Attachments are served through the API with the bearer token, never from a public path. */
  downloadAttachment(id: string, attachmentId: string): Observable<Blob> {
    return this.http.get(`/api/requests/${id}/attachments/${attachmentId}`, {
      responseType: 'blob',
    });
  }

  listStaff(): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>('/api/staff');
  }

  listCustomers(): Observable<CustomerSummary[]> {
    return this.http.get<CustomerSummary[]>('/api/customers');
  }

  summary(): Observable<SummaryReport> {
    return this.http.get<SummaryReport>('/api/reports/summary');
  }
}
