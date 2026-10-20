#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CurtainCall.UI.SpectatorV1;
using CurtainCall.UI.GuidesV1;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Button = UnityEngine.UI.Button;
using Builder = CurtainCall.UI.SpectatorV1.Editor.SpectatorBuilder;

namespace CurtainCall.Spectator.Editor
{
    public static class SpectatorProjectBuilder
    {
        public const string Kit="Assets/_ThirdParty/curtaincall_spectator_v1";
        public const string PrefabFolder="Assets/Resources/Prefabs/UIs/Spectator";
        public const string PrefabPath=PrefabFolder+"/Spectator.prefab";
        public const string FontFolder="Assets/Resources/Fonts/Spectator";
        public const string FontPath=FontFolder+"/Pretendard-Medium SDF.asset";
        public const string SceneFolder="Assets/Scenes/Tests/Spectator";
        public const string ScenePath=SceneFolder+"/Spectator.unity";
        const string ButtonPath="Assets/Resources/Prefabs/UIs/CommonButtons/Button_General.prefab";
        static readonly string[] Platforms={"Standalone","Android","iPhone","WebGL","Web","Windows Store Apps","tvOS"};
        public static readonly string[] Labels={"사망","관전","대상 없음","숨김","즉시 숨김","긴 대상 이름","긴 키","한쪽 빈 키","가장자리 끔","왼쪽만","오른쪽만","양쪽 복원","반복 호출","전환 반전","시간 정지","배경 전환","C7 슬롯 예시","초기화"};
        [Serializable] sealed class Plan { public Source[] assets; }
        [Serializable] sealed class Source { public string key,relative_path,sha256; public int[] source_px; public float[] border_lr_tb_px; public bool @new; }
        static bool Exists(string path)=>File.Exists(path)||Directory.Exists(path)||File.Exists(path+".meta")||AssetDatabase.LoadMainAssetAtPath(path)!=null;
        static string Canonical(Source s)=>s.@new?Kit+"/"+s.relative_path:"Assets/_ThirdParty/"+(s.key.StartsWith("plate_",StringComparison.Ordinal)||s.key.StartsWith("keycap_",StringComparison.Ordinal)?"curtaincall_performance_hud_v1":"curtaincall_icons_v1")+"/"+s.relative_path;
        static Vector2 Size(Source s)=>new Vector2(s.source_px[0],s.source_px[1]);
        static Vector4 Border(Source s)=>new Vector4(s.border_lr_tb_px[0],s.border_lr_tb_px[3],s.border_lr_tb_px[1],s.border_lr_tb_px[2]);

        static bool finalizationQueued;
        [MenuItem("Tools/Curtain Call/C11 Spectator/Build prefab and preview")]
        public static void Build()
        {
            Builder.ValidateEnvironment();
            if(finalizationQueued)throw new InvalidOperationException("C11 finalization is already queued.");
            foreach(string path in new[]{PrefabFolder,FontFolder,SceneFolder})
                if(Exists(path))throw new InvalidOperationException("Existing output is protected: "+path);
            ValidateButton();
            var fontSource=AssetDatabase.LoadAssetAtPath<Font>(Kit+"/Source/Fonts/Pretendard-Medium.ttf");
            if(fontSource==null)throw new InvalidOperationException("Medium source font missing.");
            var sources=ValidateSources(false);
            foreach(var s in sources.Where(x=>x.@new)){ConfigureNewImporter(s);ValidateSprite(s);}
            MakeFont(fontSource);
            EnsureFolder(PrefabFolder);EnsureFolder(SceneFolder);
            QueueFinalization();
        }

