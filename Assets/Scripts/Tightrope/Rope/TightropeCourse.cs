using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>
    /// 외줄 코스 스테이지(프리팹 루트)이자 코스 조회의 공개 진입점. 다른 기능은 <see cref="Current"/>로 이 코스를 찾는다.
    /// 직선 코스를 외줄 세팅(<see cref="TightropeSettings.Course"/>)의 값대로 만든다: 시작 플랫폼 → 나란한 줄(줄마다 끝 거리) → 도착 거리.
    /// 만들 때 세팅 값을 복사해 두고 그 값을 쓴다. 게임 시작 전에는 호스트의 코스 값을 모든 화면에 맞추고(<see cref="CourseShapeSync"/>), 게임이 시작되면 값을 잠근다.
    /// 코스 전진 방향은 이 오브젝트의 앞(+Z)이다. 플레이어 이동(<see cref="PlayerMover"/>)의 전진이 월드 +Z이므로 회전하지 않고 배치한다.
    /// 출발 위치는 <see cref="spawnPoints"/>를 자리 번호 순으로 옮겨 두며, 같은 오브젝트의 <see cref="PlayerSpawnPoints"/>(007)가 이 Transform들을 쓴다.
    /// 에디터에서는 기즈모로, 플레이 중에는 실제 메시·충돌체로 보인다. 플레이 중 세팅을 바꾸면 다시 만든다(잠겨 있으면 풀릴 때).
    /// 플레이 중에는 모든 플레이어의 옆줄 점프 거리를 줄 간격에 맞추고, 내 캐릭터가 줄 아래로 떨어지면 추락 처리한다.
    /// </summary>
    public sealed class TightropeCourse : MonoBehaviour
    {
        /// <summary>줄 없음.</summary>
        public const int NoLane = CourseLayout.NoLane;

        [Header("연결")]
        [Tooltip("자리 번호 0, 1, 2… 순서의 출발 위치. 코스가 줄 위치에 맞춰 옮긴다. 같은 오브젝트의 PlayerSpawnPoints에도 같은 순서로 넣는다.")]
        [SerializeField] Transform[] spawnPoints;

        [Header("색")]
        [SerializeField] Color ropeColor = new(0.85f, 0.75f, 0.55f);
        [SerializeField] Color platformColor = new(0.45f, 0.45f, 0.5f);
        [SerializeField] Color finishColor = new(0.3f, 0.8f, 0.4f);
        [SerializeField] Color netColor = new(0.2f, 0.2f, 0.25f);

        const string GeneratedName = "Generated";

        static TightropeCourse current;

        TightropeSettings.CourseSettings shape; // 플레이 중 코스를 만든 값(세팅 복사본 또는 받은 호스트 값)
        CourseLayout layout;
        bool[] blocked = Array.Empty<bool>();
        readonly System.Collections.Generic.List<(int lane, float from, float to, bool includeEnd)> blockedSegments = new();
        Transform generated;
        bool rebuildRequested;

        /// <summary>씬에 놓인 코스. 없으면 null.</summary>
        public static TightropeCourse Current
        {
            get
            {
                if (current == null) current = FindAnyObjectByType<TightropeCourse>();
                return current;
            }
        }

        /// <summary>코스가 다시 만들어졌을 때(플레이 중 세팅 변경·호스트 값 받기 포함).</summary>
        public event Action Rebuilt;

        /// <summary>막힌 구간(<see cref="BlockSegment"/>)이 바뀌었을 때.</summary>
        public event Action SegmentsChanged;

        /// <summary>줄 사용 가능 여부가 바뀌었을 때. 인자는 (줄, 막혔는지).</summary>
        public event Action<int, bool> LaneBlockedChanged;

        /// <summary>지금 코스 모양 값. 플레이 중에는 만들 때 붙잡아 둔 값(온라인이면 호스트 값), 플레이 전에는 세팅 그대로.</summary>
        public TightropeSettings.CourseSettings Shape => Application.isPlaying ? (shape ??= Settings.Clone()) : Settings;

        static TightropeSettings.CourseSettings Settings => GameSettings.Tightrope.Course;

        /// <summary>코스 모양 계산(읽기 전용).</summary>
        public CourseLayout Layout => Application.isPlaying ? (layout ??= CreateLayout()) : CreateLayout();

        public int LaneCount => Layout.LaneCount;
        public float LaneSpacing => Layout.LaneSpacing;

        /// <summary>도착 거리(m, 출발선에서).</summary>
        public float FinishDistance => Layout.FinishDistance;

        /// <summary>코스 전진 방향(수평, 정규화).</summary>
        public Vector3 Forward
        {
            get
            {
                Vector3 forward = transform.forward;
                forward.y = 0f;
                return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            }
        }

        /// <summary>코스 오른쪽(수평, 정규화).</summary>
        public Vector3 Right => Vector3.Cross(Vector3.up, Forward);

        // ── 조회 ────────────────────────────────────────────

        /// <summary>위치의 진행 거리(m). 출발선이 0, 전진 방향이 +.</summary>
        public float GetDistance(Vector3 position) => Vector3.Dot(position - transform.position, Forward);

        /// <summary>위치의 옆 좌표(m). 코스 가운데가 0, 오른쪽이 +.</summary>
        public float GetSide(Vector3 position) => Vector3.Dot(position - transform.position, Right);

        /// <summary>
        /// 위치를 가장 가까운 줄과 진행 거리로 바꾼다. 그 거리에 줄이 하나도 없으면(플랫폼 위, 도착 뒤) false이고 <paramref name="lane"/>은 <see cref="NoLane"/>.
        /// 줄과 얼마나 떨어져 있는지는 보지 않는다(줄 위에 있는지는 <see cref="GetLanePosition"/>과 비교).
        /// </summary>
        public bool TryGetRopePoint(Vector3 position, out int lane, out float distance)
        {
            distance = GetDistance(position);
            lane = Layout.FindNearestLane(GetSide(position), distance);
            return lane != NoLane;
        }

        /// <summary>
        /// 위치가 줄 위인지(발이 줄 높이 근처이고, 그 거리에 놓인 줄의 걸을 수 있는 폭 + <paramref name="radius"/> 안). 맞으면 그 줄 번호.
        /// 땅에 서 있는지는 보지 않으므로 공중 판정은 부르는 쪽이 한다. 플랫폼 위는 false.
        /// </summary>
        public bool IsOnRope(Vector3 position, float radius, out int lane)
        {
            lane = NoLane;
            if (Mathf.Abs(position.y - transform.position.y) > Shape.RopeHeightTolerance) return false;
            if (!TryGetRopePoint(position, out int nearest, out _)) return false;
            if (Mathf.Abs(GetSide(position) - Layout.GetLaneSide(nearest)) > Shape.RopeWalkWidth * 0.5f + radius) return false;
            lane = nearest;
            return true;
        }

        /// <summary>
        /// 그 위치에서 왼쪽(-1)/오른쪽(+1)으로 옆줄 이동하면 착지할 줄. 바로 옆 줄이 그 거리에 놓여 있고 사용 가능할 때만 그 번호, 아니면 <see cref="NoLane"/>.
        /// 거리는 넘긴 위치로 판단하므로, 착지 예상 지점에서 보려면 앞뒤로 옮긴 위치를 넘긴다. 옆줄 이동 가능 구간은 <see cref="IsLaneChangeAllowed"/>로 따로 본다.
        /// </summary>
        public int FindLandingLane(Vector3 position, int direction)
        {
            if (direction == 0) return NoLane;
            float distance = GetDistance(position);
            float side = GetSide(position);
            int from = Layout.FindNearestLane(side, distance);
            if (from == NoLane) from = Layout.GetLaneSlotAt(side);
            int target = from + Math.Sign(direction);
            return IsLaneUsable(target, distance) ? target : NoLane;
        }

        /// <summary>그 거리가 옆줄 이동 가능 구간 안인지.</summary>
        public bool IsLaneChangeAllowed(float distance) => Layout.IsLaneChangeAllowed(distance);

        /// <summary>그 거리에 줄이 놓여 있고 막히지 않았는지(줄 전체 막힘 + 불타는 구간 등 구간 막힘).</summary>
        public bool IsLaneUsable(int lane, float distance) =>
            Layout.LaneExists(lane, distance) && !IsLaneBlocked(lane) && !IsSegmentBlocked(lane, distance);

        /// <summary>그 줄의 그 거리가 막힌 구간 안인지.</summary>
        public bool IsSegmentBlocked(int lane, float distance)
        {
            foreach (var segment in blockedSegments)
                if (segment.lane == lane && distance >= segment.from
                    && (segment.includeEnd ? distance <= segment.to : distance < segment.to)) return true;
            return false;
        }

        /// <summary>
        /// 줄의 일부 구간을 막는다(예: 불타는 구간, 2026-10-03 규칙 — 줄은 이어져 있지만 그 구간은 쓸 수 없음).
        /// 이 화면에만 적용되므로 온라인에서는 모든 화면이 같은 신호로 부른다. 화재 기능이 생기면 쓴다.
        /// </summary>
        public void BlockSegment(int lane, float from, float to, bool includeEnd = true)
        {
            if (lane < 0 || lane >= LaneCount) return;
            blockedSegments.Add((lane, Mathf.Min(from, to), Mathf.Max(from, to), includeEnd));
            SegmentsChanged?.Invoke();
        }

        /// <summary>막은 구간을 모두 푼다(재시작 때 묘기 진행이 부른다).</summary>
        public void ClearBlockedSegments()
        {
            if (blockedSegments.Count == 0) return;
            blockedSegments.Clear();
            SegmentsChanged?.Invoke();
        }

        /// <summary>줄이 막혔는지.</summary>
        public bool IsLaneBlocked(int lane) => lane >= 0 && lane < blocked.Length && blocked[lane];

        /// <summary>
        /// 줄을 막거나 다시 연다(자리만 마련, 이후 화재용). 이 화면에만 적용되므로 온라인에서는 모든 화면이 같은 신호로 부른다.
        /// 재시작하면 기능 쪽에서 다시 연다.
        /// </summary>
        public void SetLaneBlocked(int lane, bool isBlocked)
        {
            if (lane < 0 || lane >= blocked.Length || blocked[lane] == isBlocked) return;
            blocked[lane] = isBlocked;
            LaneBlockedChanged?.Invoke(lane, isBlocked);
        }

        /// <summary>줄 위 그 거리의 월드 위치(줄 윗면).</summary>
        public Vector3 GetLanePosition(int lane, float distance) =>
            transform.position + Right * Layout.GetLaneSide(lane) + Forward * distance;

        /// <summary>자리 번호의 출발 위치·방향. 자리 번호 = 줄 번호, 줄보다 많으면 뒤로 1m씩 줄 세운다.</summary>
        public Pose GetStartPose(int slot)
        {
            slot = Mathf.Max(0, slot);
            int lane = slot % LaneCount;
            float distance = Shape.SpawnDistance - slot / LaneCount;
            return new Pose(GetLanePosition(lane, distance), Quaternion.LookRotation(Forward, Vector3.up));
        }

        /// <summary>자리 번호의 도착(세리머니 공간) 위치·방향. 자기 줄 자리, 도착선 뒤 세리머니 공간 가운데(최대 2m).</summary>
        public Pose GetFinishPose(int slot)
        {
            int lane = Mathf.Max(0, slot) % LaneCount;
            float distance = FinishDistance + Mathf.Clamp(Shape.FinishPlatformLength * 0.5f, 0.5f, 2f);
            return new Pose(GetLanePosition(lane, distance), Quaternion.LookRotation(Forward, Vector3.up));
        }

        /// <summary>위치가 도착 거리에 닿았는지.</summary>
        public bool HasReachedFinish(Vector3 position) => GetDistance(position) >= FinishDistance;

        // ── 생성 ────────────────────────────────────────────

        void Awake()
        {
            if (current == null) current = this;
            Rebuild();
        }

        void OnEnable()
        {
            current = this;
            GameSettings.Tightrope.Changed += HandleSettingsChanged;
        }

        void OnDisable()
        {
            if (current == this) current = null;
            GameSettings.Tightrope.Changed -= HandleSettingsChanged;
        }

        /// <summary>세팅이 바뀌면 다음 프레임에 다시 읽는다. 잠겨 있으면 풀릴 때 읽는다.</summary>
        void HandleSettingsChanged()
        {
            if (!Application.isPlaying) return;
            if (Locked)
            {
                if (!rebuildRequested)
                    Debug.LogWarning($"[Tightrope] 코스 세팅 변경은 잠금이 풀릴 때 반영됩니다: {lockReason}", this);
                rebuildRequested = true;
                return;
            }
            rebuildRequested = true; // OnValidate 안에서는 오브젝트를 만들지 않는다
        }

        void Update()
        {
            if (rebuildRequested && !Locked)
            {
                rebuildRequested = false;
                ReloadSettings();
            }
            ApplyLaneSpacing();
            CheckLocalFall();
        }

        /// <summary>
        /// 모든 플레이어의 옆줄 점프 거리를 코스 줄 간격에 맞춘다(늦게 생성된 플레이어 포함).
        /// 코스 값은 호스트 값으로 맞춰지므로(<see cref="CourseShapeSync"/>) 모든 화면이 같은 거리가 된다.
        /// </summary>
        void ApplyLaneSpacing()
        {
            float spacing = Layout.LaneSpacing;
            foreach (var mover in PlayerMover.All)
                if (!Mathf.Approximately(mover.LaneSpacing, spacing))
                    mover.LaneSpacing = spacing;
        }

        /// <summary>
        /// 내 캐릭터가 줄 높이보다 세팅의 추락 깊이 넘게 내려가면 추락시킨다(→ 007 흐름으로 호스트에 추락 요청).
        /// 줄 밖 착지 자체의 판정은 줄 위 캐릭터 동작(011)이 한다. 이 검사는 어떤 이유로든 떨어진 경우를 놓치지 않기 위한 바닥선이다.
        /// </summary>
        void CheckLocalFall()
        {
            var player = NetworkPlayer.Local;
            if (player == null || player.State != PlayerState.Normal) return;
            if (player.transform.position.y >= transform.position.y - Shape.FallDepth) return;

            if (player.TryGetComponent(out PlayerBalance balance) && balance.enabled)
                balance.ForceFall(); // 이미 추락했으면 무시된다. Fell → RequestState(Fallen)
            else
                player.RequestState(PlayerState.Fallen);
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) PlaceSpawnPoints();
            };
