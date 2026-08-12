import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConfirmationService } from '../../core/ui/confirmation.service';

@Component({
  selector: 'app-confirmation-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
  <div *ngIf="svc.current()" class="position-fixed top-0 start-0 w-100 h-100 d-flex align-items-center justify-content-center" style="z-index:1085">
    <div class="modal-dialog">
      <div class="modal-content">
        <div class="modal-header">
          <h5 class="modal-title">{{ svc.current()?.title || 'Confirm' }}</h5>
          <button type="button" class="btn-close" aria-label="Close" (click)="svc.cancel()"></button>
        </div>
        <div class="modal-body">
          <p>{{ svc.current()?.message }}</p>
        </div>
        <div class="modal-footer">
          <button class="btn btn-secondary" (click)="svc.cancel()">{{ svc.current()?.cancelText || 'Cancel' }}</button>
          <button class="btn btn-danger" (click)="svc.accept()">{{ svc.current()?.confirmText || 'Confirm' }}</button>
        </div>
      </div>
    </div>
  </div>
  `
})
export class ConfirmationModalComponent {
  constructor(public svc: ConfirmationService) {}
}
