﻿using Splat;
using Totoro.Plugins.Anime.Contracts;
using Totoro.Plugins.Anime.Models;

namespace Totoro.Core.Services.StreamResolvers;

public class AnimDLVideoStreamResolver : IVideoStreamModelResolver, IEnableLogger
{
    private readonly AnimeProvider _provider;
    private readonly ISettings _settings;
    private readonly string _baseUrlSub;
    private readonly string _baseUrlDub;

    public AnimDLVideoStreamResolver(AnimeProvider provider,
                                     ISettings settings,
                                     string baseUrlSub)
    {

        _provider = provider;
        _settings = settings;
        _baseUrlSub = baseUrlSub;
    }

    public AnimDLVideoStreamResolver(AnimeProvider provider,
                                     ISettings settings,
                                     string baseUrlSub,
                                     string baseUrlDub)
    {

        _provider = provider;
        _settings = settings;
        _baseUrlSub = baseUrlSub;
        _baseUrlDub = baseUrlDub;
    }

    public async Task<VideoStreamsForEpisodeModel> ResolveEpisode(int episode, StreamType streamType)
    {
        this.Log().Debug("resolving stream link for episode {0}", episode);

        var results = await GetStreams(episode, streamType);

        if (results.Count == 0)
        {
            this.Log().Debug("No streams found");
            return null;
        }

        return new VideoStreamsForEpisodeModel(results.First());
    }

    public async Task<int> GetNumberOfEpisodes(StreamType streamType)
    {
        try
        {
            var url = GetUrlForStreamType(streamType);
            var count = _provider?.StreamProvider switch
            {
                IMultiLanguageAnimeStreamProvider mp => await mp.GetNumberOfStreams(url, streamType),
                IAnimeStreamProvider sp => await sp.GetNumberOfStreams(url),
                _ => 0
            };

            return count;
        }
        catch (Exception ex)
        {
            this.Log().Fatal(ex);
            return 0;
        }
    }

    public async Task<EpisodeModelCollection> ResolveAllEpisodes(StreamType streamType)
    {
        return EpisodeModelCollection.FromEpisodeCount(await GetNumberOfEpisodes(streamType), _settings.SkipFillers);
    }

    private async Task<List<VideoStreamsForEpisode>> GetStreams(int episode, StreamType streamType)
    {
        try
        {
            var url = GetUrlForStreamType(streamType);
            return _provider?.StreamProvider switch
            {
                IMultiLanguageAnimeStreamProvider mp => await mp.GetStreams(url, episode..episode, streamType).ToListAsync(),
                IAnimeStreamProvider sp => await sp.GetStreams(url, episode..episode).ToListAsync(),
                _ => []
            };
        }
        catch (Exception ex)
        {
            this.Log().Fatal(ex);
            return [];
        }
    }

    private string GetUrlForStreamType(StreamType streamType)
    {
        return streamType.AudioLanguage == Languages.Japanese
            ? _baseUrlSub
            : _baseUrlDub ?? _baseUrlSub;
    }
}

