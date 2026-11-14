# Naval combat — Unity import

Open `Assets/NavalCombat/Scenes/NavalCombat.unity` (or **Naval Combat > Open Battle Scene**) and enter Play mode. Select a scenario, then a fleet or command post. Fleet command, role playing and observation use the original BattleSession permissions. No UMA setup is required.

The UI uses UI Toolkit: `Runtime/Resources/Naval/NavalScreen.uxml`, `NavalScreen.uss`, and a custom theme. The sea, procedural ships, coastlines and effects use Unity rendering. Left click selects a ship; right click on a ship opens the original contextual tactical menu; right click on the sea orders a course; middle drag pans; Shift + middle drag rotates; the wheel zooms; Space pauses. In bridge view, left drag looks around. The unit list remains usable when overlapping map labels are suppressed. The inspector scrolls to weapon controls and the combat log.

## Imported scope

- 38 standalone XML scenarios, ship/weapon/leader catalogs, original simulation, gunnery, torpedo, damage, AI, formation and victory rules.
- Fleet/role/observer entry, pause and 1/5/20/60/120× simulation speed, 2D chart, 3D and bridge views, ship records, weapon targeting, ammunition selection, fire control detail, combat log and results.
- Original procedural ship geometry, gun/torpedo effects, two optional music tracks, and simplified Chinese, traditional Chinese, Japanese and English resources. The UI uses the bundled Noto Sans font for the active language first. For missing glyphs, it checks the English, Japanese, simplified Chinese and traditional Chinese fonts in that order, skipping the active font.
- Natural Earth 1:50m and 1:10m coastlines are visual only. Ships retain original ship collision rules; navigation does not avoid land.

This is a one-time source import. There is no dependency on the source checkout at runtime, no political campaign/UMA integration, no content editor, and no in-battle save migration. Unity presents the existing combat rules through UI Toolkit, retaining the original tactical menus, parameter dialogs, dossier tabs and links, torpedo analysis and history/weapon diagrams. Windows are in-game overlays with nested back/close behavior rather than Avalonia OS windows; fonts and custom-rendered charts are not pixel-identical. Naval data and code are isolated under this folder and `StreamingAssets/NavalCombat`.

## Compiler and compatibility

Verified with Unity **6000.6.5f1** on Windows. Its bundled Roslyn compiler accepts C# 12 through assembly-local `csc.rsp` files containing `-langversion:12`. This preserves collection expressions, primary constructors and other original syntax without changing the project's default language settings. This is a locally verified compiler configuration, not a claim that all C# 12/.NET 10 APIs are officially supported by Unity.

The source targets .NET 10; Unity uses .NET Standard 2.1. Consequently, a small compatibility file supplies `IsExternalInit` and internal `MaxBy`; four imported source files replace unavailable static argument-guard APIs with equivalent explicit checks. Rendering helper copies replace `MathF.Tau` and the single-argument `Array.Clear`. Unity MonoBehaviour scripts use block namespaces for script import compatibility. The core does not depend on Unity and still advances in original one-second simulation steps; Unity physics does not resolve naval hits.

## Build and checks

- **Naval Combat > Build Windows** creates `Builds/NavalCombat/NavalCombat.exe`. Distribute the entire output folder. This build command explicitly includes only the naval scene and does not replace the project's configured build scene list. Other existing Resources assets may still be included by Unity.
- After Unity compiles, run `dotnet test Tools/NavalCombat/Tests/NavalCombat.Tests.csproj`. These imported tests reference the actual Unity-built core/application assemblies. Three source test files requiring the excluded content-editing API were not imported.
- `NavalCombat.Unity.Editor.NavalValidation.Smoke()` checks tutorial, Ulsan and the 244-ship Jutland scenario, advancing each simulation and verifying pause/finite coordinates.
- Launch the Windows executable with `--naval-smoke <absolute-output-directory>` in a visible window to load those scenarios in the actual player, check observer controls, advance simulation, render all three camera modes, write screenshots and `player-smoke.txt`, then exit. Hidden windows cannot capture a backbuffer and their update-loop rate is not a rendering benchmark. Frame rates from visible runs are local observations, not performance guarantees.

