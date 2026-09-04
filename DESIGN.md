# IRONWAKE — дизайн клиента (Unity URP)

## Цель

Заменить WebView/Three.js бой на нативный Unity URP клиент с «тяжёлым»
ощущением техники: инерция корпуса, лаг башни, модульные попадания,
last-stand без респавна. Мета: ангар, валюты, магазин, достижения, Google Sign-In.

Референсы ощущений: Massive Warfare, Battle of Tanks — не клоны ассетов.

## Сцены

| Сцена | Роль |
|-------|------|
| **Hangar** | Кошелёк (Сталь / Разведка / Награды), гараж, магазин-заглушка, достижения, вход в бой |
| **Battle** | Одна жизнь, комната `laststand`, FPS-наводчик + chase-камера, HUD модулей |

## Техника

`VehicleCatalog` — четыре класса-плейсхолдера:

| id | класс | заметки |
|----|-------|---------|
| `k72-ural` | Танк | Базовый геймплей: инерция, лаг башни |
| `btr-82` | БТР | Быстрее, слабее «урон» снаряда |
| `ka-52` | Вертолёт | `cruiseAltitude`, выше скорость |
| `su-25` | Штурмовик | Ещё выше, быстрее очередь |

Визуал сейчас — примитивы. Меши/VFX подключаются без смены протокола.

## Бой

- **VehicleController** — ввод стиков/клавы → скорость с accel/drag, поворот корпуса, turret yaw / gun pitch, fire cooldown.
- **Камеры** — `GunnerFps` (из башни) и `Chase`; переключение кнопкой.
- **ModuleDamagePresenter** — зоны `hull_front`, `engine`, `ammo`, `track_l/r`; огонь, гусеницы, cook-off БК.
- **ProjectileVisual** — трассер; хит локально красит модули, сервер остаётся источником правды.

Правило: **нет возрождения** в режиме laststand (как на сервере).

## Сеть

`IronwakeClient` (`Net/`):

1. `GET /health`
2. Попытка `WS /ws` + `{type:"join"| "input"}`
3. Fallback HTTP: `POST /room/join`, `POST /room/input`, poll `GET /room/state`
4. Live Beget: WS часто blocked → HTTP — основной путь

Hit payload (клиент → сервер):

```json
"hit": { "target": "<userId>", "module": "hull_front", "damage": 110 }
```

Auth: `POST /auth/google` `{ "credential": "<id_token>" }` — см. `GoogleAuthPlaceholder`.

Сестра: https://github.com/ivanivorontsov-sudo/ironwake-server

## Мета

- Валюты: **Сталь**, **Разведка**, **Награды**
- Магазин / достижения — UI-заглушки до стабильного REST гаража
- Google Sign-In на Android документирован в `GoogleAuthPlaceholder.cs`

## Сборка

- Unity APK — **локально** (нет Unity на CI / cloud agents)
- Legacy WebView APK — GitHub Actions → `legacy-webview/`

## Roadmap (кратко)

1. Авторские сцены Hangar/Battle + URP Asset в Editor  
2. Нормальный Json DTO слой под застывший протокол  
3. Настоящие меши / треки / роторы  
4. Серверный гараж → кошелёк и анлок техники  
5. Google Sign-In plugin + SHA-1  
6. CI Unity (GameCI) когда появится license  

## Ограничения scaffold

- Нет `Library/` в git (генерирует Editor)
- Нет бинарных Asset Store паков в репозитории
- Сцены — минимальные YAML-плейсхолдеры; bootstrap-скрипты создают примитивы в Play Mode
