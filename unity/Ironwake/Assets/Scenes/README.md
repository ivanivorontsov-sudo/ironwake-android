# Scenes

Placeholders. Open the project in **Unity Hub** (Unity 6 recommended, or 2022.3 LTS with URP package aligned) — the Editor will regenerate `Library/` and can rebuild scene YAML.

| Scene | Purpose | Bootstrap |
|-------|---------|-----------|
| `Hangar.unity` | Meta: currencies, garage, shop, achievements, Google auth stub | Add empty GO + `HangarUI` |
| `Battle.unity` | Combat: last-stand room vs live server | Add empty GO + `BattleBootstrap` |

Both scenes are listed in `ProjectSettings/EditorBuildSettings.asset`.

## Quick wire-up after first open

1. Create scene **Hangar**: Directional Light + empty `Hangar` with `Meta/HangarUI`.
2. Create scene **Battle**: empty `Battle` with `Combat/BattleBootstrap` (it spawns ground/sun/vehicle).
3. File → Build Settings → add both scenes (order: Hangar = 0).
4. Enter Play Mode in Hangar → «В БОЙ».

Until scenes are authored, `HangarUI` can boot a primitive battle in-place if `battleSceneName` is cleared.
