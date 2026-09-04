# IRONWAKE — дизайн клиента (Unity URP)

## Цель

Нативный Unity URP клиент с «тяжёлой» техникой: инерция, модульные попадания,
last-stand **без респавна**, spectator, читаемая военная графика.
Ощущения в духе Massive Warfare / Battle of Tanks / War Thunder mobile —
**не** клон ассетов.

## Архитектурный сдвиг (device-authoritative)

| Слой | Где | Что |
|------|-----|-----|
| **Бой / графика** | **На телефоне** | Движение, баллистика, модули, боты, камера, VFX, окружение |
| **Мета** | Сервер `http://biker9td.beget.tech` | Аккаунты, гараж, валюты, достижения, отчёт матча |

Сервер **не** является визуальным / combat-движком. Старый online hangar +
HTTP poll комнат остаётся опциональным (`Online`); по умолчанию бой =
**LocalBattle** (`LocalBattleSim` @ 20 Hz + боты), работает офлайн / demo.

```
HangarUI ──► Local Battle (primary) ──► LocalBattleSim + Graphics builders
         └─► Online (optional)      ──► IronwakeClient room join/poll
```

После локального матча клиент best-effort шлёт `POST /match` (если известен
`userId`) для наград. Боевой симулятор при этом не ждёт сервер.

## Мета-API (сервер)

| Метод | Назначение |
|-------|------------|
| `POST /auth/google` | Google auth (placeholder на клиенте) |
| `GET /user?userId=` | Кошелёк / профиль |
| `GET /catalog/vehicles` | Гараж / магазин |
| `GET /achievements` | Достижения |
| `POST /match` | Отчёт результата локального/онлайн боя |

Опциональный online room (legacy): `POST /room/join`, `POST /room/input`,
`GET /room/state` @ 10–20 Hz. WS на Beget заблокирован nginx.

## Локальный симулятор

- **`Sim/LocalBattleSim`** — тик 20 Hz: игроки + боты, throttle/steer,
  снаряды + гравитация, карта модулей, fire DoT, cook-off, **без респавна**,
  spectator, конец матча.
- **`Sim/LocalBotAI`** — заполняет комнату ботами (chase / circle / fire).
- **`Sim/ModuleSystem`** — упрощённый pen/facing → эффекты на mobility/fire.
- Модули: `hull_f/s/r`, `turret`, `gun`, `engine`, `ammo`, `track_l/r`,
  `fuel`, `optics` ∈ [0,1].

`VehicleController` в LocalSim-режиме получает позу из снимков сима;
ввод (в т.ч. мобильный) уходит в `LocalBattleSim.SetLocalInput`.

## Графика (URP, procedural vertical slice)

- **`Graphics/BattleEnvironmentBuilder`** — земля с grid/dirt, холмы
  (displaced mesh) + berms, мешки/руины из примитивов, sun + ambient, fog,
  sky clear-color gradient.
- **`Graphics/UrpVisualTuner`** — Volume bloom/vignette/color adjust если URP
  Volume доступен; иначе QualitySettings + camera HDR (см. README).
- **`Graphics/TankVisualBuilder`** — танк/БТР/авто/вертолёт/самолёт из
  нескольких примитивов, тёмные military цвета, декали, exhaust smoke.
- **`Graphics/CombatVfx`** — вспышка, трассер, искры, огонь, cook-off, пыль.
- **`Assets/Shaders/IW_SimpleLitTriplanar.shader`** — mobile-friendly dirt/metal.

**Честно:** AAA scanned tanks требуют art pipeline. Этот PR даёт системы +
сильный procedural vertical slice; ниже — рекомендованные free Asset Store
паки для замены.

## Ввод

`Input/MobileBattleInput` — виртуальный джойстик, кнопка огня, aim-drag;
клавиатура/мышь в Editor (WASD, мышь, Space, V).

## Сцены

| Сцена | Роль |
|-------|------|
| **Hangar** | Кошелёк, каталог, **Локальный бой** + Онлайн |
| **Battle** | Runtime environment + LocalSim (default) или online remotes |

Пустые YAML-сцены играют за счёт bootstrap (среда и техника создаются в Play).

## Roadmap

1. Авторские меши / VFX вместо procedurals (Asset Store / собственный пайплайн)
2. Опциональный multiplayer sync через IronwakeClient (уже есть каркас)
3. Google Sign-In plugin + SHA-1
4. GameCI Unity APK при появлении license

## Ограничения

- Нет `Library/` / огромных бинарников в git
- Нет Unity Editor на cloud/box — валидный C# + scene YAML + shader
- Procedural ≠ AAA; цель — читаемый военный vertical slice на устройстве
