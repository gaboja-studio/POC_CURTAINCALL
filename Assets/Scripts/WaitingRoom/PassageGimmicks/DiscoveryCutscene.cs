using System.Collections;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.WaitingRoom.Pursuit;
using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 발견 튜토리얼 연출(2026-11-14 사용자: 숙소 녹슨 문). 누군가 대상 가까이에 처음 다가가면 한 번 재생한다.
    /// 순서: 조작 잠금 + 추격자·탐색 제한 시간 정지 → 카메라가 대상을 가까이 비춤 → 화면 위 시스템 알림 → 내 시점으로 복귀 → 잠금·정지 해제.
    /// 발견 여부(<see cref="seenId"/>)는 탐색 장부로 공유되고, 모든 화면이 같은 순간에 재생한다. 들어왔을 때 이미 발견돼 있으면 재생하지 않는다.
    /// </summary>
    public sealed class DiscoveryCutscene : MonoBehaviour
    {
        [SerializeField] string seenId = "learning:dorm-rust-door-seen";
        [Tooltip("이 대상이 이미 열려(부서져) 있으면 발견 처리만 하고 재생하지 않는다.")]
        [SerializeField] string skipIfOpen = "";
        [Tooltip("이 범위(월드 기준) 안에 사람이 들어서면 발견한 것으로 본다.")]
        [SerializeField] Bounds trigger;
        [SerializeField] Vector3 cameraPosition, cameraLook;
        [SerializeField, Min(.5f)] float seconds = 4.5f;
        [SerializeField, Min(.1f)] float enterSeconds = .5f, returnSeconds = .8f;
        [SerializeField] string textKey = "cutscene.rust-door";
        [SerializeField] string title = "녹슨 문";
        [SerializeField, TextArea] string message = "녹이 슨 문은 부숴서 열 수 있습니다. F키를 눌러 상호작용하세요. 도구가 있으면 더 빠르게 부술 수 있습니다.";

        bool seen, was, playing;

        // 한 번만 나오는 이벤트: 세대가 바뀌어도 발견 기록은 남긴다.
        void OnEnable() => ExplorationState.KeepAcrossGenerations(seenId);

        void Update()
        {
            var state = ExplorationState.Instance;
            if (state == null || !state.StateReady) return; // 판정 화면의 상태를 받은 뒤부터 본다
            bool found = state.IsOpen(seenId);
            if (!found && !WaitingRoomRehearsal.IsPreTutorialRestricted)
            {
                bool near = false; int players = 0;
                foreach (var player in NetworkPlayer.All)
                {
                    if (player == null || !player.IsSpawned || player.State != PlayerState.Normal) continue;
                    players++; near |= trigger.Contains(player.transform.position + Vector3.up * .5f);
                }
                if (players == 0 && state.LocalFeet is Vector3 feet) near = trigger.Contains(feet + Vector3.up * .5f);
                if (near) state.MarkVisited(seenId); // 판정 화면에서만 기록된다
            }
            bool skip = !string.IsNullOrEmpty(skipIfOpen) && state.IsOpen(skipIfOpen);
            if (seen && found && !was && !playing && !skip) StartCoroutine(Play());
            seen = true; was = found;
        }

        IEnumerator Play()
        {
            playing = true;
            InputGate.SetBlocking(this, true);
            PursuitDirector.HoldForCutscene(seconds + returnSeconds + .5f);
            var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            var blend = brain != null ? brain.DefaultBlend : default;
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, enterSeconds);
            var go = new GameObject("DiscoveryCutsceneCamera");
            var cam = go.AddComponent<CinemachineCamera>();
            if (Camera.main != null) cam.Lens = LensSettings.FromCamera(Camera.main);
            cam.Priority = 240;
            go.transform.SetPositionAndRotation(cameraPosition, Quaternion.LookRotation(cameraLook - cameraPosition, Vector3.up));
            WaitingRoomGuidePanel.ShowAs(WaitingRoomText.Get(textKey + ".title", title), WaitingRoomText.Get(textKey + ".body", message), seconds + 2f, RoomNotice.Notice);
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) { PursuitDirector.HoldForCutscene(.5f); yield return null; }
            if (brain != null) brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, returnSeconds);
            Destroy(go);
            for (float t = 0f; t < returnSeconds + .1f; t += Time.unscaledDeltaTime) { PursuitDirector.HoldForCutscene(.3f); yield return null; }
            if (brain != null) brain.DefaultBlend = blend;
            InputGate.SetBlocking(this, false);
            playing = false;
        }

        void OnDisable()
        {
            if (!playing) return;
            StopAllCoroutines(); InputGate.SetBlocking(this, false); playing = false;
        }

        void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawWireCube(trigger.center, trigger.size); }
    }
}
