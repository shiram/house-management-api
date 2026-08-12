import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
  <aside class="sidebar bg-white border-end">
    <nav class="nav flex-column p-2">
      <div class="nav-group mb-2">
        <div class="nav-group-title px-2 text-muted">Main</div>
        <a class="nav-link px-2" routerLink="/dashboard" routerLinkActive="active"> <i class="bi bi-speedometer2 me-2"></i> Dashboard</a>
        <a class="nav-link px-2" routerLink="/users" routerLinkActive="active"> <i class="bi bi-people me-2"></i> Users</a>
      </div>

      <div class="nav-group mt-3">
        <div class="nav-group-title px-2 text-muted">Directory</div>
        <a class="nav-link px-2" routerLink="/househelps" routerLinkActive="active"> <i class="bi bi-person-badge me-2"></i> HouseHelps</a>
      </div>
    </nav>
  </aside>
  `,
  styles: [`
    .sidebar { width: 250px; min-height: calc(100vh - 56px); }
    .nav-link.active { background: rgba(21,94,239,0.06); color: var(--primary); border-radius:4px }
  `]
})
export class SidebarComponent {}
