# Scenes

Placeholders. Open in **Unity Hub** (Unity 6 or 2022.3 LTS) — Editor regenerates `Library/`.

| Scene | Purpose | Bootstrap |
|-------|---------|-----------|
| `Hangar.unity` | Сталь / Разведка / Награды, catalog, «В БОЙ» | `Meta/HangarUI` |
| `Battle.unity` | laststand vs live server, HTTP poll 10–20 Hz | `Combat/BattleBootstrap` |

Both listed in `EditorBuildSettings.asset` (Hangar = 0).

## Wire-up

1. Hangar: Directional Light + empty GO + `HangarUI`.
2. Battle: empty GO + `BattleBootstrap` (spawns ground/sun/vehicle/remotes).
3. Play Hangar → «В БОЙ». Camera toggle **V**. Death → spectator (no respawn).

Live Beget: WS blocked → client auto-falls back to HTTP `/room/*`.
If `battleSceneName` missing, Hangar boots battle in-place.
