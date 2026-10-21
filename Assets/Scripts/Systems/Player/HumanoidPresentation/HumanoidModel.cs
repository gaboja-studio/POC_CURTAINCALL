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
        public int Stage { get; private set; }
        public int PieceCount => pieces.Count;
        public Color SurfaceColor { get; private set; } = Color.white;
        public string Motion { get; private set; }

        sealed class Piece
        {
            public int Index;
            public GameObject Object;
            public Mesh Mesh;
            public float Expires;
        }

        readonly List<Piece> pieces = new();
        MaterialPropertyBlock block;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        Vector3 basePosition;
        bool initialized;
        int stateHash;

        bool positionCaptured;

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
                initialized = true;
                // 분리된 래그돌의 월드 위치는 더 이상 슬롯 기준으로 되돌리지 않는다.
                if (transform.parent != null) transform.localPosition = basePosition + Vector3.up * (next >= 2 ? CrawlHeightOffset : 0f);
                // 정지한 기어가기의 속도는 거의 0이므로 전환을 기다리면 서 있는 포즈가 남는다.
                if (next >= 2 && next < 4) Play("Crawl", .0001f);
                if (Animator != null && !Animator.enabled) StopMotion();
                foreach (var piece in pending)
                {
                    piece.Object.SetActive(true);
                    var rb = piece.Object.GetComponent<Rigidbody>();
                    var settings = GameSettings.Base.BodyDamage;
                    Vector3 outward = CutPoint(piece) - transform.position;
                    outward.y = 0;
                    outward = outward.sqrMagnitude > .0001f ? outward.normalized : transform.right;
                    rb.linearVelocity = body != null ? body.velocity : Vector3.zero;
                    rb.angularVelocity = UnityEngine.Random.onUnitSphere * settings.LimbSpin;
                    rb.AddForceAtPosition(outward * settings.LimbFlingSpeed + Vector3.up * settings.LimbFlingUpSpeed,
                        CutPoint(piece), ForceMode.Impulse);
                    var collider = piece.Object.GetComponent<Collider>();
                    foreach (var other in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                        if (other != collider && (other.isTrigger || other.attachedRigidbody != null || other is CharacterController))
                            Physics.IgnoreCollision(collider, other);
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
                piece.Object.AddComponent<MeshFilter>().sharedMesh = piece.Mesh;
                var renderer = piece.Object.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                Tint(renderer, SurfaceColor, Original.sharedMesh.subMeshCount);
                var collider = piece.Object.AddComponent<BoxCollider>();
                collider.center = piece.Mesh.bounds.center;
                collider.size = piece.Mesh.bounds.size;
                var rb = piece.Object.AddComponent<Rigidbody>();
                rb.mass = 1;
                rb.useGravity = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                if (body != null) Physics.IgnoreCollision(collider, body);
                foreach (var other in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                    if (other != collider && (other.isTrigger || other.attachedRigidbody != null || other is CharacterController))
                        Physics.IgnoreCollision(collider, other);
                float lifetime = GameSettings.Base.BodyDamage.LimbLifetime;
                piece.Expires = lifetime > 0 ? Time.time + lifetime : float.PositiveInfinity;
                CutPoint(piece);
                return piece;
            }
            catch { Dispose(piece); throw; }
        }

        static Vector3 CutPoint(Piece piece)
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
                block.SetColor(BaseColor, color);
                block.SetColor(ColorId, color);
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

        public void StopMotion()
        {
            if (Animator != null) Animator.enabled = false;
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
                if (piece.Index == index && piece.Object != null) return piece.Object.GetComponent<Rigidbody>();
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
