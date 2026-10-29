using System.Collections.Generic;
using CurtainCall.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CurtainCall.SpaceLink
{
    /// <summary>
    /// 시작 씬(Tests/Systems/SpaceLink/SpaceLink)에 둔다. 장소 전환과 개발자 모드 로그를 띄우고,
    /// 추가 로드로 공간 씬이 붙어 있는 동안에는 시작 씬의 카메라·Canvas·EventSystem(<see cref="hubUi"/>)을 꺼서 공간 씬과 겹치지 않게 한다.
    /// 더미 공간 씬을 따로 열면 이 컴포넌트가 없으므로 장소 전환도 일어나지 않는다(035 단독 확인 유지).
    /// </summary>
    public sealed class SpaceLinkHub : MonoBehaviour
    {
        [Tooltip("공간 씬이 붙어 있는 동안 끌 시작 씬 화면(Hub Canvas·Hub EventSystem). 빌더가 채운다.")]
        [SerializeField] GameObject[] hubUi = new GameObject[0];

        readonly List<Camera> hubCameras = new();
        SpaceLinkDirector director;
        bool spaceShown;

        void Awake()
        {
            director = SpaceLinkDirector.EnsureExists();
            if (!director.TryGetComponent(out SpaceLinkDevLog _)) director.gameObject.AddComponent<SpaceLinkDevLog>();

            foreach (var root in gameObject.scene.GetRootGameObjects())
                hubCameras.AddRange(root.GetComponentsInChildren<Camera>(true));
        }

        void Update()
        {
            // 공통 UI/커튼/배경 additive 씬은 로비 화면을 대체하는 게임 공간이 아니다.
            bool shown = HasLoadedSpace(gameObject.scene) || (director != null && director.IsTransitioning);
            if (shown == spaceShown) return;
            spaceShown = shown;
            foreach (var cam in hubCameras)
                if (cam != null) cam.gameObject.SetActive(!shown);
            foreach (var ui in hubUi)
                if (ui != null) ui.SetActive(!shown);
        }

        public static bool HasLoadedSpace(Scene hub)
        {
            var settings = GameSettings.Base.SpaceTransition;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != hub && scene.isLoaded
                    && SpaceRoute.TryFind(scene.name, settings, out var space) && space != SpaceId.Hub)
                    return true;
            }
            return false;
        }
    }
}
