using System;
using System.Collections.Generic;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Player
{
    /// <summary>교체 가능한 외형 참조와 누적 절단 표시. 게임 판정은 변경하지 않는다.</summary>
    public sealed class HumanoidModel : MonoBehaviour
    {
        public Animator Animator;
        public SkinnedMeshRenderer Original;
        public SkinnedMeshRenderer[] Bodies;
        public SkinnedMeshRenderer[] Limbs;
        [Tooltip("양다리 손실 시 외형 높이 보정(모델 로컬 좌표). 충돌/눈높이는 변경하지 않음.")]
        public float CrawlHeightOffset;
        [Tooltip("비어 있으면 모든 표면을 기존 방식으로 착색한다. 지정하면 해당 재질 슬롯만 플레이어 색을 사용한다.")]
        public int[] PlayerTintMaterialSlots;
        [Min(.01f)] public float RagdollArmRadius = .055f;
        [Min(.01f)] public float RagdollLegRadius = .075f;
        [Min(.01f)] public float RagdollHeadRadius = .11f;
        public int Stage { get; private set; }
        public int PieceCount => pieces.Count;
        public Color SurfaceColor { get; private set; } = Color.white;
        public string Motion { get; private set; }

        sealed class Piece
        {
            public int Index;
            public GameObject Object;
            public Mesh Mesh;
            public Rigidbody Body;
            public Vector3 CutLocal;
            public float Expires;
        }

        readonly List<Piece> pieces = new();
        readonly List<(BodyPart Part, Rigidbody Body, Collider Collider)> ragdollParts = new();
        bool articulated;
        readonly Dictionary<Transform, Quaternion> restRotations = new();
        MaterialPropertyBlock block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        Vector3 basePosition;
        bool initialized;
        int stateHash;

        bool positionCaptured;

        void Awake()
        {
            if (Original == null || Original.rootBone == null) return;
            foreach (var bone in Original.rootBone.GetComponentsInChildren<Transform>())
                restRotations[bone] = bone.localRotation;
        }

        public static int StageFor(BodyPart parts)
        {
            for (int i = 0; i <= 4; i++)
                if (PlayerCondition.PartsForLostCount(i) == parts) return i;
            throw new ArgumentException("지원하지 않는 비순차 손실: " + parts);
        }

        public void Apply(BodyPart previous, BodyPart current, bool spawn, CharacterController body)
        {
            if (!positionCaptured) { basePosition = transform.localPosition; positionCaptured = true; }
            int next = StageFor(current);
            var pending = new List<Piece>();
            try
            {
                if (spawn && initialized)
                    for (int i = 0; i < 4; i++)
                        if ((previous & PlayerCondition.CutOrder[i]) == 0 && (current & PlayerCondition.CutOrder[i]) != 0)
                            pending.Add(BakePiece(i, body));
                for (int i = pieces.Count - 1; i >= 0; i--)
                    if ((current & PlayerCondition.CutOrder[pieces[i].Index]) == 0) RemovePiece(i);
                Original.enabled = next == 0;
                for (int i = 0; i < 4; i++) { Bodies[i].enabled = next == i + 1; Limbs[i].enabled = false; }
                Stage = next;
                foreach (var part in ragdollParts)
                    if ((current & part.Part) != 0)
                    {
                        part.Collider.enabled = false;
                        foreach (var joint in part.Body.GetComponents<CharacterJoint>()) Destroy(joint);
                        part.Body.isKinematic = true;
                    }
                initialized = true;
                // 분리된 래그돌의 월드 위치는 더 이상 슬롯 기준으로 되돌리지 않는다.
                if (transform.parent != null) transform.localPosition = basePosition + Vector3.up * (next >= 2 ? CrawlHeightOffset : 0f);
                // 정지한 기어가기의 속도는 거의 0이므로 전환을 기다리면 서 있는 포즈가 남는다.
                if (next >= 2 && next < 4) Play("Crawl", .0001f);
                if (Animator != null && !Animator.enabled) StopMotion();
                foreach (var piece in pending)
                {
                    piece.Object.SetActive(true);
                    var rb = piece.Body;
                    var settings = GameSettings.Base.BodyDamage;
                    Vector3 outward = CutPoint(piece) - transform.position;
                    outward.y = 0;
                    outward = outward.sqrMagnitude > .0001f ? outward.normalized : transform.right;
                    foreach (var limbBody in piece.Object.GetComponentsInChildren<Rigidbody>())
                    {
                        limbBody.isKinematic = false;
                        limbBody.linearVelocity = body != null ? body.velocity : Vector3.zero;
                        limbBody.angularVelocity = ReleaseSpin(limbBody) + UnityEngine.Random.onUnitSphere * settings.LimbSpin;
                    }
                    rb.AddForceAtPosition(outward * settings.LimbFlingSpeed + Vector3.up * settings.LimbFlingUpSpeed,
                        CutPoint(piece), ForceMode.Impulse);
                    foreach (var collider in piece.Object.GetComponentsInChildren<Collider>())
                    {
                        foreach (var other in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                            if (other != collider && (other.isTrigger || other.attachedRigidbody != null || other is CharacterController))
                                Physics.IgnoreCollision(collider, other);
                        CurtainCall.Tightrope.TightropeCourse.Current?.IgnoreRopeCollision(collider);
                    }
                    pieces.Add(piece);
                }
                pending.Clear();
            }
            finally { foreach (var piece in pending) Dispose(piece); }
        }

        Piece BakePiece(int index, CharacterController body)
        {
            var piece = new Piece { Index = index, Mesh = new Mesh { name = "절단 당시 포즈" } };
            try
            {
                var source = Limbs[index];
                source.BakeMesh(piece.Mesh, false);
                piece.Object = new GameObject("Detached " + PlayerCondition.CutOrder[index]);
                piece.Object.SetActive(false);
                // 상호작용 ray 대상에서 제외하고, 아래에서 판정 trigger/동적 물체 충돌도 제외한다.
                piece.Object.layer = Physics.IgnoreRaycastLayer;
                piece.Object.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                piece.Object.transform.localScale = source.transform.lossyScale;
                Vector3 cut = BakedCutPoint(piece);
                var bones = new Dictionary<Transform, Transform>();
                var skeleton = CloneSkeleton(source.rootBone, piece.Object.transform, bones);
                var upperId = new[] { HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm }[index];
                var lowerId = new[] { HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm }[index];
                var endId = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftHand, HumanBodyBones.RightHand }[index];
                var upper = bones[Animator.GetBoneTransform(upperId)];
                var lower = bones[Animator.GetBoneTransform(lowerId)];
                var end = bones[Animator.GetBoneTransform(endId)];
                // 절단면의 몸통 쪽 가중치도 함께 운반한다. 원본 뼈나 고정된 복제 뼈에 매달리지 않는다.
                upper.SetParent(piece.Object.transform, true);
                skeleton.SetParent(upper, true);
                var skin = new GameObject("Detached Skin").transform;
                skin.gameObject.layer = Physics.IgnoreRaycastLayer;
                skin.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                skin.localScale = source.transform.lossyScale;
                skin.SetParent(upper, true);
                var renderer = skin.gameObject.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = source.sharedMesh;
                renderer.sharedMaterials = source.sharedMaterials;
                var snapshotBlock = new MaterialPropertyBlock();
                for (int slot = 0; slot < Original.sharedMesh.subMeshCount; slot++)
                {
                    source.GetPropertyBlock(snapshotBlock, slot);
                    renderer.SetPropertyBlock(snapshotBlock, slot);
                    snapshotBlock.Clear();
                }
                renderer.quality = source.quality;
                renderer.rootBone = skeleton;
                renderer.bones = Array.ConvertAll(source.bones, bone => bones[bone]);
                renderer.updateWhenOffscreen = true;
                Tint(renderer, SurfaceColor, Original.sharedMesh.subMeshCount);
                float radius = index < 2 ? RagdollLegRadius : RagdollArmRadius;
                piece.Body = AddRagdollBone(upper, lower.position, null, PlayerCondition.CutOrder[index], 2f, radius, false);
                AddRagdollBone(lower, end.position, piece.Body, PlayerCondition.CutOrder[index], 1f, radius * .8f, false);
                piece.CutLocal = upper.InverseTransformPoint(cut);
                float lifetime = GameSettings.Base.BodyDamage.LimbLifetime;
                piece.Expires = lifetime > 0 ? Time.time + lifetime : float.PositiveInfinity;
                CutPoint(piece);
                return piece;
            }
            catch { Dispose(piece); throw; }
        }

        static Transform CloneSkeleton(Transform source, Transform parent, Dictionary<Transform, Transform> bones)
        {
            var clone = new GameObject(source.name).transform;
            clone.gameObject.layer = Physics.IgnoreRaycastLayer;
            clone.SetPositionAndRotation(source.position, source.rotation);
            clone.localScale = source.lossyScale;
            clone.SetParent(parent, true);
            bones.Add(source, clone);
            foreach (Transform child in source)
                if (child.name != "Ragdoll Collider") CloneSkeleton(child, clone, bones);
            return clone;
        }

        static Vector3 CutPoint(Piece piece) => piece.Body.transform.TransformPoint(piece.CutLocal);

        static Vector3 BakedCutPoint(Piece piece)
        {
            var vertices = piece.Mesh.vertices;
            var triangles = piece.Mesh.GetTriangles(piece.Mesh.subMeshCount - 1);
            Vector3 sum = Vector3.zero;
            float area = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                float weight = Vector3.Cross(b - a, c - a).magnitude;
                sum += (a + b + c) / 3 * weight;
                area += weight;
            }
            if (area <= 1e-10f) throw new InvalidOperationException("절단면이 없는 조각");
            return piece.Object.transform.TransformPoint(sum / area);
        }

        public void SetColor(Color color)
        {
            SurfaceColor = color;
            int surfaces = Original.sharedMesh.subMeshCount;
            Tint(Original, color, surfaces);
            foreach (var renderer in Bodies) Tint(renderer, color, surfaces);
            foreach (var renderer in Limbs) Tint(renderer, color, surfaces);
        }

        void Tint(Renderer renderer, Color color, int surfaces)
        {
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < surfaces; i++)
            {
                renderer.GetPropertyBlock(block, i);
                bool tint = PlayerTintMaterialSlots == null || PlayerTintMaterialSlots.Length == 0 ||
                    Array.IndexOf(PlayerTintMaterialSlots, i) >= 0;
                var surface = tint ? color : new Color(1f, 1f, 1f, color.a);
                block.SetColor(BaseColor, surface);
                block.SetColor(ColorId, surface);
                renderer.SetPropertyBlock(block, i);
                block.Clear();
            }
        }

        public void Play(string motion, float speed = 1f)
        {
            if (Animator == null || !Animator.isActiveAndEnabled) return;
            Animator.applyRootMotion = false;
            Animator.speed = speed;
            int hash = UnityEngine.Animator.StringToHash(motion);
            if (stateHash == hash) return;
            if (!Animator.HasState(0, hash)) return;
            Motion = motion;
            stateHash = hash;
            if (motion == "Crawl")
            {
                Animator.Play(hash, 0, 0f);
                Animator.Update(0f);
            }
            else Animator.CrossFadeInFixedTime(hash, .12f);
        }

        /// <summary>현재 포즈의 Humanoid 뼈에 물리를 연결한다. 절단된 사지는 만들지 않는다.</summary>
        public void EnableRagdoll()
        {
            if (articulated || Animator == null || !Animator.isHuman) return;
            StopMotion();
            var hips = Animator.GetBoneTransform(HumanBodyBones.Hips);
            var chest = Animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = Animator.GetBoneTransform(HumanBodyBones.Spine);
            var head = Animator.GetBoneTransform(HumanBodyBones.Head);
            if (hips == null || chest == null || head == null) return;
            articulated = true;
            Original.updateWhenOffscreen = true;
            foreach (var renderer in Bodies) renderer.updateWhenOffscreen = true;
            var rootCollider = GetComponent<BoxCollider>();
            if (rootCollider != null) rootCollider.enabled = false;
            // 매달린 자세로 관절을 만들면 그 자세가 제한각의 기준이 된다. 중립 자세에서 구성한 뒤 현재 포즈로 돌린다.
            var pose = new Dictionary<Transform, Quaternion>();
            foreach (var pair in restRotations)
            {
                pose[pair.Key] = pair.Key.localRotation;
                pair.Key.localRotation = pair.Value;
            }
            RelaxShoulder(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm);
            RelaxShoulder(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm);
            var pelvis = AddRagdollBone(hips, chest.position, null, BodyPart.None, 8f, .14f);
            var torso = AddRagdollBone(chest, head.position, pelvis, BodyPart.None, 8f, .16f);
            AddRagdollBone(head, head.position + (head.position - chest.position).normalized * .2f,
                torso, BodyPart.None, 3f, RagdollHeadRadius);
            AddRagdollLimb(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, torso, BodyPart.LeftArm, RagdollArmRadius);
            AddRagdollLimb(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, torso, BodyPart.RightArm, RagdollArmRadius);
            AddRagdollLimb(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, pelvis, BodyPart.LeftLeg, RagdollLegRadius);
            AddRagdollLimb(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, pelvis, BodyPart.RightLeg, RagdollLegRadius);
            foreach (var pair in pose) pair.Key.localRotation = pair.Value;
            // 연결되지 않은 부위끼리도 자기 충돌을 막아 물리 시작 직후 튀거나 떨리지 않게 한다.
            for (int i = 0; i < ragdollParts.Count; i++)
                for (int j = i + 1; j < ragdollParts.Count; j++)
                    Physics.IgnoreCollision(ragdollParts[i].Collider, ragdollParts[j].Collider);
        }

        void RelaxShoulder(HumanBodyBones upperId, HumanBodyBones lowerId)
        {
            var upper = Animator.GetBoneTransform(upperId);
            var lower = Animator.GetBoneTransform(lowerId);
            if (upper == null || lower == null) return;
            Vector3 direction = (lower.position - upper.position).normalized;
            upper.rotation = Quaternion.FromToRotation(direction, (direction - transform.up).normalized) * upper.rotation;
        }

        void AddRagdollLimb(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones endBone,
            Rigidbody parent, BodyPart part, float radius)
        {
            if ((PlayerCondition.PartsForLostCount(Stage) & part) != 0) return;
            var upper = Animator.GetBoneTransform(upperBone);
            var lower = Animator.GetBoneTransform(lowerBone);
            var end = Animator.GetBoneTransform(endBone);
            if (upper == null || lower == null || end == null) return;
            var first = AddRagdollBone(upper, lower.position, parent, part, 2f, radius);
            AddRagdollBone(lower, end.position, first, part, 1f, radius * .8f);
        }

        Rigidbody AddRagdollBone(Transform bone, Vector3 end, Rigidbody parent, BodyPart part, float mass, float radius, bool track = true)
        {
            // 콜라이더는 뼈 로컬 축과 무관하게 실제 두 관절 사이를 덮는다.
            var shape = new GameObject("Ragdoll Collider");
            shape.layer = Physics.IgnoreRaycastLayer;
            shape.transform.SetPositionAndRotation((bone.position + end) * .5f, Quaternion.FromToRotation(Vector3.up, end - bone.position));
            shape.transform.SetParent(bone, true);
            var capsule = shape.AddComponent<CapsuleCollider>();
            capsule.radius = radius;
            capsule.height = Mathf.Max(radius * 2f, Vector3.Distance(bone.position, end));
            var body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.solverIterations = 12;
            if (parent != null)
            {
                var joint = bone.gameObject.AddComponent<CharacterJoint>();
                joint.connectedBody = parent;
                joint.axis = bone.InverseTransformDirection((end - bone.position).normalized);
                Vector3 swing = Vector3.ProjectOnPlane(transform.forward, end - bone.position).normalized;
                if (swing.sqrMagnitude < .01f) swing = Vector3.ProjectOnPlane(transform.right, end - bone.position).normalized;
                joint.swingAxis = bone.InverseTransformDirection(swing);
                joint.lowTwistLimit = new SoftJointLimit { limit = -70f };
                joint.highTwistLimit = new SoftJointLimit { limit = 70f };
                float swingLimit = mass >= 3f ? 40f : mass > 1f ? 70f : 100f;
                joint.swing1Limit = new SoftJointLimit { limit = swingLimit };
                joint.swing2Limit = new SoftJointLimit { limit = swingLimit };
                joint.enableProjection = true;
                joint.enablePreprocessing = false;
            }
            if (track) ragdollParts.Add((part, body, capsule));
            return body;
        }

        public Vector3 ReleaseSpin(Rigidbody body)
        {
            // 몸 전체는 세 축으로 구르고, 팔다리는 서로 다른 회전으로 풀려 일체로 굳지 않게 한다.
            Vector3 tumble = transform.TransformDirection(new Vector3(2.2f, 1.6f, 2.8f));
            if (body.mass >= 3f) return tumble;
            float side = Vector3.Dot(body.worldCenterOfMass - transform.position, transform.right) >= 0 ? 1f : -1f;
            return tumble + transform.forward * (side * (body.mass > 1f ? 4f : -6f));
        }

        public void StopMotion()
        {
            if (Animator != null) Animator.enabled = false;
            if (articulated) return;
            // 기존 한 덩어리 래그돌이 현재 남은 몸의 실제 스킨 범위로 바닥과 충돌하게 한다.
            var collider = GetComponent<BoxCollider>();
            if (collider == null) return;
            var source = Stage == 0 ? Original : Bodies[Stage - 1];
            var mesh = new Mesh();
            try
            {
                source.BakeMesh(mesh);
                var vertices = mesh.vertices;
                if (vertices.Length == 0) return;
                var matrix = transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                var bounds = new Bounds(matrix.MultiplyPoint3x4(vertices[0]), Vector3.zero);
                for (int i = 1; i < vertices.Length; i++) bounds.Encapsulate(matrix.MultiplyPoint3x4(vertices[i]));
                collider.center = bounds.center;
                collider.size = bounds.size;
            }
            finally { Destroy(mesh); }
        }
        public Rigidbody GetPieceBody(int index)
        {
            foreach (var piece in pieces)
                if (piece.Index == index && piece.Object != null) return piece.Body;
            return null;
        }
        void Update()
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
                if (pieces[i].Object == null || Time.time >= pieces[i].Expires) RemovePiece(i);
        }
        public void ClearPieces() { for (int i = pieces.Count - 1; i >= 0; i--) RemovePiece(i); }
        void RemovePiece(int index) { Dispose(pieces[index]); pieces.RemoveAt(index); }
        static void Dispose(Piece piece)
        {
            if (piece.Object != null) { piece.Object.SetActive(false); Destroy(piece.Object); }
            if (piece.Mesh != null) Destroy(piece.Mesh);
        }
        void OnDisable() { ClearPieces(); initialized = false; stateHash = 0; }
    }
}
