using System.Threading.Tasks;
using CurtainCall.UI.Wayfinding.Editor;

public static class CapturePulseLoop
{
    public static Task<string> Run() => NavigationPulseBuilder.CaptureLoop();
}
