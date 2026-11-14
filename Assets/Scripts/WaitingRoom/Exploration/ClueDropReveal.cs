using System.Collections;
using CurtainCall.ItemArt;
using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 보관함에서 나오는 기물을 먼저 오버뷰로 보여 준 뒤 바닥에 떨어뜨린다(2026-11-14 사용자: 키 카드 복제본).
    /// 보관함(container)이 열리면 가까이 있는 내 화면에 오버뷰(clue)가 한 번 뜬다. 오버뷰를 닫으면 이 대상이 열리고(팀 공유),
    /// 그때 발견품 자리(availableAfter = 이 대상)가 기물을 바닥에 놓는다. 닫은 사람 화면에는 떨어지는 모습과 카메라 강조가 나온다.
    /// </summary>
    public sealed class ClueDropReveal : MonoBehaviour, IExplorationTarget
    {
        [SerializeField] string targetId = "final:keycard-dropped";
        [SerializeField] string container = "structure-a:cabinet-3";
        [SerializeField] ClueInspectable clue;
        [Tooltip("기물이 떨어질 바닥 자리(발견품 자리와 같은 곳).")]
        [SerializeField] Transform dropPoint;
        [Tooltip("떨어지는 모습에 쓸 기물 ID.")]
        [SerializeField] string itemId = "keycard-copy";
        [Tooltip("오버뷰가 저절로 뜨는 거리(m).")]
        [SerializeField, Min(.5f)] float showRadius = 2.4f;
        [SerializeField, Min(.1f)] float fallSeconds = .55f;
        [Tooltip("카메라가 떨어진 기물을 비추는 시간(초).")]
        [SerializeField, Min(.5f)] float highlightSeconds = 2.4f;

        public string TargetId => targetId;
        public string ToolId => "";
        public string ToolName => "";
        public string Label => clue != null ? clue.Title : name;
        public string RewardItemId => "";
        public Vector3 RewardPoint => dropPoint != null ? dropPoint.position : transform.position;
        public int RewardUidOffset => 0;
        public bool Resolve(int attemptNumber, System.Random rng) => true;
        public string Describe(bool success, int attemptNumber) => "";
        bool Dropped => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(targetId);
        bool ContainerOpen => ExplorationState.Instance != null && ExplorationState.Instance.IsOpen(container);

        // 보관함이 열렸고 오버뷰를 본 사람이 가까이 있을 때만 떨어뜨릴 수 있다(시야 검사는 하지 않는다: 기물이 보관함 속에 있다).
        public float Reach(Vector3 feet, Component actor = null)
        {
            if (!ContainerOpen || Dropped) return -1f;
            float d = Vector2.Distance(new Vector2(feet.x, feet.z), new Vector2(transform.position.x, transform.position.z));
            return d <= showRadius + 1.5f && Mathf.Abs(feet.y - RewardPoint.y) < 3f ? .1f : -1f;
        }

        bool shownOnce, wasViewing, dropSeen, dropWas;
        void OnEnable() => ExplorationTargets.Add(this);
        void OnDisable() => ExplorationTargets.Remove(this);

        void Update()
        {
            var state = ExplorationState.Instance;
            if (state == null || clue == null) return;
            bool dropped = Dropped, viewing = clue.IsOpen;
            // 떨어지는 연출 동안에는 탐색 제한 시간이 멈춘다(모든 화면이 같은 순간을 보므로 호스트 화면에서도 걸린다).
            if (dropSeen && dropped && !dropWas) CurtainCall.Campaign.CampaignRun.HoldClock(fallSeconds + highlightSeconds);
            dropSeen = true; dropWas = dropped;
            // 떨어진 뒤에는 보관함 속 기물(오버뷰 대상)을 치운다.
            if (clue.gameObject.activeSelf == dropped && !viewing) clue.gameObject.SetActive(!dropped);
            if (dropped) { wasViewing = false; return; }
            if (wasViewing && !viewing)
            {
                state.RequestUse(targetId);
                StartCoroutine(FallAndHighlight());
            }
            else if (!viewing && !shownOnce && ContainerOpen && ClueInspectable.Current == null
                && state.LocalFeet is Vector3 feet && Reach(feet) >= 0f
                && Vector2.Distance(new Vector2(feet.x, feet.z), new Vector2(transform.position.x, transform.position.z)) <= showRadius)
            {
                clue.SetOpen(true);
                shownOnce = viewing = clue.IsOpen;
            }
            wasViewing = viewing;
        }

        IEnumerator FallAndHighlight()
        {
            Vector3 from = clue.transform.position, to = RewardPoint + Vector3.up * .02f;
            var visual = ItemVisualAssets.CreateWorld(itemId);
            var view = BeginHighlight(to);
            for (float t = 0f; t < 1f; t += Time.deltaTime / fallSeconds)
            {
                if (visual != null)
                {
                    // 앞으로 밀려 나오며 떨어진다.
                    var p = Vector3.Lerp(from, to, t); p.y = Mathf.Lerp(from.y, to.y, t * t);
                    visual.transform.SetPositionAndRotation(p, Quaternion.Euler(90f * (1f - t), 240f * t, 0f));
                }
                yield return null;
            }
            // 실제 기물이 놓일 때까지 잠깐 남겨 둔다(판정 화면 왕복).
            if (visual != null) { visual.transform.SetPositionAndRotation(to, Quaternion.identity); Destroy(visual, .35f); }
            yield return new WaitForSecondsRealtime(highlightSeconds);
            if (view != null) Destroy(view);
        }

        // 떨어진 자리를 내려다보는 임시 카메라와 은은한 조명. 치우면 원래 화면으로 돌아간다.
        static GameObject BeginHighlight(Vector3 point)
        {
            var main = Camera.main; if (main == null) return null;
            var away = main.transform.position - point; away.y = 0f;
            if (away.sqrMagnitude < .01f) away = Vector3.back;
            away.Normalize();
            var go = new GameObject("DropHighlightCamera");
            var eye = point + away * 1.25f + Vector3.up * 1.05f;
            // 벽·난간에 가리면 기물 쪽으로 당긴다.
            if (Physics.Linecast(point + Vector3.up * .15f, eye, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                eye = Vector3.Lerp(point + Vector3.up * .15f, hit.point, .8f);
            go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(point - eye, Vector3.up));
            var cam = go.AddComponent<CinemachineCamera>();
            cam.Lens = LensSettings.FromCamera(main); cam.Priority = 220;
            var glow = new GameObject("Glow").AddComponent<Light>(); glow.transform.SetParent(go.transform, false);
            glow.transform.position = point + Vector3.up * .5f;
            glow.type = LightType.Point; glow.range = 2.2f; glow.intensity = 2.2f; glow.color = new Color(1f, .9f, .65f); glow.shadows = LightShadows.None;
            return go;
        }
    }
}
