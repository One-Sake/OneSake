import { useEffect, useState } from 'react';

import { formatBountyChange } from '../../api/leaderboardApi';

const SHOW_MS = 6000;

interface BountyChangeBannerProps {
  // From useBountyChange (shared with the game over screen).
  change: number | null;
  // The game over screen shows the change itself.
  hidden?: boolean;
}

// Short "Kopfgeld +฿ 24.000" after a ranked match.
export function BountyChangeBanner({ change, hidden = false }: BountyChangeBannerProps) {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    if (change === null) {
      setVisible(false);
      return undefined;
    }

    setVisible(true);
    const timeoutId = window.setTimeout(() => setVisible(false), SHOW_MS);
    return () => window.clearTimeout(timeoutId);
  }, [change]);

  if (change === null || !visible || hidden) {
    return null;
  }

  return (
    <div className={`bounty-banner ${change >= 0 ? 'is-up' : 'is-down'}`} role="status">
      <span>Kopfgeld</span>
      <strong>{formatBountyChange(change)}</strong>
    </div>
  );
}
