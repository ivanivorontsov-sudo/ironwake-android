using UnityEngine;

namespace Ironwake.Graphics
{
    /// <summary>
    /// Runtime battlefield for Built-in RP: dirt ground with procedural texture,
    /// hills/berms, props, sun + fill light, fog, sky clear color.
    /// </summary>
    public static class BattleEnvironmentBuilder
    {
        public static GameObject Build(Transform parent = null, float size = 220f)
        {
            var root = new GameObject("BattleEnvironment");
            if (parent) root.transform.SetParent(parent, false);

            BuildGround(root.transform, size);
            BuildHills(root.transform, size);
            BuildProps(root.transform, size);
            BuildLighting();
            ApplyAtmosphere();

            var tuner = root.AddComponent<UrpVisualTuner>();
            tuner.Apply();

            return root;
        }

        static void BuildGround(Transform parent, float size)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(parent, false);
            float scale = size / 10f;
            ground.transform.localScale = new Vector3(scale, 1f, scale);
            var r = ground.GetComponent<Renderer>();
            if (r)
            {
                var mat = IwMaterials.Dirt(new Color(0.48f, 0.40f, 0.28f));
                ApplyTiling(mat, 24f);
                r.sharedMaterial = mat;
            }

            // Subtle darker tracks / dirt lanes (lit, not unlit flat)
            int lanes = 6;
            for (int i = -lanes; i <= lanes; i++)
            {
                if (i == 0) continue;
                float t = i / (float)lanes * size * 0.42f;
                MakeStrip(parent, $"LaneX_{i}", new Vector3(t, 0.015f, 0f),
                    new Vector3(0.55f, 0.02f, size * 0.85f),
                    new Color(0.36f, 0.30f, 0.21f, 1f));
                MakeStrip(parent, $"LaneZ_{i}", new Vector3(0f, 0.015f, t),
                    new Vector3(size * 0.85f, 0.02f, 0.55f),
                    new Color(0.34f, 0.29f, 0.20f, 1f));
            }

            // Edge ring so map boundary reads clearly
            float half = size * 0.48f;
            MakeStrip(parent, "EdgeN", new Vector3(0f, 0.4f, half), new Vector3(size * 0.96f, 0.8f, 1.2f),
                new Color(0.32f, 0.28f, 0.22f));
            MakeStrip(parent, "EdgeS", new Vector3(0f, 0.4f, -half), new Vector3(size * 0.96f, 0.8f, 1.2f),
                new Color(0.32f, 0.28f, 0.22f));
            MakeStrip(parent, "EdgeE", new Vector3(half, 0.4f, 0f), new Vector3(1.2f, 0.8f, size * 0.96f),
                new Color(0.32f, 0.28f, 0.22f));
            MakeStrip(parent, "EdgeW", new Vector3(-half, 0.4f, 0f), new Vector3(1.2f, 0.8f, size * 0.96f),
                new Color(0.32f, 0.28f, 0.22f));
        }

        static void ApplyTiling(Material m, float tile)
        {
            if (m == null) return;
            if (m.HasProperty("_MainTex")) m.SetTextureScale("_MainTex", new Vector2(tile, tile));
            if (m.HasProperty("_BaseMap")) m.SetTextureScale("_BaseMap", new Vector2(tile, tile));
        }

