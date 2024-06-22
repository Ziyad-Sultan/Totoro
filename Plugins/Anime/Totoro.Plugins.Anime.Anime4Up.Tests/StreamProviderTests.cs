﻿using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Flurl;
using Totoro.Plugins.Options;
using Xunit.Abstractions;

namespace Totoro.Plugins.Anime.Anime4Up.Tests;

[ExcludeFromCodeCoverage]
public class StreamProviderTests
{
    public const string Hyouka = "hyouka";
    public const string OPM = "one punch man";
    public const string Nagatoro = "nagatoro san";
    public const string JJK2 = "jujutsu kaisen 2nd season";
    public const string Masumune2 = "masamune-kun-no-revenge-r";

    private readonly ITestOutputHelper _output;
    private readonly JsonSerializerOptions _searializerOption = new() { WriteIndented = true };
    private readonly Dictionary<string, string> _urlMap = new()
    {
        { Hyouka, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/hyouka/") },
        { OPM, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/one-punch-man/") },
        { Nagatoro, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/ijiranaide-nagatoro-san-2nd-attack/") },
        { JJK2, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/jujutsu-kaisen-2nd-season/") },
        { Masumune2, Url.Combine(ConfigManager<Config>.Current.Url, "/anime/masamune-kun-no-revenge-r") }
    };
    private readonly bool _allEpisodes = false;

    public StreamProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(Hyouka, 22)]
    public async Task GetNumberOfEpisodes(string key, int expected)
    {
        // arrange
        var url = _urlMap[key];
        var sut = new StreamProvider();

        // act
        var actual = await sut.GetNumberOfStreams(url);

        // assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(Hyouka)]
    [InlineData(OPM)]
    [InlineData(Nagatoro)]
    [InlineData(Masumune2)]
    public async Task GetStreams(string key)
    {
        // arrange
        var url = _urlMap[key];
        var sut = new StreamProvider();

        // act
        var result = await sut.GetStreams(url, _allEpisodes ? Range.All : 1..1).ToListAsync();

        Assert.NotEmpty(result);
        foreach (var item in result)
        {
            _output.WriteLine(JsonSerializer.Serialize(item, item.GetType(), _searializerOption));
        }
    }
}
