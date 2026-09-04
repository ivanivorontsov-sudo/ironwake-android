# URP Settings

`Packages/manifest.json` already depends on `com.unity.render-pipelines.universal`.

After opening in Unity Hub:

1. Window → Rendering → Render Pipeline Converter (or create via
   Assets → Create → Rendering → URP Asset (with Universal Renderer)).
2. Assign the URP Asset in Project Settings → Graphics / Quality.
3. Save assets under this folder (`Ironwake_URP.asset`, `Ironwake_Renderer.asset`).

We intentionally do **not** commit binary `.asset` blobs from a missing Editor —
Hub generates them cleanly on first open.
