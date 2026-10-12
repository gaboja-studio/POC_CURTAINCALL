// Waiting room layout v3 (circus dressing, stage tunnel, cloth curtains, flicker, fog). Run with: unity command eval "<this file>"
// Rebuilds the "WaitingRoom" root in Assets/Scenes/Levels/waiting_room.unity. Uses only Assets/_ThirdParty prefabs/materials.
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/Scenes/Levels/waiting_room.unity") return "WRONG SCENE " + scene.path;
var log = new System.Text.StringBuilder();
var A = "Assets/_ThirdParty/Synty/PolygonApocalypse/Prefabs/";
var GB = "Assets/_ThirdParty/Synty/PolygonGeneric/Prefabs/Base/";
var cache = new System.Collections.Generic.Dictionary<string, UnityEngine.GameObject>();
System.Func<string, UnityEngine.GameObject> Load = (p) => {
  UnityEngine.GameObject g;
  if (!cache.TryGetValue(p, out g)) { var full = p.StartsWith("Assets/") ? p : A + p;
    if (p.StartsWith("@")) { full = null; foreach (var gid in UnityEditor.AssetDatabase.FindAssets(p.Substring(1) + " t:Prefab")) { var gp = UnityEditor.AssetDatabase.GUIDToAssetPath(gid); if (gp.Contains("_ThirdParty") && System.IO.Path.GetFileNameWithoutExtension(gp) == p.Substring(1)) { full = gp.Substring(0, gp.Length - 7); break; } } if (full == null) full = p; } g = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(full + ".prefab"); cache[p] = g; if (g == null) log.AppendLine("MISSING " + full); }
  return g;
};
var mcache = new System.Collections.Generic.Dictionary<string, UnityEngine.Material>();
System.Func<string, UnityEngine.Material> Mat = (n) => {
  UnityEngine.Material m;
  if (!mcache.TryGetValue(n, out m)) {
    foreach (var id in UnityEditor.AssetDatabase.FindAssets(n + " t:Material")) { var p = UnityEditor.AssetDatabase.GUIDToAssetPath(id); if (p.Contains("_ThirdParty") && System.IO.Path.GetFileNameWithoutExtension(p) == n) { m = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(p); break; } }
    mcache[n] = m; if (m == null) log.AppendLine("MISSING MAT " + n);
  }
  return m;
};
var V3 = new System.Func<float, float, float, UnityEngine.Vector3>((x, y, z) => new UnityEngine.Vector3(x, y, z));
var RED = "PolygonApocalypse_03_A"; var YEL = "PolygonApocalypse_02_A"; var NAVY = "PolygonApocalypse_04_A"; // tent fabric: red / gold / navy

// ---- reset previous build
foreach (var r in scene.GetRootGameObjects()) if (r.name == "WaitingRoom" || r.name == "Cube") UnityEngine.Object.DestroyImmediate(r);
var root = new UnityEngine.GameObject("WaitingRoom");
UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);

System.Func<string, UnityEngine.Transform, UnityEngine.Transform> Group = (n, parent) => {
  var t = parent.Find(n); if (t != null) return t;
  var g = new UnityEngine.GameObject(n); g.transform.SetParent(parent, false); return g.transform;
};
System.Action<UnityEngine.GameObject, string> Paint = (go, mn) => {
  if (go == null || string.IsNullOrEmpty(mn)) return; var m = Mat(mn); if (m == null) return;
  foreach (var r in go.GetComponentsInChildren<UnityEngine.Renderer>()) { var arr = r.sharedMaterials; for (int k = 0; k < arr.Length; k++) arr[k] = m; r.sharedMaterials = arr; }
};
// raw pivot placement (modular pieces)
System.Func<string, UnityEngine.Transform, UnityEngine.Vector3, float, UnityEngine.GameObject> S = (p, parent, pos, ry) => {
  var src = Load(p); if (src == null) return null;
  var go = (UnityEngine.GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(src, scene);
  go.transform.SetParent(parent, false);
  go.transform.position = pos; go.transform.rotation = UnityEngine.Quaternion.Euler(0, ry, 0);
  return go;
};
// bounds-aligned placement: renderer bounds center xz -> (x,z), bottom -> yb
System.Func<string, UnityEngine.Transform, float, float, float, float, UnityEngine.GameObject> P = (p, parent, x, z, ry, yb) => {
  var go = S(p, parent, UnityEngine.Vector3.zero, ry); if (go == null) return null;
  var rs = go.GetComponentsInChildren<UnityEngine.Renderer>();
  if (rs.Length == 0) { go.transform.position = V3(x, yb, z); return go; }
  var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
  go.transform.position += V3(x - b.center.x, yb - b.min.y, z - b.center.z);
  return go;
};
System.Action<UnityEngine.GameObject, float, float, float> Realign = (go, x, z, yb) => {
  var rs = go.GetComponentsInChildren<UnityEngine.Renderer>(); var b = rs[0].bounds; foreach (var rr in rs) b.Encapsulate(rr.bounds);
  go.transform.position += V3(x - b.center.x, yb - b.min.y, z - b.center.z);
};
var warm = new UnityEngine.Color(1f, 0.62f, 0.32f);
var redLight = new UnityEngine.Color(1f, 0.28f, 0.2f);
System.Func<UnityEngine.Transform, string, UnityEngine.Vector3, UnityEngine.LightType, float, float, UnityEngine.Color, UnityEngine.GameObject> L = (parent, n, pos, type, intensity, range, col) => {
  var g = new UnityEngine.GameObject(n); g.transform.SetParent(parent, false); g.transform.position = pos;
  var l = g.AddComponent<UnityEngine.Light>(); l.type = type; l.intensity = intensity; l.range = range; l.color = col;
  l.shadows = UnityEngine.LightShadows.None;
  if (type == UnityEngine.LightType.Spot) { g.transform.rotation = UnityEngine.Quaternion.Euler(90, 0, 0); l.spotAngle = 60; l.innerSpotAngle = 30; }
  return g;
};
// marquee bulb: built-in sphere + Synty glow material, no collider
var bulbMat = new UnityEngine.Material(Mat("Generic_Candle_Glow_01")); bulbMat.name = "WR_Bulb_Warm"; // embedded in the scene, third-party asset untouched
bulbMat.SetColor("_BaseColor", new UnityEngine.Color(1.35f, 0.82f, 0.28f, 1f));
int nBulb = 0;
System.Action<UnityEngine.Transform, UnityEngine.Vector3, float> Bulb = (parent, pos, size) => {
  var s = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Sphere); s.name = "Bulb";
  UnityEngine.Object.DestroyImmediate(s.GetComponent<UnityEngine.Collider>());
  s.transform.SetParent(parent, false); s.transform.position = pos; s.transform.localScale = UnityEngine.Vector3.one * size;
  var mr = s.GetComponent<UnityEngine.MeshRenderer>(); mr.sharedMaterial = bulbMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; nBulb++;
};
System.Action<UnityEngine.Transform, UnityEngine.Vector3, UnityEngine.Vector3, float, float> BulbLine = (parent, a, b, spacing, sag) => {
  int n = UnityEngine.Mathf.Max(2, UnityEngine.Mathf.RoundToInt((b - a).magnitude / spacing));
  for (int i = 0; i <= n; i++) { float t = (float)i / n; var p = UnityEngine.Vector3.Lerp(a, b, t); p.y -= sag * 4f * t * (1f - t); Bulb(parent, p, 0.13f); }
};

