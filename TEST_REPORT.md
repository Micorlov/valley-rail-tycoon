# Valley Rail test report

Date: 2026-09-24  
Project: `com.valleyrail.tycoon`  
Result: **17,454 assertions passed**

For plain-language individual test descriptions, see [TEST_DESCRIPTIONS.md](/Users/michael/Downloads/transport/TEST_DESCRIPTIONS.md).

## How the tests were run

The headless core suite was run with:

```sh
dotnet run --project Tests/CoreChecks.csproj
```

Final output:

```text
30 simulated minutes / 1 active train: 39.79 ms, 40 bytes
PASS: 17454 assertions
```

The suite compiles the production core scripts together with `Tests/Program.cs`. Every `Assert` is counted; a failed assertion stops the process with a non-zero exit code.

## Test coverage

### Game events and construction

- Rejected station, track, train and route commands produce no event or side effect.
- Track construction records both endpoints and cost.
- Re-laying existing track is idempotent.
- Station construction and removal events contain the correct IDs and cells.
- Bulldozing tracks, stations, bridges, route legs, occupied platforms and active infrastructure follows protection rules.
- Bridge removal is atomic and refunds exactly once.
- Track, station and train limits are enforced.
- Curves, turnouts, crossings and route transitions are validated.
- The bounded event queue keeps the newest events and clears correctly.

### Trains, cargo and economy

- All six train models are checked against valid and invalid cargo types.
- Invalid models and cargo values are rejected without charging the player.
- Purchase prices, capacities, running costs and sale refunds are verified.
- Automatic destination selection rejects missing, disconnected, duplicate-producer and incompatible stops.
- Automatic destination selection chooses the shortest reachable compatible station.
- Coal, goods, passenger, wood, oil, iron ore and steel routes load, travel and deliver correctly.
- Revenue is based on delivered units, cargo rate and producer distance.
- Running costs enter the ledger; construction and purchases do not appear as recurring expenses.
- Zero-funds trains stop safely and can be sold without corrupting the economy.
- Return-to-station requests unload cargo once and leave trains parked.
- Fleet numbers remain sequential and independent of shared world entity IDs.

### Industries and supply chains

- Raw industries replenish inventory and obey storage limits.
- Sawmills, refineries and steel mills require input cargo before producing output.
- Processing output is collected once and can continue to another factory or town.
- Wrong input cargo is rejected.
- The original power stations and Valley Works chain continue to function.
- The timber railway is simulated end to end.

### Expanded map and terrain

- Cell keys are unique and reversible across the full `128 × 128` map.
- The map contains 16,384 tiles, four times the original 64 × 64 area.
- Coordinates beyond the former x/z=63 boundary do not alias old cells.
- New bridge rows 78 and 110 are buildable.
- Raised mountains reject track construction and city growth.
- Lakes reject construction.
- Railways can route around mountain foothills.
- Grassland, woodland, dry plains and snowy peak surface classifications are distinct.
- New towns and industries accept nearby stations.
- Expanded-map railways and save data survive capture and restore.
- Version 4 saves retain the prior 13-producer layout; older version 2/3 saves retain their original layouts.

### Signals and routing

- Diamond crossings allow straight-through movement on both axes and reject turns.
- Pathfinder routes cannot turn through a diamond crossing.
- Trains wait at red signals and resume on green.
- Occupied crossings hold the correct direction green and block conflicting traffic.
- Signal state and train movement replay deterministically after save/load.
- Signalled coal routes deliver successfully.

### Saves, migration and deterministic simulation

- Save snapshots round-trip train, cargo, route, industry, city and terrain state.
- Tampered map IDs and duplicate entity IDs are rejected.
- Legacy saves without cities migrate to day-zero towns.
- Mid-route save/load produces an identical replay.
- Paused simulation does not advance ticks.
- 1×, 2× and 4× speed scheduling preserves the expected tick count.
- Delayed catch-up retains pending simulation ticks.
- The 30-minute delivery soak completes without discarded state.
- The simulation hot loop allocates only 40 bytes in the measured soak.

### Towns and tutorial

- Towns start with the expected population, buildings, passenger production and storage.
- Passenger and goods deliveries count toward town growth.
- Tutorial steps advance from construction through the first delivery and then end cleanly.
- Town buildings and roads do not overlap tracks or stations.

## Android verification

The successful combined Android build is recorded in [Logs/android-roads.log](/Users/michael/Downloads/transport/Logs/android-roads.log). The APK was installed with `adb install -r` on device `R5GYB2NHR5D`.

The installed app was launched and verified with:

```text
com.valleyrail.tycoon/com.unity3d.player.UnityPlayerGameActivity
```

The activity stayed in the foreground with no crash-buffer entries. The expanded terrain was viewed and panned into the northern mountain/lake region. Evidence screenshots:

- [terrain-device.png](/Users/michael/Downloads/transport/Logs/terrain-device.png)
- [terrain-north.png](/Users/michael/Downloads/transport/Logs/terrain-north.png)

The device runtime sample reported approximately 29.9 FPS, 0.02 ms average simulation time and 70 MB total memory. This is a startup and visual verification sample, not a long-duration performance soak.

## Related evidence

- [VALIDATION.md](/Users/michael/Downloads/transport/VALIDATION.md) contains the dated implementation and device notes.
- [Tests/Program.cs](/Users/michael/Downloads/transport/Tests/Program.cs) contains the executable assertions.
- [Logs/core-checks.txt](/Users/michael/Downloads/transport/Logs/core-checks.txt) contains the headless suite output.
