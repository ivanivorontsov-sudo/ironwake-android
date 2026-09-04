# IRONWAKE Android

Fullscreen WebView client for **IRONWAKE**. GitHub Actions builds a debug APK on every push.

The APK loads the live hangar (tanks, helicopters, jets, first-person gunner, module damage). Change `GAME_URL` in `app/src/main/res/values/strings.xml` to your deployed hangar.

## GitHub Actions

Workflow: [`.github/workflows/build-apk.yml`](.github/workflows/build-apk.yml)

- JDK 17
- Android SDK 35
- `assembleDebug`
- Uploads `Ironwake-debug.apk` as an artifact

Open the **Actions** tab after push, download the APK from the run.

## Local

```bash
# with Android Studio: Open this folder, Run app
# or
gradle :app:assembleDebug
```

`minSdk 26`. Internet permission required. Hardware-accelerated WebView.
