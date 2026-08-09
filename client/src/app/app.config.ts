import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { MAT_FORM_FIELD_DEFAULT_OPTIONS } from '@angular/material/form-field';
import { routes } from './app.routes';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),

    // withComponentInputBinding lets a route param arrive as an @Input, so detail components
    // never have to inject ActivatedRoute just to read an id from the URL.
    provideRouter(routes, withComponentInputBinding()),

    // Order matters: auth runs first so it can retry a 401 with a fresh token, and the error
    // interceptor only sees failures that survived that retry.
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),

    provideAnimationsAsync(),

    { provide: MAT_FORM_FIELD_DEFAULT_OPTIONS, useValue: { appearance: 'outline' } },
  ],
};
