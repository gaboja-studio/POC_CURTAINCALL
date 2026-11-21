using System;
using System.Collections.Generic;
using CurtainCall.Damage;
using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using UnityEngine;

namespace CurtainCall.EscapeRoute
{
    public sealed partial class EscapeExitSaw
    {
        [SerializeField] EscapeCuttableCable[] cables = Array.Empty<EscapeCuttableCable>();
        [Tooltip("연결된 배선이 없어도 사용 키로 톱날을 발사할 수 있습니다. 배선 대상은 별도로 연결합니다.")]
        [SerializeField] bool fireWithoutCables;
        [Tooltip("필수 배선을 모두 자르면 탐색 장부에서 열림으로 기록할 대상(대기실 문 등). 비우면 기록하지 않습니다.")]
        [SerializeField] string openTarget = "";
        [SerializeField] Transform muzzle;
        [SerializeField] GameObject projectileVisual;
        float shotInterval => CurtainCall.Settings.GameSettings.Base.CarryDevices.SawShotInterval;
        [SerializeField, Min(.1f)] float projectileSpeed = 18f;
        [Tooltip("0이면 시간/사거리 제한 없이 지형에 닿을 때 소멸한다.")]
        [SerializeField, Min(0f)] float projectileLifetime = 2.5f;
        [SerializeField, Min(.01f)] float projectileRadius = .55f;
        [SerializeField, Min(.01f)] float projectileHalfThickness = .12f;
        [SerializeField] AudioSource launchAudio;
        [SerializeField] ParticleSystem launchFlash;
        [SerializeField] Transform recoilVisual;
        [SerializeField, Min(0)] float recoilDistance = .08f;
        const int MaxProjectiles = 64;
        bool UnlimitedProjectiles => projectileLifetime <= 0;
        sealed class Shot
        {
            public uint Id, Epoch;
            public ulong Shooter;
            public bool CutAny;
            public Vector3 Position, Direction;
            public float Age;
        }
        readonly List<Shot> shots = new(MaxProjectiles);
        readonly GameObject[] projectilePool = new GameObject[MaxProjectiles];
        readonly Vector3[] cutPoints = new Vector3[32];
        readonly RaycastHit[] shotHits = new RaycastHit[64];
        readonly Collider[] shotOverlaps = new Collider[64];
        uint cutMask, shotSerial, lastPresentedShot, courseEpoch;
        bool finaleDoorReported;
        float shotWait, recoilTime;
        static readonly int RechargeAmount = Shader.PropertyToID("_DissolveAmount");
        readonly List<(Renderer renderer, Material[] materials, MaterialPropertyBlock properties)> rechargeRenderers = new();
        MaterialPropertyBlock rechargeProperties;
        public static event Action<EscapeExitSaw, ulong, string, string, Vector3, Vector3, string> ShotObserved;
        void NotifyShot(Shot shot, string state, Vector3 position, string detail)
        {
            if (ShotObserved == null) return;
            foreach (Action<EscapeExitSaw, ulong, string, string, Vector3, Vector3, string> observer in ShotObserved.GetInvocationList())
                try { observer(this, shot.Shooter, ShotKey(shot), state, position, shot.Direction, detail); }
                catch (Exception error) { Debug.LogException(error, this); }
        }