// ---- rooms: id, key, x0,x1,z0,z1 (m, 2.5 grid), height, floor prefab, wall style
// wall style: "B:<bunker prefab prefix>" (variant 1/2 appended) or "G:<material>" (PolygonGeneric Base_Wall_01 repainted)
var G = 2.5f;
var rooms = new object[][] {
  new object[]{1,  "R01_Lobby",            -10f, 10f, -10f, 10f, 9f, "Buildings/SM_Bld_Bunker_Floor_Wood_02",     "G:Generic_Wood"},
  new object[]{2,  "R02_Dormitory",        -25f,-10f,   5f, 20f, 6f, "Buildings/SM_Bld_Bunker_Concrete_Floor_01", "B:Buildings/SM_Bld_Bunker_Wall_Concrete_x1_0"},
  new object[]{3,  "R03_Restroom",          10f,17.5f, 2.5f, 10f, 6f, "Buildings/SM_Bld_Bunker_Floor_Stone_01",    "B:Buildings/SM_Bld_Bunker_Wall_Slab_x1_0"},
  new object[]{4,  "R04_DressingRoom",       0f,12.5f,  10f, 20f, 6f, "Buildings/SM_Bld_Bunker_Floor_Wood_02",     "G:Generic_Brick"},
  new object[]{5,  "R05_Shop",           -22.5f,-10f,  -5f,  5f, 6f, "Buildings/SM_Bld_Bunker_Floor_Wood_03",     "G:Generic_Brick"},
  new object[]{6,  "R06_PropRoom",           0f,12.5f, -20f,-10f, 6f, "Buildings/SM_Bld_Bunker_Floor_Wood_01",     "G:Generic_Wood"},
  new object[]{7,  "R07_Lounge",           -25f,-10f, -20f, -5f, 6f, "Buildings/SM_Bld_Bunker_Floor_Wood_01",     "G:Generic_Brick"},
  new object[]{8,  "R08_JunkStorage_Hidden",-15f,-2.5f,22.5f,32.5f,6f, "Buildings/SM_Bld_Bunker_Concrete_Floor_01", "B:Buildings/SM_Bld_Bunker_Wall_Container_x1_0"},
  new object[]{9,  "R09_StageCorridor",     10f,47.5f, -5f,  0f, 4.5f, "Buildings/SM_Bld_Bunker_Floor_Wood_03",     "G:Generic_Wood"},
  new object[]{10, "R10_StageEntrance",   47.5f,57.5f, -5f,  0f, 4.5f, "Buildings/SM_Bld_Bunker_Floor_Wood_02",     "G:Generic_Wood"},
  new object[]{11, "R08a_HiddenPassage",  -17.5f,-15f, 20f,27.5f,3f, "Buildings/SM_Bld_Bunker_Concrete_Floor_01", "B:Buildings/SM_Bld_Bunker_Wall_Container_x1_0"},
};
var cell = new System.Collections.Generic.Dictionary<long, int>();
var info = new System.Collections.Generic.Dictionary<int, object[]>();
var rootOf = new System.Collections.Generic.Dictionary<int, UnityEngine.Transform>();
System.Func<int, int, long> K = (i, j) => (long)(i + 1000) * 10000 + (j + 1000);
foreach (var r in rooms) {
  int id = (int)r[0]; info[id] = r;
  rootOf[id] = Group((string)r[1], root.transform);
  int i0 = UnityEngine.Mathf.RoundToInt((float)r[2] / G), i1 = UnityEngine.Mathf.RoundToInt((float)r[3] / G);
  int j0 = UnityEngine.Mathf.RoundToInt((float)r[4] / G), j1 = UnityEngine.Mathf.RoundToInt((float)r[5] / G);
  for (int i = i0; i < i1; i++) for (int j = j0; j < j1; j++) { if (cell.ContainsKey(K(i, j))) log.AppendLine("OVERLAP " + id + " " + i + "," + j); cell[K(i, j)] = id; }
}
// ---- special edges: V,xi,zj = wall on x = xi*G covering z cell zj ; H,xi,zj = wall on z = zj*G covering x cell xi
var edge = new System.Collections.Generic.Dictionary<string, string>();
edge["V,-4,2"] = "door";  edge["V,-4,-1"] = "door"; edge["V,-4,-3"] = "door";   // lobby -> dorm, shop, lounge
edge["H,1,4"] = "door";   edge["H,1,-4"] = "door";                             // lobby -> dressing, prop room
edge["V,4,2"] = "door";                                                         // lobby -> restroom
edge["V,4,-2"] = "open";  edge["V,4,-1"] = "open";                             // lobby -> corridor
edge["V,19,-2"] = "open"; edge["V,19,-1"] = "open";                            // corridor -> stage entrance
edge["H,-7,8"] = "open";                                                        // dorm -> hidden passage
edge["V,-6,10"] = "open";                                                       // hidden passage -> junk storage
edge["V,23,-2"] = "skip"; edge["V,23,-1"] = "skip";                            // big stage door (placed below)
for (int k = 0; k < 5; k++) { int xi = 10 + 2 * k; edge["H," + xi + ",0"] = "slot"; edge["H," + xi + ",-2"] = "slot"; } // backstage slot doors (closed)

int nFloor = 0, nWall = 0, nCeil = 0, nProp = 0;
var genWall = GB + "SM_Bld_Base_Wall_01";
var genDoor = GB + "SM_Bld_Base_Wall_Door_Double_Large_01";
var bunkerDoor = "Buildings/SM_Bld_Bunker_Wall_Concrete_Door_01";
var slotDoor = "Buildings/SM_Bld_Bunker_Wall_Container_Door_01";
System.Func<string, UnityEngine.Transform, UnityEngine.Vector3, float, int, UnityEngine.GameObject> WallPiece = (style, parent, pos, ry, variant) => {
  if (style.StartsWith("G:")) { var w = S(genWall, parent, pos, ry); Paint(w, style.Substring(2)); return w; }
  return S(style.Substring(2) + variant, parent, pos, ry);
};
System.Action<int, string, bool, int, int, float> PlaceEdge = (rid, style, vertical, xi, zj, height) => {
  var key = (vertical ? "V," : "H,") + xi + "," + zj;
  string type; if (!edge.TryGetValue(key, out type)) type = "wall";
  var parent = Group("Walls", rootOf[rid]);
  var pos = vertical ? V3(xi * G, 0, zj * G) : V3((xi + 1) * G, 0, zj * G);
  float ry = vertical ? 90f : 0f;
  int variant = ((xi * 7 + zj * 13) & 1) + 1;
  if (type == "wall") { WallPiece(style, parent, pos, ry, variant); nWall++; }
  else if (type == "door") {
    if (style.StartsWith("G:")) { var dg = S(genDoor, parent, pos, ry); Paint(dg, style.Substring(2)); }
    else {
      var dg = S(bunkerDoor, parent, pos, ry);
      foreach (var bc in dg.GetComponents<UnityEngine.BoxCollider>()) if (bc.center.y < 0.5f) bc.enabled = false; // threshold > stepOffset 0.3
      foreach (var leaf in dg.GetComponentsInChildren<UnityEngine.Transform>()) {
        if (leaf.name.EndsWith("_Door_L")) leaf.localRotation = UnityEngine.Quaternion.Euler(0, 100, 0);
        if (leaf.name.EndsWith("_Door_R")) leaf.localRotation = UnityEngine.Quaternion.Euler(0, -100, 0);
      }
    }
    nWall++;
  }
  else if (type == "slot") { var sd = S(slotDoor, Group("BackstageSlots", rootOf[rid]), pos, ry); sd.name = "Slot_" + (zj == 0 ? "U" : "D") + ((xi - 10) / 2 + 1) + "_" + sd.name; nWall++; }
  for (float y = 3f; y < height - 0.01f; y += 3f) { WallPiece(style, parent, pos + V3(0, y, 0), ry, variant); nWall++; }
};
foreach (var kv in cell) {
  int i = (int)(kv.Key / 10000) - 1000, j = (int)(kv.Key % 10000) - 1000, id = kv.Value;
  var r = info[id]; float h = (float)r[6];
  S((string)r[7], Group("Floor", rootOf[id]), V3((i + 1) * G, -0.25f, j * G), 0); nFloor++;
  S("Buildings/SM_Bld_Bunker_Ceiling_Concrete_0" + (((i + j) & 1) + 1), Group("Ceiling", rootOf[id]), V3((i + 1) * G, h + 0.25f, j * G), 0); nCeil++;
  var dirs = new int[][] { new[]{-1,0}, new[]{1,0}, new[]{0,-1}, new[]{0,1} };
  foreach (var d in dirs) {
    int n; if (!cell.TryGetValue(K(i + d[0], j + d[1]), out n)) n = 0;
    if (n == id) continue;
    if (n != 0 && n < id) continue;
    float eh = UnityEngine.Mathf.Max(h, n != 0 ? (float)info[n][6] : 0f);
    if (d[0] == -1) PlaceEdge(id, (string)r[8], true, i, j, eh);
    else if (d[0] == 1) PlaceEdge(id, (string)r[8], true, i + 1, j, eh);
    else if (d[1] == -1) PlaceEdge(id, (string)r[8], false, i, j, eh);
    else PlaceEdge(id, (string)r[8], false, i, j + 1, eh);
  }
}
// big stage door (east wall of R10, z -5..0)
var bigDoor = "Buildings/SM_Bld_Bunker_Wall_Metal_Door_x2_01";
S(bigDoor, Group("Walls", rootOf[10]), V3(57.5f, 0, -5f), 90); nWall++;
for (float y = 3f; y < 6f; y += 3f) { WallPiece("G:Generic_Wood", Group("Walls", rootOf[10]), V3(57.5f, y, -5f), 90, 1); WallPiece("G:Generic_Wood", Group("Walls", rootOf[10]), V3(57.5f, y, -2.5f), 90, 1); nWall += 2; }

