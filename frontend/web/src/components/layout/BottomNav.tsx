import type { ReactNode } from 'react';

import { MetaIcon } from '../ui/Icons';

export type AppSection = 'match' | 'deckbuilder' | 'meta' | 'tools' | 'profile';

interface BottomNavItem {
  id: AppSection;
  label: string;
  icon: ReactNode;
}

const iconProps = {
  'aria-hidden': true,
  fill: 'none',
  height: 22,
  stroke: 'currentColor',
  strokeLinecap: 'round' as const,
  strokeLinejoin: 'round' as const,
  strokeWidth: 1.8,
  viewBox: '0 0 24 24',
  width: 22,
};

const NAV_ITEMS: BottomNavItem[] = [
  {
    id: 'match',
    label: 'Match',
    // Crossed swords
    icon: (
      <svg {...iconProps}>
        <path d="M3 3h3l11 11-3 3L3 6z" />
        <path d="M21 3h-3l-5.5 5.5 3 3L21 6z" />
        <path d="M13 19l6-6M16 16l4 4M5 14l4 4M7 17l-3 3" />
      </svg>
    ),
  },
  {
    id: 'deckbuilder',
    label: 'Deckbuilder',
    // Stack of cards
    icon: (
      <svg {...iconProps}>
        <rect height="14" rx="2" width="10" x="9" y="6" />
        <path d="M6 17V6a2 2 0 0 1 2-2h7M3 14V5a2 2 0 0 1 2-2" />
      </svg>
    ),
  },
  {
    id: 'meta',
    label: 'Meta',
    icon: <MetaIcon />,
  },
  {
    id: 'tools',
    label: 'Tools',
    // Wrench
    icon: (
      <svg {...iconProps}>
        <path d="M14.5 6.5a4 4 0 0 0 5 5L12 19a2.1 2.1 0 0 1-3-3l7.5-7.5a4 4 0 0 1-2-2z" />
        <path d="M14.5 6.5l2-2 3 3-2 2" />
      </svg>
    ),
  },
  {
    id: 'profile',
    label: 'Profile',
    // Person
    icon: (
      <svg {...iconProps}>
        <circle cx="12" cy="8" r="4" />
        <path d="M4 21a8 8 0 0 1 16 0" />
      </svg>
    ),
  },
];

interface BottomNavProps {
  activeSection: AppSection;
  onChange: (section: AppSection) => void;
}

export function BottomNav({ activeSection, onChange }: BottomNavProps) {
  return (
    <nav aria-label="Main navigation" className="bottom-nav">
      {NAV_ITEMS.map((item) => (
        <button
          key={item.id}
          aria-current={item.id === activeSection ? 'page' : undefined}
          className="bottom-nav__item"
          onClick={() => onChange(item.id)}
          type="button"
        >
          <span className="bottom-nav__icon">{item.icon}</span>
          <span className="bottom-nav__label">{item.label}</span>
        </button>
      ))}
    </nav>
  );
}
