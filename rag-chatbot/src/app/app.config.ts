import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import {
  SocialAuthServiceConfig,
  GoogleLoginProvider,
  SOCIAL_AUTH_CONFIG,
} from '@abacritt/angularx-social-login';

import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth-interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAnimationsAsync(),
    {
      provide: SOCIAL_AUTH_CONFIG,
      useValue: {
        autoLogin: false,
        lang: 'en',
        providers: [
          {
            id: GoogleLoginProvider.PROVIDER_ID,
            // Replace with your actual Google OAuth Client ID from Google Cloud Console
            provider: new GoogleLoginProvider('YOUR_GOOGLE_CLIENT_ID', {
              // Keep explicit sign-in button flow and avoid One Tap prompt callbacks
              // that currently emit noisy FedCM migration warnings in dev tools.
              oneTapEnabled: false,
              prompt: 'select_account',
            }),
          },
        ],
        onError: (err: unknown) => {
          const message =
            err instanceof Error
              ? err.message
              : typeof err === 'string'
                ? err
                : '';

          // GIS can emit AbortError when no identity session is available.
          if (/AbortError|Not signed in with the identity provider/i.test(message)) {
            console.info('Google sign-in prompt dismissed or no active provider session.');
            return;
          }

          console.error('Google sign-in error:', err);
        },
      } as SocialAuthServiceConfig,
    },
  ]
};