// ---- props / lights helpers
System.Func<int, UnityEngine.Transform> PR = (id) => Group("Props", rootOf[id]);
System.Func<int, UnityEngine.Transform> LT = (id) => Group("Lights", rootOf[id]);
var Pt = UnityEngine.LightType.Point; var Sp = UnityEngine.LightType.Spot;
// hanging bulb lamp: pendant prop under the ceiling, glow bulb at its tip, warm point light
System.Action<int, float, float, float, float, float> Lamp = (id, x, z, h, inten, range) => {
  P("Props/SM_Prop_Bunker_Light_01", PR(id), x, z, 0, h - 0.8f); nProp++;
  Bulb(PR(id), V3(x, h - 0.85f, z), 0.18f);
  L(LT(id), "PointLight", V3(x, h - 1.1f, z), Pt, inten * 1.8f, range * 1.25f, warm);
};
// striped awning (red/white sunshade) centred at (x,z), facing yaw ry, width w
System.Action<int, float, float, float, float, float> Awning = (id, x, z, ry, y, w) => {
  var a = P("Buildings/SM_Bld_Sunshade_01", PR(id), x, z, ry, y); a.transform.localScale = V3(w / 6.56f, 1, 1);
  var rs = a.GetComponentsInChildren<UnityEngine.Renderer>(); var b = rs[0].bounds; foreach (var rr in rs) b.Encapsulate(rr.bounds);
  a.transform.position += V3(x - b.center.x, y - b.min.y, z - b.center.z); nProp++;
};
// bunting line from a to b (FlagLine_0x pivot is its centre, length along local x)
System.Action<int, string, UnityEngine.Vector3, UnityEngine.Vector3, float> Bunting = (id, prefab, a, b, baseLen) => {
  var mid = (a + b) * 0.5f; var dir = b - a;
  var f = S(prefab, PR(id), mid, 0);
  f.transform.rotation = UnityEngine.Quaternion.FromToRotation(UnityEngine.Vector3.right, dir.normalized);
  f.transform.localScale = V3(dir.magnitude / baseLen, 1, 1); nProp++;
};
// bulb frame around a rectangle (vertical plane), used for marquee mirrors / door arches
System.Action<UnityEngine.Transform, UnityEngine.Vector3, UnityEngine.Vector3, float, float, float> BulbFrame = (parent, center, right, w, hgt, spacing) => {
  var up = UnityEngine.Vector3.up;
  var c0 = center - right * (w / 2) - up * (hgt / 2); var c1 = center + right * (w / 2) - up * (hgt / 2);
  var c2 = center + right * (w / 2) + up * (hgt / 2); var c3 = center - right * (w / 2) + up * (hgt / 2);
  BulbLine(parent, c0, c3, spacing, 0); BulbLine(parent, c3, c2, spacing, 0); BulbLine(parent, c2, c1, spacing, 0);
};

// ---- v3 helpers: flicker animation, cloth curtains, particle fog/dust (no scripts: built-in Animation/Cloth/ParticleSystem only)
int nCloth = 0, nFlicker = 0, nFx = 0;
var rnd = new System.Random(20261012);
// legacy AnimationClip embedded in the scene. target "" = the holder itself.
System.Func<string, float, float, UnityEngine.AnimationClip> FlickerClip = (lightPath, baseInt, period) => {
  var clip = new UnityEngine.AnimationClip(); clip.legacy = true; clip.name = "WR_Flicker"; clip.wrapMode = UnityEngine.WrapMode.Loop;
  var ci = new UnityEngine.AnimationCurve(); var ce = new UnityEngine.AnimationCurve();
  float t = 0f; ci.AddKey(0f, baseInt); ce.AddKey(0f, 1f);
  while (t < period - 0.6f) {
    t += 0.4f + (float)rnd.NextDouble() * 1.6f;                      // steady stretch
    int bursts = 1 + rnd.Next(3);
    for (int b = 0; b < bursts && t < period - 0.3f; b++) {
      ci.AddKey(t, baseInt); ce.AddKey(t, 1f);
      t += 0.04f + (float)rnd.NextDouble() * 0.08f; ci.AddKey(t, baseInt * 0.05f); ce.AddKey(t, 0f);
      t += 0.03f + (float)rnd.NextDouble() * 0.12f; ci.AddKey(t, baseInt * (0.7f + (float)rnd.NextDouble() * 0.4f)); ce.AddKey(t, 1f);
    }
  }
  ci.AddKey(period, baseInt); ce.AddKey(period, 1f);
  for (int k = 0; k < ci.length; k++) { UnityEditor.AnimationUtility.SetKeyLeftTangentMode(ci, k, UnityEditor.AnimationUtility.TangentMode.Constant); UnityEditor.AnimationUtility.SetKeyRightTangentMode(ci, k, UnityEditor.AnimationUtility.TangentMode.Constant); }
  for (int k = 0; k < ce.length; k++) { UnityEditor.AnimationUtility.SetKeyLeftTangentMode(ce, k, UnityEditor.AnimationUtility.TangentMode.Constant); UnityEditor.AnimationUtility.SetKeyRightTangentMode(ce, k, UnityEditor.AnimationUtility.TangentMode.Constant); }
  if (lightPath != null) clip.SetCurve(lightPath, typeof(UnityEngine.Light), "m_Intensity", ci);
  clip.SetCurve(lightPath == null ? "" : "Bulb", typeof(UnityEngine.MeshRenderer), "m_Enabled", ce);
  return clip;
};
System.Action<UnityEngine.GameObject, UnityEngine.AnimationClip> AddAnim = (holder, clip) => {
  var an = holder.AddComponent<UnityEngine.Animation>(); an.AddClip(clip, clip.name); an.clip = clip; an.playAutomatically = true; an.wrapMode = UnityEngine.WrapMode.Loop; nFlicker++;
};
// flickering hanging lamp: holder(Animation) -> Bulb + PointLight
System.Action<int, float, float, float, float, float> FlickerLamp = (id, x, z, h, inten, range) => {
  P("Props/SM_Prop_Bunker_Light_01", PR(id), x, z, 0, h - 0.8f); nProp++;
  var holder = new UnityEngine.GameObject("FlickerLamp"); holder.transform.SetParent(LT(id), false); holder.transform.position = V3(x, h - 0.85f, z);
  Bulb(holder.transform, V3(x, h - 0.85f, z), 0.18f);
  var pl = L(holder.transform, "PointLight", V3(x, h - 1.1f, z), Pt, inten * 1.8f, range * 1.25f, warm);
  AddAnim(holder, FlickerClip("PointLight", inten * 1.8f, 4f + (float)rnd.NextDouble() * 4f));
};
// flickering marquee bulb (renderer only)
System.Action<UnityEngine.Transform, UnityEngine.Vector3, float> FlickerBulb = (parent, pos, size) => {
  Bulb(parent, pos, size); var b = parent.GetChild(parent.childCount - 1).gameObject; b.name = "FlickerBulb";
  AddAnim(b, FlickerClip(null, 1f, 3f + (float)rnd.NextDouble() * 5f));
};
System.Action<UnityEngine.Transform, UnityEngine.Vector3, UnityEngine.Vector3, float, float, int> BulbLineFlicker = (parent, a, b, spacing, sag, every) => {
  int n = UnityEngine.Mathf.Max(2, UnityEngine.Mathf.RoundToInt((b - a).magnitude / spacing));
  for (int i = 0; i <= n; i++) { float t = (float)i / n; var p = UnityEngine.Vector3.Lerp(a, b, t); p.y -= sag * 4f * t * (1f - t); if (i % every == every / 2) FlickerBulb(parent, p, 0.13f); else Bulb(parent, p, 0.13f); }
};

