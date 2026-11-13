using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Video;

public static class EndingVideoVerification
{
    static async Task Wait(Func<bool> test,string why)
    {var end=DateTime.UtcNow.AddSeconds(30);while(!test()){if(DateTime.UtcNow>end)throw new Exception(why);await Task.Delay(30);}}
    public static async Task<object> Review(string name)
    {
        const string folder="Tasks/Active/Task-20261114-131-escape-success-art/Videos/";
        var root=new GameObject("Video verification"){hideFlags=HideFlags.HideAndDontSave};
        var player=root.AddComponent<VideoPlayer>();
        var target=new RenderTexture(1920,1080,0);target.Create();
        var small=new RenderTexture(480,270,0);small.Create();
        var frame=new Texture2D(480,270,TextureFormat.RGB24,false);
        var sheet=new Texture2D(960,810,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;string error=null;
        try
        {
            player.playOnAwake=false;player.source=VideoSource.Url;player.url=Path.GetFullPath(folder+name+".mp4");
            player.renderMode=VideoRenderMode.RenderTexture;player.targetTexture=target;player.audioOutputMode=VideoAudioOutputMode.None;
            player.errorReceived+=(p,e)=>error=e;player.Prepare();await Wait(()=>player.isPrepared||error!=null,"Video prepare");
            if(error!=null)throw new Exception(error);
            var times=new[]{.5,3.5,7.0,11.5,16.0,Math.Max(17,player.length-1)};
            for(int i=0;i<times.Length;i++)
            {
                player.time=times[i];player.Play();
                await Wait(()=>player.frame>=(long)(times[i]*player.frameRate)-1||error!=null,"Decode frame");
                await Task.Delay(150);player.Pause();
                Graphics.Blit(target,small);RenderTexture.active=small;
                frame.ReadPixels(new Rect(0,0,480,270),0,0);frame.Apply();
                sheet.SetPixels(i%2*480,(2-i/2)*270,480,270,frame.GetPixels());
            }
            sheet.Apply();File.WriteAllBytes(folder+name+"-review.png",sheet.EncodeToPNG());
            return new{name,player.length,player.width,player.height,player.frameRate,player.frameCount,times,bytes=new FileInfo(player.url).Length};
        }
        finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(root);target.Release();small.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(small);UnityEngine.Object.DestroyImmediate(frame);UnityEngine.Object.DestroyImmediate(sheet);}
    }
}
