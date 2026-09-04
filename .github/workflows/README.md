# CI

| Workflow | What |
|----------|------|
| `build-legacy-apk.yml` | Debug APK from `legacy-webview/` (Kotlin WebView) — работает без секретов |
| `build-unity-apk.yml` | Debug APK from `unity/Ironwake/` via [GameCI](https://game.ci/) |

## Unity APK secrets

В репозитории: **Settings → Secrets and variables → Actions**:

| Secret | Когда |
|--------|--------|
| `UNITY_LICENSE` | Personal (содержимое `.ulf` после активации) |
| `UNITY_EMAIL` + `UNITY_PASSWORD` + `UNITY_SERIAL` | Plus / Pro |

Инструкция активации: https://game.ci/docs/github/activation

Запуск: Actions → **Build Unity Android APK** → Run workflow.
