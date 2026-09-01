# Building 01 — The Diner · Pass 1: Massing

**Status: proposed.** Locks when approved. Once locked, façade, interior and
material passes treat every number here as fixed.

Typology: **railcar diner with a masonry rear addition.** A prefabricated
dining car sits on the street frontage; a later, plainer kitchen block was
added behind it. This is how most surviving American diners actually grew,
and it does the massing work for us — it produces a dominant mass, a
subordinate mass and a broken roofline as a consequence of the story rather
than as decoration applied on top.

Coordinates: origin at the west end of the street setback line. **+x** runs
east along the street, **+y** runs back away from the street, **+z** is up.

---

## Masses

| | Mass | Footprint (u) | Position | Top height |
|---|---|---|---|---|
| **A** | Dining car | 512 × 224 | x 0–512, y 48–272 | 128 eave, **160 crown** |
| **B** | Kitchen block | 320 × 256 | x 192–512, y 272–528 | **144** parapet |
| **C** | Entry vestibule | 96 × 48 | x 96–192, y 0–48 | **112** |

Plinth: 16 high, projecting 16 proud, under A and B.

**A** is 32M × 14M — a 42'8" × 18'8" car. Long, low, and parallel to the
street, which is the whole point of the type: it presents maximum frontage
and minimum height.

**B** is flush with A at the east end (x 512) and set back 192 at the west
end, so the west approach reads as car-first with the block clearly behind.

**C** is the only thing that touches the setback line. A projects *back* 48
from it, so the vestibule reaches the street line while the car sits behind —
which breaks the façade plane without anything crossing the frontage.

## The height ladder

The four tops step by exactly one module each:

```
160  ── A vault crown
144  ── B parapet
128  ── A eave
112  ── C vestibule
```

No two masses share a height, and no gap is smaller than 16. This is the
whole silhouette strategy: an even ladder reads as deliberate, where uneven
near-misses read as sloppy.

## Bays

Frontage 512 = **4 bays × 128**. Even count, so there is no centre — the
entrance is forced off-axis, which is correct for a roadside commercial
building. A symmetric, centred entrance would make it read civic.

Vestibule sits over bay 1 (x 96–192), leaving bays 2–4 (x 192–512, 320 units)
as one uninterrupted window run for the booths.

## Roof

Barrel vault over A, profile in the y–z plane, constant along x. Springs from
the 128 eave on both long sides, crowns at 160 over the centreline — a 32
rise across 224.

For Hammer, facet it. **10 facets** across the 224 width is smooth at player
distance and stays cheap; the profile is `z = 128 + 32·sin(π·t)`, t from 0 at
y=48 to 1 at y=272. Do not attempt true curved geometry in the blockout.

B is flat with a 16 parapet: roof deck at 128, parapet top at 144.

## Interior zoning (indicative — locked in Pass 3)

Recorded here only to prove the massing can hold the programme. Do not build
to these numbers yet.

Car interior after 16 walls: x 16–496, y 64–256 → **480 × 192**. Across the
depth:

| Zone | Depth | y |
|---|---|---|
| Window / booth run | 64 | 64–128 |
| Aisle | 40 | 128–168 |
| Stools | 24 | 168–192 |
| Counter | 24 | 192–216 |
| Back line | 40 | 216–256 |
| | **192** | |

That resolves exactly. Four booths at 64 (x 240–496) and a 288 counter run
seating nine stools at 32 centres.

## Rule check

| Rule | | |
|---|---|---|
| R1 Tripartite | ✔ | plinth 16 / body 112 / vault + parapet cap |
| R2 Dominant mass | ✔ | A owns the full frontage and is tallest |
| R3 No near-misses | ✔ | offsets are 0, 48, 192; heights step by 16 |
| R4 Footprint from bays | ✔ | 512 = 4 × 128 |
| R5 Sit on the ground | ✔ | 16 plinth, 16 proud |
| R6 Break the roofline | ✔ | four tops, 16 apart |
| R7 Break the façade plane | ✔ | vestibule projects 48 |
| R8 Depth beats decoration | ✔ | no detail proposed in this pass |
| R9 Street alignment | ✔ | C on the line, A and B behind it |

Every dimension is divisible by 16.

## Open questions for approval

1. **Vault or flat fascia.** The vault crown only reads from the ends and in
   three-quarter view; from straight on you see the 128 eave. A stainless
   fascia band hiding the vault is equally authentic and much cheaper to
   build. The vault is the better silhouette; the fascia is the better sign
   surface.
2. **Which end the kitchen sits at.** Currently east. If the lot's approach is
   from the east, flip it so arrivals see the car and not the service block.
3. **Lot.** Assumed parking west and rear, no site geometry committed yet.
