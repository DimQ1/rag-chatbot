import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth';

@Component({
  selector: 'app-account',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatToolbarModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './account.html',
  styleUrl: './account.scss',
})
export class Account implements OnInit {
  private readonly fb = inject(FormBuilder);
  readonly authService = inject(AuthService);

  profileSaving = false;
  passwordSaving = false;
  profileMessage = '';
  passwordMessage = '';
  errorMessage = '';

  readonly profileForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.minLength(2)]],
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', [Validators.required]],
    newPassword: ['', [Validators.required, Validators.minLength(6)]],
  });

  ngOnInit(): void {
    const currentName = this.authService.currentUser?.name ?? '';
    this.profileForm.patchValue({ name: currentName });

    this.authService.refreshCurrentUser().subscribe({
      next: (user) => this.profileForm.patchValue({ name: user.name }),
      error: () => {},
    });
  }

  saveProfile(): void {
    if (this.profileForm.invalid) {
      return;
    }

    this.errorMessage = '';
    this.profileMessage = '';
    this.profileSaving = true;

    this.authService.updateProfile(this.profileForm.getRawValue()).subscribe({
      next: () => {
        this.profileSaving = false;
        this.profileMessage = 'Profile updated.';
      },
      error: (err) => {
        this.profileSaving = false;
        this.errorMessage = err?.error?.message ?? 'Failed to update profile.';
      },
    });
  }

  changePassword(): void {
    if (this.passwordForm.invalid) {
      return;
    }

    this.errorMessage = '';
    this.passwordMessage = '';
    this.passwordSaving = true;

    this.authService.changePassword(this.passwordForm.getRawValue()).subscribe({
      next: (message) => {
        this.passwordSaving = false;
        this.passwordMessage = message;
        this.passwordForm.reset();
      },
      error: (err) => {
        this.passwordSaving = false;
        this.errorMessage = err?.error?.message ?? 'Failed to update password.';
      },
    });
  }
}
