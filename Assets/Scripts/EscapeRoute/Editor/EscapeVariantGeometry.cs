using System;
using System.Linq;
using CurtainCall.Player;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        static Material vWall, vFloor, vSteel, vDepth, vFixture, vBulb, vPaint;

        static Transform VGroup(string name, Vector3 position, float yaw = 0)
        {
            var g = new GameObject(name);
            g.transform.SetParent(root, false);
            g.transform.localPosition = position;
            g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return g.transform;
        }

        static GameObject VBlock(Transform p, string name, Vector3 centre, Vector3 size, Material material, bool collision = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = name; g.transform.SetParent(p, false);
            g.transform.localPosition = centre; g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static GameObject VAsset(Transform p, string path, Vector3 bottom, Vector3 size, Material material,
            Quaternion? rotation = null, bool collision = false)
        {
            // All chunk rotations are quarter turns. Convert desired local AABB to world AABB.
            Vector3 x = p.TransformVector(Vector3.right * size.x);
            Vector3 y = p.TransformVector(Vector3.up * size.y);
            Vector3 z = p.TransformVector(Vector3.forward * size.z);
            var worldSize = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            var g = Real(p, path, p.TransformPoint(bottom), worldSize, material, p.rotation * (rotation ?? Quaternion.identity));
            if (!collision)
                foreach (var c in g.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            else if (g.GetComponentsInChildren<Collider>().Length == 0) g.AddComponent<BoxCollider>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
            return g;
        }

        static void VFloor(Transform p, float x1, float x2, float z1, float z2, float y)
        {
            for (float x = x1; x < x2 - .001f; x += 5)
            for (float z = z1; z < z2 - .001f; z += 5)
            {
                float w = Mathf.Min(5, x2 - x), d = Mathf.Min(5, z2 - z);
                VAsset(p, Factory + "SM_ConcreteFloor.prefab", new Vector3(x + w / 2, y - .3f, z + d / 2),
                    new Vector3(w, .3f, d), vFloor, null, true);
            }
        }

        static void VPlatform(Transform p, float x1, float x2, float z1, float z2, float y)
        {
            VFloor(p, x1, x2, z1, z2, y);
            float top = y - .32f;
            VBlock(p, "플랫폼 하부 기초", new Vector3((x1 + x2) / 2, (top - 60) / 2, (z1 + z2) / 2),
                new Vector3(x2 - x1, top + 60, z2 - z1), vDepth);
        }

        static void VWallX(Transform p, float z, float x1, float x2, float height)
        {
            for (float x = x1; x < x2 - .001f; x += 6)
            for (float y = 0; y < height - .001f; y += 8)
            {
                float w = Mathf.Min(6, x2 - x), h = Mathf.Min(8, height - y);
                VAsset(p, Factory + "SM_ConcreteFloor.prefab", new Vector3(x + w / 2, y, z),
                    new Vector3(w, h, .6f), vWall, Quaternion.Euler(90, 0, 0), true);
            }
        }

        static void VWallZ(Transform p, float x, float z1, float z2, float height)
        {
            for (float z = z1; z < z2 - .001f; z += 6)
            for (float y = 0; y < height - .001f; y += 8)
            {
                float w = Mathf.Min(6, z2 - z), h = Mathf.Min(8, height - y);
                VAsset(p, Factory + "SM_ConcreteFloor.prefab", new Vector3(x, y, z + w / 2),
                    new Vector3(.6f, h, w), vWall, Quaternion.Euler(0, 0, 90), true);
            }
        }

        static void VCeiling(Transform p, float x1, float x2, float z1, float z2, float height)
        {
            var ceiling = new GameObject("Ceiling").transform; ceiling.SetParent(p, false);
            for (float x = x1; x < x2 - .001f; x += 10)
            for (float z = z1; z < z2 - .001f; z += 10)
            {
                float w = Mathf.Min(10, x2 - x), d = Mathf.Min(10, z2 - z);
                VAsset(ceiling, Factory + "SM_ConcreteFloor.prefab", new Vector3(x + w / 2, height, z + d / 2),
                    new Vector3(w, .5f, d), vWall);
            }
        }

        static void VRail(Transform p, Vector3 a, Vector3 b)
        {
            float length = Vector3.Distance(a, b);
            int count = Mathf.CeilToInt(length / 2.5f);
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            for (int i = 0; i < count; i++)
                VAsset(p, Industrial + "SM_Scaffolding_Railing_200m.prefab", Vector3.Lerp(a, b, (i + .5f) / count),
                    alongX ? new Vector3(length / count, 1.1f, .12f) : new Vector3(.12f, 1.1f, length / count),
                    vSteel, Quaternion.Euler(0, alongX ? 90 : 0, 0));
        }

        static void VCable(Transform p, Vector3 a, Vector3 b, float diameter, string name)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name;
            g.transform.SetParent(p, false); g.transform.localPosition = (a + b) / 2;
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            g.transform.localScale = new Vector3(diameter, Vector3.Distance(a, b) / 2, diameter);
            g.GetComponent<Renderer>().sharedMaterial = vSteel;
            UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        }

        static void VLight(Transform p, string name, Vector3 at, Vector3 target, float power, float range = 22, float angle = 65)
        {
            var g = new GameObject(name, typeof(Light)); g.transform.SetParent(p, false);
            g.transform.localPosition = at;
            g.transform.localRotation = Quaternion.LookRotation(target - at);
            var l = g.GetComponent<Light>(); l.type = LightType.Spot; l.intensity = power;
            l.range = range; l.spotAngle = angle; l.innerSpotAngle = angle * .55f;
            l.color = new Color(.94f, .97f, 1); l.shadows = LightShadows.Soft;
            l.shadowBias = .04f; l.shadowNormalBias = .12f;
        }

        static void VLabel(Transform p, string text, Vector3 at)
        {
            font = GetFont(); Sign(p, text, p.TransformPoint(at), p.eulerAngles.y, 9);
        }

        static void VBay(Transform p, float x, float z, float height, string id)
        {
            VAsset(p, Factory + "SM_ConcreteColumn.prefab", new Vector3(x, -30, z), new Vector3(1.8f, height + 30, 1.8f), vWall);
            VAsset(p, Industrial + "SM_Pipe_Medium_01.prefab", new Vector3(x + 1.3f, -18, z),
                new Vector3(.3f, height + 14, .3f), vSteel, Quaternion.Euler(0, 0, 90));
            VBlock(p, "작업등", new Vector3(x + .7f, 3.8f, z + .7f), new Vector3(.15f, .12f, .5f), vBulb, false);
            VLight(p, "설비 작업등 " + id, new Vector3(x + .9f, 4.2f, z + 1), new Vector3(x + 4, 0, z + 2), 125, 16);
            ContextSign(p, "점검 표식 " + id, "P-" + id, new Vector3(x + 1, 2, z + .95f), 0, 1.2f);
        }

        static void VService(Transform p, Vector3 at, string id)
        {
            VAsset(p, Industrial + "SM_ToolCart.prefab", at, new Vector3(.7f, .95f, 1.2f), vFixture);
            VAsset(p, Factory + "SM_Cablespool.prefab", at + new Vector3(2, 0, 1.1f), new Vector3(1.1f, 1.25f, 1.1f), vSteel);
            for (int i = 0; i < 14; i++)
                VBlock(p, "배수 그레이팅", at + new Vector3(1, .012f, -3 + i * .4f), new Vector3(.35f, .02f, .065f), vSteel, false);
            ContextSign(p, "설비 구역 " + id, "SERVICE " + id, at + new Vector3(.3f, 2.2f, 1), 90, 1.5f);
        }

        static void VPerson(Transform p, Vector3 feet)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_ThirdParty/Task062/PlayerPresentation/Humanoid.prefab");
            var g = (GameObject)PrefabUtility.InstantiatePrefab(source, p); g.name = "사람 크기 기준 1.73m";
            g.transform.localPosition = feet;
            var human = g.GetComponent<HumanoidModel>(); human.Original.enabled = true;
            foreach (var r in human.Bodies.Concat(human.Limbs)) r.enabled = false;
            human.Animator.Rebind(); human.Animator.Play("Idle", 0, 0); human.Animator.Update(0); human.Animator.enabled = false;
            var bounds = human.Original.bounds;
            g.transform.localScale *= 1.73f / bounds.size.y;
            bounds = human.Original.bounds; g.transform.position += Vector3.up * (p.TransformPoint(feet).y - bounds.min.y);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
            foreach (var body in g.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            foreach (var component in g.GetComponentsInChildren<Component>(true))
                if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }
}
