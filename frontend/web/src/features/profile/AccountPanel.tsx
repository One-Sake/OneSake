import { useState, type FormEvent } from 'react';

import {
  AuthApiError,
  changePassword,
  login,
  logout,
  logoutEverywhere,
  register,
  resetPassword,
  type AccountDto,
} from '../../api/authApi';

// Login, register, "forgot password" (recovery code) and password change in the profile.

type Mode = 'login' | 'register' | 'reset' | 'change';

const PASSWORD_MIN_LENGTH = 10;

interface AccountPanelProps {
  account: AccountDto | null;
  onAccountChange: (account: AccountDto | null) => void;
}

function describeError(error: unknown): string {
  if (error instanceof AuthApiError) {
    if (error.code === 'LOCKED_OUT' && error.retryAfterSeconds) {
      return `${error.message} (noch ca. ${Math.ceil(error.retryAfterSeconds / 60)} Min.)`;
    }

    return error.message;
  }

  return 'Unbekannter Fehler.';
}

function downloadCode(username: string, code: string): void {
  const text = `OneSake Wiederherstellungscode\nBenutzername: ${username}\nCode: ${code}\n\nDer Code funktioniert genau einmal. Danach bekommst du einen neuen.\n`;
  const url = URL.createObjectURL(new Blob([text], { type: 'text/plain' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = `onesake-wiederherstellungscode-${username}.txt`;
  link.click();
  URL.revokeObjectURL(url);
}

export function AccountPanel({ account, onAccountChange }: AccountPanelProps) {
  const [mode, setMode] = useState<Mode>('login');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [recoveryCode, setRecoveryCode] = useState('');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [isBusy, setIsBusy] = useState(false);
  // Shown once after register/reset until the player confirms that it is saved.
  const [shownCode, setShownCode] = useState<{ username: string; code: string } | null>(null);
  const [isCopied, setIsCopied] = useState(false);

  const switchMode = (next: Mode): void => {
    setMode(next);
    setError('');
    setNotice('');
    setPassword('');
    setNewPassword('');
    setRecoveryCode('');
  };

  const run = async (action: () => Promise<void>): Promise<void> => {
    setIsBusy(true);
    setError('');
    setNotice('');
    try {
      await action();
    } catch (actionError) {
      setError(describeError(actionError));
    } finally {
      setIsBusy(false);
    }
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>): void => {
    event.preventDefault();
    void run(async () => {
      if (mode === 'login') {
        const result = await login(username, password);
        setPassword('');
        onAccountChange(result);
        return;
      }

      if (mode === 'register') {
        const result = await register(username, password);
        setPassword('');
        // After a later logout the panel shows the login form again.
        setMode('login');
        setShownCode({ username: result.username, code: result.recoveryCode });
        onAccountChange({ username: result.username });
        return;
      }

      if (mode === 'reset') {
        const result = await resetPassword(username, recoveryCode, newPassword);
        setRecoveryCode('');
        setNewPassword('');
        setMode('login');
        setShownCode({ username: result.username, code: result.recoveryCode });
        onAccountChange({ username: result.username });
        return;
      }

      await changePassword(password, newPassword);
      setPassword('');
      setNewPassword('');
      setMode('login');
      setNotice('Passwort geändert. Andere Geräte wurden abgemeldet.');
    });
  };

  const copyCode = (code: string): void => {
    void navigator.clipboard
      ?.writeText(code)
      .then(() => setIsCopied(true))
      .catch(() => setIsCopied(false));
  };

  if (shownCode) {
    return (
      <section className="ui-glass account-panel" aria-labelledby="account-code-title">
        <h2 className="ui-heading" id="account-code-title">
          Dein Wiederherstellungscode
        </h2>
        <p>
          Mit diesem Code kannst du ein neues Passwort setzen, falls du deines vergisst. Er wird{' '}
          <strong>nur jetzt</strong> angezeigt und funktioniert genau einmal. Speichere ihn an einem sicheren Ort.
        </p>
        <code className="account-code" aria-label="Wiederherstellungscode">
          {shownCode.code}
        </code>
        <div className="profile-actions">
          <button className="ui-btn-secondary" onClick={() => copyCode(shownCode.code)} type="button">
            {isCopied ? 'Kopiert' : 'Kopieren'}
          </button>
          <button
            className="ui-btn-secondary"
            onClick={() => downloadCode(shownCode.username, shownCode.code)}
            type="button"
          >
            Als Datei speichern
          </button>
          <button
            className="ui-btn-primary ui-btn-primary--compact"
            onClick={() => {
              setShownCode(null);
              setIsCopied(false);
            }}
            type="button"
          >
            <span>Ich habe den Code gespeichert</span>
          </button>
        </div>
      </section>
    );
  }

  if (account && mode !== 'change') {
    return (
      <section className="ui-glass account-panel" aria-labelledby="account-title">
        <h2 className="ui-heading" id="account-title">
          Account
        </h2>
        <p>
          Angemeldet als <strong>{account.username}</strong>. Deine Matches und dein Kopfgeld werden diesem Account
          zugeordnet.
        </p>
        {notice && (
          <p className="account-notice" role="status">
            {notice}
          </p>
        )}
        {error && (
          <p className="account-error" role="alert">
            {error}
          </p>
        )}
        <div className="profile-actions">
          <button className="ui-btn-secondary" onClick={() => switchMode('change')} type="button">
            Passwort ändern
          </button>
          <button
            className="ui-btn-ghost"
            disabled={isBusy}
            onClick={() => void run(async () => {
              await logout();
              onAccountChange(null);
            })}
            type="button"
          >
            Abmelden
          </button>
          <button
            className="ui-link ui-link--danger"
            disabled={isBusy}
            onClick={() => void run(async () => {
              await logoutEverywhere();
              onAccountChange(null);
            })}
            title="Zum Beispiel, wenn jemand anderer dein Passwort kennt"
            type="button"
          >
            Auf allen Geräten abmelden
          </button>
        </div>
      </section>
    );
  }

  const titles: Record<Mode, string> = {
    login: 'Anmelden',
    register: 'Account erstellen',
    reset: 'Passwort vergessen',
    change: 'Passwort ändern',
  };

  return (
    <section className="ui-glass account-panel" aria-labelledby="account-form-title">
      <h2 className="ui-heading" id="account-form-title">
        {titles[mode]}
      </h2>

      {mode !== 'change' && (
        <div className="account-tabs" role="tablist" aria-label="Account">
          {(['login', 'register'] as const).map((tab) => (
            <button
              aria-selected={mode === tab}
              className={mode === tab ? 'ui-btn-secondary' : 'ui-btn-ghost'}
              key={tab}
              onClick={() => switchMode(tab)}
              role="tab"
              type="button"
            >
              {titles[tab]}
            </button>
          ))}
        </div>
      )}

      {mode === 'register' && (
        <p className="account-hint">
          Deine bisherigen Matches in diesem Browser werden übernommen. Nach dem Erstellen bekommst du einen
          Wiederherstellungscode für den Fall, dass du dein Passwort vergisst.
        </p>
      )}
      {mode === 'reset' && (
        <p className="account-hint">Gib deinen Benutzernamen, deinen Wiederherstellungscode und ein neues Passwort ein.</p>
      )}

      <form className="account-form" onSubmit={handleSubmit}>
        {mode !== 'change' && (
          <label className="account-field">
            <span className="ui-label">Benutzername</span>
            <input
              autoComplete="username"
              className="ui-plate-input"
              maxLength={20}
              onChange={(event) => setUsername(event.target.value)}
              required
              value={username}
            />
          </label>
        )}

        {(mode === 'login' || mode === 'register' || mode === 'change') && (
          <label className="account-field">
            <span className="ui-label">{mode === 'change' ? 'Aktuelles Passwort' : 'Passwort'}</span>
            <input
              autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
              className="ui-plate-input"
              maxLength={128}
              minLength={mode === 'register' ? PASSWORD_MIN_LENGTH : undefined}
              onChange={(event) => setPassword(event.target.value)}
              required
              type="password"
              value={password}
            />
          </label>
        )}

        {mode === 'reset' && (
          <label className="account-field">
            <span className="ui-label">Wiederherstellungscode</span>
            <input
              autoCapitalize="characters"
              autoComplete="off"
              className="ui-plate-input"
              onChange={(event) => setRecoveryCode(event.target.value)}
              placeholder="XXXX-XXXX-XXXX-XXXX"
              required
              spellCheck={false}
              value={recoveryCode}
            />
          </label>
        )}

        {(mode === 'reset' || mode === 'change') && (
          <label className="account-field">
            <span className="ui-label">Neues Passwort</span>
            <input
              autoComplete="new-password"
              className="ui-plate-input"
              maxLength={128}
              minLength={PASSWORD_MIN_LENGTH}
              onChange={(event) => setNewPassword(event.target.value)}
              required
              type="password"
              value={newPassword}
            />
          </label>
        )}

        {(mode === 'register' || mode === 'reset' || mode === 'change') && (
          <small className="account-hint">Mindestens {PASSWORD_MIN_LENGTH} Zeichen. Ein langer Satz ist ideal.</small>
        )}

        {error && (
          <p className="account-error" role="alert">
            {error}
          </p>
        )}

        <div className="profile-actions">
          <button className="ui-btn-primary ui-btn-primary--compact" disabled={isBusy} type="submit">
            <span>{isBusy ? '…' : titles[mode]}</span>
          </button>
          {mode === 'login' && (
            <button className="ui-link" onClick={() => switchMode('reset')} type="button">
              Passwort vergessen?
            </button>
          )}
          {(mode === 'reset' || mode === 'change') && (
            <button className="ui-link" onClick={() => switchMode('login')} type="button">
              Abbrechen
            </button>
          )}
        </div>
      </form>
    </section>
  );
}
