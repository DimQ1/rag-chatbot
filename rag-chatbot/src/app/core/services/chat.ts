import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';

export type MessageRole = 'user' | 'assistant';

export interface ChatMessage {
  id: string;
  role: MessageRole;
  content: string;
  sources?: { title: string; url: string }[];
  timestamp: Date;
}

@Injectable({
  providedIn: 'root',
})
export class ChatService {
  private readonly messages$ = new BehaviorSubject<ChatMessage[]>([]);

  readonly messages = this.messages$.asObservable();

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
}