## Provenance

Source: `D:/agent3/meiji-oligarchy-data-hoarding`, commit `e420f1478a1ca5accecf337fff838589dea61648`. Original CoreUtils, NavalCombatCore, YYZLib, application runtime/scenario code, selected geometry helpers, localized strings and data were copied into this module. The source repository is not changed or synchronized by this import. GeographicLib.dll and SunCalcSharp.dll are copied unchanged from the source's `game/src/NavalCombat.Core/lib` directory, which contains no accompanying license files.

Audio attribution and original source URLs are shipped in `StreamingAssets/NavalCombat/Credits/`. The two music recordings were transcoded from OGG to MP3 for Unity import and are streamed at runtime. Natural Earth source metadata/readmes accompany the coastline files. Preserve these credits when distributing the player.

The source checkout already contained uncommitted naval changes (including torpedo evaluation and localization). This import uses that working-tree snapshot, not solely the commit tree. `import-manifest.json` records imported code hashes and corresponding source paths so the copied version is identifiable without setting up continued synchronization.

## Validation — 2026-11-12

- 109 imported xUnit tests passed against Unity-compiled assemblies.
- Windows x64 build succeeded with zero build errors (1,089,569,513 reported bytes, including existing project Resources).
- Visible Windows player integration check: PASS for 1, 10 and 244 ships, simulation advancement, observer read-only controls, screenshots and camera transitions. The short frame-rate sample measures a paused scene, not sustained combat performance.
- Manual Windows-player checks: scenario search/loading, selecting a ship captain's post, disabled commands on another friendly ship, right-click course orders, 20× advancement, pause, 3D/bridge views and battle conclusion with victory points. Editor checks also covered Chinese text and fleet command.
- Existing project shader/compiler warnings remain; there were no naval compile errors or errors in the successful visible-player check. The first hidden-window check could not capture screenshots and is not counted as a rendering pass.

## UI restoration — Editor validation, 2026-11-12

The current UI revision is validated in Editor only, as requested. The Windows results above describe the earlier import, not this revision.

- Restored ship-context commands: detach, reverse chain, follow/relative station, formation parameters, enemy attack/cancel, dossiers and torpedo analysis. Blank-sea course orders remain separate and respect command permissions.
- Dedicated follow/relative/formation dialogs validate input before applying. Dossiers contain tabs, linked records, two-column facts, armor tables, ship plans, virtualized ship logs and original history aggregates. Nested dialogs restore the parent and the previous simulation speed.
- Torpedo analysis uses the original evaluation snapshot, target/mount selection and intercept geometry, with pan, zoom and fit. Weapon sectors and penetration/history charts use UITK Painter2D. Historical text now uses the earlier Unity project's Markdig-based renderer, including tables, lists, code blocks, selectable text, inline links and images. Its styling is adapted to this interface; the earlier project's overall GUI is not imported.
- Restored dynamic control and OOB views, global battle log, ship damage/propulsion/weapon readouts, plan image, bridge controls and display settings. Restart/return actions have discard confirmation; final results and fault details have their own actions.
- Expected invalid orders are displayed as UI validation messages. Language changes no longer issue orders or reload coastlines from translated labels.
- `await NavalCombat.Unity.Editor.NavalUiValidation.Run()` in Play mode verifies menus, applied orders, invalid parameters, dossier nesting, torpedo diagrams and observer permissions. Results are written to `.artifacts/naval-ui-validation.txt`.
- Core simulation rules were not changed by this UI revision. No UMA linkage, content editors or ongoing source synchronization were added.
- Final Editor run: all 21 UI checks passed. Ulsan advanced 2 simulation seconds over 124 rendered frames; after initial loading, Jutland (244 ships) advanced 3 seconds over 183 frames. These short samples demonstrate live progression, not sustained-combat benchmarks. Jutland emits 61 original data-registration warnings for generated missing object IDs; no runtime errors were observed.

