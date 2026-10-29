using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CurtainCall.Player.Validation
{
    [RequireComponent(typeof(PlayerCondition))]
    public sealed class SequentialCutValidation : MonoBehaviour
    {
        public SkinnedMeshRenderer Original;
        public SkinnedMeshRenderer[] Bodies;
        public SkinnedMeshRenderer[] Limbs;
        public Animator Animator;
        public Vector3 LaunchImpulse = new(-2.5f, 3f, .8f);
        public AnimationClip[] IdleClips = new AnimationClip[5];
        public AnimationClip[] MoveClips = new AnimationClip[5];
        public AnimationClip[] LowMoveClips = new AnimationClip[5];
        public AnimationClip[] JumpClips = new AnimationClip[5];
        public string Failure;
        public string MotionStatus { get; private set; }
        public int Stage { get; private set; }
        public int PieceCount => pieces.Count;
        public float LastPoseError { get; private set; }
        public bool IsDead => Stage == 4;

        sealed class Piece
        {
            public int Index;
            public GameObject Object;
            public Mesh Mesh;
        }

        readonly List<Piece> pieces = new();
        PlayerCondition condition;
        PlayableGraph graph;
        int requestedMotion;

        void OnEnable()
        {
            condition = GetComponent<PlayerCondition>();
            condition.Changed += Changed;
            Apply(condition.LostParts, condition.LostParts, false);
        }

        void OnDisable()
        {
            if (condition != null) condition.Changed -= Changed;
            ClearPieces();
            StopGraph();
            if (Original != null) Original.enabled = true;
            if (Bodies != null) foreach (var body in Bodies) if (body != null) body.enabled = false;
            if (Limbs != null) foreach (var limb in Limbs) if (limb != null) limb.enabled = false;
        }

        void Changed(BodyPart previous, BodyPart current) => Apply(previous, current, true);

        static int StageFor(BodyPart parts)
        {
            for (int i = 0; i <= 4; i++)
                if (PlayerCondition.PartsForLostCount(i) == parts) return i;
            throw new InvalidOperationException($"Unsupported non-sequential mask: {parts}");
        }

        void Apply(BodyPart previous, BodyPart current, bool spawn)
        {
            var pending = new List<Piece>();
            try
            {
                int next = StageFor(current);
                if (Original == null || Animator == null || Bodies == null || Bodies.Length != 4 ||
                    Limbs == null || Limbs.Length != 4)
                    throw new InvalidOperationException("Sequential validation references are incomplete.");
                for (int i = 0; i < 4; i++)
                    if (Bodies[i] == null || Limbs[i] == null) throw new InvalidOperationException($"Missing stage {i} renderer.");
                if (spawn)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var part = PlayerCondition.CutOrder[i];
                        if ((previous & part) == 0 && (current & part) != 0) pending.Add(BakePiece(i));
                    }
                }
                for (int i = pieces.Count - 1; i >= 0; i--)
                    if ((current & PlayerCondition.CutOrder[pieces[i].Index]) == 0)
                    {
                        Dispose(pieces[i]);
                        pieces.RemoveAt(i);
                    }
                Original.enabled = next == 0;
                for (int i = 0; i < 4; i++)
                {
                    Bodies[i].enabled = next == i + 1;
                    Limbs[i].enabled = false;
                }
                Stage = next;
                Failure = null;
                foreach (var piece in pending)
                {
                    piece.Object.SetActive(true);
                    var rigidbody = piece.Object.GetComponent<Rigidbody>();
                    rigidbody.AddForceAtPosition(transform.TransformDirection(LaunchImpulse), CutPoint(piece), ForceMode.Impulse);
                    pieces.Add(piece);
                }
                pending.Clear();
                SelectMotion(requestedMotion);
            }
            catch (Exception exception)
            {
                foreach (var piece in pending) Dispose(piece);
                Failure = exception.Message;
                MotionStatus = "ERROR: " + Failure;
                StopGraph();
                if (Animator != null) Animator.speed = 0;
                Debug.LogError(MotionStatus, this);
            }
        }

        Piece BakePiece(int index)
        {
            var piece = new Piece { Index = index, Mesh = new Mesh { name = "Sequential baked fragment" } };
            try
            {
                var source = Limbs[index];
                source.BakeMesh(piece.Mesh, false);
                piece.Object = new GameObject("Task062 detached " + PlayerCondition.CutOrder[index]);
                piece.Object.SetActive(false);
                piece.Object.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                piece.Object.transform.localScale = source.transform.lossyScale;
                piece.Object.AddComponent<MeshFilter>().sharedMesh = piece.Mesh;
                piece.Object.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
                var collider = piece.Object.AddComponent<BoxCollider>();
                collider.center = piece.Mesh.bounds.center;
                collider.size = piece.Mesh.bounds.size;
                var rigidbody = piece.Object.AddComponent<Rigidbody>();
                rigidbody.mass = 1;
                rigidbody.useGravity = true;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                LastPoseError = 0;
                var vertices = piece.Mesh.vertices;
                foreach (var vertex in vertices)
                    LastPoseError = Mathf.Max(LastPoseError, Vector3.Distance(source.transform.TransformPoint(vertex), piece.Object.transform.TransformPoint(vertex)));
                if (LastPoseError > .00001f) throw new InvalidOperationException("Fragment world pose mismatch.");
                CutPoint(piece);
                return piece;
            }
            catch
            {
                Dispose(piece);
                throw;
            }
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
            if (area <= 1e-10f) throw new InvalidOperationException("Fragment has no valid cap surface.");
            return piece.Object.transform.TransformPoint(sum / area);
        }

        public void CutNext()
        {
            if (string.IsNullOrEmpty(Failure) && !IsDead) condition.LosePart(PlayerCondition.NextFixedCut(condition.LostParts));
        }

        public void RestorePrevious() => condition.SetLostParts(condition.LostParts & ~PlayerCondition.NextRestore(condition.LostParts));
        public void RestoreAll() => condition.RestoreAll();
        public Rigidbody GetPieceBody(int index)
        {
            foreach (var piece in pieces) if (piece.Index == index) return piece.Object.GetComponent<Rigidbody>();
            return null;
        }

        public bool SelectMotion(int motion)
        {
            if (motion < 0 || motion > 3) throw new ArgumentOutOfRangeException(nameof(motion));
            requestedMotion = motion;
            StopGraph();
            if (!string.IsNullOrEmpty(Failure)) return false;
            if (IsDead)
            {
                Animator.speed = 0;
                MotionStatus = "Dead / no movement or jump (death clip not supplied)";
                return false;
            }
            AnimationClip[] clips = motion switch { 0 => IdleClips, 1 => MoveClips, 2 => LowMoveClips, _ => JumpClips };
            var clip = clips != null && clips.Length > Stage ? clips[Stage] : null;
            if (clip == null)
            {
                if (Stage == 0 && motion != 3)
                {
                    Animator.speed = 1;
                    Animator.Play(motion == 0 ? "Idle" : motion == 1 ? "Walk" : "CrouchWalk", 0, 0);
                    MotionStatus = "Normal reference / " + motion;
                    return true;
                }
                Animator.speed = 0;
                MotionStatus = $"MISSING clip: stage {Stage}, motion {motion}. Pose held; not injury animation.";
                return false;
            }
            Animator.speed = 1;
            Animator.applyRootMotion = false;
            AnimationPlayableUtilities.PlayClip(Animator, clip, out graph);
            MotionStatus = clip.name + " / preview only";
            return true;
        }

        void StopGraph() { if (graph.IsValid()) graph.Destroy(); }
        static void Dispose(Piece piece)
        {
            if (piece.Object != null) { piece.Object.SetActive(false); Destroy(piece.Object); }
            if (piece.Mesh != null) Destroy(piece.Mesh);
        }
        void ClearPieces() { foreach (var piece in pieces) Dispose(piece); pieces.Clear(); }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 680, 240), GUI.skin.box);
            GUILayout.Label($"Sequential capped cuts: {Stage}/4 | fragments: {PieceCount}");
            GUILayout.Label("Order: left leg > right leg > left arm > right arm (death)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Next cut")) CutNext();
            if (GUILayout.Button("Restore previous")) RestorePrevious();
            if (GUILayout.Button("Restore all")) RestoreAll();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Idle")) SelectMotion(0);
            if (GUILayout.Button("Move")) SelectMotion(1);
            if (GUILayout.Button("Low move")) SelectMotion(2);
            if (GUILayout.Button("Jump preview")) SelectMotion(3);
            GUILayout.EndHorizontal();
            GUILayout.Label(MotionStatus);
            GUILayout.Label("Missing clips are NOT replaced with ordinary walking or jumping.");
            if (!string.IsNullOrEmpty(Failure)) GUILayout.Label(Failure);
            GUILayout.EndArea();
        }
    }
}
