using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ⑧ 전문가 대기실 화면 표시(각 화면이 <see cref="ExpertService"/> 상태와 구매자 위치를 보고 똑같이 그린다).
    /// 기준: 대기실 2차 기획서 3.3.1 8번 비고("붉은 커튼에 도착하면 커튼이 좌우로 열리면서 황금빛이 쏟아지는 연출"), 4.2.3.2 이용 방식.
    /// - 붉은 커튼: 구매자가 커튼 앞에 오면 좌우로 열리고(예약 중), 들어가면 닫히고(이용 중), 복구가 끝나면 다시 열린다(퇴실).
    ///   커튼 자리의 보이지 않는 막이 늘 통로를 막아 걸어서는 못 들어간다. 구매자 위치는 공용 위치 동기화 값이라 모든 화면이 같게 연다.
    /// - 황금빛: 커튼이 열린 만큼 방 안 조명이 밝아져 쏟아져 나온다.
    /// - 커튼 앞 F: 구매자(예약 중)만 들어간다. 다른 경우에는 들어가지 않고 이유만 안내한다.
    /// - 쇠붙이 소리: 커튼이 닫혀 이용이 시작될 때와 복구가 끝날 때.
    /// - 커튼 위 표지: 오늘 담당·이용 상태("담당자 부재중" 포함).
    /// </summary>
    public sealed class ExpertRoomView : MonoBehaviour, IShopTarget, IShopPresentation
    {
        /// <summary>내 캐릭터를 실제로 옮긴 뒤 알린다. bool은 복귀 여부.</summary>
        public static event System.Action<ExpertRoomView, PlayerMover, bool> LocalMoved;
        [SerializeField] Transform curtainLeft;
        [SerializeField] Transform curtainRight;
        [SerializeField] Transform inside;
        [SerializeField] Transform outside;
        [SerializeField] TextMesh sign;
        [SerializeField] AudioSource sound;
        [SerializeField] Light glow;

        [Tooltip("커튼이 다 열렸을 때 한쪽이 옆으로 비키는 거리(m).")]
        [SerializeField, Min(.1f)] float slide = .85f;

        [Tooltip("다 열렸을 때 커튼 한쪽 폭 비율(주름져 모인 모습).")]
        [SerializeField, Range(.1f, 1f)] float bunched = .25f;

        [Tooltip("커튼이 열리고 닫히는 속도(초당 0~1).")]
        [SerializeField, Min(.1f)] float curtainSpeed = 1.6f;

        [Tooltip("구매자가 이 거리(m, 수평) 안에 오면 커튼이 열린다.")]
        [SerializeField, Min(.5f)] float arriveDistance = 2.5f;

        [Tooltip("커튼 앞 F가 닿는 수평 거리(m).")]
        float CommonReach => CurtainCall.Settings.GameSettings.Base.Interaction.Distance;

        [Tooltip("커튼이 다 열렸을 때 방 안 황금빛 조명 밝기.")]
        [SerializeField, Min(0f)] float glowIntensity = 6f;

        Vector3 leftClosed, rightClosed, leftScale, rightScale;
        float open;

        /// <summary>커튼이 열린 정도(0 닫힘 ~ 1 다 열림). 테스트 확인용.</summary>
        public float CurtainOpen => open;
        public Transform VisualRoot => curtainLeft != null ? curtainLeft : transform;
        // The lift's door meshes are centred above the player. Use the existing boarding point at
        // common body-centre height so an injured customer's reduced reach still reaches the entrance.
        public Vector3 PresentationPoint => outside != null
            ? outside.position + Vector3.up * (CurtainCall.Settings.GameSettings.Base.Character.CapsuleHeight * .5f)
            : transform.TransformPoint(CurtainCenterLocal());
        public bool IsAvailable => ExpertService.Instance != null && ExpertService.Instance.Phase == ExpertRoomPhase.Booked && ExpertService.Instance.LocalIsBuyer
            && (NetworkPlayer.Local == null || PlayerInteraction.CanInteract(NetworkPlayer.Local));
        public string ActionLabel => IsAvailable ? WaitingRoomText.Get("expert-room.ride-lift", "신체 복구 승강기 타기") : ExpertService.Instance != null ? RefusalText(ExpertService.Instance) : WaitingRoomText.Get("expert-room.absent", "담당자 부재중");

        /// <summary>그 위치(발)가 커튼 앞 F 거리 안인지.</summary>
        public bool InReach(Vector3 feet) => FlatDistance(feet) <= CommonReach;
        ExpertService bound;
        AudioClip clank;
        string signText;
        UIInputBlocker workInput;
        bool localInside;
        NetworkPlayer cabinOwner;
        PlayerMover cabinMover;
        bool cabinHadNetworkOwner;

        void Awake()
        {
            if (curtainLeft != null) { leftClosed = curtainLeft.localPosition; leftScale = curtainLeft.localScale; }
            if (curtainRight != null) { rightClosed = curtainRight.localPosition; rightScale = curtainRight.localScale; }
            clank = MakeClank();
            ApplyCurtain(0f);
        }

        void OnEnable() => ShopReach.Add(this);

        void OnDisable()
        {
            if (workInput != null) workInput.SetOpen(false);
            localInside = false;
            cabinOwner = null; cabinMover = null; cabinHadNetworkOwner = false;
            ShopReach.Remove(this);
            Bind(null);
        }

        void Update()
        {
            ShopReach.Pump();
            var service = ExpertService.Instance;
            if (service != bound) Bind(service);
            ReconcileLocalOccupancy();
            if (service == null) return;

            GuideAfterBooking(service);
            float target = ShouldOpen(service) ? 1f : 0f;
            open = Mathf.MoveTowards(open, target, curtainSpeed * Time.deltaTime);
            ApplyCurtain(open);
            UpdateSign(service);
        }

        bool ShouldOpen(ExpertService service) => service.Phase switch
        {
            ExpertRoomPhase.Booked => BuyerArrived(service.Buyer),
            ExpertRoomPhase.Exiting => true,
            _ => false,
        };

        // 구매자가 커튼 앞에 왔는지. 모든 화면이 같은 동기화 위치로 판단한다.
        bool BuyerArrived(ulong buyer)
        {
            foreach (var player in NetworkPlayer.All)
                if (player.IsSpawned && player.OwnerClientId == buyer) return FlatDistance(player.transform.position) <= arriveDistance;
            return false;
        }

        // 좌우 커튼: 열린 만큼 옆으로 비키며 주름져 좁아진다. 황금빛도 같은 만큼 밝힌다.
        void ApplyCurtain(float t)
        {
            float eased = Mathf.SmoothStep(0f, 1f, t);
            if (curtainLeft != null)
            {
                curtainLeft.localPosition = leftClosed + Vector3.left * (slide * eased);
                curtainLeft.localScale = new Vector3(leftScale.x * Mathf.Lerp(1f, bunched, eased), leftScale.y, leftScale.z);
            }
            if (curtainRight != null)
            {
                curtainRight.localPosition = rightClosed + Vector3.right * (slide * eased);
                curtainRight.localScale = new Vector3(rightScale.x * Mathf.Lerp(1f, bunched, eased), rightScale.y, rightScale.z);
            }
            if (glow != null)
            {
                glow.intensity = glowIntensity * eased;
                glow.enabled = eased > .01f;
            }
        }

        void Bind(ExpertService service)
        {
            if (localInside && bound != service) MoveLocal(outside, true);
            if (bound != null) bound.PhaseChanged -= HandlePhase;
            bound = service;
            cabinOwner = null; cabinMover = null; cabinHadNetworkOwner = false;
            if (bound != null) bound.PhaseChanged += HandlePhase;
            ReconcileLocalOccupancy();
        }

        void HandlePhase(ExpertRoomPhase before, ExpertRoomPhase after)
        {
            if (after == ExpertRoomPhase.Working || (before == ExpertRoomPhase.Working && after == ExpertRoomPhase.Exiting)) PlayClank();
            ReconcileLocalOccupancy();
        }

        // A phase event is not guaranteed when binding late or receiving a snapshot for a different buyer.
        // The current reservation and local life state own the cabin/input lock, not a previous event.
        void ReconcileLocalOccupancy()
        {
            if (bound == null || bound.Phase != ExpertRoomPhase.Working)
            {
                if (localInside) MoveLocal(outside, true);
                cabinOwner = null; cabinMover = null; cabinHadNetworkOwner = false;
                return;
            }
            // Keep this identity until Working ends. A despawned last player is not an offline
            // avatar, and a replacement Local must not inherit the old cabin's lock or teleport.
            bool sameOwner = !cabinHadNetworkOwner || cabinOwner != null
                && cabinOwner == NetworkPlayer.Local && cabinOwner.IsSpawned;
            bool workingHere = bound != null && bound.Phase == ExpertRoomPhase.Working && bound.LocalIsBuyer
                && sameOwner
                && (NetworkPlayer.Local != null ? PlayerInteraction.CanInteract(NetworkPlayer.Local) : NetworkPlayer.All.Count == 0);
            if (localInside && !workingHere) MoveLocal(outside, true);
            else if (!localInside && workingHere)
            {
                // 서비스 상태가 목마 해제 패킷보다 먼저 와도 Follow가 승강기 위치를 덮어쓰지 않는다.
                var stack = CurtainCall.Tightrope.PiggybackSystem.Current;
                if (stack != null && stack.IsInStack(NetworkPlayer.Local)) return;
                MoveLocal(inside, false);
            }
        }

        // 커튼 앞이면 누구나 F 대상이 된다(들어가는 건 구매자만, 나머지는 안내만).
        public float Reach(Vector3 feet, Component actor = null)
        {
            if (ExpertService.Instance == null) return -1f;
            return ExplorationReach.From(VisualRoot, PresentationPoint, feet, CommonReach, actor: actor);
        }

        internal float EntryDistance(NetworkPlayer player) => player != null
            ? PlayerInteraction.RestrictReach(Reach(player.transform.position, player), player) : -1f;

        internal bool CanBoard(NetworkPlayer player) => PlayerInteraction.CanInteract(player)
            && ExpertService.Instance is { Phase: ExpertRoomPhase.Booked } service
            && service.Buyer == player.OwnerClientId && EntryDistance(player) >= 0f;

        public void Interact()
        {
            if (NetworkPlayer.Local != null && !PlayerInteraction.CanInteract(NetworkPlayer.Local)) return;
            var service = ExpertService.Instance;
            if (service == null) return;
            if (service.Phase == ExpertRoomPhase.Booked && service.LocalIsBuyer) { service.RequestEnter(); return; }
            service.ShowLocalMessage(RefusalText(service));
        }

        static string RefusalText(ExpertService service) => service.Phase switch
        {
            ExpertRoomPhase.Booked => ExpertServiceBook.Explain(ExpertRefusal.NotBuyer),
            ExpertRoomPhase.Working or ExpertRoomPhase.Exiting => WaitingRoomText.Get("expert-room.occupied", "다른 사람이 신체 복구를 받고 있습니다."),
            _ => !ExpertService.IsServiceTime ? ExpertServiceBook.Explain(ExpertRefusal.InPerformance)
                : WaitingRoomText.Get("expert-room.buy-first", "신체 복구는 ③ 상점 수화기에서 먼저 구매하세요."),
        };

        float FlatDistance(Vector3 feet)
        {
            var d = feet - transform.TransformPoint(CurtainCenterLocal());
            return new Vector2(d.x, d.z).magnitude;
        }

        Vector3 CurtainCenterLocal()
        {
            if (curtainLeft == null || curtainRight == null) return Vector3.zero;
            return transform.InverseTransformPoint((curtainLeft.parent.TransformPoint(leftClosed) + curtainRight.parent.TransformPoint(rightClosed)) / 2f);
        }

        // 내 캐릭터만 옮긴다. 다른 화면은 공용 NetworkPlayer 위치 동기화로 같은 자리를 본다.
        void MoveLocal(Transform point, bool returned)
        {
            var mover = returned ? cabinMover : NetworkPlayer.Local != null ? NetworkPlayer.Local.GetComponent<PlayerMover>() : null;
            // Death/despawn or missing scene points must never retain this room's global input lock.
            if (returned)
            {
                localInside = false;
                if (workInput != null) workInput.SetOpen(false);
            }
            if (point == null) return;
            if (!returned && mover == null && NetworkPlayer.All.Count == 0) mover = FindFirstObjectByType<PlayerMover>();
            if (mover == null) return;
            localInside = !returned;
            if (!returned)
            {
                cabinOwner = NetworkPlayer.Local;
                cabinMover = mover;
                cabinHadNetworkOwner = cabinOwner != null;
            }
            if (workInput == null)
            {
                workInput = gameObject.AddComponent<UIInputBlocker>();
                workInput.BlockWhileActive = false;
            }
            workInput.SetOpen(localInside);
            // 공용 이동기는 출발점을 바꾸며 옮기는 방법만 연다. 마지막 순간이동이 커튼 앞(로비)이라 재시작 위치도 로비에 남는다.
            var movingPlayer = mover.GetComponent<NetworkPlayer>();
            bool normal = movingPlayer == null || PlayerInteraction.CanInteract(movingPlayer);
            bool bodyEnabled = mover.Body != null && mover.Body.enabled;
            CurtainCall.Tightrope.PiggybackSystem.Current?.GetComponent<CurtainCall.Tightrope.PiggybackFollow>()?.ReleaseForDevice(movingPlayer);
            mover.SetStartPose(point.position, point.rotation);
            if (!normal)
            {
                if (mover.Body != null) mover.Body.enabled = bodyEnabled;
                mover.SetControlEnabled(false);
            }
            if (LocalMoved != null)
                foreach (System.Action<ExpertRoomView, PlayerMover, bool> observer in LocalMoved.GetInvocationList())
                    try { observer(this, mover, returned); } catch (System.Exception error) { Debug.LogException(error, this); }
        }

        // 예약한 사람에게 한 번: 상점 창이 닫히면 화면을 승강기 쪽으로 돌리고 이용 안내 패널을 띄운다(2026-10-30 사용자).
        bool guidedBooking;
        void GuideAfterBooking(ExpertService service)
        {
            bool booked = service.Phase == ExpertRoomPhase.Booked && service.LocalIsBuyer;
            if (!booked) { guidedBooking = false; return; }
            if (guidedBooking || CurtainCall.Player.InputGate.IsBlocked) return;
            guidedBooking = true;
            TurnLocalToward(PresentationPoint);
            WaitingRoomGuidePanel.ShowAs(WaitingRoomText.Get("narration.expert-booked-title", "신체 복구 예약 완료"),
                WaitingRoomText.Format("narration.expert-booked-body", "신체 복구 {n}개를 예약했습니다.\n의료 서비스를 이용하려면 로비의 ⑧ 신체 복구 승강기로 이동해 F를 누르세요.", ("n", service.BookedLimbs)), 8f,WaitingRoomHudProfile.Current.medicalBooked);
        }

        static void TurnLocalToward(Vector3 point)
        {
            var player = NetworkPlayer.Local;
            if (player == null) return;
            var flat = Vector3.ProjectOnPlane(point - player.transform.position, Vector3.up);
            if (flat.sqrMagnitude < .01f) return;
            var look = Quaternion.LookRotation(flat.normalized);
            var director = CurtainCall.Cameras.CameraViewDirector.Current;
            // 마우스 시선 시점은 카메라를, 고정 시점은 캐릭터를 승강기 쪽으로 돌린다.
            if (director != null && director.UsesMouseLook) director.SetLookRotation(look);
            else player.transform.rotation = look;
        }

        void UpdateSign(ExpertService service)
        {
            if (sign == null) return;
            string text = StatusText;
            if (text == signText) return;
            signText = text;
            sign.text = text;
        }

        public string StatusText => ExpertService.Instance == null ? WaitingRoomText.Get("expert-room.status-preparing", "준비 중") : DescribeStatus(ExpertService.Instance);
        static string DescribeStatus(ExpertService service) => service.Phase switch
            {
                ExpertRoomPhase.Booked => WaitingRoomText.Format("expert-room.status-waiting-entry", "{player} 입장 대기", ("player", WaitingRoomShop.PlayerLabel(service.Buyer))),
                ExpertRoomPhase.Working => WaitingRoomText.Get("expert-room.status-in-use", "이용 중"),
                ExpertRoomPhase.Exiting => WaitingRoomText.Get("expert-room.status-done", "복구 완료"),
                _ => ExpertService.IsServiceTime ? WaitingRoomText.Get("expert-room.status-open", "신체 복구 영업 중") : WaitingRoomText.Get("expert-room.status-closed-show", "공연 중 휴업"),
            };

        void PlayClank()
        {
            if (sound == null || clank == null) return;
            CurtainCall.Settings.LocalAudioController.Register(sound, CurtainCall.Settings.LocalAudioCategory.Sfx);
            sound.PlayOneShot(clank);
        }

        // 쇠붙이 부딪는 날카로운 소리(임시, 에셋 없이 만든다): 금속성 배음 몇 개 + 짧은 잡음, 빠르게 사라진다.
        static AudioClip MakeClank()
        {
            const int rate = 44100;
            const float length = .7f;
            int count = (int)(rate * length);
            var data = new float[count];
            var random = new System.Random(8);
            float[] partials = { 523f, 1187f, 1931f, 2766f };
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float ring = 0f;
                for (int p = 0; p < partials.Length; p++) ring += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * Mathf.Exp(-t * (5f + p * 3f)) / (p + 1);
                float hit = ((float)random.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 60f);
                data[i] = Mathf.Clamp((ring * .5f + hit * .6f) * .8f, -1f, 1f);
            }
            var clip = AudioClip.Create("ExpertRoomClank", count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnGUI()
        {
            var service = ExpertService.Instance;
            // 안내: 예약한 사람에게 승강기까지 남은 거리를 화면 위에 계속 보여 준다(승강기 앞에 오면 아래 F 안내로 바뀐다).
            if (service != null && service.Phase == ExpertRoomPhase.Booked && service.LocalIsBuyer && !ReferenceEquals(ShopReach.Focused, this))
            {
                var me = CurtainCall.Network.PlayerSync.NetworkPlayer.Local;
                string hint = me != null
                    ? WaitingRoomText.Format("expert-room.booked-hint-distance", "신체 복구 예약됨 — 로비 ⑧ 신체 복구 승강기 앞에서 F · {meters}m", ("meters", Vector3.Distance(me.transform.position, transform.position).ToString("0", System.Globalization.CultureInfo.InvariantCulture)))
                    : WaitingRoomText.Get("expert-room.booked-hint", "신체 복구 예약됨 — 로비 ⑧ 신체 복구 승강기 앞에서 F");
                GUI.Label(new Rect(Screen.width / 2f - 230, 64, 520, 24), hint);
            }
            if (ShopReach.HasExternalPresentation || service == null || !ReferenceEquals(ShopReach.Focused, this)) return;
            var rect = new Rect(Screen.width / 2f - 230, Screen.height * .62f, 520, 24);
            GUI.Label(rect, service.Phase == ExpertRoomPhase.Booked && service.LocalIsBuyer
                ? "[F] ⑧ 신체 복구 승강기 타기"
                : $"[F] ⑧ 신체 복구 승강기 — {RefusalText(service)}");
        }
    }
}