## Native UITK controls and compact layout

- Battle OOB, dynamic command and post selection use native `TreeView` with stable object-to-item IDs. Fleet modes and dossier pages use native `TabView`.
- Global battle logs, dossier ship logs and armor records use `MultiColumnListView` with virtualized rows.
- Pause, confirmation and formation dialogs size to their content within screen bounds. Dossiers and graphical analysis retain larger scrollable layouts; the dialog stack preserves parent content and prior simulation speed.
- Time controls share the header with the scenario title and clock. Camera controls sit above/inside the map. Right-click tactical commands are no longer repeated in the inspector; maneuver, damage and weapon readings use compact aligned rows. Display/audio controls remain in Settings.
- Map-label projection runs every `LateUpdate`, after the camera (explicit script execution order). It no longer waits for the 0.25-second status refresh. The current validation samples eight pans between status ticks and checks label projection error.
- MarkdownRenderer and Markdig.dll were copied from `D:/agent2/First Sino-Japanese War/Late-Qing-Naval-Combat-Demo`; Unity link/image service dependencies were adapted locally. Licenses are included under `Plugins`.
- `await NavalCombat.Unity.Editor.NavalUiValidation.RunNative()` verifies native selection/page behavior, column geometry, dialog sizing, Markdown table/link rendering and label tracking. Only Editor validation is required for this revision; no Windows build is run.

Final native-control validation: 21 existing interaction checks plus 10 native-control/layout checks passed in Editor; all eight map-pan samples had zero measured label projection error. Console had no errors or warnings in this run.
## Chart ship detail levels

Default ship display scale is 1. In 2D, ships with a projected scaled length of roughly 28 pixels or more retain the mesh; smaller ships use a fixed 22-by-8-pixel heading symbol, and ships below roughly 2 pixels use a fixed 6-pixel point. Threshold hysteresis (24/32 pixels for mesh/symbol and 1.7/2.4 for symbol/point) prevents flicker near boundaries. Pixel sizes refer to the camera render viewport, not a percentage of the window. Marker rendering and picking share the selected detail level. 3D uses the requested display scale and bridge view stays at real scale.

Editor verification checked model/symbol/point transitions at five zoom levels, constant symbol dimensions at different zooms, picking in all three levels, and return to mesh rendering in 3D/bridge. No Windows build was run.

## Ocean, wakes and runtime hierarchy

Shell-trail correction: both head and tail now sample the same visual arc; the former tail incorrectly interpolated at sea level, producing a long downward rod. Trails taper/fade and are capped at approximately 120 render pixels and 175 metres (increased fivefold at user request); a separate two-pixel bright head is also shown in projectiles-only mode. The line shader now reads vertex colors and blends alpha. Editor checks covered three flight phases at three camera depths (all tail arc errors zero), head-only mode, and a paused synthetic volley screenshot. Synthetic events were cleared afterwards. This remains a compressed visual replay of resolved shots, not a physical trajectory simulation. Results: `.artifacts/naval-shell-validation.txt`.

The ocean uses procedural multiscale ripple normals, sea-state-dependent roughness, sky-tinted Fresnel reflection, sun highlights and distance haze. This is a visual shader, not a fluid simulation or scene-reflection system. Wakes sample ship positions, broaden and fade over 90 simulation seconds, and scale their strength with speed. Scenario entry seeds a straight steady-motion wake; subsequent samples follow actual motion. Batteries have no wakes and point-level chart markers hide them. Water animation and wake aging pause with the battle.

Runtime objects live under `World (runtime)`: `Environment`, `Ships`, and `Effects`. Each vessel has a stable identity container containing its Model, chart marker and wake. Model carries vessel position, heading and display scale; its Weapons children carry local mount rotation, and barrel children carry local recoil translation. The identity container remains at the world origin so historical wake positions do not inherit vessel movement. Geometry and materials remain shared. Ship meshes and markers are no longer global render slots; only transient line effects retain that pool.

