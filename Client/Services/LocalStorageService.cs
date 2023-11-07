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
        await _jsRuntime.InvokeAsync<Guid>("localStorage.setItem", key, value.ToString());
    }

    public async Task<Guid> GetItemAsync(string key)
    {
        var value = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);

        return Guid.TryParse(value, out Guid result) ? result : Guid.Empty;
    }
}
