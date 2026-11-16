var menu = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenu>();
var title = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuHangingTitle>();
var rect = (RectTransform)title.transform;
var e = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
{ position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.width * .25f, 0))) };
var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
UnityEngine.EventSystems.EventSystem.current.RaycastAll(e, hits);
if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<CurtainCall.StartLobby.StartMenuHangingTitle>() == null)
    throw new Exception("Real GraphicRaycaster cannot select title");
menu.Show(CurtainCall.StartLobby.StartMenu.Page.Credits);
// Graphic depth updates on the rendered frame. Run verify-modal-raycast.cs next.
return new { titleRaycast = true, modalOpened = true };
