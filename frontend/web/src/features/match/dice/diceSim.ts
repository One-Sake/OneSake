// Small deterministic rigid-body simulation for two dice (pure TypeScript, no DOM).
//
// Both clients run the same simulation with the same seed, so they see the same path.
// The server already decided the values: after the simulation we only choose which pip
// numbers are painted on which face, so that the server value ends up on top.
//
// Units: one die edge = 1. The table is TABLE_WIDTH x TABLE_HEIGHT, centered at (0, 0),
// z points up. Die 0 starts on the bottom side (y < 0), die 1 on the top side (y > 0).

export const TABLE_WIDTH = 12;
export const TABLE_HEIGHT = 8;
export const DIE_SIZE = 1;
export const SIM_DT = 1 / 120;

const HALF = DIE_SIZE / 2;
const GRAVITY = 42;
const RESTITUTION = 0.35;
const FRICTION = 0.4;
const WALL_RESTITUTION = 0.5;
const WALL_FRICTION = 0.2;
const LINEAR_DAMPING = 0.12;
const ANGULAR_DAMPING = 0.35;
const MASS = 1;
const INV_MASS = 1 / MASS;
// Cube inertia around its center: m * L^2 / 6 (the same for every axis).
const INV_INERTIA = 6 / (MASS * DIE_SIZE * DIE_SIZE);
const DIE_RADIUS = 0.62;
const REST_LINEAR = 0.25;
const REST_ANGULAR = 0.35;
const REST_STEPS = 30;
const MIN_ROLL_TIME = 0.6;
// Extra damping after this time so the dice settle before the hard limit.
const SETTLE_TIME = 2.2;
const MAX_ROLL_TIME = 3.05;
const SNAP_TIME = 0.15;

export type FaceKey = '+x' | '-x' | '+y' | '-y' | '+z' | '-z';

export const FACE_KEYS: FaceKey[] = ['+x', '-x', '+y', '-y', '+z', '-z'];

export interface DiceImpact {
  time: number;
  die: number;
  // Speed into the surface at the impact (for the sound volume).
  strength: number;
}

export interface DiceSimResult {
  // Per die: 7 floats per frame (x, y, z, qw, qx, qy, qz), one frame per SIM_DT.
  frames: Float32Array[];
  frameCount: number;
  duration: number;
  impacts: DiceImpact[];
  // Per die: which pip number is painted on which local face.
  faceValues: Record<FaceKey, number>[];
  topFaces: FaceKey[];
}

type Vec3 = [number, number, number];
type Quat = [number, number, number, number];

interface Body {
  p: Vec3;
  v: Vec3;
  q: Quat;
  w: Vec3;
  restSteps: number;
}

// ---------- Seeded random numbers (mulberry32) ----------

export function mulberry32(seed: number): () => number {
  let state = seed >>> 0;
  return () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let t = state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function range(random: () => number, min: number, max: number): number {
  return min + (max - min) * random();
}

// ---------- Small vector and quaternion helpers ----------

function cross(a: Vec3, b: Vec3): Vec3 {
  return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
}

function dot(a: Vec3, b: Vec3): number {
  return a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
}

function length(a: Vec3): number {
  return Math.sqrt(dot(a, a));
}

function quatMultiply(a: Quat, b: Quat): Quat {
  return [
    a[0] * b[0] - a[1] * b[1] - a[2] * b[2] - a[3] * b[3],
    a[0] * b[1] + a[1] * b[0] + a[2] * b[3] - a[3] * b[2],
    a[0] * b[2] - a[1] * b[3] + a[2] * b[0] + a[3] * b[1],
    a[0] * b[3] + a[1] * b[2] - a[2] * b[1] + a[3] * b[0],
  ];
}

function quatNormalize(q: Quat): Quat {
  const n = Math.hypot(q[0], q[1], q[2], q[3]) || 1;
  return [q[0] / n, q[1] / n, q[2] / n, q[3] / n];
}

// Row-major 3x3 rotation matrix of a unit quaternion.
export function quatToMatrix(q: ArrayLike<number>): number[] {
  const [w, x, y, z] = [q[0], q[1], q[2], q[3]];
  return [
    1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y),
    2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x),
    2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y),
  ];
}

function rotate(m: number[], v: Vec3): Vec3 {
  return [
    m[0] * v[0] + m[1] * v[1] + m[2] * v[2],
    m[3] * v[0] + m[4] * v[1] + m[5] * v[2],
    m[6] * v[0] + m[7] * v[1] + m[8] * v[2],
  ];
}

