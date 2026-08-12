import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet } from '@angular/router';
import { NavbarComponent } from './navbar.component';
import { SidebarComponent } from './sidebar.component';
import { UiStateService } from '../core/ui/ui-state.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, NavbarComponent, SidebarComponent, RouterOutlet],
  template: `
  <div class="app-shell bg-page min-vh-100 d-flex flex-column">
    <app-navbar></app-navbar>
    <div class="d-flex flex-grow-1 position-relative">
      <!-- desktop sidebar -->
      <div class="d-none d-md-block">
        <app-sidebar></app-sidebar>
      </div>

      <!-- mobile sidebar overlay -->
      <div *ngIf="sidebarOpen()" class="mobile-sidebar-overlay d-md-none">
        <div class="mobile-sidebar bg-white">
          <app-sidebar></app-sidebar>
        </div>
        <div class="mobile-backdrop" (click)="close()"></div>
      </div>

      <div class="flex-grow-1 p-3">
        <router-outlet></router-outlet>
      </div>
    </div>
  </div>
  `,
  styles: [`
    .mobile-sidebar-overlay { position: absolute; inset:0; z-index:1040; display:flex }
    .mobile-sidebar { width:260px; box-shadow:0 6px 18px rgba(17,24,39,0.08); }
    .mobile-backdrop { flex:1; background:rgba(0,0,0,0.35); }
  `]
})
export class ShellComponent {
  private ui = inject(UiStateService);
  sidebarOpen = this.ui.sidebarOpen;
  close() { this.ui.closeSidebar(); }
}

