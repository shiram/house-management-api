import { inject, Injectable, signal } from '@angular/core';
import { ApiService } from '../http/api.service';

export interface NotificationItem {
  id: string;
  title: string;
  body?: string;
  read: boolean;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class NotificationsService {
  private api = inject(ApiService);
  private items = signal<NotificationItem[]>([]);
  items$ = this.items.asReadonly();

  load() {
    // Attempt to load from backend; if API missing, return empty silently
    this.api.get<NotificationItem[]>('/notifications').subscribe({
      next: (v) => this.items.set(v || []),
      error: () => this.items.set([])
    });
  }

  unreadCount() {
    return this.items().filter(i => !i.read).length;
  }
}
