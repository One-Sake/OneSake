import { useState } from 'react';
import { createPortal } from 'react-dom';

import { Modal } from '../../components/ui/Modal';
import { CHANGELOG, LATEST_VERSION } from '../../content/changelog';

const SEEN_VERSION_STORAGE_KEY = 'onesake.changelog.seenVersion';

function loadSeenVersion(): string {
  try {
    return window.localStorage.getItem(SEEN_VERSION_STORAGE_KEY) ?? '';
  } catch {
    return '';
  }
}

function formatDate(isoDate: string): string {
  const date = new Date(`${isoDate}T12:00:00`);
  return Number.isNaN(date.getTime())
    ? isoDate
    : date.toLocaleDateString(undefined, { day: '2-digit', month: '2-digit', year: 'numeric' });
}

// Small "v0.5.0 · Was ist neu?" link. A dot shows news the player has not opened yet.
export function WhatsNewLink() {
  const [isOpen, setIsOpen] = useState(false);
  const [seenVersion, setSeenVersion] = useState(loadSeenVersion);
  const hasUnread = seenVersion !== LATEST_VERSION;

  const open = (): void => {
    setIsOpen(true);
    setSeenVersion(LATEST_VERSION);
    try {
      window.localStorage.setItem(SEEN_VERSION_STORAGE_KEY, LATEST_VERSION);
    } catch {
      // Without storage the dot simply shows again next time.
    }
  };

  // Portal: the lobby is its own stacking context, the modal must cover the whole page.
  const modal = (
    <Modal className="whats-new-modal" onClose={() => setIsOpen(false)} open={isOpen} title="Was ist neu">
      <ol className="whats-new-list">
        {CHANGELOG.map((entry) => (
          <li key={entry.version}>
            <header>
              <strong>v{entry.version}</strong>
              <span>{entry.title}</span>
              <time dateTime={entry.date}>{formatDate(entry.date)}</time>
            </header>
            <ul>
              {entry.items.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
          </li>
        ))}
      </ol>
    </Modal>
  );

  return (
    <>
      <button className="whats-new-link" onClick={open} type="button">
        {hasUnread && <span aria-label="Neu" className="whats-new-link__dot" role="img" />}
        v{LATEST_VERSION} · Was ist neu?
      </button>
      {createPortal(modal, document.body)}
    </>
  );
}
