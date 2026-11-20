import { getBackendBaseUrl } from './backendUrl';

export type BugCategory = 'EFFECT' | 'ORDER' | 'STUCK' | 'DISPLAY' | 'OTHER';

export interface BugReportRequest {
  roomCode: string;
  guestId: string;
  description: string;
  category: BugCategory;
  cardId?: string;
  // Number of recorded decisions when the report was sent (the server replays up to here).
  atSeq: number;
}

// Sends a bug report to our own server (never to another service). Returns the report number.
export async function submitBugReport(request: BugReportRequest): Promise<number> {
  const response = await fetch(`${getBackendBaseUrl()}/bugreports`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  const body = (await response.json().catch(() => ({}))) as { id?: number; error?: string };
  if (!response.ok || typeof body.id !== 'number') {
    if (response.status === 429) {
      throw new Error('Zu viele Meldungen - bitte später noch einmal versuchen.');
    }

    throw new Error(body.error || `Meldung fehlgeschlagen (${response.status}).`);
  }

  return body.id;
}
