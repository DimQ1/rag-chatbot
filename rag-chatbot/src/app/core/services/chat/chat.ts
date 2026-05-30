import { Injectable, inject, Injector, signal } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../../environments/environment';

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
  private readonly injector = inject(Injector);
  private readonly apiUrl = `${environment.apiUrl}/chatsession`;

  private readonly messagesState = signal<ChatMessage[]>([]);
  private readonly sessionsState = signal<ChatSession[]>([]);
  private readonly currentSessionIdState = signal<string | null>(null);
  private readonly currentThinkingSessionIdState = signal<string | null>(null);

  readonly messagesSignal = this.messagesState.asReadonly();
  readonly sessionsSignal = this.sessionsState.asReadonly();
  readonly currentSessionIdSignal = this.currentSessionIdState.asReadonly();
  readonly currentThinkingSessionIdSignal = this.currentThinkingSessionIdState.asReadonly();

  readonly messages = toObservable(this.messagesState, { injector: this.injector });
  readonly sessions = toObservable(this.sessionsState, { injector: this.injector });
  readonly currentSessionId = toObservable(this.currentSessionIdState, { injector: this.injector });
  readonly currentThinkingSessionId = toObservable(this.currentThinkingSessionIdState, { injector: this.injector });

  // Get current values
  get currentSessionId$Value(): string | null {
    return this.currentSessionIdState();
  }

  get currentThinkingSessionId$Value(): string | null {
    return this.currentThinkingSessionIdState();
  }

  get messagesList(): ChatMessage[] {
    return this.messagesState();
  }

  get sessionsList(): ChatSession[] {
    return this.sessionsState();
  }

  addMessage(role: MessageRole, content: string, sources?: { title: string; url: string }[]): void {
    const msg: ChatMessage = {
      id: crypto.randomUUID(),
      role,
      content,
      sources,
      timestamp: new Date(),
    };
    this.messagesState.update((messages) => [...messages, msg]);
  }

  clearMessages(): void {
    this.messagesState.set([]);
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

  loadSessions(): Observable<ChatSession[]> {
    return this.getSessions().pipe(tap((sessions) => {
      this.sessionsState.set(sessions);

      const currentSessionId = this.currentSessionIdState();
      if (!currentSessionId) {
        if (sessions.length > 0) {
          this.loadSessionDetail(sessions[0].id);
        }

        return;
      }

      const currentSessionStillExists = sessions.some((session) => session.id === currentSessionId);
      if (!currentSessionStillExists) {
        if (sessions.length > 0) {
          this.loadSessionDetail(sessions[0].id);
        } else {
          this.setCurrentSession(null);
        }
      }
    }));
  }

  getSessionDetail(sessionId: string): Observable<ChatSessionDetail> {
    return this.http.get<ChatSessionDetail>(`${this.apiUrl}/${sessionId}`);
  }

  loadSessionDetail(sessionId: string, options?: { setAsCurrent?: boolean }): void {
    const setAsCurrent = options?.setAsCurrent ?? true;

    this.getSessionDetail(sessionId).subscribe({
      next: (session) => {
        if (setAsCurrent) {
          this.currentSessionIdState.set(sessionId);
        }

        const messages: ChatMessage[] = session.messages.map(m => ({
          id: m.id,
          role: m.role as MessageRole,
          content: m.content,
          sources: m.sources,
          timestamp: new Date(m.createdAtUtc),
        }));

        if (setAsCurrent || this.currentSessionIdState() === sessionId) {
          this.messagesState.set(messages);
        }
      },
      error: (err) => {
        console.error('Failed to load session detail:', err);
      },
    });
  }

  addMessageToSession(sessionId: string, question: string, includeReasoning = false): Observable<any> {
    return this.http.post(
      `${this.apiUrl}/${sessionId}/add-message`,
      { question, includeReasoning }
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
    this.currentSessionIdState.set(sessionId);
    if (!sessionId) {
      this.clearMessages();
    }
  }

  setThinkingSession(sessionId: string | null): void {
    this.currentThinkingSessionIdState.set(sessionId);
  }
}
