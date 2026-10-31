# Rushed Crops open world

Open `Assets/_Game/Scenes/SC_Farm.unity` and press Play. The scene is already wired; no Inspector setup is required.

This map follows the proposal's inherited barn and nearby planting plots, plus the concept image's merchant clearing, stone quarry, timber camp, woodland trails, and river crossings. There is no separate corrupted field, boss, boss gate, or direction signage. The existing first-person controller, input actions, hands, shared Inventory, and inventory HUD are retained.

## Layout

The walkable valley is approximately 208 by 176 Unity units. The farm is southwest of the merchant crossroads; the quarry is northeast, and the logging area is southeast. Trails form a return loop through the farm and timber camp. Two timber bridges lead to riverside woodland walks.

| Landmark | Approximate X, Z |
| --- | --- |
| Barn | -36, -20 |
| Six planting plots | -35, -44 |
| Merchant stall | 0, 0 |
| Quarry | 39, 39 |
| Logging area | 39, -35 |
| Willow bridge | -66, 51 |
| Timber bridge | 77, -28 |

The six farming plots are empty soil beds. The pack's stock plants are hidden in these scene instances, and the added crop previews have been removed. Planting/harvesting, trading, gathering, combat, and barn-repair interactions remain separate gameplay work. The shared inventory's resource types are unchanged.

## Editing the map

- `WORLD_RushedCrops` contains named terrain, trail, river, landmark, forest, and boundary groups. Holotna objects remain prefab instances with scene overrides. Source pack assets are not edited. The reserved wayfinding group is empty.
- The original `Ground` object is inactive and retained. The new terrain and bridge deck colliders use the controller's existing Ground layer.
- Generated mesh/material assets are under `Assets/_Game/World/`. They are saved assets; the map is not generated each time the game starts.
- `Rushed Crops > World > Build concept map` is an editor authoring tool. It refuses to overwrite an existing world root. Normal adjustments should be made directly to the saved scene.
- `Rushed Crops > World > Validate saved map` checks landmark groups, collision mesh, player, Inventory/HUD, missing scripts/materials, and lighting references.
- `Rushed Crops > World > Clear farming plots` restores bare soil beds without editing the imported pack.
- `Rushed Crops > World > Apply low poly cartoon style` rebuilds the faceted tree/plant visuals and flat prop palette while retaining the layout. `Align scenery to ground` aligns visible mesh bases against the actual terrain collider. Display produce, hanging lanterns, and the chopping-block axe use their own supports.

## Environment cycle

Select `WORLD_Environment` and edit `EnvironmentCycle`:

| Field | Default / purpose |
| --- | --- |
| Day Length Seconds | 480 seconds for a full day |
| Current Hour | Starts at 09:00; editable during Play |
| Cycle Running | Freeze/unfreeze time |
| Sun / Moon | Existing directional sun and added moon light |
| Lanterns | All 10 path and building lights |
| Light intensities | Tune daylight, moonlight, and lantern brightness |
| Night readability | Tune sky, horizon, and ground ambient colours |

Sun direction and colour, moonlight, ambient light, sky exposure, and fog change through the day. Lanterns turn on before 06:00 and from 18:30. The clock display shows the day, time, and period. Scaled game time drives the cycle, so `Time.timeScale = 0` pauses it. Runtime sky changes use a temporary material instance.

This pass includes a day/night cycle. Weather, seasons, and gameplay effects of time are future work.

## CI and delivery

The fast CI workflow runs the real inventory and clock logic on every branch push and pull request:

```powershell
dotnet run --project CI/LogicTests -c Release
```

The `Windows delivery` GitHub Actions workflow is manually dispatched against the chosen branch/revision. It runs the logic gate, validates the Unity scene, builds Windows, and uploads a revision-named artifact. Configure the repository's GameCI Unity licence secrets (`UNITY_LICENSE`, and `UNITY_EMAIL` / `UNITY_PASSWORD` when required by that licence) before using that hosted workflow. No credentials are stored in the project.

The same Unity gate/build can be run locally:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe' -batchmode -quit -projectPath 'G:\Unity\RushedCrops' -executeMethod RushedCrops.Editor.WorldBuildChecks.BuildWindows -logFile 'Builds\world-build.log'
```

Close this project's interactive editor before starting a second editor against it. The output is `Builds/Windows/RushedCrops.exe`; generated build/cache files remain ignored by Git. Publishing and pushing are separate actions.

## Validation retained for this pass

- 24 logic tests passed, including the existing 17 resource tests and 7 clock tests.
- Unity 6000.5.9f1 compiled the runtime and editor scripts without C# errors.
- The saved map validator passed with all 10 lantern references assigned, six empty plots, no direction signs, and zero buried scenery roots in the visible-base support check.
- The initial pivot placement check found 599 buried scenery bases. Grounding now samples the low mesh vertices against the terrain collider and preserves mounted supports.
- Play-mode input checks repeated after the cartoon/grounding revision used the existing controller: farm-to-merchant trail 20.21 m; Willow bridge 19.70 m; Timber bridge 19.81 m. All passed.
- The live cycle crossed from Day 1 at 23:54 into Day 2; the ten lanterns enabled at night.
- Images and traversal/cycle logs are retained under `Claude-Code-Game-Studios/production/qa/evidence/holotna-world/` in the local working copy.
- The first local Windows build attempt was cancelled when the art direction was revised. No completed Windows player is claimed for this pass; the manual delivery workflow still needs a hosted Unity licence setup and a successful run.

Inspector setup: none. Gamepad-specific traversal and a full gameplay session remain manual playtest steps.
