using CurtainCall.HumanCannon;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using UnityEngine;

namespace CurtainCall.Tightrope
{
    /// <summary>줄별 봉의 배치와 도착 알림. 모든 봉은 공통 점유·조작·도약 서비스를 사용한다.</summary>
    [DefaultExecutionOrder(-55)]
    public sealed partial class TightropeSwingPole : MonoBehaviour
    {
        TightropeCourse course;
        TightropeRun run;
        HumanCannonPole physicalPole;
        HumanCannonPoleSystem common;
        int sharedIndex = -1, round = -1;
        double feedbackUntil;
        string feedback;
        public static TightropeSwingPole Current { get; private set; }
        public static event System.Action<ulong, TightropeSwingPole> ServerCrossed;
        public float AirControlSpeed => GameSettings.Poles.AirControlSpeed;
        public float LandingDistance => next != null ? next.EntryDistance : GapTo + SwingPoleMath.ExitOffset;
        bool OwnOccupied => Active && common != null && common.OccupantCount(sharedIndex) > 0;
        float OwnAngle => common != null ? common.AngleAt(sharedIndex) : 0;
        bool OwnCanLetGo => OwnOccupied && course.GetDistance(PredictLanding(true)) >= (next != null ? next.EntryDistance - .5f : GapTo + .35f);
        public bool IsOccupied => LocalPole.OwnOccupied;
        public float SwingAngle => LocalPole.OwnAngle;
        public bool CanLetGo => LocalPole.OwnCanLetGo;
        public bool CanGrabPole(NetworkPlayer player) => Active && common != null && common.CanApproachPole(sharedIndex, player);
        public string LocalFeedback => LatestFeedback();
        bool Active => isActiveAndEnabled && course != null && HasGap && course.IsGameplayActive && run != null && run.State == TightropeRunState.Running;
        double Clock => common != null ? common.Clock : Time.timeAsDouble;
        double startedAt => common != null ? common.HoldUntil(sharedIndex) - GameSettings.Poles.HoldSeconds : 0;
        PlayerMover flightMover => OwnFlight(NetworkPlayer.Local) ? NetworkPlayer.Local.GetComponent<PlayerMover>() : null;

        void Awake()
        {
            course = GetComponentInParent<TightropeCourse>(); run = course != null ? course.GetComponent<TightropeRun>() : null;
            common = HumanCannonPoleSystem.Ensure(course != null ? course.gameObject : gameObject);
            var pivot = new GameObject("HumanCannonPole physics (runtime)"); pivot.transform.SetParent(transform, false);
            physicalPole = pivot.AddComponent<HumanCannonPole>(); physicalPole.InteractionAvailable = false;
            sharedIndex = common.Register(physicalPole);
            PlacePivot(); EnsureLanePoles();
        }
        void OnEnable()
        {
            if (common != null) common.Landed += OnLanded;
            if (coordinator != null) return;
            Current = this;
            foreach (var pole in poles) if (pole != this && pole != null) pole.enabled = true;
        }
        void PlacePivot()
        {
            if (course == null || physicalPole == null) return;
            physicalPole.transform.SetPositionAndRotation(course.GetLanePosition(assignedLane, EntryDistance)
                + Vector3.up * GameSettings.Poles.HangLength, Quaternion.LookRotation(course.Forward));
        }
        void Update()
        {
            EnsureLanePoles(); PlacePivot();
            if (run == null && course != null) run = course.GetComponent<TightropeRun>();
            int generation = run != null ? run.Round : -1;
            if (generation != round) { common.Deactivate(sharedIndex); round = generation; }
            if (!Active && physicalPole.InteractionAvailable) common.Deactivate(sharedIndex);
            physicalPole.InteractionAvailable = Active;
            physicalPole.PresentationVisible = isActiveAndEnabled && HasGap && course.IsTraversalActive;
        }
        public bool TryHandleInput(PlayerControlContext context, in PlayerInputFrame input) => Active && common != null && common.TryHandleInput(context, input);
        void OnLanded(int index, ulong actor, Vector3 position, bool safe)
        {
            if (index == sharedIndex && Active && safe && course.GetDistance(position) >= GapTo) ServerCrossed?.Invoke(actor, this);
        }
        Vector3 WorldFeet(float angle, int slot) => physicalPole.HangingPosition(angle);
        Vector3 PredictLanding(bool withInput)
        {
            Vector3 origin = WorldFeet(OwnAngle, assignedLane);
            Vector3 velocity = physicalPole.ReleaseVelocity(OwnAngle, common != null ? common.SpeedAt(sharedIndex) : 0);
            if (withInput) velocity += course.Forward * AirControlSpeed;
            float gravity = GameSettings.Base.Character.Gravity;
            float drop = Mathf.Max(0, origin.y - course.transform.position.y);
            float time = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2 * gravity * drop)) / gravity;
            return origin + velocity * time + Vector3.down * (.5f * gravity * time * time);
        }
        void LateUpdate() => UpdateViews();
        void OnDisable()
        {
            if (common != null) { common.Landed -= OnLanded; common.Deactivate(sharedIndex); }
            if (Current == this) Current = null;
            if (coordinator == null) foreach (var pole in poles) if (pole != this && pole != null) pole.enabled = false;
            if (viewRoot != null) viewRoot.gameObject.SetActive(false);
        }
        void OnDestroy()
        {
            DestroyViews();
            if (coordinator == null) foreach (var pole in poles) if (pole != this && pole != null) Destroy(pole.gameObject);
        }
    }
}
