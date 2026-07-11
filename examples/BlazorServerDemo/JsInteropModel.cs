using Microsoft.JSInterop;

namespace BlazorServerDemo;

public class JsInteropModel
{
    [JSInvokable]
    public string GetServerTime()
    {
        return DateTime.Now.ToString("HH:mm:ss.fff");
    }
}
