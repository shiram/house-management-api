import { inject, Injectable } from '@angular/core';
import { ApiService } from '../../core/http/api.service';
import { Observable } from 'rxjs';

export interface UserDto {
  id: number;
  userName: string;
  email: string;
  role?: string;
  isActive?: boolean;
}

export interface ApiResponse<T> {
  data: T;
  statusCode: number;
  message: string;
  requestId?: string;
  responseDateTime?: string;
  error?: any;
}

@Injectable({ providedIn: 'root' })
export class UsersService {
  private api = inject(ApiService);

  list(): Observable<ApiResponse<UserDto[]>> {
    return this.api.get<ApiResponse<UserDto[]>>('/users');
  }

  get(id: number) {
    return this.api.get<ApiResponse<UserDto>>(`/users/${id}`);
  }

  create(payload: Partial<UserDto>) {
    return this.api.post<ApiResponse<UserDto>>('/users', payload);
  }

  update(id: number, payload: Partial<UserDto>) {
    return this.api.put<ApiResponse<UserDto>>(`/users/${id}`, payload);
  }

  delete(id: number) {
    return this.api.delete<ApiResponse<null>>(`/users/${id}`);
  }
}