        string ShotKey(Shot shot) => courseEpoch + ":" + shot.Epoch + ":" + shot.Id;
        Vector3 recoilRest;
        public bool ProjectileMode => fireWithoutCables || cables != null && cables.Length > 0;
        public int RequiredCableCount => BitCount(RequiredMask);
        public int CutCableCount
        {
            get
            {
                int count = 0;
                for (int i = 0; cables != null && i < cables.Length && i < 32; i++)
                    if (cables[i] != null && cables[i].RequiredForExit && SharedCableCut(i, out _)) count++;
                return count;
            }
        }
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
            shots.Clear(); shotWait = 0; recoilTime = 0;
            ClearRechargeVisual();
            for (int i = 0; i < projectilePool.Length; i++) if (projectilePool[i] != null) projectilePool[i].SetActive(false);
            if (recoilVisual != null) recoilVisual.localPosition = recoilRest;
            if (launchAudio != null) launchAudio.Stop();
            if (launchFlash != null) launchFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        void CarrierChanged()
        {
            // 발사된 무제한 톱날은 장치를 내려놓거나 다른 사람이 들어도 계속 이동한다.
            if (!UnlimitedProjectiles)
            {
                float remaining = shotWait;
                ClearShots();
                shotWait = remaining;
            }
            recoilTime = 0;
        }
        void ResetProjectileCourse()
        {
            ClearShots(); cutMask = shotSerial = lastPresentedShot = 0;
            finaleDoorReported = false;
            Array.Clear(cutPoints, 0, cutPoints.Length);
            PresentCables(false);
        }
        void PresentCables(bool feedback)
        {
            for (int i = 0; cables != null && i < cables.Length && i < 32; i++)
                if (cables[i] != null)
                {
                    bool cut = SharedCableCut(i, out Vector3 point);
                    if (cut) cables[i].SetCut(true, point, feedback && !cables[i].IsCut);
                    // 대기실 장부에 이미 끊김으로 남은 배선은 씬을 다시 불러와도 끊긴 채로 둔다(2026-11-15 사용자: 씬 이동 뒤 숙소 배선이 다시 보임).
                    else if (!string.IsNullOrEmpty(cables[i].OpenTarget) && CurtainCall.WaitingRoom.ExplorationState.Instance is { } book && book.IsOpen(cables[i].OpenTarget)) cables[i].SetCut(true);
                    else if (cables[i].IsCut) cables[i].SetCut(false);
                }
        }
        // The visible cable, objective count, and physical shared door use the same newest-round verdict.
        // A lagging door-owning launcher must use cuts already received by its peers.
        bool SharedCableCut(int i, out Vector3 point) => SharedCableCut(i, out point, out _);

        bool SharedCableCut(int i, out Vector3 point, out uint newestCourse)
        {
            bool cut = (cutMask & (1u << i)) != 0;
            newestCourse = courseEpoch;
            point = cutPoints[i];
            foreach (var peer in devices)
            {
                if (peer == null || peer == this || peer.cables == null || peer.courseEpoch < newestCourse) continue;
                int index = Array.IndexOf(peer.cables, cables[i]);
                if (index < 0 || index >= 32) continue;
                if (peer.courseEpoch > newestCourse) { newestCourse = peer.courseEpoch; cut = false; }
                if (!cut && (peer.cutMask & (1u << index)) != 0)
                { cut = true; point = peer.cutPoints[index]; }
            }
            return cut;
        }
        void RestoreSharedCableState()
        {
            if (cables == null || cables.Length == 0) return;
            uint newestCourse = courseEpoch;
            foreach (var peer in devices)
            {
                if (peer == null || peer == this || peer.cables == null || peer.courseEpoch <= newestCourse) continue;
                foreach (var cable in cables)
                {
                    if (cable == null || Array.IndexOf(peer.cables, cable) < 0) continue;
                    newestCourse = peer.courseEpoch;
                    break;
                }
            }

            // A peer can see a genuine restart or finish cutting a cable while this
            // launcher is disabled. Only a shared cable identifies the same course.
            if (newestCourse != courseEpoch)
            {
                courseEpoch = newestCourse;
                TeamReachedExitZone = false;
                ResetProjectileCourse();
            }
            for (int i = 0; i < cables.Length && i < 32; i++)
            {
                if (cables[i] == null || !SharedCableCut(i, out Vector3 point)) continue;
                cutMask |= 1u << i;
                cutPoints[i] = point;
            }
            PresentCables(false);
        }

