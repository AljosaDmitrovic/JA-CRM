using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace JaCrm.IntegrationTests.Infrastructure;

public static class HttpExtensions
{
    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        await response.EnsureStatusAsync(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(AppFactory.Json))!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync(this HttpClient client, string url, object? body = null) =>
        client.PostAsJsonAsync(url, body ?? new { }, AppFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync(this HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, AppFactory.Json);

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        await response.EnsureStatusAsync(expected);
        return (await response.Content.ReadFromJsonAsync<T>(AppFactory.Json))!;
    }

    /// <summary>Asserts the status code and shows the response body when it differs.</summary>
    public static async Task EnsureStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expected} {expected} but got {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
    }

    /// <summary>The field → messages map of a 400 validation problem.</summary>
    public static async Task<Dictionary<string, string[]>> ValidationErrorsAsync(this HttpResponseMessage response)
    {
        await response.EnsureStatusAsync(HttpStatusCode.BadRequest);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("errors").Deserialize<Dictionary<string, string[]>>(AppFactory.Json)!;
    }
}