// velvet material (embedded copy of a Synty Generic_Basic material, texture removed, two-sided)
var velvet = new UnityEngine.Material(Mat("Generic_Plaster")); velvet.name = "WR_Curtain_Velvet";
velvet.SetTexture("_Albedo_Map", null); if (velvet.HasProperty("_Normal_Map")) velvet.SetTexture("_Normal_Map", null); velvet.DisableKeyword("_NORMALMAP");
velvet.SetColor("_BaseColor", new UnityEngine.Color(0.42f, 0.035f, 0.05f, 1f));
if (velvet.HasProperty("_Smoothness")) velvet.SetFloat("_Smoothness", 0.12f);
if (velvet.HasProperty("_Cull")) velvet.SetFloat("_Cull", 0f);
velvet.doubleSidedGI = true;
var hemMat = new UnityEngine.Material(velvet); hemMat.name = "WR_Curtain_Gold"; hemMat.SetColor("_BaseColor", new UnityEngine.Color(0.75f, 0.52f, 0.12f, 1f));

// pleated grid mesh (pivot = top centre), embedded in the scene
System.Func<float, float, int, int, UnityEngine.Mesh> CurtainMesh = (w, h, nx, ny) => {
  var m = new UnityEngine.Mesh(); m.name = "WR_CurtainMesh_" + w.ToString("F1") + "x" + h.ToString("F1");
  var v = new UnityEngine.Vector3[(nx + 1) * (ny + 1)]; var uv = new UnityEngine.Vector2[v.Length];
  for (int j = 0; j <= ny; j++) for (int i = 0; i <= nx; i++) {
    float u = (float)i / nx, t = (float)j / ny;
    v[j * (nx + 1) + i] = new UnityEngine.Vector3((u - 0.5f) * w, -t * h, 0.07f * UnityEngine.Mathf.Sin(u * nx * UnityEngine.Mathf.PI * 0.5f));
    uv[j * (nx + 1) + i] = new UnityEngine.Vector2(u, 1f - t);
  }
  var tris = new int[nx * ny * 6]; int q = 0;
  for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++) {
    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
    tris[q++] = a; tris[q++] = c; tris[q++] = b; tris[q++] = b; tris[q++] = c; tris[q++] = d;
  }
  m.vertices = v; m.uv = uv; m.triangles = tris; m.RecalculateNormals(); m.RecalculateBounds();
  return m;
};
var meshCache = new System.Collections.Generic.Dictionary<string, UnityEngine.Mesh>();
// player capsule the cloth reacts to (trigger, so it never changes CharacterController movement)
UnityEngine.CapsuleCollider playerCloth = null;
foreach (var r in scene.GetRootGameObjects()) if (r.name == "PlaySetup") {
  var pl = r.transform.Find("Player");
  if (pl != null) {
    var old = pl.Find("ClothCollider"); if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
    var cgo = new UnityEngine.GameObject("ClothCollider"); cgo.transform.SetParent(pl, false);
    playerCloth = cgo.AddComponent<UnityEngine.CapsuleCollider>(); playerCloth.isTrigger = true; playerCloth.radius = 0.38f; playerCloth.height = 1.6f; playerCloth.center = V3(0, 0.8f, 0);
  }
}
if (playerCloth == null) log.AppendLine("WARN no PlaySetup/Player for cloth collider");
// one cloth panel: top edge pinned, rest free. pos = top centre, ry = yaw (panel faces local +z)
System.Func<UnityEngine.Transform, string, UnityEngine.Vector3, float, float, float, UnityEngine.GameObject> ClothPanel = (parent, name, pos, ry, w, h) => {
  string key = w.ToString("F2") + "x" + h.ToString("F2");
  UnityEngine.Mesh mesh; int nx = UnityEngine.Mathf.Max(6, UnityEngine.Mathf.RoundToInt(w * 6f)), ny = UnityEngine.Mathf.Max(8, UnityEngine.Mathf.RoundToInt(h * 5f));
  if (!meshCache.TryGetValue(key, out mesh)) { mesh = CurtainMesh(w, h, nx, ny); meshCache[key] = mesh; }
  var go = new UnityEngine.GameObject(name); go.transform.SetParent(parent, false); go.transform.position = pos; go.transform.rotation = UnityEngine.Quaternion.Euler(0, ry, 0);
  var smr = go.AddComponent<UnityEngine.SkinnedMeshRenderer>(); smr.sharedMesh = mesh; smr.sharedMaterial = velvet; smr.updateWhenOffscreen = false;
  smr.localBounds = new UnityEngine.Bounds(V3(0, -h / 2, 0), V3(w + 2f, h + 1f, 3f));
  var cloth = go.AddComponent<UnityEngine.Cloth>();
  cloth.stretchingStiffness = 0.95f; cloth.bendingStiffness = 0.15f; cloth.damping = 0.25f; cloth.friction = 0.6f;
  cloth.useGravity = true; cloth.worldVelocityScale = 0.3f; cloth.worldAccelerationScale = 0.6f; cloth.useTethers = true;
  cloth.randomAcceleration = V3(0.35f, 0f, 0.35f); cloth.sleepThreshold = 0.05f; cloth.collisionMassScale = 0.15f;
  var co = cloth.coefficients;
  for (int k = 0; k < co.Length; k++) { float yy = mesh.vertices[k].y; co[k].maxDistance = yy > -0.02f ? 0f : h * 0.9f; co[k].collisionSphereDistance = 0f; }
  cloth.coefficients = co;
  if (playerCloth != null) cloth.capsuleColliders = new UnityEngine.CapsuleCollider[] { playerCloth };
  nCloth++;
  return go;
};
// split red curtain across an opening. centre = top centre of the opening, right = direction along the opening, facing via ry.
System.Action<UnityEngine.Transform, string, UnityEngine.Vector3, UnityEngine.Vector3, float, float, float, float> SplitCurtain = (parent, name, top, right, ry, width, h, gap) => {
  var g = new UnityEngine.GameObject(name); g.transform.SetParent(parent, false); g.transform.position = top;
  float pw = (width - gap) / 2f + 0.15f;
  ClothPanel(g.transform, "Cloth_L", top - right * (gap / 2f + pw / 2f - 0.15f), ry, pw, h);
  ClothPanel(g.transform, "Cloth_R", top + right * (gap / 2f + pw / 2f - 0.15f), ry, pw, h);
  // valance (gold-trimmed static pelmet) hides the rail
  var val = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube); val.name = "Valance"; UnityEngine.Object.DestroyImmediate(val.GetComponent<UnityEngine.Collider>());
  val.transform.SetParent(g.transform, false); val.transform.position = top + V3(0, 0.18f, 0); val.transform.rotation = UnityEngine.Quaternion.Euler(0, ry, 0);
  val.transform.localScale = V3(width + 0.3f, 0.42f, 0.12f); val.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial = velvet;
  var hem = UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube); hem.name = "ValanceTrim"; UnityEngine.Object.DestroyImmediate(hem.GetComponent<UnityEngine.Collider>());
  hem.transform.SetParent(g.transform, false); hem.transform.position = top + V3(0, -0.04f, 0); hem.transform.rotation = UnityEngine.Quaternion.Euler(0, ry, 0);
  hem.transform.localScale = V3(width + 0.32f, 0.06f, 0.14f); hem.GetComponent<UnityEngine.MeshRenderer>().sharedMaterial = hemMat;
};
// particle FX instance with per-instance overrides (shape box size or circle radius, tint)
System.Func<UnityEngine.Transform, string, UnityEngine.Vector3, UnityEngine.Vector3, float, UnityEngine.Color, float, UnityEngine.GameObject> Fx = (parent, prefab, pos, box, radius, tint, rateScale) => {
  var go = S("FX/" + prefab, parent, pos, 0); if (go == null) return null;
  foreach (var ps in go.GetComponentsInChildren<UnityEngine.ParticleSystem>()) {
    var sh = ps.shape; if (radius > 0) sh.radius = radius; if (box != UnityEngine.Vector3.zero) sh.scale = box;
    var main = ps.main; main.startColor = tint;
    var em = ps.emission; em.rateOverTime = new UnityEngine.ParticleSystem.MinMaxCurve(em.rateOverTime.constant * rateScale);
  }
  nFx++; return go;
};
var fogBlue = new UnityEngine.Color(0.42f, 0.52f, 0.78f, 0.32f);
var dustWarm = new UnityEngine.Color(0.85f, 0.72f, 0.55f, 0.35f);
// ===== R01 lobby — big-top ring
var ring = Group("Ring", PR(1));
for (int q = 0; q < 4; q++) { var fr = S(GB + "SM_Bld_Base_Floor_Round_01", ring, V3(0, 0.0f, 0), 90f * q); fr.transform.localScale = V3(2.6f, 1, 2.6f); Paint(fr, "Generic_Wood"); nProp++; }
var tent = P("Buildings/SM_Bld_Military_Tent_02", PR(1), 0, 0, 0, 0.05f); tent.name = "CenterTent_" + tent.name; Paint(tent, RED); nProp++;
// radiating bunting + bulb strings from the peak to the wall tops
var peak = V3(0, 8.3f, 0);
for (int k = 0; k < 12; k++) {
  float ang = k * 30f * UnityEngine.Mathf.Deg2Rad; var dir = V3(UnityEngine.Mathf.Cos(ang), 0, UnityEngine.Mathf.Sin(ang));
  float reach = 9.6f / UnityEngine.Mathf.Max(UnityEngine.Mathf.Abs(dir.x), UnityEngine.Mathf.Abs(dir.z));
  var end = dir * reach + V3(0, 5.6f, 0);
  Bunting(1, "Props/SM_Prop_FlagLine_0" + (1 + k % 3), peak, end, k % 3 == 2 ? 13.71f : 27.4f);
  if (k % 2 == 0) BulbLine(PR(1), peak + V3(0, -0.4f, 0), end + V3(0, -0.4f, 0), 0.9f, 0.5f);
}
// ring rim bulbs
for (int k = 0; k < 28; k++) { float ang = k * (360f / 28f) * UnityEngine.Mathf.Deg2Rad; Bulb(PR(1), V3(6.3f * UnityEngine.Mathf.Cos(ang), 0.12f, 6.3f * UnityEngine.Mathf.Sin(ang)), 0.15f); }
// awnings over the lobby doorways (inside face)
Awning(1, -9.4f, 6.25f, 90, 2.75f, 3.2f); Awning(1, -9.4f, -1.25f, 90, 2.75f, 3.2f); Awning(1, -9.4f, -6.25f, 90, 2.75f, 3.2f);
Awning(1, 3.75f, 9.4f, 0, 2.75f, 3.2f); Awning(1, 3.75f, -9.4f, 0, 2.75f, 3.2f); Awning(1, 9.4f, 6.25f, 90, 2.75f, 3.2f);
Awning(1, 9.3f, -2.5f, 90, 3.1f, 5.6f); // corridor mouth
P("Props/SM_Prop_Plank_Bench_01", PR(1), -8.4f, 2.5f, 0, 0); P("Props/SM_Prop_Plank_Bench_01", PR(1), 8.4f, 2.0f, 0, 0);
P("Props/SM_Prop_Barrel_Coloured_01", PR(1), -9.1f, -9.1f, 0, 0); P("Props/SM_Prop_Barrel_Coloured_01", PR(1), -8.3f, -9.2f, 40, 0);
P("Props/SM_Prop_Barrel_Coloured_01", PR(1), 9.1f, 9.1f, 0, 0); P("Props/SM_Prop_Barrel_Coloured_01", PR(1), -9.1f, 9.1f, 0, 0);
P("Props/SM_Prop_Barrel_Coloured_01", PR(1), 9.1f, -9.1f, 0, 0); P("Props/SM_Prop_Crate_01", PR(1), 8.2f, -9.2f, 15, 0);
nProp += 8;
var spot = L(LT(1), "RingSpotLight", V3(0, 8.6f, 0), Sp, 14f, 18f, new UnityEngine.Color(1f, 0.82f, 0.55f));
spot.GetComponent<UnityEngine.Light>().shadows = UnityEngine.LightShadows.Soft; spot.GetComponent<UnityEngine.Light>().spotAngle = 70;
foreach (var pp in new float[][] { new[]{-6.5f,-6.5f}, new[]{6.5f,-6.5f}, new[]{-6.5f,6.5f}, new[]{6.5f,6.5f} }) L(LT(1), "WarmFill", V3(pp[0], 5.5f, pp[1]), Pt, 5.0f, 15f, warm);

