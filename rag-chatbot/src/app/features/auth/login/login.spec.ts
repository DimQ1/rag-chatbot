import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { NO_ERRORS_SCHEMA } from '@angular/core';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { Login } from './login';
import { AuthService } from '../../../core/services/auth';

describe.skip('Login', () => {
  let component: Login;
  let fixture: ComponentFixture<Login>;
  let authService: AuthService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Login, HttpClientTestingModule, RouterTestingModule],
      providers: [
        AuthService,
        {
          provide: SocialAuthService,
          useValue: {
            authState: of(null),
            signIn: vi.fn(),
            signOut: vi.fn(),
          },
        },
      ],
      schemas: [NO_ERRORS_SCHEMA],
    })
    .overrideComponent(Login, {
      remove: { imports: [] },
    })
    .compileComponents();

    authService = TestBed.inject(AuthService);
    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
