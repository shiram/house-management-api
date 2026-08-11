import { Component, signal } from '@angular/core';
import { ShellComponent } from './layout/shell.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [ShellComponent],
  template: `<app-shell></app-shell>`,
  styleUrl: './app.scss'
})
export class App {
  protected readonly title = signal('house-management-web');
}
