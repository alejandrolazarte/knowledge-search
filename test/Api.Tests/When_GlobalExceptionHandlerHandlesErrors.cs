using System.Net;
using System.Net.Http.Json;
using KnowledgeSearch;
using KnowledgeSearch.Core.Abstractions.Search;
using KnowledgeSearch.Core.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_GlobalExceptionHandlerHandlesErrors : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public When_GlobalExceptionHandlerHandlesErrors()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                var searchIndex = new Mock<IDocumentSearchIndex>();
                searchIndex
                    .Setup(index => index.Search(
                        It.IsAny<string>(),
                        It.IsAny<int>(),
                        It.IsAny<SearchMode>(),
                        It.IsAny<IReadOnlyList<string>?>()))
                    .Throws(new InvalidOperationException("SQLite FTS query failed"));

                ReplaceService<IDocumentSearchIndex>(services, searchIndex.Object);
            }));

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Then_ReturnsExceptionMessage()
    {
        var response = await _client.GetAsync("/search?q=broken");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        var error = await response.Content.ReadFromJsonAsync<ErrorResult>();
        error!.Error.ShouldBe("SQLite FTS query failed");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void ReplaceService<T>(IServiceCollection services, T instance)
        where T : class
    {
        var descriptor = services.SingleOrDefault(service => service.ServiceType == typeof(T));
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(instance);
    }
}