        static void MakeStrip(Transform parent, string name, Vector3 pos, Vector3 scale, Color col)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            if (r) r.sharedMaterial = IwMaterials.Dirt(col);
        }

        static void BuildHills(Transform parent, float size)
        {
            CreateDisplacedHill(parent, "Hill_N", new Vector3(0f, 0f, size * 0.28f), 28f, 7f, 24);
            CreateDisplacedHill(parent, "Hill_SW", new Vector3(-size * 0.22f, 0f, -size * 0.18f), 22f, 5.5f, 20);
            CreateDisplacedHill(parent, "Hill_E", new Vector3(size * 0.3f, 0f, 10f), 18f, 4.2f, 18);

            for (int i = 0; i < 5; i++)
            {
                float ang = i * 72f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (size * 0.18f);
                CreateBerm(parent, $"Berm_{i}", p, ang * Mathf.Rad2Deg + 90f, 14f + i, 2.2f);
            }
        }

        static void CreateDisplacedHill(Transform parent, string name, Vector3 center, float radius, float height, int segments)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = IwMaterials.Dirt(new Color(0.44f, 0.37f, 0.26f));
            mf.sharedMesh = BuildHillMesh(radius, height, segments);
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mf.sharedMesh;
        }

        static Mesh BuildHillMesh(float radius, float height, int segments)
        {
            int rings = Mathf.Max(4, segments / 3);
            int vertsPerRing = segments;
            var verts = new Vector3[(rings + 1) * vertsPerRing + 1];
            var norms = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            int vi = 0;
            verts[vi] = new Vector3(0f, height, 0f);
            norms[vi] = Vector3.up;
            uvs[vi] = new Vector2(0.5f, 0.5f);
            vi++;
            for (int r = 1; r <= rings; r++)
            {
                float t = r / (float)rings;
                float rad = radius * t;
                float y = height * Mathf.Cos(t * Mathf.PI * 0.5f);
                y = Mathf.Max(0f, y);
                for (int s = 0; s < vertsPerRing; s++)
                {
                    float a = (s / (float)vertsPerRing) * Mathf.PI * 2f;
                    float x = Mathf.Cos(a) * rad;
                    float z = Mathf.Sin(a) * rad;
                    float n = Mathf.PerlinNoise(x * 0.08f + 2.1f, z * 0.08f + 4.7f);
                    verts[vi] = new Vector3(x, y * (0.85f + n * 0.3f), z);
                    norms[vi] = Vector3.up;
                    uvs[vi] = new Vector2(x / radius * 0.5f + 0.5f, z / radius * 0.5f + 0.5f);
                    vi++;
                }
            }

            var tris = new System.Collections.Generic.List<int>();
            for (int s = 0; s < vertsPerRing; s++)
            {
                int a = 1 + s;
                int b = 1 + (s + 1) % vertsPerRing;
                tris.Add(0); tris.Add(b); tris.Add(a);
            }
            for (int r = 0; r < rings - 1; r++)
            {
                int ring0 = 1 + r * vertsPerRing;
                int ring1 = 1 + (r + 1) * vertsPerRing;
                for (int s = 0; s < vertsPerRing; s++)
                {
                    int s1 = (s + 1) % vertsPerRing;
                    int i0 = ring0 + s;
                    int i1 = ring0 + s1;
                    int i2 = ring1 + s;
                    int i3 = ring1 + s1;
                    tris.Add(i0); tris.Add(i1); tris.Add(i3);
                    tris.Add(i0); tris.Add(i3); tris.Add(i2);
                }
            }

            var mesh = new Mesh { name = "HillMesh" };
            mesh.vertices = verts;
            mesh.triangles = tris.ToArray();
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void CreateBerm(Transform parent, string name, Vector3 pos, float yawDeg, float length, float height)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            for (int i = 0; i < 3; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Layer" + i;
                cube.transform.SetParent(root.transform, false);
                float h = height * (1f - i * 0.22f);
                float w = 2.4f + i * 0.8f;
                cube.transform.localScale = new Vector3(w, h, length - i * 1.5f);
                cube.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
                var r = cube.GetComponent<Renderer>();
                if (r) r.sharedMaterial = IwMaterials.Dirt(new Color(0.46f - i * 0.03f, 0.38f, 0.27f));
            }
        }

        static void BuildProps(Transform parent, float size)
        {
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (35f + (i % 3) * 12f);
                CreateSandbags(parent, $"Sandbags_{i}", p, i * 25f);
            }

            CreateRuin(parent, "Ruin_A", new Vector3(42f, 0f, -18f), 35f);
            CreateRuin(parent, "Ruin_B", new Vector3(-38f, 0f, 26f), -20f);
            CreateRuin(parent, "Ruin_C", new Vector3(15f, 0f, 48f), 70f);
            CreateRuin(parent, "Ruin_D", new Vector3(-55f, 0f, -40f), 10f);
        }

        static void CreateSandbags(Transform parent, string name, Vector3 pos, float yaw)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Color bag = new Color(0.52f, 0.46f, 0.32f);
            for (int row = 0; row < 2; row++)
            for (int col = 0; col < 4; col++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                c.name = $"Bag_{row}_{col}";
                c.transform.SetParent(root.transform, false);
                c.transform.localScale = new Vector3(0.9f, 0.35f, 0.45f);
                c.transform.localPosition = new Vector3((col - 1.5f) * 0.85f + row * 0.2f, 0.18f + row * 0.35f, row * 0.15f);
                Object.Destroy(c.GetComponent<Collider>());
                var r = c.GetComponent<Renderer>();
                if (r) r.sharedMaterial = IwMaterials.Dirt(bag * (0.92f + (col % 2) * 0.08f));
            }
            var cover = root.AddComponent<BoxCollider>();
            cover.size = new Vector3(3.6f, 0.8f, 1.2f);
            cover.center = new Vector3(0f, 0.4f, 0f);
        }

        static void CreateRuin(Transform parent, string name, Vector3 pos, float yaw)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Color concrete = new Color(0.55f, 0.52f, 0.46f);
            Color dark = new Color(0.32f, 0.30f, 0.26f);

            void Wall(string n, Vector3 lp, Vector3 sc, float ry = 0f)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                w.name = n;
                w.transform.SetParent(root.transform, false);
                w.transform.localPosition = lp;
                w.transform.localScale = sc;
                w.transform.localRotation = Quaternion.Euler(0f, ry, 0f);
                var r = w.GetComponent<Renderer>();
                if (r) r.sharedMaterial = IwMaterials.Metal(concrete);
            }

            Wall("WallA", new Vector3(0f, 2.2f, 0f), new Vector3(8f, 4.4f, 0.6f));
            Wall("WallB", new Vector3(3.5f, 1.6f, 2.5f), new Vector3(0.55f, 3.2f, 5f), 12f);
            Wall("Rubble", new Vector3(-1.5f, 0.5f, 1.8f), new Vector3(3.2f, 1f, 2.4f), -25f);
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.name = "Beam";
            beam.transform.SetParent(root.transform, false);
            beam.transform.localPosition = new Vector3(0.5f, 3.8f, 1.2f);
            beam.transform.localScale = new Vector3(0.35f, 0.35f, 6f);
            beam.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var br = beam.GetComponent<Renderer>();
            if (br) br.sharedMaterial = IwMaterials.Metal(dark);
        }

        static void BuildLighting()
        {
            var existing = Object.FindObjectsOfType<Light>();
            Light sun = null;
            Light fill = null;
            foreach (var l in existing)
            {
                if (l.type == LightType.Directional)
                {
                    if (sun == null) sun = l;
                    else if (fill == null && l.name.Contains("Fill")) fill = l;
                }
            }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.name = "Sun";
            sun.color = new Color(1f, 0.94f, 0.82f);
            sun.intensity = 1.45f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.transform.rotation = Quaternion.Euler(46f, -40f, 0f);

            if (fill == null)
            {
                fill = new GameObject("FillLight").AddComponent<Light>();
                fill.type = LightType.Directional;
            }
            fill.name = "FillLight";
            fill.color = new Color(0.55f, 0.65f, 0.85f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(25f, 140f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.44f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.24f, 0.20f, 0.15f);
            RenderSettings.ambientIntensity = 1.15f;
        }

        static void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.72f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 260f;

            // Procedural skybox if available; else solid sky clear
            var sky = IwMaterials.TrySkybox();
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                foreach (var cam in Object.FindObjectsOfType<Camera>())
                {
                    cam.clearFlags = CameraClearFlags.Skybox;
                    cam.farClipPlane = 450f;
                    cam.backgroundColor = new Color(0.45f, 0.58f, 0.75f);
                }
            }
            else
            {
                foreach (var cam in Object.FindObjectsOfType<Camera>())
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.45f, 0.58f, 0.75f);
                    cam.farClipPlane = 450f;
                }
            }
        }
    }

    /// <summary>Shared runtime materials — Built-in Standard / Mobile / Diffuse only (no URP).</summary>
    public static class IwMaterials
    {
        static Shader _builtinLit;
        static Shader _unlit;
        static Shader _sky;
        static Texture2D _dirtTex;
        static Texture2D _noiseTex;
        static bool _searched;

        static void Ensure()
        {
            if (_searched) return;
            _searched = true;
            // Built-in RP only — never prefer URP Lit (magenta without URP asset).
            _builtinLit = Shader.Find("Standard")
                          ?? Shader.Find("Mobile/Diffuse")
                          ?? Shader.Find("Diffuse")
                          ?? Shader.Find("Legacy Shaders/Diffuse");
            _unlit = Shader.Find("Unlit/Color")
                     ?? Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Diffuse");
            _sky = Shader.Find("Skybox/Procedural")
                   ?? Shader.Find("Mobile/Skybox");
        }

        static Texture2D DirtTexture()
        {
            if (_dirtTex != null) return _dirtTex;
            const int n = 64;
            _dirtTex = new Texture2D(n, n, TextureFormat.RGB24, true);
            _dirtTex.name = "IW_DirtProc";
            _dirtTex.wrapMode = TextureWrapMode.Repeat;
            _dirtTex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float p = Mathf.PerlinNoise(x * 0.18f, y * 0.18f);
                float p2 = Mathf.PerlinNoise(x * 0.55f + 9f, y * 0.55f + 3f);
                float v = 0.55f + p * 0.28f + p2 * 0.12f;
                float r = v * 0.95f;
                float g = v * 0.82f;
                float b = v * 0.58f;
                _dirtTex.SetPixel(x, y, new Color(r, g, b));
            }
            _dirtTex.Apply(true, true);
            return _dirtTex;
        }

        static Texture2D NoiseTexture()
        {
            if (_noiseTex != null) return _noiseTex;
            const int n = 32;
            _noiseTex = new Texture2D(n, n, TextureFormat.RGB24, false);
            _noiseTex.name = "IW_MetalNoise";
            _noiseTex.wrapMode = TextureWrapMode.Repeat;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float p = Mathf.PerlinNoise(x * 0.35f + 1.7f, y * 0.35f + 4.2f);
                float v = 0.7f + p * 0.3f;
                _noiseTex.SetPixel(x, y, new Color(v, v, v));
            }
            _noiseTex.Apply(false, true);
            return _noiseTex;
        }

        public static Material Dirt(Color c)
        {
            Ensure();
            Material m;
            if (_builtinLit != null)
            {
                m = new Material(_builtinLit);
                SetColor(m, c);
                var tex = DirtTexture();
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                SetMetallicSmooth(m, 0f, 0.12f);
            }
            else
            {
                m = new Material(_unlit) { color = c };
            }
            return m;
        }

        public static Material Metal(Color c)
        {
            Ensure();
            Material m;
            if (_builtinLit != null)
            {
                m = new Material(_builtinLit);
                SetColor(m, c);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", NoiseTexture());
                SetMetallicSmooth(m, 0.45f, 0.38f);
            }
            else
            {
                m = new Material(_unlit) { color = c };
            }
            return m;
        }

        public static Material Paint(Color c, float metallic = 0.25f, float smooth = 0.32f)
        {
            Ensure();
            Material m;
            if (_builtinLit != null)
            {
                m = new Material(_builtinLit);
                SetColor(m, c);
                SetMetallicSmooth(m, metallic, smooth);
            }
            else
            {
                m = new Material(_unlit) { color = c };
            }
            return m;
        }

        public static Material Unlit(Color c)
        {
            Ensure();
            var m = new Material(_unlit != null ? _unlit : Shader.Find("Sprites/Default"));
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else m.color = c;
            return m;
        }

        public static Material TrySkybox()
        {
            Ensure();
            if (_sky == null) return null;
            var m = new Material(_sky);
            if (m.HasProperty("_SunSize")) m.SetFloat("_SunSize", 0.04f);
            if (m.HasProperty("_AtmosphereThickness")) m.SetFloat("_AtmosphereThickness", 1.15f);
            if (m.HasProperty("_SkyTint")) m.SetColor("_SkyTint", new Color(0.45f, 0.55f, 0.7f));
            if (m.HasProperty("_GroundColor")) m.SetColor("_GroundColor", new Color(0.35f, 0.3f, 0.22f));
            if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", 1.25f);
            return m;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
        }

        static void SetMetallicSmooth(Material m, float metallic, float smooth)
        {
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        }
    }
}
