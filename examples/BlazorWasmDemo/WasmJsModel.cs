using Microsoft.JSInterop;

namespace BlazorWasmDemo;

public class WasmJsModel
{
    [JSInvokable]
    public string GetCurrentTime() => DateTime.Now.ToString("HH:mm:ss.fff");
}