Editor verification: 157 frames advanced during the pause/run check; the paused surface clock stayed unchanged, then advanced 1.80 seconds while running; point-level chart view had no visible wakes. Near 3D screenshots were visually reviewed and the current console had no errors or warnings. No Windows build was run. Results: `.artifacts/naval-ocean-validation.txt`.


## Unity ship transforms

The runtime no longer flattens ship parts with AddInstances each frame. Ship views cache their MeshFilters and Transforms, create geometry nodes lazily on first model display, and retain them across chart detail-level changes. Layout revision changes rebuild only the affected Model subtree. Destroyed vessels switch to hull geometry and hide Weapons; scenario replacement releases owned views and shared scene meshes. The existing geometry generator and battle-state angle/recoil adapter remain independent of Unity scene ownership.

Editor validation: 120 mount comparisons across three headings and two scales matched the previous coordinate formulas to within 0.001 metres, with zero angular difference. Local barrel recoil, stationary wake ownership, model/marker switching, unchanged object identity after LOD round trips, destroyed-vessel weapon hiding and scenario cleanup were checked. A load-time UI clock null access found during reload testing was guarded while session data is loading. See `.artifacts/naval-hierarchy-validation.txt`. No Windows build was run.

## Battle HUD redesign

This revision follows the new Unity HUD requirements rather than the Avalonia panel layout. Settings, pause/speed and clock share the top bar with fleet toggles, camera controls, report and map framing controls. Scenario title, the participation/status strip and duplicate restart are absent from battle view; language is configured in Settings. The two fleet toggles are mutually exclusive and initially off.

The right HUD and compact six-entry text log overlay the camera with translucent backgrounds. Vessel information, damage and armament fold independently while retaining the ship name, damage percentage bar and undamaged weapon counts. Doctrine and weapon command widgets are no longer displayed there. Log details use a large dialog with a virtualized plain list of all battle logs, not the 80-entry HUD query limit.

Victory report uses the existing VictoryStatus model also used by the earlier Unity project: faction scores/victory levels and ship-type losses in Summary, plus per-vessel initial/current damage, yellow/red/empty damage segments and firing/damage statistics in Detail. The main battle camera still renders directly to its viewport.

The square vessel preview offers 2D, 3D and Hidden. The plan uses heading minus 90 degrees and diagonal fitting, matching Avalonia ShipDetailControl's orientation convention. The live 3D view uses a separate 512-square RenderTexture camera looking at the actual battlefield. There are no duplicate preview vessels, water planes or wakes. Models near this camera remain active even when the main chart substitutes symbols/dots: layer 30 hides those models only from the main camera; layer 29 hides chart symbols/selection/routes from the close-up. Nearby ships, sea, wakes and world effects are shared. Hidden/2D modes disable the close-up camera; destruction releases its render target.

Editor validation: 23 layout/interaction checks passed (fleet exclusivity, camera resizing, collapsed persistent headers, report contents, log dialog, preview geometry/modes and settings placement). Settings language Apply was exercised and restored; a live battle run moved vessels, updated the preview and exposed all 20 generated log entries in details. That simulation emitted one existing core auto-ID warning for a DamageControlModifier. The final fresh Editor run had no errors or warnings. Screenshots and checks are under `.artifacts/hud-*.png` and `.artifacts/naval-hud-validation.txt`. No Windows build was run.

## Full-screen main camera

The main naval camera now keeps its default full-screen viewport. UI geometry no longer writes Camera.rect: the viewport geometry callback and UpdateViewport calculation were removed. Fleet panes, the bridge toolbar, logs and other HUD elements are overlays/input regions only. The main and bridge toolbar backgrounds are translucent. Screen transitions preserve the preview layer exclusion so detailed world models retained for the close-up do not overlap their chart symbols in the main camera.

Editor checks covered Chart, 3D and Bridge with the fleet sidebar both open and closed: viewport and pixel rectangle stayed full-screen, and panel toggles did not alter the camera pose. Both toolbar backgrounds were translucent and the preview layer remained isolated. No console errors or warnings were observed; no Windows build was run. Results: `.artifacts/fullscreen-camera-validation.txt`.

