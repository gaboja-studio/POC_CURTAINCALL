using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 소지 칸 아래쪽 내구도 바(2026-11-02 사용자, Task-094). 내구도 1을 한 칸으로 나눠 남은 횟수만큼 채운다.
    /// CommonMenuUI 씬의 슬롯 템플릿(00_SlotTemplate/70_Durability)에 배치하고, 칸 모양·간격·색은 씬에서 고친다.
    /// - 칸 수 = 새 제품 내구도(남은 횟수가 더 크면 그 값). 내구도가 없는 기물(UV 등·열쇠)은 바를 숨긴다.
    /// - 칸 그림은 비활성 자식 <see cref="segmentTemplate"/>을 복제한다. 배치는 이 오브젝트의 HorizontalLayoutGroup이 맡는다.
    /// - 사용 연출: 방금 쓴 칸을 밝게 번쩍인 뒤 빈 칸 색으로 꺼뜨린다(<see cref="PlayConsume"/>).
    /// </summary>
    public sealed class InventorySlotDurabilityBar : MonoBehaviour
    {
        [Tooltip("칸 하나의 그림. 비활성으로 두면 남은 횟수만큼 복제한다")]
        [SerializeField] Image segmentTemplate;
        [Tooltip("바 전체를 켜고 끄는 대상. 비우면 이 오브젝트")]
        [SerializeField] GameObject visibleRoot;
        [SerializeField] Color filledColor = new(1f, .84f, .38f, 1f);
        [Tooltip("낡은 상태(기물 목록 wornAtOrBelow 이하)일 때 채운 칸 색")]
        [SerializeField] Color wornColor = new(1f, .45f, .25f, 1f);
        [SerializeField] Color emptyColor = new(1f, 1f, 1f, .2f);
        [Header("사용 연출")]
        [SerializeField] Color consumeFlashColor = Color.white;
        [SerializeField, Min(0f)] float consumeFlashSeconds = .08f;
        [SerializeField, Min(0f)] float consumeFadeSeconds = .45f;
        [SerializeField] float consumePunchScale = .6f;

        readonly List<Image> segments = new();
        int shownUses = -1, shownMax = -1;
        bool shownWorn;

        GameObject Root => visibleRoot != null ? visibleRoot : gameObject;

        void Awake()
        {
            if (segmentTemplate != null) segmentTemplate.gameObject.SetActive(false);
        }

        /// <summary>내구도가 없는 기물·빈 칸.</summary>
        public void Hide()
        {
            Root.SetActive(false);
            shownUses = shownMax = -1;
        }

        /// <summary>남은 <paramref name="uses"/> / 전체 <paramref name="max"/> 칸.</summary>
        public void Show(int uses, int max, bool worn)
        {
            if (segmentTemplate == null || max <= 0) { Hide(); return; }
            Root.SetActive(true);
            if (uses == shownUses && max == shownMax && worn == shownWorn) return;
            EnsureCount(max);
            Color fill = worn ? wornColor : filledColor;
            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].DOKill(complete: false);
                segments[i].rectTransform.DOKill(complete: false);
                segments[i].rectTransform.localScale = Vector3.one;
                segments[i].color = i < uses ? fill : emptyColor;
            }
            shownUses = uses; shownMax = max; shownWorn = worn;
        }

        /// <summary>방금 1 줄어든 칸(남은 횟수 <paramref name="usesAfter"/> 위치)을 번쩍인 뒤 비운다. <see cref="Show"/> 뒤에 부른다.</summary>
        public void PlayConsume(int usesAfter)
        {
            if (usesAfter < 0 || usesAfter >= segments.Count) return;
            var seg = segments[usesAfter];
            seg.DOKill(complete: false);
            seg.rectTransform.DOKill(complete: true);
            seg.color = consumeFlashColor;
            DOTween.Sequence().SetTarget(seg).SetUpdate(true)
                .AppendInterval(consumeFlashSeconds)
                .Append(seg.DOColor(emptyColor, consumeFadeSeconds).SetEase(Ease.OutQuad));
            seg.rectTransform.DOPunchScale(Vector3.one * consumePunchScale, consumeFlashSeconds + consumeFadeSeconds, 6, .6f).SetUpdate(true);
        }

        void EnsureCount(int count)
        {
            segments.RemoveAll(s => s == null);
            while (segments.Count < count)
            {
                var seg = Instantiate(segmentTemplate, segmentTemplate.transform.parent, false);
                seg.name = "Segment_" + (segments.Count + 1);
                seg.gameObject.SetActive(true);
                segments.Add(seg);
            }
            for (int i = 0; i < segments.Count; i++) segments[i].gameObject.SetActive(i < count);
            if (segments.Count > count)
            {
                for (int i = segments.Count - 1; i >= count; i--) { segments[i].DOKill(); Destroy(segments[i].gameObject); }
                segments.RemoveRange(count, segments.Count - count);
            }
        }

        void OnDisable()
        {
            foreach (var seg in segments) if (seg != null) { seg.DOKill(complete: true); seg.rectTransform.DOKill(complete: true); }
        }
    }
}