function quatSlerp(a: Quat, b: Quat, t: number): Quat {
  let cosTheta = a[0] * b[0] + a[1] * b[1] + a[2] * b[2] + a[3] * b[3];
  let target = b;
  if (cosTheta < 0) {
    cosTheta = -cosTheta;
    target = [-b[0], -b[1], -b[2], -b[3]];
  }

  if (cosTheta > 0.9995) {
    return quatNormalize([
      a[0] + (target[0] - a[0]) * t,
      a[1] + (target[1] - a[1]) * t,
      a[2] + (target[2] - a[2]) * t,
      a[3] + (target[3] - a[3]) * t,
    ]);
  }

  const theta = Math.acos(cosTheta);
  const sinTheta = Math.sin(theta);
  const wa = Math.sin((1 - t) * theta) / sinTheta;
  const wb = Math.sin(t * theta) / sinTheta;
  return [
    a[0] * wa + target[0] * wb,
    a[1] * wa + target[1] * wb,
    a[2] * wa + target[2] * wb,
    a[3] * wa + target[3] * wb,
  ];
}

// Uniform random rotation (Shoemake).
function randomQuat(random: () => number): Quat {
  const u1 = random();
  const u2 = random() * Math.PI * 2;
  const u3 = random() * Math.PI * 2;
  const a = Math.sqrt(1 - u1);
  const b = Math.sqrt(u1);
  return [a * Math.sin(u2), a * Math.cos(u2), b * Math.sin(u3), b * Math.cos(u3)];
}

const CORNERS: Vec3[] = [];
for (const x of [-HALF, HALF]) {
  for (const y of [-HALF, HALF]) {
    for (const z of [-HALF, HALF]) {
      CORNERS.push([x, y, z]);
    }
  }
}

// Planes the dice can hit: the table and the four side walls (normal points into the table).
const PLANES: { normal: Vec3; offset: number; restitution: number; friction: number; isFloor: boolean }[] = [
  { normal: [0, 0, 1], offset: 0, restitution: RESTITUTION, friction: FRICTION, isFloor: true },
  { normal: [1, 0, 0], offset: -TABLE_WIDTH / 2, restitution: WALL_RESTITUTION, friction: WALL_FRICTION, isFloor: false },
  { normal: [-1, 0, 0], offset: -TABLE_WIDTH / 2, restitution: WALL_RESTITUTION, friction: WALL_FRICTION, isFloor: false },
  { normal: [0, 1, 0], offset: -TABLE_HEIGHT / 2, restitution: WALL_RESTITUTION, friction: WALL_FRICTION, isFloor: false },
  { normal: [0, -1, 0], offset: -TABLE_HEIGHT / 2, restitution: WALL_RESTITUTION, friction: WALL_FRICTION, isFloor: false },
];

// Applies an impulse at the contact point r (relative to the center).
function applyImpulse(body: Body, r: Vec3, impulse: Vec3): void {
  body.v[0] += impulse[0] * INV_MASS;
  body.v[1] += impulse[1] * INV_MASS;
  body.v[2] += impulse[2] * INV_MASS;
  const torque = cross(r, impulse);
  body.w[0] += torque[0] * INV_INERTIA;
  body.w[1] += torque[1] * INV_INERTIA;
  body.w[2] += torque[2] * INV_INERTIA;
}

function pointVelocity(body: Body, r: Vec3): Vec3 {
  const spin = cross(body.w, r);
  return [body.v[0] + spin[0], body.v[1] + spin[1], body.v[2] + spin[2]];
}

// Contact of the 8 corners with the table and the walls: normal impulse with restitution,
// then a friction impulse (Coulomb, limited by mu * normal impulse). Hitting with a corner
// creates torque, which makes the die tip over and tumble.
function collideWithPlanes(body: Body, time: number, dieIndex: number, impacts: DiceImpact[]): void {
  const m = quatToMatrix(body.q);

  for (const plane of PLANES) {
    let deepest = 0;

    for (const corner of CORNERS) {
      const r = rotate(m, corner);
      const point: Vec3 = [body.p[0] + r[0], body.p[1] + r[1], body.p[2] + r[2]];
      const distance = dot(point, plane.normal) - plane.offset;
      if (distance >= 0) {
        continue;
      }

      deepest = Math.min(deepest, distance);
      const n = plane.normal;
      const vn = dot(pointVelocity(body, r), n);
      if (vn >= 0) {
        continue;
      }

      // Slow contacts do not bounce (avoids endless jitter on the table).
      const restitution = vn > -1 ? 0 : plane.restitution;
      const rn = cross(r, n);
      const normalImpulse = (-(1 + restitution) * vn) / (INV_MASS + INV_INERTIA * dot(rn, rn));
      applyImpulse(body, r, [n[0] * normalImpulse, n[1] * normalImpulse, n[2] * normalImpulse]);

      if (vn < -1.5) {
        impacts.push({ time, die: dieIndex, strength: -vn });
      }

      const velocity = pointVelocity(body, r);
      const vnAfter = dot(velocity, n);
      const tangent: Vec3 = [
        velocity[0] - n[0] * vnAfter,
        velocity[1] - n[1] * vnAfter,
        velocity[2] - n[2] * vnAfter,
      ];
      const tangentSpeed = length(tangent);
      if (tangentSpeed < 1e-6) {
        continue;
      }

      const t: Vec3 = [tangent[0] / tangentSpeed, tangent[1] / tangentSpeed, tangent[2] / tangentSpeed];
      const rt = cross(r, t);
      const maxFriction = plane.friction * normalImpulse;
      const frictionImpulse = Math.min(tangentSpeed / (INV_MASS + INV_INERTIA * dot(rt, rt)), maxFriction);
      applyImpulse(body, r, [-t[0] * frictionImpulse, -t[1] * frictionImpulse, -t[2] * frictionImpulse]);
    }

    // Push the die out of the surface so it does not sink in.
    if (deepest < 0) {
      body.p[0] -= plane.normal[0] * deepest;
      body.p[1] -= plane.normal[1] * deepest;
      body.p[2] -= plane.normal[2] * deepest;
    }
  }
}

