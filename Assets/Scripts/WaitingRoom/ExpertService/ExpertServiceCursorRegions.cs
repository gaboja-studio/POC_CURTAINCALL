using System.Reflection;
using CurtainCall.Cameras;
using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 5단계 테스트 씬 전용: 마우스 시점(커서 잠금)에서도 테스트 패널을 누를 수 있게 한다. 정식 기능 아님.
    /// 공용 카메라는 ESC로 커서를 풀고, 왼쪽 클릭으로 다시 잠근다. 다만 공용 <see cref="CameraCursorExclusion"/> 영역 안을 누르면 잠그지 않는다.
    /// 그 영역은 화면 왼쪽 위 기준 고정 크기라, 화면 크기에 따라 붙는 패널(접속·5단계·4단계·소지 안내)에 맞춰 매 프레임 영역을 옮긴다.
    /// 공용 코드를 고치지 않으려고 공용 컴포넌트의 저장 필드(screenRect)를 이 씬에서만 바꾼다.
    /// 또 수화기·스케줄표 창처럼 게임 입력을 막는 창(<see cref="CurtainCall.Player.InputGate"/>)이 열린 동안에는 공용 카메라의
    /// <see cref="CameraViewDirector.SetInputBlocked"/>로 커서를 풀어 창 버튼을 누를 수 있게 한다(공용 쪽에서 아무도 부르지 않아 생긴 문제 — PM 보고 대상).
    /// </summary>
    public sealed class ExpertServiceCursorRegions : MonoBehaviour
    {
        static readonly FieldInfo RectField = typeof(CameraCursorExclusion).GetField("screenRect", BindingFlags.NonPublic | BindingFlags.Instance);

        CameraCursorExclusion network, bottomLeft, bottomRight;

        void Awake()
        {
            if (RectField == null)
            {
                // 영역은 못 두어도 창이 열렸을 때 커서 풀기는 계속한다.
                Debug.LogWarning("[ExpertService] CameraCursorExclusion.screenRect를 찾지 못해 패널 클릭 영역을 둘 수 없습니다(공용 카메라 코드가 바뀜).", this);
                return;
            }
            network = Region("CursorRegion_Network");
            bottomLeft = Region("CursorRegion_BottomLeft");
            bottomRight = Region("CursorRegion_BottomRight");
        }

        void Update()
        {
            var director = CameraViewDirector.Current;
            if (director != null) director.SetInputBlocked(CurtainCall.Player.InputGate.IsBlocked);

            float w = Screen.width, h = Screen.height;
            Set(network, new Rect(10f, 10f, 340f, 320f));                              // 공용 접속 패널
            Set(bottomLeft, new Rect(10f, h - ExpertServiceTestUI.BottomReserve, 370f, ExpertServiceTestUI.BottomReserve)); // 5단계 패널 + 소지 안내
            Set(bottomRight, new Rect(w - 320f, h - 320f, 320f, 320f));               // 4단계 밤 정비 패널
        }

        CameraCursorExclusion Region(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<CameraCursorExclusion>();
        }

        static void Set(CameraCursorExclusion region, Rect rect)
        {
            if (region != null && RectField != null) RectField.SetValue(region, rect);
        }
    }
}
