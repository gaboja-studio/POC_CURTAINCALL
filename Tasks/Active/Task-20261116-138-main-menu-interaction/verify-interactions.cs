var menu = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenu>();
var title = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuHangingTitle>();
var rect = (RectTransform)title.transform;
var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(-rect.rect.width * .25f, 0)));
var events = UnityEngine.EventSystems.EventSystem.current;
var e = new UnityEngine.EventSystems.PointerEventData(events) { position = point, button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
menu.Show(CurtainCall.StartLobby.StartMenu.Page.Credits);
for (int i = 0; i < 8; i++) { title.OnPointerDown(e); title.OnPointerUp(e); }
if (title.HasSlipped) throw new Exception("Title reacted through modal");
menu.Show(CurtainCall.StartLobby.StartMenu.Page.Main);
for (int i = 0; i < 5; i++) { title.OnPointerDown(e); title.OnPointerUp(e); }
if (title.HasSlipped) throw new Exception("Title slipped before six presses");
title.OnPointerDown(e); title.OnPointerUp(e);
if (!title.LeftSlipped) throw new Exception("Six left presses did not slip left rope");
e.position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.width * .25f, 0)));
for (int i = 0; i < 30; i++) { title.OnPointerDown(e); title.OnPointerUp(e); }
if (!title.LeftSlipped) throw new Exception("Already slipped title changed side");
var settings = Resources.Load<CurtainCall.StartLobby.StartMenuCreditsSettings>(CurtainCall.StartLobby.StartMenuCreditsSettings.ResourcePath);
if (settings.contributors.Count != 4 || settings.licenses.Count != 5) throw new Exception("Initial credits data mismatch");
return new { modalBlocked = true, fivePressesStable = true, sixPressesSlipLeft = true, thirtyMoreCannotSlipAgain = true, contributors = 4, licenses = 5 };
