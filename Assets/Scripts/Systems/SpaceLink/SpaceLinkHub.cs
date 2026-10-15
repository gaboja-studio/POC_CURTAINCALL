using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.SpaceLink
{
    /// <summary>
    /// 시작 씬(Tests/Systems/SpaceLink/SpaceLink)에 둔다. 장소 전환과 확인 화면을 띄우고,
    /// 추가 로드로 공간 씬이 붙어 있는 동안에는 시작 씬의 카메라를 꺼서 공간 씬 카메라와 겹치지 않게 한다.
    /// 더미 공간 씬을 따로 열면 이 컴포넌트가 없으므로 장소 전환도 일어나지 않는다(035 단독 확인 유지).
    /// </summary>
    public sealed class SpaceLinkHub : MonoBehaviour
    {
        readonly List<Camera> hubCameras = new();
        bool spaceShown;

        void Awake()
        {
            var director = SpaceLinkDirector.EnsureExists();
            if (!director.TryGetComponent(out SpaceLinkTestUI _)) director.gameObject.AddComponent<SpaceLinkTestUI>();

            foreach (var root in gameObject.scene.GetRootGameObjects())
                hubCameras.AddRange(root.GetComponentsInChildren<Camera>(true));
        }

        void Update()
        {
            bool shown = SceneManager.sceneCount > 1; // 시작 씬 + 공간 씬(DontDestroyOnLoad는 세지 않는다)
            if (shown == spaceShown) return;
            spaceShown = shown;
            foreach (var cam in hubCameras)
                if (cam != null) cam.gameObject.SetActive(!shown);
        }
    }
}
