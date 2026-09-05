# Android export (Godot 4.4)

1. Install **Godot 4.4.x** + Android Build Template (Editor → Export → Manage Export Templates).
2. Install Android SDK / JDK as in [Godot Android docs](https://docs.godotengine.org/en/4.4/tutorials/export/exporting_for_android.html).
3. Open `godot/Ironwake/project.godot`.
4. Project → Export → **Android** preset (`export_presets.cfg`).
5. Ensure **Internet** + cleartext HTTP (Beget meta uses `http://`).
   - With Gradle build: add to `android/build/src/.../AndroidManifest.xml` or use Godot 4 export option
     `permissions/internet` (already on) and network security config allowing cleartext for `biker9td.beget.tech`.
6. Export APK → `godot/build/ironwake-android.apk`.

CI cannot sign/export without Android SDK + export templates; see `.github/workflows/README.md`.
