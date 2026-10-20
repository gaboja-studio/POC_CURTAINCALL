using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>정본의 정적 폰트는 보존하고 누락된 게임 문구에만 기존 게임 폰트를 사용한다.</summary>
    [DefaultExecutionOrder(-300)]
    public sealed class UiRuntimeFontFallback : MonoBehaviour
    {
        [SerializeField] TMP_FontAsset fallback;
        [SerializeField] Font mediumSource;
        [SerializeField] Font semiBoldSource;
        readonly Dictionary<TMP_FontAsset, TMP_FontAsset> copies = new();
        readonly Dictionary<Font, TMP_FontAsset> generated = new();
        public void Configure(TMP_FontAsset font) { fallback = font; }
        public void ConfigureSources(Font medium, Font semiBold) { mediumSource = medium; semiBoldSource = semiBold; }
        void Awake()
        {
            if (fallback == null) return;
            foreach (var label in GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.GetComponentInParent<CurtainCall.UI.GuidesV1.GuideView>() == null
                    && label.GetComponentInParent<CurtainCall.UI.TooltipsV1.TooltipView>() == null
                    && label.GetComponentInParent<PerformanceEndingPresentation>() == null) continue;
                var original = label.font;
                if (original == null || original == fallback) continue;
                if (!copies.TryGetValue(original, out var copy))
                {
                    copy = Instantiate(original);
                    copy.hideFlags = HideFlags.DontSave;
                    copy.fallbackFontAssetTable = new List<TMP_FontAsset>(original.fallbackFontAssetTable ?? new List<TMP_FontAsset>());
                    var source = original.name.Contains("SemiBold") ? semiBoldSource : mediumSource;
                    if (source != null)
                    {
                        if (!generated.TryGetValue(source, out var complete))
                        {
                            complete = TMP_FontAsset.CreateFontAsset(source);
                            complete.hideFlags = HideFlags.DontSave;
                            complete.TryAddCharacters("FSpace길게발사내리기트램펄린놓기봉잡기대포타기현재구간출발쉼터최종0123456789[] ·+공연성공실패팀호응동자금수입단원망했습니다이의개인은으로고정됩니다생존연결료잠시후다음간이동화면전환을완하지못상태를확인해세요");
                            generated.Add(source, complete);
                        }
                        copy.fallbackFontAssetTable.Add(complete);
                    }
                    if (!copy.fallbackFontAssetTable.Contains(fallback)) copy.fallbackFontAssetTable.Add(fallback);
                    copies.Add(original, copy);
                }
                label.font = copy;
            }
        }
        void OnDestroy()
        {
            foreach (var copy in copies.Values)
            {
                if (copy == null) continue;
                // TMP destroys its atlas/material in OnDestroy. These are borrowed source assets.
                copy.atlasTextures = System.Array.Empty<Texture2D>();
                copy.material = null;
                Destroy(copy);
            }
            foreach (var font in generated.Values) if (font != null) Destroy(font);
        }
    }
}
