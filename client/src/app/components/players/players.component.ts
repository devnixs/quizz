import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { GameStateDto, PlayerDto } from '../../models';

@Component({
  selector: 'app-players',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './players.component.html'
})
export class PlayersComponent {
  @Input() state!: GameStateDto;
  @Input() meId: string | null = null;
  @Input() isAdmin = false;
  @Input() nameEdits: Record<string, string> = {};
  @Output() rename = new EventEmitter<{ playerId: string; currentName: string }>();

  get openSlots(): number[] {
    const remaining = Math.max(0, (this.state?.maxPlayers ?? 0) - (this.state?.players?.length ?? 0));
    return Array.from({ length: remaining }, (_value, index) => index);
  }

  trackById(_index: number, player: PlayerDto): string {
    return player.id;
  }
}
