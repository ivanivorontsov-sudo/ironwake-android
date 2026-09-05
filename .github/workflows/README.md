# CI

| Workflow | What |
|----------|------|
| `build-legacy-apk.yml` | Debug APK from `legacy-webview/` (Kotlin WebView) — работает без секретов |
| `build-unity-apk.yml` | Debug APK from `unity/Ironwake/` via [GameCI](https://game.ci/) — нужен Unity license |
| Godot Android | **Документировано** в `godot/Ironwake/export/README.md`. Полноценный export job требует Android SDK + Godot export templates на runner (без секретов возможно для debug unsigned, но templates ~тяжёлые). Primary клиент — Godot; собирайте APK локально из Editor. |

## Unity APK secrets (legacy only)

В репозитории: **Settings → Secrets and variables → Actions**:

| Secret | Когда |
|--------|--------|
| `UNITY_LICENSE` | Personal (содержимое `.ulf` после активации) |
| `UNITY_EMAIL` + `UNITY_PASSWORD` + `UNITY_SERIAL` | Plus / Pro |

Инструкция: https://game.ci/docs/github/activation
