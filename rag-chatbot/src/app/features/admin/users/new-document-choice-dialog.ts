import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export type NewDocumentChoice = 'form' | 'file';

@Component({
  selector: 'app-new-document-choice-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title class="dialog-title">
      <mat-icon aria-hidden="true">note_add</mat-icon>
      <span>Create New Document</span>
    </h2>

    <mat-dialog-content class="dialog-content">
      <p class="dialog-intro">Choose how you want to prepare the document source.</p>
      <p class="dialog-subtitle">You can type manually or import plain text/markdown and auto-fill the form.</p>
    </mat-dialog-content>

    <mat-dialog-actions align="end" class="dialog-actions">
      <button mat-stroked-button type="button" (click)="close('form')">
        Start from form
      </button>
      <button mat-flat-button color="primary" type="button" (click)="close('file')">
        Upload text file
      </button>
      <button mat-button type="button" class="cancel-button" (click)="cancel()">Cancel</button>
    </mat-dialog-actions>
  `,
  styleUrl: './new-document-choice-dialog.scss',
})
export class NewDocumentChoiceDialog {
  constructor(private readonly dialogRef: MatDialogRef<NewDocumentChoiceDialog, NewDocumentChoice>) {}

  close(choice: NewDocumentChoice): void {
    this.dialogRef.close(choice);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