        /// <summary>Explicit recovery only: reuses the validated dedicated font, never overwrites partial outputs.</summary>
        [MenuItem("Tools/Curtain Call/C11 Spectator/Resume after dedicated font")]
        public static void ResumeAfterFont()
        {
            Builder.ValidateEnvironment();
            if(finalizationQueued)throw new InvalidOperationException("C11 finalization is already queued.");
            ValidateEmptyOutputs();ValidateDedicatedFont();ValidateButton();ValidateSources(true);
            // Missing directories may be created, but existing directories must be entirely empty.
            EnsureFolder(PrefabFolder);EnsureFolder(SceneFolder);
            QueueFinalization();
        }
        static void QueueFinalization()
        {
            finalizationQueued=true;
            EditorApplication.update+=FinalizeWhenIdle;
            Debug.Log("C11 finalization queued for an idle editor tick. If domain reload cancels it, explicitly use ResumeAfterFont after compilation.");
        }
        static void FinalizeWhenIdle()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            EditorApplication.update-=FinalizeWhenIdle;
            finalizationQueued=false;
            Scene original=default,created=default;bool saved=false;
            try
            {
                // Nothing captured across the importer/font operation is trusted; every preflight is repeated.
                Builder.ValidateEnvironment();ValidateEmptyOutputs();
                var font=ValidateDedicatedFont();var button=ValidateButton();var sources=ValidateSources(true);
                var sprites=sources.ToDictionary(s=>s.key,ValidateSprite);
                original=SceneManager.GetActiveScene();
                created=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(created);
                var canvas=Builder.BuildProject(created,font,sprites);
                var view=canvas.GetComponentInChildren<SpectatorView>(true);
                if(PrefabUtility.SaveAsPrefabAssetAndConnect(canvas,PrefabPath,InteractionMode.AutomatedAction)==null)throw new IOException("C11 prefab save failed.");
                CreatePreview(created,view,font,button);view.HideImmediate();
                if(!EditorSceneManager.SaveScene(created,ScenePath))throw new IOException("Preview scene save failed.");saved=true;
                Debug.Log("C11 finalization complete: "+PrefabPath+" / "+ScenePath);
            }
            catch(Exception ex)
            { Debug.LogError("C11 finalization stopped. Existing font/partial outputs preserved; no deletion or automatic retry: "+ex); }
            finally
            {
                if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
                // Only our own scene created in this invocation can be closed. Never search for old/user scenes.
                if(created.IsValid()&&created.isLoaded&&(saved||(!created.isDirty&&created.rootCount==0&&string.IsNullOrEmpty(created.path))))
                    EditorSceneManager.CloseScene(created,true);
            }
        }
        static void ValidateEmptyOutputs()
        {
            foreach(string path in new[]{PrefabPath,ScenePath})if(Exists(path))throw new InvalidOperationException("Existing output is protected: "+path);
            foreach(string folder in new[]{PrefabFolder,SceneFolder})
            {
                if(Directory.Exists(folder))
                { if(Directory.EnumerateFileSystemEntries(folder).Any())throw new InvalidOperationException("Recovery requires an entirely empty output folder: "+folder); }
                else if(Exists(folder))throw new InvalidOperationException("Conflicting output path/meta: "+folder);
            }
        }
        static GameObject ValidateButton()
        {
            var button=AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPath);
            if(button==null||button.GetComponent<Button>()==null||button.GetComponent<RectTransform>()==null||button.GetComponentInChildren<TMP_Text>(true)==null)
                throw new InvalidOperationException("Approved CommonButtons prefab missing/invalid.");
            return button;
        }
        static TMP_FontAsset ValidateDedicatedFont()
        {
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            string sourcePath=Kit+"/Source/Fonts/Pretendard-Medium.ttf";
            var source=AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            // TMP clears m_SourceFontFile on Static conversion; its serialized GUID retains provenance.
            string sourceGuid=AssetDatabase.AssetPathToGUID(sourcePath);
            var serializedFont=font==null?null:new SerializedObject(font);
            var recordedGuid=serializedFont?.FindProperty("m_SourceFontFileGUID");
            if(font==null||source==null||string.IsNullOrEmpty(sourceGuid)||recordedGuid==null||recordedGuid.stringValue!=sourceGuid||AssetDatabase.GetAssetPath(font)!=FontPath||
                font.atlasPopulationMode!=AtlasPopulationMode.Static||font.isMultiAtlasTexturesEnabled||
                (font.fallbackFontAssetTable!=null&&font.fallbackFontAssetTable.Count!=0)||font.atlasWidth!=8192||font.atlasHeight!=8192||
                font.atlasTextures==null||font.atlasTextures.Length!=1||font.atlasTextures[0]==null||
                font.atlasTextures[0].width!=8192||font.atlasTextures[0].height!=8192||AssetDatabase.GetAssetPath(font.atlasTextures[0])!=FontPath||
                font.material==null||AssetDatabase.GetAssetPath(font.material)!=FontPath||!font.HasCharacters(Characters()))
                throw new InvalidOperationException("Recovery requires the intact dedicated Static C11 Medium font, full Korean/ASCII glyphs and embedded 8192 atlas/material.");
            if(!Directory.Exists(FontFolder)||Directory.EnumerateFileSystemEntries(FontFolder).Any(p=>
                !string.Equals(Path.GetFullPath(p),Path.GetFullPath(FontPath),StringComparison.OrdinalIgnoreCase)&&
                !string.Equals(Path.GetFullPath(p),Path.GetFullPath(FontPath+".meta"),StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Unexpected files in dedicated font output folder; recovery refused.");
            return font;
        }
        static Source[] ValidateSources(bool includeNewSprites)
        {
            var sources=ReadSources();
            foreach(var s in sources)
            {
                string path=Canonical(s);var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null)throw new InvalidOperationException("Missing importer: "+path);
                importer.GetSourceTextureWidthAndHeight(out int w,out int h);
                if(new Vector2(w,h)!=Size(s)||!importer.DoesSourceTextureHaveAlpha())throw new InvalidOperationException("Source size/alpha mismatch: "+path);
                foreach(string p in Platforms)if(importer.GetPlatformTextureSettings(p).overridden)throw new InvalidOperationException("Platform override needs review: "+path);
                if(!s.@new)
                {
                    using var sha=SHA256.Create();string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
                    if(hash!=s.sha256)throw new InvalidOperationException("Canonical SHA mismatch: "+path);
                }
                if(!s.@new||includeNewSprites)ValidateSprite(s);
            }
            return sources;
        }
        static Source[] ReadSources()
        {
            var sources=JsonUtility.FromJson<Plan>(File.ReadAllText(Kit+"/Docs/spectator_assembly.json"))?.assets;
            var expected=new Dictionary<string,string>();
            foreach(string side in new[]{"left","right"})expected.Add("vignette_"+side,"Assets/CurtainCallUI/Spectator/Sprites/ui_fx_spectator_vignette_"+side+"_normal@2x.png");
            foreach(string part in new[]{"shadow","body","frame","corners"})expected.Add("plate_"+part,"Assets/CurtainCallUI/PerformanceHUD/Sprites/ui_hud_smallplate_"+part+"_normal@2x.png");
            foreach(string part in new[]{"body","frame"})expected.Add("keycap_"+part,"Assets/CurtainCallUI/PerformanceHUD/Sprites/ui_keycap_basic_"+part+"_normal@2x.png");
            foreach(string icon in new[]{"eye","skull"})expected.Add(icon,"Assets/CurtainCallUI/CommonIcons/Sprites/ui_icon_"+(icon=="eye"?"eye_open":icon)+"_normal@2x.png");
            if(sources==null||sources.Length!=10||sources.Any(s=>s==null||s.key==null)||!sources.Select(s=>s.key).OrderBy(x=>x).SequenceEqual(expected.Keys.OrderBy(x=>x)))throw new InvalidOperationException("C11 ten-source manifest mismatch.");
            foreach(var s in sources)
            {
                bool fresh=s.key.StartsWith("vignette_",StringComparison.Ordinal);int w=fresh?3840:s.key.StartsWith("plate_",StringComparison.Ordinal)?256:s.key.StartsWith("keycap_",StringComparison.Ordinal)?80:64;
                int h=fresh?2160:w==256?112:w;int border=w==256?24:w==80?16:0;
                if(s.@new!=fresh||s.relative_path!=expected[s.key]||s.source_px==null||s.source_px.Length!=2||Size(s)!=new Vector2(w,h)||s.border_lr_tb_px==null||s.border_lr_tb_px.Length!=4||s.border_lr_tb_px.Any(b=>b!=border)||(!fresh&&(s.sha256==null||s.sha256.Length!=64)))throw new InvalidOperationException("C11 source contract mismatch: "+s.key);
            }
            return sources;
        }
        static void ConfigureNewImporter(Source s)
        {
            if(!s.@new)throw new InvalidOperationException("Canonical imports are read-only.");
            var t=(TextureImporter)AssetImporter.GetAtPath(Canonical(s));t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=200;t.spriteBorder=Vector4.zero;
            t.alphaSource=TextureImporterAlphaSource.FromInput;t.alphaIsTransparency=t.sRGBTexture=true;t.mipmapEnabled=t.isReadable=false;
            t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;t.npotScale=TextureImporterNPOTScale.None;t.textureCompression=TextureImporterCompression.Uncompressed;t.crunchedCompression=false;t.maxTextureSize=4096;
            var settings=new TextureImporterSettings();t.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=Vector2.one*.5f;t.SetTextureSettings(settings);t.SaveAndReimport();
        }
        static Sprite ValidateSprite(Source source)
        {
            string path=Canonical(source);var s=AssetDatabase.LoadAssetAtPath<Sprite>(path);var t=AssetImporter.GetAtPath(path) as TextureImporter;
            var settings=new TextureImporterSettings();if(t!=null)t.ReadTextureSettings(settings);
            if(t==null||s==null||s.rect.size!=Size(source)||new Vector2(s.texture.width,s.texture.height)!=Size(source)||s.border!=Border(source)||s.pivot!=Size(source)/2||s.pixelsPerUnit!=200||
                (s.texture.format!=TextureFormat.RGBA32&&s.texture.format!=TextureFormat.ARGB32&&s.texture.format!=TextureFormat.BGRA32)||t.textureType!=TextureImporterType.Sprite||t.spriteImportMode!=SpriteImportMode.Single||t.mipmapEnabled||t.isReadable||!t.sRGBTexture||!t.alphaIsTransparency||t.alphaSource!=TextureImporterAlphaSource.FromInput||t.wrapMode!=TextureWrapMode.Clamp||t.filterMode!=FilterMode.Bilinear||t.npotScale!=TextureImporterNPOTScale.None||settings.spriteMeshType!=SpriteMeshType.FullRect||t.textureCompression!=TextureImporterCompression.Uncompressed||t.crunchedCompression)
                throw new InvalidOperationException("C11 sprite contract mismatch: "+path);
            return s;
        }
        // Full modern Korean syllables and ASCII; reused for exact read-only recovery validation.
        static string Characters()=>new string(Enumerable.Range(32,95).Concat(Enumerable.Range(0xAC00,0xD7A3-0xAC00+1)).Concat(Enumerable.Range(0x3131,0x3163-0x3131+1)).Select(c=>(char)c).ToArray())+"…·—•←→×";
        static TMP_FontAsset MakeFont(Font source)
        {
            EnsureFolder(FontFolder);
            string characters=Characters();
            var font=TMP_FontAsset.CreateFontAsset(source,48,5,GlyphRenderMode.SDFAA,8192,8192,AtlasPopulationMode.Dynamic,false);
            if(font==null)throw new InvalidOperationException("Dedicated C11 font creation failed.");
            font.name="Spectator Pretendard-Medium SDF";AssetDatabase.CreateAsset(font,FontPath);
            font.material.name=font.name+" Material";AssetDatabase.AddObjectToAsset(font.material,font);
            foreach(var atlas in font.atlasTextures.Where(x=>x!=null)){atlas.name=font.name+" Atlas";AssetDatabase.AddObjectToAsset(atlas,font);}
            bool complete=font.TryAddCharacters(characters,out string missing);
            font.atlasPopulationMode=AtlasPopulationMode.Static;font.isMultiAtlasTexturesEnabled=false;font.fallbackFontAssetTable=new List<TMP_FontAsset>();
            EditorUtility.SetDirty(font);foreach(var atlas in font.atlasTextures.Where(x=>x!=null))EditorUtility.SetDirty(atlas);AssetDatabase.SaveAssetIfDirty(font);
            if(!complete||!font.HasCharacters(characters))throw new InvalidOperationException("Dedicated C11 glyphs missing: "+missing);
            return font;
        }
        static void CreatePreview(Scene scene,SpectatorView view,TMP_FontAsset font,GameObject buttonAsset)
        {
            var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(14,24,28,255);camera.transform.position=new Vector3(0,0,-10);
            new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            var controls=new GameObject("PreviewControls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            controls.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;controls.GetComponent<Canvas>().sortingOrder=10;
            var scaler=controls.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var status=Text("Status",controls.transform,font,new Vector2(32,-24),new Vector2(1856,90));
            status.text="C11 합성 표시 / 게임 연결 없음 · Play 버튼으로 확인 · Game View 1920×1080 / 1920×1200";
            var lower=Text("IndependentCaptionSlot",controls.transform,font,new Vector2(32,-1012),new Vector2(1856,40));lower.text="C9 독립 최하단 슬롯 위치 예시 · 실제 자막 시스템 연결 없음";
            GuideView guide=null;var guideAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/UIs/Guides/Guides.prefab");
            if(guideAsset!=null)
            {
                var guideCanvas=new GameObject("ExplicitC7PreviewCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
                guideCanvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var gs=guideCanvas.GetComponent<CanvasScaler>();gs.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;gs.referenceResolution=new Vector2(1920,1080);gs.matchWidthOrHeight=.5f;
                var go=(GameObject)PrefabUtility.InstantiatePrefab(guideAsset,guideCanvas.transform);guide=go.GetComponentInChildren<GuideView>(true);
            }
            var preview=controls.AddComponent<SpectatorPreview>();preview.Configure(view,status,camera,guide,lower);
            UnityEngine.Events.UnityAction[] actions={preview.Dead,preview.Spectating,preview.NoTarget,preview.Hide,preview.HideImmediate,preview.LongTarget,preview.LongKey,preview.OneEmptyKey,preview.EdgeOff,preview.LeftOnly,preview.RightOnly,preview.BothEdges,preview.Repeat,preview.Reverse,preview.TogglePause,preview.ToggleBackground,preview.ToggleGuide,preview.ResetPreview};
            var buttons=new Button[Labels.Length];
            for(int i=0;i<Labels.Length;i++)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(buttonAsset,controls.transform);go.name="Sample_"+i;
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=new Vector2(i%6/6f,1);rect.anchorMax=new Vector2((i%6+1)/6f,1);rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(8,-132-i/6*76-60);rect.offsetMax=new Vector2(-8,-132-i/6*76);
                var label=go.GetComponentInChildren<TMP_Text>(true);label.font=font;label.fontSize=22;label.text=Labels[i];label.enableAutoSizing=label.richText=label.parseCtrlCharacters=false;
                buttons[i]=go.GetComponent<Button>();UnityEventTools.AddPersistentListener(buttons[i].onClick,actions[i]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);PrefabUtility.RecordPrefabInstancePropertyModifications(label);PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
            }
            for(int i=0;i<buttons.Length;i++)
            {
                int row=i/6*6,col=i%6;buttons[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,selectOnLeft=buttons[row+(col+5)%6],selectOnRight=buttons[row+(col+1)%6],selectOnUp=buttons[i>=6?i-6:i],selectOnDown=buttons[i+6<buttons.Length?i+6:i]};PrefabUtility.RecordPrefabInstancePropertyModifications(buttons[i]);
            }
        }
        static TMP_Text Text(string name,Transform parent,TMP_FontAsset font,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var r=go.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=position;r.sizeDelta=size;
            var text=go.GetComponent<TMP_Text>();text.font=font;text.fontSize=22;text.color=new Color32(240,238,220,255);text.raycastTarget=text.richText=text.parseCtrlCharacters=false;text.textWrappingMode=TextWrappingModes.NoWrap;return text;
        }
        static void EnsureFolder(string path)
        {
            var parts=path.Split('/');string current=parts[0];for(int i=1;i<parts.Length;i++){string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next)&&string.IsNullOrEmpty(AssetDatabase.CreateFolder(current,parts[i])))throw new IOException("Folder creation failed: "+next);current=next;}
        }
    }
}
#endif
