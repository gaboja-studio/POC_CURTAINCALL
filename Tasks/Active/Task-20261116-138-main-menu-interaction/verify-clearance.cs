var menu = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenu>();
var title = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuHangingTitle>();
var rect = (RectTransform)title.transform;
var main = (RectTransform)menu.mainPage.transform;
var corners = new Vector3[4]; main.GetWorldCorners(corners);
float menuTop = Mathf.Max(corners[1].y, corners[2].y);
float bottom = Mathf.Min(rect.TransformPoint(new Vector3(-rect.rect.width * .48f, -rect.rect.height * .34f)).y,
    rect.TransformPoint(new Vector3(rect.rect.width * .48f, -rect.rect.height * .34f)).y);
if (bottom - menuTop < 23f) throw new Exception("Title overlaps menu");
var ropes = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.TitleRopeGraphic>();
if (ropes.canvasRenderer.GetMesh() == null || ropes.canvasRenderer.GetMesh().vertexCount < 100) throw new Exception("Rope geometry missing");
ScreenCapture.CaptureScreenshot("D:/github/POC_CURTAINCALL/.harness/local/title-slipped.png");
return new { drop = title.Drop, leftSlipped = title.LeftSlipped, menuClearance = bottom - menuTop, ropeVertices = ropes.canvasRenderer.GetMesh().vertexCount };
