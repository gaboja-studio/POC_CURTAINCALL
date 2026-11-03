var d = CurtainCall.WaitingRoom.Pursuit.PursuitDirector.Current;
if (d == null) return new { error = "Pursuit scene must be playing and initialized" };
var roots = d.gameObject.scene.GetRootGameObjects();
var renderers = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
var active = renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
var lobby = d.Rooms.FirstOrDefault(r => r != null && r.name == "Lobby");
var nearLobby = lobby == null || lobby.Area == null ? System.Array.Empty<MeshRenderer>() : active.Where(r => lobby.Area.bounds.Contains(r.bounds.center)).ToArray();
var lights = roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)).ToArray();
object Stats(MeshRenderer[] rs) => new {
    renderers = rs.Length,
    staticObjects = rs.Count(r => r.gameObject.isStatic),
    batchingStatic = rs.Count(r => (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.BatchingStatic) != 0),
    staticBatched = rs.Count(r => r.isPartOfStaticBatch),
    shadowCasters = rs.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off),
    shaders = rs.GroupBy(r => r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "none").Select(g => new { shader = g.Key, count = g.Count() }).OrderByDescending(g => g.count).Take(10).ToArray(),
    materials = rs.GroupBy(r => r.sharedMaterial).Where(g => g.Key != null).Select(g => new { path = AssetDatabase.GetAssetPath(g.Key), material = g.Key.name, count = g.Count(), instancing = g.Key.enableInstancing }).OrderByDescending(g => g.count).Take(15).ToArray(),
    meshes = rs.Select(r => r.GetComponent<MeshFilter>()).Where(f => f != null && f.sharedMesh != null).GroupBy(f => f.sharedMesh).Select(g => new { path = AssetDatabase.GetAssetPath(g.Key), mesh = g.Key.name, count = g.Count(), vertices = g.Key.vertexCount }).OrderByDescending(g => g.count).Take(15).ToArray()
};
var report = new {
    measuredAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
    note = "Read-only runtime scene audit. Lobby selection uses renderer bounds center; counts are not visible draw calls. No flags, materials, lights or scene assets modified.",
    scene = Stats(active), lobby = Stats(nearLobby),
    lights = lights.Where(l => l.isActiveAndEnabled && l.shadows != LightShadows.None).Select(l => new { name = l.name, type = l.type.ToString(), range = l.range, shadows = l.shadows.ToString(), shadowResolution = l.shadowResolution.ToString() }).ToArray()
};
var path = System.IO.Path.GetFullPath(".harness/local/pursuit-render-audit.json");
System.IO.File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
return new { path, renderers = active.Length, lobbyRenderers = nearLobby.Length };
