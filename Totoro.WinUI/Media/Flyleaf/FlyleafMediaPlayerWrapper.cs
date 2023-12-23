﻿using System.Reactive.Concurrency;
using FlyleafLib.MediaPlayer;
using Splat;
using Totoro.Plugins.Anime.Models;

namespace Totoro.WinUI.Media.Flyleaf
{
    public sealed class FlyleafMediaPlayerWrapper : IMediaPlayer, IEnableLogger
    {
        private double? _offsetInSeconds;

        public Player MediaPlayer { get; set; } = new();

        public IObservable<Unit> Paused { get; }

        public IObservable<Unit> Playing { get; }

        public IObservable<Unit> PlaybackEnded { get; }

        public IObservable<TimeSpan> PositionChanged { get; }

        public IObservable<TimeSpan> DurationChanged { get; }

        public IMediaTransportControls TransportControls { get; private set; }

        public MediaPlayerType Type => MediaPlayerType.FFMpeg;

        public void SetTransportControls(IMediaTransportControls transportControls)
        {
            TransportControls = transportControls;
        }

        public FlyleafMediaPlayerWrapper()
        {
            Paused = MediaPlayer.WhenAnyValue(x => x.Status).Where(x => x is Status.Paused).Select(x => Unit.Default);
            Playing = MediaPlayer.WhenAnyValue(x => x.Status).Where(x => x is Status.Playing).Select(x => Unit.Default);
            PlaybackEnded = MediaPlayer.WhenAnyValue(x => x.Status).Where(x => x is Status.Ended).Select(x => Unit.Default);
            DurationChanged = MediaPlayer.WhenAnyValue(x => x.Duration).Select(x => new TimeSpan(x));
            PositionChanged = MediaPlayer.WhenAnyPropertyChanged(nameof(MediaPlayer.CurTime)).Select(_ => new TimeSpan(MediaPlayer.CurTime));
            MediaPlayer.OpenCompleted += MediaPlayer_OpenCompleted;
        }

        private void MediaPlayer_OpenCompleted(object sender, OpenCompletedArgs e)
        {
            if(e.IsSubtitles)
            {
                return;
            }

            if(_offsetInSeconds is > 0)
            {
                MediaPlayer.SeekAccurate((int)TimeSpan.FromSeconds(_offsetInSeconds.Value).TotalMilliseconds);
            }

            if(MediaPlayer.Subtitles.Streams.Count > 1)
            {
                RxApp.MainThreadScheduler.Schedule(() =>
                {
                    TransportControls.IsCCSelectionVisible = true;
                    ((FlyleafTransportControls)TransportControls).UpdateSubtitleFlyout(MediaPlayer.Subtitles.Streams);
                });
            }
        }

        public ValueTask AddSubtitle(string file)
        {
            MediaPlayer.OpenAsync(file);
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            MediaPlayer.Pause();
            MediaPlayer.Dispose();
        }

        public void Pause()
        {
            MediaPlayer.Pause();
        }

        public void Play()
        {
            MediaPlayer.Play();
        }

        public void Play(double offsetInSeconds)
        {
            _offsetInSeconds = offsetInSeconds;
            MediaPlayer.Play();
        }

        public void Seek(TimeSpan ts, SeekDirection direction)
        {
            var currentTime = new TimeSpan(MediaPlayer.CurTime);
            var newTime = direction == SeekDirection.Forward
                ? currentTime + ts
                : currentTime - ts;

            MediaPlayer.SeekAccurate((int)newTime.TotalMilliseconds);
        }

        public void SeekTo(TimeSpan ts)
        {
            MediaPlayer.SeekAccurate((int)ts.TotalMilliseconds);
        }

        public Task<Unit> SetMedia(VideoStreamModel stream)
        {
            if(stream.HasHeaders)
            {
                MediaPlayer.Config.Demuxer.FormatOpt["header"] = string.Join("\r\n", stream.Headers.Select(x => $"{x.Key}:{x.Value}"));
            }

            if(stream.Stream is not null)
            {
                MediaPlayer.OpenAsync(stream.Stream);
            }
            else
            {
                this.Log().Debug($"Creating media from {stream.StreamUrl}");
                MediaPlayer.OpenAsync(stream.StreamUrl);
            }

            if (stream.AdditionalInformation.Subtitles.Count != 0)
            {
                OpenSubtitles(stream.AdditionalInformation.Subtitles);
            }

            return Task.FromResult(Unit.Default);
        }

        private void OpenSubtitles(List<Subtitle> subtitles)
        {
            foreach (var subtitle in subtitles)
            {
                MediaPlayer.OpenAsync(subtitle.Url);
            }
        }

        public void SetPlaybackRate(PlaybackRate rate)
        {
            MediaPlayer.Speed = rate.ToDouble();
        }
    }
}
