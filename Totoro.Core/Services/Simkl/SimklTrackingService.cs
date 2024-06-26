﻿using Microsoft.Extensions.Configuration;

namespace Totoro.Core.Services.Simkl;

internal class SimklTrackingService(ISimklClient simklClient,
                                    IAnilistService anilistService,
                                    ILocalSettingsService localSettingsService,
                                    IConfiguration configuration) : ITrackingService
{
    private readonly ISimklClient _simklClient = simklClient;
    private readonly IAnilistService _anilistService = anilistService;
    private readonly SimklWatchStatus[] _status = [SimklWatchStatus.Watching, SimklWatchStatus.PlanToWatch, SimklWatchStatus.Completed, SimklWatchStatus.Hold, SimklWatchStatus.Dropped];
    private readonly string _clientId = configuration["ClientIdSimkl"];

    public ListServiceType Type => ListServiceType.Simkl;

    public bool IsAuthenticated { get; private set; } = !string.IsNullOrEmpty(localSettingsService.ReadSetting<string>("SimklToken"));

    public async Task<bool> Delete(long id)
    {
        await _simklClient.RemoveItems(new SimklMutateListBody
        {
            Shows =
            [
                new()
                {
                    Ids = new SimklIds
                    {
                        Simkl = id
                    }
                }
            ]
        });

        return true;
    }

    public async IAsyncEnumerable<AnimeModel> GetAnime()
    {
        if(!IsAuthenticated)
        {
            yield break;
        }

        foreach (var status in _status)
        {
            var items = await _simklClient.GetAllItems(ItemType.Anime, status);
            foreach (var item in items.Anime)
            {
                var model = SimklToAnimeModelConverter.Convert(item);
                if (item.Status == "watching" && model.AiringStatus == AiringStatus.CurrentlyAiring)
                {
                    var epAndTime = await _anilistService.GetNextAiringEpisode(item.Show.Id.Simkl ?? item.Show.Id.Simkl2 ?? 0);
                    model.AiredEpisodes = epAndTime.Episode - 1 ?? 0;
                    model.NextEpisodeAt = epAndTime.Time;
                }

                yield return model;
            }
        }
    }

    public IAsyncEnumerable<AnimeModel> GetCurrentlyAiringTrackedAnime()
    {
        return AsyncEnumerable.Empty<AnimeModel>();
    }

    public async Task<User> GetUser()
    {
        var settings = await _simklClient.GetUserSettings();
        return new User
        {
            Name = settings.User.Name,
            Image = settings.User.Avatar
        };
    }

    public void SetAccessToken(string accessToken)
    {
        IsAuthenticated = true;
    }

    public async Task<Tracking> Update(long id, Tracking tracking)
    {
        if(!IsAuthenticated)
        {
            return tracking;
        }

        var episodes = new List<EpisodeSlim>();

        if (tracking.WatchedEpisodes is > 0)
        {
            var eps = await _simklClient.GetEpisodes(id, _clientId);
            if (eps.FirstOrDefault(x => x.EpisodeNumber == tracking.WatchedEpisodes) is { } epInfo)
            {
                episodes.Add(new EpisodeSlim
                {
                    Ids = new SimklIds
                    {
                        Simkl = epInfo.Ids.Simkl
                    }
                });
            }
        }

        if (episodes.Count > 0 || tracking.Score is { })
        {
            await _simklClient.AddItems(new SimklMutateListBody
            {
                Shows =
                [
                    new()
                    {
                        Ids = new SimklIds
                        {
                            Simkl = id
                        },
                        Rating = tracking.Score
                    }
                ],
                Episodes = episodes
            });
        }

        if (tracking.Status is { } status)
        {
            await _simklClient.MoveItems(new SimklMutateListBody
            {
                Shows =
                [
                    new()
                    {
                        Ids = new SimklIds
                        {
                            Simkl = id
                        },
                        To = status switch
                        {
                            AnimeStatus.PlanToWatch => "plantowatch",
                            AnimeStatus.Watching => "watching",
                            AnimeStatus.OnHold => "hold",
                            AnimeStatus.Completed => "completed",
                            AnimeStatus.Dropped => "dropped",
                            _ => null
                        }
                    }
                ],
            });
        }

        return tracking;
    }
}
