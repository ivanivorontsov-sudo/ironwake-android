# URP / Quality settings

1. Create **URP Asset** (Universal Render Pipeline Asset) + Renderer.
2. Project Settings → Graphics → Scriptable Render Pipeline Settings → assign URP Asset.
3. Quality → each level → Rendering → same URP Asset.
4. Enable HDR on URP Asset; soft shadows recommended.
5. Optional: Global Volume (Bloom ~0.35, Vignette ~0.28, Color Adjustments).
   Runtime `UrpVisualTuner` tries to create these; if overrides missing, cameras still get `allowHDR`.

Mobile: keep MSAA 2x, shadow distance ≤ 100, no expensive SSAO for mid-tier phones.
