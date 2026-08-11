import { ApplicationConfig, provideBrowserGlobalErrorListeners, importProvidersFrom } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';

import { routes } from './app.routes';
import { TokenInterceptor } from './core/http/token.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptorsFromDi()),
    importProvidersFrom(HttpClientModule),
    { provide: HTTP_INTERCEPTORS, useClass: TokenInterceptor, multi: true },
    provideRouter(routes)
  ]
};
