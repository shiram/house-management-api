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

  // derived current user information from token
  private _currentUser = signal<{ name?: string; roles: string[] } | null>(this.parseTokenToUser(this._token()));
  readonly currentUser = this._currentUser.asReadonly();

  isAuthenticated() {
    const t = this._token();
    return !!t && !this.isTokenExpired(t);
  }

  login(payload: LoginRequest) {
    return this.http.post<AuthResponse>(`${environment.apiUrl}/auth/login`, payload);
  }

  setToken(token: string | null) {
    this._token.set(token);
    this._currentUser.set(this.parseTokenToUser(token));
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

  private parseTokenToUser(token: string | null) {
    if (!token) return null;
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const name = payload.unique_name || payload.name || payload.email || payload.sub;
      const role = payload.role || payload.roles || payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
      const roles = Array.isArray(role) ? role : (role ? [role] : []);
      return { name, roles };
    } catch {
      return null;
    }
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
