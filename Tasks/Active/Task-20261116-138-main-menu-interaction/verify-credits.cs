var menu = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenu>();
menu.Show(CurtainCall.StartLobby.StartMenu.Page.Credits);
var panel = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuCreditsPanel>();
var field = typeof(CurtainCall.StartLobby.StartMenuCreditsPanel).GetField("data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var original = (CurtainCall.StartLobby.StartMenuCreditsSettings)field.GetValue(panel);
var temporary = UnityEngine.Object.Instantiate(original);
temporary.contributors.Add(new CurtainCall.StartLobby.StartMenuCreditsSettings.Contributor { nickname = "GuestTester", role = "QA" });
temporary.licenses.RemoveAt(temporary.licenses.Count - 1);
field.SetValue(panel, temporary);
menu.Show(CurtainCall.StartLobby.StartMenu.Page.Main); menu.Show(CurtainCall.StartLobby.StartMenu.Page.Credits);
bool added = false;
foreach (var t in panel.GetComponentsInChildren<TMPro.TMP_Text>()) if (t.text == "GuestTester") added = true;
if (!added) throw new Exception("Contributor addition was not reflected");
temporary.contributors.RemoveAt(temporary.contributors.Count - 1);
menu.Show(CurtainCall.StartLobby.StartMenu.Page.Main); menu.Show(CurtainCall.StartLobby.StartMenu.Page.Credits);
foreach (var t in panel.GetComponentsInChildren<TMPro.TMP_Text>()) if (t.text == "GuestTester") throw new Exception("Deleted contributor remained visible");
field.SetValue(panel, original);
menu.Show(CurtainCall.StartLobby.StartMenu.Page.Main); menu.Show(CurtainCall.StartLobby.StartMenu.Page.Credits);
UnityEngine.Object.Destroy(temporary);
var scroll = panel.GetComponentInChildren<UnityEngine.UI.ScrollRect>();
if (scroll.content.rect.height <= scroll.viewport.rect.height || scroll.verticalScrollbar == null) throw new Exception("Credits cannot scroll");
foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>())
    if (button.name == "LicenseText") { button.onClick.Invoke(); break; }
bool fullText = false;
foreach (var t in panel.GetComponentsInChildren<TMPro.TMP_Text>())
    if (t.text.Contains("SIL OPEN FONT LICENSE Version 1.1")) fullText = true;
if (!fullText) throw new Exception("Full license text did not open");
ScreenCapture.CaptureScreenshot("D:/github/POC_CURTAINCALL/.harness/local/license-full.png");
return new { addRemoveContributors = true, editableLicenseList = true, scrollable = true, fullLicense = true };
