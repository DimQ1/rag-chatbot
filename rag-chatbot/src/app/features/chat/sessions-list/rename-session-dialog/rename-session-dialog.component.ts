import { Component, Inject } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-rename-session-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Rename Chat Session</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full-width">
        <mat-label>Session Name</mat-label>
        <input
          matInput
          [formControl]="topicControl"
          placeholder="Enter new session name"
          (keyup.enter)="confirm()"
        />
        <mat-error *ngIf="topicControl.hasError('required')">
          Session name is required
        </mat-error>
        <mat-error *ngIf="topicControl.hasError('maxlength')">
          Maximum 200 characters
        </mat-error>
      </mat-form-field>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button matDialogClose>Cancel</button>
      <button
        mat-raised-button
        color="primary"
        [disabled]="!topicControl.valid"
        (click)="confirm()"
      >
        Save
      </button>
    </mat-dialog-actions>
  `,
  styles: [
    `
      .full-width {
        width: 100%;
      }

      mat-dialog-content {
        padding: 16px;
      }

      mat-dialog-actions {
        padding: 16px;
      }
    `,
  ],
})
export class RenameSessionDialogComponent {
  readonly topicControl = new FormControl('', [
    Validators.required,
    Validators.maxLength(200),
  ]);

  constructor(
    public dialogRef: MatDialogRef<RenameSessionDialogComponent>,
    @Inject(MAT_DIALOG_DATA) data: { topic: string }
  ) {
    this.topicControl.setValue(data.topic);
  }

  confirm(): void {
    if (this.topicControl.valid) {
      this.dialogRef.close(this.topicControl.value);
    }
  }
}
