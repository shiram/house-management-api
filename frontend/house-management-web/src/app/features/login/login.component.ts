import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
  <div class="row justify-content-center">
    <div class="col-md-6">
      <div class="card shadow-sm">
        <div class="card-body">
          <h3 class="card-title mb-3">Sign in</h3>
          <form [formGroup]="form" (ngSubmit)="submit()">
            <div class="mb-3">
              <label class="form-label">Username</label>
              <input class="form-control" formControlName="userName" />
            </div>
            <div class="mb-3">
              <label class="form-label">Password</label>
              <input type="password" class="form-control" formControlName="password" />
            </div>
            <div class="d-flex justify-content-end">
              <button class="btn btn-primary" [disabled]="form.invalid">Sign in</button>
            </div>
          </form>
        </div>
      </div>
    </div>
  </div>
  `,
  styles: [``]
})
export class LoginComponent {
  form = new FormGroup({
    userName: new FormControl('', Validators.required),
    password: new FormControl('', Validators.required)
  });

  constructor(private auth: AuthService, private router: Router) {}

  submit() {
    if (this.form.invalid) return;
    const v = this.form.value as { userName: string; password: string };
    this.auth.login(v).subscribe({
      next: (res: any) => {
        if (res?.token) {
          this.auth.setToken(res.token);
          this.router.navigate(['/']);
        }
      },
      error: () => {
        // TODO: show toast
      }
    });
  }
}
