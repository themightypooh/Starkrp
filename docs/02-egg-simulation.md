# Egg — simulation design

Research pass for a breakable egg: shell, yolk, and a two-phase white.
Cooking is explicitly out of scope. This document covers what the egg *is*
and how the liquid behaves and reads on screen.

Sections 1–6 describe the egg as measured. **Section 7 is the one that ships**
— the measured egg turned out to be too honest to read in a first-person
kitchen, and everything since is a single dial that exaggerates it back into
legibility.

Units are inches, as everywhere else on this map. A US large egg is
**2.3in long, 1.7in wide, ~50g**. That matters more than it sounds — see
[Scale](#scale-is-the-first-problem).

---

## 1. What actually makes an egg read as an egg

The temptation is to reach for a fluid solver. That is the wrong first
move, because the things that make a cracked egg recognisable are not
fluid-dynamics things. They are these:

**The white is two different substances.** Roughly 60% of the albumen is
*thick* white — a viscoelastic gel that clings around the yolk and holds a
mound. The other 40% is *thin* white — watery, and it runs. When you crack
a fresh egg into a pan you get a tight compact mound with a thin skirt
around it. When you crack an old egg you get a wide flat puddle, because
thick albumen degrades into thin over time. A single-viscosity fluid can
be water or it can be slime, but it can never be egg. **This is the single
highest-value modelling decision in the whole system.**

**The thick white has a yield stress.** It does not flow until the local
slope exceeds a threshold, then it flows, then it stops. That is why the
mound holds its shape instead of spreading to a film. Water has no yield
stress. One threshold test converts "puddle of goo" into "egg".

**The yolk is a pressurised sac, not a blob of fluid.** It is bounded by
the vitelline membrane, which is elastic and under tension. Fresh: domed
and taut, and it resists. Old: flaccid and it slumps. It can *rupture* —
and a broken yolk behaves completely differently from an intact one. A
particle fluid cannot express any of that; a mass-spring shell expresses
all of it, including the rupture, for almost no cost.

**The silhouette of the pool edge does the heavy lifting.** Thin white
creeps outward with an irregular, fingered contact line — never a clean
circle. Get the edge right and the middle can be quite crude.

**It's wet.** Fresh-cracked egg is a strong Fresnel specular sheet over a
translucent, absorbing body. Read the shading section — more realism lives
there than in the simulation.

---

## 2. Scale is the first problem

At 1 unit = 1 inch, the whole egg is ~2.3 units and shell fragments are
0.1–0.4 units. Source 2 runs at a 50Hz fixed step with `SubSteps = 1` by
default (`Sandbox.ProjectSettings.PhysicsSettings`). Sub-quarter-inch
dynamic bodies at that step jitter, tunnel, and cost more than they are
worth.

Two decisions fall out, and they are locked:

- **Nothing smaller than ~0.3in is a rigidbody.** Tiny shell fragments are
  particles with collision enabled and a `ParticleModelRenderer`. Only the
  two or three fragments that come to rest inside the food get promoted to
  real tracked objects — for everything else, nobody can tell.
- **Build the egg ~20% oversized.** Cooking games do this near-universally;
  it reads better in first person and it keeps the physics in a range the
  solver is happy in.

---

## 3. The five-layer model

The egg is never one system. It is five, and only one of them is running
at any given moment.

| Layer | When | What it is | Cost |
|---|---|---|---|
| **A — Slosh** | Intact, held | Lagging mass-centre offset on the rigidbody | ~free |
| **B — Crack** | One frame | Impact evaluation → outcome | ~free |
| **C — Pour** | ~0.4s | Built-in `ParticleEffect`, collision on | cheap |
| **D — Pool** | Rest of the game | 2D height field, two channels | ~0.3ms |
| **E — Yolk** | From crack onward | Mass-spring membrane sac | ~0.1ms |

### Layer A — slosh (intact egg)

Do not simulate the interior. An intact egg needs to *feel* like it has
liquid in it, and that is entirely a mass-distribution effect. Drive
`Rigidbody.MassCenterOverride` from a damped spring that lags the object's
motion, with `OverrideMassCenter = true`. Fifteen lines, and shaking an
egg feels right.

### Layer B — crack

The outcome is a function of three inputs, all available from
`Component.ICollisionListener.OnCollisionStart( Collision c )`:

1. **`c.Contact.NormalSpeed`** — impact speed along the contact normal.
   This is the "right force" axis the mechanic is built on.
2. **Angle between `c.Contact.Normal` and the egg's long axis.** Real eggs
   are an arch: they are dramatically stronger end-on than side-on. Hitting
   the equator cracks cleanly with far less force. This turns the mechanic
   from a timing test into an aiming test, and it is free.
3. **What it hit** (`c.Other.Surface`, plus an `edge` tag on geometry).
   A *flat* surface produces a wide crushed zone and drives fragments
   *inward* — this is exactly why cracking on a pan rim is a bad habit and
   cracking on a flat counter is the taught technique. Encoding it makes
   the skill real rather than arbitrary.

Outcome bands, with damage accumulating across taps so a light knock
followed by a second one opens it:

| Band | Result |
|---|---|
| below threshold | hairline + decal + sound, `Damage` accrues |
| in window | 2 shell halves, 0–2 fragments, clean |
| above window | 6–12 fragments, high contamination chance, contents lost |

The two big halves come from authored `break_list_piece` gibs in ModelDoc
(`Model.GetData<ModelBreakPiece[]>()`, spawned the way `Prop.CreateGibs`
does it). Art-directs properly, and the halves are the part players look
at. The tiny fragments are particles, per §2.

**Shell in the food** is then an exact test, not a fudge: a fragment that
comes to rest inside the pool's height-field footprint is contamination.
One point-in-grid lookup.

### Layer C — pour

The ~0.4 seconds of airborne fluid between shell and pan is the one place
particles genuinely win — falling egg white is stringy and stretched, and
that is what a stretched billboard is for. Use the stock `ParticleEffect`
with `Collision = true`; the engine writes `HitPos`/`HitNormal` onto each
`Particle`. On contact the particle dies and **deposits its volume into the
height field**. Thick and thin white are separate emitters with different
lifetimes and damping. The yolk is not a particle — it is Layer E from the
moment the shell opens.

### Layer D — pool (the important one)

**This is a height field, not a fluid solver.** The argument:

To resolve a 0.1in surface feature across a 5in puddle you need ~0.05in
particle spacing. For the ~4in³ an egg actually contains, that is on the
order of 30,000 SPH particles — per frame, on the CPU, in C#, alongside a
game. It will not run, and when it does it will look like blobs, because
metaball surfaces from sparse particles always do.

A 96×96 grid over 8 inches is ~9,000 cells on a regular lattice. It
vectorises, it threads perfectly, it conserves volume exactly (which
Layer F, cooking, will want), and it gives a clean contact line for free.

Two height channels, `Thick` and `Thin`, plus an optional static `Ground`
channel so the pan's curvature is baked in and fluid pools where it should.

Per fixed step, for each cell and each of four neighbours:

```
dH = (Ground[i] + h[i]) - (Ground[j] + h[j])
if dH <= 0            : no flow
thin  : flow = kThin  * dH
thick : excess = dH - Yield * CellSize
        flow = excess <= 0 ? 0 : kThick * excess
```

Outflow is clamped so a cell cannot go negative; `k <= 0.25` keeps it
stable. Add a small per-cell noise multiplier on `k` and the contact line
stops being a circle and starts being an egg. Add a cohesion term pulling
boundary cells inward and you get the rim bead.

That `Yield` term is the entire difference between water and egg white.

The surface mesh is a grid mesh rebuilt into a pre-allocated dynamic
`Mesh` (`CreateVertexBuffer` once, `SetVertexBufferData` + `SetVertexRange`
per rebuild). Cells below an epsilon are skipped, so the puddle's silhouette
is the mesh silhouette. Per-vertex we ship total thickness and the
thick/thin ratio to the shader.

### Layer E — yolk

A `Rigidbody` with a `SphereCollider` so it lands and rolls correctly, and
a mass-spring shell over an icosphere (~80–160 vertices) for the shape:

- **radial springs** to the centre — hold volume, resist squash
- **surface springs** between neighbours — membrane tension, this is what
  makes it *taut* rather than jelly
- **a volume-preservation term** — squash it flat and it bulges sideways,
  which is what a sac does and what a spring lattice alone will not do

Free consequences: it squashes on landing and wobbles with a decaying
oscillation; under gravity it settles into a dome rather than a sphere;
and if any membrane spring exceeds its break length, **the membrane
ruptures** — at which point you delete the sac and dump its volume into the
height field as a third, deep-orange phase. Broken yolk, from the same
model, no extra system.

Fresh vs old is one number: membrane stiffness.

---

## 4. Shading

More of the realism budget belongs here than in the simulation.

**White.** Not opaque and not glass. It is a thin refractive layer with a
faint blue-green cast, and it goes milky and forward-scattering where the
thick phase is deep. Sample the grabbed frame texture
(`Graphics.GrabFrameTexture`, exposed to the shader as an attribute) with
an offset from the height-field normal, scaled by thickness; apply
Beer–Lambert absorption over thickness so shallow film is nearly clear and
the mound is cloudy. Then a strong Fresnel specular over the top — the wet
sheet is most of what sells it.

**Yolk.** The standard PBR path will make it an orange rubber ball. It
needs wrapped diffuse plus a translucent back-lit term driven by local
thickness, a tight specular lobe for the wet membrane, and slight rim
brightening. Depth is available to custom shaders through
`common/classes/Depth.hlsl` (`Depth::Get`, `Depth::Linearize`).

**The meniscus.** Darken the last few cells at the contact line. This is
small and it is the difference between a puddle that sits *on* the pan and
a decal that is painted on it. Do not skip it.

---

## 5. Networking

The height field is deterministic given its deposit events. Replicate the
**events** — crack outcome, deposit position/volume/phase, yolk rupture —
never the field. Particles are already client-side. A 96×96×2 float grid
is ~72KB and has no business on the wire.

---

## 6. What this deliberately does not do

- No GPU compute. Everything here fits comfortably on the CPU at these
  sizes, and `DispatchCompute` is available later if the pool ever needs
  to be a countertop rather than a pan.
- No real SPH. See §Layer D.
- No shell deformation. Shells are rigid until they are fragments.

---

## 7. Stylisation — selling it

The sim above is accurate and, played, it is underwhelming. That is not a
failure of the sim; it is what a real egg is. A US large egg cracked into a
pan is a ~2.3in object producing a ~4in puddle a tenth of an inch deep, in
colours (near-colourless white, a yolk closer to mustard than to orange) that
nobody's memory agrees with, over about 400ms, with a yolk whose wobble is two
low cycles and gone. Held in first person at arm's length, on a hob, for the
half second the player is looking at it, most of that lands as *nothing
happened*.

So the measured egg stays exactly as it is, and the exaggeration goes on top
as multipliers — never as replacements. One dial, `Caricature`, 0 to 1:

| Preset | Dial | What it is for |
|---|---|---|
| **Documentary** | 0.00 | The measured egg. For checking the sim, not for playing. |
| **Cookbook** | 0.55 | **Shipping default.** Reads across a kitchen, still behaves like an egg. |
| **Saturday Morning** | 1.00 | Everything at once. Big, slow, wobbly, luridly yellow. |

Keeping 0 meaningful is the point of building it this way: any time the egg
looks wrong, the first question is whether it is the sim or the lie, and that
is one dial away from being answered.

### What the dial actually does

| Exaggeration | At 1.0 | Why it earns its place |
|---|---|---|
| **Size** | ×1.3 on the egg, yolk and pool | On top of the ×1.2 in §2. A true-scale egg reads as a pebble. |
| **Mound** | yield ×2.2 | The mound is the shape an egg is *recognised* by, so it gets the biggest single push. |
| **Skirt** | thin flow ×1.4 | The two-phase read only works if the skirt is visibly running out from under the mound. |
| **Rim** | cohesion ×2.4, meniscus ×2 | Silhouette is all a player reads at speed. A fat bead and a dark contact line are the cheapest legibility in the system. |
| **Fingering** | flow noise ×1.6 | A circle reads as a decal. Irregularity reads as fluid. |
| **Height** | render lift ×1.7 | On top of the renderer's own ×1.45. Volume is conserved; it is just carried in a shape you can see. |
| **Wobble** | membrane ×0.45, damping retention ×0.35 | Softened *and* underdamped together. Softening alone gives jelly; undamping alone gives a metronome. The yolk rings past the landing, where the player is looking. |
| **Squash** | pressure ×1.8 | A squashed sac bulges harder, so the landing has a real anticipation-and-settle rather than a stop. |
| **Hitstop** | 100ms, on t² | A beat of `Scene.TimeScale` dip on the crack. Physically a lie; it is the difference between an egg that broke and an egg that *you* broke. Curved on t² because a short dip is imperceptible and there is no point spending the first half of the dial on frames nobody feels. |
| **Pour time** | ×0.7 | The real thing dribbles for most of a second after the interesting part is over. |
| **Pour body** | ×1.45 | A physically sized thread of albumen is two pixels wide and simply is not there. |
| **Forgiveness** | shatter threshold ×1.6 | Widened upward only: the crack still takes the same swing, there is just more room above it. Losing a breakfast to a correct 4in/s overswing is not a mechanic, it is a bug with a rationale. |
| **Colour** | saturation, warmth, milkiness, rim | Pushed toward the egg people think they saw. Real yolk is duller than anyone remembers. |

Two of these are not free and are worth naming:

- **Volume.** A bigger egg carries more white. It is scaled by the *square* of
  the size dial rather than the cube — the full cube outgrows the pan, and the
  vertical exaggeration is already carrying the depth half of the read.
- **Outflow has to be budgeted.** The cohesion pull is a fifth outflow on top
  of four neighbours, each already capped at a quarter of the cell. Uncapped
  together they can ask a cell for more than it holds, the clamp at zero
  absorbs the shortfall, and the field silently *gains* volume — at the rim
  multiplier the preview gained about 8× in two seconds. Every outflow now
  draws on a single per-cell budget.
- **`ThickYield` was too high.** The yield slope is an angle of repose: at the
  original 0.85 the mound stands at 40°, which is a gel that would not pour
  out of a shell. A fresh mound measures nearer **0.35**, so the default is now
  0.38 (yolk 0.85) and the dial multiplies up from there.
- **Forgiveness widens the window, not the skill.** The crack threshold, the
  equator advantage and the flat-vs-edge rule in §Layer B are untouched. The
  aiming test is the mechanic and the dial does not soften it; it only stops
  the *upper* bound from being invisible and unfair.

**Preview.** [`02-egg-stylisation.html`](02-egg-stylisation.html) is a playable
rig: drag the egg around a counter in synced elevation and plan views, slam it
into the flat or into the `edge`-tagged rail, and watch the crack test, the
pour and the pool run at whatever the dial is set to. The JavaScript is a port
of `EggFluidField.cs`, `EggShell.cs`'s crack test and the yolk sac rather than
a mock-up, which is how the volume-conservation bug below was found.

The elevation is drawn true scale with the renderer's vertical lift dotted over
it — which shows up the **open tuning question**: the mound stands around 1.1in
tall, where a real one is nearer 0.4in. The yield stress acts as an angle of
repose, so a pour that lands in a tight footprint builds a cone the solver then
refuses to let down. Either `ThickYield` comes down again or Layer C has to
scatter deposits over the egg's own footprint; the preview leans on the
second.

### Where it lives

`EggStyle` is a scene component holding the preset. `EggStyle.Current` is what
every other system reads, and with no component in the scene it falls back to
Cookbook rather than to the honest setting — shipping should not depend on
someone remembering to place it. The two shaders take `ColourPunch` and
`MeniscusGain` as attributes, written per-frame by the renderer and the yolk,
so the dial moves the shading live in the editor.

---

## 8. Code

| File | Layer |
|---|---|
| [`code/Egg/EggStyle.cs`](../code/Egg/EggStyle.cs) | the stylisation dial, and hitstop |
| [`02-egg-stylisation.html`](02-egg-stylisation.html) | interactive preview of the dial |
| [`code/Egg/EggFluidField.cs`](../code/Egg/EggFluidField.cs) | D — the solver |
| [`code/Egg/EggFluidRenderer.cs`](../code/Egg/EggFluidRenderer.cs) | D — surface mesh |
| [`code/Egg/EggYolk.cs`](../code/Egg/EggYolk.cs) | E — membrane sac |
| [`code/Egg/EggShell.cs`](../code/Egg/EggShell.cs) | A, B — slosh, pickup, crack |
| [`code/Egg/EggPour.cs`](../code/Egg/EggPour.cs) | C — particles to field |
| [`code/Egg/EggFrameGrab.cs`](../code/Egg/EggFrameGrab.cs) | shading — frame copy for refraction |
| [`code/Egg/Shaders/egg_white.shader`](../code/Egg/Shaders/egg_white.shader) | shading |
| [`code/Egg/Shaders/egg_yolk.shader`](../code/Egg/Shaders/egg_yolk.shader) | shading |

Written against the s&box engine source, not against memory — every API
used is verified present. It has **not** been compiled; there is no s&box
SDK in this repo. Treat tuning constants as starting points.
