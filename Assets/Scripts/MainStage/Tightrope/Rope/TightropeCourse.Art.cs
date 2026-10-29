using UnityEngine;

namespace CurtainCall.Tightrope
{
    public sealed partial class TightropeCourse
    {
        [Header("외형 준비본 (비우면 기존 도형)")]
        [SerializeField] TightropeArtProfile artProfile;
        public TightropeArtProfile ArtProfile => artProfile;

        void ApplyPreparedArt(GameObject box, string role, Color color)
        {
            if (artProfile == null) return;
            if (role == "StartPlatform" || role == "FinishPlatform") TightropeArtFit.Box(box.transform, artProfile.floor);
            else if (role.StartsWith("Section")) TightropeArtFit.Rope(box.transform, artProfile.rope);
            else if (role.StartsWith("RelayRope")) TightropeArtFit.Rope(box.transform,
                artProfile.relayRope != null ? artProfile.relayRope : artProfile.rope);
        }
    }
}
