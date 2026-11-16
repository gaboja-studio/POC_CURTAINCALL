var backdrop = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuBackdrop>();
var body = backdrop.GetComponentInChildren<Rigidbody>();
var bounds = body.GetComponentInChildren<Collider>().bounds;
body.AddForceAtPosition(new Vector3(16, 0, 90), new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), ForceMode.Impulse);
return new { impulse = 90, initialPosition = body.position.ToString() };
