using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CurtainCall.UI.SceneIntegration
{
    /// <summary>
    /// 소지 칸 사용 연출(2026-11-02 사용자, Task-094). 기물 내구도가 줄면 그 칸에 사용 연출, 다 써서 사라지면 파괴 연출을 띄운다.
    /// CommonMenuUI 씬의 슬롯 템플릿(00_SlotTemplate)에 배치하고, 세기·시간·색은 씬에서 고친다. CommonInventoryUI가 부른다.
    /// - 사용: 칸 번쩍임(<see cref="flash"/>) + 아이콘 튀김 + 아이콘 흰빛(All In 1 Sprite Shader Hit Effect).
    /// - 파괴: 사라진 기물 그림(<see cref="breakGhost"/>)이 커지며 타 없어짐(All In 1 Sprite Shader Fade) + 칸 붉은 번쩍임.
    /// - 머티리얼(<see cref="effectMaterial"/>)은 _ThirdParty에 둔 All In 1 UI 머티리얼. 연출 동안만 복사본을 씌우고 끝나면 원래대로 돌린다.
    /// </summary>
    public sealed class InventorySlotUseEffect : MonoBehaviour
    {
        static readonly int HitBlend = Shader.PropertyToID("_HitEffectBlend");
        static readonly int FadeAmount = Shader.PropertyToID("_FadeAmount");

        [Tooltip("기물 아이콘(30_Item)")]
        [SerializeField] Image icon;
        [Tooltip("칸 전체를 덮는 번쩍임 그림. 평소 알파 0")]
        [SerializeField] Image flash;
        [Tooltip("파괴 연출 때 사라진 기물 그림을 띄우는 그림. 평소 꺼 둠")]
        [SerializeField] Image breakGhost;
        [Tooltip("All In 1 Sprite Shader UI 머티리얼(HITEFFECT_ON·FADE_ON). 비우면 셰이더 효과 없이 나머지만")]
        [SerializeField] Material effectMaterial;

        [Header("사용")]
        [SerializeField] Color useFlashColor = new(1f, .93f, .6f, .85f);
        [SerializeField, Min(0f)] float useSeconds = .4f;
        [SerializeField] float useIconPunch = .35f;
        [SerializeField, Range(0f, 1f)] float useHitBlend = .85f;

        [Header("파괴")]
        [SerializeField] Color breakFlashColor = new(1f, .3f, .22f, .9f);
        [SerializeField, Min(0f)] float breakSeconds = .7f;
        [SerializeField] float breakGhostScale = 1.45f;
        [SerializeField] float breakGhostSpin = 18f;
        [Tooltip("파괴 때 Fade Amount 끝 값. 0.6 근처면 그림이 다 타 없어진다")]
        [SerializeField, Range(0f, 1f)] float breakFadeTo = .65f;

        Material iconInstance, ghostInstance;
        Material iconOriginal;
        bool iconSwapped;

        void Awake()
        {
            if (flash != null) SetAlpha(flash, 0f);
            if (breakGhost != null) breakGhost.enabled = false;
        }

        /// <summary>내구도 1 사용.</summary>
        public void PlayUse()
        {
            StopAll();
            if (flash != null)
            {
                flash.color = useFlashColor;
                flash.DOFade(0f, useSeconds).SetEase(Ease.OutQuad).SetUpdate(true);
            }
            if (icon != null && icon.enabled)
            {
                icon.rectTransform.DOPunchScale(Vector3.one * useIconPunch, useSeconds, 7, .7f).SetUpdate(true);
                var mat = IconMaterial();
                if (mat != null)
                {
                    mat.SetFloat(HitBlend, useHitBlend);
                    mat.DOFloat(0f, HitBlend, useSeconds).SetEase(Ease.OutQuad).SetUpdate(true).SetTarget(this)
                        .OnComplete(RestoreIcon).OnKill(RestoreIcon);
                }
            }
        }

        /// <summary>마지막 내구도를 써서 기물이 사라졌다. <paramref name="lostSprite"/>는 사라진 기물 그림.</summary>
        public void PlayBreak(Sprite lostSprite)
        {
            StopAll();
            if (flash != null)
            {
                flash.color = breakFlashColor;
                DOTween.Sequence().SetTarget(flash).SetUpdate(true)
                    .Append(flash.DOFade(0f, breakSeconds * .3f).SetEase(Ease.OutQuad))
                    .Append(flash.DOFade(breakFlashColor.a * .6f, breakSeconds * .1f))
                    .Append(flash.DOFade(0f, breakSeconds * .6f).SetEase(Ease.InQuad));
            }
            if (breakGhost == null || lostSprite == null) return;
            breakGhost.sprite = lostSprite;
            breakGhost.enabled = true;
            breakGhost.color = Color.white;
            var rect = breakGhost.rectTransform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            if (effectMaterial != null)
            {
                if (ghostInstance == null) ghostInstance = new Material(effectMaterial) { name = effectMaterial.name + " (Ghost)" };
                ghostInstance.SetFloat(HitBlend, 1f);
                ghostInstance.SetFloat(FadeAmount, -.1f);
                breakGhost.material = ghostInstance;
                DOTween.Sequence().SetTarget(this).SetUpdate(true)
                    .Append(ghostInstance.DOFloat(0f, HitBlend, breakSeconds * .35f))
                    .Join(ghostInstance.DOFloat(breakFadeTo, FadeAmount, breakSeconds).SetEase(Ease.Linear));
            }
            else breakGhost.DOFade(0f, breakSeconds).SetEase(Ease.InQuad).SetUpdate(true);
            rect.DOScale(breakGhostScale, breakSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
            rect.DOLocalRotate(new Vector3(0f, 0f, breakGhostSpin), breakSeconds).SetEase(Ease.OutCubic).SetUpdate(true)
                .OnComplete(HideGhost).OnKill(HideGhost);
            rect.DOShakeAnchorPos(breakSeconds * .35f, 4f, 30, 90f, false, true).SetUpdate(true);
        }

        Material IconMaterial()
        {
            if (effectMaterial == null || icon == null) return null;
            if (iconInstance == null) iconInstance = new Material(effectMaterial) { name = effectMaterial.name + " (Icon)" };
            iconInstance.SetFloat(FadeAmount, -.1f);
            if (!iconSwapped) { iconOriginal = icon.material == icon.defaultMaterial ? null : icon.material; iconSwapped = true; }
            icon.material = iconInstance;
            return iconInstance;
        }

        void RestoreIcon()
        {
            if (!iconSwapped || icon == null) return;
            iconSwapped = false;
            icon.material = iconOriginal;
        }

        void HideGhost()
        {
            if (breakGhost == null) return;
            breakGhost.enabled = false;
            breakGhost.material = null;
            var rect = breakGhost.rectTransform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        void StopAll()
        {
            DOTween.Kill(this);
            if (flash != null) { flash.DOKill(); SetAlpha(flash, 0f); }
            if (icon != null) { icon.rectTransform.DOKill(complete: true); }
            if (breakGhost != null) { breakGhost.DOKill(); breakGhost.rectTransform.DOKill(complete: true); }
            RestoreIcon();
            HideGhost();
        }

        static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

        void OnDisable() => StopAll();

        void OnDestroy()
        {
            if (iconInstance != null) Destroy(iconInstance);
            if (ghostInstance != null) Destroy(ghostInstance);
        }
    }
}
