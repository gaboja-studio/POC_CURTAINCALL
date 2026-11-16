using System;
using System.Collections.Generic;
using UnityEngine;

namespace CurtainCall.StartLobby
{
    [CreateAssetMenu(menuName = "Curtain Call/Start Menu Credits", fileName = "StartMenuCredits")]
    public sealed class StartMenuCreditsSettings : ScriptableObject
    {
        public const string ResourcePath = "Settings/StartMenu/StartMenuCredits";
        [Tooltip("추가한 이름과 라이선스 문구를 표시할 원본 폰트. 실행 중에만 글리프를 생성합니다.")]
        public Font sourceFont;

        [Serializable]
        public sealed class Contributor
        {
            public string nickname;
            public string role;
            public string url;
        }

        [Serializable]
        public sealed class AssetLicense
        {
            public string asset;
            public string author;
            public string license;
            [TextArea(2, 8)] public string notice;
            public string url;
            public TextAsset fullLicense;
        }

        [Header("개발자 · + / − 로 추가 및 삭제")]
        public List<Contributor> contributors = new()
        {
            new Contributor { nickname = "MoHoDu", role = "개발", url = "https://github.com/MoHoDu" },
            new Contributor { nickname = "gyumin9048-ctrl", role = "개발", url = "https://github.com/gyumin9048-ctrl" },
            new Contributor { nickname = "RagSpirin", role = "개발", url = "https://github.com/RagSpirin" },
            new Contributor { nickname = "lakuase2", role = "개발", url = "https://github.com/lakuase2" }
        };

        [Header("에셋 및 라이선스 · 원문 TextAsset 연결 가능")]
        public List<AssetLicense> licenses = new();
    }
}
