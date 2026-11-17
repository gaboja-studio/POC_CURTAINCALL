using System;
using UnityEngine;

namespace CurtainCall.Settings
{
    [Serializable]
    public sealed class AudienceFeedbackSettings
    {
        public enum PopupAnchor { BalanceSide, CheerGauge }
        [SerializeField] PopupAnchor anchor = PopupAnchor.CheerGauge;
        [SerializeField, Range(.5f, 1.5f)] float popupSeconds = 1.2f;
        [SerializeField, Range(.05f, .3f)] float combineSeconds = .2f;
        [SerializeField, Range(20, 56)] float fontSize = 44;
        [SerializeField, Range(0, 1)] float audioVolume = .58f;
        [SerializeField, Range(.15f, 1)] float audioInterval = .4f;
        [SerializeField, Range(0, 1)] float spatialBlend = .65f;
        [SerializeField, Range(0, 1)] float ambienceVolume = .18f;
        [SerializeField, Range(0, 1)] float ambienceDuck = .65f;
        [SerializeField, Range(.2f, 3)] float ambienceFadeSeconds = 1.2f;
        [Tooltip("한 구역 시험 전용. 효과/성능 검수 전에는 기본 꺼짐.")]
        [SerializeField] bool crowdTrial;
        public PopupAnchor Anchor => anchor;
        public float PopupSeconds => Mathf.Clamp(popupSeconds, .5f, 1.5f);
        public float CombineSeconds => Mathf.Clamp(combineSeconds, .05f, .3f);
        public float FontSize => Mathf.Clamp(fontSize, 20, 56);
        public float AudioVolume => Mathf.Clamp01(audioVolume);
        public float AudioInterval => Mathf.Clamp(audioInterval, .15f, 1);
        public float SpatialBlend => Mathf.Clamp01(spatialBlend);
        public float AmbienceVolume => Mathf.Clamp01(ambienceVolume);
        public float AmbienceDuck => Mathf.Clamp01(ambienceDuck);
        public float AmbienceFadeSeconds => Mathf.Clamp(ambienceFadeSeconds, .2f, 3);
        public bool CrowdTrial => crowdTrial;
    }
}
