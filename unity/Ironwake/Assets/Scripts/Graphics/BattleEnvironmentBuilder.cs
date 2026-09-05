using UnityEngine;

namespace Ironwake.Graphics
{
    /// <summary>
    /// Runtime battlefield: large dirt ground, hills/berms, sandbag/ruin props,
    /// directional sun, ambient, fog, simple sky gradient feel.
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
            float scale = size / 10f; // Plane is 10x10
            ground.transform.localScale = new Vector3(scale, 1f, scale);
            var r = ground.GetComponent<Renderer>();
            if (r)
            {
                r.sharedMaterial = IwMaterials.Dirt(new Color(0.42f, 0.36f, 0.26f));
                // Grid overlay via second thin plane with darker lines look
            }

            // Grid / dirt lanes — thin darker quads
            int lanes = 8;
            for (int i = -lanes; i <= lanes; i++)
            {
                if (i == 0) continue;
                float t = i / (float)lanes * size * 0.45f;
                MakeStrip(parent, $"LaneX_{i}", new Vector3(t, 0.02f, 0f), new Vector3(0.35f, 0.02f, size * 0.9f),
                    new Color(0.32f, 0.28f, 0.2f, 1f));
                MakeStrip(parent, $"LaneZ_{i}", new Vector3(0f, 0.02f, t), new Vector3(size * 0.9f, 0.02f, 0.35f),
                    new Color(0.3f, 0.26f, 0.19f, 1f));
            }
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
            if (r) r.sharedMaterial = IwMaterials.Unlit(col);
        }

        static void BuildHills(Transform parent, float size)
        {
            // Displaced mesh hill near center-north
            CreateDisplacedHill(parent, "Hill_N", new Vector3(0f, 0f, size * 0.28f), 28f, 7f, 24);
            CreateDisplacedHill(parent, "Hill_SW", new Vector3(-size * 0.22f, 0f, -size * 0.18f), 22f, 5.5f, 20);
            CreateDisplacedHill(parent, "Hill_E", new Vector3(size * 0.3f, 0f, 10f), 18f, 4.2f, 18);

            // Stacked berms (earthen walls)
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
            mr.sharedMaterial = IwMaterials.Dirt(new Color(0.38f, 0.33f, 0.24f));
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
                    // Mild noise for organic look
                    float n = Mathf.PerlinNoise(x * 0.08f + 2.1f, z * 0.08f + 4.7f);
                    verts[vi] = new Vector3(x, y * (0.85f + n * 0.3f), z);
                    norms[vi] = Vector3.up;
                    uvs[vi] = new Vector2(x / radius * 0.5f + 0.5f, z / radius * 0.5f + 0.5f);
                    vi++;
                }
            }

            var tris = new System.Collections.Generic.List<int>();
            // Cap to first ring
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
                if (r) r.sharedMaterial = IwMaterials.Dirt(new Color(0.4f - i * 0.03f, 0.34f, 0.25f));
            }
        }

        static void BuildProps(Transform parent, float size)
        {
            // Sandbag clusters
            for (int i = 0; i < 12; i++)
            {
                float ang = i * 30f * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * (35f + (i % 3) * 12f);
                CreateSandbags(parent, $"Sandbags_{i}", p, i * 25f);
            }

            // Ruins from combined primitives
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
            Color bag = new Color(0.45f, 0.4f, 0.28f);
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
                if (r) r.sharedMaterial = IwMaterials.Unlit(bag * (0.9f + (col % 2) * 0.08f));
            }
            // Keep one collider for cover
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
            Color concrete = new Color(0.48f, 0.46f, 0.42f);
            Color dark = new Color(0.28f, 0.27f, 0.24f);

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
            foreach (var l in existing)
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.name = "Sun";
            sun.color = new Color(1f, 0.92f, 0.78f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.45f, 0.55f, 0.65f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.38f, 0.32f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.16f);
            RenderSettings.ambientIntensity = 1.05f;
        }

        static void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.55f, 0.58f, 0.52f);
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 280f;
            // Skybox-less gradient feel via camera clear color (set by tuner / cameras)
            foreach (var cam in Object.FindObjectsOfType<Camera>())
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.52f, 0.62f, 0.72f);
                cam.farClipPlane = 450f;
            }
        }
    }

    /// <summary>Shared runtime materials — prefers IW_SimpleLitTriplanar when present.</summary>
    public static class IwMaterials
    {
        static Shader _triplanar;
        static Shader _urpLit;
        static Shader _unlit;
        static bool _searched;

        static void Ensure()
        {
            if (_searched) return;
            _searched = true;
            _triplanar = Shader.Find("Ironwake/IW_SimpleLitTriplanar");
            _urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("URP/Lit");
            _unlit = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Diffuse");
        }

        public static Material Dirt(Color c)
        {
            Ensure();
            Material m;
            if (_triplanar != null)
            {
                m = new Material(_triplanar);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                if (m.HasProperty("_Tile")) m.SetFloat("_Tile", 0.12f);
            }
            else if (_urpLit != null)
            {
                m = new Material(_urpLit);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                else if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            }
            else
            {
                m = new Material(_unlit);
                m.color = c;
            }
            return m;
        }

        public static Material Metal(Color c)
        {
            Ensure();
            Material m;
            if (_urpLit != null)
            {
                m = new Material(_urpLit);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.55f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            }
            else if (_triplanar != null)
            {
                m = new Material(_triplanar);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
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
    }
}
