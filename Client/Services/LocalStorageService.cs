using Microsoft.JSInterop;

namespace SeaBattle.Client.Services;

public class LocalStorageService : ILocalStorageService, IDisposable
{
    private IJSRuntime? _jsRuntime;
    private bool _disposed = false;

    public LocalStorageService(IJSRuntime runtime)
    {
        _jsRuntime = runtime;
    }

    public async Task SetItemAsync(string key, object value)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(LocalStorageService));
        
        if (_jsRuntime != null)
        {
            await _jsRuntime.InvokeAsync<string>("localStorage.setItem", key, value.ToString());
        }
    }

    public async Task<string> GetItemAsync(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(LocalStorageService));;
        
        if (_jsRuntime != null)
        {
            return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", key);
        }
        
        return string.Empty;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _jsRuntime = null;
            _disposed = true;
        }
    }
}
