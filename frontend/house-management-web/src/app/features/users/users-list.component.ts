import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UsersService, UserDto } from './users.service';
import { ToastService } from '../../core/ui/toast.service';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [CommonModule],
  template: `
  <div class="card">
    <div class="card-body">
      <h5 class="card-title">Users</h5>
      <div *ngIf="loading" class="text-center py-4">
        <div class="spinner-border" role="status"></div>
      </div>

      <div *ngIf="error" class="alert alert-danger">{{ error }}</div>

      <div *ngIf="!loading && !error">
        <table class="table table-hover table-sm">
          <thead>
            <tr>
              <th>Id</th>
              <th>Username</th>
              <th>Email</th>
              <th>Role</th>
              <th>Status</th>
            </tr>
          </thead>
          <tbody>
            <tr *ngFor="let u of users">
              <td>{{ u.id }}</td>
              <td>{{ u.userName }}</td>
              <td>{{ u.email }}</td>
              <td>{{ u.role }}</td>
              <td>
                <span class="badge" [ngClass]="u.isActive ? 'bg-success' : 'bg-secondary'">{{ u.isActive ? 'Active' : 'Inactive' }}</span>
              </td>
            </tr>
          </tbody>
        </table>

        <div *ngIf="users.length === 0" class="text-center text-muted py-3">No users found.</div>
      </div>
    </div>
  </div>
  `
})
export class UsersListComponent {
  users: UserDto[] = [];
  loading = false;
  error: string | null = null;

  constructor(private svc: UsersService, private toast: ToastService) {
    this.load();
  }

  load() {
    this.loading = true;
    this.error = null;
    this.svc.list().subscribe({
      next: (res) => {
        this.loading = false;
        if (res?.data) this.users = res.data;
        else this.users = [];
      },
      error: (err) => {
        this.loading = false;
        this.error = 'Failed to load users.';
        this.toast.error('Failed to load users.');
      }
    });
  }
}
