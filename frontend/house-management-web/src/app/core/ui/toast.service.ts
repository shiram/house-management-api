import { inject, Injectable, signal } from '@angular/core';

export interface ToastItem {
  id: string;
  type: 'success' | 'info' | 'warning' | 'danger';
  title?: string;
  message: string;
  timeout?: number; // ms
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  private items = signal<ToastItem[]>([]);
  items$ = this.items.asReadonly();

  show(type: ToastItem['type'], message: string, title?: string, timeout = 5000) {
    const id = Math.random().toString(36).slice(2, 9);
    const item: ToastItem = { id, type, title, message, timeout };
    this.items.set([item, ...this.items()]);
    if (timeout && timeout > 0) {
      setTimeout(() => this.remove(id), timeout);
    }
    return id;
  }

  success(message: string, title?: string, timeout = 5000) { return this.show('success', message, title, timeout); }
  info(message: string, title?: string, timeout = 5000) { return this.show('info', message, title, timeout); }
  warning(message: string, title?: string, timeout = 5000) { return this.show('warning', message, title, timeout); }
  error(message: string, title?: string, timeout = 7000) { return this.show('danger', message, title, timeout); }

  remove(id: string) {
    this.items.set(this.items().filter(i => i.id !== id));
  }

  clear() { this.items.set([]); }
}
