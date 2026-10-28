using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        // ⑦ stage entrance in world space (the scaled Greybox composition is left untouched; cloth needs an unscaled parent).
        const float StageCurtainX = -6f, StageCurtainTop = 7.84f, StageCurtainZ = -13.35f, StageCurtainWidth = 13.3f;
        const float StageGoldZ = -13.86f;
        const string StageClothMeshPath = AssetDir + "/LA_StageCurtainCloth.asset";

        /// <summary>
        /// Replaces the rigid velvet folds at the stage entrance with a pleated cloth pair that sways in a draft,
        /// turns the wall behind into a golden light and adds beams, lights and falling gold dust driven by
        /// <see cref="StageCurtainReveal"/> (opens when anyone stands in the gather zone).
        /// </summary>
        public static string BuildStageCurtainReveal()
        {
            var (scene, target) = Begin(true); int undo = StartUndo("Stage entrance cloth curtain and golden reveal");
            try
            {
                var route = target.Find("S06_StageCorridor/StageProcessionalRoute") ?? throw new InvalidOperationException("Stage route missing");
                var composition = route.Find("GreyboxComposition");
                var gather = target.Find("S07_StageEntrance/Gameplay/GatherZone")?.GetComponent<StageEntranceGather>() ?? throw new InvalidOperationException("Gather zone missing");
                // The old curtain was stretched non-uniformly with its parent; cloth simulates badly under that scale.
                var oldCurtain = composition.Find("Curtain_StageEntrance");
                if (oldCurtain != null) { Undo.RecordObject(oldCurtain.gameObject, "Retire stretched curtain"); oldCurtain.gameObject.SetActive(false); }
                // Rigid fold boxes stay as the blocking volume; only their rendering is replaced by the cloth.
                foreach (var fold in composition.Find("S07_StageEntrance_Temp").Cast<Transform>().Where(t => t.name.StartsWith("CurtainFold_")))
                    if (fold.TryGetComponent(out MeshRenderer r)) { Undo.RecordObject(r, "Hide rigid fold"); r.enabled = false; }

                Remove(route, "StageCurtainReveal");
                if (AssetDatabase.LoadMainAssetAtPath(StageClothMeshPath) != null) AssetDatabase.DeleteAsset(StageClothMeshPath);
                var g = Group(route, "StageCurtainReveal", new Vector3(StageCurtainX, 0, StageCurtainZ));
                g.rotation = Quaternion.identity;

                var curtain = StageClothPair(g, StageVelvet());
                var glow = StageGoldWall(g);
                var beams = StageBeams(g);
                var lights = new[]
                {
                    StageLight(g, "GoldenFloodSpot", LightType.Spot, new Vector3(0, 6.6f, -.35f), Quaternion.LookRotation(new Vector3(0, -.55f, 1)), 160f, 24f),
                    StageLight(g, "GoldenSpill", LightType.Point, new Vector3(0, 2.6f, .6f), Quaternion.identity, 70f, 13f),
                };
                var dust = StageGoldDust(g);
                var burst = StageGoldBurst(g);

                var reveal = g.gameObject.AddComponent<StageCurtainReveal>();
                reveal.gather = gather;
                reveal.glowColor = new Color(2.2f, 1.45f, .5f, 1);
                reveal.curtain = curtain;
                reveal.lights = lights;
                reveal.glows = new[] { glow };
                reveal.beams = beams;
                reveal.dust = new[] { dust };
                reveal.bursts = new[] { burst };
                // Saved closed: the reveal script brings these up at runtime.
                foreach (var l in lights) l.enabled = false;
                foreach (var b in beams) b.enabled = false;

                Physics.SyncTransforms();
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
                return $"Stage curtain: {curtain.panels.Sum(p => p.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertexCount)} cloth vertices, {beams.Length} beams, {lights.Length} lights, dust + burst";
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        static CurtainController StageClothPair(Transform g, Material velvet)
        {
            var rail = Group(g, "StageVelvetCurtain", g.TransformPoint(new Vector3(0, StageCurtainTop, 0)));
            rail.localRotation = Quaternion.identity;
            const float gap = .03f;
            float half = (StageCurtainWidth - gap) * .5f, height = StageCurtainTop - .04f;
            var left = StageClothPanel(rail, "Velvet_Left", -StageCurtainWidth * .5f, half, height, velvet, false, 3);
            var right = StageClothPanel(rail, "Velvet_Right", gap * .5f, half, height, velvet, true, 11);
            var c = rail.gameObject.AddComponent<CurtainController>();
            c.panels = new[] { left, right };
            c.openDuration = 2.6f;
            c.initialOpen = 0;
            c.openOnStart = false;
            c.draft = 2.6f;
            c.draftSpeed = .6f;
            return c;
        }

        // Same rig as the prototype ClothCurtainBuilder (ring/hem bones, pinned rail row), with deeper, irregular
        // pleats, a finer grid and a looser hem so the fabric folds and flutters instead of reading as a corrugated sheet.
        static CurtainPanel StageClothPanel(Transform parent, string name, float x0, float width, float height, Material m, bool gatherToEnd, int seed)
        {
            int nx = Mathf.RoundToInt(width / .115f) + 1, ny = Mathf.RoundToInt(height / .27f) + 1;
            float dx = width / (nx - 1), dy = height / (ny - 1);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x0, 0, 0);
            var rings = new Transform[nx]; var hems = new Transform[nx]; var bones = new Transform[nx * 2]; var closedX = new float[nx];
            for (int i = 0; i < nx; i++)
            {
                rings[i] = new GameObject("Ring_" + i).transform; rings[i].SetParent(go.transform, false); rings[i].localPosition = new Vector3(i * dx, 0, 0);
                hems[i] = new GameObject("Hem_" + i).transform; hems[i].SetParent(go.transform, false); hems[i].localPosition = new Vector3(i * dx, -height, 0);
                bones[i] = rings[i]; bones[nx + i] = hems[i]; closedX[i] = i * dx;
            }
            // Pleat phase advances with a slowly varying period so no two folds are the same width.
            var phase = new float[nx]; float acc = 0;
            for (int i = 0; i < nx; i++) { phase[i] = acc; acc += dx * 2 * Mathf.PI / Mathf.Lerp(.42f, .66f, Mathf.PerlinNoise(seed * 1.7f, i * .09f)); }
            var verts = new Vector3[nx * ny]; var uvs = new Vector2[nx * ny]; var weights = new BoneWeight[nx * ny];
            for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                int v = j * nx + i; float down = (float)j / (ny - 1), hem = down * down * (3 - 2 * down);
                // Folds deepen slightly toward the hem, where the fabric has more slack.
                float depth = Mathf.Lerp(.12f, .17f, down);
                verts[v] = new Vector3(i * dx, -j * dy, depth * Mathf.Sin(phase[i]));
                uvs[v] = new Vector2(x0 + i * dx, -j * dy);
                weights[v] = new BoneWeight { boneIndex0 = i, weight0 = 1 - hem, boneIndex1 = nx + i, weight1 = hem };
            }
            var tris = new int[(nx - 1) * (ny - 1) * 6]; int t = 0;
            for (int j = 0; j < ny - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b; tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
            var mesh = new Mesh { name = "LA_StageCurtain_" + name, vertices = verts, uv = uvs, triangles = tris, boneWeights = weights };
            mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * go.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            if (AssetDatabase.LoadMainAssetAtPath(StageClothMeshPath) == null) AssetDatabase.CreateAsset(mesh, StageClothMeshPath);
            else AssetDatabase.AddObjectToAsset(mesh, StageClothMeshPath);

            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh; smr.bones = bones; smr.rootBone = go.transform; smr.sharedMaterial = m; smr.updateWhenOffscreen = true;
            var cloth = go.AddComponent<Cloth>();
            var k = cloth.coefficients;
            for (int v = 0; v < k.Length; v++)
            {
                float down = Mathf.Clamp01(-verts[v].y / height);
                k[v].maxDistance = verts[v].y > -.01f ? 0 : 1.15f * Mathf.Pow(down, .65f);
                k[v].collisionSphereDistance = 0;
            }
            cloth.coefficients = k;
            cloth.stretchingStiffness = .95f; cloth.bendingStiffness = .06f; cloth.damping = .1f; cloth.friction = .6f;
            cloth.useGravity = true; cloth.worldVelocityScale = .5f; cloth.worldAccelerationScale = 1;
            cloth.collisionMassScale = .2f; cloth.randomAcceleration = new Vector3(2.4f, 0, 3.4f); cloth.sleepThreshold = .001f;

            var panel = go.AddComponent<CurtainPanel>();
            panel.rings = rings; panel.hems = hems; panel.hemY = -height; panel.closedX = closedX; panel.gatherToEnd = gatherToEnd;
            // A full reveal: the rail gathers to a third and the hem is drawn up and aside.
            panel.topWidthOpen = .32f; panel.gatheredWidth = .14f; panel.hemLift = 1.4f;
            panel.SetOpen(0);
            return panel;
        }

        static Material StageVelvet()
        {
            var m = Lit("LA_StageVelvet", new Color(.4f, .028f, .045f), .3f, VelvetBaseTexture(), VelvetNormalTexture());
            m.SetFloat("_BumpScale", .9f);
            m.SetFloat("_SpecularHighlights", 1); m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            // No sky reflection indoors: it reads as a window.
            m.SetFloat("_EnvironmentReflections", 0); m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(m);
            return m;
        }

        static Renderer StageGoldWall(Transform g)
        {
            var m = StageMaterial("LA_StageGoldLight", "Universal Render Pipeline/Unlit", GoldLightTexture(), false);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            wall.name = "GoldenLightWall"; Object.DestroyImmediate(wall.GetComponent<Collider>());
            wall.transform.SetParent(g, false);
            wall.transform.localPosition = new Vector3(0, StageCurtainTop * .5f, StageGoldZ - StageCurtainZ);
            wall.transform.localRotation = Quaternion.Euler(0, 180, 0);
            wall.transform.localScale = new Vector3(StageCurtainWidth + .1f, StageCurtainTop, 1);
            var r = wall.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }

        // Additive light shafts: the four faces of a frustum from the opening to the floor in front, fading with distance.
        static Renderer[] StageBeams(Transform g)
        {
            var m = StageMaterial("LA_StageGoldBeam", "Universal Render Pipeline/Particles/Unlit", GoldBeamTexture(), true);
            float hw = StageCurtainWidth * .5f - .3f;
            Vector3[] near = { new(-hw, .04f, -.2f), new(hw, .04f, -.2f), new(hw, StageCurtainTop - .45f, -.2f), new(-hw, StageCurtainTop - .45f, -.2f) };
            Vector3[] far = { new(-hw - 1.6f, .05f, 8.5f), new(hw + 1.6f, .05f, 8.5f), new(hw + 1.2f, 2.2f, 8.5f), new(-hw - 1.2f, 2.2f, 8.5f) };
            var result = new Renderer[4];
            string[] names = { "BeamFloor", "BeamRight", "BeamTop", "BeamLeft" };
            for (int i = 0; i < 4; i++)
            {
                int n = (i + 1) % 4;
                var mesh = new Mesh { name = "LA_" + names[i], vertices = new[] { near[i], near[n], far[n], far[i] }, uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                string path = $"{AssetDir}/LA_{names[i]}.asset";
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                var go = new GameObject(names[i]); go.transform.SetParent(g, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                result[i] = r;
            }
            return result;
        }

        static Light StageLight(Transform g, string name, LightType type, Vector3 at, Quaternion rotation, float intensity, float range)
        {
            var t = new GameObject(name).transform; t.SetParent(g, false); t.localPosition = at; t.localRotation = rotation;
            var l = t.gameObject.AddComponent<Light>();
            l.type = type; l.color = new Color(1, .76f, .36f); l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
            if (type == LightType.Spot) { l.spotAngle = 120; l.innerSpotAngle = 60; }
            return l;
        }

        // Fine glitter drifting down from the top of the opening and settling on the floor in front of it.
        static ParticleSystem StageGoldDust(Transform g)
        {
            var ps = StageParticles(g, "GoldDustFall", new Vector3(0, StageCurtainTop - .5f, .45f), 3500);
            var main = ps.main;
            main.loop = true; main.duration = 5;
            main.startLifetime = new ParticleSystem.MinMaxCurve(11, 15);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.1f, .5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.04f, .09f);
            main.gravityModifier = .13f;
            var emission = ps.emission; emission.rateOverTime = 180;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(StageCurtainWidth - .8f, .3f, .7f);
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-.35f, .35f); velocity.y = new ParticleSystem.MinMaxCurve(0, 0); velocity.z = new ParticleSystem.MinMaxCurve(.5f, 1.5f);
            return ps;
        }

        // A one-off spray when the curtain parts.
        static ParticleSystem StageGoldBurst(Transform g)
        {
            var ps = StageParticles(g, "GoldRevealBurst", new Vector3(0, 3.8f, .3f), 1400);
            var main = ps.main;
            main.loop = false; main.duration = 1;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7, 10);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .11f);
            main.gravityModifier = .5f;
            var emission = ps.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 650), new ParticleSystem.Burst(.3f, 350), new ParticleSystem.Burst(.7f, 200) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 50; shape.radius = 2.4f; shape.radiusThickness = 1;
            return ps;
        }

        static ParticleSystem StageParticles(Transform g, string name, Vector3 at, int max)
        {
            var t = new GameObject(name).transform; t.SetParent(g, false); t.localPosition = at; t.localRotation = Quaternion.identity;
            var ps = t.gameObject.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false; main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, .78f, .3f), new Color(1, .95f, .68f));
            var rotation = ps.rotationOverLifetime; rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-3, 3);
            var color = ps.colorOverLifetime; color.enabled = true;
            var fade = new Gradient(); fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .04f), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
            color.color = fade;
            // Settle on the flat corridor floor and stay put. A plane is exact (world collision let some fall through) and cheap.
            var floor = g.Find("GoldDustFloorPlane") ?? new GameObject("GoldDustFloorPlane").transform;
            floor.SetParent(g, false); floor.position = new Vector3(g.position.x, .012f, g.position.z); floor.rotation = Quaternion.identity;
            var collision = ps.collision; collision.enabled = true; collision.type = ParticleSystemCollisionType.Planes; collision.SetPlane(0, floor);
            collision.dampen = .92f; collision.bounce = .06f; collision.lifetimeLoss = 0; collision.minKillSpeed = 0; collision.radiusScale = .4f;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = StageMaterial("LA_StageGoldDust", "Universal Render Pipeline/Particles/Unlit", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"), true, new Color(5f, 3.4f, 1.1f));
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.maxParticleSize = .5f;
            return ps;
        }

        static Material StageMaterial(string name, string shader, Texture2D tex, bool additive, Color? tint = null)
        {
            string path = $"{AssetDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find(shader);
            m.SetTexture("_BaseMap", tex);
            // Edit-time colour matches the closed state (glowColor × closedGlow); the reveal script overrides it per renderer.
            m.SetColor("_BaseColor", tint ?? new Color(.48f, .31f, .11f));
            if (additive)
            {
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2); m.SetFloat("_Cull", 0); m.SetFloat("_ZWrite", 0);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.One);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---------- generated textures ----------

        // Velvet pile: fine vertical fibre streaks with soft, uneven fading. Tiles once per metre.
        static Texture2D VelvetBaseTexture() => GeneratedTexture("LA_StageVelvet_Base.png", 256, false, (u, v) =>
        {
            float streak = Mathf.PerlinNoise(u * 90f, v * 2.5f) * .55f + Mathf.PerlinNoise(u * 23f, v * .8f) * .45f;
            float wear = Mathf.PerlinNoise(u * 4f + 3, v * 3f);
            float s = Mathf.Lerp(.72f, 1f, streak) * Mathf.Lerp(.86f, 1f, wear);
            return new Color(s, s * .97f, s * .97f);
        });

        static Texture2D VelvetNormalTexture()
        {
            float H(float u, float v) => Mathf.PerlinNoise(u * 90f, v * 2.5f) * .6f + Mathf.PerlinNoise(u * 260f, v * 260f) * .4f;
            return GeneratedTexture("LA_StageVelvet_Normal.png", 256, true, (u, v) =>
            {
                const float e = 1f / 256f;
                float dx = (H(u + e, v) - H(u - e, v)) * 2.2f, dy = (H(u, v + e) - H(u, v - e)) * 2.2f;
                var n = new Vector3(-dx, -dy, 1).normalized;
                return new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f);
            });
        }

        // Hot white-gold core fading to amber, with a faint sunburst so the light has depth.
        static Texture2D GoldLightTexture() => GeneratedTexture("LA_StageGoldLight.png", 512, false, (u, v) =>
        {
            float px = (u - .5f) * 1.7f, py = v - .42f, r = Mathf.Sqrt(px * px + py * py), a = Mathf.Atan2(py, px);
            float core = Mathf.Pow(Mathf.Clamp01(1 - r / .95f), 1.4f);
            float burst = .9f + .1f * Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 9)), 4) * (1 - core);
            var c = Color.Lerp(new Color(.32f, .14f, .025f), new Color(1, .93f, .74f), core) * burst;
            return new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b));
        }, TextureWrapMode.Clamp);

        // Beam: bright at the opening (v=0), fading out, broken into soft shafts across u. Additive, so black is transparent.
        static Texture2D GoldBeamTexture() => GeneratedTexture("LA_StageGoldBeam.png", 256, false, (u, v) =>
        {
            float shafts = .35f + .65f * Mathf.Pow(Mathf.PerlinNoise(u * 14f, 1.3f), 2.2f);
            float edge = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
            float s = Mathf.Pow(1 - v, 1.8f) * shafts * Mathf.Pow(edge, .5f) * .32f;
            return new Color(s, s, s);
        }, TextureWrapMode.Clamp);

        static Texture2D GeneratedTexture(string file, int size, bool normal, Func<float, float, Color> pixel, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            EnsureFolder();
            string path = $"{AssetDir}/{file}";
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = pixel((float)x / size, (float)y / size);
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.SetPixels(pixels); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = wrap; importer.anisoLevel = 4;
            if (normal) importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
