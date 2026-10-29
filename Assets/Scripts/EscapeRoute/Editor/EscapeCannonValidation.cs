using System;
using System.Linq;
using System.Text;
using CurtainCall.HumanCannon;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.EscapeRoute.EditorTools
{
    public static partial class EscapeBackgroundBuilder
    {
        public static string ValidateA03Cannon()
        {
            string path = SceneManager.GetActiveScene().path;
            if (path != ScenePath || Application.isPlaying)
                throw new InvalidOperationException("현재 A 씬의 편집 모드가 필요합니다.");
            Physics.SyncTransforms();
            int passed = 0, failed = 0; var report = new StringBuilder();
            Action<string, bool> check = (name, ok) => { if (ok) passed++; else { failed++; report.AppendLine("FAIL " + name); } };
            var systems = UnityEngine.Object.FindObjectsByType<HumanCannonSystem>(FindObjectsSortMode.None);
            check("one cannon system", systems.Length == 1);
            if (systems.Length != 1) return report.ToString();
            var system = systems[0]; check("A03 station registered", system.StationCount >= 1);
            if (system.StationCount < 1) return report.ToString();
            var station = system.Station(0);
            check("station and prefab", station != null && PrefabUtility.GetCorrespondingObjectFromSource(station) != null);
            if (station == null) return report.ToString();
            check("eastward muzzle", Vector3.Dot(station.Muzzle.forward, Vector3.right) > .999f);
            var anchor = GameObject.Find(TrickSetup).transform.Find("03_CannonToIsland/Landing_Feet");
            var zones = UnityEngine.Object.FindObjectsByType<HumanCannonLandingZone>(FindObjectsSortMode.None);
            var zone = zones.FirstOrDefault(z => z.SupportsLanding(anchor.position));
            check("safe landing at authored platform", zone != null);
            check("void not safe", !zones.Any(z => z.SupportsLanding(new Vector3(38, -6, 75))));
            check("platform underside not safe", !zones.Any(z => z.SupportsLanding(anchor.position + Vector3.down)));
            // Sweep a project-sized body along the same integration as the pilot, without moving any player.
            float radius = GameSettings.Base.Character.CapsuleRadius;
            float height = GameSettings.Base.Character.CapsuleHeight;
            var position = station.Muzzle.position; var velocity = station.LaunchVelocity;
            Collider firstHit = null; Vector3 hitPoint = default;
            const float dt = 1f / 120;
            for (int i = 0; i < 240; i++)
            {
                Vector3 step = BallisticMotion.Step(ref velocity, Vector3.zero, GameSettings.Base.Character.Gravity, dt);
                if (Physics.CapsuleCast(position + Vector3.up * radius, position + Vector3.up * (height - radius),
                    radius, step.normalized, out var hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
                { firstHit = hit.collider; hitPoint = hit.point; break; }
                position += step;
            }
            check("neutral flight reaches safe platform first" + (firstHit != null ? " (" + firstHit.name + ")" : " (no collision)"),
                firstHit != null && zone != null && firstHit == zone.GetComponent<BoxCollider>());
            if (firstHit != null) report.AppendLine("First trajectory contact: " + hitPoint);
            var guide = GameObject.Find(RootName).transform.Find(VerticalGroup + "/02_Route_0_to_minus6_to_plus2/대포 설치 자리");
            check("old cannon block inactive", guide != null && !guide.gameObject.activeSelf);
            if (GameObject.Find(OriginalReview) != null)
            {
                check("preview flight disabled", !UnityEngine.Object.FindObjectsByType<EscapeRouteFlyCamera>(FindObjectsSortMode.None).Any(f => f.enabled));
                var spawns = UnityEngine.Object.FindObjectsByType<CurtainCall.Network.PlayerSync.PlayerSpawnPoints>(FindObjectsSortMode.None);
                check("one spawn roster", spawns.Length == 1);
                if (spawns.Length == 1)
                {
                    var points = new SerializedObject(spawns[0]).FindProperty("points");
                    check("4 spawn slots", points.arraySize == 4);
                    for (int i = 0; i < points.arraySize; i++)
                    {
                        var t = points.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                        check("spawn floor " + i, t != null && Physics.Raycast(t.position + Vector3.up * .1f, Vector3.down, .3f));
                    }
                }
                check("camera brain", Camera.main != null && Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>() != null);
                check("network camera", UnityEngine.Object.FindObjectsByType<CurtainCall.Network.PlayerSync.LocalPlayerCamera>(FindObjectsSortMode.None).Length == 1);
                check("automatic solo development", UnityEngine.Object.FindObjectsByType<EscapeDevelopmentSession>(FindObjectsSortMode.None).Length == 1
                    && UnityEngine.Object.FindObjectsByType<CurtainCall.Network.Session.NetworkSessionTestUI>(FindObjectsSortMode.None).Length == 0);
            }
            check("missing scripts", SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)) == 0);
            report.AppendLine("Passed=" + passed + "; Failed=" + failed + ". Static body sweep only; no player inputs, live shot or network traversal were automated.");
            return report.ToString();
        }
    }
}
