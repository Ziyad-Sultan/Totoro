﻿namespace Totoro.Core.Services.MediaEvents;

internal class TrackingUpdater : MediaEventListener
{
    private readonly ITrackingServiceContext _trackingService;
    private readonly ISettings _settings;
    private readonly IViewService _viewService;
    private TimeSpan _updateAt = TimeSpan.MaxValue;
    private TimeSpan _position;
    private TimeSpan _duration;
    private static readonly TimeSpan _nextBuffer = TimeSpan.FromMinutes(3);
    private bool _isUpdated;

    public TrackingUpdater(ITrackingServiceContext trackingService,
                           ISettings settings,
                           IViewService viewService)
    {
        _trackingService = trackingService;
        _settings = settings;
        _viewService = viewService;
    }

    protected override void OnDurationChanged(TimeSpan duration)
    {
        _duration = duration;
        _updateAt = duration - TimeSpan.FromSeconds(_settings.TimeRemainingWhenEpisodeCompletesInSeconds);
    }

    protected override void OnTimestampsChanged()
    {
        if(_timeStamps.Ending is not { } ed)
        {
            return;
        }

        _updateAt = TimeSpan.FromSeconds(ed.Interval.StartTime);
    }

    protected override void OnPositionChanged(TimeSpan position)
    {
        _position = position;
        if(!IsEnabled || position < _updateAt || _isUpdated || _animeModel.Tracking.WatchedEpisodes >= _currentEpisode)
        {
            return;
        }

        _ = UpdateTracking();
    }

    protected override void OnNextTrack()
    {
        // if less than 3 minutes remaining when clicking next episode, update tracking
        if(_duration - _position > _nextBuffer)
        {
            return;
        }

        _ = UpdateTracking();
    }

    protected override void OnEpisodeChanged()
    {
        _isUpdated = false;
    }

    private async Task UpdateTracking()
    {
        _isUpdated = true;
        var tracking = new Tracking() { WatchedEpisodes = _currentEpisode };

        if (_currentEpisode == _animeModel.TotalEpisodes)
        {
            tracking.Status = AnimeStatus.Completed;
            tracking.FinishDate = DateTime.Today;

            if(_animeModel?.Tracking?.Score is null && await _viewService.RequestRating(_animeModel) is { } score && score > 0 )
            {
                tracking.Score = score;
            }

        }
        else if (_currentEpisode == 1)
        {
            tracking.Status = AnimeStatus.Watching;
            tracking.StartDate = DateTime.Today;
        }

        _trackingService
            .Update(_animeModel.Id, tracking)
            .Subscribe(tracking => _animeModel.Tracking = tracking);
    }

    public bool IsEnabled => _animeModel is not null;
}
