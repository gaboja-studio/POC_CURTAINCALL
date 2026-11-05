using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.WaitingRoom.Editor
{
    public sealed partial class WaitingRoomObjectWindow
    {
        readonly Dictionary<FoundItemSpot, GameObject> spotPreviews = new();
        void PrepareSpotPreviews()
        {
            var spots = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<FoundItemSpot>(true))
                .Where(s => CurrentLayoutTarget(s.transform) && OnSelectedFloor(s.transform)
                    && (showConditionalContents || !ConditionalContent(s.transform) || s.transform == selected)).ToArray();
            foreach (var spot in spotPreviews.Keys.ToArray()) if (spot == null || !spots.Contains(spot)) {
                if (spotPreviews[spot] != null) DestroyImmediate(spotPreviews[spot]); spotPreviews.Remove(spot);
            }
            foreach (var spot in spots) {
                var art = SpotArt(spot); if (art == null) continue;
                if (!spotPreviews.TryGetValue(spot, out var preview) || preview == null || preview.name != "__SpotPreview_" + SpotItem(spot)) {
                    if (preview != null) DestroyImmediate(preview);
                    preview = new GameObject("__SpotPreview_" + SpotItem(spot)) { hideFlags = HideFlags.HideAndDontSave };
                    CopyMeshVisual(art.transform, preview.transform, true); spotPreviews[spot] = preview;
                }
                preview.transform.SetPositionAndRotation(spot.transform.position, spot.transform.rotation);
                preview.transform.localScale = Vector3.Scale(spot.transform.lossyScale, art.transform.localScale) * CurtainCall.ItemArt.ItemVisualAssets.WorldDisplayScale;
            }
        }
        void CleanupSpotPreviews()
        {
            foreach (var preview in spotPreviews.Values) if (preview != null) DestroyImmediate(preview);
            spotPreviews.Clear();
        }
    }
}
