import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { Account } from './account';
import { AuthService } from '../../../core/services/auth';

describe('Account', () => {
  let component: Account;
  let fixture: ComponentFixture<Account>;
  let authServiceMock: {
    currentUser: { name: string } | null;
    refreshCurrentUser: ReturnType<typeof vi.fn>;
    updateProfile: ReturnType<typeof vi.fn>;
    changePassword: ReturnType<typeof vi.fn>;
    logout: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    authServiceMock = {
      currentUser: { name: 'Existing User' },
      refreshCurrentUser: vi.fn().mockReturnValue(of({ name: 'Refreshed User' })),
      updateProfile: vi.fn().mockReturnValue(of({})),
      changePassword: vi.fn().mockReturnValue(of('Password updated.')),
      logout: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [Account],
      providers: [
        {
          provide: AuthService,
          useValue: authServiceMock,
        },
      ],
    })
      .overrideComponent(Account, {
        set: {
          template: '',
        },
      })
      .compileComponents();

    fixture = TestBed.createComponent(Account);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should initialize profile name from current user and refresh result', () => {
    component.ngOnInit();

    expect(authServiceMock.refreshCurrentUser).toHaveBeenCalled();
    expect(component.profileForm.getRawValue().name).toBe('Refreshed User');
  });

  it('should save profile when form is valid', () => {
    component.profileForm.setValue({ name: 'Updated Name' });

    component.saveProfile();

    expect(authServiceMock.updateProfile).toHaveBeenCalledWith({ name: 'Updated Name' });
    expect(component.profileSaving).toBe(false);
    expect(component.profileMessage).toBe('Profile updated.');
  });

  it('should set profile error on save failure', () => {
    authServiceMock.updateProfile.mockReturnValue(
      throwError(() => ({ error: { message: 'Profile update failed.' } }))
    );
    component.profileForm.setValue({ name: 'Updated Name' });

    component.saveProfile();

    expect(component.profileSaving).toBe(false);
    expect(component.errorMessage).toBe('Profile update failed.');
  });

  it('should change password and reset form on success', () => {
    component.passwordForm.setValue({
      currentPassword: 'old-pass',
      newPassword: 'new-pass-123',
    });

    component.changePassword();

    expect(authServiceMock.changePassword).toHaveBeenCalledWith({
      currentPassword: 'old-pass',
      newPassword: 'new-pass-123',
    });
    expect(component.passwordSaving).toBe(false);
    expect(component.passwordMessage).toBe('Password updated.');
    expect(component.passwordForm.getRawValue()).toEqual({ currentPassword: '', newPassword: '' });
  });
});
