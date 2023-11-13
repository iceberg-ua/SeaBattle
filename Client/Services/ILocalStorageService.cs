namespace SeaBattle.Client.Services;

public interface ILocalStorageService
{
    Task SetItemAsync(string key, object value);

    Task<string> GetItemAsync(string key);
}
