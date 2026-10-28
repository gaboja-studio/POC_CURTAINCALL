using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        // Keep the original shop pillar and service references facing the plaza. No enclosure around it.
        static void PlaceLobbyShop(Transform target, Transform dome)
        {
            var shop = target.Find("S03_Shop"); var pillar = shop.Find("Pillar");
            Undo.RecordObject(shop, "Face shop into the plaza");
            shop.rotation = Quaternion.identity;
            shop.position += new Vector3(4.5f, 5f, 68f) - pillar.position;
            Remove(dome, "ShopAlcove");
        }

        static void FitLogisticsCorridorEnds(Transform dome)
        {
            // Add infills beside the smaller corridor mouths without regenerating the retained LogisticsFrontages.
            var g = Group(dome, "LogisticsDoorReturns", dome.position);
            foreach (var v in new[] { ("Logistics11", -13.2f, -4.1f), ("Logistics12", -1.5f, 6.4f) })
            {
                var p = LobbyPassages().Single(p => p.Name == v.Item1); float l = p.O.x - p.W * .5f, r = p.O.x + p.W * .5f;
                foreach (var span in new[] { (v.Item2, l), (r, v.Item3) })
                    if (span.Item2 > span.Item1)
                        Box(g, "DoorReturn", new Vector3((span.Item1 + span.Item2) * .5f, 4.5f, 55.82f), new Vector3(span.Item2 - span.Item1, 9f, .3f), Mat("LA_WallConcrete"), true);
                Box(g, "DoorHeader", new Vector3(p.O.x, (9f + p.H) * .5f, 55.82f), new Vector3(p.W, 9f - p.H, .3f), Mat("LA_WallConcrete"), true);
            }
        }

        static void RecutPropRoomEntrance(Transform target)
        {
            var room = target.Find("S04_PropRoom"); var shell = room.Find("Shell");
            var reveals = shell.GetComponentsInChildren<Collider>().Where(c => c.name == "DoorReveal").ToArray();
            if (reveals.Length == 2 && Mathf.Abs(reveals.Average(c => c.bounds.center.z) - (Origin.z + PropDoorZ)) < .01f) return;
            var mats = shell.Find("WallSurfaces").GetComponent<Renderer>().sharedMaterials;
            // Only the generated envelope changes. Keep all furniture, handover shelf and its references.
            Undo.DestroyObjectImmediate(shell.gameObject); BuildPropShell(room); room.Find("Shell").position += PropShift;
            Physics.SyncTransforms();
            room.Find("Shell/WallSurfaces").GetComponent<Renderer>().sharedMaterials = mats;
            var factory = target.Find("FactoryFrame");
            if (factory != null)
            {
                Remove(factory, "S04_PropRoom_S04_PropRoom");
                SteelFrame(target, room, Group(factory, "S04_PropRoom_S04_PropRoom", room.position), room.Find("Shell/WallSurfaces").GetComponent<Renderer>().bounds);
            }
        }

        static void FitPassageWaitingAreas(Transform area, System.Collections.Generic.List<Passage> passages)
        {
            foreach (var p in passages)
            {
                var name = "Area_DomePassage_" + p.Name; var t = area.Find(name);
                if (t == null) { t = Group(area, name, area.position); Undo.AddComponent<BoxCollider>(t.gameObject).isTrigger = true; }
                float a = Mathf.Min(p.T0(-p.W * .5f), p.T0(p.W * .5f));
                float b = Mathf.Max(p.T1(-p.W * .5f), p.T1(p.W * .5f)); var m = p.At(0, (a + b) * .5f);
                Undo.RecordObject(t, "Fit corridor waiting area"); t.SetPositionAndRotation(new Vector3(m.x, 3f, m.y), Quaternion.LookRotation(new Vector3(p.U.x, 0, p.U.y)));
                var box = t.GetComponent<BoxCollider>(); Undo.RecordObject(box, "Fit corridor waiting area");
                box.center = Vector3.zero; box.size = new Vector3(p.W + .6f, 8f, b - a + .6f);
            }
            var practice = area.Find("Area_RoundPractice");
            if (practice == null) { practice = Group(area, "Area_RoundPractice", area.position); Undo.AddComponent<BoxCollider>(practice.gameObject).isTrigger = true; }
            FitBox(practice, PracC.x - PracR, PracC.x + PracR, PracC.y - PracR, PracC.y + PracR, -1, 18);
        }

        static void PlaceDormitoryWithFittings(Transform target, Vector3 to)
        {
            var root = target.Find("S02_Dormitory"); var delta = to - root.position;
            if (delta.sqrMagnitude < 1e-6f) return;
            var oldBounds = Bounds(root); MoveFacility(root, to);
            var frame = target.Find("FactoryFrame/S02_Dormitory_Dormitory");
            if (frame != null) MoveFacility(frame, frame.position + delta);
            var supports = target.Find("SupportFinishing");
            if (supports != null)
                foreach (Transform t in supports)
                    if (oldBounds.Contains(t.position) && t.position.y >= 0) MoveFacility(t, t.position + delta);
            // The schedule board and its camera live outside the dorm root, although mounted on its front wall.
            var gameplay = target.Find("S01_Lobby/Gameplay");
            foreach (var name in new[] { "ScheduleBoard", "ScheduleFrame", "ScheduleCloseUp" })
            { var t = gameplay.Find(name); if (t != null) MoveFacility(t, t.position + delta); }
            var area = target.Find("ExplorationTimer/WaitingRoomArea/Area_Dormitory");
            if (area != null) MoveFacility(area, area.position + delta);
        }

        static void RestoreLegacyPracticeRing(Transform room)
        {
            // Reparent the preserved originals rather than recreating them, keeping corrected ladder collision,
            // the raised 0.6m deck, rim/rail/skirt artwork, lanes and both access stairs unchanged.
            var legacy = room.Find("LegacyRing");
            if (legacy == null)
            {
                var arena = room.Find("Arena");
                legacy = Group(room, "LegacyRing", W(PracRing.x, PracRing.y));
                foreach (var name in new[] { "PracticeRing", "PracticeLanes", "CanopyAndRig", "RaisedRingAccess", "CollisionRepairs" })
                {
                    var t = arena.Find(name); if (t == null) throw new InvalidOperationException("Preserved ring part missing: " + name);
                    Undo.SetTransformParent(t, legacy, "Restore preserved practice ring");
                    Undo.RecordObject(t.gameObject, "Enable preserved ring part"); t.gameObject.SetActive(true);
                }
            }
            MoveFacility(legacy, new Vector3(PracC.x, 0, PracC.y));
            Remove(legacy, "DomeSuspension"); var ties = Group(legacy, "DomeSuspension", legacy.position);
            void Tie(Vector3 a, Vector3 b)
            {
                Prim(ties, PrimitiveType.Cylinder, "RoofTie", (a + b) * .5f, new Vector3(.035f, Vector3.Distance(a, b) * .5f, .035f), Mat("V4_Iron_Dark"))
                    .rotation = Quaternion.FromToRotation(Vector3.up, b - a);
                Box(ties, "RoofAnchor", b, new Vector3(.18f, .08f, .18f), Mat("LA_SteelPlate"));
            }
            Tie(PW(0, 0, 15.15f), PW(0, 0, PracTopH - .08f));
            foreach (var deg in new[] { 45f, 135f, 225f, 315f })
            {
                float a = deg * Mathf.Deg2Rad, r = 4.2f * .6f, h = DomeHeightAt(r, PracR, PracWallH, PracTopH) - .08f;
                Tie(PW(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 15.6f), PW(Mathf.Cos(a) * r, Mathf.Sin(a) * r, h));
            }
        }
    }
}