### Shared battlefield gunnery effects

Resolved shots now produce a short muzzle flash and lingering smoke, followed by water spray/mist for misses when the displayed shell reaches its endpoint. Battery muzzle origins use the firing mount/barrel geometry; weapons without a matching model muzzle fall back to the reported shooter position at deck height. The rules expose hit/miss and target coordinates, not a physical miss location, so miss dispersion is presentation-only and shared by the shell endpoint and splash. Hits do not spawn a water column at the hull. These are shared world-space Unity particle systems, visible to both cameras, with gravity-driven spray, pause/resume, bounded particle counts and per-frame burst budgets; no per-shot GameObjects are instantiated. Existing visual flight timing remains compressed at simulation speed. Scenario reload clears effects.

Editor verification passed ten checks covering muzzle resolution, arrival timing at 120x, pause/resume, single emission, camera visibility, hit/miss distinction, expiry and reload cleanup. Visual captures: `.artifacts/muzzle-flash.png`, `.artifacts/shell-splash.png`; checks: `.artifacts/gunnery-effects-validation.txt`. No Windows build was run.

### Scenario sun and day/night rendering

NavalSunlight reuses ScenarioState.GetSunPosition (the same SunCalcSharp ephemeris used by combat day/night rules). It consumes scenario UTC time directly; the UI timezone is display-only. At each main/preview camera render, the camera's horizontal world position is converted back to latitude/longitude and solar azimuth/elevation are converted to Unity east/up/north. Camera-local Skybox components and ship/ocean materials share these parameters, without modifying global RenderSettings or other scene cameras. The procedural sky includes a solar disk, horizon gradient and twilight glow; ocean reflection samples the same sky function. Direct sunlight fades out at sunset; ambient light transitions through twilight to a dim night baseline. Wake/smoke/splash brightness follows the environment, while muzzle flashes remain emissive. Atmospheric colors are an approximation, not a weather or physically based scattering simulation.

Editor validation: 11 checks passed for both camera paths, shared material direction, noon/night illumination, paused time, and UTC interpretation. The Ulsan 05:20 local opening is civil twilight (sun about -5.4 degrees altitude). Captured noon, night and sun-facing views under `.artifacts/solar-*.png`; checks in `.artifacts/solar-lighting-validation.txt`. No Windows build was run.

## Manual scenario catalog and platform loading

`Runtime/Resources/Naval/ScenarioCatalog.json` is the manually maintained catalog. Each entry supplies `file`, `historical`, `startUtc` and `recommendation` (`recommended`, `neutral`, or `discouraged`). Keep the timestamp synchronized with the scenario when editing it. Listed scenarios are ordered historical-first, then by UTC date; filename breaks ties. Neither the Editor nor a build regenerates this file.

Recommendation provenance: the original Unity project's `Assets/DynamicStringTableCollection/Dynamic Table Shared Data.asset` maps filenames to IDs; `Dynamic Table_en.asset` supplies the star/triangle suffixes. The four retained recommended scenarios are Pundo, Yalu River, Ulsan, and Yalu River (Beiyang with foreign ships). All seven tutorials and both Jutland scenarios are discouraged in addition to the four original discouraged scenarios. Current totals: 38 listed, 4 recommended, 21 neutral, 13 discouraged. The archive exposes three recommendation filters and defaults to excluding discouraged scenarios.

Editor/Standalone selection loads the manual catalog and additionally scans the top-level naval data directory when opening/returning to the archive. Unlisted files are prepended and visibly marked; ordinary filters still apply. Android/Web use only the catalog. `NavalCatalogBuildCheck` runs before builds and warns for unlisted scenario files (and missing listed files), without rewriting the catalog.

