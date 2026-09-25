# Validation record

## Completed automated checks

- .NET 8 headless simulation checks: **400 assertions passed**. Includes coal, goods and passenger routes, profitability, component ownership, atomic bridge removal, save replay, pause/speed scheduling, insolvency recovery, route editing, and return-to-station while loading.
- Unity 6000.5.8f1 EditMode: **26 tests passed**. Includes every turnout orientation, disconnected ports, unaffordable/non-adjacent construction, save schema rejection, preservation of good backups, corruption recovery, shared production, path-cache invalidation and zero-allocation simulation ticks.
- Unity PlayMode: **2 integration tests passed**. The three-service demo delivers all cargo types; locomotive sampling remains valid; cancelling a displayed construction preview leaves tracks and money unchanged.
- A **30-minute simulated-time soak** with an active coal service passes. The headless timing was 5.76 ms for 36,000 ticks on the development host, with 40 bytes from the benchmark stopwatch. The corresponding Unity test measured zero allocation inside its tick loop. This is a simulation microbenchmark, not mobile FPS evidence.
- A separate headless fixture validates **1,500 tracks and 12 parked trains** and simulates 30 minutes without state-validation errors. This is not a 12-active-train device stress test.
- Rendered overview and construction captures were inspected. The UI has larger touch controls, scrollable detail/menu panels, safe-area anchors, and context-specific build confirmation/cancellation.

Machine-readable results are under `docs/validation/`. Render captures are under `docs/screenshots/`.

## Build and runtime checks

An ARM64 IL2CPP development APK builds with Android API 29 minimum and OpenGL ES 3. The first APK exposed a stripped runtime shader on the emulator; the project now includes a serialized `WorldMaterial` resource to retain that shader. The corrected APK builds successfully and renders on the physical device described below.

The installed editor is 6000.5.8f1; Unity 6.3 LTS compatibility has not been tested. Build tools used: external Android SDK, NDK 27.2.12479018 and JDK 17. These paths are supplied through environment variables, not committed user-specific paths.

## Physical Android smoke test — 2026-09-23

- Installed `Builds/ValleyRail.apk` successfully on **Samsung SM-S731B**, **Android 16 / API 36**, physical display **2340 × 1080** in landscape.
- Launched the IL2CPP app; verified rendered terrain, buildings, river, HUD, safe-area layout, build/station mode selection, pause and menu interaction from device screenshots and ADB input.
- Sample memory: **379,332 KB PSS (~370 MiB)** and **504,029 KB RSS (~492 MiB)** in an empty new game. This is one sample, not a maximum-load or thermal result.
- No fatal crash or gameplay C# exception observed in the captured app-process log. Unity logs a startup `ClassNotFoundException` for optional `AssetPackManager`; startup and gameplay rendering continue. Investigate before release packaging.
- Concurrent manual phone interaction changed camera/tool state during automated taps. Consequently, construction-to-delivery, save recovery, multitouch and sustained FPS are **not claimed as physically verified** in this run. Automated domain and Unity test results above still apply.
- Device screenshot: `docs/screenshots/samsung-device.png`. App remains installed for hands-on testing.
- APK SHA-256: `36cca64df295f55593aa1ce5ad410b53a334cda0183bcd2e501b0d1e50595c02`.

## Gap audit and fixes — 2026-09-24

The installed development build was driven on the same Samsung SM-S731B over adb (new game, track, stations, train, route, 4x, menu, save, load, bulldoze, demo) and the code was audited against `PLAN.md`. The state after the fixes is recorded here.

Fixed and re-verified on the phone with the rebuilt APK:

- Menu → Resume now restores the previous speed instead of leaving the game paused.
- Android Back opens the menu on the first press and closes it on the next; it also leaves the settings and recovery screens and cancels a track preview.
- The in-game guide is now a five-step tutorial driven by world state (`Tutorial` in Core, persisted as `tutorialStep`), with the working coordinates (8,15)→(50,15) and stations at (10,15)/(48,15). It fast-forwards through steps already done and ends after the first deliveries.
- The HUD "per min" expenses count only running costs; construction and purchases no longer read as a per-minute loss.
- The New Station panel closes after a placement, Save Game shows "Game saved." in the menu, and the title screen hides the previous game's HUD.
- Trains carry a fleet number (#1, #2…) instead of the shared id counter; older saves are renumbered on load.
- Menu layout no longer clips "Return to title"; HUD subtitle no longer truncates; map labels are spaced apart.
- Both route legs are protected from bulldozing and platform conversion (only the current leg was checked before).
- The three train models render differently (small red freight with two wagons, blue streamlined fast freight, green passenger locomotive with windowed coaches).
- Shop buttons show capacity, speed and running cost; station panels name the cargo, unit and accepted cargo; train panels show the route.
- One finger pans the camera in every mode except Track; loading a missing save explains itself instead of showing the corruption screen.
- A settings screen (sound, tutorial hints, version) replaced the lone sound toggle.
- App icon art (`Assets/Game/Art`) is applied to legacy, round and adaptive slots; **Build Android Release** produces a non-development APK or AAB signed with a keystore taken from environment variables.

Automated checks after the changes: headless **421 assertions passed**, EditMode **30 passed**, PlayMode **2 passed**.

Two startup log lines that looked like problems are benign: the `AssetPackManager` ClassNotFoundException is Unity probing for Play Asset Delivery, and the `liblibswappywrapper.so` miss is immediately followed by a successful `libswappywrapper.so` load ("SwappyWrapperInit() succeeded"), so frame pacing is active.

Development builds now log a `VR_PERF` line every 30 s (fps, simulation ms per frame, ticks, managed heap, GC count in the window). First sample on the phone at 4x with one train: 29.9 FPS, 0.01 ms average and 0.45 ms maximum simulation time per frame. `Tools/soak.sh [minutes]` runs the demo at 4x and records memory, battery, temperature and those lines.

## Device soak — 2026-09-24

`Tools/soak.sh 30` on the Samsung SM-S731B, USB-powered (so battery drain is not measurable) with screen mirroring active on the phone.

| Run | Content | Samples | PSS | Temperature | Frame rate | Simulation per frame | Gen-0 GC |
|---|---|---|---|---|---|---|---|
| 1 | manual save, one coal train, 4x throughout | 30 × 1 min | 382 → 393 MB, flat after minute 12 | 37.1–37.6 °C | 30.0 avg, 29.9 min | 0.02 ms avg, 1.42 ms max | 0–2 per 30 s after the first window |
| 2 | three-line demo, 4x in 43 of 60 windows (the phone was handled at minute 9 and switched to 1x for a while) | 30 × 1 min | 383 → 400 MB | 36.3–37.3 °C | 30.0 avg, 29.9 min | 0.02 ms avg, one 4.65 ms outlier in 60 windows | ≤9 per 30 s after the first window |
| 3 | three-line demo, 4x in all 60 windows, untouched | 30 × 1 min | 378 → 385 MB, flat | 36.7–37.2 °C | 30.0 avg, 29.9 min | 0.02 ms avg, 1.25 ms max | 1–4 per 30 s after the first window (33 in the first) |
| 4 | three-line demo over Wi-Fi adb, on-battery reporting for `batterystats`; 4x for 9 min, then 1x (the phone is shared with another automation session) | 30 × 1 min | 376 → 389 MB, flat | 36.5 °C throughout | 30.0 avg, 29.9 min | 0.02 ms avg, one 4.17 ms outlier | ≤5 per 30 s after the first window |

**Battery.** The cable could not be removed, so run 4 used Android's own model instead: battery statistics were reset, the phone was told it was on battery (`dumpsys battery unplug`, restored afterward), and the demo ran 31 minutes in the foreground. `dumpsys batterystats` attributes **17.2 mAh** to the app (UID u0a321, 17.1 mAh foreground) out of a 4,700 mAh capacity, about 0.37% per half hour, with 17.7 minutes of CPU time over the 31 minutes. The display is accounted separately and will dominate a real unplugged run; the actual discharge was 0 mAh because the phone kept charging. Raw dump: the `batterystats` text in the session scratchpad; samples in `Logs/soak-20260924-1334*`.

Against the plan's targets: 30 FPS held throughout; simulation time stays far below 4 ms per frame at 4x (the single 4.65 ms frame is a scheduler stall, since the clock caps catch-up at 32 ticks per frame and a tick costs about 0.02 ms); PSS stays under 400 MB; the simulation itself allocates nothing, while the HUD refresh produces a small steady trickle of garbage (a few gen-0 collections per minute). Raw data: `Logs/soak-20260924-0922*`, `Logs/soak-20260924-0953*` and `Logs/soak-20260924-1047*`.

## Interactive smoke test over adb — 2026-09-24 (build of 09:20)

`Builds/ValleyRail.apk` (SHA-256 `e661feaa…5bc5a`) reinstalled on the SM-S731B and driven with hold-taps, explicit down/move/up drags and screenshot checks at each step. All passed: New Game; Explore cell inspection; Build Track (8,15) → (50,15) previewed as 43 cells / $5,500 and built (money $44,500, bridge rendered, tutorial 2/5); stations at (10,15) and (48,15) ($40,500, tutorial 3/5); Small freight · Coal bought at the mine, routed to Eastbank Power, 4x; deliveries paid (+$2,280 / −$20 per min after ~40 s real time) and the tutorial ended; Save Game showed "Game saved." and Resume restored 4x; Android Back opened then closed the menu; Return to title → Load Manual Save restored the game paused at the saved money. No app exceptions in logcat; 387 MB PSS during the coal service. Screenshot: `docs/screenshots/samsung-coal-service.png`.

One observation: the very first track drag after New Game shifted the camera by about six cells once (the line started at (14,22) instead of (8,15)); later drags did not reproduce it. It may be an input-injection artefact or a genuine first-drag issue and deserves a check with a finger.

## City growth — 2026-09-24

The city-growth part of the Dynamic City & World Growth plan (milestones 12 and 13, plus the town rendering from milestone 14) was implemented and tested the same day. The world stays 64 × 64; chunk expansion is not built yet.

Automated (final code): headless **531 assertions passed** (the 30-minute allocation soak still measures 40 bytes with growth running), EditMode **40 passed** (four new tests: service outgrows isolation, growth replays exactly from a save, legacy saves migrate, demolition frees a cell), PlayMode **2 passed** with the demo render capture showing both towns. Results: `docs/validation/`.

Physical device (Samsung SM-S731B, `Builds/ValleyRail.apk` SHA-256 `44b8b4a35d0a35de1cbd3fc39b54af4f6dba32136bbfb0f66f2f8b3e24e0c1e5`):

- The map-version-1 autosave from the morning soak (three-line demo) loaded through the migration with no errors and both towns appeared at their day-0 size (8 houses, 420 people, "SMALL VILLAGE · POP 420").
- At 4x with the demo's passenger line: "Willowbrook has become a Village (500 people)." appeared within the first real minute; after three real minutes Willowbrook was a Small Town of 1,540 with streets and houses clustered around its station, Oakridge a Village of ~1,180. After a reload from autosave the grown town persisted (1,980 people).
- City panel on tapping a house: level, population, growth pace and rate, next-building estimate, links, recent passengers and waiting passengers, in five lines.
- Performance while towns grew: 30.0 FPS, simulation 0.02–0.03 ms per frame average, worst window 2.04 ms, managed heap 6–7.5 MB, 0–4 gen-0 collections per 30 s, no app exceptions.
- Found and fixed during the test: the world view only refreshed after player actions, so towns grew in the simulation without being redrawn. The bootstrap now refreshes when `cityRevision` changes. Screenshot: `docs/screenshots/samsung-city-growth.png`.
- Ten further real minutes at 4x (40 game minutes): Willowbrook reached Town level with 3,330 people, +193 points/min, 640 passengers in the last 4 game minutes and 616 passenger storage; company income rose to about $12,200 per minute from $7,700 with the same three trains. Over the last 20 half-minute windows: 30.0 FPS minimum, 0.02 ms average and 1.61 ms worst simulation time per frame, 58 gen-0 collections in total, managed heap at most 7.8 MB, 393 MB PSS, no app exceptions.



- A soak at the supported world limits (12 trains, 1,500 tracks) needs a way to load that fixture on the device; the headless fixture covers the simulation side only.
- A physically unplugged run of `Tools/soak.sh` would replace the modelled 17.2 mAh figure with measured drain including the display; run it over Wi-Fi adb (`adb tcpip 5555`, `ANDROID_SERIAL=ip:5555`) with the cable out.
- Physical multitouch (simultaneous pinch and pan, gesture cancellation), varied DPI, display cutouts, Android navigation modes and tablet layout remain untested; adb cannot inject multitouch reliably.
- A new-player usability session.
- Store submission: **Build Android Release** signs from environment variables and can emit an AAB, but it still needs your keystore, a review of the generated icon art, and store listing assets.

The current art is an original procedural low-poly prototype. It implements the reference's isometric countryside direction but does not reproduce its detailed buildings, landscaping or promotional presentation. World assets are constructed in code rather than a production art/prefab library.

## Industry expansion — 2026-09-24

- Added six industries, four freight cargos, delivery-driven sawmill/refinery/steel-mill output, and map version 4; older maps retain their layouts.
- `dotnet run --project Tests/CoreChecks.csproj`: **854 assertions passed**, including a working timber railway, processing chains, capacity limits, cargo compatibility and saves.
- Unity Android development build succeeded (`Logs/android-industries.log`); installed `Builds/ValleyRail.apk` on device R5GYB2NHR5D with `adb install -r`.
- Launched and verified `com.valleyrail.tycoon/com.unity3d.player.UnityPlayerGameActivity` as the foreground activity; new industries visibly rendered on the device. Screenshot: `Logs/industries-device.png`.
- No immediate crash. Unity logged a nonfatal missing optional Play Asset Delivery class during startup; gameplay remained active.

## Four-times-area terrain map — 2026-09-24

- Expanded to 128 × 128 tiles with unique cell keys, larger city occupancy grids, full-map camera limits, wider zoom and terrain-aware picking.
- Added woodland, dry plains, alpine meadows, shoreline, lake water and elevated rocky/snowy mountains. Raised tiles reject rail construction and city growth. Added two river crossings, three towns and six industries.
- Core checks passed: **17,454 assertions**, including full-map cell-key uniqueness, construction past x=63, northern bridges, hill/lake rejection, mountain detours, station access for every new location, and old/new save restoration.
- Initial build encountered an unrelated temporarily missing ThemeMusic class. An isolated build was stopped once the shared dependency was ready; its temporary project was removed.
- Successful combined Unity APK build: `Logs/android-roads.log`. The combined APK includes the terrain and concurrent road changes and was installed on R5GYB2NHR5D at 16:27.
- Independently verified `com.valleyrail.tycoon/com.unity3d.player.UnityPlayerGameActivity` in foreground after installation, with no crash-buffer entries. Viewed expanded terrain on the device and panned into the northern mountain/lake region (`Logs/terrain-north.png`). Runtime sample: 29.9 fps, average simulation 0.02 ms, total memory 70 MB; this is a startup check, not a long performance soak.
