﻿using Humanizer;

namespace AnimDL.UI.Core.Models;

public class AiredEpisode
{
    public string Anime { get; set; }
    public string InfoText { get; set; }
    public string EpisodeUrl { get; set; }
    public string Image { get; set; }
    public DateTime TimeOfAiring { get; set; }
    public string HumanizedTimeOfAiring => TimeOfAiring.Humanize();
    public IAnimeModel Model { get; set; }
}
