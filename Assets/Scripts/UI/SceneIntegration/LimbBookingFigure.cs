using CurtainCall.Player;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 상점 서비스 탭 · 신체 복구 설명 칸의 인체 픽토그램(2026-11-26 M3 A안).
    /// - 초록 = 이번 예약으로 복구될 팔·다리, 빨강 = 잃었지만 이번엔 복구 안 함, 흰색 = 원래 있음. 머리·몸통은 항상 흰색.
    /// - 그림·범례 배치는 상점 프리팹(40_DetailTooltip/40_Content/07_LimbFigure)에서 고친다. 그림은 설명 칸 왼쪽 세로 한 줄을 쓰고,
    ///   보이는 동안 설명 글(스크롤 영역)을 그림 오른쪽·범례 아래로 옮긴다(<see cref="descriptionInset"/>).
    /// - 신체 그림(2026-11-26 사용자 제공 관절 마네킹): 같은 크기 캔버스의 흰색 레이어 6장. 색은 여기서 입힌다.
    /// - 만들기·다시 연결: Tools/Curtain Call/상점/신체 복구 인체 그림 넣기.
    /// </summary>
    public sealed class LimbBookingFigure : MonoBehaviour
    {
        [SerializeField] Image head, torso, rightArm, leftArm, rightLeg, leftLeg;
        [Tooltip("그림이 보이는 동안 옮길 설명 칸(스크롤 영역)")]
        [SerializeField] RectTransform descriptionViewport;
        [Tooltip("그림이 보이는 동안 설명 칸을 옮기는 거리(px). x = 왼쪽 끝을 오른쪽으로, y = 위쪽 끝을 아래로")]
        [SerializeField] Vector2 descriptionInset = new(112, 138);
        [SerializeField] Color restoreColor = new(.42f, .86f, .54f);
        [SerializeField] Color lostColor = new(.93f, .37f, .33f);
        [SerializeField] Color intactColor = new(.86f, .90f, .89f);
        [Tooltip("범례 색 점(초록·빨강·흰색 순서)")]
        [SerializeField] Image[] legendDots;

        bool shifted, painted;
        Vector2 viewportMin, viewportMax;

        public BodyPart Lost { get; private set; }
        public BodyPart Restoring { get; private set; }
        public bool Showing => gameObject.activeSelf;

        /// <summary>최근에 잃은 것부터 <paramref name="count"/>개(복구 순서 = 서버 <see cref="PlayerCondition.NextRestore"/>).</summary>
        public static BodyPart RestoreSet(BodyPart lost, int count)
        {
            lost &= PlayerCondition.AllLimbs;
            BodyPart chosen = BodyPart.None;
            for (int i = 0; i < count; i++)
            {
                var next = PlayerCondition.NextRestore(lost & ~chosen);
                if (next == BodyPart.None) break;
                chosen |= next;
            }
            return chosen;
        }

        public void Show(BodyPart lost, BodyPart restoring)
        {
            lost &= PlayerCondition.AllLimbs; restoring &= lost;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Shift(true);
            if (painted && lost == Lost && restoring == Restoring) return;
            Lost = lost; Restoring = restoring; painted = true;
            Paint(head, BodyPart.None); Paint(torso, BodyPart.None);
            Paint(rightArm, BodyPart.RightArm); Paint(leftArm, BodyPart.LeftArm);
            Paint(rightLeg, BodyPart.RightLeg); Paint(leftLeg, BodyPart.LeftLeg);
            if (legendDots != null)
                for (int i = 0; i < legendDots.Length; i++)
                    if (legendDots[i] != null) legendDots[i].color = i == 0 ? restoreColor : i == 1 ? lostColor : intactColor;
        }

        // 프리팹에는 보이는 상태로 저장한다(편집용). 게임에서는 신체 복구를 고를 때만 켠다.
        void Awake() { if (!painted) gameObject.SetActive(false); }

        public void Hide()
        {
            Shift(false);
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public Color ColorOf(BodyPart part) =>
            (Restoring & part) != 0 ? restoreColor : (Lost & part) != 0 ? lostColor : intactColor;

        void Paint(Image image, BodyPart part)
        {
            if (image != null) image.color = part == BodyPart.None ? intactColor : ColorOf(part);
        }

        void Shift(bool on)
        {
            if (descriptionViewport == null || shifted == on) return;
            if (on)
            {
                viewportMin = descriptionViewport.offsetMin; viewportMax = descriptionViewport.offsetMax;
                descriptionViewport.offsetMin = new Vector2(viewportMin.x + descriptionInset.x, viewportMin.y);
                descriptionViewport.offsetMax = new Vector2(viewportMax.x, viewportMax.y - descriptionInset.y);
            }
            else { descriptionViewport.offsetMin = viewportMin; descriptionViewport.offsetMax = viewportMax; }
            shifted = on;
        }

        void OnDisable() => Shift(false);
    }
}
