// Stable guest ID for this browser. The backend uses it to link match results to a player.
// This is NOT secure authentication: anyone could copy or fake the ID.
// A real login will replace it later (stage 2).

const GUEST_ID_STORAGE_KEY = 'onesake.guestId';

function createUuid(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }

  // crypto.randomUUID only exists in secure contexts (https/localhost), not on a LAN IP over http.
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  bytes[6] = (bytes[6] & 0x0f) | 0x40; // version 4
  bytes[8] = (bytes[8] & 0x3f) | 0x80; // variant 10xx
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');

  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

let memoryGuestId = '';

export function getGuestId(): string {
  try {
    const storedGuestId = window.localStorage.getItem(GUEST_ID_STORAGE_KEY);
    if (storedGuestId && UUID_PATTERN.test(storedGuestId)) {
      return storedGuestId;
    }

    const guestId = createUuid();
    window.localStorage.setItem(GUEST_ID_STORAGE_KEY, guestId);
    return guestId;
  } catch {
    // Without localStorage the ID only lives for this page load.
    memoryGuestId = memoryGuestId || createUuid();
    return memoryGuestId;
  }
}
