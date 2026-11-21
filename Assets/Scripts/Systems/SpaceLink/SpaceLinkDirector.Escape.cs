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
        // Same timeout as a network scene load, with time for an owner to place after scene initialization.
        const float MinimumEscapeAckTimeoutSeconds = 30f;

        public bool EscapePrepared => EscapeSceneTransfer.Current != null && EscapeSceneTransfer.Current.Ready;

        IEnumerator TransitionEscape(TransitionRequest request)
        {
            var transfer = EscapeSceneTransfer.Current;
            if (transfer == null || !transfer.Pending) yield break;
            uint curtainVersion = 0;
            stepFailed = false;
            // Give the replicated rear seal a frame before the existing loading curtain covers it.
            yield return new WaitForSecondsRealtime(.25f);
            if (!IsCurrent(request) || !transfer.Coordinating) yield break;
            yield return SceneCurtainTransition.CoverFor(version => curtainVersion = version, true);
            try
            {
                if (!IsCurrent(request) || !SceneCurtainTransition.CanTransitionFor(curtainVersion))
                {
                    if (IsCurrent(request)) transfer.Failed("로딩 화면 준비 실패");
                    yield break;
                }
                yield return WaitForEscapeAcknowledgements(request, transfer, false);
                if (!IsCurrent(request) || !transfer.Coordinating || !transfer.AllCaptured) yield break;

                using (CommonUiSceneLoader.DeferSceneChanges())
                {
                    double uiDeadline = Time.unscaledTimeAsDouble + MinimumEscapeAckTimeoutSeconds;
                    while (CommonUiSceneLoader.IsLoading && IsCurrent(request) && transfer.Coordinating
                        && Time.unscaledTimeAsDouble < uiDeadline) yield return null;
                    if (!IsCurrent(request) || !transfer.Coordinating) yield break;
                    if (CommonUiSceneLoader.IsLoading)
                    {
                        transfer.Failed("공통 UI 정리 시간 초과");
                        yield break;
                    }
                    // An already-loaded host scene is only reusable after NGO confirmed the crew's load.
                    // Before that point no owner can move into it, so a partial copy can be unloaded safely.
                    if (!transfer.PlacementAuthorized)
                    {
                        yield return LoadEscapeDestination(request, transfer);
                        if (!IsCurrent(request) || !transfer.Coordinating || !transfer.PlacementAuthorized) yield break;
                    }
                    var destination = SceneManager.GetSceneByName(request.SceneName);
                    if (!destination.isLoaded)
                    {
                        transfer.Failed("탈출로 씬을 찾을 수 없음");
                        yield break;
                    }
                    SceneManager.SetActiveScene(destination);
                    yield return WaitForEscapeAcknowledgements(request, transfer, true);
                    if (!IsCurrent(request) || !transfer.Coordinating || !transfer.AllPlaced) yield break;
                    foreach (var old in LoadedSpaceScenes(GameSettings.Base.SpaceTransition))
                    {
                        if (old == destination) continue;
                        yield return Unload(old, ignoreDisconnected: true);
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

        IEnumerator LoadEscapeDestination(TransitionRequest request, EscapeSceneTransfer transfer)
        {
            var partial = SceneManager.GetSceneByName(request.SceneName);
            if (partial.isLoaded)
            {
                var departure = transfer.DepartureScene;
                if (departure.isLoaded) SceneManager.SetActiveScene(departure);
                yield return Unload(partial, ignoreDisconnected: true);
                if (!IsCurrent(request) || !transfer.Coordinating) yield break;
                if (stepFailed)
                {
                    transfer.Failed("이전 탈출로 부분 로드 정리 실패");
                    yield break;
                }
            }

            yield return Load(request.SceneName, LoadSceneMode.Additive, ignoreDisconnected: true);
            if (!IsCurrent(request) || !transfer.Coordinating) yield break;
            if (stepFailed)
            {
                transfer.Failed("탈출로 씬 로드 실패");
                yield break;
            }
            transfer.AuthorizePlacement();
        }

        IEnumerator WaitForEscapeAcknowledgements(TransitionRequest request, EscapeSceneTransfer transfer, bool placement)
        {
            var network = Unity.Netcode.NetworkManager.Singleton;
            float timeout = Mathf.Max(MinimumEscapeAckTimeoutSeconds,
                network != null ? network.NetworkConfig.LoadSceneTimeOut : 0f);
            double deadline = Time.unscaledTimeAsDouble + timeout;
            while (IsCurrent(request) && transfer.Coordinating
                && !(placement ? transfer.AllPlaced : transfer.AllCaptured))
            {
                if (Time.unscaledTimeAsDouble >= deadline)
                {
                    // Retain placement acknowledgements and loaded scenes. The transfer coordinator retries.
                    transfer.Failed(placement ? "탈출로 도착 확인 시간 초과" : "대기실 출발 확인 시간 초과");
                    yield break;
                }
                yield return null;
            }
        }
    }
}
