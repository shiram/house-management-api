import { Component } from '@angular/core';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `
    <div class="card">
      <div class="card-body">
        <h3>Dashboard</h3>
        <p class="text-muted">Welcome to the House Management admin shell. No business screens implemented yet.</p>
      </div>
    </div>
  `
})
export class DashboardComponent {}
