import { Component, inject, signal, ElementRef, ViewChild, HostListener, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { distinctUntilChanged } from 'rxjs';
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
import { ChatService, ChatMessage } from '../../../core/services/chat';
import { AuthService } from '../../../core/services/auth';
import { MarkdownContentDirective } from '../../../shared/directives/markdown-content';
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
    MarkdownContentDirective,
    RouterLink,
    SessionsListComponent,
  ],
  templateUrl: './chat.html',
  styleUrl: './chat.scss',
})
export class Chat {
  private readonly chatService = inject(ChatService);
  readonly authService = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly includeReasoningStorageKey = 'chat_include_reasoning';

  @ViewChild('messagesEnd') private messagesEnd!: ElementRef;

  readonly messages$ = this.chatService.messages;
  readonly currentSessionId = this.chatService.currentSessionId;
  readonly inputControl = new FormControl('', [
    Validators.required,
    Validators.maxLength(2000),
  ]);

  readonly sessionsPanelOpen = signal(true);
  readonly sidebarWidth = signal(320);
  readonly includeReasoning = signal(this.loadIncludeReasoningPreference());
  readonly isSending = signal(false);

  private readonly sidebarMinWidth = 260;
  private readonly sidebarMaxWidth = 520;
  private isResizingSidebar = false;

  private readonly questionHistory: string[] = [];
  private historyPosition = -1;
  private pendingDraft = '';
  private shouldScrollAfterSessionLoad = false;

  constructor() {
    this.currentSessionId
      .pipe(distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((sessionId) => {
        this.shouldScrollAfterSessionLoad = Boolean(sessionId);
      });

    this.messages$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (!this.shouldScrollAfterSessionLoad) {
          return;
        }

        this.shouldScrollAfterSessionLoad = false;
        requestAnimationFrame(() => this.scrollToBottom());
      });
  }

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

  toggleSessionsPanel(): void {
    this.sessionsPanelOpen.update((isOpen) => !isOpen);
  }

  toggleIncludeReasoning(): void {
    const next = !this.includeReasoning();
    this.includeReasoning.set(next);
    localStorage.setItem(this.includeReasoningStorageKey, String(next));
  }

  startSidebarResize(event: MouseEvent): void {
    if (!this.sessionsPanelOpen()) {
      return;
    }

    event.preventDefault();
    this.isResizingSidebar = true;
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
  }

  @HostListener('window:mousemove', ['$event'])
  onWindowMouseMove(event: MouseEvent): void {
    if (!this.isResizingSidebar) {
      return;
    }

    const viewportLimitedMax = Math.floor(window.innerWidth * 0.6);
    const effectiveMax = Math.max(this.sidebarMinWidth, Math.min(this.sidebarMaxWidth, viewportLimitedMax));
    const nextWidth = Math.max(this.sidebarMinWidth, Math.min(effectiveMax, Math.floor(event.clientX)));
    this.sidebarWidth.set(nextWidth);
  }

  @HostListener('window:mouseup')
  stopSidebarResize(): void {
    if (!this.isResizingSidebar) {
      return;
    }

    this.isResizingSidebar = false;
    document.body.style.cursor = '';
    document.body.style.userSelect = '';
  }

  createNewChat(): void {
    this.chatService.createSession().subscribe({
      next: (session) => {
        this.chatService.setCurrentSession(session.id);
        this.chatService.clearMessages();
        this.chatService.loadSessions();
      },
      error: (err) => {
        console.error('Failed to create session:', err);
      },
    });
  }

  clearChat(): void {
    this.chatService.clearMessages();
  }

  send(): void {
    const question = this.inputControl.value?.trim();
    if (!question || this.inputControl.invalid || this.isSending() || this.isCurrentSessionThinking()) return;

    this.isSending.set(true);

    const sessionId = this.chatService.currentSessionId$Value;
    if (!sessionId) {
      const fallbackSession = this.chatService.sessionsList[0];
      if (fallbackSession) {
        this.chatService.setCurrentSession(fallbackSession.id);
        this.sendMessage(fallbackSession.id, question);
        return;
      }

      // Create a new session if one doesn't exist
      this.chatService.createSession().subscribe({
        next: (session) => {
          this.chatService.setCurrentSession(session.id);
          this.sendMessage(session.id, question);
        },
        error: (err) => {
          console.error('Failed to create session:', err);
          this.isSending.set(false);
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
    this.chatService.setThinkingSession(sessionId);

    this.chatService.addMessageToSession(sessionId, question, this.includeReasoning()).subscribe({
      next: () => {
        // Reload the session to get the updated messages
        this.chatService.loadSessionDetail(sessionId, { setAsCurrent: false });
        this.chatService.loadSessions();
        this.chatService.setThinkingSession(null);
        this.isSending.set(false);
      },
      error: (err) => {
        console.error('Failed to send message:', err);
        this.chatService.addMessage('assistant', 'Sorry, I encountered an error. Please try again.');
        this.chatService.setThinkingSession(null);
        this.isSending.set(false);
      },
    });
  }

  isCurrentSessionThinking(): boolean {
    const currentSessionId = this.chatService.currentSessionId$Value;
    const currentThinkingSessionId = this.chatService.currentThinkingSessionId$Value;

    return Boolean(currentSessionId) && currentSessionId === currentThinkingSessionId;
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

  resolveSourceUrl(url: string): string {
    const legacyPrefix = 'local://knowledge/';
    if (!url.toLowerCase().startsWith(legacyPrefix)) {
      return url;
    }

    const encodedFileName = url.slice(legacyPrefix.length).split(/[?#]/, 1)[0];
    const decodedFileName = decodeURIComponent(encodedFileName);
    const fileName = decodedFileName.includes('/')
      ? decodedFileName.substring(decodedFileName.lastIndexOf('/') + 1)
      : decodedFileName;
    const fileStem = fileName.endsWith('.md') ? fileName.slice(0, -3) : fileName;

    const normalizedId = fileStem
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/-+/g, '-')
      .replace(/^-|-$/g, '');

    return normalizedId ? `/document/${encodeURIComponent(normalizedId)}` : url;
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

  private loadIncludeReasoningPreference(): boolean {
    const saved = localStorage.getItem(this.includeReasoningStorageKey);
    return saved === 'true';
  }
}
