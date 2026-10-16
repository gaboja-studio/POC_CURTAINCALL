using System;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.HumanCannon
{
    /// <summary>현재 위치의 코스 구간만 표시한다. 체크포인트나 진행 상태는 변경하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class HumanCannonCourseSections : MonoBehaviour
    {
        public enum Section { Departure, First, Shelter, Second, Final }

        [Serializable]
        public struct Boundaries
        {
            public float FirstStartZ, ShelterStartZ, SecondStartZ, FinalStartZ, RaisedMinY;

            public Boundaries(float firstStartZ, float shelterStartZ, float secondStartZ, float finalStartZ, float raisedMinY)
            {
                FirstStartZ = firstStartZ;
                ShelterStartZ = shelterStartZ;
                SecondStartZ = secondStartZ;
                FinalStartZ = finalStartZ;
                RaisedMinY = raisedMinY;
            }
        }

        [SerializeField] Boundaries boundaries = new(18f, 72f, 90f, 141.5f, 5f);
        [SerializeField] TMP_FontAsset font;
        static readonly string[] labels = { "현재 구간  ·  출발", "현재 구간  ·  1구간", "현재 구간  ·  쉼터", "현재 구간  ·  2구간", "현재 구간  ·  최종구간" };
        GameObject overlay;
        TMP_Text label;
        Section shown;

        public void Configure(Boundaries values, TMP_FontAsset labelFont = null)
        {
            boundaries = values;
            if (labelFont != null) font = labelFont;
            if (label != null && font != null) label.font = font;
        }

        public static Section SectionAt(Vector3 position, Boundaries values)
        {
            // 리프트 아래 운반자가 위층 쉼터/2구간으로 표시되지 않도록 발 높이도 확인한다.
            if (position.y >= values.RaisedMinY)
            {
                if (position.z >= values.FinalStartZ) return Section.Final;
                if (position.z >= values.SecondStartZ) return Section.Second;
                if (position.z >= values.ShelterStartZ) return Section.Shelter;
            }
            return position.z >= values.FirstStartZ ? Section.First : Section.Departure;
        }

        void Update()
        {
            var local = NetworkPlayer.Local;
            // 전용 씬의 1인 연습은 LAN Waiting에서도 플레이하므로 세션 시작 버튼에 묶지 않는다.
            bool visible = local != null && local.IsSpawned && local.State == PlayerState.Normal
                && HumanCannonSystem.Current != null;
            if (!visible)
            {
                if (overlay != null) overlay.SetActive(false);
                return;
            }
            if (overlay == null) BuildOverlay();
            if (overlay == null) return;
            overlay.SetActive(true);
            var section = SectionAt(local.transform.position, boundaries);
            if (section == shown && label.text.Length > 0) return;
            shown = section;
            label.text = labels[(int)section];
        }

        void BuildOverlay()
        {
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/Pretendard-Medium/Pretendard-Medium SDF");
            if (font == null) return;
            overlay = new GameObject("HumanCannonCourseSection", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            overlay.transform.SetParent(transform, false);
            var canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("Section", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(overlay.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0, 22);
            rect.sizeDelta = new Vector2(290, 48);
            var background = panel.GetComponent<Image>();
            background.color = new Color(0.035f, 0.055f, 0.085f, 0.88f);
            background.raycastTarget = false;
            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panel.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16, 0);
            textRect.offsetMax = new Vector2(-12, 0);
            label = textObject.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 23;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = new Color(0.91f, 0.97f, 1f);
            label.raycastTarget = false;
            label.text = string.Empty;
        }

        void OnDisable()
        {
            if (overlay != null) overlay.SetActive(false);
        }

        void OnDestroy()
        {
            if (overlay != null) Destroy(overlay);
        }
    }
}
