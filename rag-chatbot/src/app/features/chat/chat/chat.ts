import { Component, inject, signal, ElementRef, ViewChild, AfterViewChecked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormControl, Validators } from '@angular/forms';
import { CdkTextareaAutosize } from '@angular/cdk/text-field';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { RagService } from '../../../core/services/rag';
import { ChatService, ChatMessage } from '../../../core/services/chat';
import { AuthService } from '../../../core/services/auth';
import { SessionsListComponent } from '../sessions-list/sessions-list';

@Component({
  selector: 'app-chat',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CdkTextareaAutosize,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatToolbarModule,
    MatTooltipModule,
    MatMenuModule,
    RouterLink,
    SessionsListComponent,
  ],
  templateUrl: './chat.html',
  styleUrl: './chat.scss',
})
export class Chat implements AfterViewChecked {
  private readonly ragService = inject(RagService);
  private readonly chatService = inject(ChatService);
  readonly authService = inject(AuthService);

  @ViewChild('messagesEnd') private messagesEnd!: ElementRef;

  readonly messages$ = this.chatService.messages;
  readonly currentSessionId = this.chatService.currentSessionId;
  readonly inputControl = new FormControl('', [
    Validators.required,
    Validators.maxLength(2000),
  ]);

  thinking = signal(false);

  private readonly questionHistory: string[] = [];
  private historyPosition = -1;
  private pendingDraft = '';

  get userInitial(): string {
    const name = this.authService.currentUser?.name ?? '';
    return name.charAt(0).toUpperCase() || 'U';
  }

  get userName(): string {
    return this.authService.currentUser?.name ?? 'User';
  }

  get isAdmin(): boolean {
    return this.authService.isAdmin();
  }

  createNewChat(): void {
    this.chatService.createSession().subscribe({
      next: (session) => {
        this.chatService.setCurrentSession(session.id);
        this.chatService.clearMessages();
      },
      error: (err) => {
        console.error('Failed to create session:', err);
      },
    });
  }

  clearChat(): void {
    this.chatService.clearMessages();
  }

  ngAfterViewChecked(): void {
    this.scrollToBottom();
  }

  send(): void {
    const question = this.inputControl.value?.trim();
    if (!question || this.inputControl.invalid || this.thinking()) return;

    const sessionId = this.chatService.currentSessionId$Value;
    if (!sessionId) {
      // Create a new session if one doesn't exist
      this.chatService.createSession(question).subscribe({
        next: (session) => {
          this.chatService.setCurrentSession(session.id);
          this.sendMessage(session.id, question);
        },
        error: (err) => {
          console.error('Failed to create session:', err);
        },
      });
    } else {
      this.sendMessage(sessionId, question);
    }
  }

  private sendMessage(sessionId: string, question: string): void {
    this.addToHistory(question);
    this.resetHistoryNavigation();

    this.chatService.addMessage('user', question);
    this.inputControl.reset();
    this.thinking.set(true);

    this.chatService.addMessageToSession(sessionId, question).subscribe({
      next: () => {
        // Reload the session to get the updated messages
        this.chatService.loadSessionDetail(sessionId);
        this.thinking.set(false);
      },
      error: (err) => {
        console.error('Failed to send message:', err);
        this.chatService.addMessage('assistant', 'Sorry, I encountered an error. Please try again.');
        this.thinking.set(false);
      },
    });
  }

  onInputKeydown(event: KeyboardEvent): void {
    if (event.key === 'ArrowUp' && this.isCursorAtStart(event)) {
      event.preventDefault();
      this.navigateHistoryUp();
      return;
    }

    if (event.key === 'ArrowDown' && this.isCursorAtEnd(event)) {
      event.preventDefault();
      this.navigateHistoryDown();
      return;
    }

    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
      return;
    }

    if (this.historyPosition !== -1) {
      this.resetHistoryNavigation();
    }
  }

  trackById(_: number, msg: ChatMessage): string {
    return msg.id;
  }

  private scrollToBottom(): void {
    try {
      this.messagesEnd?.nativeElement.scrollIntoView({ behavior: 'smooth' });
    } catch {}
  }

  private navigateHistoryUp(): void {
    this.addCurrentDraftToHistory();
    if (this.questionHistory.length === 0) return;

    if (this.historyPosition === -1) {
      this.pendingDraft = this.inputControl.value ?? '';
    }

    this.historyPosition = Math.min(this.historyPosition + 1, this.questionHistory.length - 1);
    this.restoreHistoryEntry(this.historyPosition);
  }

  private navigateHistoryDown(): void {
    if (this.historyPosition === -1) return;

    this.historyPosition -= 1;
    if (this.historyPosition === -1) {
      this.inputControl.setValue(this.pendingDraft);
      return;
    }

    this.restoreHistoryEntry(this.historyPosition);
  }

  private restoreHistoryEntry(position: number): void {
    const reverseIndex = this.questionHistory.length - 1 - position;
    this.inputControl.setValue(this.questionHistory[reverseIndex]);
  }

  private addCurrentDraftToHistory(): void {
    const currentDraft = this.inputControl.value?.trim();
    if (!currentDraft) return;
    this.addToHistory(currentDraft);
  }

  private addToHistory(value: string): void {
    if (this.questionHistory[this.questionHistory.length - 1] === value) {
      return;
    }

    this.questionHistory.push(value);
  }

  private resetHistoryNavigation(): void {
    this.historyPosition = -1;
    this.pendingDraft = '';
  }

  private isCursorAtStart(event: KeyboardEvent): boolean {
    const target = event.target as HTMLTextAreaElement | null;
    if (!target) return false;
    return target.selectionStart === 0 && target.selectionEnd === 0;
  }

  private isCursorAtEnd(event: KeyboardEvent): boolean {
    const target = event.target as HTMLTextAreaElement | null;
    if (!target) return false;

    const length = target.value.length;
    return target.selectionStart === length && target.selectionEnd === length;
  }
}
