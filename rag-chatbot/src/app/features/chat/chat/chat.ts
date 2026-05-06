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
import { RagService } from '../../../core/services/rag';
import { ChatService, ChatMessage } from '../../../core/services/chat';
import { AuthService } from '../../../core/services/auth';

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
    RouterLink,
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
  readonly inputControl = new FormControl('', [
    Validators.required,
    Validators.maxLength(2000),
  ]);

  thinking = signal(false);

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

  clearChat(): void {
    this.chatService.clearMessages();
  }

  ngAfterViewChecked(): void {
    this.scrollToBottom();
  }

  send(): void {
    const question = this.inputControl.value?.trim();
    if (!question || this.inputControl.invalid || this.thinking()) return;

    this.chatService.addMessage('user', question);
    this.inputControl.reset();
    this.thinking.set(true);

    this.ragService.query(question).subscribe({
      next: (res) => {
        this.chatService.addMessage('assistant', res.answer, res.sources);
        this.thinking.set(false);
      },
      error: () => {
        this.chatService.addMessage('assistant', 'Sorry, I encountered an error. Please try again.');
        this.thinking.set(false);
      },
    });
  }

  onEnter(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
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
}
