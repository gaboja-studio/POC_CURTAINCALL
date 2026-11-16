using System;
using System.Collections.Generic;
using System.Linq;
using CurtainCall.CharacterArt;
using CurtainCall.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>Project donor surface coordinates into the existing mannequin UV atlas. Draw meshes are temporary 2D bake data only.</summary>
    public static class PursuitGraftProjection
    {
        static readonly string[][] Names = {
            new[] { "UpperArm_L", "Forearm_L", "Hand_L" }, new[] { "UpperArm_R", "Forearm_R", "Hand_R" },
            new[] { "Thigh_L", "Shin_L", "Foot_L" }, new[] { "Thigh_R", "Shin_R", "Foot_R" } };
        static readonly HumanBodyBones[][] Human = {
            new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand },
            new[] { HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand },
            new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot },
            new[] { HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot } };
        const int Resolution = 1024;

        sealed class Body
        {
            public Vector3[] Vertices, Anchor = new Vector3[3], Along = new Vector3[3], Side = new Vector3[3], Front = new Vector3[3];
            public Vector2[] UV;
            public BoneWeight[] Weights;
            public int[] Segments, Used;
            public float[] Length = new float[3], Width = new float[3], Depth = new float[3];
            public Body(SkinnedMeshRenderer skin, Transform[] chain, Transform root, int[] used)
            {
                var mesh = skin.sharedMesh; Vertices = mesh.vertices; UV = mesh.uv; Weights = mesh.boneWeights; Used = used;
                Segments = skin.bones.Select(b => Segment(b, chain)).ToArray();
                for (int i = 0; i < 3; i++) {
                    int index = Array.IndexOf(skin.bones, chain[i]);
                    if (index < 0) throw new InvalidOperationException("Missing graft bind pose: " + chain[i].name);
                    Anchor[i] = mesh.bindposes[index].inverse.MultiplyPoint3x4(Vector3.zero);
                }
                var forward = skin.transform.InverseTransformDirection(root.forward);
                for (int i = 0; i < 3; i++) {
                    int first = Mathf.Min(i, 1);
                    Along[i] = (Anchor[first + 1] - Anchor[first]).normalized;
                    Side[i] = Vector3.Cross(Along[i], forward).normalized;
                    if (Side[i].sqrMagnitude < .1f) Side[i] = Vector3.Cross(Along[i], Vector3.up).normalized;
                    Front[i] = Vector3.Cross(Side[i], Along[i]).normalized;
                    Length[i] = Vector3.Distance(Anchor[first], Anchor[first + 1]);
                    foreach (int v in used) if (Dominant(v) == i) {
                        var delta = Vertices[v] - Anchor[i];
                        Width[i] = Mathf.Max(Width[i], Mathf.Abs(Vector3.Dot(delta, Side[i])));
                        Depth[i] = Mathf.Max(Depth[i], Mathf.Abs(Vector3.Dot(delta, Front[i])));
                    }
                    if (i == 2) Length[i] = used.Where(v => Dominant(v) == i).Select(v => Mathf.Abs(Vector3.Dot(Vertices[v] - Anchor[i], Along[i]))).DefaultIfEmpty(Length[i]).Max();
                    Width[i] = Mathf.Max(Width[i], Length[i] * .08f); Depth[i] = Mathf.Max(Depth[i], Length[i] * .08f);
                }
            }
            static int Segment(Transform bone, Transform[] chain)
            {
                if (bone != null) for (int i = 2; i >= 0; i--) if (bone == chain[i] || bone.IsChildOf(chain[i])) return i;
                return -1;
            }
            public int Dominant(int vertex)
            {
                var w = Weights[vertex]; int best = w.boneIndex0; float weight = w.weight0;
                if (w.weight1 > weight) { best = w.boneIndex1; weight = w.weight1; }
                if (w.weight2 > weight) { best = w.boneIndex2; weight = w.weight2; }
                if (w.weight3 > weight) best = w.boneIndex3;
                return Segments[best];
            }
            public float Influence(int vertex, int start)
            {
                var w = Weights[vertex];
                return (Segments[w.boneIndex0] >= start ? w.weight0 : 0) + (Segments[w.boneIndex1] >= start ? w.weight1 : 0)
                    + (Segments[w.boneIndex2] >= start ? w.weight2 : 0) + (Segments[w.boneIndex3] >= start ? w.weight3 : 0);
            }
            public Vector3 Encode(Vector3 p, int segment)
            {
                var v = p - Anchor[segment];
                return new Vector3(Vector3.Dot(v, Side[segment]) / Width[segment], Vector3.Dot(v, Along[segment]) / Mathf.Max(.00001f, Length[segment]), Vector3.Dot(v, Front[segment]) / Depth[segment]);
            }
        }

        struct Face { public int A, B, C, Material; }
        sealed class Donor
        {
            sealed class Node { public Bounds Bounds; public Node Left, Right; public Face[] Faces; }
            public Body Body;
            public List<Face>[] Faces = { new(), new(), new() };
            public Vector3[][] Coordinates;
            readonly Node[] trees = new Node[3];
            public Donor(Body body, SkinnedMeshRenderer skin)
            {
                Body = body; Coordinates = Enumerable.Range(0, 3).Select(s => body.Vertices.Select(v => body.Encode(v, s)).ToArray()).ToArray();
                for (int sub = 0; sub < skin.sharedMesh.subMeshCount; sub++) {
                    if (sub >= skin.sharedMaterials.Length || skin.sharedMaterials[sub].name.Contains("CutCap")) continue;
                    var t = skin.sharedMesh.GetTriangles(sub);
                    for (int i = 0; i < t.Length; i += 3) {
                        var face = new Face { A = t[i], B = t[i + 1], C = t[i + 2], Material = sub };
                        for (int segment = 0; segment < 3; segment++)
                            if (body.Dominant(face.A) == segment || body.Dominant(face.B) == segment || body.Dominant(face.C) == segment) Faces[segment].Add(face);
                    }
                }
                for (int segment = 0; segment < 3; segment++) trees[segment] = Build(Faces[segment].ToArray(), Coordinates[segment]);
            }
            static Node Build(Face[] faces, Vector3[] coordinates)
            {
                if (faces.Length == 0) return null;
                var node = new Node { Bounds = new Bounds(coordinates[faces[0].A], Vector3.zero) };
                foreach (var face in faces) { node.Bounds.Encapsulate(coordinates[face.A]); node.Bounds.Encapsulate(coordinates[face.B]); node.Bounds.Encapsulate(coordinates[face.C]); }
                if (faces.Length <= 8) { node.Faces = faces; return node; }
                var size = node.Bounds.size; int axis = size.x > size.y && size.x > size.z ? 0 : size.y > size.z ? 1 : 2;
                Array.Sort(faces, (a, b) => (coordinates[a.A][axis] + coordinates[a.B][axis] + coordinates[a.C][axis]).CompareTo(coordinates[b.A][axis] + coordinates[b.B][axis] + coordinates[b.C][axis]));
                node.Left = Build(faces.Take(faces.Length / 2).ToArray(), coordinates); node.Right = Build(faces.Skip(faces.Length / 2).ToArray(), coordinates);
                return node;
            }
            public Face Nearest(Vector3 point, int segment)
            {
                float distance = float.PositiveInfinity; Face face = default;
                Search(trees[segment], point, Coordinates[segment], ref distance, ref face);
                return face;
            }
            static void Search(Node node, Vector3 point, Vector3[] coordinates, ref float best, ref Face selected)
            {
                if (node == null || node.Bounds.SqrDistance(point) > best) return;
                if (node.Faces != null) {
                    foreach (var face in node.Faces) {
                        var a = coordinates[face.A]; var b = coordinates[face.B]; var c = coordinates[face.C];
                        var bary = Closest(point, a, b, c);
                        float distance = (point - (a * bary.x + b * bary.y + c * bary.z)).sqrMagnitude;
                        if (distance >= best) continue;
                        best = distance; selected = face;
                    }
                } else {
                    bool leftFirst = node.Left.Bounds.SqrDistance(point) < node.Right.Bounds.SqrDistance(point);
                    Search(leftFirst ? node.Left : node.Right, point, coordinates, ref best, ref selected);
                    Search(leftFirst ? node.Right : node.Left, point, coordinates, ref best, ref selected);
                }
            }
            public Vector2 Project(Vector3 point, int segment, Face face)
            {
                var coordinates = Coordinates[segment];
                var bary = Closest(point, coordinates[face.A], coordinates[face.B], coordinates[face.C]);
                return Body.UV[face.A] * bary.x + Body.UV[face.B] * bary.y + Body.UV[face.C] * bary.z;
            }
        }

        public static (RenderTexture color, RenderTexture surface) Bake(SkinnedMeshRenderer target, Transform root, PursuitLimbGraft[] choices, PlayerAppearanceCatalog catalog)
        {
            var shader = Resources.Load<Shader>("PursuitGraft/PursuitGraftBake");
            if (shader == null || !shader.isSupported) throw new InvalidOperationException("Graft surface baker shader unavailable");
            var color = MakeTexture("Pursuit graft color"); var surface = MakeTexture("Pursuit graft stitches");
            var previous = RenderTexture.active; var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var coverage = RenderTexture.GetTemporary(Resolution, Resolution, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            try {
                foreach (var rt in new[] { color, surface, coverage }) { RenderTexture.active = rt; GL.Clear(false, true, Color.clear); }
                foreach (var choice in choices) {
                    // Read prefab bind poses directly. No player object is instantiated or attached.
                    var prefab = catalog.PrefabFor(choice.DonorModel, choice.DonorOutfit);
                    var model = prefab.GetComponentInChildren<HumanoidModel>(true);
                    int limb = choice.Source switch { PursuitLimb.LeftLeg => 0, PursuitLimb.RightLeg => 1, PursuitLimb.LeftArm => 2, _ => 3 };
                    if (model == null || model.Limbs == null || model.Limbs.Length <= limb) continue;
                    var donorSkin = model.Limbs[limb];
                    var donorChain = Human[(int)choice.Source].Select(model.Animator.GetBoneTransform).ToArray();
                    var targetChain = Names[(int)choice.Target].Select(n => target.bones.FirstOrDefault(b => b.name == n)).ToArray();
                    if (targetChain.Any(b => b == null) || donorChain.Any(b => b == null)) continue;
                    var targetBody = new Body(target, targetChain, root, target.sharedMesh.triangles.Distinct().ToArray());
                    var donorBody = new Body(donorSkin, donorChain, prefab.transform, donorSkin.sharedMesh.triangles.Distinct().ToArray());
                    var donor = new Donor(donorBody, donorSkin);
                    var mesh = ProjectionMesh(target, root, targetBody, donor, choice, donorSkin.sharedMaterials.Length);
                    try {
                        material.SetColor("_VariantColor", catalog.outfitColors[choice.DonorOutfit]);
                        material.SetFloat("_VariantPattern", catalog.PatternFor(choice.DonorOutfit)); material.SetFloat("_Model", choice.DonorModel);
                        for (int sub = 0; sub < mesh.subMeshCount; sub++) {
                            if (mesh.GetIndexCount(sub) == 0) continue;
                            var source = donorSkin.sharedMaterials[sub];
                            material.SetTexture("_MainTex", source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : Texture2D.whiteTexture);
                            material.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white);
                            for (int pass = 0; pass < 2; pass++) {
                                RenderTexture.active = pass == 0 ? color : surface; material.SetFloat("_OutputSurface", pass);
                                material.SetPass(0); Graphics.DrawMeshNow(mesh, Matrix4x4.identity, sub);
                            }
                            RenderTexture.active = coverage; material.SetPass(2); Graphics.DrawMeshNow(mesh, Matrix4x4.identity, sub);
                        }
                    } finally { PursuitSuturedLimbs.Dispose(mesh); }
                }
                material.SetTexture("_CoverageMap", coverage);
                foreach (var rt in new[] { color, surface }) {
                    var pad = RenderTexture.GetTemporary(Resolution, Resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    try { Graphics.Blit(rt, pad, material, 1); Graphics.Blit(pad, rt, material, 1); }
                    finally { RenderTexture.ReleaseTemporary(pad); }
                    rt.GenerateMips();
                }
                return (color, surface);
            } catch {
                color.Release(); surface.Release(); PursuitSuturedLimbs.Dispose(color); PursuitSuturedLimbs.Dispose(surface); throw;
            } finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(coverage); PursuitSuturedLimbs.Dispose(material); }
        }

        static RenderTexture MakeTexture(string name)
        {
            var rt = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) {
                name = name, hideFlags = HideFlags.HideAndDontSave, useMipMap = true, autoGenerateMips = false, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            rt.Create(); return rt;
        }

        struct Sample
        {
            public Vector3 Point;
            public Vector2 UV, Seam;
            public float Segment;
            public static Sample Mid(Sample a, Sample b) => new Sample { Point = (a.Point + b.Point) * .5f, UV = (a.UV + b.UV) * .5f, Seam = (a.Seam + b.Seam) * .5f, Segment = (a.Segment + b.Segment) * .5f };
        }

        static Mesh ProjectionMesh(SkinnedMeshRenderer skin, Transform root, Body target, Donor source, PursuitLimbGraft choice, int materials)
        {
            int start = choice.Seam == PursuitSeam.ElbowOrKnee ? 1 : 0;
            float meters = skin.transform.lossyScale.magnitude / root.lossyScale.magnitude;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var seam = new List<Vector2>();
            var triangles = Enumerable.Range(0, materials).Select(_ => new List<int>()).ToArray();
            var indices = skin.sharedMesh.triangles;
            // Subdivide bake data only. A source face per small patch prevents interpolation across
            // unrelated donor UV islands (which otherwise paints atlas borders across hands).
            void Draw(Sample a, Sample b, Sample c, int depth)
            {
                if (Mathf.Max(a.Seam.x, Mathf.Max(b.Seam.x, c.Seam.x)) < -.014f) return;
                if (depth > 0) {
                    var ab = Sample.Mid(a, b); var bc = Sample.Mid(b, c); var ca = Sample.Mid(c, a);
                    Draw(a, ab, ca, depth - 1); Draw(ab, b, bc, depth - 1); Draw(ca, bc, c, depth - 1); Draw(ab, bc, ca, depth - 1);
                    return;
                }
                int segment = Mathf.Clamp(Mathf.RoundToInt((a.Segment + b.Segment + c.Segment) / 3), start, 2);
                var face = source.Nearest(target.Encode((a.Point + b.Point + c.Point) / 3, segment), segment);
                foreach (var sample in new[] { a, b, c }) {
                    triangles[face.Material].Add(vertices.Count);
                    vertices.Add(new Vector3(sample.UV.x, sample.UV.y, 0));
                    uv.Add(source.Project(target.Encode(sample.Point, segment), segment, face)); seam.Add(sample.Seam);
                }
            }
            for (int i = 0; i < indices.Length; i += 3) {
                int a = indices[i], b = indices[i + 1], c = indices[i + 2];
                if (Mathf.Max(target.Influence(a, start), Mathf.Max(target.Influence(b, start), target.Influence(c, start))) < .05f) continue;
                var corners = new[] { a, b, c }; var samples = new Sample[3];
                for (int j = 0; j < 3; j++) {
                    int v = corners[j], segment = Mathf.Clamp(target.Dominant(v), start, 2);
                    var delta = target.Vertices[v] - target.Anchor[start];
                    float cut = Vector3.Dot(delta, target.Along[start]) * meters - .025f;
                    float signed = Mathf.Min(cut, (target.Influence(v, start) - .5f) * .12f);
                    float angle = Mathf.Atan2(Vector3.Dot(delta, target.Side[start]), Vector3.Dot(delta, target.Front[start]));
                    samples[j] = new Sample { Point = target.Vertices[v], UV = target.UV[v], Seam = new Vector2(signed, angle), Segment = segment };
                }
                float circumferenceRadius = (target.Width[start] + target.Depth[start]) * .5f * meters;
                float firstAngle = samples[0].Seam.y;
                for (int j = 0; j < 3; j++) {
                    float angle = samples[j].Seam.y;
                    while (angle - firstAngle > Mathf.PI) angle -= Mathf.PI * 2;
                    while (angle - firstAngle < -Mathf.PI) angle += Mathf.PI * 2;
                    samples[j].Seam.y = angle * circumferenceRadius;
                }
                float edge = Mathf.Max(Vector3.Distance(samples[0].Point, samples[1].Point), Mathf.Max(Vector3.Distance(samples[1].Point, samples[2].Point), Vector3.Distance(samples[2].Point, samples[0].Point))) * meters;
                Draw(samples[0], samples[1], samples[2], Mathf.Clamp(Mathf.CeilToInt(Mathf.Log(Mathf.Max(1, edge / .012f), 2)), 0, 5));
            }
            var mesh = new Mesh { name = "Temporary graft UV projection", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, seam); mesh.subMeshCount = materials;
            for (int sub = 0; sub < materials; sub++) mesh.SetTriangles(triangles[sub], sub);
            mesh.RecalculateBounds(); return mesh;
        }

        // Closest point on a triangle, returned as barycentric coordinates (handles its edge regions).
        static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            var ab = b - a; var ac = c - a; var ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return new Vector3(1, 0, 0);
            var bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return new Vector3(0, 1, 0);
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) { float v = d1 / Mathf.Max(1e-12f, d1 - d3); return new Vector3(1 - v, v, 0); }
            var cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return new Vector3(0, 0, 1);
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) { float w = d2 / Mathf.Max(1e-12f, d2 - d6); return new Vector3(1 - w, 0, w); }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) { float w = (d4 - d3) / Mathf.Max(1e-12f, d4 - d3 + d5 - d6); return new Vector3(0, 1 - w, w); }
            float sum = va + vb + vc;
            return Mathf.Abs(sum) < 1e-12f ? new Vector3(1, 0, 0) : new Vector3(va, vb, vc) / sum;
        }
    }
}
