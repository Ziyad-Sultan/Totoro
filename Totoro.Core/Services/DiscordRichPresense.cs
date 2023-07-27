﻿
using DiscordRPC;

namespace Totoro.Core.Services;

public class DiscordRichPresense : IDiscordRichPresense
{
    private readonly DiscordRpcClient _client = new("997177919052984622");

    public void Initialize() => _client.Initialize();
    public bool IsInitialized => _client.IsInitialized;
    public void UpdateDetails(string details) => _client.UpdateDetails(details);
    public void UpdateState(string state) => _client.UpdateState(state);
    public void UpdateTimer(TimeSpan timeSpan) => _client.UpdateEndTime(DateTime.UtcNow + timeSpan);
    public void SetPresence() => _client.SetPresence(new RichPresence().WithAssets(new Assets() { LargeImageKey = "icon" }));
    public void UpdateImage(string url)
    {
        _client.UpdateLargeAsset(url);

        if (url != "icon")
        {
            _client.UpdateSmallAsset("icon");
        }
    }
    public void ClearTimer() => _client.UpdateClearTime();
    public void Clear() => _client.ClearPresence();
    public void SetUrl(string url)
    {
        if(string.IsNullOrEmpty(url))
        {
            return;
        }

        var buttons = new Button[]
        {
            new Button()
            {
                Url = url,
                Label = "View Anime"
            }
        };

        _client.UpdateButtons(buttons);
    }
}
