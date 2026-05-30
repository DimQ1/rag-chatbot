import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface AdminUser {
  id: string;
  name: string;
  email: string;
  role: 'User' | 'Admin';
  hasPassword: boolean;
  createdAtUtc: string;
}

export interface AdminUpdateUserPayload {
  name: string;
  role: 'User' | 'Admin';
  newPassword?: string;
}

export interface AdminDocument {
  id: string;
  title: string;
  fileName: string;
  content: string;
  updatedAtUtc: string;
}

export interface AdminUpsertDocumentPayload {
  title: string;
  content: string;
}

export interface AdminRagConfiguration {
  openAIBaseUrl: string;
  modelId: string;
  embeddingModelId: string;
  openAIApiKey: string;
  topK: number;
  updatedAtUtc: string;
}

export interface AdminUpdateRagConfigurationPayload {
  openAIBaseUrl: string;
  modelId: string;
  embeddingModelId: string;
  openAIApiKey: string;
  topK: number;
}

export interface AdminLogEntry {
  id: number;
  timestampUtc: string;
  level: string;
  category: string;
  message: string;
  exception?: string | null;
  eventId: number;
  eventName?: string | null;
  traceId?: string | null;
  requestPath?: string | null;
  requestMethod?: string | null;
  userId?: string | null;
}

export interface AdminLogQueryResponse {
  items: AdminLogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AdminLogQueryParams {
  search?: string;
  level?: string;
  page?: number;
  pageSize?: number;
}

@Injectable({
  providedIn: 'root',
})
export class AdminService {
  private readonly http = inject(HttpClient);

  getUsers(): Observable<AdminUser[]> {
    return this.http.get<AdminUser[]>(`${environment.apiUrl}/admin/users`);
  }

  updateUser(id: string, payload: AdminUpdateUserPayload): Observable<AdminUser> {
    return this.http.put<AdminUser>(`${environment.apiUrl}/admin/users/${id}`, payload);
  }

  deleteUser(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${environment.apiUrl}/admin/users/${id}`);
  }

  getDocuments(): Observable<AdminDocument[]> {
    return this.http.get<AdminDocument[]>(`${environment.apiUrl}/admin/documents`);
  }

  createDocument(payload: AdminUpsertDocumentPayload): Observable<AdminDocument> {
    return this.http.post<AdminDocument>(`${environment.apiUrl}/admin/documents`, payload);
  }

  updateDocument(id: string, payload: AdminUpsertDocumentPayload): Observable<AdminDocument> {
    return this.http.put<AdminDocument>(`${environment.apiUrl}/admin/documents/${id}`, payload);
  }

  deleteDocument(id: string): Observable<{ message: string }> {
    return this.http.delete<{ message: string }>(`${environment.apiUrl}/admin/documents/${id}`);
  }

  reprocessAllDocuments(): Observable<{ message: string; processedCount: number; removedCount: number; refreshedAtUtc: string }> {
    return this.http.post<{ message: string; processedCount: number; removedCount: number; refreshedAtUtc: string }>(
      `${environment.apiUrl}/admin/documents/reprocess`,
      {}
    );
  }

  reprocessDocument(id: string): Observable<{ message: string; documentId: string; refreshedAtUtc: string }> {
    return this.http.post<{ message: string; documentId: string; refreshedAtUtc: string }>(
      `${environment.apiUrl}/admin/documents/${id}/reprocess`,
      {}
    );
  }

  getRagConfiguration(): Observable<AdminRagConfiguration> {
    return this.http.get<AdminRagConfiguration>(`${environment.apiUrl}/admin/rag-configuration`);
  }

  updateRagConfiguration(payload: AdminUpdateRagConfigurationPayload): Observable<AdminRagConfiguration> {
    return this.http.put<AdminRagConfiguration>(`${environment.apiUrl}/admin/rag-configuration`, payload);
  }

  getLogs(params: AdminLogQueryParams): Observable<AdminLogQueryResponse> {
    let httpParams = new HttpParams();

    if (params.search) {
      httpParams = httpParams.set('search', params.search);
    }

    if (params.level) {
      httpParams = httpParams.set('level', params.level);
    }

    if (params.page) {
      httpParams = httpParams.set('page', params.page);
    }

    if (params.pageSize) {
      httpParams = httpParams.set('pageSize', params.pageSize);
    }

    return this.http.get<AdminLogQueryResponse>(`${environment.apiUrl}/admin/logs`, {
      params: httpParams,
    });
  }
}
