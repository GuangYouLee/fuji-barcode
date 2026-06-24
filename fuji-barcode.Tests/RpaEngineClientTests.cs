using System.Net;
using System.Text;
using System.Text.Json;
using fuji_barcode.Models;
using fuji_barcode.Services;
using Xunit;

namespace fuji_barcode.Tests;

public class RpaEngineClientTests
{
    private static RpaEngineClient CreateClient(FakeMessageHandler handler, string? targetName = null)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        return new RpaEngineClient(httpClient, targetName);
    }

    [Fact]
    public async Task RunScriptAsync_returns_success_for_started_status_payload()
    {
        var body = JsonSerializer.Serialize(new
        {
            status = "started",
            scriptName = "group_script1",
            totalCommands = 5
        });

        var handler = new FakeMessageHandler(HttpStatusCode.Accepted, body);
        var client = CreateClient(handler);

        var result = await client.RunScriptAsync("group_script1");

        Assert.True(result.Success);
        Assert.Equal("group_script1", result.Message);
    }

    [Fact]
    public async Task RunScriptAsync_preserves_message_for_started_status_payload()
    {
        var body = JsonSerializer.Serialize(new
        {
            status = "started",
            scriptName = "group_script1",
            message = "Queued on remote target"
        });

        var handler = new FakeMessageHandler(HttpStatusCode.Accepted, body);
        var client = CreateClient(handler);

        var result = await client.RunScriptAsync("group_script1");

        Assert.True(result.Success);
        Assert.Equal("Queued on remote target", result.Message);
    }

    [Fact]
    public async Task RunScriptAsync_preserves_legacy_success_payload()
    {
        var body = JsonSerializer.Serialize(new
        {
            success = true,
            message = "Started"
        });

        var handler = new FakeMessageHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        var result = await client.RunScriptAsync("some_script");

        Assert.True(result.Success);
        Assert.Equal("Started", result.Message);
    }

    [Fact]
    public async Task RunScriptAsync_preserves_explicit_failure_payload()
    {
        var body = JsonSerializer.Serialize(new
        {
            success = false,
            message = "Already running"
        });

        var handler = new FakeMessageHandler(HttpStatusCode.OK, body);
        var client = CreateClient(handler);

        var result = await client.RunScriptAsync("some_script");

        Assert.False(result.Success);
        Assert.Equal("Already running", result.Message);
    }

    [Fact]
    public async Task RunScriptAsync_treats_empty_success_body_as_success()
    {
        var handler = new FakeMessageHandler(HttpStatusCode.Accepted, "");
        var client = CreateClient(handler);

        var result = await client.RunScriptAsync("empty_response_script");

        Assert.True(result.Success);
        Assert.Equal("empty_response_script", result.Message);
    }

    [Fact]
    public async Task RunScriptAsync_appends_target_query_when_target_name_is_configured()
    {
        var handler = new FakeMessageHandler(HttpStatusCode.Accepted, """{"status":"started"}""");
        var client = CreateClient(handler, targetName: "my-target");

        var result = await client.RunScriptAsync("abc");

        Assert.True(result.Success);
        Assert.Contains("?target=my-target", handler.RequestUri);
    }

    private sealed class FakeMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public string? RequestUri { get; private set; }

        public FakeMessageHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
