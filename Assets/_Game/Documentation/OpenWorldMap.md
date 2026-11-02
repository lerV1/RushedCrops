# Rushed Crops open world

Open `Assets/_Game/Scenes/SC_Farm.unity` and press Play. The scene is already wired; no Inspector setup is required.

This map follows the proposal's inherited barn and nearby planting plots, plus the concept image's merchant clearing, stone quarry, timber camp, woodland trails, and river crossings. There is no separate corrupted field, boss, boss gate, or direction signage. The existing first-person controller, input actions, hands, shared Inventory, and inventory HUD are retained.

## Layout

The current walkable valley is approximately 160 by 140 Unity units, with distant hills beyond it. The farm is southwest of the merchant crossroads, the quarry is north across the river, and the logging camp is east. Three terrain-painted routes connect the farm, market, quarry bridge, and logging camp. There is one river bridge.

| Landmark | Approximate X, Z |
| --- | --- |
| Barn | -41, -2 |
| Crop field | -25, -31 |
| Merchant stall | 4, -6 |
| Quarry | 10, 49 |
| Logging camp | 42, -22 |
| River bridge | 6, 24 |

The crop field is the brown soil inside the garden fence, about 20 by 16 m. It starts bare, and any 1 m square of it can be tilled, in whatever shape the player likes. Crops are sold to the merchant at the market stall (see Market). Gathering wood and stone, enemies, and barn repairs are still to come.

## Farming

The player starts with empty hands. The hoe and the watering can wait on the cut tree stump just west of the garden gate. Seeds come from the merchant, or free from the seed crates along the fence east of the gate.

| Step | How |
| --- | --- |
| Pick up a tool | Look at it on the stump, press E. A tool picked up with empty hands goes straight into the hand. |
| Switch tools | Keys 1-5 pick a hotbar slot. A tool in that slot goes into the hand. |
| Till a square | Hold the hoe and aim anywhere on the field's soil. An outline shows the square that will be tilled; left click. Squares line up on a 1 m grid, and something standing on the spot stops the hoe. |
| Plant | Pick the seeds' hotbar slot, aim at a tilled square, press E. One seed per square. |
| Water | Hold the watering can, left click a planted square. A dry crop doesn't grow. Wet soil is darker. |
| Fill the can | Hold the can, aim at the river, left click. One fill waters 6 squares; the can starts empty. |
| Harvest | Press E on a ripe square. The soil stays tilled for the next seed. |

A crop that needs two waterings grows half way, dries out, and waits for the can again.

| Crop | Grows in | Waterings | Harvest per square |
| --- | --- | --- | --- |
| Beet | 45 s | 1 | 2 beets |
| Carrot | 60 s | 1 | 2 carrots |
| Onion | 75 s | 1 | 3 onions |
| Tomato | 90 s | 2 | 3 tomatoes |
| Wheat | 100 s | 2 | 4 wheat |
| Cabbage | 120 s | 2 | 2 cabbages |

| Part | Where |
| --- | --- |
| Crop settings (seed, harvest, grow time, waterings, stage models) | `Assets/_Game/Data/DA_Beet`, `DA_Cabbage`, `DA_Carrot`, `DA_Onion`, `DA_Tomato`, `DA_Wheat` |
| Carrot plants, hoe, stump | The Holotna pack (`CarrotPlant01-03`, `Hoe01`, `OakStump01`) |
| Other crop plants, watering can, soil mound, square outline | `Assets/_Game/Models/Crops`, `Models/Tools`, `Models/Farm` |
| Field and square behaviour | `FarmField` and `FarmPlot` on `03_Farm/Crop field`. Each tilled square is a copy of `Prefabs/Farm/PFB_FarmTile` (`FarmTile`), made while playing. |
| Tools | `PlayerTools` on the Player; `ToolPickup` on the stump tools; `WaterSource` on `River water` |

`FarmPlot.GrowthSpeed` scales growing for later upgrades. The river's collider is on the Water layer and touches nothing, so the player still wades through; water can be aimed at from up to 6 m because it lies below the bank.

The farm parts have editor tools, and `Rebuild map` runs the first three:

- `Rushed Crops > Farming > Set up crop field` lays out the field, its outline and the square it copies when tilling.
- `Rushed Crops > Farming > Place tool stump` puts the stump, hoe and watering can back by the gate.
- `Rushed Crops > Farming > Set up river water` gives the river its collider and `WaterSource`.
- `Rushed Crops > Farming > Update crops in crop field` gives the field every crop listed in `FarmPlotSetup.CropAssets`, for when a crop is added there.
- `Rushed Crops > Player > Fit tools to the hand` puts the hoe, watering can and hatchet back in the fist after a hand or tool model changes.

## Hotbar

The five hotbar slots show a picture of what they hold, with the count in the corner for things that stack. Seeds show a sack with their crop in the corner. The selected slot lights up and its item's name shows above the hotbar. An item without a picture shows its name instead.

