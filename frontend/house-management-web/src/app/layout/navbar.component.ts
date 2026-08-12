import { Component, signal, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';
import { NotificationsService } from '../core/notifications/notifications.service';
import { UiStateService } from '../core/ui/ui-state.service';

@Component({
  selector: 'app-navbar',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
  <nav class="navbar navbar-expand-lg navbar-light bg-white border-bottom py-2">
    <div class="container-fluid">
      <a class="navbar-brand text-primary" routerLink="/">House Management</a>

      <button class="navbar-toggler" type="button" (click)="toggleMobile()">
        <span class="navbar-toggler-icon"></span>
      </button>

      <div class="d-flex align-items-center gap-2">
        <div class="dropdown me-2">
          <a class="nav-link position-relative" href="#" role="button" (click)="toggleNotifications($event)">
            <i class="bi bi-bell" style="font-size:1.2rem"></i>
            <span *ngIf="unread() > 0" class="badge bg-danger rounded-pill position-absolute" style="top:0;right:-6px">{{ unread() }}</span>
          </a>
        </div>

        <div class="dropdown">
          <a class="nav-link dropdown-toggle d-flex align-items-center" href="#" role="button" (click)="toggleUserMenu($event)">
            <i class="bi bi-person-circle me-2" style="font-size:1.2rem"></i>
            <span>{{ userName() || 'Account' }}</span>
          </a>
          <ul class="dropdown-menu dropdown-menu-end" [class.show]="menuOpen()" style="min-width:180px">
            <li><a class="dropdown-item" routerLink="/profile">Profile</a></li>
            <li><hr class="dropdown-divider" /></li>
            <li><a class="dropdown-item text-danger" (click)="logout()">Logout</a></li>
          </ul>
        </div>
      </div>
    </div>
  </nav>
  `
})
export class NavbarComponent {
  private auth = inject(AuthService);
  private notifications = inject(NotificationsService);
  private ui = inject(UiStateService);

  // signals for UI state
  menuOpen = signal(false);
  notificationsOpen = signal(false);

  unread = signal(0);

  userName = signal<string | undefined>(this.auth.currentUser()?.name);

  constructor() {
    // load notifications and keep unread in sync
    this.notifications.load();
    this.unread.set(this.notifications.unreadCount());
  }

  toggleUserMenu(e?: Event) {
    e?.preventDefault();
    this.menuOpen.set(!this.menuOpen());
  }

  toggleNotifications(e?: Event) {
    e?.preventDefault();
    this.notificationsOpen.set(!this.notificationsOpen());
  }

  toggleMobile() {
    this.ui.toggleSidebar();
  }

  logout() {
    this.auth.logout();
  }
}
