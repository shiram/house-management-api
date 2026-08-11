import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);

  get<T>(path: string) {
    return this.http.get<T>(`${environment.apiUrl}${path}`);
  }

  post<T>(path: string, body: any) {
    return this.http.post<T>(`${environment.apiUrl}${path}`, body);
  }

  put<T>(path: string, body: any) {
    return this.http.put<T>(`${environment.apiUrl}${path}`, body);
  }

  delete<T>(path: string) {
    return this.http.delete<T>(`${environment.apiUrl}${path}`);
  }
}
