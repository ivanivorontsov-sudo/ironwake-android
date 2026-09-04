# IRONWAKE — Unity URP клиент (движок на устройстве)

Мультиплатформенный клиент танков / БТР / авто / вертолётов / самолётов
(ощущения Massive Warfare / Battle of Tanks / War Thunder mobile).

**Бой и графика считаются на телефоне.** Сервер
[http://biker9td.beget.tech](http://biker9td.beget.tech) хранит аккаунты,
гараж, валюты, достижения и принимает `POST /match` — он **не** является
visual/combat engine.

Unity проект: `unity/Ironwake/` (URP 17 / Unity 6).  
Legacy Kotlin + WebView: [`legacy-webview/`](legacy-webview/).  
Sister server: [ironwake-server](https://github.com/ivanivorontsov-sudo/ironwake-server).

## Архитектура (кратко)

| | |
|--|--|
| **Default battle** | `LocalBattleSim` @ 20 Hz + боты (офлайн/demo) |
| **Graphics** | Runtime builders: среда, техника, VFX, URP tuner |
| **Meta** | `GET /user`, `/catalog/vehicles`, `/achievements`, `POST /auth/google`, `POST /match` |
| **Optional online** | `IronwakeClient` room join / HTTP poll (WS blocked on Beget) |

Подробности: [DESIGN.md](DESIGN.md).

## Структура

```
unity/Ironwake/Assets/
  Scripts/
    Sim/           LocalBattleSim, LocalBotAI, ModuleSystem
    Graphics/      BattleEnvironmentBuilder, TankVisualBuilder, CombatVfx, UrpVisualTuner
    Input/         MobileBattleInput
    Combat/        BattleBootstrap (default LocalSim), VehicleController, …
    Net/           IronwakeClient (meta + optional rooms)
    Meta/          HangarUI (Локальный бой + Онлайн), GoogleAuthPlaceholder
    Vehicles/      VehicleCatalog
  Shaders/         IW_SimpleLitTriplanar.shader
  Scenes/          Hangar.unity, Battle.unity (placeholders — bootstrap fills Play Mode)
```

## Открыть в Unity Hub

1. Установите **Unity 6** (`6000.0.x`) с Android Build Support (или 2022.3 LTS + подправьте URP).
2. Hub → **Open** → выберите папку `unity/Ironwake/`.
3. Дождитесь импорта (`Library/` создастся локально — **не коммитьте**).
4. URP Asset назначьте в Project Settings → Graphics / Quality
   (см. `Assets/Settings/README.md`). Для bloom/vignette: Global Volume
   (runtime `UrpVisualTuner` пытается создать overrides; иначе добавьте в Editor).
5. Scenes in Build: Hangar (0), Battle (1) — уже в `EditorBuildSettings.asset`.
6. Play → Hangar → **ЛОКАЛЬНЫЙ БОЙ**. Камера **V** / кнопка КАМЕРА. Смерть → spectator, без респавна.

## Управление

| | |
|--|--|
| WASD / джойстик | Ход |
| Мышь / свайп справа | Прицел |
| ЛКМ / Space / ОГОНЬ | Выстрел |
| Shift / ТОРМОЗ | Тормоз |
| V / КАМЕРА | FPS ↔ chase / spectator |

## Сборка Android

Локально (на CI нет Unity license):

1. File → Build Settings → Android → Switch Platform.
2. Player: package `com.ironwake.combat`, minSdk **26**, orientation **Landscape**,
   **Cleartext HTTP** для Beget (meta).
3. Build APK / AAB.

Legacy WebView APK: `.github/workflows/build-legacy-apk.yml`.

## Рекомендуемые free Asset Store паки (замена procedurals)

Честная замена «коробок» на более богатые меши/VFX (без пиратства):

1. **Unity Particle Pack** (free) — вспышки, дым, огонь  
2. **Simple Military / Low Poly tanks** (ищите актуальные free pack’и в Store: *Low Poly Tank*, *Military Props*)  
3. **Outdoor Ground Textures** / *Terrain Textures Free* — dirt/sand  
4. **Free Yughues Free Sand Materials** или аналог URP-ready dirt  
5. **War FX** / *Explosion* free packs — cook-off / impacts  

После импорта: подставьте prefab’ы в `TankVisualBuilder` / `CombatVfx` /
`BattleEnvironmentBuilder` вместо `CreatePrimitive`.

## HDR / Volume fallback

Если Volume overrides не резолвятся в runtime, `UrpVisualTuner` всё равно
включает shadows, MSAA, `camera.allowHDR`, target 60 FPS. В Editor добавьте
**Global Volume** + Bloom / Vignette / Color Adjustments на URP asset.

## Честное ограничение

AAA scanned tanks нужен art pipeline (high-poly → LODs → PBR). Этот репозиторий
поставляет **системы + сильный procedural vertical slice**, чтобы графика и бой
работали на устройстве без серверного рендера.

## Документация

- [DESIGN.md](DESIGN.md) — device-authoritative бой, meta server, roadmap  
- PROTOCOL (server) — опциональный online room sync  
