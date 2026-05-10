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
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<IDbService> _mockDb = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public When_LogEndpointIsRequested()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                var logDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILogService));
                if (logDescriptor != null)
                {
                    services.Remove(logDescriptor);
                }
                services.AddSingleton(_mockLog.Object);

                var dbDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDbService));
                if (dbDescriptor != null)
                {
                    services.Remove(dbDescriptor);
                }
                services.AddSingleton(_mockDb.Object);
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
        var result = await response.Content.ReadFromJsonAsync<List<LogEvent>>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ShouldHaveSingleItem();
        result![0].Type.ShouldBe("updated");
        result![0].Path.ShouldBe("golang/install.md");
        _mockLog.Verify(l => l.ReadLast(100), Times.Once);
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
