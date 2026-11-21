import { forwardRef } from 'react';

// Overlay over the board for flying cards, numbers, arrows and particles. It never catches
// pointer events, so the board below stays fully usable while something animates.
export const FxLayer = forwardRef<HTMLDivElement>(function FxLayer(_props, ref) {
  return <div ref={ref} aria-hidden="true" className="fx-layer" />;
});
