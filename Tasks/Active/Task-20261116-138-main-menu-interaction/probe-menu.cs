var menu = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenu>();
var title = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuHangingTitle>();
var rope = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.TitleRopeGraphic>();
var mesh = rope.canvasRenderer.GetMesh();

var result = new { rope = new { active = rope.isActiveAndEnabled, color = rope.color.ToString(),
    cull = rope.canvasRenderer.cull, rect = rope.rectTransform.rect.ToString(), pos = rope.transform.localPosition.ToString(),
    vertices = mesh.vertexCount, bounds = mesh.bounds.ToString(), siblings = rope.transform.GetSiblingIndex(),
    shader = rope.materialForRendering.shader.name, canvas = rope.canvas.name }, titleIndex = title.transform.GetSiblingIndex(),
    images = menu.GetComponentsInChildren<UnityEngine.UI.Image>(true).Length };

return result;

