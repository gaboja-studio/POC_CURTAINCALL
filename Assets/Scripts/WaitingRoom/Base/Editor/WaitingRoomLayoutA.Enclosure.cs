using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CurtainCall.WaitingRoom.Editor
{
    public static partial class WaitingRoomLayoutA
    {
        // Sky showed through the stage approach (no roof above the source vault) and above the expert lift car.
        // Close those openings with real geometry, then wrap the layout in a dark shell so a follow camera that
        // swings behind an outer wall sees darkness instead of the skybox.
        public static string SealExteriorOpenings()
        {
            var (scene, target) = Begin(); var original = Snapshot(source); int undo = StartUndo("Seal exterior openings");
            try
            {
                Remove(target, "ExteriorEnclosure"); var g = Group(target, "ExteriorEnclosure", Origin);
                int pieces = SealStageApproach(target, g) + EncloseLiftShaft(target, g) + CapInfillSlot(target, g) + DarkOuterShell(target, g);
                Physics.SyncTransforms(); AssertUnchanged(original);
                Undo.CollapseUndoOperations(undo); EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
                return "Exterior openings sealed; pieces=" + pieces;
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }

        // The lobby opening is square up to the portal header (9.2m) but the source vault is round, so its two upper
        // corners looked through the roofless approach to the sky. Fill the spandrels on the lobby face and roof the run.
        static int SealStageApproach(Transform target, Transform g)
        {
            var stage = target.Find("FacilityConnections/StageProcessionalRoute"); if (stage == null) return 0;
            var vault = stage.Find("OriginalVaultApproach/Vault").GetComponent<MeshFilter>();
            var vb = vault.GetComponent<Renderer>().bounds; float x = vb.min.x, top = vb.max.y;
            var profile = EndProfile(vault, x); var mesh = new MeshBuilder("LA_StageApproachSpandrel", 1);
            for (int k = 0; k < profile.Length - 1; k++)
            {
                var a = profile[k]; var b = profile[k + 1]; float la = a.y - .03f, lb = b.y - .03f, h = top + .05f;
                foreach (var face in new[] { x - .03f, x + .03f })
                    mesh.Quad(0, new Vector3(face, la, a.z), new Vector3(face, h, a.z), new Vector3(face, h, b.z), new Vector3(face, lb, b.z), face < x ? Vector3.left : Vector3.right, 3);
            }
            var header = target.Find("FacilityConnections/LogisticsFrontages/StagePortalHeader");
            var material = header != null ? header.GetComponent<Renderer>().sharedMaterial : Mat("LA_WallRust");
            MeshObject(g, "StageApproachSpandrel", Vector3.zero, SaveMesh(mesh.Build()), material);
            var walls = stage.Cast<Transform>().Where(t => t.name == "ApproachWall").Select(t => t.GetComponent<Renderer>().bounds).ToArray();
            float z0 = walls.Min(b => b.min.z), z1 = walls.Max(b => b.max.z), x1 = walls.Max(b => b.max.x), wallTop = walls.Max(b => b.max.y);
            Box(g, "StageApproachRoof", new Vector3((x + x1) * .5f, wallTop + .15f, (z0 + z1) * .5f), new Vector3(x1 - x + .1f, .3f, z1 - z0 + .1f), Mat("LA_WallConcrete"));
            return 2;
        }

        // The lift car is 3.7m tall inside a 13m lobby; the follow camera sat above its roof with nothing around it.
        static int EncloseLiftShaft(Transform target, Transform g)
        {
            var roof = target.Find("S08_ElevatorHall/FreightLift/CarRoof"); if (roof == null) return 0;
            // Built in the lift's own frame: the dome turns the lift to face the plaza. It rises to the lobby roof, or the dome drum.
            var lift = roof.parent; var lobbyRoof = target.Find("S01_Lobby/Overhead/Roof_East");
            float y1 = lobbyRoof != null && lobbyRoof.gameObject.activeInHierarchy ? lobbyRoof.GetComponent<Renderer>().bounds.max.y : DomeWallH + .3f;
            Vector3 c = roof.localPosition, s = roof.localScale; float y0 = roof.position.y - s.y * .5f, h = y1 - y0;
            float x0 = c.x - s.x * .5f - .1f, x1 = c.x + s.x * .5f + .2f, z0 = c.z - s.z * .5f - .2f, z1 = c.z + s.z * .5f + .2f;
            var m = Mat("LA_SteelPlate");
            void Panel(string name, float lx, float lz, Vector3 size, float ly) { var t = Box(g, name, Vector3.zero, size, m); t.SetPositionAndRotation(lift.TransformPoint(new Vector3(lx, 0, lz)) + Vector3.up * ly, lift.rotation); }
            Panel("LiftShaftBack", x1 - .1f, (z0 + z1) * .5f, new Vector3(.2f, h, z1 - z0), y0 + h * .5f);
            Panel("LiftShaftSide", (x0 + x1) * .5f, z0 + .1f, new Vector3(x1 - x0, h, .2f), y0 + h * .5f);
            Panel("LiftShaftSide", (x0 + x1) * .5f, z1 - .1f, new Vector3(x1 - x0, h, .2f), y0 + h * .5f);
            Panel("LiftShaftCap", (x0 + x1) * .5f, (z0 + z1) * .5f, new Vector3(x1 - x0, .3f, z1 - z0), y1 - .15f);
            return 4;
        }

        // The lobby's south infill and the logistics north infill stop at the same edge 7cm apart; the slot between them
        // opened to the outside at the lobby's south-west corner. Cap the edge without touching the retained frontages.
        static int CapInfillSlot(Transform target, Transform g)
        {
            var lobby = target.Find("FacilityConnections/LogisticsFrontages/LobbySouthInfill"); var cargo = target.Find("FacilityConnections/LogisticsFrontages/LogisticsNorthInfill");
            if (lobby == null || cargo == null || !lobby.gameObject.activeInHierarchy) return 0; // the dome replaced that lobby wall
            var a = lobby.GetComponent<Renderer>().bounds; var b = cargo.GetComponent<Renderer>().bounds;
            float z0 = Mathf.Min(a.min.z, b.min.z), z1 = Mathf.Max(a.max.z, b.max.z), h = Mathf.Max(a.max.y, b.max.y);
            Box(g, "InfillSlotCap", new Vector3(a.max.x - .05f, h * .5f, (z0 + z1) * .5f), new Vector3(.1f, h, z1 - z0), lobby.GetComponent<Renderer>().sharedMaterial);
            return 1;
        }

        // Inward-facing, unlit, shadowless box well outside every room: no collider, no light blocking.
        static int DarkOuterShell(Transform target, Transform g)
        {
            var b = new Bounds(target.position, Vector3.zero);
            foreach (var r in target.GetComponentsInChildren<Renderer>()) if (!r.transform.IsChildOf(g)) b.Encapsulate(r.bounds);
            b.Expand(new Vector3(16, 12, 16));
            var mesh = new MeshBuilder("LA_ExteriorVoid", 1); Vector3 lo = b.min, hi = b.max;
            Vector3 P(float x, float y, float z) => new(Mathf.Lerp(lo.x, hi.x, x), Mathf.Lerp(lo.y, hi.y, y), Mathf.Lerp(lo.z, hi.z, z));
            mesh.Quad(0, P(0, 0, 0), P(0, 1, 0), P(0, 1, 1), P(0, 0, 1), Vector3.right, 50);
            mesh.Quad(0, P(1, 0, 0), P(1, 1, 0), P(1, 1, 1), P(1, 0, 1), Vector3.left, 50);
            mesh.Quad(0, P(0, 0, 0), P(1, 0, 0), P(1, 1, 0), P(0, 1, 0), Vector3.forward, 50);
            mesh.Quad(0, P(0, 0, 1), P(1, 0, 1), P(1, 1, 1), P(0, 1, 1), Vector3.back, 50);
            mesh.Quad(0, P(0, 0, 0), P(1, 0, 0), P(1, 0, 1), P(0, 0, 1), Vector3.up, 50);
            mesh.Quad(0, P(0, 1, 0), P(1, 1, 0), P(1, 1, 1), P(0, 1, 1), Vector3.down, 50);
            var go = MeshObject(g, "DarkOuterShell", Vector3.zero, SaveMesh(mesh.Build()), VoidMaterial());
            var mr = go.GetComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return 1;
        }

        static Material VoidMaterial()
        {
            EnsureFolder();
            string path = $"{AssetDir}/LA_ExteriorVoid.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", Hex("0B0908"));
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
