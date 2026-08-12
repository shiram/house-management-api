import { inject, Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class UiStateService {
  private _sidebarOpen = signal(false);
  readonly sidebarOpen = this._sidebarOpen.asReadonly();

  toggleSidebar() {
    this._sidebarOpen.set(!this._sidebarOpen());
  }

  openSidebar() { this._sidebarOpen.set(true); }
  closeSidebar() { this._sidebarOpen.set(false); }
}
