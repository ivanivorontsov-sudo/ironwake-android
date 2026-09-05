# IRONWAKE — Godot 4 клиент (движок на устройстве)

Мультиплатформенный клиент танков / БТР / авто (ощущения Massive Warfare /
Battle of Tanks / War Thunder mobile).

**Бой и графика считаются на телефоне.** Сервер
[http://biker9td.beget.tech](http://biker9td.beget.tech) хранит аккаунты,
гараж, валюты, достижения и принимает `POST /match` — он **не** является
visual/combat engine.

| Клиент | Путь | Статус |
|--------|------|--------|
| **Godot 4.4 (primary)** | [`godot/Ironwake/`](godot/Ironwake/) | Открывать / экспортировать APK отсюда |
| Unity URP (legacy) | [`unity/Ironwake/`](unity/Ironwake/) | Сохранён, не требует лицензии для Godot-сборки |
| Kotlin + WebView | [`legacy-webview/`](legacy-webview/) | Legacy launcher |

Sister server: [ironwake-server](https://github.com/ivanivorontsov-sudo/ironwake-server).

## Архитектура (кратко)

| | |
|--|--|
| **Default battle** | `LocalBattleSim` @ 20 Hz + боты (офлайн/demo), **без респавна** |
| **Graphics** | `BattleEnvironment` + `TankVisual` (StandardMaterial3D, olive paints, dirt PBR) |
| **Meta** | `GET /health`, `/catalog/vehicles`, `POST /room/join`, `POST /match` |
| **Online button** | Join с таймаутом 10 с → fallback на локальный бой |

Подробности: [DESIGN.md](DESIGN.md).

## Структура (Godot)

```
godot/Ironwake/
  project.godot
  scenes/          hangar.tscn, battle.tscn
  scripts/
    meta/          hangar, game_state, vehicle_catalog
    net/           api_client (Beget HTTP, cleartext ok)
    sim/           local_battle_sim, module_system, local_bot_ai, sim_unit
    graphics/      battle_environment, tank_visual, materials
    combat/        battle_bootstrap, tank_controller
    input/         virtual_stick, battle_hud
  assets/textures/ Poly Haven aerial_grass_rock (CC0)
  export_presets.cfg
```

## Открыть в Godot

1. Установите **Godot 4.4.x** (на box: `/home/box/godot/Godot_v4.4.1-stable_linux.x86_64`).
2. Project → Import / Open → `godot/Ironwake/project.godot`.
3. Main scene: **Hangar**. Play → **ЛОКАЛЬНЫЙ БОЙ**.
4. Камера: **V** / кнопка **КАМЕРА** (chase ↔ FPS/gunner). Смерть → spectator, без респавна.

## Управление

| | |
|--|--|
| WASD / виртуальный стик | Ход |
| Свайп справа / RMB+мышь | Прицел |
| Space / ЛКМ / **ОГОНЬ** | Выстрел |
| Shift / тормоз | Тормоз |
| V / **КАМЕРА** | FPS ↔ chase |
| **АНГАР** | Выход из боя |

## Сборка Android APK (Godot)

Локально (CI без SDK/templates — только документация):

1. Editor → Manage Export Templates → скачать 4.4.x Android.
2. Project → Export → preset **Android** (`export_presets.cfg`).
3. Package: `com.ironwake.combat`, minSdk **26**, orientation **landscape**,
   permissions **Internet**; cleartext HTTP для Beget meta.
4. Export APK / AAB. См. [`godot/Ironwake/export/README.md`](godot/Ironwake/export/README.md).

Legacy WebView APK: `.github/workflows/build-legacy-apk.yml`.  
Unity APK: требует Unity license (legacy).

## Честное ограничение

AAA scanned tanks нужен art pipeline. Этот репозиторий даёт **системы +
procedural vertical slice** с нормальными материалами (не magenta cubes):
олива, dirt PBR, sky, sun+fill shadows, модульный урон, огонь/cook-off.

## Документация

- [DESIGN.md](DESIGN.md) — device-authoritative бой, meta server, roadmap  
- [godot/Ironwake/assets/CREDITS.md](godot/Ironwake/assets/CREDITS.md) — CC0 textures  
