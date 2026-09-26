# Valley Rail

A small, offline Unity train tycoon for Android. Build independent railways, connect industries and towns, and run coal, goods, and passenger services through a handcrafted low-poly valley.

## Open and play

1. Open this folder in **Unity 6000.5.8f1**. Package versions are pinned in `Packages/manifest.json` and `Packages/packages-lock.json`.
2. Open `Assets/Game/Scenes/Bootstrap.unity` and press Play.
3. Choose **New Game** for the empty map or **Explore a Working Railway** for a three-route showcase.

The planned 6.3 LTS editor was not installed on the development machine. This project uses the available 6.5 editor instead; do not open it in an older editor without making a separate copy and matching its packages.

All environment, track, station and train visuals are original procedural meshes. No paid packs or OpenTTD assets are required. TextMeshPro's bundled Liberation Sans font retains its license under `Assets/TextMesh Pro/Fonts`.

The music is a playlist of 16 public-domain classical favourites (The Blue Danube, Eine kleine Nachtmusik, Für Elise, The Entertainer, Morning Mood and more), arranged note by note in `Tools/music/pieces/` and synthesized by `python3 Tools/compose_music.py` into `Assets/Game/Resources/Audio/Music/` with a `playlist.json` (standard library only, identical on every run; `python3 -m unittest discover -s Tools/music/tests -t Tools` checks every bar). Every piece also rides on a TR-808-style beat (kick, snare, clap, hi-hats and a tuned 808 sub-bass that follows the chords), synthesized by `Tools/music/beat808.py` and `synth.py`, fitted to each piece's meter and tempo and mixed 4 dB under the music. No recordings or samples are used, so nothing needs a license. Unity streams the pieces as Vorbis. The Blue Danube opens the title screen, then the pieces shuffle with a short pause between them, at full level on the title screen and lower during play; Settings → Music turns it off.

Sound effects are original as well: `python3 Tools/compose_sfx.py` synthesizes them into `Assets/Game/Resources/Audio/Sfx/`. Delivery income plays a coin "cha-ching" (a cascade for big deliveries) at any zoom level. Every other sound is heard only when zoomed in (full at orthographic size ≤ 16, silent from 26), and sounds with a place on the map only when that place is on screen, panned left or right. These include station built, track laid, bulldoze, train bought, route started, train depart/arrive, town level-up, the rolling-train ambience, button clicks and the "can't do that" buzz. Menu screens always click. Settings → Sound turns them all off.

## First delivery

- In **Build Track**, drag or tap from `(8,15)` to `(50,15)`, crossing the southern bridge site on row 15. Confirm the green preview.
- In **Station**, tap `(10,15)` for Pinecrest Mine and `(48,15)` for Eastbank Power. Choose the location in the side panel.
- Return to **Explore**, tap the mine station and buy a **Small freight / Coal** train.
- Choose its destination: Eastbank Power Station, or tap **Auto Destination** to pick the nearest reachable station compatible with its cargo. The train starts a repeating service between the two stops. Set speed to 1x, 2x or 4x.
- Freight pays only when unloaded at an accepting destination. Track length does not increase revenue.

For goods, connect a station at `(12,32)` with one at `(16,40)` using a line from `(12,31)` south to `(12,40)`, then east to `(18,40)`. For passengers, build from `(14,46)` to `(50,46)`, with town stations at `(16,46)` and `(48,46)`.

The map uses north as positive Z; the coordinates shown after tapping help locate cells. These examples are also built by the optional demo using normal construction and purchase validation. Demo funds are $100,000; a regular new game starts at $50,000.

New maps also include **Westvale Power** at `(20,23)` and **Riverside Power** at `(42,30)`, alongside Eastbank Power. All three accept coal deliveries through nearby rail stations and have dedicated turbine halls, twin stacks and transformers. Existing saves retain their original industry locations; start a New Game to use the expanded map.

## Expanded terrain map

The map is now **128 × 128 tiles (16,384 tiles)**, four times the original 64 × 64 area. New games open with a wide overview; pinch to zoom in and drag to explore all four corners.

Grassland, woodland, dry plains, alpine meadows, rocky hills, snowy peaks and lakes give the landscape distinct regions. Mountains have actual elevation and block tracks and town growth; build around their foothills. The lake is not bridgeable. Railways remain on level land. The main river now has four bridge sites: rows **15, 46, 78 and 110**. Tap bare terrain to identify its surface.

