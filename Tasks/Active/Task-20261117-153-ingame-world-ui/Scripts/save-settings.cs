UnityEditor.EditorUtility.SetDirty(CurtainCall.Settings.GameSettings.EscapeRoute);
UnityEditor.AssetDatabase.SaveAssetIfDirty(CurtainCall.Settings.GameSettings.EscapeRoute);
return new { path=CurtainCall.Settings.GameSettings.WorldUi.showGrabPath, distance=CurtainCall.Settings.GameSettings.WorldUi.escapeThreatDistance, prepare=CurtainCall.Settings.GameSettings.EscapeRoute.Pursuer.grabPrepareSeconds };
