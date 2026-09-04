# IRONWAKE Android

Клиент боя для сервера **http://biker9td.beget.tech**.

APK — полноэкранный WebView с локальным ангаром (`app/src/main/assets/hangar.html`).
Он проверяет `/health`, открывает `ws://biker9td.beget.tech/ws`, шлёт `join` / `input` и рисует снимок комнаты.

## Собрать APK

После пуша откройте GitHub → Actions → **Build APK** → скачайте `Ironwake-debug.apk`.

Локально: Android Studio → Open эту папку → Run.

`minSdk 26`, HTTP cleartext разрешён (Beget пока без HTTPS).

## Протокол

- `GET /health`
- `WS /ws` → `{type:"join", room, callsign, userId, vehicleId}`
- `{type:"input", x,z,yaw,turretYaw, hit?}`
- сервер шлёт `state` / `hit` / `kill` / `end`