        void HostProjectileStep(float deltaTime, NetworkPlayer carrier)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0) return;
            uint beforeMask = cutMask;
            int beforeCount = shots.Count;
            StepProjectiles(deltaTime, true);
            if (TryConsumeFinaleDoorOpened())
            {
                CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Mechanism(null, this, "EXIT_DOOR", "OPEN",
                    CableSocket ? CableSocket.position : transform.position, true);
                if (!string.IsNullOrEmpty(openTarget))
                    CurtainCall.WaitingRoom.ExplorationState.Instance?.MarkVisited(openTarget);
            }
            shotWait = Mathf.Max(0, shotWait - deltaTime);
            if (cutting || beforeMask != cutMask || beforeCount != shots.Count)
            {
                cutting = false; stateRevision++;
                Publish(); nextPublish = Time.unscaledTimeAsDouble + .1;
            }
            else if (Time.unscaledTimeAsDouble >= nextPublish)
            { nextPublish = Time.unscaledTimeAsDouble + .1; stateRevision++; Publish(); }
        }
        bool TryConsumeFinaleDoorOpened()
        {
            if (!finaleBoss || finaleDoorReported || (!doorPanel && (!leftDoorHinge || !rightDoorHinge)) || !DoorOpen) return false;
            finaleDoorReported = true;
            return true;
        }
        bool TryFire(NetworkPlayer carrier)
        {
            if (!ProjectileMode || !CanOperate(carrier) || Carrier != carrier.OwnerClientId
                || shotWait > 0 || shots.Count >= MaxProjectiles) return false;
            // A blocked turn can leave a valid held pose. Reject a shot only if that pose lost the carrier's hand.
            if (carryBlocked && Vector3.Distance(Handle.position, RouteFeet(carrier) + Vector3.up * carryHandleHeight) >= .3f)
                return false;
            if (!SpawnProjectile()) return false;
            shotWait = shotInterval;
            return true;
        }
        bool CanOperate(NetworkPlayer player)
        {
            if (!Alive(player) || inspection != null && inspection.IsInspecting(player.OwnerClientId)
                || !player.TryGetComponent(out CurtainCall.Player.PlayerMover mover) || !mover.Body || !mover.Body.enabled
                || player.IsLocal && (!mover.Simulated || !mover.ControlEnabled)) return false;
            var cannon = HumanCannonSystem.Current;
            if (cannon != null && (cannon.IsFlying(player.OwnerClientId) || cannon.IsSeated(player.OwnerClientId))) return false;
            return !(HumanCannonPoleSystem.Current != null && HumanCannonPoleSystem.Current.HeldBy(player.OwnerClientId) >= 0)
                && !(CurtainCall.Tightrope.TightropeHangSystem.Current != null && CurtainCall.Tightrope.TightropeHangSystem.Current.IsHanging(player))
                && !(CurtainCall.Tightrope.PiggybackSystem.Current != null && CurtainCall.Tightrope.PiggybackSystem.Current.IsInStack(player));
        }
        bool SpawnProjectile()
        {
            if (shots.Count >= MaxProjectiles) return false;
            var origin = muzzle != null ? muzzle : blade != null ? blade : CarryRoot;
            Vector3 direction = Vector3.Cross(CarryRoot.right, Vector3.up).normalized;
            // A guest can still be looking at the boss as the host changes phase or
            // updates the held pose. A valid click still fires along the launcher.
            if (!Finite(origin.position) || direction.sqrMagnitude < .99f) return false;
            // 벽·설비에 바짝 붙어 쏘면 발사구가 이미 물체에 닿아 있어 톱날이 나오자마자 사라졌다(2026-11-13 대기실 플레이: 간헐적으로 발사 안 됨).
            // 발사구가 막혀 있으면 든 사람 쪽으로 조금씩 당겨, 막히지 않은 가장 앞 지점에서 내보낸다.
            Vector3 start = origin.position;
            var carrier = FindPlayer(Carrier);
            float level = start.y;
            Vector3 feet = carrier ? RouteFeet(carrier) : default;
            if (carrier)
            {
                float muzzleAboveHand = (CarryRoot.InverseTransformPoint(origin.position).y
                    - CarryRoot.InverseTransformPoint(Handle.position).y) * Mathf.Abs(CarryRoot.lossyScale.y);
                level = feet.y + Mathf.Max(0, carryHandleHeight) + muzzleAboveHand;
                if (finaleBoss && Mathf.Abs(start.y - level) > .25f) start.y = level;
            }
            Vector3 alignedOrigin = start;
            float back = Vector3.Dot(start - CarryRoot.position, direction);
            for (int step = 0; step < 6 && back > 0 && ObstructionDistance(start, direction, 0, out _); step++)
                start = alignedOrigin - direction * (back * (step + 1) / 6f);
            if (ObstructionDistance(start, direction, 0, out _)
                || carrier != null && LaunchPathObstructed(feet, start))
            {
                // The held body can cross a rail or wall, but the blade must start on
                // the bearer's side. Do not discard a click just because the muzzle
                // protrudes through scenery; launch from a clear point near the hand.
                if (carrier == null || !TryFindNearHandLaunch(feet, level, direction, out start)) return false;
            }
            var shot = new Shot { Id = ++shotSerial, Epoch = ownership.Revision, Shooter = Carrier, Position = start, Direction = direction };
            shots.Add(shot);
            CurtainCall.WaitingRoom.Pursuit.PursuitStimuli.Emit(CurtainCall.WaitingRoom.Pursuit.PursuitNoiseKind.SawFire, start, CarryRoot);
            NotifyShot(shot, "FIRED", shot.Position, "");
            lastPresentedShot = shotSerial;
            EmitLaunch();
            return true;
        }

        bool TryFindNearHandLaunch(Vector3 feet, float height, Vector3 direction, out Vector3 start)
        {
            var anchor = feet;
            anchor.y = height;
            start = anchor;
            Vector3 side = Vector3.Cross(Vector3.up, direction);
            for (int step = 0; step <= 8; step++)
            {
                var back = anchor - direction * (projectileRadius * step / 8f);
                for (int lane = 0; lane < 3; lane++)
                {
                    var candidate = back + side * ((lane == 2 ? -1 : lane) * .20f);
                    if (ObstructionDistance(candidate, direction, 0, out _)
                        || LaunchPathObstructed(feet, candidate)) continue;
                    start = candidate;
                    return true;
                }
            }
            return false;
        }

        // The carried body may pass through scenery. A muzzle on the far side of a
        // wall must not create a blade there, even if the muzzle itself is clear.
        bool LaunchPathObstructed(Vector3 carrierPosition, Vector3 start)
        {
            carrierPosition.y = start.y;
            var offset = start - carrierPosition;
            float distance = offset.magnitude;
            if (distance <= .001f) return false;
            var direction = offset / distance;
            int count = Physics.RaycastNonAlloc(carrierPosition, direction, shotHits, distance, ~0, QueryTriggerInteraction.Ignore);
            var hits = count == shotHits.Length
                ? Physics.RaycastAll(carrierPosition, direction, distance, ~0, QueryTriggerInteraction.Ignore) : shotHits;
            if (hits != shotHits) count = hits.Length;
            for (int i = 0; i < count; i++)
                if (ShotObstacle(hits[i].collider)) return true;
            return false;
        }
        void StepProjectiles(float deltaTime, bool host)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0) return;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i];
                if (!UnlimitedProjectiles && (shot.Epoch != ownership.Revision || Carrier == CannonSeatState.Nobody))
                { shots.RemoveAt(i); continue; }
                float step = UnlimitedProjectiles ? deltaTime : Mathf.Min(deltaTime, Mathf.Max(0, projectileLifetime - shot.Age));
                Vector3 from = shot.Position;
                float distance = projectileSpeed * step;
                // 클라이언트도 자기 화면의 벽에서 즉시 숨긴다. 절단 확정은 호스트만 한다.
                bool blocked = ObstructionDistance(from, shot.Direction, distance, out distance);
                Vector3 to = from + shot.Direction * distance;
                Vector3 normal = Quaternion.LookRotation(shot.Direction, Vector3.up) * Vector3.up;
                if (host && finaleBoss && finaleBoss.TryHitSaw(from, to, normal, projectileRadius, projectileHalfThickness, out var bossContact))
                { to = bossContact; blocked = true; }
                if (host && CurtainCall.WaitingRoom.Pursuit.PursuitDirector.Current is { } pursuit
                    && pursuit.TryHitSaw(from, to, shot.Direction, projectileRadius, projectileHalfThickness, out var pursuitContact))
                { to = pursuitContact; blocked = true; }
                // 막혀서 못 나아간 자리에서도 날에 닿은 배선은 끊는다(벽에 붙은 배선을 가까이서 쏜 경우).
                if (host) CutAlong(from, to, normal, shot);
                shot.Position = to; shot.Age += step;
                bool outsideEncounter = !blocked && UnlimitedProjectiles && finaleBoss
                    && finaleBoss.SawHasLeftEncounter(to, shot.Direction);
                if (blocked || outsideEncounter || !UnlimitedProjectiles && shot.Age >= projectileLifetime) {
                    if (host) NotifyShot(shot, "ENDED", to,
                        (shot.CutAny ? "CABLE_HIT/" : "NO_CABLE_HIT/")
                        + (blocked ? "OBSTRUCTION" : outsideEncounter ? "OUTSIDE_ARENA" : "LIFETIME"));
                    shots.RemoveAt(i);
                }
            }
        }
        bool ObstructionDistance(Vector3 from, Vector3 direction, float wanted, out float allowed)
        {
            allowed = wanted;
            var orientation = Quaternion.LookRotation(direction, Vector3.up);
            var half = new Vector3(projectileRadius, projectileHalfThickness, projectileRadius);
            int overlapsCount = Physics.OverlapBoxNonAlloc(from, half, shotOverlaps, orientation, ~0, QueryTriggerInteraction.Ignore);
            var overlaps = overlapsCount == shotOverlaps.Length
                ? Physics.OverlapBox(from, half, orientation, ~0, QueryTriggerInteraction.Ignore) : shotOverlaps;
            if (overlaps != shotOverlaps) overlapsCount = overlaps.Length;
            for (int i = 0; i < overlapsCount; i++) if (ShotObstacle(overlaps[i])) { allowed = 0; return true; }
            if (wanted <= 0) return false;
            int count = Physics.BoxCastNonAlloc(from, half, direction, shotHits, orientation, wanted, ~0, QueryTriggerInteraction.Ignore);
            var hits = count == shotHits.Length
                ? Physics.BoxCastAll(from, half, direction, orientation, wanted, ~0, QueryTriggerInteraction.Ignore) : shotHits;
            if (hits != shotHits) count = hits.Length;
            bool blocked = false;
            for (int i = 0; i < count; i++)
                if (ShotObstacle(hits[i].collider) && hits[i].distance <= allowed)
                { allowed = Mathf.Max(0, hits[i].distance - .002f); blocked = true; }
            return blocked;
        }
        bool ShotObstacle(Collider collider) => collider != null && !collider.transform.IsChildOf(CarryRoot)
            && !DismemberedLimb.Contains(collider)
            && collider.GetComponentInParent<CurtainCall.WaitingRoom.Pursuit.PursuitActor>() == null
            && !collider.GetComponent<EscapePursuerDebris>()
            && collider.GetComponentInParent<NetworkPlayer>() == null && collider.GetComponentInParent<EscapeCuttableCable>() == null
            && (!finaleBoss || collider.GetComponentInParent<EscapePursuerBoss>() is not { } boss || !boss.BelongsTo(finaleBoss));
        void CutAlong(Vector3 from, Vector3 to, Vector3 normal, Shot shot = null)
        {
            for (int i = 0; cables != null && i < cables.Length && i < 32; i++)
            {
                var cable = cables[i];
                uint bit = 1u << i;
                if (cable == null || !cable.isActiveAndEnabled
                    || SharedCableCut(i, out _, out uint latest) || latest != courseEpoch) continue;
                if (!cable.TryIntersectDisk(from, to, normal, projectileRadius, projectileHalfThickness, out _, out var hit)) continue;
                cutMask |= bit; cutPoints[i] = hit; cable.SetCut(true, hit, true);
                if (shot != null) {
                    shot.CutAny = true;
                    NotifyShot(shot, "CABLE_CUT", hit,
                        CurtainCall.Telemetry.Pipeline.DetailLogIds.ObjectId(cable, "IN_"));
                }
                if (!finaleBoss && DoorOpen)
                {
                    CurtainCall.Telemetry.Pipeline.TelemetryRuntime.Mechanism(null, this, "EXIT_DOOR", "OPEN",
                        CableSocket ? CableSocket.position : hit, true);
                    if (!string.IsNullOrEmpty(openTarget))
                        CurtainCall.WaitingRoom.ExplorationState.Instance?.MarkVisited(openTarget);
                }
                if (!string.IsNullOrEmpty(cable.OpenTarget)) CurtainCall.WaitingRoom.ExplorationState.Instance?.MarkVisited(cable.OpenTarget);
                ShareCut(cable, hit);
            }
        }
        // Every launcher owns its carrier/projectiles; cable cuts belong to the shared exit.
        void ShareCut(EscapeCuttableCable cable, Vector3 point)
        {
            foreach (var device in devices)
            {
                if (device == null || device == this || !device.ProjectileMode || device.courseEpoch != courseEpoch) continue;
                int index = Array.IndexOf(device.cables, cable);
                if (index < 0 || index >= 32 || (device.cutMask & (1u << index)) != 0) continue;
                device.cutMask |= 1u << index; device.cutPoints[index] = point;
                device.Changed();
            }
        }
        void EmitLaunch()
        {
            CurtainCall.Sound.SoundManager.PlayAt(CurtainCall.Sound.SoundCueIds.EscapeSawLaunch,
                launchAudio ? launchAudio.transform.position : CarryFocus);
            if (launchFlash != null) launchFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            recoilTime = .15f;
        }
        void PresentProjectiles(float deltaTime)
        {
            PresentRecharge(deltaTime);
            for (int i = 0; i < projectilePool.Length; i++)
            {
                bool show = ProjectileSessionReady && ProjectileMode && i < shots.Count;
                if (show && projectilePool[i] == null && projectileVisual != null && Application.isPlaying)
                {
                    var visual = Instantiate(projectileVisual);
                    visual.transform.localScale = Vector3.one;
                    visual.name = "__ExitSawProjectile_" + i; visual.hideFlags = HideFlags.DontSave;
                    foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    StripProjectileEffects(visual);
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
                        Quaternion.LookRotation(shot.Direction, Vector3.up) * Quaternion.AngleAxis(shot.Age * BladeSpinDegreesPerSecond, Vector3.up));
                }
            }
            if (recoilVisual != null)
            {
                recoilTime = Mathf.Max(0, recoilTime - Mathf.Max(0, deltaTime));
                recoilVisual.localPosition = recoilRest - Vector3.forward * (recoilDistance * recoilTime / .15f);
            }
        }
        void PresentRecharge(float deltaTime)
        {
            if (!Application.isPlaying || !ProjectileMode) return;
            if (session != null && !session.IsHost) shotWait = Mathf.Max(0f, shotWait - deltaTime);
            var material = CurtainCall.Settings.GameSettings.Base.CarryDevices.SawRechargeMaterial;
            if (!blade || !material || shotWait <= 0f || !CarryRoot.gameObject.activeInHierarchy)
            {
                ClearRechargeVisual();
                return;
            }
            rechargeProperties ??= new MaterialPropertyBlock();
            if (rechargeRenderers.Count == 0)
                foreach (var renderer in blade.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
                    if (renderer.name.StartsWith("__", StringComparison.Ordinal)) continue;
                    var original = renderer.sharedMaterials;
                    if (original.Length == 0) continue;
                    var properties = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(properties);
                    rechargeRenderers.Add((renderer, original, properties));
                    var temporary = new Material[original.Length];
                    Array.Fill(temporary, material);
                    renderer.sharedMaterials = temporary;
                }
            rechargeProperties.SetFloat(RechargeAmount, Mathf.Clamp01(shotWait / Mathf.Max(.01f, shotInterval - .25f)));
            foreach (var item in rechargeRenderers)
                if (item.renderer) item.renderer.SetPropertyBlock(rechargeProperties);
        }

        void ClearRechargeVisual()
        {
            foreach (var item in rechargeRenderers)
                if (item.renderer)
                {
                    item.renderer.sharedMaterials = item.materials;
                    item.renderer.SetPropertyBlock(item.properties);
                }
            rechargeRenderers.Clear();
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
        static void StripProjectileEffects(GameObject visual)
        {
            foreach (var particles in visual.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) { renderer.enabled = false; if (Application.isPlaying) Destroy(renderer); else DestroyImmediate(renderer); }
                if (Application.isPlaying) Destroy(particles); else DestroyImmediate(particles);
            }
        }
        bool ValidProjectileSnapshot(uint epoch, ulong carrier, uint mask, Vector3[] points, Shot[] incoming, float wait)
        {
            if (points == null || points.Length != 32 || incoming == null || incoming.Length > MaxProjectiles
                || (mask & ~ValidCableMask) != 0 || !float.IsFinite(wait) || wait < 0 || wait > shotInterval + .001f
                || !ProjectileMode && (mask != 0 || incoming.Length != 0)
                || !UnlimitedProjectiles && carrier == CannonSeatState.Nobody && incoming.Length != 0) return false;
            for (int i = 0; i < 32; i++) if ((mask & (1u << i)) != 0 && !Finite(points[i])) return false;
            for (int i = 0; i < incoming.Length; i++)
            {
                var shot = incoming[i];
                if (shot == null || shot.Id == 0 || (UnlimitedProjectiles ? shot.Epoch > epoch : shot.Epoch != epoch) || !Finite(shot.Position) || !Finite(shot.Direction)
                    || Mathf.Abs(shot.Direction.y) > .001f || Mathf.Abs(shot.Direction.sqrMagnitude - 1) > .01f
                    || !float.IsFinite(shot.Age) || shot.Age < 0 || !UnlimitedProjectiles && shot.Age >= projectileLifetime) return false;
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
