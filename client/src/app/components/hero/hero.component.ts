import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-hero',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './hero.component.html'
})
export class HeroComponent {
  @Input() title = '';
  @Input() connected = false;
}
