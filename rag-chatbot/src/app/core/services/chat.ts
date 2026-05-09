import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable } from 'rxjs';
import { environment } from '../../../environments/environment.development';

export type MessageRole = 'user' | 'assistant';

export interface ChatMessage {
  id: string;
  role: MessageRole;
  content: string;
  sources?: { title: string; url: string }[];
  timestamp: Date;
}

export interface ChatMessageSource {
  title: string;
  url: string;
}

export interface ChatSessionMessageDto {
  id: string;
  role: string;
  content: string;
  sources?: ChatMessageSource[];
  createdAtUtc: string | Date;
}

export interface ChatSession {
  id: string;
  topic: string;
  isPinned: boolean;
  createdAtUtc: Date;
  updatedAtUtc: Date;
  messageCount: number;
}

export interface ChatSessionDetail extends ChatSession {
  messages: ChatSessionMessageDto[];
}

@Injectable({
  providedIn: 'root',
})
export class ChatService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = `${environment.apiUrl}/chatsession`;

  private readonly messages$ = new BehaviorSubject<ChatMessage[]>([]);
  private readonly sessions$ = new BehaviorSubject<ChatSession[]>([]);
  private readonly currentSessionId$ = new BehaviorSubject<string | null>(null);

  readonly messages = this.messages$.asObservable();
  readonly sessions = this.sessions$.asObservable();
  readonly currentSessionId = this.currentSessionId$.asObservable();

  // Get current values
  get currentSessionId$Value(): string | null {
    return this.currentSessionId$.value;
  }

  get messagesList(): ChatMessage[] {
    return this.messages$.value;
  }

  addMessage(role: MessageRole, content: string, sources?: { title: string; url: string }[]): void {
    const msg: ChatMessage = {
      id: crypto.randomUUID(),
      role,
      content,
      sources,
      timestamp: new Date(),
    };
    this.messages$.next([...this.messages$.value, msg]);
  }

  clearMessages(): void {
    this.messages$.next([]);
  }

  // Session management
  createSession(): Observable<ChatSession> {
    return this.http.post<ChatSession>(
      `${this.apiUrl}/create`,
      {}
    );
  }

  getSessions(): Observable<ChatSession[]> {
    return this.http.get<ChatSession[]>(this.apiUrl);
  }

  loadSessions(): void {
    this.getSessions().subscribe({
      next: (sessions) => {
        this.sessions$.next(sessions);
      },
      error: (err) => {
        console.error('Failed to load sessions:', err);
      },
    });
  }

  getSessionDetail(sessionId: string): Observable<ChatSessionDetail> {
    return this.http.get<ChatSessionDetail>(`${this.apiUrl}/${sessionId}`);
  }

  loadSessionDetail(sessionId: string): void {
    this.getSessionDetail(sessionId).subscribe({
      next: (session) => {
        this.currentSessionId$.next(sessionId);
        const messages: ChatMessage[] = session.messages.map(m => ({
          id: m.id,
          role: m.role as MessageRole,
          content: m.content,
          sources: m.sources,
          timestamp: new Date(m.createdAtUtc),
        }));
        this.messages$.next(messages);
      },
      error: (err) => {
        console.error('Failed to load session detail:', err);
      },
    });
  }

  addMessageToSession(sessionId: string, question: string): Observable<any> {
    return this.http.post(
      `${this.apiUrl}/${sessionId}/add-message`,
      { question }
    );
  }

  renameSession(sessionId: string, topic: string): Observable<any> {
    return this.http.patch(
      `${this.apiUrl}/${sessionId}/rename`,
      { topic }
    );
  }

  pinSession(sessionId: string, isPinned: boolean): Observable<any> {
    return this.http.patch(
      `${this.apiUrl}/${sessionId}/pin`,
      { isPinned }
    );
  }

  deleteSession(sessionId: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${sessionId}`);
  }

  setCurrentSession(sessionId: string | null): void {
    this.currentSessionId$.next(sessionId);
    if (!sessionId) {
      this.clearMessages();
    }
  }
}
