import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';

import type { ManualActionDto } from '../../../types/realtime';
import type { ManualItem } from './manualItems';

export interface ManualMenuState {
  x: number;
  y: number;
  title: string;
  items: ManualItem[];
}

interface ManualActionListProps {
  items: ManualItem[];
  disabled: boolean;
  onAction: (action: ManualActionDto) => void;
  // Called after an "open" item ran (e.g. to close the menu).
  onDone?: () => void;
}

function NumberItem({
  disabled,
  item,
  onAction,
}: {
  disabled: boolean;
  item: Extract<ManualItem, { kind: 'number' }>;
  onAction: (action: ManualActionDto) => void;
}) {
  const [value, setValue] = useState(String(item.defaultValue));
  const number = Number(value);
  const isValid = Number.isFinite(number) && number !== 0 && number >= item.min && number <= item.max;

  return (
    <form
      className="manual-actions__number"
      onSubmit={(event) => {
        event.preventDefault();
        if (isValid) {
          onAction(item.toAction(Math.round(number)));
        }
      }}
    >
      <label>
        <span>{item.label}</span>
        <input
          inputMode="numeric"
          max={item.max}
          min={item.min}
          onChange={(event) => setValue(event.target.value)}
          step={item.step}
          type="number"
          value={value}
        />
      </label>
      <button disabled={disabled || !isValid} type="submit">
        OK
      </button>
    </form>
  );
}

// The list of manual actions, used in the context menu and in the inspect overlay.
export function ManualActionList({ disabled, items, onAction, onDone }: ManualActionListProps) {
  return (
    <div className="manual-actions">
      {items.map((item, index) => {
        switch (item.kind) {
          case 'heading':
            return (
              <p key={`h-${item.label}-${index}`} className="manual-actions__heading">
                {item.label}
              </p>
            );
          case 'number':
            return <NumberItem key={`n-${item.label}`} disabled={disabled} item={item} onAction={onAction} />;
          case 'open':
            return (
              <button
                key={`o-${item.label}`}
                className="manual-actions__item"
                onClick={() => {
                  item.run();
                  onDone?.();
                }}
                type="button"
              >
                {item.label}
              </button>
            );
          default:
            return (
              <button
                key={`a-${item.label}-${index}`}
                className="manual-actions__item"
                disabled={disabled || item.disabled}
                onClick={() => onAction(item.action)}
                type="button"
              >
                {item.label}
              </button>
            );
        }
      })}
    </div>
  );
}

interface ManualMenuProps {
  menu: ManualMenuState | null;
  disabled: boolean;
  onAction: (action: ManualActionDto) => void;
  onClose: () => void;
}

const EDGE_PX = 8;

// Context menu for manual actions (right click, long press or the "⋯" button).
// It lives in document.body, like the drag ghost, because the board frame is a size container.
export function ManualMenu({ disabled, menu, onAction, onClose }: ManualMenuProps) {
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menu) {
      return undefined;
    }

    function handlePointerDown(event: PointerEvent): void {
      if (!ref.current?.contains(event.target as Node)) {
        onClose();
      }
    }

    function handleKeyDown(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        onClose();
      }
    }

    document.addEventListener('pointerdown', handlePointerDown);
    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('resize', onClose);
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown);
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('resize', onClose);
    };
  }, [menu, onClose]);

  // Keep the menu inside the window.
  useLayoutEffect(() => {
    const element = ref.current;
    if (!element || !menu) {
      return;
    }

    const rect = element.getBoundingClientRect();
    const left = Math.max(EDGE_PX, Math.min(menu.x, window.innerWidth - rect.width - EDGE_PX));
    const top = Math.max(EDGE_PX, Math.min(menu.y, window.innerHeight - rect.height - EDGE_PX));
    element.style.left = `${left}px`;
    element.style.top = `${top}px`;
  }, [menu]);

  if (!menu) {
    return null;
  }

  return createPortal(
    <div ref={ref} aria-label={`Manual actions: ${menu.title}`} className="manual-menu" role="menu">
      <header className="manual-menu__title">
        <span aria-hidden="true">✋</span> {menu.title}
      </header>
      <ManualActionList disabled={disabled} items={menu.items} onAction={onAction} onDone={onClose} />
    </div>,
    document.body,
  );
}

interface ManualConfirmProps {
  text: string | null;
  onConfirm: () => void;
  onCancel: () => void;
}

// Asks before a drag & drop move that is not part of the normal game flow.
export function ManualConfirm({ onCancel, onConfirm, text }: ManualConfirmProps) {
  useEffect(() => {
    if (!text) {
      return undefined;
    }

    function handleKeyDown(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        onCancel();
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [onCancel, text]);

  if (!text) {
    return null;
  }

  return (
    <div className="manual-confirm" role="presentation" onMouseDown={onCancel}>
      <section
        aria-label="Manuelle Aktion bestätigen"
        aria-modal="true"
        className="manual-confirm__dialog"
        onMouseDown={(event) => event.stopPropagation()}
        role="alertdialog"
      >
        <p className="manual-confirm__kicker">✋ Manuell – nicht der normale Spielweg</p>
        <p>{text}</p>
        <div className="manual-confirm__actions">
          <button className="is-primary" onClick={onConfirm} type="button">
            Ausführen
          </button>
          <button onClick={onCancel} type="button">
            Abbrechen
          </button>
        </div>
      </section>
    </div>
  );
}
