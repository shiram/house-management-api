import { inject, Injectable, signal } from '@angular/core';

export interface ConfirmationOptions {
  title?: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
}

@Injectable({ providedIn: 'root' })
export class ConfirmationService {
  private state = signal<ConfirmationOptions | null>(null);
  private resolver: ((value: boolean) => void) | null = null;

  get current() { return this.state; }

  confirm(options: ConfirmationOptions) {
    this.state.set(options);
    return new Promise<boolean>((resolve) => {
      this.resolver = resolve;
    });
  }

  accept() {
    if (this.resolver) this.resolver(true);
    this.cleanup();
  }

  cancel() {
    if (this.resolver) this.resolver(false);
    this.cleanup();
  }

  private cleanup() {
    this.resolver = null;
    this.state.set(null);
  }
}
