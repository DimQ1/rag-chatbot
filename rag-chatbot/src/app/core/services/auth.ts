import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Observable, map, tap } from 'rxjs';
import { SocialAuthService, SocialUser } from '@abacritt/angularx-social-login';
import { environment } from '../../../environments/environment';

export interface AuthUser {
  id: string;
  email: string;
  name: string;
  token: string;
  role?: string;
}

export interface RegisterPayload {
  name: string;
  email: string;
  password: string;
}

export interface UpdateProfilePayload {
  name: string;
}

export interface ChangePasswordPayload {
  currentPassword: string;
  newPassword: string;
}

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly socialAuth = inject(SocialAuthService);

  private readonly TOKEN_KEY = 'auth_token';
  private readonly currentUser$ = new BehaviorSubject<AuthUser | null>(null);
  private readonly googleAuthError$ = new BehaviorSubject<string>('');

  readonly user$ = this.currentUser$.asObservable();
  readonly googleAuthError = this.googleAuthError$.asObservable();

  get currentUser() {
    return this.currentUser$.value;
  }

  constructor() {
    const stored = localStorage.getItem(this.TOKEN_KEY);
    if (stored) {
      const user: AuthUser = JSON.parse(stored);
      this.currentUser$.next(user);
    }

    // Exchange Google ID token for backend JWT.
    this.socialAuth.authState.subscribe((socialUser: SocialUser) => {
      if (socialUser?.idToken) {
        this.handleGoogleUser(socialUser);
      }
    });
  }

  register(payload: RegisterPayload): Observable<AuthUser> {
    return this.http
      .post<AuthUser>(`${environment.apiUrl}/auth/register`, payload)
      .pipe(tap((user) => {
        this.persist(user);
        this.router.navigate(['/chat']);
      }));
  }

  login(email: string, password: string): Observable<AuthUser> {
    return this.http
      .post<AuthUser>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(tap((user) => {
        this.persist(user);
        this.router.navigate(['/chat']);
      }));
  }

  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    this.currentUser$.next(null);
    this.socialAuth.signOut().catch(() => {});
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.currentUser$.value?.token ?? null;
  }

  isLoggedIn(): boolean {
    return !!this.currentUser$.value;
  }

  isAdmin(): boolean {
    return this.currentUser$.value?.role === 'Admin';
  }

  refreshCurrentUser(): Observable<AuthUser> {
    return this.http
      .get<AuthUser>(`${environment.apiUrl}/account/me`)
      .pipe(tap((user) => this.persist(user)));
  }

  updateProfile(payload: UpdateProfilePayload): Observable<AuthUser> {
    return this.http
      .put<AuthUser>(`${environment.apiUrl}/account/profile`, payload)
      .pipe(tap((user) => this.persist(user)));
  }

  changePassword(payload: ChangePasswordPayload): Observable<string> {
    return this.http
      .put<{ message: string }>(`${environment.apiUrl}/account/password`, payload)
      .pipe(map((r) => r.message));
  }

  private handleGoogleUser(socialUser: SocialUser): void {
    this.googleAuthError$.next('');
    this.http
      .post<AuthUser>(`${environment.apiUrl}/auth/google`, {
        idToken: socialUser.idToken,
      })
      .subscribe({
        next: (user) => {
          this.persist(user);
          this.router.navigate(['/chat']);
        },
        error: (err) => {
          const message = err?.error?.message ?? 'Google sign-in failed. Please try again.';
          this.googleAuthError$.next(message);
          this.socialAuth.signOut().catch(() => {});
        },
      });
  }

  private persist(user: AuthUser): void {
    localStorage.setItem(this.TOKEN_KEY, JSON.stringify(user));
    this.currentUser$.next(user);
  }
}
