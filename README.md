# Stark RP — Map Design

Map design for an s&box RP server. Source 2 / Hammer, units are inches.

## Why this repo exists

Buildings were coming out wrong because massing, façade, interior and
materials were all being decided in a single pass. Each one silently
constrains the next, so none of them get a real decision, and the result
reads as mushy from every angle.

So: **one building at a time, one layer at a time.** A layer is locked before
the next one starts, and a locked layer is treated as a fixed constraint —
not something to renegotiate when it becomes inconvenient.

## The pipeline

| # | Pass | Question it answers | Locked output |
|---|---|---|---|
| 1 | **Massing** | What shape, how big, how does it sit? | Footprint, storey heights, mass hierarchy, roofline |
| 2 | **Façade** | What does the street see? | Bay rhythm, openings, entrance, cornice |
| 3 | **Interior** | What does the RP need inside? | Circulation, room programme, sightlines |
| 4 | **Materials** | What is it made of and how worn is it? | Material set, wear, signage, props |

Pass 1 is drawn and reviewed as orthographic plan + elevations *before* any
geometry is built, because fixing a silhouette in a drawing costs minutes and
fixing it in Hammer costs an afternoon.

## Documents

- [`docs/00-scale-system.md`](docs/00-scale-system.md) — **read first.** The
  module, the storey heights, the bay system, and the nine massing rules.
  Every building on the map obeys it.
- [`docs/01-diner-massing.md`](docs/01-diner-massing.md) — Building 01, Pass 1.
  The diner's masses, heights and bays. Drawing set in
  [`docs/01-diner-massing.html`](docs/01-diner-massing.html).
- [`docs/02-egg-simulation.md`](docs/02-egg-simulation.md) — breakable egg:
  shell, two-phase white, membrane-bounded yolk — plus the stylisation dial
  that exaggerates the measured egg back into something readable in first
  person. Research and design for the cooking side, with working code in
  [`code/Egg/`](code/Egg).

## Buildings

| # | Building | Pass 1 Massing | Pass 2 Façade | Pass 3 Interior | Pass 4 Materials |
|---|---|---|---|---|---|
| 01 | Diner | proposed | — | — | — |

## Repo layout

```
code/          gameplay code, by system
docs/          design documents and per-building specs
```

Further directories get added when there is something to put in them.
