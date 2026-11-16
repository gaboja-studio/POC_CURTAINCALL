var title = UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenuHangingTitle>();
var rect = (RectTransform)title.transform;
var e = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
{ position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.width * .25f, 0))) };
var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
UnityEngine.EventSystems.EventSystem.current.RaycastAll(e, hits);
string names = string.Join(", ", hits.ConvertAll(h => h.gameObject.name));
if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<CurtainCall.StartLobby.StartMenuCreditsPanel>() == null)
    throw new Exception("Modal does not intercept pointer: " + names);
UnityEngine.Object.FindFirstObjectByType<CurtainCall.StartLobby.StartMenu>().Show(CurtainCall.StartLobby.StartMenu.Page.Main);
return new { modalRaycast = true, topHit = names };
