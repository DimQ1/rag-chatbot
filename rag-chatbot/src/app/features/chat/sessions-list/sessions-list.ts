import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { MatDividerModule } from '@angular/material/divider';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ChatService, ChatSession } from '../../../core/services/chat';
import { RenameSessionDialogComponent } from './rename-session-dialog/rename-session-dialog.component';

@Component({
  selector: 'app-sessions-list',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatMenuModule,
    MatDividerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './sessions-list.html',
  styleUrl: './sessions-list.scss',
})
export class SessionsListComponent implements OnInit {
  private readonly chatService = inject(ChatService);
  private readonly dialog = inject(MatDialog);

  readonly sessions = this.chatService.sessions;
  readonly currentSessionId = this.chatService.currentSessionId;
  readonly currentThinkingSessionId = this.chatService.currentThinkingSessionId;
  readonly loading = signal(false);

  ngOnInit(): void {
    this.loadSessions();
  }

  loadSessions(): void {
    this.loading.set(true);
    this.chatService.loadSessions();
    // Simulate loading completion after a short delay
    setTimeout(() => this.loading.set(false), 500);
  }

  createNewSession(): void {
    this.chatService.createSession().subscribe({
      next: (session) => {
        this.chatService.setCurrentSession(session.id);
        this.chatService.clearMessages();
        this.loadSessions();
      },
      error: (err) => {
        console.error('Failed to create session:', err);
      },
    });
  }

  selectSession(sessionId: string): void {
    this.chatService.setCurrentSession(sessionId);
    this.chatService.loadSessionDetail(sessionId);
  }

  togglePin(event: Event, sessionId: string, currentPinned: boolean): void {
    event.stopPropagation();
    this.chatService.pinSession(sessionId, !currentPinned).subscribe({
      next: () => {
        this.loadSessions();
      },
      error: (err) => {
        console.error('Failed to pin session:', err);
      },
    });
  }

  openRenameDialog(event: Event, session: ChatSession): void {
    event.stopPropagation();
    const dialogRef = this.dialog.open(RenameSessionDialogComponent, {
      data: { topic: session.topic },
      width: '400px',
    });

    dialogRef.afterClosed().subscribe((result) => {
      if (result) {
        this.chatService.renameSession(session.id, result).subscribe({
          next: () => {
            this.loadSessions();
          },
          error: (err) => {
            console.error('Failed to rename session:', err);
          },
        });
      }
    });
  }

  deleteSession(event: Event, sessionId: string): void {
    event.stopPropagation();
    if (confirm('Are you sure you want to delete this session?')) {
      this.chatService.deleteSession(sessionId).subscribe({
        next: () => {
          this.loadSessions();
          this.chatService.setCurrentSession(null);
        },
        error: (err) => {
          console.error('Failed to delete session:', err);
        },
      });
    }
  }
}
