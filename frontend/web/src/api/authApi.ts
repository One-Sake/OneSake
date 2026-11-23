import { getBackendBaseUrl } from './backendUrl';
import { getGuestId } from './guestId';

// Player accounts (POST /auth/...). The session is an HttpOnly cookie set by the backend:
// JavaScript never sees it, every request only needs credentials: 'include'.

export interface AccountDto {
  username: string;
}

// Only after register and reset, and only this once.
export interface AccountWithCodeDto extends AccountDto {
  recoveryCode: string;
}

export class AuthApiError extends Error {
  readonly code: string;
  readonly retryAfterSeconds: number | null;

  constructor(code: string, message: string, retryAfterSeconds: number | null = null) {
    super(message);
    this.code = code;
    this.retryAfterSeconds = retryAfterSeconds;
  }
}

interface AuthErrorBody {
  error?: string;
  message?: string;
  retryAfterSeconds?: number | null;
}

async function send<T>(path: string, method: 'GET' | 'POST', body?: unknown): Promise<T | null> {
  let response: Response;
  try {
    response = await fetch(`${getBackendBaseUrl()}${path}`, {
      method,
      credentials: 'include',
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new AuthApiError('NETWORK', 'Der Server ist nicht erreichbar.');
  }

  if (response.status === 204) {
    return null;
  }

  if (!response.ok) {
    const errorBody = (await response.json().catch(() => ({}))) as AuthErrorBody;
    if (response.status === 429 && !errorBody.error) {
      // Rate limit of the server (too many requests from this network).
      throw new AuthApiError('RATE_LIMITED', 'Zu viele Versuche. Bitte warte eine Minute.');
    }

    throw new AuthApiError(
      errorBody.error ?? `HTTP_${response.status}`,
      errorBody.message ?? `Anfrage fehlgeschlagen (${response.status}).`,
      errorBody.retryAfterSeconds ?? null,
    );
  }

  return (await response.json()) as T;
}

// Null for guests (401).
export async function getCurrentAccount(): Promise<AccountDto | null> {
  try {
    return await send<AccountDto>('/auth/me', 'GET');
  } catch (error) {
    if (error instanceof AuthApiError && error.code === 'HTTP_401') {
      return null;
    }

    throw error;
  }
}

// The guest ID goes along once, so the match history of this browser moves to the new account.
export async function register(username: string, password: string): Promise<AccountWithCodeDto> {
  return (await send<AccountWithCodeDto>('/auth/register', 'POST', { username, password, guestId: getGuestId() }))!;
}

export async function login(username: string, password: string): Promise<AccountDto> {
  return (await send<AccountDto>('/auth/login', 'POST', { username, password }))!;
}

export async function resetPassword(
  username: string,
  recoveryCode: string,
  newPassword: string,
): Promise<AccountWithCodeDto> {
  return (await send<AccountWithCodeDto>('/auth/reset-password', 'POST', { username, recoveryCode, newPassword }))!;
}

export async function changePassword(currentPassword: string, newPassword: string): Promise<AccountDto> {
  return (await send<AccountDto>('/auth/change-password', 'POST', { currentPassword, newPassword }))!;
}

export async function logout(): Promise<void> {
  await send('/auth/logout', 'POST');
}

export async function logoutEverywhere(): Promise<void> {
  await send('/auth/logout-all', 'POST');
}
