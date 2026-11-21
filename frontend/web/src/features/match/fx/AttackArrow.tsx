import { useEffect, useRef } from 'react';
import type { RefObject } from 'react';

import { cardElement } from './fxDom';

interface AttackArrowProps {
  attackerId: string;
  frameRef: RefObject<HTMLElement>;
}

// Curved golden arrow from the attacking card to the pointer while an attack target is chosen.
// It only draws; clicking a target still uses the normal board handlers.
export function AttackArrow({ attackerId, frameRef }: AttackArrowProps) {
  const pathRef = useRef<SVGPathElement>(null);
  const headRef = useRef<SVGPolygonElement>(null);

  useEffect(() => {
    const frame = frameRef.current;
    if (!frame) {
      return undefined;
    }

    let pointer: { x: number; y: number } | null = null;
    let frameRequest = 0;

    function draw(): void {
      frameRequest = 0;
      const attacker = cardElement(frame, attackerId);
      const path = pathRef.current;
      const head = headRef.current;
      if (!attacker || !path || !head || !frame) {
        return;
      }

      const base = frame.getBoundingClientRect();
      const rect = attacker.getBoundingClientRect();
      const startX = rect.left + rect.width / 2 - base.left;
      const startY = rect.top + rect.height / 2 - base.top;
      // Before the pointer moved: point straight up across the board.
      const endX = pointer ? pointer.x - base.left : startX;
      const endY = pointer ? pointer.y - base.top : startY - base.height * 0.3;
      const distance = Math.hypot(endX - startX, endY - startY);
      // Control point: halfway, bent to the side so the arrow forms an arc.
      const controlX = (startX + endX) / 2 - (endY - startY) * 0.25;
      const controlY = (startY + endY) / 2 - distance * 0.25;
      path.setAttribute('d', `M ${startX} ${startY} Q ${controlX} ${controlY} ${endX} ${endY}`);

      const angle = Math.atan2(endY - controlY, endX - controlX);
      const size = 16;
      const points = [
        [endX + Math.cos(angle) * size * 0.6, endY + Math.sin(angle) * size * 0.6],
        [endX + Math.cos(angle + 2.5) * size, endY + Math.sin(angle + 2.5) * size],
        [endX + Math.cos(angle - 2.5) * size, endY + Math.sin(angle - 2.5) * size],
      ];
      head.setAttribute('points', points.map((point) => point.join(',')).join(' '));
    }

    function handleMove(event: PointerEvent): void {
      pointer = { x: event.clientX, y: event.clientY };
      if (!frameRequest) {
        frameRequest = requestAnimationFrame(draw);
      }
    }

    draw();
    window.addEventListener('pointermove', handleMove, { passive: true });
    window.addEventListener('pointerdown', handleMove, { passive: true });
    return () => {
      window.removeEventListener('pointermove', handleMove);
      window.removeEventListener('pointerdown', handleMove);
      cancelAnimationFrame(frameRequest);
    };
  }, [attackerId, frameRef]);

  return (
    <svg aria-hidden="true" className="fx-arrow">
      <path ref={pathRef} />
      <polygon ref={headRef} />
    </svg>
  );
}
