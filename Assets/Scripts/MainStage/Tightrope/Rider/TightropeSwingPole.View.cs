using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeSwingPole
    {
        Transform viewRoot, crossbar, leftRod, rightRod, landingAim;
        TextMesh aimLabel;
        TextMesh sign;
        Transform entry, exit;
        TightropeSettings.CourseSettings shownShape;
        readonly List<Material> viewMaterials = new();
        Material orange, green;
        public Transform PoleVisual => crossbar;
        public Vector3 GripPoint(NetworkPlayer player)
        {
            var pole = ForPlayer(player);
            return pole.WorldFeet(pole.OwnAngle, pole.assignedLane) + Vector3.up * SwingPoleMath.GripHeight;
        }
        public string LocalHint
            => LocalPole.OwnLocalHint;
        string OwnLocalHint
        {
            get
            {
                var local = NetworkPlayer.Local;
                if (!Active || local == null) return null;
                if (flightMover != null) return "공중 WASD 조절 · 초록색 착지 표시로 이동 · 동료는 F 받기 준비";
                if (OwnHeld(local)) return OwnCanLetGo ? "지금 Space! · 공중 W로 전진 · A/D 옆 조절 · 동료는 F 받기 준비"
                    : $"W/S로 반동 키우기 · 초록색에서 Space 놓기 · 남은 잡기 {Mathf.Max(0f, physicalPole.HoldSeconds - (float)(Clock - startedAt)):0.0}초";
                float distance = course.GetDistance(local.transform.position);
                if (distance >= course.SwingGapFrom - 7f && distance < course.SwingGapFrom)
                    return "끊어진 줄 · 끝의 청록색 표시에서 F로 회전봉 잡기 · 목마는 먼저 해제";
                if (distance >= course.SwingGapTo && distance <= LandingDistance + 3f)
                    return "먼저 건넜다면 F로 동료 받기 · 줄 끝에서 한 걸음 안쪽에 서 주세요";
                return null;
            }
        }

        void UpdateViews()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            bool visible = course != null && course.HasSwingGapOnLane(assignedLane) && course.IsTraversalActive;
            if (!visible) { if (viewRoot != null) viewRoot.gameObject.SetActive(false); return; }
            if (viewRoot == null || shownShape != course.Shape) BuildViews();
            viewRoot.gameObject.SetActive(true);
            const float width = 1.4f;
            float side = course.Layout.GetLaneSide(assignedLane);
            Vector3 feet = transform.InverseTransformPoint(WorldFeet(OwnAngle, assignedLane));
            float center = course.SwingGapFrom - SwingPoleMath.EntryOffset;
            Vector3 pivot = new(side, physicalPole.HangLength + SwingPoleMath.GripHeight + .3f, center);
            Vector3 grip = feet + Vector3.up * SwingPoleMath.GripHeight;
            crossbar.localPosition = grip;
            SetRod(leftRod, pivot + Vector3.left * width * .5f, grip + Vector3.left * width * .5f);
            SetRod(rightRod, pivot + Vector3.right * width * .5f, grip + Vector3.right * width * .5f);
            crossbar.GetComponent<Renderer>().sharedMaterial = OwnCanLetGo ? green : orange;
            TightropeArtFit.Tint(crossbar, OwnCanLetGo ? Color.green : new Color(1f, .55f, .08f));
            Vector3 predicted = PredictLanding(true);
            bool aimVisible = (OwnOccupied || flightMover != null) && course.TryGetRopePoint(predicted, out int predictedLane, out float predictedDistance)
                && course.IsLaneUsable(predictedLane, predictedDistance);
            landingAim.gameObject.SetActive(aimVisible); aimLabel.gameObject.SetActive(aimVisible);
            if (aimVisible)
            {
                landingAim.position = predicted + Vector3.up * .12f;
                aimLabel.transform.position = predicted + Vector3.up * .8f;
                if (Camera.main != null) aimLabel.transform.rotation = Camera.main.transform.rotation;
            }
            entry.gameObject.SetActive(course.IsLaneUsable(assignedLane, course.SwingGapFrom - SwingPoleMath.EntryOffset));
            exit.gameObject.SetActive(course.IsLaneUsable(assignedLane, LandingDistance));
            if (Camera.main != null && sign != null) sign.transform.rotation = Camera.main.transform.rotation;
        }

        void BuildViews()
        {
            DestroyViews(); shownShape = course.Shape;
            viewRoot = new GameObject("SwingPole (runtime)").transform;
            viewRoot.SetParent(transform, false);
            var cyan = NewMaterial(new Color(.1f, .9f, 1f));
            orange = NewMaterial(new Color(1f, .55f, .08f)); green = NewMaterial(new Color(.15f, 1f, .3f));
            var frame = NewMaterial(new Color(.28f, .3f, .32f));
            const float width = 1.4f;
            float laneSide = course.Layout.GetLaneSide(assignedLane);
            float center = course.SwingGapFrom - SwingPoleMath.EntryOffset;
            float height = physicalPole.HangLength + SwingPoleMath.GripHeight + .3f;
            Box("Pivot", new Vector3(laneSide, height, center), new Vector3(width + .5f, .18f, .18f), frame);
            foreach (float side in new[] { -1f, 1f })
                Box("Frame", new Vector3(laneSide + side * (width * .5f + .3f), height * .5f, center), new Vector3(.22f, height, .22f), frame);
            crossbar = Box("RotatingBar", Vector3.zero, new Vector3(width, .14f, .14f), orange);
            leftRod = Box("LeftRod", Vector3.zero, Vector3.one, frame);
            rightRod = Box("RightRod", Vector3.zero, Vector3.one, frame);
            landingAim = Box("PredictedLanding", Vector3.zero, new Vector3(.7f, .035f, .6f), green);
            var aim = new GameObject("LandingHint"); aim.transform.SetParent(viewRoot, false);
            aimLabel = aim.AddComponent<TextMesh>(); aimLabel.text = "공중 W 유지 시 착지 예상";
            aimLabel.fontSize = 48; aimLabel.characterSize = .022f; aimLabel.anchor = TextAnchor.MiddleCenter; aimLabel.color = Color.green;
            entry = Box("Grab", new Vector3(laneSide, .09f, course.SwingGapFrom - SwingPoleMath.EntryOffset), new Vector3(.65f, .035f, 1.4f), cyan);
            exit = Box("Catch", new Vector3(laneSide, .09f, LandingDistance), new Vector3(.65f, .035f, 1.6f), green);
            var label = new GameObject("SwingInstructions"); label.transform.SetParent(viewRoot, false);
            label.transform.localPosition = new Vector3(laneSide + 1.1f, 2f, course.SwingGapFrom - 2f);
            sign = label.AddComponent<TextMesh>(); sign.text = $"{assignedLane + 1}번 줄 회전봉\nF 잡기 · W/S 반동 · Space 놓기\n공중 WASD 조절 · 동료 F 받기";
            sign.fontSize = 64; sign.characterSize = .035f; sign.anchor = TextAnchor.MiddleCenter; sign.color = Color.cyan;
        }

        Material NewMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color, hideFlags = HideFlags.HideAndDontSave };
            viewMaterials.Add(material); return material;
        }
        Transform Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(viewRoot, false); go.transform.localPosition = position; go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = material;
            var profile = course != null ? course.ArtProfile : null;
            if (profile != null && name == "RotatingBar") TightropeArtFit.Box(go.transform, profile.barX, material.color);
            else if (profile != null && (name == "LeftRod" || name == "RightRod")) TightropeArtFit.Box(go.transform, profile.cableY, material.color);
            return go.transform;
        }
        static void SetRod(Transform rod, Vector3 a, Vector3 b)
        {
            rod.localPosition = (a + b) * .5f;
            rod.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            rod.localScale = new Vector3(.1f, Vector3.Distance(a, b), .1f);
        }
        void DestroyViews()
        {
            if (viewRoot != null) { viewRoot.gameObject.SetActive(false); Destroy(viewRoot.gameObject); }
            foreach (var material in viewMaterials) if (material != null) Destroy(material);
            viewMaterials.Clear(); viewRoot = null;
        }
    }
}
