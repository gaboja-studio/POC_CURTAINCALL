using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>플레이 시작 때 대기실 문구 파일을 읽어 <see cref="WaitingRoomText"/>에 설치한다. 파일이 없으면 코드 기본 문구를 쓴다.</summary>
    static class WaitingRoomTextLoader
    {
        /// <summary>Assets/Resources/Settings/WaitingRoom/WaitingRoomTexts.txt</summary>
        const string TextsPath = "Settings/WaitingRoom/WaitingRoomTexts";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            var file = Resources.Load<TextAsset>(TextsPath);
            if (file == null) { WaitingRoomText.Clear(); Debug.LogWarning("[WaitingRoomText] 문구 파일이 없어 코드 기본 문구를 씁니다."); return; }
            WaitingRoomText.Install(file.text);
        }
    }
}
