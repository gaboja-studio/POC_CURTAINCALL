if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play before setting version");
UnityEditor.PlayerSettings.bundleVersion = "0.8.1";
return new { version = UnityEditor.PlayerSettings.bundleVersion };
