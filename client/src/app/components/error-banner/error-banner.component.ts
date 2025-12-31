import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-error-banner',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './error-banner.component.html'
})
export class ErrorBannerComponent {
  @Input() message = '';
}
