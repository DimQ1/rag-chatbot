import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../environments/environment';

export interface RagResponse {
  answer: string;
  sources: { title: string; url: string }[];
}

export interface RagDocumentResponse {
  documentId: string;
  title: string;
  content: string;
  sourceUpdatedAtUtc: string;
}

@Injectable({
  providedIn: 'root',
})
export class RagService {
  private readonly http = inject(HttpClient);

  query(question: string, includeReasoning = false): Observable<RagResponse> {
    return this.http.post<RagResponse>(`${environment.apiUrl}/rag/query`, {
      question,
      includeReasoning,
    });
  }

  getDocument(documentId: string): Observable<RagDocumentResponse> {
    return this.http.get<RagDocumentResponse>(
      `${environment.apiUrl}/rag/documents/${encodeURIComponent(documentId)}`
    );
  }
}
