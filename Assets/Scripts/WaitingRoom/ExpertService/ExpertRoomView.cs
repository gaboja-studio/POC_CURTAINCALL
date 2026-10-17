using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// ⑧ 전문가 대기실 화면 표시(각 화면이 <see cref="ExpertService"/> 상태를 보고 똑같이 그린다).
    /// - 문: 예약·퇴실 때 위로 열리고, 비었거나 이용 중이면 닫힌다. 문 자리의 보이지 않는 막이 늘 통로를 막아 걸어서는 못 들어간다.
    /// - 입구 F: 구매자만 안내가 뜨고 누르면 들어간다(다른 사람은 "구매자만" 안내). 들어가면 방 안으로, 복구가 끝나면 문 앞으로 순간이동한다.
    /// - 쇠붙이 소리: 문이 닫혀 이용이 시작될 때와 복구가 끝날 때.
    /// - 문 위 표지: 오늘 담당·이용 상태("담당자 부재중" 포함).
    /// </summary>
    public sealed class ExpertRoomView : MonoBehaviour, IShopTarget
    {
        [SerializeField] Transform door;
        [SerializeField] Transform inside;
        [SerializeField] Transform outside;
        [SerializeField] TextMesh sign;
        [SerializeField] AudioSource sound;

        [Tooltip("문이 열릴 때 올라가는 높이(m).")]
        [SerializeField, Min(.5f)] float openLift = 2.8f;

        [Tooltip("문이 열리고 닫히는 속도(m/초).")]
        [SerializeField, Min(.5f)] float doorSpeed = 4f;

        [Tooltip("입구 F가 닿는 수평 거리(m).")]
        [SerializeField, Min(.5f)] float reach = 2f;

        Vector3 closedPosition;
        ExpertService bound;
        AudioClip clank;
        string signText;

        void Awake()
        {
            if (door != null) closedPosition = door.localPosition;
            clank = MakeClank();
        }

        void OnEnable() => ShopReach.Add(this);

        void OnDisable()
        {
            ShopReach.Remove(this);
            Bind(null);
        }

        void Update()
        {
            ShopReach.Pump();
            var service = ExpertService.Instance;
            if (service != bound) Bind(service);
            if (service == null) return;

            bool open = service.Phase is ExpertRoomPhase.Booked or ExpertRoomPhase.Exiting;
            if (door != null)
            {
                var target = closedPosition + (open ? Vector3.up * openLift : Vector3.zero);
                door.localPosition = Vector3.MoveTowards(door.localPosition, target, doorSpeed * Time.deltaTime);
            }
            UpdateSign(service);
        }

        void Bind(ExpertService service)
        {
            if (bound != null) bound.PhaseChanged -= HandlePhase;
            bound = service;
            if (bound != null) bound.PhaseChanged += HandlePhase;
        }

        void HandlePhase(ExpertRoomPhase before, ExpertRoomPhase after)
        {
            if (after == ExpertRoomPhase.Working || (before == ExpertRoomPhase.Working && after == ExpertRoomPhase.Exiting)) PlayClank();
            if (!bound.LocalIsBuyer) return;
            if (after == ExpertRoomPhase.Working) MoveLocal(inside);
            else if (after == ExpertRoomPhase.Exiting) MoveLocal(outside);
        }

        public float Reach(Vector3 feet)
        {
            var service = ExpertService.Instance;
            if (service == null || service.Phase != ExpertRoomPhase.Booked || !service.LocalIsBuyer) return -1f;
            float distance = FlatDistance(feet);
            return distance <= reach ? distance : -1f;
        }

        public void Interact() => ExpertService.Instance?.RequestEnter();

        float FlatDistance(Vector3 feet)
        {
            var at = door != null ? door.position : transform.position;
            var d = feet - at;
            return new Vector2(d.x, d.z).magnitude;
        }

        // 내 캐릭터만 옮긴다. 다른 화면은 공용 NetworkPlayer 위치 동기화로 같은 자리를 본다.
        static void MoveLocal(Transform point)
        {
            if (point == null) return;
            var mover = NetworkPlayer.Local != null ? NetworkPlayer.Local.GetComponent<PlayerMover>() : null;
            if (mover == null && NetworkPlayer.All.Count == 0) mover = FindFirstObjectByType<PlayerMover>();
            if (mover == null) return;
            // 공용 이동기는 출발점을 바꾸며 옮기는 방법만 연다. 마지막 순간이동이 문 앞(로비)이라 재시작 위치도 로비에 남는다.
            mover.SetStartPose(point.position, point.rotation);
        }

        void UpdateSign(ExpertService service)
        {
            if (sign == null) return;
            string text = service.Phase switch
            {
                ExpertRoomPhase.Booked => $"{WaitingRoomShop.PlayerLabel(service.Buyer)} 입장 대기",
                ExpertRoomPhase.Working => "이용 중",
                ExpertRoomPhase.Exiting => "복구 완료",
                _ => !ExpertService.IsNight ? "밤 정비 때 영업"
                    : ExpertService.Availability(ExpertKind.Medical) == ExpertRefusal.None ? "의료 전문가 대기 중"
                    : "의료: 담당자 부재중",
            };
            if (text == signText) return;
            signText = text;
            sign.text = text;
        }

        void PlayClank()
        {
            if (sound == null || clank == null) return;
            sound.PlayOneShot(clank);
        }

        // 쇠붙이 부딪는 소리(임시, 에셋 없이 만든다): 금속성 배음 몇 개 + 짧은 잡음, 빠르게 사라진다.
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
            if (service == null || service.Phase != ExpertRoomPhase.Booked || !ShopReach.TryLocalFeet(out var feet) || FlatDistance(feet) > reach) return;
            var rect = new Rect(Screen.width / 2f - 120, Screen.height * .62f, 300, 24);
            if (ReferenceEquals(ShopReach.Focused, this)) GUI.Label(rect, "[F] ⑧ 전문가 대기실 들어가기");
            else if (!service.LocalIsBuyer) GUI.Label(rect, "서비스를 구매한 사람만 들어갈 수 있습니다.");
        }
    }
}