// The dice bounce off each other like two spheres (good enough for two dice).
function collideDice(a: Body, b: Body): void {
  const delta: Vec3 = [b.p[0] - a.p[0], b.p[1] - a.p[1], b.p[2] - a.p[2]];
  const distance = length(delta);
  if (distance >= DIE_RADIUS * 2 || distance < 1e-6) {
    return;
  }

  const n: Vec3 = [delta[0] / distance, delta[1] / distance, delta[2] / distance];
  const relative = dot([b.v[0] - a.v[0], b.v[1] - a.v[1], b.v[2] - a.v[2]], n);
  const overlap = DIE_RADIUS * 2 - distance;

  a.p = [a.p[0] - n[0] * overlap / 2, a.p[1] - n[1] * overlap / 2, a.p[2] - n[2] * overlap / 2];
  b.p = [b.p[0] + n[0] * overlap / 2, b.p[1] + n[1] * overlap / 2, b.p[2] + n[2] * overlap / 2];

  if (relative >= 0) {
    return;
  }

  const impulse = (-(1 + WALL_RESTITUTION) * relative) / (2 * INV_MASS);
  a.v = [a.v[0] - n[0] * impulse, a.v[1] - n[1] * impulse, a.v[2] - n[2] * impulse];
  b.v = [b.v[0] + n[0] * impulse, b.v[1] + n[1] * impulse, b.v[2] + n[2] * impulse];
}

function integrate(body: Body, time: number): void {
  const extra = time > SETTLE_TIME ? 4 : 1;
  body.v[2] -= GRAVITY * SIM_DT;

  const linear = Math.max(0, 1 - LINEAR_DAMPING * extra * SIM_DT);
  const angular = Math.max(0, 1 - ANGULAR_DAMPING * extra * SIM_DT);
  body.v = [body.v[0] * linear, body.v[1] * linear, body.v[2] * linear];
  body.w = [body.w[0] * angular, body.w[1] * angular, body.w[2] * angular];

  body.p = [body.p[0] + body.v[0] * SIM_DT, body.p[1] + body.v[1] * SIM_DT, body.p[2] + body.v[2] * SIM_DT];
  const spin: Quat = [0, body.w[0], body.w[1], body.w[2]];
  const dq = quatMultiply(spin, body.q);
  body.q = quatNormalize([
    body.q[0] + 0.5 * SIM_DT * dq[0],
    body.q[1] + 0.5 * SIM_DT * dq[1],
    body.q[2] + 0.5 * SIM_DT * dq[2],
    body.q[3] + 0.5 * SIM_DT * dq[3],
  ]);
}

// Which local face points up, and the rotation that makes it lie exactly flat.
export function findTopFace(q: Quat | ArrayLike<number>): { face: FaceKey; flat: Quat } {
  const quat: Quat = [q[0], q[1], q[2], q[3]];
  const m = quatToMatrix(quat);
  // Column a of the matrix is the world direction of local axis a; row 2 holds the z parts.
  const upParts = [m[6], m[7], m[8]];
  let axis = 0;
  for (let index = 1; index < 3; index += 1) {
    if (Math.abs(upParts[index]) > Math.abs(upParts[axis])) {
      axis = index;
    }
  }

  const sign = upParts[axis] >= 0 ? 1 : -1;
  const face = `${sign > 0 ? '+' : '-'}${'xyz'[axis]}` as FaceKey;
  const localNormal: Vec3 = [0, 0, 0];
  localNormal[axis] = sign;
  const worldNormal = rotate(m, localNormal);

  // Shortest rotation from worldNormal to straight up.
  const up: Vec3 = [0, 0, 1];
  const rotationAxis = cross(worldNormal, up);
  const sinAngle = length(rotationAxis);
  const cosAngle = dot(worldNormal, up);
  let delta: Quat = [1, 0, 0, 0];
  if (sinAngle > 1e-9) {
    const angle = Math.atan2(sinAngle, cosAngle);
    const s = Math.sin(angle / 2) / sinAngle;
    delta = [Math.cos(angle / 2), rotationAxis[0] * s, rotationAxis[1] * s, rotationAxis[2] * s];
  }

  return { face, flat: quatNormalize(quatMultiply(delta, quat)) };
}

