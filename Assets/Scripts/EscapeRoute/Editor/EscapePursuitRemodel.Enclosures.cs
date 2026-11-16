using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapePursuitRemodel
    {
        static Bounds Room(Vector3 from, Vector3 to, bool jump, float headroom = 6.6f)
        {
            var b = new Bounds(from + Vector3.up * 3.275f, new Vector3(4.8f, 6.65f, 4.8f));
            float arc = jump ? EscapeUnderstageChase.ServiceJumpArc(from, to) : 0;
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f;
                var p = Vector3.Lerp(from, to, t) + Vector3.up * (arc * t * (1 - t));
                b.Encapsulate(new Bounds(p + Vector3.up * ((headroom - .05f) * .5f), new Vector3(4.8f, headroom + .05f, 4.8f)));
            }
            return b;
        }

        static void BuildEnclosures(EscapePursuerRoute route, List<EscapeUnderstageChase.PhysicalEncounter> encounters)
        {
            var rooms = new List<Bounds>();
            for (int i = 2; i <= 70; i++) rooms.Add(Room(Local(route.Points[i-1].feet), Local(route.Points[i].feet), route.Points[i].jump));
            foreach (var e in encounters)
            {
                var origin = Local(route.Points[e.routePoint - 1].feet);
                rooms.Add(Room(origin, Local(e.ingress), false));
                rooms.Add(Room(Local(e.ingress), Local(e.staging), false));
                rooms.Add(Room(Local(e.staging), Local(e.approach), false));
                rooms.Add(Room(Local(e.approach), Local(e.exit), false, 14));
            }
            // Merge collinear rooms first: no coincident exterior faces or seams along straight tunnels.
            MergeRooms(rooms);
            foreach (var room in rooms)
            {
                var outer = room; outer.Expand(new Vector3(1.6f, 1.6f, 1.6f));
                var min = outer.min; min.y = -90; outer.SetMinMax(min, outer.max);
                Box("Concealed shell", outer.center, outer.size);
            }
            foreach (var room in rooms) EscapePursuerConcreteRoutes.OpenPhysicalVolume(frame, module, structure, room);
            // The first transition uses the existing public hall floor and its breakable wall.
            EscapePursuerConcreteRoutes.OpenPhysicalPassage(frame, module, structure, route.Points[0].feet.position, route.Points[1].feet.position);
            foreach (var e in encounters)
            {
                var a = Local(e.exit); var b = Local(e.landing);
                // A single rectangular aperture avoids staircase-shaped CSG cuts around a jump arc.
                EscapePursuerConcreteRoutes.OpenPhysicalVolume(frame, module, structure, Room(a, b, true));
            }
        }

        static void MergeRooms(List<Bounds> rooms)
        {
            bool again;
            do
            {
                again = false;
                for (int i = 0; i < rooms.Count && !again; i++)
                for (int j = i + 1; j < rooms.Count && !again; j++)
                {
                    var a = rooms[i]; var b = rooms[j];
                    bool contained = a.Contains(b.min) && a.Contains(b.max) || b.Contains(a.min) && b.Contains(a.max);
                    int aligned = 0;
                    for (int axis = 0; axis < 3; axis++)
                        if (Mathf.Abs(a.min[axis]-b.min[axis]) < .01f && Mathf.Abs(a.max[axis]-b.max[axis]) < .01f) aligned++;
                    if (!contained && (!a.Intersects(b) || aligned < 2)) continue;
                    a.Encapsulate(b); rooms[i] = a; rooms.RemoveAt(j); again = true;
                }
            } while (again);
        }

        static void BuildEncounterFloors(List<EscapeUnderstageChase.PhysicalEncounter> encounters)
        {
            var route = module.GetComponent<EscapePursuerRoute>();
            foreach (var e in encounters)
            {
                var nodes = new[] { Local(route.Points[e.routePoint-1].feet), Local(e.ingress), Local(e.staging), Local(e.approach), Local(e.exit) };
                Gallery(nodes.Distinct().Select(p => p - Vector3.up * .02f).ToList(), 100 + e.gate);
            }
            UnityEditor.AssetDatabase.SaveAssetIfDirty(UnityEditor.AssetDatabase.LoadMainAssetAtPath(GalleryAsset));
        }

        static void CloseVestibuleSightlines(EscapePursuerRoute route, List<EscapeUnderstageChase.PhysicalEncounter> encounters)
        {
            foreach (var e in encounters)
            {
                Vector3 a = Local(e.ingress);
                Vector3 normal = (Local(e.staging)-a).normalized;
                Vector3 side = (Local(e.approach)-Local(e.staging)).normalized;
                float offset = Vector3.Distance(Local(e.approach),Local(e.staging));
                void Wall(string name, float across, float depth, float width, float thickness)
                {
                    var t = Box(name + " " + e.gate, a + side*across + normal*depth + Vector3.up*7.45f, new Vector3(width,15.1f,.4f));
                    t.localRotation = Quaternion.LookRotation(normal);
                    t.localScale = new Vector3(width,15.1f,thickness);
                }
                // Restore a precise facade after the broad jump clearance cut. Only the breakable bay opens.
                float leftEdge = e.gate == 1 ? -1.6f : -3.2f;
                float leftClearance = LeftClearance(e.gate);
                float rightClearance = RightClearance(e.gate);
                float leftWidth = offset - leftClearance - leftEdge;
                Wall("Opaque breach facade left", leftEdge + leftWidth*.5f, 8, leftWidth,.8f);
                Wall("Opaque breach facade right", offset + rightClearance + .4f,8,.8f,.8f);
                // A second opaque plane hides the service corridor even after the panel is broken.
                float baffleWidth = offset + 3.2f - 2.2f;
                Wall("Backstage sightline baffle",2.2f + baffleWidth*.5f,3.1f,baffleWidth,.2f);
                if (e.gate == 3)
                {
                    // The incoming conveyor tunnel needs a second return to hide its oblique sightline.
                    var shield = Box("Incoming conveyor sightline return", a + normal*2.5f - side*11.9f + Vector3.up*7.45f,
                        new Vector3(19.2f,15.1f,.6f));
                    shield.localRotation = Quaternion.LookRotation(normal);
                }
            }
        }
        static float LeftClearance(int gate) => gate == 1 ? 3.2f : gate == 4 ? 4.6f : gate == 7 ? 5.15f : gate == 10 ? 4.8f : 2.4f;
        static float RightClearance(int gate) => gate == 3 ? 4.4f : 2.4f;
        static Vector3 Local(Transform t) => frame.InverseTransformPoint(t.position);
    }
}
