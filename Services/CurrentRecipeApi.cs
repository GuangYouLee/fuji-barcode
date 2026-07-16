using System.Net;
using System.Text;
using System.Text.Json;

namespace fuji_barcode.Services;

public sealed class CurrentRecipeApi : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly object _sync = new();
    private CurrentRecipeResponse? _current;

    public CurrentRecipeApi() => _listener.Prefixes.Add("http://+:5070/");

    public void SetRecipe(string recipeName, string scriptName, string status)
    {
        lock (_sync)
            _current = new(recipeName, scriptName, status, DateTimeOffset.UtcNow);
    }

    public void Start() => _ = ListenAsync();

    private async Task ListenAsync()
    {
        _listener.Start();
        try
        {
            while (_listener.IsListening)
                _ = HandleAsync(await _listener.GetContextAsync());
        }
        catch (HttpListenerException) when (!_listener.IsListening) { }
        catch (ObjectDisposedException) { }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            if (context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != "/api/current-recipe")
            {
                context.Response.StatusCode = 404;
                return;
            }

            CurrentRecipeResponse? current;
            lock (_sync)
                current = _current;

            var body = new
            {
                success = true,
                data = current is null
                    ? null
                    : new
                    {
                        execute_label = "recipe",
                        message = current.RecipeName + "\r\n"
                    }
            };
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body));
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        finally
        {
            context.Response.Close();
        }
    }

    public void Dispose()
    {
        if (_listener.IsListening)
            _listener.Stop();
        _listener.Close();
    }
}

public sealed record CurrentRecipeResponse(
    string RecipeName,
    string ScriptName,
    string Status,
    DateTimeOffset ScannedAt);