// Paints the pips so that `value` is on the top face. Opposite faces always add up to 7.
export function assignFaceValues(topFace: FaceKey, value: number): Record<FaceKey, number> {
  const axis = topFace[1];
  const opposite = (topFace[0] === '+' ? '-' : '+') + axis;
  const result = { [topFace]: value, [opposite]: 7 - value } as Record<FaceKey, number>;
  const freePairs = [1, 2, 3].filter((low) => low !== value && low !== 7 - value);
  const freeAxes = ['x', 'y', 'z'].filter((entry) => entry !== axis);

  freeAxes.forEach((freeAxis, index) => {
    result[`+${freeAxis}` as FaceKey] = freePairs[index];
    result[`-${freeAxis}` as FaceKey] = 7 - freePairs[index];
  });

  return result;
}

function createBody(random: () => number, side: number): Body {
  return {
    p: [range(random, -TABLE_WIDTH * 0.3, TABLE_WIDTH * 0.3), side * (TABLE_HEIGHT / 2 - 0.9), range(random, 2.2, 3.6)],
    v: [range(random, -3.5, 3.5), -side * range(random, 6.5, 9.5), range(random, 0.5, 3)],
    q: randomQuat(random),
    w: [range(random, -16, 16), range(random, -16, 16), range(random, -16, 16)],
    restSteps: 0,
  };
}

function isAtRest(body: Body): boolean {
  return length(body.v) < REST_LINEAR && length(body.w) < REST_ANGULAR && body.p[2] < HALF + 0.05;
}

// Computes the whole roll in advance. `values` are the server results for die 0 and die 1.
export function simulateDiceRoll(seed: number, values: [number, number]): DiceSimResult {
  const bodies = [0, 1].map((index) => createBody(mulberry32((seed ^ Math.imul(index + 1, 0x9e3779b9)) >>> 0), index === 0 ? -1 : 1));
  const maxSteps = Math.ceil((MAX_ROLL_TIME + SNAP_TIME) / SIM_DT) + 2;
  const recorded: number[][] = [[], []];
  const impacts: DiceImpact[] = [];
  const snapSteps = Math.round(SNAP_TIME / SIM_DT);
  let snapStart = -1;
  let snapFrom: { q: Quat; z: number }[] = [];
  let snapTo: { q: Quat; face: FaceKey }[] = [];

  const record = (): void => {
    bodies.forEach((body, index) => {
      recorded[index].push(body.p[0], body.p[1], body.p[2], body.q[0], body.q[1], body.q[2], body.q[3]);
    });
  };

  record();

  for (let step = 1; step < maxSteps; step += 1) {
    const time = step * SIM_DT;

    if (snapStart < 0) {
      bodies.forEach((body, index) => {
        integrate(body, time);
        collideWithPlanes(body, time, index, impacts);
      });
      collideDice(bodies[0], bodies[1]);

      bodies.forEach((body) => {
        body.restSteps = isAtRest(body) ? body.restSteps + 1 : 0;
      });

      const bothResting = time > MIN_ROLL_TIME && bodies.every((body) => body.restSteps >= REST_STEPS);
      if (bothResting || time >= MAX_ROLL_TIME) {
        snapStart = step;
        snapFrom = bodies.map((body) => ({ q: [...body.q] as Quat, z: body.p[2] }));
        snapTo = bodies.map((body) => {
          const top = findTopFace(body.q);
          return { q: top.flat, face: top.face };
        });
      }
    } else {
      // Snap onto the nearest flat face (short slerp, at most SNAP_TIME).
      const progress = Math.min(1, (step - snapStart) / snapSteps);
      const eased = 1 - (1 - progress) * (1 - progress);
      bodies.forEach((body, index) => {
        body.q = quatSlerp(snapFrom[index].q, snapTo[index].q, eased);
        body.p[2] = snapFrom[index].z + (HALF - snapFrom[index].z) * eased;
      });

      if (progress >= 1) {
        record();
        break;
      }
    }

    record();
  }

  const frameCount = recorded[0].length / 7;
  const topFaces = snapTo.map((entry) => entry.face);

  return {
    frames: recorded.map((values) => Float32Array.from(values)),
    frameCount,
    duration: (frameCount - 1) * SIM_DT,
    impacts,
    faceValues: topFaces.map((face, index) => assignFaceValues(face, values[index])),
    topFaces,
  };
}
