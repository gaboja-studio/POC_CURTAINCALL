using UnityEngine;

namespace CurtainCall.WaitingRoom
{
    /// <summary>
    /// 관리실 대형 모니터 한 대. 대기실 곳곳에 달린 CCTV 위치를 몇 초마다 바꿔 가며 저해상도·저프레임으로 비춘다.
    /// 카메라는 평소 꺼 두고 갱신할 때만 한 프레임 켜서 렌더 텍스처에 그린다. 모니터가 어떤 카메라에도 보이지 않으면
    /// 갱신하지 않으므로 관리실 밖에서는 비용이 없다.
    /// </summary>
    public sealed class WaitingRoomCctvMonitor : MonoBehaviour
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [Tooltip("영상이 나오는 화면 렌더러(URP Unlit 재질).")]
        public Renderer screen;

        [Tooltip("화면 구석의 채널 표시.")]
        public TextMesh label;

        [Tooltip("CCTV 카메라가 달린 위치. 카메라는 이 위치·방향을 그대로 따른다.")]
        public Transform[] mounts;

        [Tooltip("위치마다 표시할 이름(mounts와 같은 순서).")]
        public string[] mountNames;

        [Tooltip("채널 번호 시작값. 모니터마다 다르게 준다.")]
        public int firstChannel = 1;

        [Min(2f)] public float secondsPerMount = 8f;
        [Range(1f, 30f)] public float framesPerSecond = 6f;
        public Vector2Int resolution = new Vector2Int(384, 216);

        Camera _camera;
        RenderTexture _texture;
        MaterialPropertyBlock _block;
        int _index;
        float _nextSwitch, _nextFrame;
        bool _rendering;

        void Awake()
        {
            _texture = new RenderTexture(resolution.x, resolution.y, 16) { name = name + "_Feed", filterMode = FilterMode.Bilinear };
            var go = new GameObject(name + "_Camera") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            _camera = go.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.fieldOfView = 70f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 60f;
            _camera.targetTexture = _texture;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _block = new MaterialPropertyBlock();
            _block.SetTexture(BaseMapId, _texture);
            if (screen) screen.SetPropertyBlock(_block);
            _index = Random.Range(0, Mathf.Max(1, mounts?.Length ?? 1));
            _nextSwitch = Time.time + secondsPerMount * Random.Range(0.4f, 1f); // 세 대가 같이 바뀌지 않게
            ShowMount();
        }

        void Update()
        {
            if (_rendering) { _camera.enabled = false; _rendering = false; }
            if (mounts == null || mounts.Length == 0 || !screen || !screen.isVisible) return;

            float t = Time.time;
            if (t >= _nextSwitch)
            {
                _index = (_index + 1) % mounts.Length;
                _nextSwitch = t + secondsPerMount;
                ShowMount();
                _nextFrame = t; // 바뀐 화면은 바로 그린다
            }
            if (t < _nextFrame) return;
            _nextFrame = t + 1f / framesPerSecond;

            var mount = mounts[_index];
            if (!mount) return;
            _camera.transform.SetPositionAndRotation(mount.position, mount.rotation);
            _camera.enabled = true; // 이번 프레임 끝에 한 번 그리고 다음 Update에서 끈다
            _rendering = true;
        }

        void ShowMount()
        {
            if (!label) return;
            string where = mountNames != null && _index < mountNames.Length ? mountNames[_index] : "";
            label.text = $"CAM {firstChannel + _index:00}  {where}";
        }

        void OnDestroy()
        {
            if (_camera) Destroy(_camera.gameObject);
            if (_texture) { _texture.Release(); Destroy(_texture); }
        }
    }
}
