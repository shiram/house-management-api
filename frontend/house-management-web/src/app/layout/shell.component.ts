import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet],
  template: `
  <div class="app-shell bg-page min-vh-100">
    <nav class="navbar navbar-expand-lg bg-white border-bottom shadow-sm">
      <div class="container-fluid">
        <a class="navbar-brand text-primary" routerLink="/">House Management</a>
        <div class="d-flex">
          <a class="btn btn-outline-secondary btn-sm" routerLink="/login">Sign in</a>
        </div>
      </div>
    </nav>
    <main class="container py-4">
      <router-outlet></router-outlet>
    </main>
  </div>
  `,
  styles: [``]
})
export class ShellComponent {}
