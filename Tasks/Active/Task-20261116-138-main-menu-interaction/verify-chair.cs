var backdrop = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuBackdrop>();
var body = backdrop.GetComponentInChildren<Rigidbody>();
var area = (Bounds)typeof(CurtainCall.StartLobby.StartMenuBackdrop).GetField("chairArea", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(backdrop);
var bounds = body.GetComponentInChildren<Collider>().bounds;
foreach (var c in body.GetComponentsInChildren<Collider>()) bounds.Encapsulate(c.bounds);
bool contained = bounds.min.x >= area.min.x - .03f && bounds.max.x <= area.max.x + .03f && bounds.max.z <= area.max.z + .03f;
if (!contained) throw new Exception("Chair escaped fabric-safe region");
ScreenCapture.CaptureScreenshot("D:/github/POC_CURTAINCALL/.harness/local/chair-boundary.png");
return new { contained = contained, tilted = Vector3.Dot(body.transform.up, Vector3.up) < .75f,
    chairBack = bounds.max.z, curtainSafeBoundary = area.max.z, position = body.position.ToString() };
