import { useState } from 'react';

import { Tabs } from '../../components/ui/Tabs';
import { BountyLeaderboard } from './BountyLeaderboard';

const TABS = [{ id: 'bounty', label: 'Bounty-Rangliste' }];

export function MetaPage() {
  const [activeTab, setActiveTab] = useState('bounty');

  return (
    <section className="meta-page ui-page">
      <header className="ui-page-header">
        <div>
          <span className="ui-kicker">OneSake · Meta</span>
          <h1 className="ui-title">Meta &amp; Rangliste</h1>
        </div>
      </header>

      <Tabs activeId={activeTab} ariaLabel="Meta-Ansichten" items={TABS} onChange={setActiveTab} />

      {activeTab === 'bounty' && <BountyLeaderboard />}
    </section>
  );
}
