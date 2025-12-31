import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-answer-card',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './answer-card.component.html'
})
export class AnswerCardComponent {
  @Input() answerText = '';
  @Input() waitingCount = 0;
  @Input() playerCount = 0;
  @Input() isAdmin = false;
  @Input() allAnswered = false;

  @Output() answerTextChange = new EventEmitter<string>();
  @Output() submit = new EventEmitter<void>();
  @Output() reset = new EventEmitter<void>();
}