// ===== R02 dormitory — caged sleepers with costumes and masks
P("Props/SM_Prop_Bed_Bunk_01", PR(2), -21.2f, 19.3f, 0, 0); P("Props/SM_Prop_Bed_Bunk_01", PR(2), -12.4f, 19.3f, 0, 0);
P("Props/SM_Prop_Bed_Bunk_01", PR(2), -24.3f, 11.5f, 90, 0); P("Props/SM_Prop_Bed_Bunk_01", PR(2), -24.3f, 15.5f, 90, 0);
P("Props/SM_Prop_Bed_Bunk_01", PR(2), -21.2f, 5.7f, 0, 0); P("Props/SM_Prop_Bed_Bunk_01", PR(2), -16.5f, 5.7f, 0, 0);
P("Props/SM_Prop_Toy_Bear_01", PR(2), -21.6f, 19.3f, 180, 0.62f); P("Props/SM_Prop_Toy_Bear_01", PR(2), -24.3f, 15.2f, 90, 0.62f); P("Props/SM_Prop_Toy_Bear_01", PR(2), -16.2f, 5.7f, 0, 1.55f);
P("Props/SM_Prop_Dresser_01", PR(2), -24.3f, 8.0f, 90, 0); P("Props/SM_Prop_Dresser_01", PR(2), -18.7f, 19.4f, 0, 0);
P("Props/SM_Prop_Mannequin_Head_01", PR(2), -24.3f, 7.75f, 90, 0.72f); P("Props/SM_Prop_Mannequin_Head_01", PR(2), -24.3f, 8.25f, 60, 0.72f); P("Props/SM_Prop_Mannequin_Head_01", PR(2), -18.7f, 19.4f, 180, 0.72f);
P("Props/SM_Prop_Mannequin_Male_01", PR(2), -10.9f, 12.0f, 270, 0); P("Props/SM_Prop_Mannequin_Female_01", PR(2), -10.9f, 13.5f, 270, 0);
P("Props/SM_Prop_Barrel_Coloured_01", PR(2), -11.0f, 18.9f, 0, 0); P("Props/SM_Prop_Crate_01", PR(2), -14.0f, 12.0f, 20, 0);
nProp += 17;
for (int k = 0; k < 5; k++) { var pp2 = S("@SM_Gen_Bld_Pipe_Straight_01", PR(2), V3(-10.6f - 2.5f * k, 5.3f, 17.5f), 0); if (pp2 == null) break; pp2.transform.localScale = V3(1, 1.6f, 1.6f); nProp++; }
Lamp(2, -20f, 16f, 6f, 1.8f, 9f); Lamp(2, -20f, 8.5f, 6f, 1.8f, 9f); Lamp(2, -13.5f, 12.5f, 6f, 1.6f, 9f);

