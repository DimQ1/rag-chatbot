import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable } from 'rxjs';
import { of, throwError } from 'rxjs';
import { Chat } from './chat';
import { AuthService } from '../../../core/services/auth/auth';
import { ChatService } from '../../../core/services/chat/chat';

describe('Chat', () => {
  let component: Chat;
  let fixture: ComponentFixture<Chat>;
  let chatServiceMock: {
    messages: Observable<unknown[]>;
    currentSessionId: Observable<string | null>;
    currentSessionId$Value: string | null;
    currentThinkingSessionId$Value: string | null;
    sessionsList: Array<{ id: string }>;
    addMessage: ReturnType<typeof vi.fn>;
    addMessageToSession: ReturnType<typeof vi.fn>;
    createSession: ReturnType<typeof vi.fn>;
    clearMessages: ReturnType<typeof vi.fn>;
    setCurrentSession: ReturnType<typeof vi.fn>;
    loadSessionDetail: ReturnType<typeof vi.fn>;
    loadSessions: ReturnType<typeof vi.fn>;
    setThinkingSession: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    chatServiceMock = {
      messages: of([]),
      currentSessionId: of(null),
      currentSessionId$Value: 'session-1',
      currentThinkingSessionId$Value: null,
      sessionsList: [],
      addMessage: vi.fn(),
      addMessageToSession: vi.fn().mockReturnValue(of({})),
      createSession: vi.fn().mockReturnValue(of({ id: 'session-created' })),
      clearMessages: vi.fn(),
      setCurrentSession: vi.fn(),
      loadSessionDetail: vi.fn(),
      loadSessions: vi.fn().mockReturnValue(of([])),
      setThinkingSession: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [Chat],
      providers: [
        {
          provide: ChatService,
          useValue: {
            ...chatServiceMock,
            get currentSessionId$Value() {
              return chatServiceMock.currentSessionId$Value;
            },
            get currentThinkingSessionId$Value() {
              return chatServiceMock.currentThinkingSessionId$Value;
            },
          },
        },
        {
          provide: AuthService,
          useValue: {
            currentUser: { name: 'Test User' },
            isAdmin: vi.fn().mockReturnValue(false),
          },
        },
      ],
    })
      .overrideComponent(Chat, {
        set: {
          template: '',
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(Chat);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should not send when input is empty', () => {
    component.inputControl.setValue('   ');

    component.send();

    expect(chatServiceMock.addMessage).not.toHaveBeenCalled();
    expect(chatServiceMock.addMessageToSession).not.toHaveBeenCalled();
  });

  it('should send message to current session and refresh data', () => {
    component.inputControl.setValue('Hello world');

    component.send();

    expect(chatServiceMock.addMessage).toHaveBeenCalledWith('user', 'Hello world');
    expect(chatServiceMock.setThinkingSession).toHaveBeenCalledWith('session-1');
    expect(chatServiceMock.addMessageToSession).toHaveBeenCalledWith('session-1', 'Hello world', false);
    expect(chatServiceMock.loadSessionDetail).toHaveBeenCalledWith('session-1', { setAsCurrent: false });
    expect(chatServiceMock.loadSessions).toHaveBeenCalled();
    expect(chatServiceMock.setThinkingSession).toHaveBeenLastCalledWith(null);
  });

  it('should append assistant fallback message when send fails', () => {
    chatServiceMock.addMessageToSession.mockReturnValue(
      throwError(() => new Error('network error'))
    );
    component.inputControl.setValue('Hello world');

    component.send();

    expect(chatServiceMock.addMessage).toHaveBeenCalledWith('user', 'Hello world');
    expect(chatServiceMock.addMessage).toHaveBeenCalledWith(
      'assistant',
      'Sorry, I encountered an error. Please try again.'
    );
    expect(chatServiceMock.setThinkingSession).toHaveBeenLastCalledWith(null);
  });
});
