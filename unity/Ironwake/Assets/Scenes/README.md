# Scenes

Placeholders. Open in **Unity Hub** (Unity 6) — Editor regenerates `Library/`.

| Scene | Purpose | Bootstrap |
|-------|---------|-----------|
| `Hangar.unity` | Wallet, catalog, **Локальный бой** + Онлайн | `Meta/HangarUI` |
| `Battle.unity` | LocalSim (default) or online poll | `Combat/BattleBootstrap` |

Both listed in `EditorBuildSettings.asset` (Hangar = 0).

## Wire-up

1. Hangar: empty GO + `HangarUI` (builds military canvas at runtime).
2. Battle: empty GO + `BattleBootstrap` — builds environment, tanks, VFX, LocalSim.
3. Play Hangar → **ЛОКАЛЬНЫЙ БОЙ**. Camera **V**. Death → spectator (no respawn).

`PlayerPrefs iw.battleMode`: `local` (default) | `online`.
