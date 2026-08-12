import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ToastService } from '../../core/ui/toast.service';

@Component({
  selector: 'app-toasts',
  standalone: true,
  imports: [CommonModule],
  template: `
  <div class="position-fixed top-0 end-0 p-3" style="z-index: 1080;">
    <div *ngFor="let t of toasts.items$()" class="toast show mb-2" role="alert" aria-live="assertive" aria-atomic="true">
      <div class="toast-header" [ngClass]="headerClass(t.type)">
        <strong class="me-auto">{{ t.title || (t.type | titlecase) }}</strong>
        <small class="text-muted">now</small>
        <button type="button" class="btn-close ms-2 mb-1" aria-label="Close" (click)="dismiss(t.id)"></button>
      </div>
      <div class="toast-body">{{ t.message }}</div>
    </div>
  </div>
  `
})
export class ToastsComponent {
  constructor(public toasts: ToastService) {}

  dismiss(id: string) { this.toasts.remove(id); }

  headerClass(type: string) {
    switch(type){
      case 'success': return 'bg-success text-white';
      case 'warning': return 'bg-warning text-dark';
      case 'danger': return 'bg-danger text-white';
      default: return 'bg-primary text-white';
    }
  }
}
