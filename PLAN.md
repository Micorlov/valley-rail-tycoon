# Valley Rail — implementation plan and architecture

## Scope

Offline landscape Android train tycoon: isometric 3D map, touch pan/zoom, preview-and-confirm grid railway construction, stations, three train models, two-stop repeat routes, coal/goods/passengers, delivery income, running costs, pause/1x/2x/4x, local manual save/load/autosave.

Confirmed constraints: one train per connected network; an authored 64×64 starting valley inside a chunk-based world that expands during play (see Dynamic City & World Growth); bridges only at fixed corridors; original/free art; no tunnels, freeform geometry, signals, complex production chains, loans, competition, multiplayer or backend.

The original brief is retained in `docs/original-brief.md`. The approved plan remains the product target. Actual validation and unfinished release gates are recorded separately in `VALIDATION.md`.

## Technical implementation

- Unity 6000.5.8f1 (installed editor; deviation from the proposed unavailable 6.3 LTS), URP 17.5.0, Input System 1.20.0, uGUI/TMP 2.5.0 and Test Framework 1.7.0 (the manifest pins 2.0.0 and 1.6.0, but both are built-in packages that the editor resolves to its own versions in `packages-lock.json`). Input handling is set to Both so the Android Back key is reliable.
- Android API 29 minimum, ARM64, IL2CPP, landscape. Mobile URP asset uses 2x MSAA, no HDR and limited shadows.
- Pure C# `ValleyRail.Core` assembly contains authoritative state and domain services. `ValleyRail.Presentation` handles Unity views and input. Editor and test assemblies are separate.
- Stable IDs and plain serializable models; no GameObject references in simulation or saves. `GameBalance` is an editable ScriptableObject for prices, production, capacity, speed, dwell time and cargo rates.

## Core gameplay and data

`WorldState` owns tracks, stations, producers, trains, accounting, camera preferences and tick count. New games contain Pinecrest Mine, Eastbank Power, Valley Works, Willowbrook and Oakridge. The world is authored by fixed coordinates; the seeded scenery generator decorates that map and does not generate gameplay terrain.

`GameSession` composes `RailNetwork`, `RailPathfinder`, `BuildService`, `StationService`, `CargoService` and `TrainSimulation`. `SimulationClock` schedules integer-state 20 Hz ticks. Production and running costs retain integer remainders. Commands execute between ticks, including when paused; visuals never determine simulation state.

Tracks use cell-edge ports and explicit allowed transitions. A turnout has one common stem; branch-to-branch shortcuts are forbidden. Direction-aware A* finds paths; disconnected graphs report failure. Paths cache by endpoints and topology revision. Curves use the same circular geometry for rails and train sampling. Train wagons follow distance offsets on the current leg; endpoint reversal is an intentionally simplified station maneuver.

`BuildService` computes a whole candidate transaction, validates cost/footprints/bridges/network ownership, and commits or rejects it atomically. Bridges consist of all three river cells and are removed together. Route and station tracks are protected. `ExclusiveNetworkPolicy` is the seam for later movement-reservation policies.

Stations require three straight non-bridge cells and an adjacent clear building strip. Catchments bind to one producer within three Manhattan cells of the platform. Producer inventory is shared by all stations bound to it. Trains support exactly two distinct reachable stops, must deliver before route changes, and pay once per accepted unload. Coal goes to the plant, goods to towns and passengers between different towns.

## Scenes, views, UI and folders

- Bootstrap: persistent composition root.
- MainMenu: new game, optional working demo, manual load, autosave continuation, sound.
- Game: runtime-authored map, scenery, railway and train views, orthographic camera, sun and touch UI.

`WorldView` builds original meshes and batches static rail geometry. `RailGeometry` is shared by rail rendering and train position sampling. `CameraController` arbitrates pointer/touch gestures and UI hit tests. `GameplayPresenter` creates the HUD, build panel, shop, train/route panels, menus and recovery UI. `GameBootstrap` composes these and handles scene/save lifecycle.

Source folders are grouped under `Assets/Game/Scripts/{Core,Presentation,Editor}`. Scenes, resources, art, audio, materials, prefabs, third-party notices and EditMode/PlayMode tests are under `Assets/Game`. Generated configuration assets are in Resources. Procedural world models are created at runtime instead of relying on imported prefab packs.

## Saves

`SaveService` accepts an injected JSON codec. Envelopes contain schema version, full snapshot payload and SHA-256 checksum. Writes use a flushed temporary file, read-back validation and replacement with backup. Restoration validates IDs, limits, map version, port masks, complete bridges, platforms, path connectivity, cargo and single-train ownership. Graph caches are rebuilt.

Manual/autosave slots are independent. Autosave occurs each changed foreground minute and on backgrounding. Corruption offers explicit previous-backup recovery. Unsupported versions are not overwritten by loading. Loaded simulations are paused.

## Milestones and acceptance

1. **Project/map/camera:** pinned project, three scenes, authored world, original scenery, touch/mouse camera. Test on phones and tablets.
2. **Rail domain:** graph, rotations, turnout transitions, component detection, A*. Test unreachable paths, directional constraints and cache invalidation.
3. **Construction:** valid/invalid previews, atomic cost, junction conversion, fixed bridges and removal. Test rejected transactions leave state unchanged.
4. **Movement:** fixed ticks, distance sampling, station reversal and wagon offsets. Test speed equivalence and no lost catch-up ticks.
5. **Stations/routes:** catchments, clear footprints, purchase, two-stop assignment, component exclusivity. Test occupied network merges and protected infrastructure.
6. **Cargo:** shared producer storage, production, load/unload, manifests and destination checks. Test all three delivery types and conservation.
7. **Economy:** starting funds/prices, route-distance-independent payout, running costs, refunds, insolvency and sale recovery. Test profitable sample services.
8. **UI/art:** full workflow, safe-area HUD, contextual panels, settings, tutorial prompts, original meshes and click sound. Test usability, clipping, touch target sizes and gesture transitions.
9. **Persistence:** manual/autosave, backups, in-motion/dwell restoration, error handling. Test exact subsequent state and malformed saves.
10. **Android release gate:** build/install, lifecycle, aspect ratios, profiling and a 30-minute maximum-scope device soak. Targets: 30 FPS, <4 ms simulation/frame at 4x, <500 MB and no recurring idle simulation GC. These require measurement, not inference from compilation.

## Risks and postponed work

The one-train-per-network rule limits network sharing by design. The map supports three separate services; the optional demo illustrates them. Visual polish uses a deliberately simpler original low-poly art set than the reference image. Fixed-world geometry and a finite piece catalog keep scope bounded.

Postpone signals/dispatch/deadlocks, arbitrary bridges/tunnels, elevation/terraforming, diagonal/four-way tracks, depots/shunting, wagon editing, more route stops, timetables/mixed cargo (city transfer stations exist since 2026-09-25: Core/CargoTransfer.cs), competitors/finance, multiplayer/cloud, offline earnings, monetization, camera rotation, minimap, city mergers, land value, pollution, road traffic and town ratings.

## Dynamic City & World Growth

Cities grow in response to railway service and the world expands chunk by chunk. The design reuses the existing
Core/Presentation split, the integer tick loop, the `Balance`/`GameBalance` data pattern, the revision-driven view
refresh and the validated JSON saves. Nothing here needs GameObjects per building or tile, per-frame scans, floats in
the simulation, or a new save envelope.

### The loop this creates

```
Railway Service ──▶ City Growth ──▶ Population ──▶ Passenger Demand ──▶ More Trains
      ▲                                                                       │
      │                                                                       ▼
New Cities / Larger World ◀── Network Expansion ◀── More Revenue ◀────────────┘
```

Service (passengers delivered/boarded, goods delivered, distinct destinations, served stations) earns a city growth
points; points buy buildings; buildings carry population; population sets passenger production and storage; more
passengers need more trains and pay more; revenue funds track into newly unlocked regions whose villages start the
loop again.

### 1. City data model

A city decorates a Town producer. The producer remains the cargo and catchment entity, so `CargoService`,
`StationService` and every existing test keep working.

```csharp
public enum CityLevel { SmallVillage, Village, SmallTown, Town, City, LargeCity }
public enum BuildingCategory { Residential, Commercial }          // Industrial, Special: post-MVP

[Serializable] public struct BuildingState { public Cell cell; public byte def; public byte rotation; }
[Serializable] public struct RoadState     { public Cell cell; }
[Serializable] public struct ClearedCell   { public Cell cell; public long untilTick; }   // bulldoze cooldown

[Serializable] public class CityState
{
    public int id, producerId;                    // id from w.nextId; producerId → ProducerState (kind Town)
    public string name;                           // same as the producer
    public Cell center;                           // == producer.cell; the plaza (reserved, never built on)
    public int population, jobs;                  // sums over buildings, stored for UI and validated on load
    public CityLevel level;
    public int growthPoints;                      // accumulator, never negative, capped at 2 × action cost
    public uint rng;                              // xorshift32 state; per-city determinism
    public int[] paxIn = new int[4], paxOut = new int[4], goodsIn = new int[4]; // one-minute ring buckets
    public int bucket, blockedEvaluations;        // ring cursor; consecutive "no space" evaluations
    public long lastMilestoneTick;                // throttles notifications
    public List<BuildingState> buildings = new List<BuildingState>();   // capacity reserved in GameSession
    public List<RoadState> roads = new List<RoadState>();
    public List<ClearedCell> cleared = new List<ClearedCell>();
}
```

`WorldState` gains `int seed`, `int mapVersion = 2`, `int chunkMinX, chunkMinZ, chunkMaxX, chunkMaxZ` (the unlocked
rectangle in chunk coordinates), `int regionsUnlocked`, `List<CityState> cities`, `int cityRevision` (bumped on any
building/road change; the view refreshes from it the way rails refresh from `revision`). `ProducerState` gains
`int production, storage` (written from population for towns, from `Balance` for industries) so `CargoService.Step`
stays a flat loop with no lookups.

Rules the model must obey (all verified by tests): only public mutable fields (no `readonly`, no computed properties,
no dictionaries, no floats) so Newtonsoft and System.Text.Json round-trip identically; list capacities are **not**
serialised, so `GameSession`'s constructor re-reserves them (next to `AssignMissingTrainNumbers`) via the `Capacity`
setter; field initialisers stay tiny because `BuildService.ValidateBuild` constructs a throwaway `WorldState` on every
preview; `JsonSnapshotCodec` sets `ObjectCreationHandling.Replace` so both codecs replace lists.

Runtime state (never saved, rebuilt on load): the per-chunk cell grid inside `Terrain` (§9) and a pre-sized
`dirtyChunks` list on `GameSession` that construction, bulldozing, station placement and growth append to.

