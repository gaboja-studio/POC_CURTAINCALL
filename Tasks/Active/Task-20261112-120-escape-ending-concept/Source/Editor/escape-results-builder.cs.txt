#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace CurtainCall.UI.EscapeResultsKit.Editor
{
    public static class EscapeResultsBuilder
    {
        const string Art="Assets/_ThirdParty/EscapeResultsKit";
        const string Output="Assets/Resources/Prefabs/UIs/EscapeResultsKit";
        const string ScenePath="Assets/Scenes/Tests/EscapeResultsKit/EscapeResultsPreview.unity";
        static readonly Color Primary=new Color32(240,238,220,255);
        static readonly Color Secondary=new Color32(182,193,191,255);
        static readonly Color Brass=new Color32(187,163,107,255);
        static readonly Dictionary<string,Sprite> Sprites=new Dictionary<string,Sprite>();
        static TMP_FontAsset medium,semibold,bold;
        [Serializable] sealed class Manifest { public Source[] assets; }
        [Serializable] sealed class Source { public string key,file,source,sha256; public int width,height,ppu; public int[] border; public bool reused; }
        public const string RequiredText="탈출 성공 명 미탈출 플레이어 런 통계 단원 나 여부 마지막 기물 로비로 돌아가기 버튼을 누르면 이동합니다 방장의 결과 확인을 기다리는 중 결과를 확인하는 동안 달리기는 계속됩니다 공연별 기록 난이도 클리어율 평균 호응 외줄타기 인간 대포 쉬움 보통 어려움 한 최고 팀 개인 총 획득 소모 누적 토큰 예시 데이터 실시간 배경 연결 영역 실제 Cinemachine — · % , . 0123456789";

        [MenuItem("Tools/Curtain Call UI/Escape Results/Build kit prefabs")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before assembly.");
            if(!Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                throw new InvalidOperationException("Save the current untitled scene before assembling additively.");
            if(File.Exists(Output+"/EscapeResultsCanvas.prefab") || File.Exists(ScenePath))
                throw new IOException("Output already exists. Existing prefabs/scenes are never overwritten; move your previous output explicitly before rebuilding.");
            if(Shader.Find("TextMeshPro/Distance Field")==null)
                throw new InvalidOperationException("TMP Essentials are required. Import Essentials before building.");
            ImportSprites();
            medium=MakeFont("Medium");semibold=MakeFont("SemiBold");bold=MakeFont("Bold");
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));AssetDatabase.Refresh();
            var original=SceneManager.GetActiveScene();
            var mode=Application.isBatchMode && string.IsNullOrEmpty(original.path) ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);
            SceneManager.SetActiveScene(scene);
            try
            {
                var canvas=CreateCanvas();var view=BuildView(canvas.transform);
                Sample(view); view.SelectTab(false); view.SetReturnAllowed(true); view.SetVisible(false);
                PrefabUtility.SaveAsPrefabAsset(view.PlayerRows[0].Root,Output+"/EscapePlayerRow.prefab");
                PrefabUtility.SaveAsPrefabAsset(view.StatisticRows[0].Root,Output+"/EscapeStatisticRow.prefab");
                var prefab=PrefabUtility.SaveAsPrefabAssetAndConnect(canvas.gameObject,Output+"/EscapeResultsCanvas.prefab",InteractionMode.AutomatedAction);
                if(prefab==null)throw new IOException("Canvas prefab save failed.");
                view.SetVisible(true);
                canvas.gameObject.AddComponent<EscapeResultsPreview>().View=view;
                var cameraObject=new GameObject("PreviewCamera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
                camera.transform.position=new Vector3(0,0,-10);camera.orthographic=true;camera.orthographicSize=5.4f;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(13,21,29,255);
                // Standalone preview uses its own EventSystem. The Canvas prefab deliberately has none.
                new GameObject("EventSystem",typeof(EventSystem));
                if(!EditorSceneManager.SaveScene(scene,ScenePath))throw new IOException("Preview scene save failed.");
                Validate(view);
                Capture(canvas,camera,view,false,"players"); Capture(canvas,camera,view,true,"statistics");
                view.SelectTab(false);
                AssetDatabase.SaveAssets();
                Debug.Log("ESCAPE_KIT_BUILD_OK: prefab, row templates, fonts and preview saved. No game transition is connected.");
            }
            finally
            {
                if(original.IsValid()&&original.isLoaded)SceneManager.SetActiveScene(original);
                if(SceneManager.sceneCount>1)EditorSceneManager.CloseScene(scene,true);
            }
        }

        static void ImportSprites()
        {
            Sprites.Clear();var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText(Art+"/manifest.json"));
            foreach(var source in manifest.assets)
            {
                string file=Art+"/Sprites/"+source.file;
                var importer=(TextureImporter)AssetImporter.GetAtPath(file);
                if(importer==null)throw new IOException(file);
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=source.key=="portraits"?SpriteImportMode.Multiple:SpriteImportMode.Single;
                importer.spritePixelsPerUnit=200;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;
                importer.filterMode=FilterMode.Bilinear;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.maxTextureSize=4096;importer.spriteBorder=new Vector4(source.border[0],source.border[1],source.border[2],source.border[3]);
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteAlignment=(int)SpriteAlignment.Center;importer.SetTextureSettings(settings);
                if(source.key=="portraits")
                {
                    int w=source.width/2,h=source.height/2;
                    string[] names={"rabbit","pierrot","girl","bear"};
                    var slices=new SpriteMetaData[4];
                    for(int i=0;i<4;i++) slices[i]=new SpriteMetaData{name=names[i],rect=new Rect((i%2)*w,(1-i/2)*h,w,h),alignment=(int)SpriteAlignment.Center,pivot=Vector2.one*.5f};
#pragma warning disable 0618
                    importer.spritesheet=slices;
#pragma warning restore 0618
                }
                importer.SaveAndReimport();
                if(source.key=="portraits")
                    foreach(var sprite in AssetDatabase.LoadAllAssetsAtPath(file).OfType<Sprite>())Sprites[sprite.name]=sprite;
                else Sprites[source.key]=AssetDatabase.LoadAssetAtPath<Sprite>(file);
            }
            foreach(string key in new[]{"rabbit","pierrot","girl","bear"})
                if(!Sprites.ContainsKey(key))throw new InvalidOperationException("Atlas slice missing: "+key);
        }

        static TMP_FontAsset MakeFont(string weight)
        {
            string asset=Art+"/Fonts/Pretendard-"+weight+" SDF.asset";
            var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(asset);
            if(existing!=null)return existing;
            var font=AssetDatabase.LoadAssetAtPath<Font>(Art+"/Fonts/Pretendard-"+weight+".ttf");
            var result=TMP_FontAsset.CreateFontAsset(font,48,6,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
            result.name="EscapeResults Pretendard "+weight;
            if(!result.TryAddCharacters(RequiredText,out string missing))throw new InvalidOperationException("Missing font glyphs: "+missing);
            AssetDatabase.CreateAsset(result,asset);
            foreach(var texture in result.atlasTextures){texture.name=result.name+" Atlas";AssetDatabase.AddObjectToAsset(texture,result);}
            result.material.name=result.name+" Material";AssetDatabase.AddObjectToAsset(result.material,result);
            EditorUtility.SetDirty(result);AssetDatabase.SaveAssets();return result;
        }

        static Canvas CreateCanvas()
        {
            var go=new GameObject("EscapeResultsCanvas",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=60;canvas.referencePixelsPerUnit=100;
            var scale=go.GetComponent<UnityEngine.UI.CanvasScaler>();scale.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution=new Vector2(1920,1080);scale.matchWidthOrHeight=.5f;scale.referencePixelsPerUnit=100;
            return canvas;
        }

        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));var rect=go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);return rect;
        }
        static UnityEngine.UI.Image ArtImage(Transform parent,string name,string key,float x,float y,float w,float h,Color? color=null,bool raycast=false)
        {
            var rect=Rect(parent,name,x,y,w,h);var image=rect.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=Sprites[key];
            image.type=image.sprite.border.sqrMagnitude>0?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;
            image.color=color??Color.white;image.raycastTarget=raycast;return image;
        }
        static TMP_Text Label(Transform parent,string name,string text,float x,float y,float w,float h,int size=26,TMP_FontAsset font=null,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
        {
            var rect=Rect(parent,name,x,y,w,h);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font=font??medium;label.text=text;label.fontSize=size;label.color=Primary;label.alignment=alignment;
            label.enableAutoSizing=false;label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
            label.richText=false;label.parseCtrlCharacters=false;label.raycastTarget=false;return label;
        }
        static void Divider(Transform parent,float y,float width=728)=>ArtImage(parent,"Divider_"+y,"tab_selected",0,y-3,width,8,new Color(0.71f,0.76f,0.75f,.48f));
        static UnityEngine.UI.Button Button(Transform parent,string name,string text,float x,float y,float w,float h,bool tab,out TMP_Text label)
        {
            var root=Rect(parent,name,x,y,w,h);string prefix=tab?"tab_":"confirm_";
            if(!tab)ArtImage(root,"00_Shadow","button_shadow",0,2,w,h,new Color(0,0,0,.32f));
            var body=ArtImage(root,"10_Body",prefix+"normal",0,0,w,h,raycast:true);
            var button=root.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=body;button.transition=UnityEngine.UI.Selectable.Transition.SpriteSwap;
            button.spriteState=new UnityEngine.UI.SpriteState{highlightedSprite=Sprites[prefix+"hover"],pressedSprite=Sprites[prefix+"pressed"],selectedSprite=Sprites[prefix+"normal"],disabledSprite=Sprites[prefix+"disabled"]};
            label=Label(root,"20_LabelTMP",text,12,4,w-24,h-8,tab?28:30,semibold,TextAlignmentOptions.Midline);
            var focus=ArtImage(root,"30_Focus",tab?"tab_focus":"button_focus",0,0,w,h);focus.enabled=false;
            root.gameObject.AddComponent<EscapeResultsFocus>().Graphic=focus;return button;
        }

        static EscapeResultsView BuildView(Transform canvas)
        {
            var root=Rect(canvas,"EscapeResultsPanel",40,60,800,960);
            var view=root.gameObject.AddComponent<EscapeResultsView>();view.Group=root.gameObject.AddComponent<CanvasGroup>();
            ArtImage(root,"00_Shadow","panel_shadow",0,4,800,960,new Color(0,0,0,.32f));
            ArtImage(root,"10_Body","panel_body",0,0,800,960,raycast:true);
            ArtImage(root,"20_Frame","panel_frame",0,0,800,960);
            ArtImage(root,"30_Corners","panel_corners",0,0,800,960);
            var content=Rect(root,"40_Content",36,32,728,896);
            ArtImage(content,"00_TitleRibbon","ribbon_success",58,0,612,84);
            Label(content,"01_TitleTMP","탈출 성공",58,0,612,84,48,bold,TextAlignmentOptions.Midline);
            view.Summary=Label(content,"02_SummaryTMP","",0,94,728,36,26,semibold,TextAlignmentOptions.Midline);
            view.PlayersTab=Button(content,"10_PlayersTab","플레이어",0,150,356,56,true,out _);
            view.StatisticsTab=Button(content,"11_StatisticsTab","런 통계",372,150,356,56,true,out _);
            view.PlayersUnderline=ArtImage(content,"12_PlayersSelected","tab_selected",16,208,324,8,Brass);
            view.StatisticsUnderline=ArtImage(content,"13_StatisticsSelected","tab_selected",388,208,324,8,Brass);
            view.TabNormal=Sprites["tab_normal"];view.TabSelected=Sprites["tab_selected"];
            view.EscapedIcon=Sprites["escaped"];view.NotEscapedIcon=Sprites["not_escaped"];
            var players=Rect(content,"20_Players",0,230,728,540);view.PlayersContent=players.gameObject;
            Label(players,"HeaderPlayerTMP","단원",8,0,310,36,22);
            Label(players,"HeaderOutcomeTMP","탈출 여부",352,0,140,36,22);
            Label(players,"HeaderItemsTMP","마지막 기물",536,0,192,36,22);
            Divider(players,40);
            view.PlayerRows=new EscapePlayerRow[4];
            for(int i=0;i<4;i++)view.PlayerRows[i]=PlayerRow(players,i,48+i*118);
            var statistics=Rect(content,"30_Statistics",0,230,728,540);view.StatisticsContent=statistics.gameObject;
            Label(statistics,"HeadingTMP","공연별 기록",0,0,728,40,28,semibold);
            var headers=new[]{"공연","난이도","클리어율","평균 호응"};float[] xs={8,250,420,574};float[] widths={230,150,140,146};
            for(int i=0;i<4;i++)Label(statistics,"Header"+i,headers[i],xs[i],44,widths[i],34,23,medium,i==0?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Midline);
            Divider(statistics,80);
            view.StatisticRows=new EscapeStatisticRow[6];
            for(int i=0;i<6;i++)
            {
                var row=Rect(statistics,"Row_"+(i+1),0,82+i*40,728,40);
                var labels=new TMP_Text[4];for(int j=0;j<4;j++)labels[j]=Label(row,"Column"+j,"",xs[j],0,widths[j],38,24,medium,j==0?TextAlignmentOptions.MidlineLeft:TextAlignmentOptions.Midline);
                Divider(row,39);view.StatisticRows[i]=new EscapeStatisticRow{Root=row.gameObject,Performance=labels[0],Difficulty=labels[1],ClearRate=labels[2],AverageCheer=labels[3]};
            }
            ArtImage(statistics,"TeamCard","reward_row",0,338,352,110);
            ArtImage(statistics,"PersonalCard","reward_row",368,338,360,110);
            Label(statistics,"TeamTitleTMP","한 공연 최고 팀 호응",12,348,328,32,24,semibold,TextAlignmentOptions.Midline);
            view.TeamBest=Label(statistics,"TeamBestTMP","—",12,382,328,58,46,bold,TextAlignmentOptions.Midline);
            Label(statistics,"PersonalTitleTMP","한 공연 최고 개인 호응",380,348,336,32,24,semibold,TextAlignmentOptions.Midline);
            view.PersonalBestPortrait=Portrait(statistics,"PersonalPortrait",380,386,58);
            view.PersonalBestPortraitFrame=statistics.Find("PersonalPortraitFrame").gameObject;
            view.PersonalBest=Label(statistics,"PersonalBestTMP","—",380,386,336,54,28,semibold);
            Divider(statistics,468);
            Label(statistics,"TokensHeadingTMP","런 누적 토큰",0,493,190,38,24,semibold);
            ArtImage(statistics,"EarnedIcon","icon_currency",204,499,30,26,Brass);
            view.EarnedTokens=Label(statistics,"EarnedTMP","—",242,493,230,38,24,semibold);
            ArtImage(statistics,"SpentIcon","icon_currency",484,499,30,26,new Color32(224,108,120,255));
            view.SpentTokens=Label(statistics,"SpentTMP","—",522,493,206,38,24,semibold);
            Divider(content,786);
            view.ReturnButton=Button(content,"50_ReturnToLobby","로비로 돌아가기",100,814,528,72,false,out var returnLabel);view.ReturnLabel=returnLabel;
            view.HelpLabel=Label(content,"60_HelpTMP","버튼을 누르면 로비로 이동합니다",0,892,728,32,21,medium,TextAlignmentOptions.Midline);
            var nav=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.Explicit};
            nav.selectOnRight=view.StatisticsTab;nav.selectOnDown=view.ReturnButton;view.PlayersTab.navigation=nav;
            nav.selectOnRight=null;nav.selectOnLeft=view.PlayersTab;view.StatisticsTab.navigation=nav;
            nav.selectOnLeft=null;nav.selectOnDown=null;nav.selectOnUp=view.PlayersTab;view.ReturnButton.navigation=nav;
            return view;
        }

        static UnityEngine.UI.Image Portrait(Transform parent,string name,float x,float y,float size)
        {
            float inset=size*.025f; // Circle radius 60/128; keep its edge inside the ring's inner radius 57.5/128.
            var mask=ArtImage(parent,name,"circle",x+inset,y+inset,size-2*inset,size-2*inset,new Color32(17,27,32,255));
            mask.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=true;
            var portrait=ArtImage(mask.transform,"Portrait","rabbit",-size*.08f-inset,-size*.08f-inset,size*1.16f,size*1.16f);portrait.preserveAspect=true;
            ArtImage(parent,name+"Frame","portrait_ring",x,y,size,size,Secondary);return portrait;
        }
        static EscapePlayerRow PlayerRow(Transform parent,int index,float y)
        {
            var row=Rect(parent,"PlayerRow_"+(index+1),0,y,728,118);
            var portrait=Portrait(row,"00_Portrait",4,8,100);
            var dot=ArtImage(row,"10_SlotColor","circle",126,44,24,24);
            var name=Label(row,"20_NameTMP","",164,32,178,48,26,semibold);
            var outcome=ArtImage(row,"30_Outcome","escaped",350,31,50,50);
            var outcomeText=Label(row,"31_OutcomeTMP","",408,32,106,48,26,semibold);
            var items=new UnityEngine.UI.Image[2];var empty=new TMP_Text[2];
            for(int i=0;i<2;i++)
            {
                var slot=ArtImage(row,"40_ItemSlot"+i,"reward_row",534+i*100,12,92,92);
                items[i]=ArtImage(slot.transform,"Icon","icon_currency",14,14,64,64);items[i].preserveAspect=true;
                empty[i]=Label(slot.transform,"EmptyTMP","—",0,0,92,92,28,medium,TextAlignmentOptions.Midline);empty[i].color=Secondary;
            }
            Divider(row,116);
            return new EscapePlayerRow{Root=row.gameObject,Portrait=portrait,SlotColor=dot,Name=name,Outcome=outcome,OutcomeLabel=outcomeText,Items=items,EmptyItems=empty};
        }

        public static void Sample(EscapeResultsView view)
        {
            string[] names={"rabbit","pierrot","girl","bear"};Color32[] colors={new Color32(255,113,122,255),new Color32(76,141,219,255),new Color32(205,166,216,255),new Color32(186,229,218,255)};
            var players=new EscapePlayerData[4];
            for(int i=0;i<4;i++) players[i]=new EscapePlayerData{Name="단원 "+(i+1)+(i==0?" · 나":""),Portrait=Sprites[names[i]],SlotColor=colors[i],Escaped=i<3};
            players[0].Items=new[]{Sprites["item_spring-shoes"],Sprites["item_rescue-gloves"]};
            players[1].Items=new[]{Sprites["item_magician-hairpin"],Sprites["item_uv-lamp"]};
            players[2].Items=new[]{Sprites["item_balloon-pants"],Sprites["item_sticky-gloves"]};
            view.SetPlayers(players);
            var stats=new EscapeStatisticData[6];string[] difficulty={"쉬움","보통","어려움"};string[] rates={"100%","75%","50%"};
            for(int i=0;i<6;i++) stats[i]=new EscapeStatisticData{Performance=i<3?"외줄타기":"인간 대포",Difficulty=difficulty[i%3],ClearRate=rates[i%3],AverageCheer=(i<3?120+(i%3)*60:140+(i%3)*70).ToString()};
            view.SetStatistics(stats,"920","단원 1 · 340",Sprites["rabbit"],"총 획득 4,280","총 소모 2,960");
        }

        static void Validate(EscapeResultsView view)
        {
            var errors=new List<string>();
            foreach(var image in view.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if(image.enabled && image.sprite==null)errors.Add("Missing Sprite: "+image.name);
            foreach(var text in view.GetComponentsInChildren<TMP_Text>(true))
            {
                text.ForceMeshUpdate(true,true);
                if(!text.font.HasCharacters(text.text,out uint[] missing))errors.Add("Missing glyphs: "+text.name);
                if(text.isTextTruncated)errors.Add("Truncated: "+text.name+" "+text.text);
            }
            // Verify transitions, permission guard and suppression of repeated return requests.
            view.ShowStatistics();if(!view.StatisticsContent.activeSelf||view.PlayersContent.activeSelf)errors.Add("Statistics tab visibility");
            view.ShowPlayers();if(!view.PlayersContent.activeSelf||view.StatisticsContent.activeSelf)errors.Add("Players tab visibility");
            int requests=0;UnityEngine.Events.UnityAction count=()=>requests++;view.ReturnRequested.AddListener(count);
            view.SetReturnAllowed(false);view.RequestReturn();if(requests!=0)errors.Add("Non-authority return accepted");
            view.SetReturnAllowed(true);view.SetVisible(false);view.RequestReturn();if(requests!=0)errors.Add("Hidden return accepted");
            view.SetVisible(true);view.RequestReturn();view.RequestReturn();if(requests!=1)errors.Add("Return repeat guard");
            view.ReturnRequested.RemoveListener(count);view.SetReturnAllowed(true);
            Directory.CreateDirectory("Artifacts");File.WriteAllText("Artifacts/verification.json",JsonUtility.ToJson(new Verification{errors=errors.ToArray(),spriteCount=Sprites.Count,textCount=view.GetComponentsInChildren<TMP_Text>(true).Length},true));
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
        }
        [Serializable] sealed class Verification {public string[] errors;public int spriteCount,textCount;}

        static void Capture(Canvas canvas,Camera camera,EscapeResultsView view,bool stats,string name)
        {
            view.SelectTab(stats);view.SetVisible(true);
            var texture=new RenderTexture(1920,1080,24);camera.targetTexture=texture;camera.aspect=1920f/1080;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
            Canvas.ForceUpdateCanvases();foreach(var text in view.GetComponentsInChildren<TMP_Text>(true))text.ForceMeshUpdate(true,true);
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=texture;
            var image=new Texture2D(1920,1080,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
            File.WriteAllBytes("Artifacts/"+name+"-unity.png",image.EncodeToPNG());RenderTexture.active=previous;
            camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(texture);
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;
        }
    }
}
#endif
