using System.Globalization;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using ValueTypeGenerators.Sample;

using Xunit;

namespace ValueTypeGenerators.Tests;

public sealed class WebApiTests : IClassFixture<WebApplicationFactory<Program>> {
    private readonly HttpClient _api;

    public WebApiTests(WebApplicationFactory<Program> factory) {
        ArgumentNullException.ThrowIfNull(factory);

        _api = factory.CreateClient();
    }

    [Fact]
    public async Task Root() {
        using var response = await _api.GetAsync("/");
        Assert.True(response.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData("B")]
    [InlineData("D")]
    [InlineData("N")]
    [InlineData("P")]
    [InlineData("X")]
    public async Task EchoGuid(string format) {
        var value = Guid.NewGuid();
        using var response = await _api.GetAsync($"/echo/guid/{value.ToString(format)}");
        Assert.Equal(value, JsonSerializer.Deserialize<Guid>(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task EchoInt() {
        const int value = 1234567890;
        using var response = await _api.GetAsync($"/echo/int/{value}");
        Assert.Equal(value, int.Parse(await response.Content.ReadAsStringAsync(), CultureInfo.InvariantCulture));
    }
}
