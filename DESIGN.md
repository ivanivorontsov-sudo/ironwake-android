# IRONWAKE — дизайн клиента (Godot 4 primary)

## Цель

Нативный клиент с «тяжёлой» техникой: инерция, модульные попадания,
last-stand **без респавна**, spectator, читаемая военная графика.
Ощущения в духе Massive Warfare / Battle of Tanks / War Thunder mobile.

**Primary engine: Godot 4.4** (`godot/Ironwake/`).  
Unity URP (`unity/Ironwake/`) оставлен как legacy reference.

## Архитектурный сдвиг (device-authoritative)

| Слой | Где | Что |
|------|-----|-----|
| **Бой / графика** | **На телефоне** | Движение, баллистика, модули, боты, камера, VFX, окружение |
| **Мета** | Сервер `http://biker9td.beget.tech` | Аккаунты, гараж, валюты, достижения, отчёт матча |

```
Hangar ──► Local Battle (primary) ──► LocalBattleSim + TankVisual / Environment
       └─► Online (optional)      ──► ApiClient join (timeout 10s → local)
```

После локального матча клиент best-effort шлёт `POST /match`.

## Мета-API

| Метод | Назначение |
|-------|------------|
| `GET /health` | Пинг |
| `GET /catalog/vehicles` | Гараж |
| `POST /room/join` | Онлайн-вход (таймаут → local) |
| `POST /match` | Отчёт результата |

## Локальный симулятор (GDScript)

- **`LocalBattleSim`** — 20 Hz: игроки + боты, снаряды + гравитация, модули,
  fire DoT, cook-off, **без респавна**, spectator, конец матча.
- **`LocalBotAI`** — chase / circle / fire.
- **`ModuleSystem`** — pen/facing → mobility/fire. Ключи: `hull_f/s/r`,
  `turret`, `gun`, `engine`, `ammo`, `track_l/r`, `fuel`, `optics`.

## Графика

- **`BattleEnvironment`** — dirt ground (Poly Haven CC0), холмы, berms,
  ruins, ProceduralSky, sun + fill, fog, ACES.
- **`TankVisual`** — танк/БТР/авто/heli из мешей + **StandardMaterial3D**
  olive paints (не unassigned/magenta).
- Mobile renderer (`rendering_method=mobile`) + MSAA.

## Сцены

| Сцена | Роль |
|-------|------|
| **hangar.tscn** | Кошелёк stub, каталог, Local + Online |
| **battle.tscn** | Environment + LocalSim + HUD (stick / fire / camera / hangar) |

## Roadmap

1. Богатые меши / VFX вместо procedurals  
2. Полноценный online room sync (каркас join уже есть)  
3. Godot Android CI при наличии SDK + export templates на runner  
4. Unity legacy можно удалить после стабилизации Godot APK  

## Ограничения

- Нет `.godot/` / огромных бинарников в git (кроме CC0 1K textures)  
- Procedural ≠ AAA; цель — читаемый военный vertical slice на устройстве  
