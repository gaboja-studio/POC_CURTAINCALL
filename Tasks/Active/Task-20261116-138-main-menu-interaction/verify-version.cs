var versions = UnityEngine.Object.FindObjectsByType<CurtainCall.UI.SceneIntegration.BuildVersionUI>(FindObjectsSortMode.None);
if (versions.Length != 1) throw new Exception("Expected exactly one persistent version HUD");
var label = versions[0].GetComponentInChildren<TMPro.TMP_Text>();
if (label.text != "0.8.1_TEST") throw new Exception("Wrong version label: " + label.text);
if (versions[0].gameObject.scene.name != "DontDestroyOnLoad") throw new Exception("Version HUD is scene-bound");
if (label.raycastTarget || versions[0].GetComponent<UnityEngine.UI.GraphicRaycaster>() != null) throw new Exception("Version HUD intercepts input");
var rect = label.rectTransform;
if (rect.anchoredPosition.y + rect.rect.height >= 70) throw new Exception("Version label overlaps microphone");
ScreenCapture.CaptureScreenshot("D:/github/POC_CURTAINCALL/.harness/local/version-hud.png");
return new { text = label.text, scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
    count = versions.Length, persistent = true, belowMicrophone = true, noInputBlocking = true };
