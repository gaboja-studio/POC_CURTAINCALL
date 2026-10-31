using System.Collections;
using CurtainCall.EscapeRoute;
using CurtainCall.Settings;
using CurtainCall.UI.SceneIntegration;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.SpaceLink
{
    public sealed partial class SpaceLinkDirector
    {
        public bool EscapePrepared => EscapeSceneTransfer.Current != null && EscapeSceneTransfer.Current.Ready;

        IEnumerator TransitionEscape(TransitionRequest request)
        {
            var transfer = EscapeSceneTransfer.Current;
            if (transfer == null || !transfer.Pending) yield break;
            uint curtainVersion = 0;
            stepFailed = false;
            // Give the replicated rear seal a frame before the existing loading curtain covers it.
            yield return new WaitForSecondsRealtime(.25f);
            yield return SceneCurtainTransition.CoverFor(version => curtainVersion = version);
            try
            {
                if (!IsCurrent(request) || !SceneCurtainTransition.CanTransitionFor(curtainVersion))
                {
                    if (IsCurrent(request)) transfer.Failed("로딩 화면 준비 실패");
                    yield break;
                }
                float deadline = Time.unscaledTime + 20;
                while (IsCurrent(request) && !transfer.AllCaptured && Time.unscaledTime < deadline) yield return null;
                if (!IsCurrent(request)) yield break;
                if (!transfer.AllCaptured) { transfer.Failed("참가자 위치 보관 시간 초과"); yield break; }

                using (CommonUiSceneLoader.DeferSceneChanges())
                {
                    while (CommonUiSceneLoader.IsLoading) yield return null;
                    // Keep W until every owner has a valid pose, camera and prepared floor in A.
                    // Both scenes coexist only behind the loading screen, not during play.
                    if (!IsLoaded(request.SceneName)) yield return Load(request.SceneName, LoadSceneMode.Additive);
                    if (stepFailed || !IsCurrent(request))
                    {
                        if (IsCurrent(request)) transfer.Failed("탈출로 씬 로드 실패");
                        yield break;
                    }
                    var destination = SceneManager.GetSceneByName(request.SceneName);
                    SceneManager.SetActiveScene(destination);
                    deadline = Time.unscaledTime + 60;
                    while (IsCurrent(request) && !transfer.AllPlaced && Time.unscaledTime < deadline) yield return null;
                    if (!IsCurrent(request)) yield break;
                    if (!transfer.AllPlaced) { transfer.Failed("탈출로 입구 배치 준비 시간 초과"); yield break; }
                    foreach (var old in LoadedSpaceScenes(GameSettings.Base.SpaceTransition))
                    {
                        if (old == destination) continue;
                        yield return Unload(old);
                        if (stepFailed || !IsCurrent(request)) break;
                    }
                }
                if (!IsCurrent(request)) yield break;
                if (stepFailed) { transfer.Failed("이전 공간 정리 실패"); yield break; }
                transfer.Complete();
                // Let destination doors, camera requests and local UI observe the completed transfer.
                yield return null;
                AddLog("대기실 전원 합류 → 로딩 → 탈출로 합류실 · 위치/방향 유지");
                SpaceChanged?.Invoke(SpaceId.EscapeRoute);
            }
            finally { SceneCurtainTransition.CompleteFor(curtainVersion); }
        }
    }
}