// ===== R03 restroom
P("Props/SM_Prop_Toilet_01", PR(3), 13.5f, 9.4f, 180, 0); P("Props/SM_Prop_Toilet_01", PR(3), 15.5f, 9.4f, 180, 0);
P("Props/SM_Prop_Toilet_Broken_01", PR(3), 16.6f, 4.2f, 0, 0);
P("Props/SM_Prop_Sink_01", PR(3), 13.0f, 3.2f, 0, 0); P("Props/SM_Prop_BathroomSink_02", PR(3), 16.9f, 7.2f, 270, 0);
nProp += 5; Lamp(3, 13.75f, 6.25f, 6f, 1.6f, 8f);

// ===== R04 dressing room — marquee mirrors and costume mannequins
foreach (var x in new float[] { 2.0f, 5.5f, 9.0f }) {
  P("Props/SM_Prop_Dresser_02", PR(4), x, 19.2f, 0, 0); P("Props/SM_Prop_Chair_Metal_01", PR(4), x, 17.8f, 0, 0); nProp += 2;
  BulbFrame(PR(4), V3(x, 1.9f, 19.75f), UnityEngine.Vector3.right, 1.7f, 1.4f, 0.32f);
  L(LT(4), "MirrorGlow", V3(x, 1.8f, 19.1f), Pt, 1.2f, 3.5f, warm);
}
P("Props/SM_Prop_Mannequin_Female_02", PR(4), 11.8f, 12.0f, 270, 0); P("Props/SM_Prop_Mannequin_Male_02", PR(4), 11.8f, 13.4f, 270, 0);
P("Props/SM_Prop_Mannequin_Female_03", PR(4), 11.8f, 14.8f, 270, 0); P("Props/SM_Prop_Mannequin_Base_01", PR(4), 11.8f, 16.4f, 270, 0);
P("Props/SM_Prop_Shelf_01_Combined", PR(4), 0.6f, 12.5f, 90, 0); P("Props/SM_Prop_Mannequin_Head_01", PR(4), 0.6f, 12.3f, 90, 1.9f);
nProp += 6;
Bunting(4, "Props/SM_Prop_FlagLine_03", V3(0.3f, 5.4f, 15f), V3(12.2f, 5.4f, 15f), 13.71f);
Lamp(4, 6.25f, 14f, 6f, 1.6f, 9f);

// ===== R05 shop — striped awning counter
P("Props/SM_Prop_ShopCounter_01", PR(5), -14.0f, -1.25f, 90, 0);
Awning(5, -14.6f, -1.25f, 90, 2.6f, 3.6f);
BulbLine(PR(5), V3(-14.2f, 3.3f, -3.2f), V3(-14.2f, 3.3f, 0.7f), 0.35f, 0.15f);
P("Props/SM_Prop_ShopShelf_Long_01", PR(5), -16.5f, 4.2f, 0, 0); P("Props/SM_Prop_ShopShelf_Long_01", PR(5), -16.5f, -4.2f, 180, 0);
P("Props/SM_Prop_Medical_Shelf_01", PR(5), -21.9f, 1.4f, 90, 0); P("Props/SM_Prop_Medical_Shelf_01", PR(5), -21.9f, -1.4f, 90, 0);
P("Props/SM_Prop_MedicalBox_01", PR(5), -14.0f, -0.6f, 20, 1.0f);
P("Props/SM_Prop_Toy_Bear_01", PR(5), -18.5f, 4.2f, 180, 1.05f); P("Props/SM_Prop_Toy_Bear_01", PR(5), -15.0f, -4.2f, 0, 1.05f);
nProp += 8; Lamp(5, -17.5f, 0f, 6f, 2.0f, 9f);

// ===== R06 prop room
foreach (var x in new float[] { 1.6f, 4.6f, 7.6f, 10.6f }) { P("Props/SM_Prop_WorkShelf_01", PR(6), x, -19.4f, 0, 0); nProp++; }
P("Props/SM_Prop_Shelf_01_Combined", PR(6), 11.9f, -12.8f, 270, 0); P("Props/SM_Prop_Shelf_01_Combined", PR(6), 11.9f, -15.4f, 270, 0);
P("Props/SM_Prop_Crate_Large_01", PR(6), 1.0f, -14.5f, 0, 0); P("Props/SM_Prop_Crate_Open_01", PR(6), 1.2f, -16.8f, 25, 0);
P("Props/SM_Prop_Crate_01", PR(6), 8.4f, -15.5f, 0, 0); P("Props/SM_Prop_Crate_01", PR(6), 9.3f, -15.5f, 10, 0); P("Props/SM_Prop_Crate_01", PR(6), 8.8f, -15.5f, 45, 0.8f);
P("Props/SM_Prop_Pallet_01", PR(6), 5.0f, -15.0f, 0, 0); P("Props/SM_Prop_Barrel_Coloured_01", PR(6), 4.7f, -15.0f, 0, 0.2f);
P("Props/SM_Prop_Barrel_Coloured_01", PR(6), 0.9f, -11.0f, 0, 0); P("Props/SM_Prop_Barrel_Coloured_01", PR(6), 1.8f, -11.0f, 30, 0);
P("Props/SM_Prop_Workbench_01", PR(6), 9.2f, -10.9f, 180, 0);
P("Props/SM_Prop_Toy_Bear_01", PR(6), 4.6f, -19.4f, 0, 1.0f); P("Props/SM_Prop_Mannequin_Head_01", PR(6), 7.6f, -19.4f, 0, 1.0f);
var rope = P("Props/SM_Prop_RopeClimb_01", PR(6), 6.0f, -12.0f, 0, 0);
nProp += 14; Lamp(6, 6.25f, -15f, 6f, 1.8f, 9f);

// ===== R07 lounge
P("Props/SM_Prop_Couch_01", PR(7), -19f, -19.3f, 0, 0); P("Props/SM_Prop_CoffeeTable_02", PR(7), -19f, -17.9f, 0, 0);
P("Props/SM_Prop_Table_01", PR(7), -15f, -12f, 0, 0);
P("Props/SM_Prop_Chair_Folding_01", PR(7), -15f, -10.9f, 180, 0); P("Props/SM_Prop_Chair_Folding_01", PR(7), -16.1f, -12f, 90, 0); P("Props/SM_Prop_Chair_Folding_01", PR(7), -14.0f, -12.8f, 300, 0);
P("Props/SM_Prop_Desk_02", PR(7), -24.4f, -10.5f, 90, 0);
P("Props/SM_Prop_VendingMachine_01", PR(7), -24.2f, -16f, 90, 0);
P("Props/SM_Prop_Toy_Bear_01", PR(7), -19.6f, -19.3f, 0, 0.5f);
nProp += 9;
Bunting(7, "Props/SM_Prop_FlagLine_02", V3(-24.7f, 5.2f, -19.7f), V3(-10.3f, 5.2f, -5.3f), 27.4f);
Lamp(7, -17.5f, -12.5f, 6f, 2.0f, 10f);

