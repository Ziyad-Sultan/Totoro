﻿using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Totoro.Plugins.MediaDetection.Contracts;

namespace Totoro.Plugins.MediaDetection.Vlc
{
    internal sealed partial class MediaPlayer : ReactiveObject, INativeMediaPlayer, IHavePosition, ICanLaunch
    {
        private Application? _application;
        private Window? _mainWindow;
        private Slider? _slider;
        private readonly Subject<TimeSpan> _positionChanged = new();
        private bool _hasCustomTitle;
        private string? _customTitle;

        [Reactive] TimeSpan Duration { get; set; }
        public IObservable<TimeSpan> PositionChanged => _positionChanged;
        public IObservable<TimeSpan> DurationChanged { get; }
        public Process? Process { get; private set; }

        public MediaPlayer()
        {
            DurationChanged = this.WhenAnyValue(x => x.Duration);
        }

        public string GetTitle()
        {
            if (_hasCustomTitle)
            {
                return _customTitle!;
            }

            if (_mainWindow is null)
            {
                return "";
            }

            var title = _mainWindow.Title;

            return title.Replace("- Vlc media player", string.Empty, StringComparison.InvariantCultureIgnoreCase).Trim();
        }

        public void Launch(string title,string url)
        {
            _hasCustomTitle = true;
            _customTitle = title;
            _application = Application.Launch(Config.FileName,$"{url} --meta-title=\"{title}\" -f");
            InitializeInternal();
        }

        public void Initialize(Window window)
        {
            _mainWindow = window;
            Process = Process.GetProcessById(window.Properties.ProcessId);
            InitializeInternal();
        }

        public void Dispose()
        {
            if(_application is null)
            {
                return;
            }

            _application.Dispose();
        }

        private void InitializeInternal()
        {
            while(_mainWindow is not { IsAvailable : true })
            {
                try
                {
                    _mainWindow = _application!.GetMainWindow(new UIA3Automation());
                }
                catch { }
            }

            GetDuration();
            GetSlider();
        }

        private void GetDuration()
        {
            foreach (var item in _mainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)))
            {
                var match = DurationRegex().Match(item.Name);
                if (match.Success && item.Patterns.LegacyIAccessible.Pattern.Description.Value.Contains("Total"))
                {
                    var parts = item.Name.Split(':');

                    if(parts.Length == 3)
                    {
                        Duration = new TimeSpan(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
                    }
                    else
                    {
                        Duration = new TimeSpan(0, int.Parse(parts[0]), int.Parse(parts[1]));
                    }
                }
            }
        }

        private void GetSlider()
        {
            foreach (var item in _mainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.Slider)).Select(x => x.AsSlider()))
            {
                if (!item.Patterns.LegacyIAccessible.Pattern.Description.Value.Contains('%'))
                {
                    _slider = item;
                    var property = item.Patterns.Value.Pattern.PropertyIds.Value;
                    var handler = item.RegisterPropertyChangedEvent(TreeScope.Element, (ae, _, _) =>
                    {
                        var percent = ae.AsSlider().Value;
                        _positionChanged.OnNext(TimeSpan.FromSeconds(Duration.TotalSeconds * percent / 10000));
                    }, property);
                }
            }
        }

        [GeneratedRegex(@"(\d)+:(\d)+")]
        private static partial Regex DurationRegex();
    }
}
