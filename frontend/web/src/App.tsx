import { useState } from 'react';

import { loadRejoinData } from './api/rejoinStorage';
import { AppShell, type Page } from './components/layout/AppShell';
import { StartScreen } from './features/start/StartScreen';

function App() {
  // A stored seat (match running before the reload): skip the start screen, the match page offers
  // "Back to the match" right away.
  const [hasStarted, setHasStarted] = useState(() => loadRejoinData() !== null);
  const [initialPage, setInitialPage] = useState<Page>('match');

  function startApp(page: Page) {
    setInitialPage(page);
    setHasStarted(true);
  }

  if (!hasStarted) {
    return (
      <StartScreen
        onPlay={() => startApp('match')}
        onLogin={() => startApp('profile')}
      />
    );
  }

  return <AppShell initialPage={initialPage} />;
}

export default App;
