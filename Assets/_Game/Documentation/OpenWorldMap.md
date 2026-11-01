# Rushed Crops open world

Open `Assets/_Game/Scenes/SC_Farm.unity` and press Play. The scene is already wired; no Inspector setup is required.

This map follows the proposal's inherited barn and nearby planting plots, plus the concept image's merchant clearing, stone quarry, timber camp, woodland trails, and river crossings. There is no separate corrupted field, boss, boss gate, or direction signage. The existing first-person controller, input actions, hands, shared Inventory, and inventory HUD are retained.

## Layout

The current walkable valley is approximately 160 by 140 Unity units, with distant hills beyond it. The farm is southwest of the merchant crossroads, the quarry is north across the river, and the logging camp is east. Three terrain-painted routes connect the farm, market, quarry bridge, and logging camp. There is one river bridge.

| Landmark | Approximate X, Z |
| --- | --- |
| Barn | -41, -2 |
| Six planting plots | -25, -31 |
| Merchant stall | 4, -6 |
| Quarry | 10, 49 |
| Logging camp | 42, -22 |
| River bridge | 6, 24 |

The six farming plots start as flat soil beds; the pack's stock plants and ridges stay hidden. Each bed is a 5 x 5 grid of 1 m squares, farmed one square at a time. Trading, gathering, combat, and barn-repair interactions remain separate gameplay work. The shared inventory's resource types are unchanged.

## Farming

The player starts with empty hands. The hoe and the watering can wait on the cut tree stump just west of the garden gate.

| Step | How |
| --- | --- |
| Pick up a tool | Look at it on the stump, press E. A tool picked up with empty hands goes straight into the hand. |
| Switch tools | F (gamepad Y / Triangle) cycles empty hands, hoe, watering can. |
| Till a square | Hold the hoe, aim at the square (it gets an outline), left click. |
| Plant | Aim at a tilled square, press E. One seed per square; Q switches carrot / tomato seeds. |
| Water | Hold the watering can, left click a planted square. A dry crop doesn't grow. Wet soil is darker. |
| Fill the can | Hold the can, aim at the river, left click. One fill waters 6 squares; the can starts empty. |
| Harvest | Press E on a ripe square. The soil stays tilled for the next seed. |

| Crop | Grows in | Waterings | Harvest per square |
| --- | --- | --- | --- |
| Carrot | 60 s | 1 | 2 carrots |
| Tomato | 90 s | 2 (it dries out half way) | 3 tomatoes |

| Part | Where |
| --- | --- |
| Crop settings (seed, harvest, grow time, waterings, stage models) | `Assets/_Game/Data/DA_Carrot`, `DA_Tomato` |
| Carrot plants, hoe, stump | The Holotna pack (`CarrotPlant01-03`, `Hoe01`, `OakStump01`) |
| Tomato plants, watering can, soil mound, square outline | `Assets/_Game/Models/Crops`, `Models/Tools`, `Models/Farm`, made in Blender because the pack has none |
| Bed and square behaviour | `FarmPlot` on each `Planting plot` (shared settings), `FarmTile` on each `Tiles/Tile NN` |
| Tools | `PlayerTools` on the Player; `ToolPickup` on the stump tools; `WaterSource` on `River water` |

`FarmPlot.GrowthSpeed` scales growing for later upgrades. The river's collider is on the Water layer and touches nothing, so the player still wades through; water can be aimed at from up to 6 m because it lies below the bank.

The farm parts have editor tools, and `Rebuild map` runs all three:

- `Rushed Crops > Farming > Set up farm plots` rebuilds every bed's squares and settings.
- `Rushed Crops > Farming > Place tool stump` puts the stump, hoe and watering can back by the gate.
- `Rushed Crops > Farming > Set up river water` gives the river its collider and `WaterSource`.

## Editing the map

