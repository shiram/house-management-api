import { HttpHandler, HttpInterceptor, HttpRequest, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { inject, Injectable } from '@angular/core';
import { AuthService } from '../auth/auth.service';

@Injectable()
export class TokenInterceptor implements HttpInterceptor {
  private auth = inject(AuthService);

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const token = this.auth.token();
    if (token) {
      const cloned = req.clone({ headers: req.headers.set('Authorization', `Bearer ${token}`) });
      return next.handle(cloned);
    }
    return next.handle(req);
  }
}
