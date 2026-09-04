# IRONWAKE — Unity URP клиент

Мультиплеерный клиент танков / БТР / вертолётов / штурмовиков (в духе Massive Warfare / Battle of Tanks).

**Бой больше не в WebView.** Боевой клиент — Unity URP (`unity/Ironwake/`).  
Старый Kotlin + WebView APK сохранён в [`legacy-webview/`](legacy-webview/) для справки и CI.

Сервер: [ironwake-server](https://github.com/ivanivorontsov-sudo/ironwake-server) · live **http://biker9td.beget.tech**

## Структура

```
unity/Ironwake/          ← открывать в Unity Hub
  Assets/Scripts/
    Net/IronwakeClient.cs
    Combat/VehicleController.cs
    Combat/ModuleDamagePresenter.cs
    Combat/ProjectileVisual.cs
    Combat/BattleBootstrap.cs
    Vehicles/VehicleCatalog.cs
    Meta/HangarUI.cs
    Meta/GoogleAuthPlaceholder.cs
  Assets/Scenes/         ← Hangar, Battle (плейсхолдеры)
  Packages/manifest.json ← URP
legacy-webview/          ← бывший Android WebView APK
DESIGN.md                ← дизайн боя / протокол / roadmap
```

## Открыть в Unity

Облачный агент **не** генерирует `Library/` — нужен локальный Editor.

1. Поставьте **Unity 6** (проект помечен `6000.0.x`) **или** Unity **2022.3 LTS**.
   - На 2022.3 откройте проект и согласуйте версию URP в Package Manager
     (для 2022.3 обычно URP 14.x вместо 17.x из `manifest.json`).
2. Unity Hub → **Open** → папка `unity/Ironwake/`.
3. Дождитесь импорта пакетов (URP подтянется из `Packages/manifest.json`).
4. Window → Rendering → создайте URP Asset, назначьте в Graphics/Quality
   (см. `Assets/Settings/README.md`).
5. Сцены: см. `Assets/Scenes/README.md` — повесьте `HangarUI` / `BattleBootstrap`.
6. Play в Hangar → «В БОЙ».

Примитивы (кубы/плоскости) используются до покупки Asset Store паков.

### Рекомендуемые бесплатные / дешёвые паки (позже)

- **Tank** — «Simple Tanks» / «War Tanks» free kits (Asset Store)
- **Helicopter** — «Simple Helicopters» или low-poly heli packs
- **Plane** — «Simple Planes» / jet placeholders
- **VFX** — Unity Particle Pack, Cartoon Explosion free
- **Terrain** — URP terrain demo / Synty low-poly nature (платно)

Список не закреплён лицензией репозитория — проверяйте EULA перед шиппингом.

## Сборка Android (локально)

Unity APK **пока только локально** (нет Unity license на CI).

1. File → Build Settings → Android.
2. Player Settings: package `com.ironwake.combat`, minSdk 26, Orientation Landscape.
3. Internet permission + cleartext HTTP (Beget без HTTPS):
   - Custom Main Manifest / `usesCleartextTraffic=true`, или
   - Network Security Config как в `legacy-webview/`.
4. Build APK / AAB.

Legacy WebView APK по-прежнему собирается GitHub Actions из `legacy-webview/`
(см. `.github/workflows/build-legacy-apk.yml`).

## Тест против live-сервера

```
BASE = http://biker9td.beget.tech
GET  /health
POST /room/join   { room, mode:"laststand", userId, callsign, vehicleId }
POST /room/input  { room, userId, x,y,z,yaw,turretYaw,gunPitch, hit? }
GET  /room/state?room=public
WS   /ws          (на Beget часто закрыт nginx → клиент падает на HTTP poll)
```

`IronwakeClient` сначала пробует WebSocket, затем HTTP poll (~8 Hz) + input pump (~12 Hz).

В Editor: Hangar → В БОЙ. Статус в HUD показывает `http joined` / `ws joined`.  
Гостевой вход: `GoogleAuthPlaceholder.GuestLogin()` (Google Sign-In — см. скрипт).

Валюты UI: **Сталь / Разведка / Награды** (серверные значения подтянем, когда API гаража стабилизируется).

## Протокол — гибкость

Сервер ещё эволюционирует. Клиент парсит `units` / `team` / `ended` толерантно
(вложенный `payload` или плоский JSON). Когда схема застынет — замените
ручной парсер на `JsonUtility` / Newtonsoft DTOs.

Подробности: [DESIGN.md](DESIGN.md).
