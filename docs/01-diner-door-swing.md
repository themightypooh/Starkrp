# Building 01 — The Diner · Kitchen Swing Door

Double-acting swing door between the cook line and the dining room.
Drawn from a field photo of a working diner kitchen. Drawing set:
[`01-diner-door-swing.html`](01-diner-door-swing.html).

## The governing idea

No handle, no latch, no lock. Everything about this door follows from the
fact that **you arrive at it with both hands full** and open it with a hip or
a shoulder. The porthole exists because you cannot stop to check — you need
to see who is coming before you hit it.

Get that premise right and the rest of the design is forced.

## Schedule

| Element | Size | Position | Material |
|---|---|---|---|
| Leaf | 48 × 96 × 4 | hinge edge x = 0 | Varnished ply, worn |
| Porthole glass | Ø 12 | x 24, z 64 | Glass, smudged |
| Bezel ring | Ø 16, 2 wide | x 24, z 64 | Stainless, screwed |
| Push plate | 8 × 32 | x 36–44, z 24–56 | Stainless, hazed |
| Kick plate | 48 × 16 | z 0–16 | Stainless, scuffed |
| Frame | 8 face | x −8 and 48 | Stainless clad |
| Pivot axis | — | x = 4 | Sprung, ×2 |
| Swing | ±95° | — | Return to centre |

## Two things worth not getting wrong

**The porthole sits at 64 — player eye height.** A real kitchen porthole is
set at the sightline of whoever is carrying the plates, and Source's eye
height happens to be the same number. Real-world logic and engine logic
agree here, so it reads correctly from both sides for free.

**There is no rebate in the jamb.** A normal door stops against a rebate.
This one has none — the jamb is square on both faces so the leaf passes clean
through the wall plane. Put a stop in the frame and the door only works one
way, which is the single most likely way to build this wrong.

## Wear

Five zones, each a record of the same motion repeated for years. Uniform
grunge everywhere reads as a texture; these read as a history.

1. **Hand rub** — beside the push plate, not on it. People miss the plate and
   shove bare wood. It polishes *lighter*, because the finish wears off.
2. **Smudge halo** — fingerprints ringing the bezel.
3. **Grime band** — directly above the kick plate, where mop water and grease
   stop at the metal edge and dry. The sharpest tell in the reference photo.
4. **Boot scuff** — on the kick plate at the leading corner, furthest from
   the pivot, where the foot lands.
5. **Hazed stainless** — never mirror. Omnidirectional micro-scratching kills
   the reflection; a polished chrome shader will look wrong instantly.

## Build notes

Cut the porthole as **real geometry**, not an alpha card — players use it as
a sightline and a flat texture breaks as soon as anyone approaches off-axis.
Collision stays a plain box; never trace the round hole.

The leaf wants a **hinge constraint with a spring return and ±95° limits**,
not a scripted animation, so it takes an impulse from a body running into it
and settles on its own. That overshoot-and-settle is most of what sells it.

## Caveats

- The reference photo is shot at an angle, so **every proportion here is
  estimated off it, not measured.** One square-on shot, or a single measured
  leaf width, would replace the lot.
- Could not reach the s&box docs from the authoring session, so the mechanism
  above is described by behaviour rather than by API name.
- **Scaled up from reality.** A real commercial swing door is about 36 × 84;
  this is 48 × 96, because the Source player hull is 32 wide and a true-size
  door leaves four inches a side. It reads slightly chunkier than the
  photograph, and that is the correct trade.
