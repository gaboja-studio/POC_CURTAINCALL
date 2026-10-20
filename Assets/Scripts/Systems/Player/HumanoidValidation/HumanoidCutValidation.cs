using System;
using UnityEngine;

namespace CurtainCall.Player.Validation
{
    [RequireComponent(typeof(PlayerCondition))]
    public sealed class HumanoidCutValidation : MonoBehaviour
    {
        public SkinnedMeshRenderer Original;
        public SkinnedMeshRenderer Body;
        public SkinnedMeshRenderer Leg;
        public Animator Animator;
        public string[] States = { "Idle", "Walk", "Run", "Crouch", "CrouchWalk" };
        public bool AutoValidate;
        public int CompletedCycles;
        public string Failure;
        public float LastPoseError;
        public int StateIndex;
        public Vector3 LaunchImpulse = new Vector3(-2.5f, 3f, 0.8f);
        public Vector3 LastCutPoint { get; private set; }
        public Rigidbody PieceBody => piece != null ? piece.GetComponent<Rigidbody>() : null;

        PlayerCondition condition;
        GameObject piece;
        Mesh pieceMesh;
        bool applied;
        float nextAction;
        bool cutStage;
        Vector3 pieceStart;
        int frames;

        public int PieceCount => piece != null ? 1 : 0;

        void OnEnable()
        {
            condition = GetComponent<PlayerCondition>();
            condition.Changed += Changed;
            applied = false;
            if (Original != null) Apply(false);
            nextAction = Time.time + 0.8f;
        }

        void OnDisable()
        {
            if (condition != null) condition.Changed -= Changed;
            ClearPiece();
            if (Original != null) Original.enabled = true;
            if (Body != null) Body.enabled = false;
            if (Leg != null) Leg.enabled = false;
            applied = false;
        }

        void Changed(BodyPart previous, BodyPart current) => Apply((previous & BodyPart.LeftLeg) == 0);

        void Apply(bool spawn)
        {
            bool lost = (condition.LostParts & BodyPart.LeftLeg) != 0;
            if (lost && !applied && spawn) Spawn();
            if (!lost) ClearPiece();
            Original.enabled = !lost;
            Body.enabled = lost;
            Leg.enabled = false;
            applied = lost;
        }

        void Spawn()
        {
            ClearPiece();
            pieceMesh = new Mesh { name = "Task062 posed left leg" };
            Leg.BakeMesh(pieceMesh, false);
            var reference = new Mesh();
            Leg.BakeMesh(reference, false);
            LastPoseError = 0f;
            var b = reference.vertices;
            piece = new GameObject("Task062 detached left leg");
            piece.transform.SetPositionAndRotation(Leg.transform.position, Leg.transform.rotation);
            piece.transform.localScale = Leg.transform.lossyScale;
            var a = pieceMesh.vertices;
            for (int i = 0; i < a.Length; i++)
                LastPoseError = Mathf.Max(LastPoseError, Vector3.Distance(piece.transform.TransformPoint(a[i]), Leg.transform.TransformPoint(b[i])));
            Destroy(reference);
            piece.AddComponent<MeshFilter>().sharedMesh = pieceMesh;
            piece.AddComponent<MeshRenderer>().sharedMaterials = Leg.sharedMaterials;
            var box = piece.AddComponent<BoxCollider>();
            box.center = pieceMesh.bounds.center;
            box.size = pieceMesh.bounds.size;
            var rigidbody = piece.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            rigidbody.useGravity = true;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var cap = pieceMesh.GetTriangles(pieceMesh.subMeshCount - 1);
            Vector3 center = Vector3.zero;
            float area = 0f;
            for (int i = 0; i < cap.Length; i += 3)
            {
                var p = a[cap[i]];
                var q = a[cap[i + 1]];
                var r = a[cap[i + 2]];
                float weight = Vector3.Cross(q - p, r - p).magnitude;
                center += (p + q + r) / 3f * weight;
                area += weight;
            }
            LastCutPoint = piece.transform.TransformPoint(area > 0f ? center / area : box.center);
            rigidbody.AddForceAtPosition(transform.TransformDirection(LaunchImpulse), LastCutPoint, ForceMode.Impulse);
            pieceStart = piece.transform.position;
        }

        void ClearPiece()
        {
            if (piece != null)
            {
                piece.SetActive(false);
                Destroy(piece);
            }
            if (pieceMesh != null) Destroy(pieceMesh);
            piece = null;
            pieceMesh = null;
        }

        public void SelectState(int index)
        {
            StateIndex = index;
            Animator.Play(States[index], 0, 0.17f);
        }

        void Update()
        {
            frames++;
            if (!AutoValidate || !string.IsNullOrEmpty(Failure) || CompletedCycles >= 50 || Time.time < nextAction) return;
            try
            {
                if (!cutStage)
                {
                    if (Original == null || Body == null || Leg == null || !Animator.isHuman || !Animator.avatar.isValid)
                        throw new InvalidOperationException("Missing renderer or invalid Humanoid Avatar");
                    if (!Animator.GetCurrentAnimatorStateInfo(0).IsName(States[StateIndex]))
                        throw new InvalidOperationException("Animator state mismatch: " + States[StateIndex]);
                    condition.LosePart(BodyPart.LeftLeg);
                    if (!Body.enabled || Original.enabled || PieceCount != 1 || LastPoseError > 0.00001f)
                        throw new InvalidOperationException("Cut state / pose mismatch");
                    cutStage = true;
                    nextAction = Time.time + 2.5f;
                }
                else
                {
                    if (piece == null || Vector3.Distance(pieceStart, piece.transform.position) < 0.001f)
                        throw new InvalidOperationException("Detached piece did not move under physics");
                    condition.RestoreAll();
                    if (!Original.enabled || Body.enabled || PieceCount != 0)
                        throw new InvalidOperationException("Restore state / cleanup mismatch");
                    CompletedCycles++;
                    SelectState((CompletedCycles / 10) % States.Length);
                    cutStage = false;
                    nextAction = Time.time + 0.5f;
                }
            }
            catch (Exception exception)
            {
                Failure = exception.Message;
                Debug.LogException(exception, this);
            }
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 360, 360), GUI.skin.box);
            GUILayout.Label("Task062 · Body Type 1 · fixed left thigh");
            GUILayout.Label($"State: {States[StateIndex]} | cycles: {CompletedCycles}/50 | frames: {frames}");
            GUILayout.Label(string.IsNullOrEmpty(Failure) ? "Capped split / independent fragment / cut-point impulse" : Failure);
            if (GUILayout.Button(AutoValidate ? "Stop automated cycles" : "Run 50 automated cycles"))
            {
                AutoValidate = !AutoValidate;
                if (AutoValidate) { CompletedCycles = 0; Failure = null; cutStage = false; condition.RestoreAll(); SelectState(0); nextAction = Time.time + 0.8f; }
            }
            for (int i = 0; i < States.Length; i++)
                if (GUILayout.Button(States[i])) SelectState(i);
            if (GUILayout.Button("Cut left leg")) condition.LosePart(BodyPart.LeftLeg);
            if (GUILayout.Button("Restore")) condition.RestoreAll();
            GUILayout.EndArea();
        }
    }
}
