var hud = UnityEngine.Object.FindFirstObjectByType<CurtainCall.UI.SceneIntegration.BuildVersionUI>();
if (hud == null) throw new Exception("Missing HUD before scene transition");
int id = hud.GetInstanceID();
var probe = UnityEngine.SceneManagement.SceneManager.CreateScene("VersionHudTransitionProbe");
UnityEngine.SceneManagement.SceneManager.SetActiveScene(probe);
var instances = UnityEngine.Object.FindObjectsByType<CurtainCall.UI.SceneIntegration.BuildVersionUI>(FindObjectsSortMode.None);
if (instances.Length != 1 || instances[0].GetInstanceID() != id) throw new Exception("HUD duplicated or replaced");
return new { scene = probe.name, sameInstance = true, count = instances.Length };