New games add **Sunvale** `(78,28)`, **Frostford** `(105,91)` and **Lakewood** `(19,96)`, plus Northwood Forest `(12,78)`, Lakeside Sawmill `(45,88)`, Sunvale Oil Wells `(107,25)`, Desert Refinery `(87,54)`, Alpine Iron Mine `(76,108)` and Northern Steel Mill `(106,111)`.

Older saves keep their towns, industries and railways and can explore the larger landscape. Start a **New Game** for all five towns and seventeen industries. Current construction, station and train limits remain unchanged.

## More industries

New games include six additional industries inspired by [OpenTTD's industry chains](https://wiki.openttd.org/en/Manual/Industries), with original procedural buildings:

| Source | Processing plant | Delivery chain |
|---|---|---|
| Pinewood Forest `(8,53)` | Westwood Sawmill `(21,53)` | Wood → goods → towns |
| Eastbank Oil Wells `(39,7)` | Riverside Refinery `(50,23)` | Oil → goods → towns |
| Highland Iron Mine `(40,55)` | Oakridge Steel Mill `(53,55)` | Iron ore → steel → Valley Works → goods → towns |

Raw materials replenish at 18 units per minute. The three new processing plants start empty and turn each delivered unit into one output unit, up to 200 stored units. Valley Works retains its existing goods production and also converts delivered steel into goods. Freight trains support all six freight cargos; the shop shows cargos produced or accepted at the selected station. Tap an industry to see its inputs and waiting output.

For a first timber service, build along row 56 from `(7,56)` to `(22,56)`, place stations at `(8,56)` and `(21,56)`, then buy a Wood freight train and use Auto Destination. Separate railways and stations can serve each stage of a supply chain. Existing saves retain their original layout; choose **New Game** to see the new industries.

## Controls

| Mode | Mouse / touch |
|---|---|
| Explore | Drag to pan; tap to inspect |
| Track | Drag a route, or tap start then end; confirm or cancel |
| All world modes | One finger pans (except in Track); pinch to zoom; two fingers to pan; mouse wheel to zoom |
| Station | Tap the middle of three straight rail cells near a producer |
| Bulldoze | Tap to remove unprotected infrastructure; 50% refund |
| Menu / Back | Save, load, settings (sound, music, tutorial hints), title; Escape / Android Back opens or closes the menu and cancels a preview |

A five-step guide runs along the bottom bar in a new game until the first coal deliveries land. Switch it off under Settings → Tutorial hints.

Station labels show live waiting passengers (or freight), and tapping a town station shows its passenger count prominently. Stations serving the same town share its queue; the count drops when passengers board and rises as new passengers arrive.

Colorful cars drive automatically along town streets, turn at junctions and turn around at dead ends. Traffic follows the game speed and pauses with the game. Small villages have a car circling the central plaza; more cars appear as streets expand. Cars are scenery and do not cost money.

At City level (5,000 residents), top-tier building upgrades become residential skyscrapers and taller glass office towers, with varied heights, lit windows, stepped crowns and antennas. Existing top-tier buildings automatically use the new skyline.

Towns grow. Every town starts as a small village of eight houses and 420 people. Passenger and goods deliveries, boarding passengers, rail links to other places and served stations all earn it growth points; points buy new houses, shops and streets, and later upgrades to larger buildings. A growing town produces and stores more passengers, and it announces each new level in the hint bar. Tap any house to see the town's population, growth rate and next building. Towns never build on tracks or stations, and a track may cross a town street; a house in the way can be demolished in Bulldoze mode for $500 per building level, and the town leaves that cell alone for five minutes.

One connected railway may hold one train. Use separate lines for additional trains. Return to Station, unload cargo, and Clear Route before restructuring a service. A train with undelivered cargo must resume delivery or be sold. Selling is confirmed and discards cargo.

## Rail crossings and signals

In **Build Track**, draw a straight line across an existing straight railway to create a four-way diamond crossing. Trains travel straight through on either axis; use a turnout to change direction. Each crossing includes four automatic red/green signals and costs $400 total (upgrading a $100 straight costs $300). Signals alternate every six simulation seconds and hold the occupied direction green until the train clears. Trains wait at red, and signals pause with the game. Existing station platforms and active routes remain protected: park and clear routes before changing track. The one-train-per-connected-railway rule still applies.

## Train models

Buy trains from a station's **Buy a Train** panel. Scroll to compare all six models with pictures rendered from their in-game models; freight models offer separate coal and goods options. The same pictures appear in your fleet and selected train details.

| Model | Cargo | Price | Capacity | Cells/sec | Running cost/min |
|---|---|---:|---:|---:|---:|
| Small freight | Coal / goods | $8,000 | 30 | 2 | $20 |
| Fast freight | Coal / goods | $14,000 | 45 | 3 | $35 |
| Passenger | Passengers | $10,000 | 40 | 3 | $25 |
| Heavy freight | Coal / goods | $22,000 | 90 | 1.5 | $50 |
| Commuter | Passengers | $6,000 | 24 | 2.5 | $12 |
| Express passenger | Passengers | $26,000 | 64 | 4.5 | $65 |

Heavy freight uses an amber locomotive and five wagons for bulk deliveries. The turquoise commuter has one coach and low operating costs for smaller towns. The burgundy express has a streamlined locomotive and four coaches for busy passenger routes. All models use existing tracks and stations; one train per connected railway still applies. Existing saves retain their original train models.

## Save files

Manual and autosave slots are written below Unity's `Application.persistentDataPath`, with `.bak` files holding the previous version. Saves include a SHA-256 integrity check, schema/map versions and graph validation. Loaded games are paused. Backgrounding pauses and saves; there is no offline income.

## Build

Use **Valley Rail → Build Android APK (development)**. This generates `Builds/ValleyRail.apk` with Android 10 minimum, ARM64, IL2CPP, OpenGL ES 3 and landscape orientation. The package identifier is `com.valleyrail.tycoon`. The APK is a development build signed with the debug keystore, not a store release. The app icon is generated art under `Assets/Game/Art` and is applied by Configure Project: Oakridge's stadium beside its skyscrapers, lettered CITY and VALLEY RAIL. The explicit PlayMode test `ValleyRail.Tests.AppIconRenderTests.RendersTheStadiumSkylineForTheCityIcon` (run it by name) opens the grown world in `Tests/Fixtures/phone-stadium.json` and renders it from the game's own camera with the floating labels hidden (`Logs/icon-stadium.png`), and `python3 Tools/make_city_icon.py` crops and letters it and writes the adaptive layers (the city behind, the lettering in front, kept inside the circle mask), the legacy and Mac icons and a mask preview in `Logs/icon-preview.png`. The earlier icon, the red steam engine charging out of Sunvale's skyline at sunset, comes from the same class's `RendersTheCityAndEngineForTheAppIcon` and `Tools/make_icon.py`; whichever script runs last writes the icon.

**Valley Rail → Build Android Release** (or `-executeMethod ValleyRail.Editor.ProjectSetup.BuildAndroidRelease`) produces `Builds/ValleyRail-release.apk`, or an `.aab` when `VALLEY_AAB=1`, without the development flag. It signs with your own keystore, which is read only from the environment and never stored in the project:

```sh
export VALLEY_KEYSTORE=/path/to/valleyrail.keystore VALLEY_KEYSTORE_PASS=… VALLEY_KEY_ALIAS=valleyrail VALLEY_KEY_PASS=…
```

`VALLEY_VERSION` (default `1.0.0`) and `VALLEY_VERSION_CODE` set the store version fields.

Batch command (adjust the editor path):

```sh
"/Applications/Unity/Hub/Editor/6000.5.8f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -nographics -quit -projectPath "$PWD" \
  -executeMethod ValleyRail.Editor.ProjectSetup.BuildAndroid -logFile Logs/android-build.log
```

If external Android tools are needed, provide `VALLEY_ANDROID_SDK`, `VALLEY_ANDROID_NDK` and `VALLEY_JAVA_HOME` environment variables. The build uses them temporarily and restores editor preferences afterward.

**Valley Rail → Configure Project** recreates missing scene/configuration assets without overwriting the balance asset.

### macOS

**Valley Rail → Build macOS App** (or `-executeMethod ValleyRail.Editor.ProjectSetup.BuildMac`) produces `Builds/Mac/Valley Rail.app`: one universal Mono binary for Intel and Apple silicon, Metal, Retina, 60 fps, bundle id `com.valleyrail.tycoon`. It opens borderless full screen at the display's resolution; Ctrl+Cmd+F (or F11, or the green button) switches to a resizable window. The Dock icon is `Assets/Game/Art/icon-mac.png`, which the icon script (`Tools/make_city_icon.py`) writes on Apple's rounded-square grid alongside the Android icons. The editor switches back to its previous build target afterwards. Unity signs the app ad hoc, so it runs on the Mac that built it; to share it, sign it with a Developer ID and notarize it. Saves go to `~/Library/Application Support/Valley Rail/Valley Rail/`.

Unity's Input System sees a trackpad swipe only as a mouse wheel and never sees pinch or twist, so the Mac app carries a small native plugin, `Assets/Plugins/macOS/ValleyTrackpad.bundle` (Mac player only), that watches the app's own event stream and hands the camera each frame's pinch, twist and swipe (`Presentation/MacTrackpad.cs`). Its source is `Native/macOS/ValleyTrackpad.m`; after editing it, run `Tools/build_trackpad_plugin.sh`, which runs the plugin's checks and rebuilds the universal bundle.

| Desktop control | Action |
|---|---|
| Left drag / click (or trackpad tap) | Pan / inspect, as a finger does; in Track mode, drag draws track |
| Right or middle drag | Pan in every mode, including while laying track |
| Trackpad two-finger swipe | Pan in every mode (follows the system's natural-scrolling setting); over a panel it scrolls the panel |
| Trackpad pinch, mouse wheel, Cmd + two-finger swipe, + / - | Zoom |
| Trackpad two-finger twist | Turn the view a quarter turn (one turn per twist of about 30°) |
| WASD / arrow keys (Shift = faster) | Pan |
| Q / E | Turn the view a quarter turn either way (E matches the rotate button) |
| Space · 1 / 2 / 3 | Pause or resume · speed 1x / 2x / 4x |
| Escape | Close panels and menus, leave a tool, open the menu |
| Cmd+Q, or QUIT GAME on the title screen | Quit (the game autosaves first) |

### Cinematic trailer

A 72 s trailer in the style of the Red Alert 2 opening cinematic. Every shot is rendered in-engine from the downtown fixture by low perspective cameras, then graded, letterboxed to 2.39:1 and cut to an original march. The steps, in order:

1. `Tools/trailer/render.sh SurveysTheWorld` writes `Logs/trailer/survey.png` (a top-down map) and `survey.json` (towns, stations, bridges, and where every train is each second).
2. `Tools/trailer/render.sh RendersTheTrailerShots` renders every shot in `Tools/trailer/shots.json` to `Logs/trailer/<shot>/NNNN.jpg`. It uses 24 frames per simulated second and replays the same motion every run. Set `only`, `every` and `scale` in `shots.json` for quick previews.
3. `python3 Tools/trailer/march.py` synthesizes the score with `Tools/music/synth.py` and writes `cues.json` with the cut times.
4. `python3 Tools/trailer/barks.py` generates the vehicles' radio lines with edge-tts through `uvx`.
5. `python3 Tools/trailer/cards.py` draws the title slam.
6. `python3 Tools/trailer/assemble.py` cuts everything into `~/Movies/ValleyRail-Cinematic/valley-rail-cinematic-72s{,-nobarks,-720p}.mp4`. Add `--audio-only` to re-mix onto the last picture.

The render test drives the simulation itself (GameBootstrap is disabled), so it never autosaves.

## Tests

Fast simulation checks, without Unity:

```sh
dotnet run --project Tests/CoreChecks.csproj
```

Unity's Test Runner contains EditMode tests for rail rules, construction transactions, economy, save restoration and scheduling, and PlayMode tests for the full demo and build cancellation. Use the usual `-runTests -testPlatform EditMode` or `PlayMode` batch flags and omit `-quit` while running tests.

Run the complete checks with `Tools/validate.sh`; add `--android` to rebuild the APK afterward.

`Tools/soak.sh [minutes]` runs the device soak from the plan's release gate on the connected phone: it starts the demo at 4x, samples memory, battery and temperature every minute, and collects the `VR_PERF` frame-rate and simulation-timing lines that development builds log every 30 seconds.

See `VALIDATION.md` for actual results and remaining device/performance gates. `PLAN.md` describes the implementation and scope.

Intercity roads develop automatically once both towns reach Village level. The nearest eligible pair builds first, one section every ten game seconds. Roads avoid buildings and hills and cross the river at bridge corridors. Completed direct roads divert 35% of travelers from the competing passenger train route; freight and other destinations are unaffected. Town details show construction progress and open roads. Progress is saved.
