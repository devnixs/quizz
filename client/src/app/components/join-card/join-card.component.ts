import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-join-card',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './join-card.component.html'
})
export class JoinCardComponent {
  @Input() maxPlayers = 10;
  @Input() joinName = '';
  @Input() isConnecting = false;
  @Output() joinNameChange = new EventEmitter<string>();
  @Output() join = new EventEmitter<void>();

  submit(): void {
    this.join.emit();
  }
}