`UnityScenarioSource` reads scenario previews, scenario XML and referenced Leaders/ShipClasses/NamedShips XML through UnityWebRequest. It preserves Android jar and Web URL roots, escapes relative path segments, rejects escaping references, supports cancellation/Abort and request disposal, and caches successful shared XML until the next catalog refresh. Preview selection cancels the previous preview and rejects obsolete results. Requests yield frames on the Unity main thread; no Task.Run was added. FileScenarioDataSource remains available for non-Unity file-based consumers. Coastline binary loading/threading, AOT/serialization/DLL handling and responsive screen layout are outside this change.

## Touch map controls

Map input uses UITK pointer coordinates rather than Mouse.current. Tap selects; one-finger drag pans the chart/3D view or looks around in bridge view; pinch zooms; two-finger drag orbits in 3D. Holding for 0.5 seconds invokes the existing right-click action: a ship opens its tactical menu, open sea issues an independent heading toward that point for the selected commandable ship. Moving over 12 screen pixels cancels the hold; adding a second finger, UI interaction, cancellation, focus loss or a modal prevents pending commands. Once multi-touch ends, remaining fingers cannot issue a single-finger command until lifted. Touch menus have larger hit areas and the map hint changes to touch controls. Existing mouse gestures remain available.

Editor verification: 54 catalog/loading checks (including all 38 previews, complete XML bundle/battle loading, filters, cancellation, Android/Web URL construction, and a temporary unlisted scenario/build warning) plus 16 injected-pointer checks (selection without an enabled mouse, drag/pinch/orbit, long-press menu/course, single firing and UI/modal cancellation). Temporary scenario files were removed. Results: `.artifacts/manual-catalog-validation.txt`, `.artifacts/touch-input-validation.txt`; screenshot `.artifacts/manual-catalog.png`. No Windows/Android/Web build or physical touch-device test was run.

## Naval combat soundscape

`NavalAudio` consumes battle-session gunfire/torpedo notifications. `NavalSoundscape` ports the Avalonia activity-driven cannon scheduler to seconds on Unity's DSP clock: capped energy from shot batches, three-second decay/expiry, alternating randomized stereo beds, four near/far variants, no consecutive variant repeat per side, and at least 0.2 seconds between onsets. Presentation randomness uses its own System.Random, never the simulation or UnityEngine.Random state. Sounds run in real time at every simulation speed; they are not synchronized one-for-one with visual shell arrivals.

`Resources/Naval/Audio/CombatAudio.asset` references all nine effects, the dedicated `NavalCombat.mixer/CombatEffects` group and playback parameters. A fixed pool of ten 2D AudioSources uses PlayScheduled with a short DSP lead; excess sounds are dropped, without per-shot GameObject allocation. Torpedo batches are grouped by firing ship with a 0.6-second per-ship cooldown. Only the main camera determines screen visibility; off-screen requests use 0.39810717 gain and the close-up preview emits no additional sounds. Pausing, modal dialogs, leaving battle, reattaching a session, disabling effects, zero volume or an audio-device change clear active and scheduled voices. Music retains its separate existing source and behavior.

All eight cannon variants retain the Avalonia timing, filtering and stereo channels, with an additional +6 dB constant sample gain applied from the source WAVs (not cumulatively). They and the torpedo clip are preloaded PCM/decompressed clips; credits are in `StreamingAssets/NavalCombat/Credits/sound-effects.json`. Shared gain based on measured clip peaks reserves 0.85 input headroom before the mixer compressor reacts, retaining relative voice gains. The mixer's compressor handles the final effects bus; this is not sample-identical to Avalonia's custom soft limiter. No SoundFlow/MiniAudio dependency is introduced. Explosion, splash, wave and bell sounds remain outside this migration.

The dedicated CombatEffects compressor is configured at -9 dB threshold, 10 ms attack, 100 ms release and 0 dB makeup. Editor verification: 157 deterministic scheduler assertions, 29 playback/lifecycle checks, and a ten-voice full-volume stress capture passed. The stress capture's listener peak was 0.761, below full scale; actual nonzero source and listener samples were verified. This is signal/behavior validation, not a subjective listening assessment. No player build was run. Validation scripts and outputs are in `.artifacts/validate-naval-audio-*.cs` and `.artifacts/naval-audio-*-results.txt`.
