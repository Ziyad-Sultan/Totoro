﻿using AnitomySharp;
using Splat;
using Totoro.Core.Services.Debrid;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Services.StreamResolvers;

public class DebridStreamModelResolver : IVideoStreamModelResolver, IEnableLogger
{
    private readonly IDebridServiceContext _debridService;
    private readonly string _magnet;
    private List<DirectDownloadLink> _links;

    public DebridStreamModelResolver(IDebridServiceContext debridService,
                                     string magnet)
    {
        _debridService = debridService;
        _magnet = magnet;
    }

    public async Task Populate()
    {
        _links = (await _debridService.GetDirectDownloadLinks(_magnet)).ToList();

        var options = new Options(title: false, extension: false, group: false);
        foreach (var item in _links)
        {
            var parsedResult = AnitomySharp.AnitomySharp.Parse(item.FileName, options);
            if (parsedResult.FirstOrDefault(x => x.Category == Element.ElementCategory.ElementEpisodeNumber) is { } epString && int.TryParse(epString.Value, out var ep))
            {
                item.Episode = ep;
            }
        }
    }

    public Task<EpisodeModelCollection> ResolveAllEpisodes(StreamType streamType)
    {
        return Task.FromResult(EpisodeModelCollection.FromDirectDownloadLinks(_links));
    }

    public Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, StreamType streamType)
    {
        var ddl = _links.FirstOrDefault(x => x.Episode == episode);
        return Task.FromResult(new VideoStreamsForEpisodeModel(ddl));
    }
}

