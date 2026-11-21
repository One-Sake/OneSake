import { useEffect, useRef, useState } from 'react';

import { updateFxSettings, useFxSettings, type AnimationMode } from './fxSettings';
import { playSound } from './sounds';

const ANIMATION_OPTIONS: { value: AnimationMode; label: string }[] = [
  { value: 'normal', label: 'Normal' },
  { value: 'fast', label: 'Schnell' },
  { value: 'off', label: 'Aus' },
];

// Gear button in the match top bar: animations, sound, vibration, auto pass.
export function FxSettingsMenu() {
  const settings = useFxSettings();
  const [open, setOpen] = useState(false);
  // The top bar clips its content, so the panel is placed fixed below the button.
  const [anchor, setAnchor] = useState({ top: 56, right: 12 });
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);

  function toggle(): void {
    const rect = buttonRef.current?.getBoundingClientRect();
    if (rect) {
      setAnchor({ top: rect.bottom + 6, right: Math.max(8, window.innerWidth - rect.right) });
    }

    setOpen((value) => !value);
  }

  useEffect(() => {
    if (!open) {
      return undefined;
    }

    function handlePointerDown(event: PointerEvent): void {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function handleKey(event: KeyboardEvent): void {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    }

    document.addEventListener('pointerdown', handlePointerDown);
    document.addEventListener('keydown', handleKey);
    return () => {
      document.removeEventListener('pointerdown', handlePointerDown);
      document.removeEventListener('keydown', handleKey);
    };
  }, [open]);

  return (
    <div ref={rootRef} className="fx-settings">
      <button
        aria-expanded={open}
        aria-label="Match-Einstellungen"
        className="match-fullscreen__toggle fx-settings__button"
        onClick={toggle}
        ref={buttonRef}
        title="Match-Einstellungen"
        type="button"
      >
        ⚙
      </button>
      {open && (
        <div
          aria-label="Match-Einstellungen"
          className="fx-settings__panel"
          role="dialog"
          style={{ top: anchor.top, right: anchor.right }}
        >
          <fieldset className="fx-settings__group">
            <legend>Animationen</legend>
            <div className="fx-settings__segments">
              {ANIMATION_OPTIONS.map((option) => (
                <label key={option.value} className={settings.animations === option.value ? 'is-active' : ''}>
                  <input
                    checked={settings.animations === option.value}
                    name="fx-animations"
                    onChange={() => updateFxSettings({ animations: option.value })}
                    type="radio"
                    value={option.value}
                  />
                  {option.label}
                </label>
              ))}
            </div>
          </fieldset>

          <div className="fx-settings__group">
            <span className="fx-settings__label">Sound</span>
            <label className="fx-settings__check">
              <input
                checked={!settings.soundMuted}
                onChange={(event) => updateFxSettings({ soundMuted: !event.target.checked })}
                type="checkbox"
              />
              Sound an
            </label>
            <label className="fx-settings__volume">
              <span>Lautstärke</span>
              <input
                aria-label="Lautstärke"
                disabled={settings.soundMuted}
                max={100}
                min={0}
                onChange={(event) => updateFxSettings({ volume: Number(event.target.value) / 100 })}
                onPointerUp={() => playSound('play')}
                step={5}
                type="range"
                value={Math.round(settings.volume * 100)}
              />
              <output>{Math.round(settings.volume * 100)} %</output>
            </label>
          </div>

          <label className="fx-settings__check">
            <input
              checked={settings.vibration}
              onChange={(event) => updateFxSettings({ vibration: event.target.checked })}
              type="checkbox"
            />
            Vibration bei Treffern (Handy)
          </label>
        </div>
      )}
    </div>
  );
}
