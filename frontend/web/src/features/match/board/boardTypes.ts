export type BoardSide = 'player' | 'opponent';

export type BoardZoneKind =
  | 'character'
  | 'life'
  | 'leader'
  | 'stage'
  | 'donDeck'
  | 'donArea'
  | 'deck'
  | 'trash';

export interface BoardZoneDefinition {
  kind: BoardZoneKind;
  label: string;
}
