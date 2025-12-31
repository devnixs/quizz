import { CommonModule } from '@angular/common';
import { Component, OnInit, ViewEncapsulation } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { AnswerCardComponent } from './components/answer-card/answer-card.component';
import { ErrorBannerComponent } from './components/error-banner/error-banner.component';
import { HeroComponent } from './components/hero/hero.component';
import { JoinCardComponent } from './components/join-card/join-card.component';
import { PlayersComponent } from './components/players/players.component';
import { GameStateDto } from './models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, HeroComponent, JoinCardComponent, AnswerCardComponent, PlayersComponent, ErrorBannerComponent],
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
  encapsulation: ViewEncapsulation.None
})
export class AppComponent implements OnInit {
  title = 'Meuhte';
  hubUrl = (window as any).MEUHTE_HUB_URL ?? '/hub/game';
  connection?: signalR.HubConnection;
  state: GameStateDto = {
    players: [],
    adminId: null,
    allAnswered: false,
    maxPlayers: 10
  };
  joinName = '';
  answerText = '';
  errorMessage = '';
  isConnecting = false;
  nameEdits: Record<string, string> = {};

  ngOnInit(): void {
    void this.connect();
  }

  get meId(): string | null {
    return this.connection?.connectionId ?? null;
  }

  get isAdmin(): boolean {
    return !!this.meId && this.state.adminId === this.meId;
  }

  get hasJoined(): boolean {
    return !!this.meId && this.state.players.some((player) => player.id === this.meId);
  }

  get waitingCount(): number {
    return this.state.players.filter((player) => !player.hasAnswered).length;
  }

  async connect(): Promise<void> {
    if (this.connection || this.isConnecting) {
      return;
    }

    this.isConnecting = true;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl)
      .withAutomaticReconnect()
      .build();

    connection.on('StateUpdated', (snapshot: GameStateDto) => {
      this.state = snapshot;
      this.errorMessage = '';
    });

    connection.onreconnected(() => {
      void this.fetchState();
    });

    try {
      await connection.start();
      this.connection = connection;
      await this.fetchState();
    } catch (error) {
      this.errorMessage = 'Unable to connect to the game server.';
      console.error(error);
    } finally {
      this.isConnecting = false;
    }
  }

  async fetchState(): Promise<void> {
    if (!this.connection) {
      return;
    }

    try {
      const snapshot = await this.connection.invoke<GameStateDto>('GetState');
      this.state = snapshot;
    } catch (error) {
      console.error(error);
    }
  }

  async joinGame(): Promise<void> {
    if (!this.connection) {
      await this.connect();
    }

    if (!this.connection) {
      return;
    }

    const name = this.joinName.trim();
    if (!name) {
      this.errorMessage = 'Please enter a username.';
      return;
    }

    try {
      await this.connection.invoke('Join', name);
      this.joinName = '';
      this.errorMessage = '';
    } catch (error: any) {
      this.errorMessage = error?.message ?? 'Unable to join.';
    }
  }

  async submitAnswer(): Promise<void> {
    if (!this.connection) {
      return;
    }

    const answer = this.answerText.trim();
    if (!answer) {
      this.errorMessage = 'Answer cannot be empty.';
      return;
    }

    try {
      await this.connection.invoke('SubmitAnswer', answer);
      this.answerText = '';
      this.errorMessage = '';
    } catch (error: any) {
      this.errorMessage = error?.message ?? 'Unable to submit answer.';
    }
  }

  async updatePlayerName(playerId: string, currentName: string): Promise<void> {
    if (!this.connection || !this.isAdmin) {
      return;
    }

    const name = (this.nameEdits[playerId] ?? '').trim();
    if (!name) {
      this.errorMessage = 'Name cannot be empty.';
      return;
    }

    try {
      await this.connection.invoke('UpdatePlayerName', playerId, currentName, name);
      delete this.nameEdits[playerId];
      await this.fetchState();
      this.errorMessage = '';
    } catch (error: any) {
      this.errorMessage = error?.message ?? 'Unable to update name.';
    }
  }

  async resetGame(): Promise<void> {
    if (!this.connection || !this.isAdmin) {
      return;
    }

    try {
      await this.connection.invoke('ResetGame');
      this.errorMessage = '';
    } catch (error: any) {
      this.errorMessage = error?.message ?? 'Unable to reset.';
    }
  }

}
