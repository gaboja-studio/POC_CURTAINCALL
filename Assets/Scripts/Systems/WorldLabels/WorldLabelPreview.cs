using System;
using CurtainCall.UI.WorldLabelsV2;
using TMPro;
using UnityEngine;

namespace CurtainCall.WorldLabels
{
    /// <summary>독립 합성 검토용 조작만 제공하며 플레이어 검색이나 실제 게임 연결은 하지 않습니다.</summary>
    public sealed class WorldLabelPreview : MonoBehaviour
    {
        [SerializeField] private WorldLabelView[] views;
        [SerializeField] private TMP_Text status;
        [SerializeField] private Transform observer;
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Sprite[] defaultIcons;
        private bool alternate, surfacesHidden, billboard, dark = true;
        private int distanceStep;
        private readonly float[] distances = { 3f, 2f, 2.2f, 2.41f, 2.2f, 1.9f };
        public WorldLabelView[] Views => views == null ? Array.Empty<WorldLabelView>() : (WorldLabelView[])views.Clone();
        public Transform Observer => observer;
        public Camera PreviewCamera => previewCamera;
        public void Configure(WorldLabelView[] labels, TMP_Text report, Transform localObserver, Camera camera)
        {
            if (labels == null || labels.Length != 5 || report == null || localObserver == null || camera == null) throw new ArgumentException("Five labels and explicit preview references are required.");
            views = (WorldLabelView[])labels.Clone(); status = report; observer = localObserver; previewCamera = camera;
            defaultIcons = new Sprite[views.Length];
            for (int i = 0; i < views.Length; i++) defaultIcons[i] = views[i].ContentIcon;
        }
        private void Start() { ResetPreview(); }
        private bool Ready => Application.IsPlaying(gameObject) && views != null && views.Length == 5;
        public void Content()
        {
            if (!Ready) return;
            alternate = !alternate;
            foreach (var view in views)
            {
                string title = alternate ? (view.Kind == WorldLabelKind.Shelf ? "12" : "안내") : DefaultTitle(view.Kind);
                string body = view.Kind == WorldLabelKind.Paper ? (alternate ? "짧은 본문입니다.\n통로를 확인하세요." : "무대가 조용해지면\n뒤쪽 통로를 살펴봐.\n서두르지 마.") : null;
                if (!view.TrySetContent(title, body, view.ContentIcon)) { Report(view.LastIssue); return; }
            }
            Report("검토 문구 전환 / 실제 게임 문구 아님");
        }
        public void MinimumSize()
        {
            if (!Ready) return;
            foreach (var view in views)
            {
                // 최소 크기에 모든 기본 문구가 들어가는 것은 아니므로 짧은 일반 문구로 확인합니다.
                if (!view.TrySetContent(view.Kind == WorldLabelKind.Shelf ? "03" : "문", view.Kind == WorldLabelKind.Paper ? "한 줄" : null) ||
                    !view.TrySetSize(WorldLabelView.MinimumSize(view.Kind))) { Report(view.LastIssue); return; }
            }
            Report("짧은 문구로 최소 크기 확인 · 기본/최소 크기는 검토 제안");
        }
        public void DefaultSize()
        {
            if (!Ready) return;
            foreach (var view in views) if (!view.TrySetSize(WorldLabelView.DefaultSize(view.Kind))) { Report(view.LastIssue); return; }
            Report("기본 크기 복원");
        }
        public void OverflowRejection()
        {
            if (!Ready) return;
            var view = views[2]; string before = view.Title;
            bool accepted = view.TrySetContent(new string('W', 200), "초과 예시");
            Report(!accepted && view.Title == before ? "PASS · 초과 내용 거부 / 이전 내용 보존" : "FAIL · 초과 거부");
        }
        public void InvalidSize()
        {
            if (!Ready) return;
            var view = views[0]; Vector2 before = view.LogicalSize;
            bool accepted = view.TrySetSize(new Vector2(float.NaN, 1));
            Report(!accepted && view.LogicalSize == before ? "PASS · 무효 크기 거부 / 이전 크기 보존" : "FAIL · 무효 크기");
        }
        public void ToggleSurfaces()
        {
            if (!Ready) return;
            surfacesHidden = !surfacesHidden;
            foreach (var view in views) if (!view.RequiresProximity) view.TrySetSurfaceVisible(!surfacesHidden);
            Report("소품 3종 표면만 전환 · TMP/아이콘 유지 · Item/Shelf 판 유지");
        }
        public void ObserverDistance()
        {
            if (!Ready) return;
            float distance = distances[distanceStep++ % distances.Length];
            // Item/Shelf의 중점을 기준으로 이동하여 두 라벨까지의 실제 3D 거리를 같은 값으로 맞춥니다.
            var a = views[3].transform.position; var b = views[4].transform.position;
            var middle = (a + b) * .5f; float half = Vector3.Distance(a, b) * .5f;
            observer.position = middle + Vector3.back * Mathf.Sqrt(Mathf.Max(0, distance * distance - half * half));
            foreach (var view in views) if (view.RequiresProximity)
            {
                var gate = view.GetComponent<WorldLabelProximity>();
                if (gate.Observer != observer) gate.BindObserver(observer);
            }
            Report("Observer 거리 " + distance.ToString("0.00") + " · enter 2 / exit 2.4 제안값 · 다음 프레임 판정");
        }
        public void ClearObserver()
        {
            if (!Ready) return;
            foreach (var view in views) if (view.RequiresProximity) view.GetComponent<WorldLabelProximity>().BindObserver(null);
            Report("Observer 해제 → Item/Shelf 숨김");
        }
        public void ToggleBillboard()
        {
            if (!Ready) return; billboard = !billboard;
            foreach (var view in views) if (view.RequiresProximity)
            {
                var component = view.GetComponent<WorldLabelBillboard>(); component.SetCamera(previewCamera); component.enabled = billboard;
            }
            Report("Item/Shelf Billboard " + (billboard ? "ON · 명시 Camera 회전 복사" : "OFF · 설치 회전 복원"));
        }
        public void RotateCamera()
        {
            if (!Ready) return;
            previewCamera.transform.rotation = previewCamera.transform.rotation == Quaternion.identity ? Quaternion.Euler(0, 12, 8) : Quaternion.identity;
            Report("명시 Camera 회전 전환 · 소품 방향 고정 / Billboard 비교");
        }
        public void ToggleBackground()
        {
            if (!Ready) return; dark = !dark;
            previewCamera.backgroundColor = dark ? new Color32(14,24,28,255) : new Color32(104,115,116,255); Report("확인 씬 배경 전환");
        }
        public void ResetPreview()
        {
            if (!Ready) return;
            alternate = surfacesHidden = billboard = false; distanceStep = 0; previewCamera.transform.rotation = Quaternion.identity;
            foreach (var view in views)
            {
                var component = view.GetComponent<WorldLabelBillboard>(); component.enabled = false; component.SetCamera(null);
                view.SetVisible(true); view.TrySetSize(WorldLabelView.DefaultSize(view.Kind)); view.TrySetSurfaceVisible(true);
                int index = Array.IndexOf(views, view);
                Sprite icon = defaultIcons != null && index >= 0 && index < defaultIcons.Length ? defaultIcons[index] : null;
                if (!view.TrySetContent(DefaultTitle(view.Kind), view.Kind == WorldLabelKind.Paper ? "무대가 조용해지면\n뒤쪽 통로를 살펴봐.\n서두르지 마." : null, icon)) Report(view.LastIssue);
                if (view.RequiresProximity) view.GetComponent<WorldLabelProximity>().BindObserver(null);
            }
            Report("독립 합성 검토 · 실제게임 연결 없음 · Item/Shelf Observer 미지정 숨김 · 거리/크기/scale은 제안값");
        }
        private static string DefaultTitle(WorldLabelKind kind)
        {
            switch (kind) { case WorldLabelKind.Wood: return "무대 입구"; case WorldLabelKind.Cloth: return "단원 대기실"; case WorldLabelKind.Paper: return "낡은 쪽지"; case WorldLabelKind.Item: return "정비 도구"; default: return "03"; }
        }
        private void Report(string message) { if (status != null) status.text = message; }
    }
}