### 2. Population model

Aggregate only. Population is the sum of residential building populations; jobs the sum of commercial and municipal ones.
Definitions live in `BuildingCatalog` (Core/BuildingCatalog.cs); saves store the index, so entries are only appended.

| Level | Homes (population) | Businesses (jobs) |
|---|---|---|
| 1 | House 40, Cottage 30, Bungalow 40, Farmhouse 50 | Shop 20, Café 15 |
| 2 | Large house 90, Villa 100, Duplex 80, Townhouses 110 | Store 50, Market hall 60 |
| 3 | Apartments 220, Walk-up flats 240, Garden flats 260 | Office 150, Hotel 170 |
| 4 | Apartment tower 500, High-rise 560 | Office tower 350 |

New buildings and upgrades pick a random variant of their level; upgrades go to the most central homes and businesses
first, so a skyline rises around the plaza while the edges stay suburban. Municipal services are unique per town, never
upgrade, unlock by town level and add jobs plus `civicPoints` (3) growth points per minute each: Town hall (day 0, 20
jobs); Chapel 10 and School 30 (Village); Fire station 25, Medical centre 40, Post office 20 and Park 0 (Small Town);
Library 15 plus the landmarks Police headquarters 3×3 (80 jobs), Hospital 4×4 (250) and Shopping mall 5×5 (350)
(Town); Museum 40, University 6×6 (300) and Stadium 8×8 (150) (City). A landmark is anchored at its south-west cell and
placed on the free square nearest the plaza that touches a street (a summed-area table over the town's reach); it may
cover street lines that have no street yet and reach size − 1 cells past the influence radius, and the grid routes
around it. Every footprint cell blocks track and is validated. Earlier single-cell and 2×2 versions (definitions 27,
29, 31–35) are retired: kept for loading, never built.

City level from population: Small Village < 500, Village < 1,200, Small Town < 2,500, Town < 5,000, City < 10,000,
Large City ≥ 10,000. Level sets the influence radius (4, 5, 7, 9, 11, 13 cells, Chebyshev from the plaza), the
maximum building level (1, 2, 2, 3, 4, 4), the target commercial share (10–35 %) and the action cost (§3).

**Day-0 layout stays inside the original 3×3 footprint:** the plaza in the centre on a three-cell main street, the town
hall facing it from the north and five different homes (bungalow, townhouses, duplex, large house, villa = 420
population, 20 town-hall jobs). Nothing outside the 3×3 is occupied, so every existing station fixture beside a town
stays valid.

**Street grid (CityLayout):** streets lie on every (blockWidth + 1)th column and (blockDepth + 1)th row counted from the
plaza (3 × 2 blocks), and a new street cell must touch an existing one, so each town is a single network around its
plaza. Buildings take the block cells beside a street. A street may cross a straight track at right angles (a level
crossing) and may run onto a highway cell; highways are planned from one town's streets to the other's. Saves with
`cityLayout` below `CityLayout.Current` (3) are re-laid out once when a session starts: the day-0 town where its cells are
still free, then growth without points until the previous population is back.

Passenger production replaces the fixed `production[2]`:

```
producer.production = passengerBase (8) + (population + jobs) / residentsPerPassenger (35)   // per 1,200 ticks
producer.storage    = storage (200) + population / 8
```

420 → 8 + 12 = **exactly 20/min** and 252 storage, so the tutorial, demo and every economic test keep their numbers;
8,700 → ~256/min and ~1,290 storage, which is what makes big cities need many trains. Goods acceptance is unchanged.

### 3. Growth algorithm

`CitySimulation.Step()` runs inside `GameSession.Step()` after `Trains.Step()`. Cities evaluate on **fixed slots**:
city index `i` evaluates when `(tick / 20) % 60 == i % 60`, i.e. once per game minute, staggered so the per-tick cost
is one city. No cursor is stored; the schedule is a pure function of `tick`, which keeps saves replayable and rates
in plain "points per minute".

```
rate (points per minute) =
      base                                  7
    + min(municipal buildings, 16) × 3      the day-0 town hall brings a village back to 10
    + min(paxIn(last 4 min)   / 5, 60)      passengers delivered here
    + min(paxOut(last 4 min)  / 5, 60)      passengers boarded here
    + min(goodsIn(last 4 min) / 4, 30)      goods delivered here
    + min(connections, 4) × 15              distinct other producers reachable by trains routed at this city's stations
    + min(servedStations, 3) × 10           stations here that a routed train uses
growthPoints = min(growthPoints + rate, 2 × actionCost[level])
```

While `growthPoints ≥ actionCost[level]` (60, 90, 130, 180, 240, 320) and fewer than two actions have run this
evaluation: deduct the cost and run one **action**:

1. Choose upgrade when the city is at least a Small Town, a building below the level cap exists, and `rng % 3 == 0`;
   otherwise expand (§4). If expansion finds no candidate, extend a road (§4); if that fails, upgrade; if nothing is
   possible, stop, refund the cost and increment `blockedEvaluations` ("Growth: no space"). Being boxed in is a silent
   no-op (the max-scope fixture surrounds every town with sidings and must still validate).
2. Recompute population, jobs and level; write `producer.production/storage`; bump `cityRevision`; append the chunk to
   `dirtyChunks`; enqueue a notification if the level changed.

Intended timings at 1x before tuning: unserved village 10/min → a building every 6 min; village with one passenger
service (≈20 delivered + 20 boarded per min, 1 connection, 1 station) ≈ 75/min → one building per minute; a three-city
hub (≈100+ pax/min each way, 3 connections, 3 stations) reaches the 250/min ceiling → still one or two per minute at
Large City costs. Costs rising with level are the natural plateau that asks for more service. Congestion penalties
are post-MVP (there is no station queue concept yet); lack of space is the implicit penalty.

Counters are updated at the three cargo sites that already know origin and destination producers — `CargoService.
Service` unload (Simulation.cs:88-97) and boarding (98-104) and `TrainSimulation.UnloadOnly` (328-335) — into the
city's current ring bucket; each evaluation advances `bucket` and zeroes the reused slot. "Last 4 min" = the sum of
the other three buckets plus the current one. Integer only; nothing allocates.

Determinism rules: iterate lists, never dictionaries or sets, when choosing candidates; argmax with lowest-key
tie-break; no `List.Sort(Comparison)` (allocates on Mono/IL2CPP); no capturing lambdas in the tick path; the per-city
xorshift32 `rng` is the only randomness. A save taken mid-growth replays identically, extending the existing
`SaveDuringTravelContinuesExactly` guarantee.

UI mapping: "Growth +75/min · 32/60 saved · next building in 1 min" plus the six terms; `blockedEvaluations > 0` →
"No space"; rate < cost/3 → "Slow", < cost → "Normal", < 2×cost → "Fast", else "Booming".

### 4. Building placement algorithm

No map scan. Candidates come from what the city already owns, read from the cell grid (§9) in O(1) per cell:

1. Enumerate the four neighbours of every road cell (in list order); if that yields nothing, the four neighbours of
   every building. A Large City has ~150 roads → ~600 grid reads per action, and actions run at most twice a minute.
2. A neighbour is a candidate if it is inside the unlocked rectangle, within the influence radius, not water, hill,
   corridor, track, platform, station strip, industry footprint, plaza, building or road, not in `cleared` (cooldown),
   and not claimed by a nearer city plaza (tie → lower city id), so overlapping influence areas resolve deterministically.
3. Score = 40 − 2·Chebyshev(plaza) + 6·(adjacent buildings) + 10·(within 4 cells of a platform of this city's station)
   + 4·(that station moved ≥ 40 units in the last 4 min) + (rng & 7). Highest wins; the jitter makes growth
   semi-organic rather than square.
4. Category: commercial if the commercial share is below the level target and the cell is within 3 of the plaza or a
   station, else residential. Definition = that category at level 1.
5. Write the building, set the grid bit, bump `cityRevision`.

Road extension (only when step 1 finds no road-adjacent candidate): consider free cells 4-adjacent to any road cell
or, when the town still has only its plaza, to any building; prefer continuing straight from a road end, then a
perpendicular branch every third cell along a street (`(x + z) % 3 == 0`); the cell must pass the building rules and
additionally must not be a track cell; choose the extension exposing the most new free neighbours, rng tie-break.
Roads never enter water (no city bridges). This gives short believable streets with no pathfinding, cars or traffic.

Scratch lists are pre-allocated members of `CitySimulation` (1,024 ints), so an action allocates nothing.

### 5. Building upgrade system

An upgrade replaces `BuildingState.def` in place with the next level of the same category when that level is within
the city's maximum. Selection: the lowest-level building nearest the plaza, station proximity as tie-break, so busy
station districts densify first. Population and jobs are recomputed from the catalog after every change; nothing
else is stored per building.

Bulldozing (player): `BuildService.Bulldoze` consults the grid before tracks; removing a building costs
`demolitionCost × buildingLevel` (default $500 × level), clears the grid bit, records the cell in `cleared` for
`demolitionCooldown` ticks (default 6,000 = 5 min) so the city does not rebuild before the player lays track, and
recomputes population. Roads and plazas cannot be bulldozed in the MVP. This guarantees the player can always reach
a station.

### 6. Railway influence on growth

- Cities never place anything on tracks, corridors, platforms, station strips, industry footprints or plazas. The
  same grid bits drive `BuildService.Placeable`, so the two systems cannot disagree.
- Player tracks may cross city road cells (level-crossing visual, straight-piece cost); the road stays. Tracks may not
  enter buildings or plazas; the preview says "City buildings block tracks; bulldoze to clear."
- Stations are development centres: +10 in candidate scoring within 4 cells of a platform, +4 more when that station
  moved ≥ 40 units recently; upgrades prefer station-adjacent buildings. Cities visibly densify along the line.
- Station catchment for towns (`StationService.Nearby`) becomes "any platform cell within 3 of the plaza or of any
  building/road of that city" (≤ 75 grid reads). Industries keep the current rule. Validation uses the same function,
  so old stations remain valid, and the town/station panel text changes accordingly.
- Connectivity: for each train whose route touches a station of this city, the other stop's producer counts once
  (O(trains) per evaluation). "Served station" = a station of this city used by any routed train.

### 7. Passenger-demand integration

`CargoService.Step` reads `p.production` and `p.storage`; `CitySimulation` writes them whenever population changes;
migration and new-game set them once. `CargoService.Service` and `UnloadOnly` add the counters (§3). Revenue keeps
`units × rate × distance`, but the distance term is tapered so a 256-cell world does not pay 6–8× today's maximum:
`distance = min(d, 80) + max(0, d − 80) / 4` (balance field `payoutDistanceKnee`). Within the valley (d ≤ 80) every
existing payout is unchanged. The train limit rises to a balance value (24); the one-train-per-network rule is untouched.

### 8. City boundaries

Each city has an influence radius from its level (§2). A cell may be claimed only if it is within that radius and no
other plaza is nearer. Cities therefore grow until their buildings touch, forming a visually continuous metropolitan
area while remaining separate producers, stations, labels and growth entities. Merging is Future scope. Tapping a
city draws its claimable ring as one faint line mesh (presentation only).

### 9. Chunk architecture

- Chunk = 32×32 cells. World = up to 8×8 chunks, chunk coordinates −3..4, cell coordinates −96..159. The authored
  valley is exactly chunks (0,0), (1,0), (0,1), (1,1), so every coordinate in saves, tutorial, README, demo and tests
  stays valid. `World` (Core, static) holds the only copies of these constants: `Origin = -96`, `Span = 256`,
  `ChunkSize = 32`, `ChunkOrigin = -3`, `Chunks = 8`.
- `Cell.Key = (z − Origin) * Span + (x − Origin)`; `Cell.FromKey` replaces the three `% 64` decodes in `BuildService`.
  `RailNetwork.At` keeps a **static** `World.InWorld` check before the dictionary lookup (cells outside −96..159 would
  alias real keys); `World.Unlocked(w, cell)` is the rectangle test used by construction, growth and the camera.
- Chunks are not objects in the save. Terrain and decoration come from the seed (§11); entities come from the global
  lists filtered by chunk; the unlocked rectangle is four ints.
- **Cell grid:** `Terrain` owns one `byte[1024]` per unlocked chunk (≤ 64 KB). Low bits are terrain (water, corridor,
  hill, tree) computed on unlock; high bits are occupancy (track, station/strip/footprint/plaza, building, road)
  maintained incrementally by `CommitBuild`, `Bulldoze`, `StationService.Place` and growth, and rebuilt in O(n) on
  load. This replaces the per-call producer scan in `Blocked` and the station-strip scan in `Footprint`, and gives
  O(1), allocation-free `Placeable`, catchment and candidate checks.
- **Track preview cost:** before the world grows, `BuildService.Preview` gets a binary-heap open list (behaviour
  preserving) and a search bounding box (the endpoints' box expanded by 12 cells, clamped to the unlocked rectangle),
  and `ValidateBuild` builds its connectivity check from a track list instead of a throwaway `WorldState`.
- Rendering: one `ChunkView` per unlocked chunk with combined static meshes — ground bands, trees, roads and building
  bases (one mesh per material via the existing `Dictionary<Material, List<CombineInstance>>` pattern), building
  detail, rails, and sleepers as their own mesh (they are ~24 of the ~25 boxes per track cell) — with correct bounds
  so Unity's frustum culling does the rest. No activation or pooling subsystem: 64 chunks × 0.1–0.5 MB of mesh is
  within budget. `WorldView.Refresh` drains `dirtyChunks` one chunk per frame.
- Simulation is uniform: growth is O(roads) per rare action and producers are a flat loop, so far cities keep growing
  logically with nothing to simplify by distance.
- The four sides of the unlocked rectangle show one muted "unsurveyed" strip each, with the next-region label.

### 10. World expansion rules

`WorldExpansion.Step()` runs every 1,200 ticks (and immediately when a delivery crosses a threshold):

1. **Milestone trigger:** expansion `n` fires when `w.delivered ≥ unlockDelivered[n]` (defaults 500, 1,500, 3,500,
   7,000, 12,000, 18,000, 25,000, then +8,000; twelve expansions take 2×2 to 8×8). HUD: "Next region: 2,140 / 3,500
   delivered".
2. **City pressure trigger:** a city at Small Town or above with `blockedEvaluations ≥ 3` whose influence ring touches
   the rectangle edge expands that side immediately ("Rivermount is spreading into the northern hills").
3. **Which side:** the side whose midpoint is nearest the centroid of the player's stations (the plaza of the
   pressured city for trigger 2; the world origin if there are no stations); sides already at the world edge are
   skipped; tie order N, E, S, W. The world grows toward the player, one whole row or column at a time.
4. **Unlock:** widen the rectangle, generate each new chunk's blueprint (§11), append producers (ids from `nextId`) and
   cities, reserve their list capacities, `regionsUnlocked++`, enqueue "New region available: {name}" with a focus cell.
   Unlocking allocates by nature (grids, producers, cities) and is the one growth event allowed to.
5. Construction, station strips and growth outside the rectangle are invalid; `CameraController` clamps focus to the
   rectangle (+1 cell) and `Ground()` rejects locked cells with the notice "Unsurveyed region".

Player-purchased surveys (choosing the side) are post-MVP; the trigger list is a balance table so they slot in.

### 11. Procedural generation strategy

`Terrain` (Core, held by `GameSession`, seeded from `w.seed`) answers `Water`, `CorridorId`, `CorridorCells(id)`,
`CorridorStart`, `Hill`, `Tree` per cell and `Blueprint(cx, cz)` per chunk. The authored rules for chunks (0,0)–(1,1)
are preserved verbatim (river x 30..32, corridors 15 and 46, both hill corners), so existing saves validate unchanged;
hills are now drawn from the grid, which also gives the south-east blocked corner the visual it lacks today.

- Hash: `h = seed ^ (cx * 0x9E3779B1) ^ (cz * 0x85EBCA6B)` mixed with xorshift32; per-cell hashes use `(seed, x, z)`;
  all `unchecked` integer math, identical on IL2CPP ARM64 and .NET 8.
- Rivers are decided per **chunk column** at world level so they never break at chunk edges: columns 0 and 1 carry
  the valley river (x 30..32) through every row; every other column carries a north–south river three cells wide with
  30 % probability at `originX + 4 + h % 24`. Rivers are always north–south, so the existing "bridges cross straight
  east–west over the full span, `mask == 10`" rules apply everywhere.
- Corridors: two per river per chunk row at `originZ + 8 + h % 12` and `originZ + 22 + h % 8`. **Corridor id** replaces
  the row-as-id convention: authored corridors keep ids 15 and 46; generated ones are `1000 + (z − Origin) * 8 +
  (cx − ChunkOrigin)`. `TrackPieceState.bridge` stores the id; cost is charged on `CorridorStart` (the western cell);
  removal and validation use `CorridorCells(id)` instead of the `x 30..32` loops.
- Hills: two or three blobs per chunk (radius 2–4) away from the settlement site.
- Forest: per-cell hash below density (30 %), excluding water, hills and cells within 6 of a producer (today's rule).
  Trees are never stored; an occupied cell simply omits its tree when the chunk mesh is built.
- Settlement: 65 % of generated chunks get one Town producer + city near the chunk centre (jitter ±8, moved off
  water/hills); two-part seeded name table; plaza plus three to five houses.
- Industry: 50 % chance of one Mine, Factory or Plant at least 10 cells from the settlement.
- Stored vs regenerated: terrain, forest and the initial blueprint are recreated from the seed; everything the player
  or simulation changes (tracks, stations, trains, producer inventory, cities' buildings/roads) lives in the global lists.
- `WorldState.New(balance, seed = 182746)` keeps tests and the demo deterministic; `NewGame` passes a clock-derived seed.

### 12. Save/load changes

- Envelope unchanged (`schemaVersion` 1, payload, SHA-256). `WorldState.mapVersion` 2; `mapId` stays "green-valley".
  `RestoreSnapshot` accepts mapVersion 1 or 2 and migrates 1 → 2 in memory; `Save` refuses to overwrite a file whose
  mapVersion is greater than 2 (generalises today's check). The `schemaVersion 9/99` rejection tests still pass.
- New fields: `seed`, the four rectangle ints, `regionsUnlocked`, `cities`, `cityRevision`; `ProducerState.production/storage`.
- **Migration** (`SaveMigration.Upgrade`): seed 1 for every migrated v1 file (reproducible), rectangle = (0,0)–(1,1),
  the two day-0 cities, producer production/storage, mapVersion 2. The file is rewritten only by the next save.
- **Validation:** the first five producers still match defaults; further producers need a valid kind and a cell inside
  the rectangle, not water/hill, not overlapping; exactly one city per Town producer with `center == producer.cell`;
  building/road cells unique, inside the rectangle, within the influence radius, not water/hill/corridor/track/platform/
  strip; definition index in catalog range; `population/jobs/production/storage` equal the recomputed values; ring
  buckets ≥ 0; limits from balance. Generated content is **not** re-derived from the generator (cities mutate and
  balance tuning would invalidate saves); the checksum already covers tampering. Validation stays O(n): `Save` runs it
  synchronously every autosave.
- Size: a `BuildingState` is ~40 bytes of JSON; 30 cities × 300 buildings ≈ 360 KB. Compact encoding is Future scope.

### 13. Android performance strategy

| Concern | Approach |
|---|---|
| Per-tick cost | `Cargo.Step` loops ≤ ~40 producers; one city evaluation per 20 ticks (O(trains + stations)) with ≤ 2 actions (O(roads × 4) grid reads); expansion check every 1,200 ticks. Worst case at 4x catch-up (32 ticks/frame) ≈ 2 evaluations, far under the 4 ms gate. |
| Allocation | Struct lists with reserved capacity; pre-allocated scratch; grid bytes instead of dictionaries; notifications as structs formatted by the presenter; no lambdas, LINQ, enum keys or `Enum.IsDefined` in growth code. The Unity soak keeps `== 0` bytes with growth enabled and expansion disabled by a balance flag; a second test asserts the first unlock allocates under a budget and the following 36,000 ticks allocate 0. |
| Preview hitches | Binary-heap A* with a bounding box; connectivity check without a throwaway `WorldState`. |
| Rendering | No per-building/tile GameObjects. Per chunk: combined meshes by material; sleepers and building detail in separate meshes hidden when `zoom > 26` (LOD); rails batched per chunk so laying a line recombines one or two chunks, not the map. Unity frustum culling handles off-screen chunks. |
| Rebuild hitches | One dirty chunk per frame (≈ 1–3 ms for 300 boxes), measured through the VR_PERF line; fall back to 16×16 sub-batches if a rebuild exceeds 4 ms on the test phone. |
| Labels | One TMP label per city (name + population) and per station, refreshed on `cityRevision`, hidden beyond zoom 34. |
| Memory | Grids ≤ 64 KB; city lists ≤ ~30 × (320 + 160) structs ≈ 240 KB; chunk meshes 0.1–0.5 MB × ≤ 64. Within the 500 MB gate; measured on device. |
| Determinism | Integer-only simulation and hashing; no floats in Core; identical across IL2CPP and .NET 8. |

### 14. Required Unity scripts/classes

Core (`Assets/Game/Scripts/Core`):
- `Models.cs` — `World` constants and chunk math, `Cell.Key/FromKey`, `WorldState` fields, `ProducerState.production/storage`; `MapDefinition` keeps only `Produces/Accepts`.
- `Terrain.cs` (new) — seeded terrain, corridor ids, per-chunk cell grid with occupancy bits, `Blueprint`, name table.
- `City.cs` (new) — `CityState`, `BuildingState`, `RoadState`, `ClearedCell`, level table, `BuildingDefinition` + `BuildingCatalog.Defaults()`.
- `CitySimulation.cs` (new) — slot scheduling, scoring, candidate search, roads, upgrades, counters, capacity reservation.
- `WorldExpansion.cs` (new) — triggers, side choice, blueprint application.
- `Notifications.cs` (new) — pre-sized struct queue on `GameSession` (kind, cityId, focus cell), not saved.
- `SaveMigration.cs` (new) — mapVersion 1 → 2.
- `BuildService.cs` — heap + bounding box in `Preview`, `FromKey`, grid-based `Placeable` (roads allowed, buildings not), corridor ids, limits from balance, building bulldoze with cooldown, `dirtyChunks`.
- `StationService.cs` — catchment via grid; strip check via grid; limits from balance.
- `Simulation.cs` — `CargoService` reads producer rates and records counters; tapered payout; `GameSession` composes `Terrain`, `Cities`, `Expansion`, `Notifications`, reserves capacities; `Step` order: commands → tick → cargo → trains → cities → expansion → ledger trim.
- `SaveService.cs` — mapVersion 2, migration hook, generalised validation, corridor loops via `Terrain`.
- `RailNetwork.cs` — `At` uses `World.InWorld`; constructor accepts a track list.

Presentation (`Assets/Game/Scripts/Presentation`):
- `WorldView.cs` — façade: owns `ChunkView`s, drains `dirtyChunks`, preview mesh, trains (unchanged), labels, LOD toggle.
- `ChunkView.cs` (new) — per-chunk meshes: ground, trees, roads/crossings, buildings base + detail, rails + sleepers, hills; unsurveyed strips.
- `CityMeshBuilder.cs` (new) — cached procedural `Mesh` per `BuildingDefinition`; road and crossing quads.
- `BuildingCatalog.cs` (new SO) — wraps `List<BuildingDefinition>`; `ProjectSetup.Configure` creates `Resources/BuildingCatalog.asset` from defaults.
- `GameBalance.cs` — `Balance` gains nested `CityBalance city` and `WorldBalance world`; the asset keeps its file.
- `JsonSnapshotCodec.cs` — `ObjectCreationHandling.Replace`.
- `GameBootstrap.cs` — seeded `NewGame`; `Select` resolves a tapped building/road to its city; drains notifications into a HUD toast; "SHOW" moves the camera.
- `GameplayPresenter.cs` — city panel (population, level, growth line and terms, connections, passengers last 4 min, next building), HUD "next region", toast, bulldoze confirmation for buildings, updated town/station text.
- `CameraController.cs` — clamp to the rectangle; `Ground()` uses `World.Unlocked`.
- `Editor/ProjectSetup.cs` — catalog asset creation.

Tests: extend `Tests/Program.cs`, `RailwayTests.cs`, `GameplayTests.cs` (§17).

### 15. ScriptableObjects/data definitions

Same pattern as `Balance`: plain serializable classes with code defaults in Core, SO wrappers in Presentation.

- `Balance.city` (`CityBalance`): `basePoints 10`, `paxDivisor 5`, `paxCap 60`, `goodsDivisor 4`, `goodsCap 30`,
  `connectionPoints 15`, `connectionCap 4`, `stationPoints 10`, `stationCap 3`, `actionCost[6]`, `maxActions 2`,
  `passengerBase 8`, `residentsPerPassenger 35`, `storagePerResident 8`, `levelThresholds[6]`, `influenceRadius[6]`,
  `maxBuildingLevel[6]`, `commercialShare[6]`, `upgradeEveryN 3`, `demolitionCost 500`, `demolitionCooldown 6000`,
  `maxBuildings 320`, `maxRoads 160`, `growthEnabled true`.
- `Balance.world` (`WorldBalance`): `unlockDelivered[]`, `pressureEvaluations 3`, `riverChance 30`, `hillBlobs 2..3`,
  `forestDensity 30`, `settlementChance 65`, `industryChance 50`, `maxTracks 4000`, `maxStations 48`, `maxTrains 24`,
  `payoutDistanceKnee 80`, `expansionEnabled true`.
- `BuildingCatalog` (SO, `Resources/BuildingCatalog.asset`): `List<BuildingDefinition { string key; BuildingCategory
  category; int level; int population; int jobs; int baseWidth, baseDepth, height (tenths of a cell); bool coneRoof;
  int wallColor, roofColor (palette indices); int windowRows; bool detail }>`. Index order is the `BuildingState.def`
  byte, so entries are append-only.
- Name table: a string array in `Terrain`; no asset.

### 16. Development milestones

Continues the existing numbering; every current test stays green at each step.

**Status 2026-09-24:** milestones 12 and 13 are implemented and verified on the phone (see `VALIDATION.md`), together
with the town rendering from milestone 14 (one combined mesh per material for all towns, rebuilt on `cityRevision`).
Deviations from the text below, to be resolved when milestone 11 lands: the world is still 64 × 64 with the original
`Cell.Key`, static `MapDefinition` terrain and row-numbered bridge corridors; the occupancy grid lives in
`CitySimulation` rather than `Terrain`; the building catalog is code defaults in `City.cs` with no ScriptableObject yet;
the payout distance is not tapered; notifications surface through the HUD notice line; per-chunk rails, the A* heap,
`dirtyChunks` and world expansion (milestone 15) are not started.

11. **World coordinates, terrain seam, preview cost.** `World` constants, `Cell.Key/FromKey`, `Terrain` with the
    authored rules and the cell grid, corridor ids, `RailNetwork.At` hard bounds, heap + bounding box in `Preview`,
    rectangle = start chunks, mapVersion 2 + migration, generalised validation, limits and payout knee in balance,
    codec `Replace`. No visible change. Tests: key round-trip over −96..159; migration of a v1 fixture; identical
    snapshots and identical preview results before/after; grid rebuild equals incremental state after random builds.
12. **Cities and demand.** `CityState`, catalog, day-0 layouts replacing the seven hard-coded houses, producer
    production/storage from population, city panel, catchment via grid, building bulldoze with cooldown, tracks blocked
    by buildings but crossing roads. Tests: 420 population ⇒ 20/min; catchment at a city edge; bulldoze cost and
    cooldown; preview blocked by a house, allowed over a road.
13. **Growth simulation.** Counters, slots, scoring, actions, roads, upgrades, notifications, growth line. Tests:
    served vs unserved ratio; deterministic replay across a save; never builds on invalid cells (checked after every
    action); boxed-in city is a no-op at max scope; upgrade ordering; Unity soak `== 0` bytes with growth on.
14. **Chunk views.** `ChunkView`, per-chunk rails/sleepers, dirty draining, LOD toggle, labels, hills from grid. Tests
    (PlayMode): demo at 4x for 3,000 ticks renders and captures without errors; no managed allocation in
    `Animate`/`Refresh` while idle.
15. **World expansion.** Generator, blueprints, triggers, side choice, camera clamp, unsurveyed strips, "SHOW" focus,
    HUD next-region. Tests: blueprint determinism; unlock sequence for a scripted delivery curve; generated producers
    validate; tracks outside the rectangle rejected; allocation budget for an unlock then 0 bytes after.
16. **Balance and release gate.** Tune against the §3 timings; max-scope fixture (30 cities, 4,000 tracks, 24 trains,
    8×8); 30-minute device soak at 4x via `Tools/soak.sh`; save size check; village → large city screenshots for `docs/`.

### 17. Testing strategy

- **Headless (`Tests/Program.cs`, .NET 8):** same seed → identical JSON after 36,000 ticks with growth; served city
  outgrows an unserved one by ≥ 4× buildings over 10 minutes; every action leaves a valid world (assert against
  `Terrain`, tracks, stations, other cities, rectangle); save mid-growth and replay equality; v1 fixture migrates and
  validates; corrupt city data (duplicate cell, wrong population, out-of-range def, building outside the rectangle)
  rejected branch by branch; unlock order for a scripted delivery curve; blueprint equality across two `Terrain`
  instances; soak with growth < 1 KB (as today); max-scope fixture reads the caps from balance.
- **EditMode (`RailwayTests.cs`):** the same domain checks under Newtonsoft; existing tests untouched except caps that
  move to balance; `SimulationSoakHasNoRecurringManagedAllocations` keeps `== 0` with growth enabled and expansion
  disabled; a new test covers one unlock's budget followed by 36,000 allocation-free ticks;
  `SpeedChangesSchedulingNotSimulation` naturally covers growth.
- **PlayMode (`GameplayTests.cs`):** demo with growth for 3,000 ticks + capture; tapping a building opens the city
  panel; a forced level-up shows a toast; unlocking a side creates its `ChunkView`s and widens the camera bounds.
- **Device:** `Tools/soak.sh` unchanged; the VR_PERF line gains dirty-chunk rebuild count and max rebuild ms; the
  release gate is re-run at the new limits (30 FPS, < 4 ms simulation, < 500 MB, no idle GC).
- **Manual:** a five-minute session confirming a new player notices "X has become a Village" and finds the new region.

### 18. Technical risks

| Risk | Mitigation |
|---|---|
| `Cell.Key` change and key aliasing outside the world | One `FromKey`; `World.InWorld` guard in `RailNetwork.At`; grep for `% 64`/`/ 64`; round-trip test over the full range. |
| Corridor ids replace the row-as-id convention | Authored ids stay 15/46 so v1 saves need no remap; `CorridorCells` is the single source for placement, removal and validation. |
| Preview A* hitch on a large unlocked area | Heap + bounding box shipped in M11, before the world can grow; measured per drag on device. |
| Growth breaks the exact-zero allocation invariant | Struct lists with reserved capacity, grid bytes, struct notifications, no lambdas/LINQ; test runs with growth on; expansion budgeted separately. |
| Cities box in stations or corridors | Bulldoze with cooldown, tracks cross roads, cities never touch platforms/strips/plazas; boxed-in growth is a silent no-op. |
| Day-0 layout or production drift breaks fixtures | Layout fixed to the 3×3 footprint; 420 population ⇒ exactly 20/min; demo unchanged. |
| Combined-mesh rebuild hitches | One chunk per frame, sleepers/detail split, VR_PERF measurement, 16×16 sub-batch fallback. |
| Payout inflation on a large world | Distance knee at 80 cells; valley payouts unchanged. |
| Codec divergence | Fields only, no readonly/computed/dictionary/float state, `Replace` on Newtonsoft; replay tests run under both. |
| Growth feels too fast/slow | All weights in `Balance.city`; a headless harness prints buildings per minute per scenario. |
| Negative coordinates in HUD text | Shown as-is; the valley corner stays (0,0); a display offset is a one-line presenter change if wanted. |

### 19. MVP vs future scope

**MVP:** population and levels; growth points from railway service; residential/commercial buildings appearing and
upgrading; simple roads and level crossings; station-oriented growth; passenger demand and storage from population;
city panel and milestone toasts; bulldoze of buildings; chunk meshes with LOD; automatic rectangular region unlocks
with generated terrain, villages and industries; mapVersion 2 saves with migration; performance gate at the new limits.

**Post-MVP:** player-purchased surveys of a chosen side; town rating and demolition penalties; congestion penalty from
station queues; Industrial and Special buildings (station district, town hall); goods demand from jobs; industry
output scaling with nearby population; road types and crossing sounds; a "News" list in the menu; east–west rivers.

**Future:** zoning and land value; pollution; road traffic; city budgets; skyscraper districts; city mergers into
named metros; lakes, elevation, tunnels; regional economies and industry chains; competitors.

## First-Time Player Tutorial

A new player's first game is a guided three-minute session on a small authored map. The tutorial drives the real
construction, purchase, routing, cargo, economy and city code through the normal UI; it never creates tutorial-only
tracks, trains, money or growth rules. A Core `TutorialDirector` advances a persisted state machine from gameplay events
and re-derives it from world state, so it survives saves, app kills and player mistakes. Presentation renders the current
step in the existing guidance strip plus a highlight (button, building, station, tiles, train, track endpoint) and
forwards touch facts (dragged, pinched, tapped) back to Core. The five-line coordinate guide (`Tutorial.cs`,
`WorldState.tutorialStep`) is replaced; the Settings "Tutorial hints" toggle stays as the switch for contextual one-shot
hints after the tutorial.

```
START ─▶ Camera ─▶ Coal Mine ─▶ Build Station ─▶ Build Destination Station ─▶ Build Track
  ─▶ Buy Train ─▶ Create Route ─▶ Load Coal ─▶ Deliver Coal ─▶ Earn Money ─▶ City Grows ─▶ FREE PLAY
```

| Time | Player sees / does |
|---|---|
| 0:00 | Greenfield fills the screen. "Welcome! Let's build your first railway." |
| 0:20 | Dragged and pinched. "✓ Camera controls learned". |
| 0:40 | Colliery inspected; Colliery Station placed. |
| 1:00 | Power Station placed. |
| 1:30 | Line confirmed between the two platforms. |
| 1:50 | Small freight bought at the Colliery Station. |
| 2:10 | Route Colliery ↔ Power started. Cards shrink to one line; camera follows the train. |
| 2:30 | "Loading coal…" 30/30; the train departs. |
| 2:45 | "Coal delivered!" money counter animates. |
| 3:00 | Camera returns to Greenfield; a house appears, population rises. "Now expand your railway." |

### 0. Scenario and map

`Maps.Greenfield` is the second authored map (`mapId = "greenfield"`, created at `SaveMigration.CurrentMapVersion`).
Under the chunk architecture (§9 of the city plan) it is exactly chunk (0,0), cells 0..31, unlocked rectangle 1×1, so
world expansion (§10) grows the world from it later. Until M11 lands it is a second `MapDefinition` instance.

| Producer | id | Kind | Cell | Notes |
|---|---|---|---|---|
| Greenfield Colliery | 1 | Mine | (7, 9) | inventory 60 at start, like Pinecrest |
| Greenfield Power | 2 | Plant | (25, 9) | accepts coal; `DrawPowerStation` |
| Greenfield | 3 | Town | (16, 22) | `CitySimulation.Found(w, p, b, 8)` = 420 people; camera start |
| Ashford | 4 | Town | (27, 28) | `Found(w, p, b, 4)`; outside the opening frame; gives free play a passenger chain, which is what feeds growth (§3 of the city plan) |

No water, bridge corridors, hills or blocked corners; ~90 trees kept 6 cells from producers (same generator, map
bounds). Camera starts at (16, 22), zoom 12; zoom clamps to 7..24 on this map. Money is `Balance.startingMoney`
($50,000). Both towns are founded exactly like Green Valley's, so `ValidateCities` passes unchanged.

Guided-path cost with the current balance: two stations at $2,300 each (station $2,000 + three platform straights), a
15-cell straight line on row 12 ($1,500), small freight $8,000 — **$14,100**, under a third of the starting funds.
First delivery pays 30 t × rate 2 × Manhattan distance 18 = **$1,080**. Card numbers come from `Balance` and the live
`BuildPlan`, never literals.

`WorldState.New(balance)` keeps building Green Valley (demo, tests, README untouched) and sets `tutorial.step = None`.
`WorldState.NewTutorial(balance)` builds Greenfield with `tutorial.step = Welcome`. `GameBootstrap.Demo` drops its
`tutorialStep = 0` line.

**Map seam (minimum the tutorial needs; M11 absorbs it into `Terrain`).**

- `Maps.cs` (Core): `MapDefinition` becomes an instance — `id, width, height, startX, startZ, startZoom, maxZoom`,
  `List<ProducerState> Producers(int mapVersion)` (Green Valley returns 5 or 7 by version, Greenfield 4),
  `Found` recipe per town, `InBounds/Water/Blocked(Cell, WorldState)`, `int Bridge(Cell)`, `BridgeSpan(int id, List<Cell>)`.
  `Maps.GreenValley`, `Maps.Greenfield`, `Maps.For(mapId)` (unknown → `InvalidDataException`), `Maps.Known(mapId)`.
  `Produces/Accepts` stay static cargo rules.
- **Key space stays 64.** `Cell.Key/FromKey`, the `% 64` decodes in `BuildService.Preview` and the `byte[64 * 64]` grid
  in `CitySimulation` are correct for any map ≤ 64 wide; the `MapDefinition.InBounds` guards in `RailNetwork.At` and
  the grid reads in `CitySimulation` protect the key space, not the map — rename that predicate `Cell.InKeySpace` and
  leave those sites alone.
- Sites that must use `map.InBounds/Water/Blocked/Bridge`: `BuildService.Placeable` (line 33), the bridge cost and span
  literals (`c.x == 30`, `x 30..32` at 172 and 179), `CitySimulation.ValidSite` (249, otherwise Greenfield grows into
  x 32..63) and `FreeNeighbours` water check, `SaveService.Validate` track/building/road checks (116, 198, 201) and the
  bridge span loop (128), `CameraController` clamp and `Ground()` (31–32, 45), `WorldView.BuildLand/BuildGrid/
  BuildScenery` (land loop, river and riverbank boxes, "BRIDGE SITE" labels, grid extent, tree range, hills — river and
  bridges only when the map has water).
- `GameSession` resolves `Map = Maps.For(w.mapId)` first and passes it to `RailNetwork`, `CitySimulation`,
  `BuildService`, `StationService`. `SaveService.Validate` builds the same objects and uses `Map.Producers(w.mapVersion)`
  instead of `WorldState.New(b)` and the `7 : 5` literal (95, 104–109); an unknown `mapId` still throws, so
  "Tampered save rejected" stays green.
- **Overwrite guard (blocking bug if missed):** `SaveService.Save` refuses to overwrite a file whose `mapId !=
  "green-valley"` (line 70). After the tutorial's first autosave every later save would throw. Use `Maps.Known`.
- `SaveMigration.Upgrade` runs for any `mapVersion == 1`; gate it on `mapId == "green-valley"`. Greenfield is created
  at the current version and never migrates.

### 1. Tutorial state machine

```csharp
public enum TutorialStep
{
    None = 0, Welcome, CameraDrag, CameraPinch, FindMine, BuildMineStation, BuildPlantStation, BuildTrack,
    BuyTrain, CreateRoute, WatchLoading, WatchDelivery, EarnMoney, CityGrowth, Expand, Complete
}
[Flags] public enum TutorialTool { None = 0, Explore = 1, Track = 2, Station = 4, Trains = 8, Bulldoze = 16, All = 31 }

[Serializable] public class TutorialState
{
    public int step;                       // TutorialStep as int; None for worlds without a tutorial
    public bool skipped, completed;
    public bool welcomeSeen, dragged, pinched, pinchSkipped, mineInspected, earnSeen, growthGranted, growthSeen;
    public bool objectiveRewarded, objectiveDismissed;
    public int mineStationId, plantStationId, trainId;   // for highlights; re-derived when they no longer resolve
    public int topUps;                     // protected-funds credits used (bounded by Balance)
}
```

`WorldState.tutorial` (default `new TutorialState()`) replaces `tutorialStep`. Public mutable fields only, no floats,
so both codecs round-trip it. `TutorialTool` lives in Core because `ToolMode` is a Presentation type; `GameBootstrap`
maps between them.

`TutorialDirector` (Core, `GameSession.Tutorial`) is a state machine over `(WorldState, TutorialState)` with the
session's `Pathfinder` and `Cities` for predicates:

- `Recompute()` sets `step` to the **first incomplete step** in script order. Steps 5–11 are predicates on world state
  (a station bound to producer 1 exists; one bound to producer 2 exists; `FindPath` between them both ways is non-null;
  a train is parked at or routed between them; that train has `a != 0`; `units > 0`; `delivered > 0`). Steps 1–4 and
  12–14 are not derivable from the world and complete through persisted flags. Recompute therefore fast-forwards (a
  player who built both stations before being asked skips ahead, as today's guide does) **and rewinds** (bulldozing
  the mine station during BuildTrack returns to BuildMineStation with "Rebuild the Colliery Station").
- Triggers: every `GameEvent` (synchronous observer, §3), every presentation fact (`NoteDragged()`, `NotePinched()`,
  `NoteProducerInspected(id)`, `Acknowledge()`, `SkipStep()` for the pinch fallback, `DismissObjective()`), `Skip()`,
  and construction of the session (load). Handlers set fields only; nothing in the tick path allocates.
- `Enabled` is `step != None && !completed`; when false every entry point returns immediately, so Green Valley worlds
  and the demo pay nothing and the EditMode soak (`SimulationSoakHasNoRecurringManagedAllocations`) is unaffected.
- `AfterTick()` runs inside `GameSession.Step()` after `Cities.Step()` and performs the tutorial's own mutations between
  ticks, deterministically and replay-safe: the growth grant (§2 Step 11), the objective reward (§2 Step 12) and the
  protected-funds top-up (§9), each guarded by a persisted flag so a restored save never repeats them.
- `Current` returns a `TutorialCue` struct: step, message, secondary text (cost, cargo bar, hint), target
  (`TutorialTarget { Kind, id, cell, buttonPath }`), allowed `TutorialTool`s, `dimUi`, camera focus (cell, zoom),
  `followTrain`, `highlightSpeedButton` (set whenever `speed == 0` in a step that needs simulation time, because
  `Load` and `OnApplicationPause` leave the world paused). Presentation reads it on every HUD refresh (0.25 s).

### 2. Tutorial steps

Card text ≤ 2 short lines. "Allowed tools" are the toolbar buttons that stay interactable; the rest are dimmed and
disabled (`Button.interactable = false`). MENU, the speed buttons from Step 8 on, and the map itself are always usable.

| # | Step | Card | Highlight | Allowed tools | Completes when |
|---|---|---|---|---|---|
| 0 | Welcome | "Welcome! Let's build your first railway." + LET'S GO | strip only (UI dimmed 40 %) | Explore | `welcomeSeen` |
| 1a | CameraDrag | "Drag to move around the map." | pan arrows around the world centre | Explore | `dragged`: one-finger pan ≥ 80 px cumulative |
| 1b | CameraPinch | "Pinch to zoom." → "✓ Camera controls learned" | none | Explore | `pinched`: zoom ratio ≥ 1.15 or ≤ 0.87 within one two-finger gesture (mouse wheel counts in the editor); or `pinchSkipped` via SKIP STEP shown after 20 s |
| 2 | FindMine | "Coal mines produce cargo." → (panel open) "Build a station next to the coal mine." | ring on the Colliery; camera `FocusOn((7,9), 11)` | Explore | `mineInspected`: `GameBootstrap.Select` opened the producer panel for producer 1 |
| 3 | BuildMineStation | "Build a station next to the coal mine." Secondary: "$2,300" | STATION button (pulse) → valid centre tiles → CONFIRM in the station panel | Explore, Station, Bulldoze | a station with `producerId == 1` exists → "✓ Colliery Station built" |
| 4 | BuildPlantStation | "The power plant needs coal." → "Build another station here." | ring on the plant; camera `FocusOn((25,9), 11)`; STATION, tiles, CONFIRM | Explore, Station, Bulldoze | a station with `producerId == 2` exists → "✓ Power Station built" |
| 5 | BuildTrack | "Connect the two stations." / "Drag from one platform end to the other." Secondary mirrors `plan.cost` / `plan.reason` | BUILD TRACK button; the two platform end cells facing each other; CONFIRM BUILD | Explore, Track, Bulldoze | `FindPath` both ways between the two platforms → "✓ Stations connected" |
| 6 | BuyTrain | "Now we need a train." → (station panel) "Buy your first train." | Colliery Station ring; BUY A TRAIN; the single train card | Explore, Trains | a train whose `stationId` is either tutorial station → "Your first train is ready!" |
| 7 | CreateRoute | "Give Train #1 a route." Panel shows "Colliery Station ↓ Power Station ↓ Repeat" | CREATE ROUTE; the Power Station row; START ROUTE | Explore, Trains | that train has `a != 0` |
| 8 | WatchLoading | "Loading coal…" + cargo bar `██████ 30/30`; one-line strip; camera `Follow(train)` | train ring | All except Bulldoze | `units > 0` |
| 9 | WatchDelivery | nothing while travelling → "Coal delivered!" `+ $1,080` floating text, money counter animates | train ring | All except Bulldoze | `delivered > 0` |
| 10 | EarnMoney | "Deliveries earn money." (3 s or tap) | money label | All | `earnSeen` via `Acknowledge()` |
| 11 | CityGrowth | camera `FocusOn(Greenfield)`; "Greenfield · Population 420" → "Good transport helps towns grow." → house appears → "Greenfield is growing!" 420 → 460 | Greenfield ring, then the new building | All | `growthSeen` after `CityPopulationChanged` for Greenfield + 3 s |
| 12 | Expand | "Now expand your railway." Objective line: "Connect Greenfield Station · Reward $10,000" (DISMISS) | Greenfield ring while shown | All | `objectiveRewarded` or `objectiveDismissed` → `completed`, step Complete |

Where the steps touch real systems:

- **Step 3/4 station flow (new UX for all play).** `StationService.Plan(Cell center, int axis, int producerId)` returns
  a `StationPlan { valid, reason, cost, side, BuildPlan platform, producerId }`. On empty cells the platform is
  `Build.ValidateBuild(three cells)` — its endpoint-stub rule (BuildService 143–147) yields three straights of the
  right mask — and `cost = platform.cost + stationCost`; on an existing straight, `platform` is empty and cost is
  `stationCost`. `Place(StationPlan)` re-validates, checks `money ≥ cost` **before** committing, then commits platform
  and station in one transaction (station id first, `paid = stationCost`; platform pieces keep their own `paid`).
  `StationService` gains the `BuildService` reference (constructed first in `GameSession`). The existing
  `Place(center, producerId)` stays as a wrapper over `Plan/Place` for the existing-track case, so the demo, `Coal()`
  fixtures and `InvalidStationPlacementsAreRejectedWithoutSideEffects` are unchanged.
  Presentation: in Station mode a tap shows the platform preview through `WorldView.Preview(plan.platform)` and the
  panel `NEW STATION · Greenfield Colliery · $2,300` with ROTATE, CONFIRM, CANCEL; with several producers in catchment
  the panel lists them, with one it is preselected. Valid tiles come from `StationService.Candidates(producerId, axis,
  List<Cell> into)`: centres whose three cells pass `Plan` (≤ 13×13 cells scanned when the step starts; list reused).
  Bulldozing a station keeps its platform as ordinary track ("Station removed. Its platform track remains.");
  the tutorial predicate for Step 5 is connectivity, not `tracks.Count`, so this never confuses the director.
- **Step 5 track flow.** `BuildService.Preview` gets two port-aware fixes (general correctness, not tutorial-only):
  when the start cell already has track, seed only directions in its mask (lines 43–49 seed all four with cost 0, so
  A* can leave a platform sideways and `ValidateBuild` then rejects the plan through `Protected`); accept the goal
  only when the end cell's mask contains the incoming port (line 70). `Protected` splits its reason: "Station
  platforms can't be altered" vs "Park the train and clear its route". A drag that starts on any platform cell snaps
  its anchor to the platform end nearest the pointer (`GameBootstrap.BeginTrack`). The preview mesh already shows
  green/red; the strip echoes `plan.cost` and `plan.reason`.
- **Step 6 shop.** While `World.tutorial` is enabled and `step ≤ CreateRoute`, `Shop` shows one `TrainCard`
  (Small freight · Coal) and calls the same `Trains.Buy(stationId, 0, Cargo.Coal)`. Keyed on world state, not
  `PlayerPrefs`, so `NewTrainModelsRenderAndAppearInShop` (three coal cards at the demo mine, six at a town) stays green.
- **Step 7 route panel (new UX for all play).** CHOOSE DESTINATION becomes CREATE ROUTE. The panel keeps AUTO
  DESTINATION as its first row (hidden while the tutorial is enabled) and lists stations; picking one shows
  "Stop 1 ↓ Stop 2 ↓ Repeat" and a START ROUTE button that calls `Trains.AssignRoute(train, current, picked)`.
  `AutoDestinationButtonStartsService` changes one string.
- **Step 8–9 camera.** `CameraController.Follow(trainId)` keeps the locomotive centred until the player drags.
- **Step 11 growth grant.** On the first `CargoDelivered` the director sets `growthGranted`; `AfterTick()` calls
  `Cities.GrantHouse(city)`. `Act` is private and, on a day-0 town, would lay a street first (`needsStreet` is true
  with one road and eight houses), so `GrantHouse` runs `Expand` directly — the real candidate/scoring/`ValidSite`
  code places a level-1 residential building on the ring around the plaza — then `Recount`, `cityRevision++` and a
  `CityPopulationChanged` event (420 → 460; a house is +40 in the catalog). The presenter already refreshes the town
  mesh and the "SMALL VILLAGE · POP" label from `cityRevision`. If `GrantHouse` finds no site the step still
  completes on `Acknowledge()`. A second delivery grants nothing (`growthGranted`).
- **Step 12 objective.** Completes when a station bound to Greenfield exists and `FindPath` from it reaches any other
  station. `AfterTick()` credits `Balance.tutorialObjectiveReward` ($10,000, non-delivery credit), sets
  `objectiveRewarded` and `completed`; Presentation writes `PlayerPrefs tutorialCompleted = 1`. From Expand on, no
  tool is gated.

### 3. Required gameplay events

> **Built (2026-09-24, for sound effects):**
> - `Core/GameEvents.cs` exists with the kinds in the table below. `GameEvent` carries `kind, id, aux, value` plus a `Cell cell`, so the presentation can place and cull sounds.
> - Every push site in the table is live except `CargoLoaded` (declared, not pushed) and `CityPopulationChanged` (not built).
> - `Presentation/SoundEffects.cs` drains the queue each frame.
> - The director's `Observer` delegate is not built yet.

`GameEvents` (Core, `GameSession.Events`) follows the existing `CitySimulation.Notifications` pattern rather than
adding a second mechanism: a pre-sized `Queue<GameEvent>` of structs (`kind, id, aux, value`; capacity 64, oldest
dropped) plus **one** synchronous Core observer (`Action<GameEvent> Observer`, the director) invoked at push time.
Services push at the site that already knows the fact; the presenter drains the queue each frame exactly as it drains
city notifications today (`GameBootstrap.Update` 141–142), for toasts, the money animation and the overlay. Pushing a
struct and invoking a pre-bound delegate allocate nothing.

| Event kind | Pushed from |
|---|---|
| `StationBuilt(stationId)` | `StationService.Place` after `w.revision++` |
| `TrackBuilt(cost)` | `BuildService.CommitBuild` |
| `Bulldozed(cellKey)` | `BuildService.Bulldoze` (station or track) |
| `TrainPurchased(trainId)` / `TrainSold(trainId)` | `TrainSimulation.Buy` / `Sell` |
| `RouteCreated(trainId)` | `TrainSimulation.AssignRoute` on success (`AutoDestination` goes through it) |
| `TrainDeparted(trainId)` / `TrainArrived(trainId)` | `TrainSimulation.Step` on Loading → Travelling / at the last path step |
| `CargoLoaded(trainId, units)` | `CargoService.Service` boarding branch |
| `CargoDelivered(trainId, units, revenue)` | **both** unload paths: `CargoService.Service` and `TrainSimulation.UnloadOnly` |
| `CityPopulationChanged(cityId, before, after)` | `CitySimulation` after `Recount` in `Act`, `Bulldoze` and `GrantHouse` when population changed |
| `CityLevelChanged(cityId, level)` | folded from `Notification.LevelChanged` in a later cleanup; the city queue stays for now |

Not events: money changes (`EconomyService` is static; a static event would leak across the many sessions the tests
create — the director and the HUD read `w.money`), and build rejections during previews (`Preview` runs on every drag
frame). Rejections reach the director as a presentation fact from `GameBootstrap.Perform` failures, which already
holds the `Result`. Other presentation facts: `CameraController.Dragged(px)`, `CameraController.Pinched(ratio)`,
`ProducerInspected(id)`, `PanelOpened(key)`. `GameBootstrap` forwards them as `Note*` calls.

Ordinary systems never consult the tutorial. The only tutorial-aware branches in gameplay code are the refund
percentage (§9), read through `Balance` and `w.tutorial`, and the shop/tool gating in Presentation.

### 4. Highlight system

`TutorialHighlighter` (Presentation) resolves the cue's target to a screen rectangle every `LateUpdate` while a cue is
active and draws the matching emphasis:

| Target kind | Resolution | Emphasis |
|---|---|---|
| UI button (`buttonPath`, e.g. `Safe area/Toolbar/STATION`, `Safe area/Context/Scrollable details/CONFIRM BUILD`) | `transform.Find(path)` cached per step and re-found when the cached reference is destroyed (`Clear()` rebuilds context children) | dimmer cutout + pulse (scale 1 → 1.06, 0.9 s) on the button image |
| Producer / station / city | world bounds (industry 3×3, town buildings, platform + strip) projected with `WorldToScreenPoint` | flat ring mesh at ground level, pulsing alpha, label untouched |
| Tile set | cells from `Candidates` | `WorldView.HighlightCells(list, color)` — a second dynamic mesh next to the preview mesh |
| Track endpoint | platform end cell | small ring + bouncing cone above the cell |
| Train | `RailGeometry.TrainPosition` each frame | ring following the locomotive |
| Off-screen world target | projected point outside `safe.rect` | edge arrow at the clamped screen edge; the cue's camera focus also moves the camera at step start |

Dimming: when `cue.dimUi` is set, four black quads (alpha 0.4, **`raycastTarget = false`** — `CameraController.OverUI`
treats any raycast-target Image as UI and would block pans and taps) cover the screen minus the target rect padded by
12 units. World targets are never dimmed. Meshes are owned by `WorldView` like the preview mesh.

### 5. UI overlay architecture

The screen has no free quadrant: HUD 140 units top, toolbar 140 bottom, the guidance strip above it, context panel 450
wide on the right. The tutorial card therefore **is the guidance strip** (`GameplayPresenter.guidance`, already sized
to the context panel and hidden behind menus): line 1 the step message (today's `TutorialText` slot), line 2 the
secondary text or `Notice`, and on the right up to two 96-unit buttons (LET'S GO / GOT IT / SKIP STEP / DISMISS) and a
small muted "Skip tutorial" link. The strip never covers a world target because every world target gets a camera focus
into the left/middle band, and never covers a UI target because the toolbar is below it and the context panel beside
it. A screen-space pointer triangle runs from the strip edge toward the target when it is on screen.

Also in `TutorialOverlay` (child of the UI canvas, procedural like everything else): the dimmer (§4); "✓ …" toast for
1.5 s above the strip; floating `+ $1,080` rising from the money label; a "Skip the tutorial?" CANCEL / SKIP dialog on
the menu panel; the Step 12 objective line inside the strip. `GameplayPresenter` gains the money animation (displayed
value lerps to `w.money` over 0.8 s on change — a general HUD improvement), the station preview panel, the route panel,
the shop filter, and REPLAY TUTORIAL in Settings **only from the title screen** (`app.InMenu`): `NewGame` never saves
the current game, only `Title()` does, so an in-game replay button would lose progress; in-game Settings shows
"Return to title to replay the tutorial." The title's NEW GAME calls `StartTutorial()` while `PlayerPrefs
tutorialCompleted == 0`, else `NewGame(false)`; `NewGame(bool demo)` keeps its signature and Green Valley, so the
PlayMode tests that call it keep their coordinates.

The overlay reads `director.Current` on each HUD refresh and rebuilds texts only when the step or secondary text
changes; steady-state frames build no strings.

### 6. Input validation

The tutorial never advances on "Next"; every progression is a fact:

| Step | Verified by |
|---|---|
| Camera | `CameraController` reports cumulative one-finger pan ≥ 80 px; pinch reports a zoom ratio outside 0.87..1.15 within one two-finger gesture |
| Coal mine | `Select` opened the producer panel for producer 1 |
| Stations | `w.stations` contains one bound to producer 1 / producer 2 (`StationBuilt` triggers, predicate confirms) |
| Track | `Pathfinder.FindPath` between the two platform centres, both directions |
| Train | a `TrainState` parked at either station (`TrainPurchased` triggers) |
| Route | that train's `a/b` are the two stations (`RouteCreated` triggers) |
| Loading | `CargoLoaded` for that train (`units > 0`) |
| Delivery | `CargoDelivered` with `delivered > 0` |
| City | `CityPopulationChanged` for Greenfield's city after the grant |
| Objective | a Greenfield-bound station connected by `FindPath` to another station |

Tool gating is defensive, not the validation: anything the player does through an allowed tool (including bulldozing)
is handled by `Recompute()`, never by trusting the UI.

Mistake hints (secondary text on the strip; the director maps `Perform` failure reasons and world facts):

| Situation | Hint |
|---|---|
| Station tapped outside catchment ("No eligible industry…") | "Place the station closer to the coal mine." |
| Station strip blocked / on a curve / on a house | "Tap one of the highlighted tiles." |
| Track preview invalid (`plan.reason`) | "Tracks can't be built here." + reason |
| Track confirmed but the line does not reach the other platform | "Keep going: the line must reach the Power Station." |
| Second finger cancels the track drag (`CameraController` cancels previews on a second touch) | "Use one finger to draw the track." |
| Player sells the train during 7–9 | rewind to BuyTrain, "Buy a train at the Colliery Station." |

### 7. Save/resume behavior

- `TutorialState` sits inside `WorldState`, so every manual save, autosave (each changed minute, on backgrounding, on
  quit) and the title-screen autosave carry the step, flags and remembered ids. Envelope, checksum and schema version
  are unchanged. `SaveService.Validate` checks `tutorial != null`, `0 ≤ step ≤ Complete` and `topUps ≤ limit`;
  remembered ids that no longer resolve are zeroed by the director, never rejected.
- On load `GameSession` constructs the director and calls `Recompute()`: a save taken at BuyTrain with both stations
  and the line resumes at BuyTrain; one whose train was sold before the kill resumes at BuyTrain again; one that
  somehow completed the route resumes at WatchLoading.
- `Load` and `OnApplicationPause` leave the world at speed 0 and the notice says "Select 1x to continue." In steps that
  need simulation time (8–11) the cue sets `highlightSpeedButton`, so the 1x button pulses until the player unpauses.
- A preview in progress lives in `GameBootstrap` and is never saved; resuming at BuildTrack simply shows the endpoint
  highlights again.
- Saves that still carry `tutorialStep` load with a default `TutorialState` (`step = None`): no tutorial on an old
  world, no migration code, field removed.

### 8. Skip/replay behavior

- "Skip tutorial" is a small muted link on the strip, never a primary button. It opens "Skip the tutorial?" CANCEL /
  SKIP. SKIP calls `director.Skip()`: `skipped = true`, `step = Complete`; gating, cards and highlights go away; the
  world keeps whatever was built; refunds return to normal. Presentation writes `PlayerPrefs tutorialCompleted = 1`
  and `tutorialSkipped = 1`.
- Device flag vs world flag: `PlayerPrefs.tutorialCompleted` (device profile, like `sound`/`hints`) decides what NEW
  GAME starts; `WorldState.tutorial.completed/skipped` records what happened in that world.
- REPLAY TUTORIAL (title-screen Settings) starts `StartTutorial()` and resets nothing else; the next NEW GAME is still
  Green Valley.
- Contextual hints after the tutorial (existing "Tutorial hints" toggle): one-shot strip messages keyed by a
  `PlayerPrefs hintsSeen` bitmask — first Bulldoze, first junction preview, first "This railway already has a train",
  first insolvency, first city level-up, first new region. Same strip, no state machine.

### 9. Error recovery

- **Mistakes are allowed.** Only the wrong tool is locked; every rejected action yields a hint, and `Recompute()`
  rewinds if the player removes something the tutorial needed.
- **Money can never strand the tutorial.** `Balance` gains `refundPercent = 50` (replacing the `/ 2` literals in
  `Bulldoze` 270/285 and `Sell` 287 — Green Valley tests such as `BridgeRemovalIsAtomicAndRefundsOnce` (+750) and
  `InsolvencyCanRecoverBySelling` (4000) keep their numbers because the default is unchanged), `tutorialRefundPercent
  = 100`, `tutorialTopUpLimit = 3`, `tutorialObjectiveReward = 10000`. While the step is between BuildMineStation
  and CreateRoute, bulldozing and selling refund 100 %. As a last resort `AfterTick()` compares `money` with the
  cost of the current step's action and credits the shortfall (non-delivery credit) at most `tutorialTopUpLimit`
  times per world, with the toast "Tutorial funds topped up"; if the train sits in `InsufficientFunds` it also calls
  `Trains.Resume(trainId)`, since `Resume` refuses at `money == 0` and the train would otherwise never move.
- **Never restart the game.** Pinch has SKIP STEP after 20 s; every world-derived step can be satisfied in any order;
  the growth step completes even if no house site exists; the objective can be dismissed.
- **Defensive validation on load:** an inconsistent `TutorialState` (e.g. `growthGranted` with `delivered == 0`) is
  re-derived, not rejected; a save is never refused for tutorial fields.

### 10. Android-specific considerations

- Touch targets: every tutorial button goes through `GameplayPresenter.Button`, which enforces 96×96 units at the
  1600×900 reference; placement tiles are whole cells (≥ 60 px at zoom 11).
- Safe areas: the strip, buttons, pointer and toasts are children of the existing `safe` panel (`SafeArea()` runs every
  frame); only the dimmer spans the full display. Cutouts use `safe.rect`.
- Aspect ratios: the strip already resizes with `safe.rect.width` and the context panel (`LateUpdate` 161–162); on
  widths under 1400 units the HUD hides its brand text, and the strip's two buttons collapse to one plus the link.
- Instructions never cover the target: strip position plus camera focus for world targets (§5).
- Android Back: with the skip dialog open, Back cancels it; otherwise `GameBootstrap.Back()` behaves as today (leave
  settings/recovery, close menu, cancel preview, open menu). Back never advances or skips a step.
- Backgrounding: `OnApplicationPause` pauses and autosaves; the tutorial rides along; on resume the strip rebuilds from
  `Current` with the 1x highlight.
- Gestures: pinch uses the existing two-touch branch; the second-finger preview cancel becomes a hint; SKIP STEP
  covers phones with flaky multitouch; mouse wheel and drag satisfy the camera steps in the editor and the Mac preview.
- Performance: the highlighter updates one rect and one ring per frame, reuses its lists and meshes, sets text only on
  change and runs only while a cue is active; Core handlers are allocation-free; the EditMode soak keeps `== 0`.
- Input System: no new bindings.

### 11. Required scripts/classes

Core (`Assets/Game/Scripts/Core`):

- `Maps.cs` (new) — `MapDefinition` instance, `Maps.GreenValley/Greenfield/For/Known`, `Cell.InKeySpace`.
- `GameEvents.cs` (new) — `GameEvent` struct, kinds, queue and observer (§3).
- `Tutorial.cs` (rewritten) — `TutorialStep`, `TutorialTool`, `TutorialState`, `TutorialTarget`, `TutorialCue`,
  `TutorialScript` (step table: texts, targets, allowed tools, predicates, hints) and `TutorialDirector`.
- `Models.cs` — `WorldState.tutorial`, `NewTutorial(balance)`, `Balance.refundPercent/tutorialRefundPercent/
  tutorialTopUpLimit/tutorialObjectiveReward`; static `MapDefinition` reduced to `Produces/Accepts`.
- `StationService.cs` — `StationPlan`, `Plan(center, axis, producerId)`, `Place(StationPlan)`, `Candidates`,
  `BuildService` reference, map-aware checks, `StationBuilt`.
- `BuildService.cs` — map-aware `Placeable`/bridges, port-aware `Preview` seeding and goal test, split `Protected`
  reason, `refundPercent`, `TrackBuilt`/`Bulldozed`.
- `CitySimulation.cs` — map in the constructor, `ValidSite`/`FreeNeighbours` via the map, `GrantHouse(CityState)`,
  `CityPopulationChanged` push after `Recount`. (Small; land after the concurrent city work settles.)
- `Simulation.cs` — `GameSession.Map/Events/Tutorial`, `Step` order commands → tick → cargo → trains → cities →
  `Tutorial.AfterTick()` → ledger trim; event pushes in `Buy/Sell/AssignRoute/Step/UnloadOnly` and `CargoService`.
- `SaveService.cs` — map-aware validation and overwrite guard, `tutorial` checks; `SaveMigration.cs` gated by `mapId`.
- `RailNetwork.cs` — `At` uses `Cell.InKeySpace`.

Presentation (`Assets/Game/Scripts/Presentation`):

- `TutorialOverlay.cs` (new) — strip content, buttons, link, pointer, dimmer, toasts, floating money text, skip dialog,
  objective line.
- `TutorialHighlighter.cs` (new) — target resolution, rings, tile highlights, endpoint cones, edge arrow.
- `CameraController.cs` — `Dragged`/`Pinched` events, `FocusOn(cell, zoom, seconds)`, `Follow(trainId)`, map bounds
  and `maxZoom`.
- `WorldView.cs` — map-driven land/grid/scenery (river and bridge sites only with water), `HighlightCells`, `Ring`,
  station platform preview.
- `GameplayPresenter.cs` — station preview panel (ROTATE/CONFIRM/CANCEL), route panel (CREATE ROUTE / START ROUTE),
  shop filter, money animation, REPLAY TUTORIAL (title only), `guidance` as the card host.
- `GameBootstrap.cs` — `StartTutorial()`, tool gating in `ChooseTool` from the cue, fact forwarding, platform-end snap
  in `BeginTrack`, event-queue drain, `Back()` with the skip dialog, `tutorialCompleted` write, `Demo` cleanup.
- `Editor/ProjectSetup.cs` — add `✓↓█` to the font's `TryAddCharacters`.

Tests: `Tests/Program.cs`, `Assets/Game/Tests/EditMode/RailwayTests.cs`, `Assets/Game/Tests/PlayMode/GameplayTests.cs`.

### 12. Tutorial test cases

Headless (`Tests/Program.cs`) and EditMode (`RailwayTests.cs`), on `WorldState.NewTutorial`:

1. **Scripted playthrough:** perform the guided actions through the real services in order; after each, the director's
   step and the cue's target kind/id match the table in §2; ends `completed`.
2. **Fast-forward:** build both stations and the line before touching the director; `Recompute()` lands on BuyTrain.
3. **Rewind:** at BuildTrack, bulldoze the mine station → BuildMineStation; at CreateRoute, sell the train → BuyTrain.
4. **Resume from every step:** capture/restore a snapshot with `SaveService` at each step; the restored director
   reports the same step and the rest of the playthrough ends with byte-equal snapshots (extends
   `SaveDuringTravelContinuesExactly`).
5. **Flags persist:** `dragged/pinched/mineInspected/earnSeen` survive a round trip; `growthGranted` with
   `delivered == 0` is re-derived, not rejected.
6. **Predicates:** a one-way connection is not "connected"; a train on another network does not satisfy BuyTrain.
7. **Events:** `CargoLoaded` precedes `TrainDeparted`; `CargoDelivered` fires once per unload on both unload paths
   with the revenue `totalIncome` gained; `RouteCreated` only on success; the queue drops oldest at capacity.
8. **Growth grant:** the first delivery yields exactly one `CityPopulationChanged` for Greenfield (420 → 460) and one
   new building on a `ValidSite`; a second delivery grants nothing; `GrantHouse` on a boxed-in city returns false.
9. **Objective:** a Greenfield station connected to Ashford credits $10,000 once; unconnected does not; dismiss
   completes without the credit.
10. **Protected funds:** with `money = 0` at BuildTrack, `AfterTick()` credits the shortfall; the fourth shortfall is
    not credited; an `InsufficientFunds` train is resumed; bulldozing refunds 100 % during guided steps and 50 %
    after `Complete`/`Skip`.
11. **Skip:** `Skip()` at any step yields `Complete`, no gating, unchanged money and entities.
12. **Station on bare ground:** `Plan` on empty cells lays three straights with the right mask and side, charges $2,300
    atomically (a rejected plan changes nothing); refuses blocked/occupied cells and missing catchment; `Plan` on an
    existing straight charges $2,000; bulldozing the station leaves its platform track.
13. **Port-aware preview:** a drag from a platform end toward a cell off-axis never proposes a turnout inside the
    platform; a plan ending on a platform end enters through its port.
14. **Map seam:** Greenfield validates (4 producers, 2 cities, no bridges); Green Valley by version (5 or 7); a
    snapshot with `mapId = "greenfield"` and Green Valley producers is rejected; `Preview` on Greenfield never leaves
    0..31; Greenfield's towns never build outside 0..31 after 12,000 ticks; a Greenfield autosave can be overwritten
    by the next Greenfield autosave (the guard).
15. **Zero allocation:** `SimulationSoakHasNoRecurringManagedAllocations` unchanged on Green Valley; a twin on
    Greenfield with the director enabled and a coal service running stays at `== 0` bytes.
16. **Replaced tests:** `TutorialFollowsWorldStateAndEndsAfterFirstDeliveries` and `Program.cs` lines 199–202 become
    tests 1–3; `AutoDestinationButtonStartsService` uses the renamed button; all other tests untouched.

PlayMode (`GameplayTests.cs`):

17. `StartTutorial()` shows the Welcome strip, the dimmer and no interactable tool button except EXPLORE; captures at
    1600×900 and 2340×1080.
18. After a simulated pan and a wheel zoom the cue is FindMine with the ring on the colliery; after `Select((7,9))`
    the STATION button is highlighted and interactable.
19. The highlighted rect follows a context button across `Clear()`/rebuild; an off-screen world target shows the edge
    arrow; the strip never intersects the target rect over 30 frames of pan.
20. Skip dialog: Back closes it; SKIP removes the overlay and enables every tool.
21. A guided run at 4x through the public calls of test 1 renders without errors and ends with `tutorialCompleted`
    set (cleared in teardown); the demo world's shops still show every card (three at the mine, six at a town).

Device (manual, added to `VALIDATION.md` gates): a new-player session on the Samsung SM-S731B timed against the pacing
table; pinch on a second device; app kill during Step 6 and resume; Back at each step; a 4:3 tablet layout.

### 13. Development milestones

Continues the numbering. M17–M20 do not depend on M11/M14/M15; M17 is the minimum map seam and is written so M11's
`Terrain` replaces `MapDefinition` behind `GameSession.Map` without touching call sites.

17. **Map seam and Greenfield.** `Maps`, `Cell.InKeySpace`, map-aware services/validation/migration/overwrite guard/
    view/camera, `NewTutorial`; tests 14.
18. **Stations on bare ground, port-aware preview, route panel, events.** `StationPlan`, candidates, platform snap,
    `Preview` fixes, CREATE ROUTE flow, `GameEvents` pushed everywhere, `refundPercent`; tests 7, 12, 13, 15.
19. **Tutorial core.** `TutorialState`, script, director, `AfterTick`, protected funds, skip, `GrantHouse` and the
    population event; tests 1–6, 8–11, 16.
20. **Tutorial presentation.** Strip content, highlighter, camera focus/follow, shop filter, replay/skip UI, money
    animation, Back handling; tests 17–21; device usability session; README "First delivery" and VALIDATION gates.

### 14. Risks

| Risk | Mitigation |
|---|---|
| Concurrent city work edits the same files (`CitySimulation`, `SaveService`, `Models`) | M17–M18 touch them in small, named places; `GrantHouse` and the population event land last; re-read before each edit. |
| Map seam collides with M11 `Terrain` | Instance behind `GameSession.Map`; key space untouched; M11 swaps the instance. |
| Station on bare ground breaks fixtures/demo | Existing-track path unchanged behind the old `Place` signature; tests 12. |
| Events make the tick allocate | Struct queue + one pre-bound observer; handlers set fields; soak twin on Greenfield. |
| Guidance strip too small for a message plus buttons at 16:9 phones | Two lines at font 18–22 plus 96-unit buttons fit the current 108-unit strip height; grow to 132 when a button is shown; capture test 17. |
| Pinch fails on some phones | SKIP STEP after 20 s; wheel in the editor; second-device check. |
| Growth demo feels scripted | One real placement through the real candidate code, labelled as growth; the passenger chain to Ashford then drives real growth. |
| Tutorial funds exploited | Top-up bounded per world and only while a required action is unaffordable; refunds only during guided steps. |