| Part | Where |
| --- | --- |
| Hotbar | `InventoryHud`; tested in `Assets/Tests/EditMode/Editor/InventoryHudTests.cs` |
| Pictures | `Assets/_Game/Textures/UI/Icons`, listed in `Assets/_Game/Data/DA_ItemIcons` |
| Seed sack model | `Assets/_Game/Models/Farm/SeedSack.fbx`, made in Blender |

The pictures are made from the item models. A new item first goes in the model list at the top of `ItemIconRenderer`. After that, or after changing a model, run `Rushed Crops > UI > Render item icons`. `Rushed Crops > UI > Add item icons to the hotbar` rebuilds the picture in each slot and links `DA_ItemIcons`.

## Market

The scheming merchant stands behind the stall counter at the market. Walk up and he waves and greets you; he turns to watch you while you are nearby.

| Step | How |
| --- | --- |
| Trade | Look at him, press E. The trading window opens and your controls pause. |
| Sell crops | Left column: how many you have and what he pays for one. Sell 1 or Sell all. |
| Buy | Right column: seeds, wood and stone with his price for one. Buy 1 or Buy 5. |
| Leave | Esc, E, the Close button, or the gamepad's East button. |

Every price is shown before you click. A trade is all or nothing: coins are only taken if the goods fit in the hotbar. After a trade he rubs his hands and says something smug; he also grumbles when you can't afford something or your hotbar is full.

Prices change every in-game day: what he pays for crops moves up to 30% up or down, and his own prices up to 20%. Usual prices:

| Crop | Seed costs | He pays per crop |
| --- | --- | --- |
| Beet | 2 | 3 |
| Carrot | 3 | 4 |
| Onion | 3 | 3 |
| Wheat | 3 | 3 |
| Tomato | 4 | 5 |
| Cabbage | 5 | 9 |

He also sells Wood for 8 and Stone for 10 each, on purpose more than they should cost once they can be gathered.

| Part | Where |
| --- | --- |
| Price list (items, usual prices, daily swing) | `Assets/_Game/Data/DA_MerchantPrices`. Change prices here, not in code. |
| Trading rules | `Merchant` on the merchant; tested in `Assets/Tests/EditMode/Editor/MerchantTests.cs` |
| Greetings, lines, facing the player | `MerchantPersonality` on the merchant. Every line can be edited in the Inspector. |
| Trading window | `MerchantShopUI` on `HUD_Shop` |
| Merchant model and animations (Idle, Greet, RubHands) | `Assets/_Game/Models/Characters/Merchant.fbx`, made in Blender |

`Rushed Crops > Market > Set up merchant` puts the merchant back behind the counter, rebuilds the trading window and adds an EventSystem if the scene has none. `Rebuild map` places the merchant with the stall.

## Editing the map

- `WORLD_RushedCrops` contains named terrain, river, farm, market, quarry, logging, forest, and boundary groups. Paths, clearings, and tilled soil are painted into the terrain mesh. Holotna objects remain prefab instances with scene overrides. Source pack assets are not edited. There are no direction signs.
- The original `Ground` object is inactive and retained. The new terrain and bridge deck colliders use the controller's existing Ground layer.
- Generated meshes are under `Assets/_Game/Models/World/` and `Models/Cartoon/`, and their materials under `Assets/_Game/Materials/World/` and `Materials/Cartoon/`. They are saved assets; the map is not generated each time the game starts.
- `Rushed Crops > World > Rebuild map` recreates the world from the current layout settings after a confirmation dialog. Rebuilding replaces hand edits to the map. Normal adjustments can be made directly to the saved scene.
- `Rushed Crops > World > Validate saved map` checks landmark groups, collision mesh, player, Inventory/HUD, the crop field, the tool stump, the river water, the merchant and his trading window, the hotbar pictures, missing scripts/materials, and lighting references.
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
- The current saved map validator passed with all 12 lantern references assigned, an untilled crop field, one river bridge, and no misplaced scenery in the footprint support check.
- The initial pivot placement check found 599 buried scenery bases. Grounding now samples the low mesh vertices against the terrain collider and preserves mounted supports.
- Historical Play-mode input checks on the earlier two-bridge layout passed: farm-to-merchant trail 20.21 m; Willow bridge 19.70 m; Timber bridge 19.81 m. These do not certify traversal of the newer one-bridge layout. Repeat traversal in Play for the current map.
- The live cycle crossed from Day 1 at 23:54 into Day 2; the ten lanterns enabled at night.
- Images and traversal/cycle logs are retained under `Claude-Code-Game-Studios/production/qa/evidence/holotna-world/` in the local working copy.
- The first local Windows build attempt was cancelled when the art direction was revised. No completed Windows player is claimed for this pass. The tracked Unity build method can still be run locally; the hosted workflow is now local-only and ignored.

Inspector setup: none. Current-layout traversal, gamepad-specific traversal, and a full gameplay session remain manual playtest steps.
