export interface PlayerDto {
  id: string;
  name: string;
  hasAnswered: boolean;
  answer?: string | null;
}

export interface GameStateDto {
  players: PlayerDto[];
  adminId?: string | null;
  allAnswered: boolean;
  maxPlayers: number;
}
