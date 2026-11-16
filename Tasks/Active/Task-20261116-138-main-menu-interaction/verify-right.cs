var title = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuHangingTitle>();
var rect = (RectTransform)title.transform;
var e = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
{ position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.width * .25f, 0))),
    button = UnityEngine.EventSystems.PointerEventData.InputButton.Left };
for (int i = 0; i < 5; i++) { title.OnPointerDown(e); title.OnPointerUp(e); }
if (title.HasSlipped) throw new Exception("Right slipped before threshold");
title.OnPointerDown(e); title.OnPointerUp(e);
if (!title.HasSlipped || title.LeftSlipped) throw new Exception("Right press did not select right rope");
return new { sixPressesSlipRight = true, resetOnFreshScene = true };
