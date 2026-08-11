import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { environment } from '../../../environments/environment';

interface LoginRequest {
  userName: string;
  password: string;
}

interface AuthResponse {
  token: string;
  expiresIn?: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private _token = signal<string | null>(localStorage.getItem('hm_token'));
  readonly token = this._token.asReadonly();

  isAuthenticated() {
    const t = this._token();
    return !!t && !this.isTokenExpired(t);
  }

  login(payload: LoginRequest) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, payload);
  }

  setToken(token: string | null) {
    this._token.set(token);
    if (token) {
      localStorage.setItem('hm_token', token);
    } else {
      localStorage.removeItem('hm_token');
    }
  }

  logout() {
    this.setToken(null);
    this.router.navigate(['/login']);
  }

  private isTokenExpired(token: string) {
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const exp = payload.exp;
      if (!exp) return false;
      const now = Math.floor(Date.now() / 1000);
      return exp < now;
    } catch {
      return false;
    }
  }
}
