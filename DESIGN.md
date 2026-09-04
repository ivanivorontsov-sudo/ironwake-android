# IRONWAKE — дизайн клиента (Unity URP)

## Цель

Нативный Unity URP клиент с «тяжёлой» техникой: инерция / prediction,
лаг башни, модульные попадания, last-stand **без респавна**, spectator.
Мета: ангар, Сталь / Разведка / Награды, каталог с сервера.

Референсы ощущений: Massive Warfare, War Thunder mobile — не клоны ассетов.

## Протокол (authoritative)

Сервер @ **20 Hz**. Клиент шлёт **только ввод**:

```json
{ "throttle", "steer", "brake", "fire", "aimYaw", "aimPitch", "turretYaw", "gunPitch" }
```

`hp` / `alive` / `hit` / `damage` **игнорируются** сервером.

State: `units[]` (pose, hp, modules, spectator…) + `projectiles[]` + `events[]`.

Модули: `hull_f`, `hull_s`, `hull_r`, `turret`, `gun`, `engine`, `ammo`,
`track_l`, `track_r`, `fuel`, `optics` ∈ [0,1].

Events: `shot`, `hit`, `module_break`, `fire_start`, `fire_end`, `cookoff`,
`kill`, `spectator`, `end`.

### Beget / HTTP poll

Live `http://biker9td.beget.tech` — WS закрыт nginx (`ws:"blocked-on-beget"`).
Клиент пробует WS (~3s timeout), затем:

1. `POST /room/join`
2. Input pump ~15 Hz → `POST /room/input`
3. Poll `GET /room/state?room=` @ **10–20 Hz**

Один и тот же room state кормит WS broadcast и HTTP poll.

## Сцены

| Сцена | Роль |
|-------|------|
| **Hangar** | Кошелёк, `GET /catalog/vehicles`, выбор техники, «В БОЙ» |
| **Battle** | Prediction + soft-correct, FPS gunner / chase (V), spectator |

## Техника

`VehicleCatalog` тянет live catalog (tank/apc/car/heli/plane) и спавнит примитивы
через `VehicleController.SpawnPrimitive`. Fallback ids совпадают с сервером
(`k72-ural`, `m-raptor`, `btr-iron`, `wolf-jeep`, `ka-scythe`, …).

## Бой

- **VehicleController** — local prediction (optional) + soft-correct к server pose;
  aim → turret lag / gun elevation; input = controls only.
- **Камеры** — GunnerFps (внутри башни) / Chase; **V**; после смерти —
  SpectatorFree или SpectatorFollow (killer).
- **ModuleDamagePresenter** — цвет/emission зон, tilt/spark гусениц, fire + cook-off;
  UGUI strip иконок модулей + OnGUI fallback.
- **ProjectilePresenter** — трассеры из `state.projectiles` и `shot` events.
- **BattleBootstrap** — local + `RemoteUnitView` lerp между снимками ~15 Hz.

## Мета

- Валюты UI: **Сталь**, **Разведка**, **Награды** (commendations / xp proxy)
- `GET /user?userId=` когда профиль есть; иначе гостевые значения
- Auth: `POST /auth/google` — см. `GoogleAuthPlaceholder.cs`

## Roadmap

1. Авторские меши / VFX вместо примитивов  
2. Newtonsoft / JsonUtility DTO когда PROTOCOL заморожен  
3. Google Sign-In plugin + SHA-1  
4. GameCI Unity APK при появлении license  

## Ограничения

- Нет `Library/` / огромных бинарников в git  
- Нет Unity Editor на cloud/box — валидный C# + scene YAML  
- Сцены-плейсхолдеры; bootstrap создаёт примитивы в Play Mode  
