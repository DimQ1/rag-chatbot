import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpClientTestingModule } from '@angular/common/http/testing';
import { RouterTestingModule } from '@angular/router/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { SocialAuthService } from '@abacritt/angularx-social-login';
import { Chat } from './chat';
import { AuthService } from '../../../core/services/auth';
import { ChatService } from '../../../core/services/chat';
import { RagService } from '../../../core/services/rag';

describe('Chat', () => {
  let component: Chat;
  let fixture: ComponentFixture<Chat>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [Chat, HttpClientTestingModule, RouterTestingModule],
      providers: [
        AuthService,
        ChatService,
        RagService,
        {
          provide: SocialAuthService,
          useValue: {
            authState: of(null),
            signIn: vi.fn(),
            signOut: vi.fn(),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(Chat);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
