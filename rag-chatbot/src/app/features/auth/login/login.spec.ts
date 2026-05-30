import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject, of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { Login } from './login';
import { AuthService } from '../../../core/services/auth/auth';

describe('Login', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;
  let authServiceMock: {
    login: ReturnType<typeof vi.fn>;
    googleAuthError: BehaviorSubject<string>;
  };

  beforeEach(async () => {
    authServiceMock = {
      login: vi.fn().mockReturnValue(of({})),
      googleAuthError: new BehaviorSubject<string>(''),
    };

    await TestBed.configureTestingModule({
      imports: [Login],
      providers: [
        {
          provide: AuthService,
          useValue: authServiceMock,
        },
      ],
    })
    .overrideComponent(Login, {
      set: {
        imports: [],
        template: '',
      },
    })
    .compileComponents();

    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not submit when form is invalid', () => {
    component.onSubmit();

    expect(authServiceMock.login).not.toHaveBeenCalled();
    expect(component.loading).toBe(false);
  });

  it('should submit with valid credentials', () => {
    component.form.setValue({
      email: 'user@example.com',
      password: 'password123',
    });

    component.onSubmit();

    expect(authServiceMock.login).toHaveBeenCalledWith('user@example.com', 'password123');
    expect(component.loading).toBe(false);
    expect(component.errorMessage).toBe('');
  });

  it('should set a fallback error message when login fails', () => {
    authServiceMock.login.mockReturnValue(
      throwError(() => ({
        error: {},
      }))
    );
    component.form.setValue({
      email: 'user@example.com',
      password: 'password123',
    });

    component.onSubmit();

    expect(component.loading).toBe(false);
    expect(component.errorMessage).toBe('Login failed. Please try again.');
  });

  it('should consume Google auth errors from AuthService', () => {
    authServiceMock.googleAuthError.next('Google sign-in failed.');

    expect(component.errorMessage).toBe('Google sign-in failed.');
  });
});
