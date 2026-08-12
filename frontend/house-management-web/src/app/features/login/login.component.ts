import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
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
          <div *ngIf="error" class="alert alert-danger">{{ error }}</div>
          <form [formGroup]="form" (ngSubmit)="submit()">
            <div class="mb-3">
              <label class="form-label">Username</label>
              <input class="form-control" formControlName="userName" />
              <div *ngIf="form.controls['userName'].touched && form.controls['userName'].invalid" class="text-danger small">Username is required</div>
            </div>
            <div class="mb-3">
              <label class="form-label">Password</label>
              <input type="password" class="form-control" formControlName="password" />
              <div *ngIf="form.controls['password'].touched && form.controls['password'].invalid" class="text-danger small">Password is required</div>
            </div>
            <div class="d-flex justify-content-end">
              <button class="btn btn-primary" [disabled]="form.invalid || loading">
                <span *ngIf="loading" class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                Sign in
              </button>
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

  loading = false;
  error: string | null = null;

  private returnUrl: string | null = null;

  constructor(private auth: AuthService, private router: Router, private route: ActivatedRoute) {
    const q = this.route.snapshot.queryParams['returnUrl'];
    this.returnUrl = q || '/';
  }

  submit() {
    if (this.form.invalid) return;
    this.loading = true;
    this.error = null;
    const v = this.form.value as { userName: string; password: string };
    this.auth.login(v).subscribe({
      next: (res: any) => {
        this.loading = false;
        if (res?.token) {
          this.auth.setToken(res.token);
          this.router.navigate([this.returnUrl || '/']);
        } else {
          this.error = 'Invalid server response';
        }
      },
      error: (err: any) => {
        this.loading = false;
        if (err?.status === 401) this.error = 'Invalid username or password';
        else this.error = 'Unable to sign in. Please try again.';
      }
    });
  }
}
