using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace wh40kAPI.Server.Controllers;

[ApiController]
[Route("api/version")]
public class VersionController(IHttpClientFactory httpClientFactory, IMemoryCache cache) : ControllerBase
{
    private const string CacheKey = "last_merged_pr";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// Returns the last merged pull request number and date for the wh40kAPI repository.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (cache.TryGetValue(CacheKey, out GithubPr? cached) && cached != null)
            return Ok(new { prNumber = cached.Number, mergedAt = cached.MergedAt });

        var client = httpClientFactory.CreateClient("github");
        var response = await client.GetAsync(
            "https://api.github.com/repos/Shooshpanius/wh40kAPI/pulls?state=closed&sort=updated&direction=desc&per_page=20");

        if (!response.IsSuccessStatusCode)
            return StatusCode((int)response.StatusCode, "Failed to fetch PR info from GitHub");

        var json = await response.Content.ReadAsStringAsync();
        var prs = JsonSerializer.Deserialize<List<GithubPr>>(json);

        var lastMerged = prs?.FirstOrDefault(p => p.MergedAt.HasValue);

        if (lastMerged == null)
            return NotFound("No merged pull requests found");

        cache.Set(CacheKey, lastMerged, CacheDuration);

        return Ok(new { prNumber = lastMerged.Number, mergedAt = lastMerged.MergedAt });
    }

    private sealed class GithubPr
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("merged_at")]
        public DateTime? MergedAt { get; set; }
    }
}
