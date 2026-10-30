using System;
using System.Collections.Generic;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeExitSaw
    {
        [SerializeField] EscapeCuttableCable[] cables = Array.Empty<EscapeCuttableCable>();
        [SerializeField] Transform muzzle;
        [SerializeField] GameObject projectileVisual;
        [SerializeField, Min(.1f)] float shotInterval = 2f;
        [SerializeField, Min(.1f)] float projectileSpeed = 18f;
        [SerializeField, Min(.1f)] float projectileLifetime = 2.5f;
        [SerializeField, Min(.01f)] float projectileRadius = .55f;
        [SerializeField, Min(.01f)] float projectileHalfThickness = .12f;
        [SerializeField] AudioSource launchAudio;
        [SerializeField] ParticleSystem launchFlash;
        [SerializeField] Transform recoilVisual;
        [SerializeField, Min(0)] float recoilDistance = .08f;
        const int MaxProjectiles = 8;
        sealed class Shot
        {
            public uint Id, Epoch;
            public Vector3 Position, Direction;
            public float Age;
        }
        readonly List<Shot> shots = new(MaxProjectiles);
        readonly GameObject[] projectilePool = new GameObject[MaxProjectiles];
        readonly Vector3[] cutPoints = new Vector3[32];
        readonly RaycastHit[] shotHits = new RaycastHit[64];
        readonly Collider[] shotOverlaps = new Collider[64];
        uint cutMask, shotSerial, shootingEpoch, lastPresentedShot, courseEpoch;
        float shotWait, recoilTime;
        bool shotArmed;
        Vector3 recoilRest;
        public bool ProjectileMode => cables != null && cables.Length > 0;
        public int RequiredCableCount => BitCount(RequiredMask);
        public int CutCableCount => BitCount(cutMask & RequiredMask);
        public int ActiveProjectileCount => shots.Count;
        public float SecondsUntilShot => Mathf.Max(0, shotWait);
        uint ValidCableMask
        {
            get
            {
                uint mask = 0;
                for (int i = 0; cables != null && i < cables.Length && i < 32; i++) if (cables[i] != null) mask |= 1u << i;
                return mask;
            }
        }
        uint RequiredMask
        {
            get
            {
                uint mask = 0;
                for (int i = 0; cables != null && i < cables.Length && i < 32; i++)
                    if (cables[i] != null && cables[i].RequiredForExit) mask |= 1u << i;
                return mask;
            }
        }
        static int BitCount(uint mask) { int count = 0; while (mask != 0) { mask &= mask - 1; count++; } return count; }
        void InitializeProjectilePresentation() { if (recoilVisual != null) recoilRest = recoilVisual.localPosition; }
        void ClearShots()
        {
            shots.Clear(); shotArmed = false; shotWait = Mathf.Max(.1f, shotInterval); recoilTime = 0;
            for (int i = 0; i < projectilePool.Length; i++) if (projectilePool[i] != null) projectilePool[i].SetActive(false);
            if (recoilVisual != null) recoilVisual.localPosition = recoilRest;
            if (launchAudio != null) launchAudio.Stop();
            if (launchFlash != null) launchFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void ResetProjectileCourse()
        {
            ClearShots(); cutMask = shotSerial = shootingEpoch = lastPresentedShot = 0;
            Array.Clear(cutPoints, 0, cutPoints.Length);
            PresentCables(false);
        }
        void PresentCables(bool feedback)
        {
            for (int i = 0; cables != null && i < cables.Length && i < 32; i++)
                if (cables[i] != null)
                {
                    bool cut = (cutMask & (1u << i)) != 0;
                    if (cut) cables[i].SetCut(true, cutPoints[i], feedback && !cables[i].IsCut);
                    else if (cables[i].IsCut) cables[i].SetCut(false);
                }
        }
        void HostProjectileStep(float deltaTime, NetworkPlayer carrier)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0) return;
            uint beforeMask = cutMask;
            int beforeCount = shots.Count;
            StepProjectiles(deltaTime, true);
            bool eligible = !carryBlocked && ownership.IsOccupied && Grounded(carrier)
                && Vector3.Distance(Handle.position, carrier.transform.position + Vector3.up * carryHandleHeight) < .3f;
            bool fired = AdvanceShotClock(deltaTime, eligible);
            if (fired) SpawnProjectile();
            if (cutting || beforeMask != cutMask || beforeCount != shots.Count || fired)
            {
                cutting = false; stateRevision++;
                Publish(); nextPublish = Time.unscaledTimeAsDouble + .1;
            }
            else if (Time.unscaledTimeAsDouble >= nextPublish)
            { nextPublish = Time.unscaledTimeAsDouble + .1; Publish(); }
        }
        bool AdvanceShotClock(float deltaTime, bool eligible)
        {
            if (!eligible || !float.IsFinite(deltaTime) || deltaTime < 0)
            { shotArmed = false; shotWait = Mathf.Max(.1f, shotInterval); return false; }
            if (!shotArmed || shootingEpoch != ownership.Revision)
            {
                shotArmed = true; shootingEpoch = ownership.Revision; shotWait = Mathf.Max(.1f, shotInterval);
                return false;
            }
            shotWait = Mathf.Max(0, shotWait - deltaTime);
            if (shotWait > 0 || shots.Count >= MaxProjectiles) return false;
            // 느린 프레임 뒤에도 한 발만 발사하고 다음 간격은 새로 센다.
            shotWait = Mathf.Max(.1f, shotInterval);
            return true;
        }
        void SpawnProjectile()
        {
            if (shots.Count >= MaxProjectiles) return;
            var origin = muzzle != null ? muzzle : blade != null ? blade : CarryRoot;
            Vector3 direction = Vector3.ProjectOnPlane(CarryRoot.forward, Vector3.up).normalized;
            if (!Finite(origin.position) || direction.sqrMagnitude < .99f) return;
            shots.Add(new Shot { Id = ++shotSerial, Epoch = ownership.Revision, Position = origin.position, Direction = direction });
            lastPresentedShot = shotSerial;
            EmitLaunch();
        }
        void StepProjectiles(float deltaTime, bool host)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0) return;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i];
                if (shot.Epoch != ownership.Revision || Carrier == CannonSeatState.Nobody)
                { shots.RemoveAt(i); continue; }
                float step = Mathf.Min(deltaTime, Mathf.Max(0, projectileLifetime - shot.Age));
                Vector3 from = shot.Position;
                float distance = projectileSpeed * step;
                bool blocked = host && ObstructionDistance(from, shot.Direction, distance, out distance);
                Vector3 to = from + shot.Direction * distance;
                if (host && (!blocked || distance > .0001f)) CutAlong(from, to, Vector3.up);
                shot.Position = to; shot.Age += step;
                if (blocked || shot.Age >= projectileLifetime) shots.RemoveAt(i);
            }
        }
        bool ObstructionDistance(Vector3 from, Vector3 direction, float wanted, out float allowed)
        {
            allowed = wanted;
            var orientation = Quaternion.LookRotation(direction, Vector3.up);
            var half = new Vector3(projectileRadius, projectileHalfThickness, projectileRadius);
            int overlapsCount = Physics.OverlapBoxNonAlloc(from, half, shotOverlaps, orientation, ~0, QueryTriggerInteraction.Ignore);
            if (overlapsCount == shotOverlaps.Length) { allowed = 0; return true; }
            for (int i = 0; i < overlapsCount; i++) if (ShotObstacle(shotOverlaps[i])) { allowed = 0; return true; }
            if (wanted <= 0) return false;
            int count = Physics.BoxCastNonAlloc(from, half, direction, shotHits, orientation, wanted, ~0, QueryTriggerInteraction.Ignore);
            if (count == shotHits.Length) { allowed = 0; return true; }
            bool blocked = false;
            for (int i = 0; i < count; i++)
                if (ShotObstacle(shotHits[i].collider) && shotHits[i].distance <= allowed)
                { allowed = Mathf.Max(0, shotHits[i].distance - .002f); blocked = true; }
            return blocked;
        }
        bool ShotObstacle(Collider collider) => collider != null && !collider.transform.IsChildOf(CarryRoot)
            && collider.GetComponentInParent<NetworkPlayer>() == null && collider.GetComponentInParent<EscapeCuttableCable>() == null;
        void CutAlong(Vector3 from, Vector3 to, Vector3 normal)
        {
            for (int i = 0; cables != null && i < cables.Length && i < 32; i++)
            {
                var cable = cables[i];
                uint bit = 1u << i;
                if (cable == null || !cable.isActiveAndEnabled || (cutMask & bit) != 0) continue;
                if (!cable.TryIntersectDisk(from, to, normal, projectileRadius, projectileHalfThickness, out _, out var hit)) continue;
                cutMask |= bit; cutPoints[i] = hit; cable.SetCut(true, hit, true);
                ShareCut(cable, hit);
            }
        }
        // Every launcher owns its carrier/projectiles; cable cuts belong to the shared exit.
        void ShareCut(EscapeCuttableCable cable, Vector3 point)
        {
            foreach (var device in devices)
            {
                if (device == null || device == this || !device.ProjectileMode) continue;
                int index = Array.IndexOf(device.cables, cable);
                if (index < 0 || index >= 32 || (device.cutMask & (1u << index)) != 0) continue;
                device.cutMask |= 1u << index; device.cutPoints[index] = point;
                device.Changed();
            }
        }
        void EmitLaunch()
        {
            if (launchAudio != null && launchAudio.clip != null) launchAudio.PlayOneShot(launchAudio.clip);
            if (launchFlash != null) { launchFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); launchFlash.Play(); }
            recoilTime = .15f;
        }
        void PresentProjectiles(float deltaTime)
        {
            for (int i = 0; i < projectilePool.Length; i++)
            {
                bool show = Ready && ProjectileMode && i < shots.Count;
                if (show && projectilePool[i] == null && projectileVisual != null && Application.isPlaying)
                {
                    var visual = Instantiate(projectileVisual);
                    visual.transform.localScale = Vector3.one;
                    visual.name = "__ExitSawProjectile_" + i; visual.hideFlags = HideFlags.DontSave;
                    foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    projectilePool[i] = visual;
                }
                var item = projectilePool[i];
                if (item == null) continue;
                item.SetActive(show);
                if (show)
                {
                    var shot = shots[i];
                    // 수평 원판: 이동은 날 평면 안, 회전축과 실제 절단 두께축은 위쪽.
                    item.transform.SetPositionAndRotation(shot.Position,
                        Quaternion.LookRotation(shot.Direction, Vector3.up) * Quaternion.AngleAxis(shot.Age * 1100, Vector3.up));
                }
            }
            if (recoilVisual != null)
            {
                recoilTime = Mathf.Max(0, recoilTime - Mathf.Max(0, deltaTime));
                recoilVisual.localPosition = recoilRest - Vector3.forward * (recoilDistance * recoilTime / .15f);
            }
        }
        void DisposeProjectiles()
        {
            ClearShots();
            for (int i = 0; i < projectilePool.Length; i++)
                if (projectilePool[i] != null)
                {
                    if (Application.isPlaying) Destroy(projectilePool[i]); else DestroyImmediate(projectilePool[i]);
                    projectilePool[i] = null;
                }
            if (launchAudio != null) launchAudio.Stop();
            if (launchFlash != null) launchFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        bool ValidProjectileSnapshot(uint epoch, ulong carrier, uint mask, Vector3[] points, Shot[] incoming, float wait)
        {
            if (points == null || points.Length != 32 || incoming == null || incoming.Length > MaxProjectiles
                || (mask & ~ValidCableMask) != 0 || !float.IsFinite(wait) || wait < 0 || wait > Mathf.Max(.1f, shotInterval) + .001f
                || !ProjectileMode && (mask != 0 || incoming.Length != 0)
                || carrier == CannonSeatState.Nobody && incoming.Length != 0) return false;
            for (int i = 0; i < 32; i++) if ((mask & (1u << i)) != 0 && !Finite(points[i])) return false;
            for (int i = 0; i < incoming.Length; i++)
            {
                var shot = incoming[i];
                if (shot == null || shot.Id == 0 || shot.Epoch != epoch || !Finite(shot.Position) || !Finite(shot.Direction)
                    || Mathf.Abs(shot.Direction.y) > .001f || Mathf.Abs(shot.Direction.sqrMagnitude - 1) > .01f
                    || !float.IsFinite(shot.Age) || shot.Age < 0 || shot.Age >= projectileLifetime) return false;
                for (int j = 0; j < i; j++) if (incoming[j].Id == shot.Id) return false;
            }
            return true;
        }
        bool SameShots(Shot[] incoming)
        {
            if (incoming.Length != shots.Count) return false;
            for (int i = 0; i < incoming.Length; i++)
                if (incoming[i].Id != shots[i].Id || incoming[i].Epoch != shots[i].Epoch || incoming[i].Direction != shots[i].Direction) return false;
            return true;
        }
        bool SameCutPoints(uint mask, Vector3[] points)
        {
            for (int i = 0; i < 32; i++)
                if ((cutMask & mask & (1u << i)) != 0 && points[i] != cutPoints[i]) return false;
            return true;
        }
    }
}
