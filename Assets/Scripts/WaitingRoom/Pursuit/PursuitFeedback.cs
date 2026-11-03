using System.Collections.Generic;
using CurtainCall.Network.PlayerSync;
using CurtainCall.Player;
using CurtainCall.Settings;
using CurtainCall.UI.SceneIntegration;
using Unity.Netcode;
using UnityEngine;

namespace CurtainCall.WaitingRoom.Pursuit
{
    /// <summary>소유 플레이어의 근접 강조와 실제 추격 신호. 관전 대상/다른 단원의 추격은 표시하지 않는다.</summary>
    public sealed class PursuitFeedback : MonoBehaviour
    {

        PursuitDirector director;
        PlayerInputReader offlineReader;
        PursuitEdgeGraphic edge;
        GameObject canvasObject;
        float intensity, beat;
        public int HighlightCount => 0; // Retained for diagnostics; silhouette outlines have been removed.
        public float Intensity => intensity;
        public bool IsChased { get; private set; }
        void OnEnable()
        {
            director = GetComponent<PursuitDirector>();
            canvasObject = new GameObject("PursuitThreatEdges", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = -5;
            var go = new GameObject("ThreatEdges", typeof(RectTransform), typeof(CanvasRenderer), typeof(PursuitEdgeGraphic));
            go.transform.SetParent(canvasObject.transform, false);
            edge = go.GetComponent<PursuitEdgeGraphic>(); edge.raycastTarget = false;
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        GameObject Observer()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) {
                var p = NetworkPlayer.Local;
                return p != null && p.IsSpawned && p.State == PlayerState.Normal ? p.gameObject : null;
            }
            if (offlineReader == null) offlineReader = FindFirstObjectByType<PlayerInputReader>();
            return offlineReader != null && offlineReader.isActiveAndEnabled ? offlineReader.gameObject : null;
        }
        public static bool Threatens(PursuitActor actor, GameObject observer) => actor != null && observer != null
            && actor.IsActivated && actor.State == PursuitMotion.Run && actor.ChasedPlayer == observer;
        void LateUpdate()
        {
            var rules = GameSettings.WaitingRoom.Pursuit;
            var observer = Observer();
            bool available = director != null && director.Elapsed >= 0 && observer != null
                && !PursuitDirector.IsCaptured(observer) && !SceneCurtainTransition.IsActive;
            float nearest = float.PositiveInfinity;
            IsChased = false;
            if (available) foreach (var actor in director.Actors) {
                if (actor != null && Threatens(actor, observer)) {
                    IsChased = true; nearest = Mathf.Min(nearest, Vector3.Distance(observer.transform.position, actor.transform.position));
                }
            }
            float danger = IsChased ? Mathf.Clamp01(1 - nearest / 20f) : 0;
            float escalation = director != null && director.Elapsed >= 0 ? Mathf.Clamp01(director.Elapsed / 180f) : 0;
            float target = available && IsChased && rules.ScreenEffect ? Mathf.Lerp(.5f, 1, danger) * rules.EdgeIntensity : 0;
            // Smooth onset/release and continuous phase: never flash the entire image.
            intensity = Mathf.MoveTowards(intensity, target, Time.unscaledDeltaTime * (target > intensity ? 1.6f : .8f));
            beat += Time.unscaledDeltaTime * Mathf.Lerp(1, 1.9f, Mathf.Max(danger, escalation));
            float pulse = Mathf.Pow((Mathf.Cos(beat * Mathf.PI * 2) + 1) * .5f, 3);
            edge.SetThreat(intensity * Mathf.Lerp(1 - rules.PulseAmount, 1, pulse), rules.EdgeWidth);
        }
        void OnDisable()
        {
            if (canvasObject != null) Destroy(canvasObject);
            intensity = 0; IsChased = false;
        }
    }
}
