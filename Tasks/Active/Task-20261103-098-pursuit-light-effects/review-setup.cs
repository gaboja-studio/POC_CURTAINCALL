// Play 모드에서 실행: 추격을 시작하고 S02_Dormitory 안에 검증용 메인 카메라를 둔다.
var d = UnityEngine.Object.FindFirstObjectByType<CurtainCall.WaitingRoom.Pursuit.PursuitDirector>();
d.DebugStart();
var room = d.Rooms.First(r => r != null && r.name == "S02_Dormitory");
var b = room.Area.bounds; var old = UnityEngine.Camera.main;
var go = new UnityEngine.GameObject("LightingReviewCam"); var cam = go.AddComponent<UnityEngine.Camera>(); go.tag = "MainCamera";
if (old != null) { old.enabled = false; old.tag = "Untagged"; }
var floor = b.min.y; if (UnityEngine.Physics.Raycast(b.center, UnityEngine.Vector3.down, out var hit, 20)) floor = hit.point.y;
var p = new UnityEngine.Vector3(b.center.x - 3f, floor + 1.6f, b.min.z + 2f);
cam.transform.position = p; cam.transform.LookAt(new UnityEngine.Vector3(b.center.x, floor + 1.2f, b.center.z + 4));
cam.fieldOfView = 70;
return $"room={room.name} inView={room.ContainsView(p)}";