// ===== R08 junk storage (hidden) — old troupe leftovers
P("Props/SM_Prop_TrashPile_05", PR(8), -5.5f, 30.2f, 0, 0); P("Props/SM_Prop_TrashPile_01", PR(8), -4.2f, 25.6f, 0, 0);
P("Props/SM_Prop_BurnPile_Books_01", PR(8), -11.5f, 30.5f, 0, 0);
P("Props/SM_Prop_Cupboard_Broken_01", PR(8), -14.2f, 23.6f, 90, 0); P("Props/SM_Prop_Bookshelf_01", PR(8), -12.3f, 22.9f, 0, 0);
P("Props/SM_Prop_Crate_Open_01", PR(8), -9.0f, 23.4f, 15, 0); P("Props/SM_Prop_Barrel_Coloured_01", PR(8), -3.2f, 32.0f, 0, 0);
P("Props/SM_Prop_Mannequin_Torso_01", PR(8), -8.0f, 32.0f, 30, 0); P("Props/SM_Prop_Mannequin_Head_01", PR(8), -9.0f, 23.4f, 200, 0.8f);
P("Props/SM_Prop_Toy_Bear_01", PR(8), -6.5f, 27.0f, 120, 0);
nProp += 10; Lamp(8, -8.75f, 27.5f, 6f, 1.0f, 9f);


// ===== R09 + R10 stage approach tunnel — long backstage tunnel in a derelict factory, rigged up as a circus
// R09 x 10..47.5, R10 x 47.5..57.5 (z -5..0, ceiling 4.5). Backstage slot doors on both walls in x 25..47.5.
// timber ribs every 5 m between slot doors
var ribs = Group("TunnelRibs", PR(9));
foreach (var x in new float[] { 13.75f, 18.75f, 23.75f, 28.75f, 33.75f, 38.75f, 43.75f, 50f, 55f }) {
  int rid = x > 47.5f ? 10 : 9; var rp = rid == 9 ? ribs : Group("TunnelRibs", PR(10));
  var a1 = S("Buildings/SM_Bld_Bunker_Beam_Curved_Wood_01", rp, V3(x, 1.45f, 0f), 180); var a2 = S("Buildings/SM_Bld_Bunker_Beam_Curved_Wood_01", rp, V3(x, 1.45f, -5f), 0);
  var p1 = P("Buildings/SM_Bld_Bunker_Beam_Wood_01", rp, x, -0.25f, 0, 0); var p2 = P("Buildings/SM_Bld_Bunker_Beam_Wood_01", rp, x, -4.75f, 0, 0);
  p1.transform.localScale = V3(0.6f, 0.5f, 0.6f); p2.transform.localScale = V3(0.6f, 0.5f, 0.6f);
  Realign(p1, x, -0.2f, 0); Realign(p2, x, -4.8f, 0);
  nProp += 4;
}
// pipes: run along both walls near the ceiling, big pipes on the floor in the first stretch (no slot doors there)
for (int k = 0; k < 19; k++) {
  float x = 12.5f + 2.5f * k; int rid = x > 47.5f ? 10 : 9;
  var pa = S("@SM_Gen_Bld_Pipe_Straight_01", PR(rid), V3(x, 3.75f, -0.35f), 0); if (pa != null) pa.transform.localScale = V3(1, 1.4f, 1.4f);
  var pb = S("@SM_Gen_Bld_Pipe_Straight_02", PR(rid), V3(x, 4.15f, -4.65f), 0); if (pb != null) pb.transform.localScale = V3(1, 1.1f, 1.1f);
  if (k % 2 == 0) S("Props/SM_Prop_Bunker_RoofWire_01", PR(rid), V3(x, 4.45f, -2.5f + (k % 4 == 0 ? 0.6f : -0.6f)), 0);
  nProp += 2;
}
var bigPipe = P("Props/SM_Prop_Pipe_Large_01_Tarp", PR(9), 15.5f, -4.05f, 90, 0); var bigPipe2 = P("Props/SM_Prop_Pipe_Large_01", PR(9), 21.0f, -4.15f, 90, 0);
P("Props/SM_Prop_Pipe_Valve_01", PR(9), 23.6f, -4.5f, 0, 0); P("Props/SM_Prop_Pipes_Valves_01", PR(9), 17.5f, -0.25f, 180, 0);
P("Props/SM_Prop_Bunker_WallWires_01", PR(9), 12.5f, -0.5f, 180, 1.9f); P("Props/SM_Prop_Bunker_WallWires_01", PR(9), 20f, -0.5f, 180, 1.9f);
P("Props/SM_Prop_Pipes_Rusted_02", PR(10), 52.5f, -0.3f, 180, 2.4f); P("Props/SM_Prop_Pipes_Rusted_02", PR(10), 52.5f, -4.7f, 0, 2.4f);
nProp += 8;
// worn cloth: old tarps draped over pipes and hanging from the ribs, faded bunting
var t1 = P("Buildings/SM_Bld_Tarp_Generic_02", PR(9), 12.6f, -0.95f, 180, 2.0f); var t2 = P("Buildings/SM_Bld_Tarp_Generic_02", PR(9), 36.3f, -0.95f, 180, 2.1f); Paint(t2, RED);
var t3 = P("Buildings/SM_Bld_Tarp_Generic_02", PR(9), 41.2f, -4.05f, 0, 2.1f); var t4 = P("Buildings/SM_Bld_Tarp_Generic_02", PR(10), 49.6f, -0.95f, 180, 2.0f); Paint(t4, YEL);
P("Props/SM_Prop_Wall_Junk_03_Tarp", PR(9), 11.6f, -4.3f, 0, 0);
nProp += 5;
Bunting(9, "Props/SM_Prop_FlagLine_02", V3(10.4f, 4.0f, -0.4f), V3(47.4f, 4.1f, -4.6f), 27.4f);
// settled dust & debris on the floor (no colliders on pebbles/rocks; heaps kept against the walls)
for (int k = 0; k < 16; k++) {
  float x = 11f + 2.9f * k + (float)rnd.NextDouble(); int rid = x > 47.5f ? 10 : 9; float z = -2.5f + ((float)rnd.NextDouble() - 0.5f) * 3.4f;
  var pb2 = P(k % 2 == 0 ? "Environment/SM_Env_Rubble_Pebbles_0" + (1 + k % 3) : "@SM_Generic_Small_Rocks_0" + (1 + k % 4), PR(rid), x, z, (float)rnd.NextDouble() * 360f, 0.0f); nProp++;
}
foreach (var dp in new float[][] { new[]{11.6f,-0.7f}, new[]{24.2f,-0.7f}, new[]{29.3f,-4.3f}, new[]{39.2f,-0.7f}, new[]{48.4f,-4.3f}, new[]{56.0f,-0.7f} }) { var d = P("Environment/SM_Env_DirtPile_0" + (1 + nProp % 2), PR(dp[0] > 47.5f ? 10 : 9), dp[0], dp[1], (float)rnd.NextDouble() * 360f, -0.08f); nProp++; }
P("Props/SM_Prop_TrashPile_03", PR(9), 46.3f, -0.95f, 0, 0); P("Props/SM_Prop_CardboardBox_Wall_01", PR(10), 52.0f, -4.35f, 0, 0); P("Props/SM_Prop_Crate_Wall_01", PR(10), 55.4f, -4.3f, 0, 0);
nProp += 3;
// backstage doors: awnings + bulb posts (one bulb in each post flickers) + faint red glow
for (int k = 0; k < 5; k++) {
  float cx = 26.25f + 5f * k;
  Awning(9, cx, -0.55f, 0, 2.95f, 3.0f); Awning(9, cx, -4.45f, 180, 2.95f, 3.0f);
  BulbLineFlicker(PR(9), V3(cx - 1.3f, 0.6f, -0.3f), V3(cx - 1.3f, 2.8f, -0.3f), 0.32f, 0, 5); BulbLine(PR(9), V3(cx + 1.3f, 0.6f, -0.3f), V3(cx + 1.3f, 2.8f, -0.3f), 0.32f, 0);
  BulbLine(PR(9), V3(cx - 1.3f, 0.6f, -4.7f), V3(cx - 1.3f, 2.8f, -4.7f), 0.32f, 0); BulbLineFlicker(PR(9), V3(cx + 1.3f, 0.6f, -4.7f), V3(cx + 1.3f, 2.8f, -4.7f), 0.32f, 0, 6);
  L(LT(9), "SlotRedGlow", V3(cx, 2.4f, -1.2f), Pt, 1.0f, 3.5f, redLight); L(LT(9), "SlotRedGlow", V3(cx, 2.4f, -3.8f), Pt, 1.0f, 3.5f, redLight);
}
BulbLineFlicker(PR(9), V3(10.3f, 4.0f, -2.5f), V3(47.4f, 4.0f, -2.5f), 1.0f, 0.35f, 7);
BulbLineFlicker(PR(10), V3(47.6f, 4.0f, -2.5f), V3(56.0f, 4.0f, -2.5f), 1.0f, 0.3f, 4);
// lamps: steady near the lobby, increasingly unreliable towards the stage
Lamp(9, 12.5f, -2.5f, 4.5f, 1.4f, 7f); FlickerLamp(9, 21.25f, -2.5f, 4.5f, 1.4f, 7f); Lamp(9, 31.25f, -2.5f, 4.5f, 1.3f, 7f); FlickerLamp(9, 41.25f, -2.5f, 4.5f, 1.3f, 7f);
FlickerLamp(10, 51.25f, -2.5f, 4.5f, 1.2f, 7f);
// cold blue fog along the tunnel floor + floating dust
for (int k = 0; k < 4; k++) Fx(PR(9), "FX_Fog_Flat_01", V3(15f + 10f * k, 0.1f, -2.5f), UnityEngine.Vector3.zero, 3.2f, fogBlue, 0.25f);
Fx(PR(10), "FX_Fog_Flat_01", V3(52.5f, 0.1f, -2.5f), UnityEngine.Vector3.zero, 3.2f, fogBlue, 0.3f);
Fx(PR(9), "FX_Dust_Interior_01", V3(28.75f, 2.2f, -2.5f), V3(37f, 3.5f, 4.5f), 0, dustWarm, 1.2f);
Fx(PR(10), "FX_Dust_Interior_01", V3(52.5f, 2.2f, -2.5f), V3(10f, 3.5f, 4.5f), 0, dustWarm, 0.6f);

