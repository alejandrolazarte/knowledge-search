using System.Net;
using System.Net.Http.Json;
using KnowledgeSearch;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_LogEndpointIsRequested : IDisposable
{
    readonly Mock<ILogService> _mockLog = new();
    readonly WebApplicationFactory<Program> _factory;
    readonly HttpClient _client;

    public When_LogEndpointIsRequested()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILogService));
                if (descriptor != null) services.Remove(descriptor);
                services.AddSingleton(_mockLog.Object);
            }));
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Then_ReturnsEventsFromLogService()
    {
        var events = new List<LogEvent>
        {
            new(DateTime.UtcNow.ToString("o"), "updated", "golang/install.md")
        };
        _mockLog.Setup(l => l.ReadLast(100)).Returns(events);

        var response = await _client.GetAsync("/log");
        var result   = await response.Content.ReadFromJsonAsync<List<LogEvent>>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ShouldHaveSingleItem();
        result![0].Type.ShouldBe("updated");
        result![0].Path.ShouldBe("golang/install.md");
        _mockLog.Verify(l => l.ReadLast(100), Times.Once);
    }

    public void Dispose() => _factory.Dispose();
}
