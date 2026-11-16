CurtainCall.StartLobby.EditorTools.StartMenuPresentationAssets.EnsureAssets();
return new { settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CurtainCall.StartLobby.EditorTools.StartMenuPresentationAssets.SettingsPath) != null,
    ropes = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/StartMenu/TitleSuspensionRopes.prefab") != null };