// ===== stage entry: closed big door hidden behind a red split curtain; warm stage light leaks through the gap
var stageCurtain = Group("StageCurtain", PR(10));
SplitCurtain(stageCurtain, "StageEntryCurtain", V3(56.55f, 4.2f, -2.5f), UnityEngine.Vector3.forward, 90, 5.0f, 4.2f, 0.12f);
BulbFrame(PR(10), V3(56.45f, 2.2f, -2.5f), UnityEngine.Vector3.forward, 5.2f, 4.4f, 0.4f);
var leak = L(LT(10), "StageLeakLight", V3(57.15f, 2.0f, -2.5f), Pt, 6f, 9f, new UnityEngine.Color(1f, 0.78f, 0.45f));
leak.GetComponent<UnityEngine.Light>().shadows = UnityEngine.LightShadows.Soft;       // curtain blocks it except at the gap / when pushed aside
var beam = L(LT(10), "StageLeakSpot", V3(57.2f, 2.2f, -2.5f), Sp, 8f, 14f, new UnityEngine.Color(1f, 0.82f, 0.5f));
beam.transform.rotation = UnityEngine.Quaternion.Euler(8, 270, 0); beam.GetComponent<UnityEngine.Light>().spotAngle = 70; beam.GetComponent<UnityEngine.Light>().shadows = UnityEngine.LightShadows.Soft;
L(LT(10), "DoorRedGlow", V3(54.8f, 3.6f, -2.5f), Pt, 1.2f, 5f, redLight);

// ===== red split curtains in front of the performance-prep rooms (dressing room, prop room, stage tunnel). Dorm/restroom excluded.
SplitCurtain(Group("Curtains", PR(4)), "DressingRoomCurtain", V3(3.75f, 2.45f, 9.72f), UnityEngine.Vector3.right, 180, 2.4f, 2.45f, 0.25f);
SplitCurtain(Group("Curtains", PR(6)), "PropRoomCurtain", V3(3.75f, 2.45f, -9.72f), UnityEngine.Vector3.right, 0, 2.4f, 2.45f, 0.25f);
SplitCurtain(Group("Curtains", PR(9)), "StageTunnelCurtain", V3(10.35f, 2.95f, -2.5f), UnityEngine.Vector3.forward, 90, 5.0f, 2.95f, 0.5f);
// dressing room changing curtain on a rail (replaces the old static curtain props)
var rail = S("@SM_Gen_Bld_Pipe_Straight_03", Group("Curtains", PR(4)), V3(11.6f, 2.3f, 12.4f), 90); if (rail != null) rail.transform.localScale = V3(2.4f, 0.4f, 0.4f);
ClothPanel(Group("Curtains", PR(4)), "ChangingCurtain", V3(11.0f, 2.25f, 13.9f), 270, 3.0f, 2.2f);
// flickering lamps in the neglected rooms
FlickerLamp(8, -12.5f, 30f, 6f, 0.9f, 7f); FlickerLamp(2, -24f, 18.5f, 6f, 0.8f, 6f);
// fog / dust in the bigger rooms
Fx(PR(1), "FX_Dust_Interior_01", V3(0, 3.5f, 0), V3(18f, 6f, 18f), 0, dustWarm, 1.0f);
Fx(PR(1), "FX_Fog_Flat_01", V3(0, 0.1f, 0), UnityEngine.Vector3.zero, 8.5f, fogBlue, 0.35f);
Fx(PR(8), "FX_Fog_Flat_01", V3(-8.75f, 0.1f, 27.5f), UnityEngine.Vector3.zero, 5f, fogBlue, 0.3f);
Fx(PR(8), "FX_Dust_Interior_01", V3(-8.75f, 2.5f, 27.5f), V3(11f, 4f, 9f), 0, dustWarm, 0.8f);
Fx(PR(6), "FX_Dust_Interior_01", V3(6.25f, 2.5f, -15f), V3(11f, 4f, 9f), 0, dustWarm, 0.6f);
Fx(PR(2), "FX_Dust_Interior_01", V3(-17.5f, 2.5f, 12.5f), V3(14f, 4f, 14f), 0, dustWarm, 0.6f);
// ---- play setup / environment
foreach (var r in scene.GetRootGameObjects()) {
  if (r.name == "PlaySetup") {
    var tf = r.transform.Find("TempFloor"); if (tf != null) UnityEngine.Object.DestroyImmediate(tf.gameObject);
    var pl = r.transform.Find("Player"); if (pl != null) { pl.position = V3(0, 0.05f, -7.8f); pl.rotation = UnityEngine.Quaternion.identity; }
  }
  if (r.name == "Directional Light") r.GetComponent<UnityEngine.Light>().intensity = 0.03f;
}
UnityEngine.RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
UnityEngine.RenderSettings.ambientLight = new UnityEngine.Color(0.21f, 0.20f, 0.29f);   // cool navy fill (warm objects / cool background)
UnityEngine.RenderSettings.fog = true;
UnityEngine.RenderSettings.fogMode = UnityEngine.FogMode.ExponentialSquared;
UnityEngine.RenderSettings.fogColor = new UnityEngine.Color(0.06f, 0.07f, 0.14f);
UnityEngine.RenderSettings.fogDensity = 0.022f;
UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Build WaitingRoom v3");
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
bool saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
log.AppendLine("floor=" + nFloor + " wall=" + nWall + " ceil=" + nCeil + " props=" + nProp + " bulbs=" + nBulb + " cloth=" + nCloth + " flicker=" + nFlicker + " fx=" + nFx + " saved=" + saved);
return log.ToString();
