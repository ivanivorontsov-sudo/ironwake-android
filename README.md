# IRONWAKE — Unity URP клиент

Мультиплеерный клиент танков / БТР / авто / вертолётов / самолётов
(ощущения в духе Massive Warfare / War Thunder mobile vertical slice).

**Бой в Unity URP** (`unity/Ironwake/`). Legacy Kotlin + WebView — в [`legacy-webview/`](legacy-webview/).

Сервер: [ironwake-server](https://github.com/ivanivorontsov-sudo/ironwake-server) · live **http://biker9td.beget.tech**  
Протокол: [PROTOCOL.md](https://github.com/ivanivorontsov-sudo/ironwake-server/blob/main/PROTOCOL.md)

## Структура

```
unity/Ironwake/
  Assets/Scripts/
    Net/IronwakeClient.cs          ← WS → HTTP poll fallback
    Combat/VehicleController.cs    ← prediction + soft-correct, FPS/chase, spectator
    Combat/ModuleDamagePresenter.cs← hull_f/s/r…optics + UI strip / OnGUI
    Combat/ProjectileVisual.cs     ← tracers from state.projectiles / shot
    Combat/BattleBootstrap.cs      ← local + RemoteUnitView interpolators
    Vehicles/VehicleCatalog.cs     ← GET /catalog/vehicles
    Meta/HangarUI.cs               ← Сталь / Разведка / Награды → В БОЙ
  Assets/Scenes/                   ← Hangar, Battle placeholders
```

## Сеть (Beget reality)

| Путь | Назначение |
|------|------------|
| `WS /ws` | Предпочтительно; на Beget **blocked** (`health.ws`) |
| `POST /room/join` | HTTP join |
| `POST /room/input` | **только** throttle/steer/brake/fire/aimYaw/aimPitch/turretYaw/gunPitch |
| `GET /room/state?room=` | Poll **10–20 Hz** → units + projectiles + events |
| `GET /catalog/vehicles` | Ангар / магазин |
| `GET /user?userId=` | Кошелёк (если профиль есть) |

Клиент **никогда** не шлёт hp / hit / damage — урон и модули только с сервера
(events: `shot`, `hit`, `module_break`, `fire_start/end`, `cookoff`, `kill`, `spectator`, `end`).

`IronwakeClient` поднимает C# events: `OnState`, `OnGameEvent`, `OnMatchEnd`, `OnStatus`.

## Открыть в Unity

1. Unity **6** (`6000.0.x`) или **2022.3 LTS** (подправьте URP в Package Manager).
2. Hub → Open → `unity/Ironwake/`.
3. URP Asset → Graphics/Quality (см. `Assets/Settings/README.md`).
4. Сцены: `HangarUI` / `BattleBootstrap` (см. `Assets/Scenes/README.md`).
5. Play → Hangar → «В БОЙ». Камера: **V**. Смерть → spectator, без респавна.

Примитивы до Asset Store паков. `Library/` не коммитить.

## Сборка Android

Локально (нет Unity license на CI). Package `com.ironwake.combat`, minSdk 26, landscape,
cleartext HTTP для Beget. Legacy APK — `.github/workflows/build-legacy-apk.yml`.

## Документация

- [DESIGN.md](DESIGN.md) — бой, модули, roadmap
- Sister PROTOCOL — authoritative 20 Hz sim
