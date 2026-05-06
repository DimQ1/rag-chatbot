import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface RagResponse {
  answer: string;
  sources: { title: string; url: string }[];
}

@Injectable({
  providedIn: 'root',
})
export class RagService {
  private readonly http = inject(HttpClient);

  query(question: string): Observable<RagResponse> {
    return this.http.post<RagResponse>(`${environment.apiUrl}/rag/query`, {
      question,
    });
  }
}