- `WORLD_RushedCrops` contains named terrain, river, farm, market, quarry, logging, forest, and boundary groups. Paths, clearings, and tilled soil are painted into the terrain mesh. Holotna objects remain prefab instances with scene overrides. Source pack assets are not edited. There are no direction signs.
- The original `Ground` object is inactive and retained. The new terrain and bridge deck colliders use the controller's existing Ground layer.
- Generated meshes are under `Assets/_Game/Models/World/` and `Models/Cartoon/`, and their materials under `Assets/_Game/Materials/World/` and `Materials/Cartoon/`. They are saved assets; the map is not generated each time the game starts.
- `Rushed Crops > World > Rebuild map` recreates the world from the current layout settings after a confirmation dialog. Rebuilding replaces hand edits to the map. Normal adjustments can be made directly to the saved scene.
- `Rushed Crops > World > Validate saved map` checks landmark groups, collision mesh, player, Inventory/HUD, farm plots and squares, the tool stump, the river water, missing scripts/materials, and lighting references.
- `Rushed Crops > World > Clear farming plots` restores bare soil beds without editing the imported pack.
- `Rushed Crops > World > Apply low poly cartoon style` rebuilds the faceted tree/plant visuals and flat prop palette while retaining the layout. `Align scenery to ground` checks each visible footprint against the terrain and distant hills: loose props rest on flat pads, while trees, rocks, and vegetation have embedded bases. Display produce, hanging lanterns, and the chopping-block axe use their own supports.

## Environment cycle

Select `WORLD_Environment` and edit `EnvironmentCycle`:

| Field | Default / purpose |
| --- | --- |
| Day Length Seconds | 480 seconds for a full day |
| Current Hour | Starts at 09:00; editable during Play |
| Cycle Running | Freeze/unfreeze time |
| Sun / Moon | Existing directional sun and added moon light |
| Lanterns | All 12 path and building lights in the current scene |
| Light intensities | Tune daylight, moonlight, and lantern brightness |
| Night readability | Tune sky, horizon, and ground ambient colours |

Sun direction and colour, moonlight, ambient light, sky exposure, and fog change through the day. Lanterns turn on before 06:00 and from 18:30. The clock display shows the day, time, and period. Scaled game time drives the cycle, so `Time.timeScale = 0` pauses it. Runtime sky changes use a temporary material instance.

This pass includes a day/night cycle. Weather, seasons, and gameplay effects of time are future work.

## CI and delivery

The local logic test runner checks the real inventory and clock logic:

```powershell
dotnet run --project CI/LogicTests -c Release
```

Under the requested repository layout, `CI/` and `.github/` are local, ignored folders. A fresh clone does not include the local logic runner or the GitHub Actions workflows, and new pushes no longer run those workflows. The Unity scene validator and Windows build method remain tracked under `Assets/`.

The same Unity gate/build can be run locally:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe' -batchmode -quit -projectPath 'G:\Unity\RushedCrops' -executeMethod RushedCrops.Editor.WorldBuildChecks.BuildWindows -logFile 'Builds\world-build.log'
```

Close this project's interactive editor before starting a second editor against it. The output is `Builds/Windows/RushedCrops.exe`; generated build/cache files remain ignored by Git. Publishing and pushing are separate actions.

## Validation retained for this pass

- 24 logic tests passed, including the existing 17 resource tests and 7 clock tests.
- Unity 6000.5.9f1 compiled the runtime and editor scripts without C# errors.
- The current saved map validator passed with all 12 lantern references assigned, six empty plots, one river bridge, and no misplaced scenery in the footprint support check.
- The initial pivot placement check found 599 buried scenery bases. Grounding now samples the low mesh vertices against the terrain collider and preserves mounted supports.
- Historical Play-mode input checks on the earlier two-bridge layout passed: farm-to-merchant trail 20.21 m; Willow bridge 19.70 m; Timber bridge 19.81 m. These do not certify traversal of the newer one-bridge layout. Repeat traversal in Play for the current map.
- The live cycle crossed from Day 1 at 23:54 into Day 2; the ten lanterns enabled at night.
- Images and traversal/cycle logs are retained under `Claude-Code-Game-Studios/production/qa/evidence/holotna-world/` in the local working copy.
- The first local Windows build attempt was cancelled when the art direction was revised. No completed Windows player is claimed for this pass. The tracked Unity build method can still be run locally; the hosted workflow is now local-only and ignored.

Inspector setup: none. Current-layout traversal, gamepad-specific traversal, and a full gameplay session remain manual playtest steps.