#endif
        }

        /// <summary>세팅 값이 지금 코스와 다르면 복사해 다시 만든다.</summary>
        void ReloadSettings()
        {
            string json = Settings.ToJson();
            if (shape != null && json == shape.ToJson()) return;
            shape = TightropeSettings.CourseSettings.FromJson(json);
            Rebuild();
        }

        /// <summary>지금 코스 모양 값(<see cref="Shape"/>)으로 코스를 다시 만든다. 에디터(플레이 전)에서는 출발 위치만 옮긴다.</summary>
        public void Rebuild()
        {
            layout = CreateLayout();
            if (blocked.Length != layout.LaneCount) blocked = new bool[layout.LaneCount];
            if (!layout.HasLaneToFinish())
                Debug.LogWarning($"[Tightrope] 도착 거리({layout.FinishDistance}m)까지 가는 줄이 없습니다. 줄 끝 거리를 확인하세요.", this);
            if (Vector3.Angle(Forward, Vector3.forward) > 1f)
                Debug.LogWarning("[Tightrope] 코스가 회전되어 있습니다. 플레이어 전진(월드 +Z)과 어긋나므로 회전 없이 배치하세요.", this);

            PlaceSpawnPoints();
            if (Application.isPlaying)
            {
                BuildMeshes();
                UpdateLocalStartPose();
            }
            Rebuilt?.Invoke();
        }

        /// <summary>
        /// 내 캐릭터의 출발 위치를 바뀐 줄에 맞춘다(다음 재시작부터). 시작 플랫폼에 서 있으면 바로 옮긴다.
        /// 캐릭터 위치는 소유자가 정하므로 내 캐릭터만 처리한다.
        /// </summary>
        void UpdateLocalStartPose()
        {
            var player = NetworkPlayer.Local;
            if (player == null || player.Slot == NetworkPlayer.NoSlot || !player.TryGetComponent(out PlayerMover mover)) return;
            Pose pose = GetStartPose(player.Slot);
            bool onStartPlatform = player.State == PlayerState.Normal && !mover.IsAirborne && GetDistance(player.transform.position) < 0f;
            mover.SetStartPose(pose.position, pose.rotation, onStartPlatform);
        }

        // ── 코스 값 공유·잠금 ───────────────────────────────

        string lockReason;

        /// <summary>코스 값이 잠겼는지. 잠긴 동안 세팅 변경은 미뤄 두고 풀릴 때 반영한다.</summary>
        public bool Locked { get; private set; }

        /// <summary>지금 코스 값(JSON). 호스트가 클라이언트에 보낼 때 쓴다.</summary>
        public string ExportShape() => Shape.ToJson();

        /// <summary>받은 코스 값(JSON)으로 바꾸고 다시 만든다. 세팅 에셋은 바꾸지 않는다.</summary>
        public void ImportShape(string json)
        {
            if (string.IsNullOrEmpty(json) || json == ExportShape()) return;
            var imported = TightropeSettings.CourseSettings.FromJson(json);
            if (imported == null) return;
            shape = imported;
            Rebuild();
        }

        /// <summary>
        /// 코스 값을 잠그거나 푼다. <paramref name="reason"/>은 잠긴 동안 세팅을 바꿨을 때 경고 문구.
        /// 풀면 세팅을 다시 읽어 다르면 다시 만든다(예: 클라이언트가 접속을 끊으면 내 세팅으로, 게임 중 바꾼 값은 다음 대기 때).
        /// </summary>
        public void SetLocked(bool locked, string reason = null)
        {
            Locked = locked;
            lockReason = reason;
            if (!locked && Application.isPlaying) rebuildRequested = true;
        }

        CourseLayout CreateLayout()
        {
            var c = Shape;
            return new(c.LaneCount, c.LaneSpacing, c.FinishDistance, c.LaneEndDistances, c.LaneChangeZones);
        }

        void PlaceSpawnPoints()
        {
            if (spawnPoints == null) return;
            for (int slot = 0; slot < spawnPoints.Length; slot++)
            {
                if (spawnPoints[slot] == null) continue;
                Pose pose = GetStartPose(slot);
                spawnPoints[slot].SetPositionAndRotation(pose.position, pose.rotation);
            }
        }

        void BuildMeshes()
        {
            if (generated != null) Destroy(generated.gameObject);
            generated = new GameObject(GeneratedName).transform;
            generated.SetParent(transform, false);

            var l = layout;
            var c = Shape;
            float width = (l.LaneCount - 1) * l.LaneSpacing + c.SideMargin * 2f;

            // 시작 플랫폼: 출발선 뒤, 모든 줄 폭
            CreateBox("StartPlatform", new Vector3(0f, -c.PlatformThickness * 0.5f, -c.StartPlatformLength * 0.5f),
                new Vector3(width, c.PlatformThickness, c.StartPlatformLength), platformColor, true);

            // 줄: 보이는 굵기는 가늘게, 충돌체는 걸을 수 있는 폭으로
            for (int lane = 0; lane < l.LaneCount; lane++)
            {
                float end = l.GetLaneEnd(lane);
                var rope = CreateBox($"Rope{lane}", new Vector3(l.GetLaneSide(lane), -c.RopeThickness * 0.5f, end * 0.5f),
                    new Vector3(c.RopeThickness, c.RopeThickness, end), ropeColor, true);
                var box = rope.GetComponent<BoxCollider>();
                box.size = new Vector3(c.RopeWalkWidth / c.RopeThickness, 1f, 1f);
            }

            // 도착 선과 도착 플랫폼: 도착까지 가는 줄들을 덮는다
            float finishMin = float.MaxValue, finishMax = float.MinValue;
            for (int lane = 0; lane < l.LaneCount; lane++)
            {
                if (l.GetLaneEnd(lane) < l.FinishDistance) continue;
                finishMin = Mathf.Min(finishMin, l.GetLaneSide(lane));
                finishMax = Mathf.Max(finishMax, l.GetLaneSide(lane));
            }
            if (finishMin <= finishMax)
            {
                float finishWidth = finishMax - finishMin + c.SideMargin * 2f;
                float center = (finishMin + finishMax) * 0.5f;
                CreateBox("FinishLine", new Vector3(center, 0.01f, l.FinishDistance),
                    new Vector3(finishWidth, 0.02f, 0.1f), finishColor, false);
                if (c.FinishPlatformLength > 0f)
                    CreateBox("FinishPlatform", new Vector3(center, -c.PlatformThickness * 0.5f, l.FinishDistance + c.FinishPlatformLength * 0.5f),
                        new Vector3(finishWidth, c.PlatformThickness, c.FinishPlatformLength), platformColor, true);
            }

            if (c.SafetyNetDepth > 0f)
            {
                float length = c.StartPlatformLength + l.FinishDistance + c.FinishPlatformLength + 10f;
                CreateBox("SafetyNet", new Vector3(0f, -c.SafetyNetDepth - 0.05f, (l.FinishDistance + c.FinishPlatformLength - c.StartPlatformLength) * 0.5f),
                    new Vector3(width + 10f, 0.1f, length), netColor, true);
            }
        }

        GameObject CreateBox(string objectName, Vector3 localCenter, Vector3 size, Color color, bool withCollider)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = objectName;
            box.transform.SetParent(generated, false);
            box.transform.localPosition = localCenter;
            box.transform.localScale = size;
            if (!withCollider) Destroy(box.GetComponent<Collider>());
            box.GetComponent<Renderer>().material.color = color;
            return box;
        }

        // ── 에디터 표시 ─────────────────────────────────────

        void OnDrawGizmos()
        {
            if (Application.isPlaying) return; // 플레이 중에는 실제 메시가 보인다
            var l = Layout;
            var c = Shape;
            Vector3 origin = transform.position, forward = Forward, right = Right;
            float width = (l.LaneCount - 1) * l.LaneSpacing + c.SideMargin * 2f;

            Gizmos.color = platformColor;
            DrawRect(origin + forward * (-c.StartPlatformLength * 0.5f), width, c.StartPlatformLength, forward, right);

            for (int lane = 0; lane < l.LaneCount; lane++)
            {
                Vector3 start = GetLanePosition(lane, 0f);
                Gizmos.color = ropeColor;
                Gizmos.DrawLine(start, GetLanePosition(lane, l.GetLaneEnd(lane)));

                // 옆줄 이동 가능 구간은 줄 옆에 초록 선
                Gizmos.color = Color.green;
                foreach (var zone in l.GetLaneChangeZones())
                {
                    float from = Mathf.Max(0f, Mathf.Min(zone.x, zone.y));
                    float to = Mathf.Min(l.GetLaneEnd(lane), Mathf.Max(zone.x, zone.y));
                    if (to > from)
                        Gizmos.DrawLine(GetLanePosition(lane, from) + Vector3.up * 0.05f + right * 0.15f,
                            GetLanePosition(lane, to) + Vector3.up * 0.05f + right * 0.15f);
                }
            }

            Gizmos.color = finishColor;
            Vector3 finishCenter = origin + forward * l.FinishDistance;
            Gizmos.DrawLine(finishCenter - right * width * 0.5f, finishCenter + right * width * 0.5f);
        }

        static void DrawRect(Vector3 center, float width, float length, Vector3 forward, Vector3 right)
        {
            Vector3 a = center - right * width * 0.5f - forward * length * 0.5f;
            Vector3 b = center + right * width * 0.5f - forward * length * 0.5f;
            Vector3 c = center + right * width * 0.5f + forward * length * 0.5f;
            Vector3 d = center - right * width * 0.5f + forward * length * 0.5f;
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c); Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        }
    }
}
