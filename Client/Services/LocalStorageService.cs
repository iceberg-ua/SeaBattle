using Microsoft.JSInterop;

namespace SeaBattle.Client.Services;

public class LocalStorageService : ILocalStorageService
{
    private IJSRuntime _jsRuntime;

    public LocalStorageService(IJSRuntime runtime)
    {
        _jsRuntime = runtime;
    }

    public async Task SetItemAsync(string key, object value)
    {
        await _jsRuntime.InvokeAsync<string>("localStorage.setItem", key, value.ToString());
    }

    public async Task<string> GetItemAsync(string key)
    {
        return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
    }
}
