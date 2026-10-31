using Unity.Cinemachine;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 튜토리얼 클로즈업(2026-10-31 사용자): 지금 시점에서 1.8초 동안 대상 쪽으로 이동 → 2.5초 비춤 → 1초 동안 원래 플레이어 시점으로 복귀.
    /// 내 화면만 바꾼다. 다른 연출 카메라(침대 220)보다 높은 우선순위를 쓰고, 켜고 끌 때는 브레인 블렌드를 잠깐 끊어(Cut) 정해진 시간대로만 움직인다.
    /// </summary>
    public sealed class WaitingRoomTutorialCamera : MonoBehaviour
    {
        public const float MoveIn = 1.8f, Hold = 2.5f, MoveOut = 1f, Total = MoveIn + Hold + MoveOut;
        const int Priority = 230;

        CinemachineCamera view;
        CinemachineBrain brain;
        CinemachineBlendDefinition savedBlend;
        CinemachineBlenderSettings savedCustom;
        bool blendCut;
        int restoreAtFrame = -1;
        Vector3 fromPos, toPos;
        Quaternion fromRot, toRot;
        float startedAt;

        /// <summary>클로즈업 중(복귀 이동 포함).</summary>
        public bool IsActive => view != null;

        /// <summary>지금 화면 시점에서 <paramref name="pos"/>·<paramref name="rot"/>로 클로즈업을 시작한다.</summary>
        public bool Begin(Vector3 pos, Quaternion rot)
        {
            var main = Camera.main;
            if (main == null) return false;
            Stop();
            fromPos = main.transform.position; fromRot = main.transform.rotation;
            toPos = pos; toRot = rot;
            startedAt = Time.unscaledTime;
            CutBlends(main);
            var go = new GameObject("TutorialCloseUpCamera");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(fromPos, fromRot);
            view = go.AddComponent<CinemachineCamera>();
            view.Lens = LensSettings.FromCamera(main);
            view.Priority = Priority;
            return true;
        }

        public void Stop()
        {
            if (view == null) return;
            Destroy(view.gameObject);
            view = null;
            // 브레인이 꺼진 카메라를 알아챈 뒤(다음 LateUpdate) 블렌드를 되돌린다.
            restoreAtFrame = Time.frameCount + 2;
        }

        void LateUpdate()
        {
            if (view != null)
            {
                float t = Time.unscaledTime - startedAt;
                float k = t < MoveIn ? Mathf.SmoothStep(0f, 1f, t / MoveIn)
                    : t < MoveIn + Hold ? 1f
                    : 1f - Mathf.SmoothStep(0f, 1f, (t - MoveIn - Hold) / MoveOut);
                view.transform.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
                if (t >= Total) Stop();
            }
            if (restoreAtFrame >= 0 && Time.frameCount >= restoreAtFrame) { restoreAtFrame = -1; RestoreBlends(); }
        }

        void CutBlends(Camera main)
        {
            if (blendCut) return;
            brain = main.GetComponent<CinemachineBrain>();
            if (brain == null) return;
            savedBlend = brain.DefaultBlend; savedCustom = brain.CustomBlends;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            brain.CustomBlends = null;
            blendCut = true; restoreAtFrame = -1;
        }

        void RestoreBlends()
        {
            if (!blendCut) return;
            if (brain != null) { brain.DefaultBlend = savedBlend; brain.CustomBlends = savedCustom; }
            brain = null; blendCut = false;
        }

        void OnDisable() { if (view != null) Destroy(view.gameObject); view = null; RestoreBlends(); }

        /// <summary>
        /// 대상 묶음(<paramref name="focus"/>)을 비추는 카메라 자리. 대상에서 플레이어 쪽으로 물러나되 벽에 막히면 벽 안쪽에 선다.
        /// </summary>
        public static void FramePose(Bounds focus, Vector3 viewer, out Vector3 pos, out Quaternion rot)
        {
            var center = focus.center;
            var away = viewer - center; away.y = 0f;
            if (away.sqrMagnitude < .01f) away = Vector3.back;
            away.Normalize();
            float radius = Mathf.Max(focus.extents.x, focus.extents.z);
            float distance = Mathf.Clamp(radius * 2.2f + 1.5f, 2.5f, 7f);
            var dir = (away * distance + Vector3.up * Mathf.Clamp(distance * .45f, 1f, 2.5f)).normalized;
            float length = Mathf.Sqrt(distance * distance + Mathf.Pow(Mathf.Clamp(distance * .45f, 1f, 2.5f), 2));
            // 대상 자신의 충돌체는 건너뛰고, 처음 막는 벽 바로 앞까지만 물러난다.
            var start = center + dir * Mathf.Min(radius, length * .5f);
            float free = length;
            foreach (var hit in Physics.RaycastAll(start, dir, length, ~0, QueryTriggerInteraction.Ignore))
                if (!focus.Contains(hit.point) && hit.distance + Mathf.Min(radius, length * .5f) - .35f < free)
                    free = Mathf.Max(1.2f, hit.distance + Mathf.Min(radius, length * .5f) - .35f);
            pos = center + dir * free;
            rot = Quaternion.LookRotation(center - pos, Vector3.up);
        }
    }
}
