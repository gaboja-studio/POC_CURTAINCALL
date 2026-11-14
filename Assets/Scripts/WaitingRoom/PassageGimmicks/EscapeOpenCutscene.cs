using System.Collections;
using CurtainCall.Player;
using CurtainCall.WaitingRoom.Pursuit;
using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 나가는 길 개방 연출(2026-11-14 사용자). 탈출로 문과 탈출로로 가는 길 문이 함께 열리는 순간(<see cref="openedBy"/>) 모든 화면에서 재생한다.
    /// 순서: 조작 잠금 + 추격자 정지·피격 면제 → 카메라가 기계실 쪽 문 앞에서 탈출로 문까지 이동 → 화면 위 시스템 알림 → 곧바로(이동 없이) 내 시점으로 복귀 → 잠금·면제 해제.
    /// 여럿이 하는 게임이라 시간 자체(Time.timeScale)는 멈추지 않는다. 열림 상태는 모두에게 공유되므로 각 화면이 같은 순간에 시작한다.
    /// 들어왔을 때 이미 열려 있던 경우에는 재생하지 않는다.
    /// </summary>
    public sealed class EscapeOpenCutscene : MonoBehaviour
    {
        [SerializeField] string openedBy = "final:escape-route";
        [Tooltip("카메라가 지나가는 자리(순서대로). 첫 자리는 기계실 쪽 문 앞.")]
        [SerializeField] Vector3[] path = System.Array.Empty<Vector3>();
        [Tooltip("마지막에 바라보는 곳(탈출로 문).")]
        [SerializeField] Vector3 finalLook;
        [Tooltip("첫 자리에서 문이 열리는 것을 보는 시간(초).")]
        [SerializeField, Min(0f)] float startHold = 1.4f;
        [Tooltip("이동 시간(초).")]
        [SerializeField, Min(.1f)] float moveSeconds = 3.2f;
        [Tooltip("탈출로 문 앞에서 머무는 시간(초).")]
        [SerializeField, Min(0f)] float endHold = 2.6f;
        [SerializeField] string title = "나가는 길";
        [SerializeField, TextArea] string message = "나가는 길이 개방되었습니다. 서커스장에서 탈출하세요.";

        bool seen, was, playing;

        void Update()
        {
            if (ExplorationState.Instance == null) return;
            bool open = ExplorationGate.IsAnyOpen(openedBy);
            if (seen && open && !was && !playing && path.Length >= 2) StartCoroutine(Play());
            seen = true; was = open;
        }

        IEnumerator Play()
        {
            playing = true;
            float total = startHold + moveSeconds + endHold;
            InputGate.SetBlocking(this, true);
            PursuitDirector.HoldForCutscene(total + .5f);
            // 탈출로 문은 누가 보고 있을 때 열린다. 카메라가 도착할 즈음 열리기 시작하게 한다.
            float watchFrom = startHold + moveSeconds * .55f;
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            var blend = brain != null ? brain.DefaultBlend : default;
            // 들어갈 때도 돌아올 때도 화면을 바로 바꾼다(카메라 이동은 길을 따라가는 구간뿐).
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            var go = new GameObject("EscapeOpenCutsceneCamera");
            var cam = go.AddComponent<CinemachineCamera>();
            if (Camera.main != null) cam.Lens = LensSettings.FromCamera(Camera.main);
            cam.Priority = 240;
            WaitingRoomGuidePanel.ShowAs(WaitingRoomText.Get("cutscene.escape-open.title", title), WaitingRoomText.Get("cutscene.escape-open.body", message), total, RoomNotice.Notice);

            float length = 0f; for (int i = 1; i < path.Length; i++) length += Vector3.Distance(path[i - 1], path[i]);
            for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - startHold) / moveSeconds));
                var at = PointAt(k * length);
                // 가는 동안은 조금 앞을, 끝으로 갈수록 탈출로 문을 본다.
                var ahead = PointAt(Mathf.Min(length, k * length + 2.5f));
                var look = Vector3.Lerp(ahead + (path[1] - path[0]).normalized * (k < .001f ? 3f : 0f), finalLook, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, 1f, k)));
                if ((look - at).sqrMagnitude > .01f) go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look - at, Vector3.up));
                if (t >= watchFrom) ExplorationGate.WatchDoors(.5f);
                PursuitDirector.HoldForCutscene(.5f);
                yield return null;
            }
            Destroy(go);
            // 카메라 이동 없이 곧바로 내 시점으로. 전환이 끝난 뒤 원래 전환 설정을 돌려놓는다.
            yield return null; yield return null;
            if (brain != null) brain.DefaultBlend = blend;
            InputGate.SetBlocking(this, false);
            playing = false;
        }

        Vector3 PointAt(float distance)
        {
            for (int i = 1; i < path.Length; i++)
            {
                float segment = Vector3.Distance(path[i - 1], path[i]);
                if (distance <= segment || i == path.Length - 1) return Vector3.Lerp(path[i - 1], path[i], segment > 0f ? Mathf.Clamp01(distance / segment) : 1f);
                distance -= segment;
            }
            return path[path.Length - 1];
        }

        void OnDisable()
        {
            if (!playing) return;
            StopAllCoroutines(); InputGate.SetBlocking(this, false); playing = false;
        }
    }
}
