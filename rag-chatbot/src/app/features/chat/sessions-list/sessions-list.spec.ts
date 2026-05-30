import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { BehaviorSubject, Observable, of, throwError } from 'rxjs';
import { SessionsListComponent } from './sessions-list';
import { ChatService, ChatSession } from '../../../core/services/chat/chat';

describe('SessionsListComponent', () => {
  let component: SessionsListComponent;
  let fixture: ComponentFixture<SessionsListComponent>;
  let chatServiceMock: {
    sessions: Observable<ChatSession[]>;
    currentSessionId: Observable<string | null>;
    currentThinkingSessionId: Observable<string | null>;
    loadSessions: ReturnType<typeof vi.fn>;
    createSession: ReturnType<typeof vi.fn>;
    setCurrentSession: ReturnType<typeof vi.fn>;
    clearMessages: ReturnType<typeof vi.fn>;
    loadSessionDetail: ReturnType<typeof vi.fn>;
    pinSession: ReturnType<typeof vi.fn>;
    renameSession: ReturnType<typeof vi.fn>;
    deleteSession: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    const sessions$ = new BehaviorSubject<ChatSession[]>([]);

    chatServiceMock = {
      sessions: sessions$.asObservable(),
      currentSessionId: of(null),
      currentThinkingSessionId: of(null),
      loadSessions: vi.fn().mockReturnValue(of([])),
      createSession: vi.fn().mockReturnValue(
        of({
          id: 'session-1',
          topic: 'Session',
          isPinned: false,
          createdAtUtc: new Date('2026-01-01T00:00:00Z'),
          updatedAtUtc: new Date('2026-01-01T00:00:00Z'),
          messageCount: 0,
        })
      ),
      setCurrentSession: vi.fn(),
      clearMessages: vi.fn(),
      loadSessionDetail: vi.fn(),
      pinSession: vi.fn().mockReturnValue(of({})),
      renameSession: vi.fn().mockReturnValue(of({})),
      deleteSession: vi.fn().mockReturnValue(of({})),
    };

    await TestBed.configureTestingModule({
      imports: [SessionsListComponent],
      providers: [
        {
          provide: ChatService,
          useValue: chatServiceMock,
        },
        {
          provide: MatDialog,
          useValue: {
            open: vi.fn(),
          },
        },
      ],
    })
      .overrideComponent(SessionsListComponent, {
        set: {
          template: '',
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(SessionsListComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should load sessions on init and clear loading state on success', () => {
    fixture.detectChanges();

    expect(chatServiceMock.loadSessions).toHaveBeenCalledTimes(1);
    expect(component.loading()).toBe(false);
    expect(component.errorMessage()).toBe('');
  });

  it('should surface load error message', () => {
    chatServiceMock.loadSessions.mockReturnValue(
      throwError(() => ({ error: { message: 'Unable to fetch sessions.' } }))
    );

    component.loadSessions();

    expect(component.loading()).toBe(false);
    expect(component.errorMessage()).toBe('Unable to fetch sessions.');
  });

  it('should create a new session and refresh list', () => {
    const loadSessionsSpy = vi.spyOn(component, 'loadSessions').mockImplementation(() => undefined);

    component.createNewSession();

    expect(chatServiceMock.createSession).toHaveBeenCalled();
    expect(chatServiceMock.setCurrentSession).toHaveBeenCalledWith('session-1');
    expect(chatServiceMock.clearMessages).toHaveBeenCalled();
    expect(loadSessionsSpy).toHaveBeenCalledTimes(1);
  });

  it('should select session and load session detail', () => {
    component.selectSession('session-42');

    expect(chatServiceMock.setCurrentSession).toHaveBeenCalledWith('session-42');
    expect(chatServiceMock.loadSessionDetail).toHaveBeenCalledWith('session-42');
  });
});
