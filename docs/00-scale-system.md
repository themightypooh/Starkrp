# Stark RP — Scale System

The dimensional rules every building on the map obeys. Nothing gets built
until it resolves against this document.

This exists because of one failure mode: **dimensions chosen independently
never share a common divisor, so nothing ever aligns.** A building whose
floor heights, bay widths and setbacks are each eyeballed will look wrong
from every angle and no amount of detailing will rescue it. Pick the numbers
from a module instead and alignment becomes automatic.

---

## 1. Units

s&box inherits Source units: **1 unit = 1 inch.**

| Conversion | Value |
|---|---|
| 1 metre | 39.37 u (**use 40**) |
| 1 foot | 12 u |
| 1 storey (typical) | 128 u |

Hammer's snap grid is powers of two. Work at 16 for massing, 8 for façade
openings, 4 for trim, 1 only for prop placement. **If you are massing at
grid 1, you have already lost.**

## 2. The module

> **M = 16 units.**

Every massing dimension on this map is an integer multiple of M. Footprints,
floor heights, setbacks, bay widths, parapets — all of it.

16 is the right choice because it divides cleanly into the vertical system
(128 = 8M), it is coarse enough that you physically cannot fiddle, and it is
still fine enough to read as deliberate detail at street level.

Useful multiples to keep in your head:

| M | Units | Reads as |
|---|---|---|
| 1 | 16 | wall thickness, trim, plinth |
| 2 | 32 | shallow recess, pier width |
| 3 | 48 | door opening width |
| 6 | 96 | door opening height, narrow bay |
| 8 | 128 | **floor-to-floor**, standard bay |
| 12 | 192 | ground-floor height, wide bay |

## 3. Human scale constants

Baseline Source figures. **Verify the first three against s&box's actual
`PlayerController` in-engine before locking anything** — Facepunch's default
controller is not guaranteed to match the HL2 hull, and every clearance below
descends from it.

| Measure | Units | Note |
|---|---|---|
| Standing hull | 32 × 32 × 72 | verify |
| Crouched hull | 32 × 32 × 36 | verify |
| Eye height | 64 | verify |
| Max step-up | 18 | why stair rise is 8 |
| Door opening | 48 × 96 | 3M × 6M |
| Comfortable corridor | 96 w × 112 clear | 2 players abreast |
| Railing height | 40 | |

Rule of thumb: a space feels *tight* below 96 clear, *normal* at 112, and
*grand* above 160. Ground floors of public buildings want to be grand.

## 4. Vertical system

Floor-to-floor is measured slab-top to slab-top. Slab is 16 (1M), so clear
ceiling height = floor-to-floor − 16.

| Floor type | Floor-to-floor | Clear | Feels |
|---|---|---|---|
| Residential / office upper | **128** (8M) | 112 | normal |
| Commercial ground floor | **192** (12M) | 176 | generous, shopfront |
| Warehouse / industrial | **256** (16M) | 240 | cavernous |
| Basement / service | **112** (7M) | 96 | oppressive, correct |

A 4-storey building with a shopfront is therefore
`192 + 128 + 128 + 128 = 576` to the roof slab, plus parapet.

**The ground floor must be taller than the floors above it.** This is the
single highest-leverage rule for making a building look like a building
rather than a stack of identical boxes. Real buildings put their public,
tall, heavy programme at the bottom.

## 5. Horizontal system — bays

A *bay* is the repeating structural rhythm of a façade: the spacing between
columns/piers. Footprints are whole numbers of bays. Never pick a footprint
dimension directly.

| Bay | Units | Use |
|---|---|---|
| Narrow | 96 (6M) | terraced housing, dense street frontage |
| Standard | **128** (8M) | default for everything |
| Wide | 192 (12M) | civic, industrial, showroom |

A 5-bay standard frontage is `5 × 128 = 640` wide. That is your footprint
dimension — derived, not chosen.

**Odd bay counts** (3, 5, 7) give you a true centre, so the entrance sits on
axis and the building reads symmetric and formal.
**Even bay counts** (4, 6) have no centre, forcing an off-axis entrance —
which reads casual and commercial. Pick deliberately; this is a character
decision, not an arithmetic one.

## 6. Openings

| Element | Units |
|---|---|
| Exterior wall thickness | 16 (1M) |
| Interior partition | 8 |
| Door opening | 48 w × 96 h |
| Window sill height | 32 (shopfront: 0, i.e. full-height glazing) |
| Window head height | 96, or 104 on the tall ground floor |
| Pier between windows | ≥ 32 |

Windows centre within their bay. If a window does not centre in its bay, the
bay is wrong, not the window.

## 7. Stairs (resolved)

Rise **8**, run **16**. Rise must stay under the 18 step-up limit or the
player walks up the stairs like a ramp and they stop reading as stairs.

For 128 floor-to-floor: 16 risers × 8 = 128. ✔

A straight run would be 16 × 16 = **256 long** — usually too long. Use a
switchback: two flights of 8 risers, each **128 long**, with a 64-deep
landing. Total stairwell footprint **192 × 128** (12M × 8M). Grid-aligned,
fits a bay, done. Use this shaft dimension everywhere and stairs stop being
a problem you re-solve per building.

---

## 8. Massing rules — why buildings read as "natural"

Alignment is arithmetic; *naturalness* is hierarchy. Nine rules:

**R1 — Tripartite.** Every building has a base, a middle and a cap. Ground
floor taller and visually heavier; top terminated by a parapet or cornice at
least 16 deep, projecting 8–16 from the wall plane. A mass that just stops at
the top looks unfinished, and this is the most common cause of "it looks like
a box".

**R2 — One dominant mass.** The primary mass carries ≥ 60% of the footprint.
Every other mass is *clearly* subordinate: at least 1M lower, or set back at
least 1M. Two masses of similar size fight and the building loses its subject.

**R3 — No near-misses.** Two faces are either flush, or offset by ≥ 16.
An 8-unit offset reads as a mistake, not a decision. This is the rule that
kills most of the "something's off but I can't say what" feeling.

**R4 — Footprint from bays.** Both footprint dimensions are whole bay counts.
Derived, never chosen.

**R5 — Sit on the ground.** A plinth of 8–16, projecting 4–8 proud of the wall
above. Without a base, buildings look pasted onto the terrain.

**R6 — Break the roofline.** Where masses meet, their parapet heights differ
by ≥ 16. A flat continuous roofline across a whole building is the silhouette
equivalent of monotone.

**R7 — Break the façade plane.** At least one recess or projection of ≥ 16 on
any elevation longer than 4 bays. Single-plane façades read as cardboard at
every distance.

**R8 — Depth beats decoration.** If an elevation looks wrong, move geometry
before you add detail. Detailing a badly-massed building makes it worse,
because it draws the eye to the proportions.

**R9 — Street alignment.** On a shared street, all frontages align to one
setback line. Buildings may step *back*, never forward, off that line. At map
scale this rule does more for coherence than anything happening on the
individual buildings.

---

## 9. Pre-build checklist

Before any geometry gets made:

- [ ] Every dimension divisible by 16
- [ ] Footprint = whole bay count in both directions
- [ ] Ground floor taller than upper floors
- [ ] Storey heights from the §4 table, unmodified
- [ ] Stair shaft is 192 × 128 and reaches every floor
- [ ] One mass is clearly dominant (R2)
- [ ] No offsets between 1 and 15 units (R3)
- [ ] Parapet/cornice present and ≥ 16 deep (R1)
- [ ] Plinth present (R5)
- [ ] Frontage on the street setback line (R9)
