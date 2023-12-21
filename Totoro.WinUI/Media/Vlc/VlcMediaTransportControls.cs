﻿using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using LibVLCSharp.Platforms.Windows;
using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Splat;
using Totoro.WinUI.Contracts;

namespace Totoro.WinUI.Media.Vlc;

public class VlcMediaTransportControls : Control, IEnableLogger, IMediaTransportControls
{
    public const string PlayState = "PlayState";
    public const string PauseState = "PauseState";
    public const string NormalState = "Normal";
    public const string ErrorState = "Error";
    public const string BufferingState = "Buffering";
    public const string MuteState = "MuteState";
    public const string VolumeState = "VolumeState";
    public const string ControlPanelFadeInState = "ControlPanelFadeIn";
    public const string ControlPanelFadeOutState = "ControlPanelFadeOut";
    public const string PlayPauseAvailableState = "PlayPauseAvailable";
    public const string PlayPauseUnavailableState = "PlayPauseUnavailable";
    public const string StopAvailableState = "StopAvailable";
    public const string StopUnavailableState = "StopUnavailable";
    public const string AudioSelectionAvailableState = "AudioSelectionAvailable";
    public const string AudioSelectionUnavailableState = "AudioSelectionUnavailable";
    public const string CCSelectionAvailableState = "CCSelectionAvailable";
    public const string CCSelectionUnavailableState = "CCSelectionUnavailable";
    public const string SeekBarAvailableState = "SeekBarAvailable";
    public const string SeekBarUnavailableState = "SeekBarUnavailable";
    public const string ZoomAvailableState = "ZoomAvailable";
    public const string ZoomUnavailableState = "ZoomUnavailable";
    public const string CastAvailableState = "CastAvailable";
    public const string CastUnavailableState = "CastUnavailable";
    public const string VolumeAvailableState = "VolumeAvailable";
    public const string VolumeUnavailableState = "VolumeUnavailable";
    public const string CompactModeState = "CompactMode";
    public const string NormalModeState = "NormalMode";

    private readonly Subject<Unit> _onNextTrack = new();
    private readonly Subject<Unit> _onPrevTrack = new();
    private readonly Subject<Unit> _onSkipIntro = new();
    private readonly Subject<string> _onQualityChanged = new();
    private readonly Subject<Unit> _onDynamicSkipIntro = new();
    private readonly Subject<Unit> _onSubmitTimeStamp = new();
    private readonly Subject<Unit> _onAddCc = new();
    private readonly MenuFlyout _qualitiesFlyout = new() { Placement = FlyoutPlacementMode.Top };

    /// <summary>
    /// Initializes a new instance of the <see cref="VlcMediaTransportControls"/> class
    /// </summary>
    public VlcMediaTransportControls()
    {
        Manager = new MediaPlayerElementManager(new DispatcherAdapter(), new DisplayInformation(), new DisplayRequestAdapter());
        Manager.Get<AspectRatioManager>().AspectRatioChanged += AspectRatioChanged;
        var autoHideManager = Manager.Get<AutoHideNotifier>();
        autoHideManager.Shown += (sender, e) => VisualStateManager.GoToState(this, ControlPanelFadeInState, true);
        autoHideManager.Hidden += (sender, e) => VisualStateManager.GoToState(this, ControlPanelFadeOutState, true);
        autoHideManager.Enabled = ShowAndHideAutomatically;
        //var audioTrackManager = Manager.Get<AudioTracksManager>();     // No audio track selection button
        //audioTrackManager.TracksCleared += OnTracksCleared;
        //audioTrackManager.TrackSelected += OnTrackSelected;
        //audioTrackManager.TrackAdded += OnTrackAdded;
        //audioTrackManager.TrackDeleted += OnTrackDeleted;
        var subTitlesTrackManager = Manager.Get<SubtitlesTracksManager>();
        subTitlesTrackManager.TracksCleared += OnTracksCleared;
        subTitlesTrackManager.TrackSelected += OnTrackSelected;
        subTitlesTrackManager.TrackAdded += OnTrackAdded;
        subTitlesTrackManager.TrackDeleted += OnTrackDeleted;
        var castRenderersDiscoverer = Manager.Get<CastRenderersDiscoverer>();
        castRenderersDiscoverer.CastAvailableChanged += (sender, e) => CastButtonAvailabilityCommand?.Update();
        castRenderersDiscoverer.Enabled = IsCastButtonVisible;
        var volumeManager = Manager.Get<VolumeManager>();
        volumeManager.EnabledChanged += (sender, e) => VolumeMuteButtonAvailabilityCommand?.Update();
        volumeManager.MuteChanged += (sender, e) => MuteStateCommand?.Update();
        var seekBarManager = Manager.Get<SeekBarManager>();
        seekBarManager.SeekableChanged += (sender, e) => SeekBarAvailabilityCommand?.Update();
        seekBarManager.PositionChanged += (sender, e) => UpdateTime();
        var bufferingManager = Manager.Get<BufferingProgressNotifier>();
        bufferingManager.IsBufferingChanged += (sender, e) => IsBufferingCommand?.Update();
        var stateManager = Manager.Get<StateManager>();
        stateManager.ErrorOccured += (sender, e) => ShowError();
        stateManager.Playing += (sender, e) => OnPlaying();
        stateManager.Paused += (sender, e) => OnPaused();
        stateManager.Stopped += (sender, e) => OnStopped();
        stateManager.PlayPauseAvailableChanged += (sender, e) => PlayPauseAvailabilityCommand?.Update();

        AddHandler(PointerEnteredEvent, new PointerEventHandler(OnPointerMoved), true);
        AddHandler(PointerMovedEvent, new PointerEventHandler(OnPointerMoved), true);
        AddHandler(TappedEvent, new TappedEventHandler(OnPointerMoved), true);
        AddHandler(PointerExitedEvent, new PointerEventHandler((sender, e) => Show()), true);

        App.GetService<IWindowService>()?
           .IsFullWindowChanged
           .Where(_ => FullWindowSymbol is not null)
           .Subscribe(isFullWindwow =>
           {
               FullWindowSymbol.Symbol = isFullWindwow ? Symbol.BackToWindow : Symbol.FullScreen;
           });
    }

    /// <summary>
    /// Finalizer
    /// </summary>
    ~VlcMediaTransportControls()
    {
        Manager.Dispose();
    }

    private MediaPlayerElementManager Manager { get; }
    private MenuFlyout ZoomMenu { get; set; }
    private IDictionary<MenuFlyout, TracksMenu> TracksMenus { get; } = new Dictionary<MenuFlyout, TracksMenu>();
    public IObservable<Unit> OnNextTrack => _onNextTrack;
    public IObservable<Unit> OnPrevTrack => _onPrevTrack;
    public IObservable<Unit> OnStaticSkip => _onSkipIntro;
    public IObservable<Unit> OnAddCc => _onAddCc;
    public IObservable<string> OnQualityChanged => _onQualityChanged;
    public IObservable<Unit> OnDynamicSkip => _onDynamicSkipIntro;
    public IObservable<Unit> OnSubmitTimeStamp => _onSubmitTimeStamp;
    public IObservable<PlaybackRate> PlaybackRateChanged => Observable.Empty<PlaybackRate>();
    public bool IsSkipButtonVisible
    {
        get => DynamicSkipButton?.Visibility == Visibility.Visible;
        set
        {
            if (DynamicSkipButton is null)
            {
                return;
            }

            RxApp.MainThreadScheduler.Schedule(() => DynamicSkipButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed);
        }
    }
    public bool IsNextTrackButtonVisible
    {
        get => NextTrackButton.Visibility == Visibility.Visible;
        set
        {
            if (NextTrackButton is null)
            {
                return;
            }

            RxApp.MainThreadScheduler.Schedule(() => NextTrackButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed);
        }
    }
    public bool IsPreviousTrackButtonVisible
    {
        get => PreviousTrackButton.Visibility == Visibility.Visible;
        set
        {
            if (PreviousTrackButton is null)
            {
                return;
            }

            RxApp.MainThreadScheduler.Schedule(() => PreviousTrackButton.Visibility = value ? Visibility.Visible : Visibility.Collapsed);
        }
    }

    private TextBlock ErrorTextBlock { get; set; }
    private Slider VolumeSlider { get; set; }
    private Slider ProgressSlider { get; set; }
    private TextBlock TimeElapsedElement { get; set; }
    private TextBlock TimeRemainingElement { get; set; }
    private DependencyObject PlayPauseButton { get; set; }
    private DependencyObject PlayPauseButtonOnLeft { get; set; }
    private SymbolIcon FullWindowSymbol { get; set; }
    private ButtonBase NextTrackButton { get; set; }
    private ButtonBase PreviousTrackButton { get; set; }
    private ButtonBase DynamicSkipButton { get; set; }
    private AppBarButton QualitiesButton { get; set; }
    private ButtonBase AddCCButton { get; set; }
    private AvailabilityCommand SeekBarAvailabilityCommand { get; set; }
    private AvailabilityCommand VolumeMuteButtonAvailabilityCommand { get; set; }
    private AvailabilityCommand MuteStateCommand { get; set; }
    private AvailabilityCommand PlayPauseAvailabilityCommand { get; set; }
    private AvailabilityCommand StopButtonAvailabilityCommand { get; set; }
    private AvailabilityCommand CastButtonAvailabilityCommand { get; set; }
    private AvailabilityCommand ZoomButtonAvailabilityCommand { get; set; }
    private AvailabilityCommand IsBufferingCommand { get; set; }
    private AvailabilityCommand IsCompactCommand { get; set; }
    public VideoView VideoView { get; set; }
    public LibVLC LibVLC { get; set; }
    public MediaPlayer MediaPlayer { get; set; }


    public IEnumerable<string> Resolutions
    {
        get { return (IEnumerable<string>)GetValue(ResolutionsProperty); }
        set { SetValue(ResolutionsProperty, value); }
    }

    public static readonly DependencyProperty ResolutionsProperty =
        DependencyProperty.Register("Resolutions", typeof(IEnumerable<string>), typeof(VlcMediaTransportControls), new PropertyMetadata(Enumerable.Empty<string>(), OnResolutionsChanged));

    public string SelectedResolution
    {
        get { return (string)GetValue(SelectedResolutionProperty); }
        set { SetValue(SelectedResolutionProperty, value); }
    }

    public static readonly DependencyProperty SelectedResolutionProperty =
        DependencyProperty.Register("SelectedResolution", typeof(string), typeof(VlcMediaTransportControls), new PropertyMetadata("", OnSelectedResolutionChanged));


    /// <summary>
    /// Identifies the <see cref="ShowAndHideAutomatically"/> dependency property
    /// </summary>
    public static readonly DependencyProperty ShowAndHideAutomaticallyProperty = DependencyProperty.Register(nameof(ShowAndHideAutomatically),
        typeof(bool), typeof(VlcMediaTransportControls),
        new PropertyMetadata(true, (d, args) => ((VlcMediaTransportControls)d).OnShowAndHideAutomaticallyPropertyChanged()));
    /// <summary>
    /// Gets or sets a value that indicates whether the controls are shown and hidden automatically
    /// </summary>
    public bool ShowAndHideAutomatically
    {
        get => (bool)GetValue(ShowAndHideAutomaticallyProperty);
        set => SetValue(ShowAndHideAutomaticallyProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="KeepDeviceAwake"/> dependency property
    /// </summary>
    public static readonly DependencyProperty KeepDeviceAwakeProperty = DependencyProperty.Register(nameof(KeepDeviceAwake), typeof(bool),
        typeof(VlcMediaTransportControls),
        new PropertyMetadata(true, (d, args) => ((VlcMediaTransportControls)d).OnKeepDeviceAwakePropertyChanged()));
    /// <summary>
    /// Gets or sets a value indicating whether the device should be kept awake
    /// </summary>
    public bool KeepDeviceAwake
    {
        get => (bool)GetValue(KeepDeviceAwakeProperty);
        set => SetValue(KeepDeviceAwakeProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsCompact"/> dependency property
    /// </summary>
    public static readonly DependencyProperty IsCompactProperty = DependencyProperty.Register(nameof(IsCompact), typeof(bool),
        typeof(VlcMediaTransportControls),
        new PropertyMetadata(false, (d, args) => ((VlcMediaTransportControls)d).IsCompactCommand?.Update()));
    /// <summary>
    /// Gets or sets a value that indicates whether playback controls are shown on one row instead of two
    /// </summary>
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="CCSelectionButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty CCSelectionButtonContentProperty = DependencyProperty.Register(nameof(CCSelectionButtonContent),
        typeof(object), typeof(VlcMediaTransportControls),
        new PropertyMetadata(new FontIcon()));
    /// <summary>
    /// Gets or sets the closed captions selection button content
    /// </summary>
    public object CCSelectionButtonContent
    {
        get => GetValue(CCSelectionButtonContentProperty);
        set => SetValue(CCSelectionButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="AudioTracksSelectionButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty AudioTracksSelectionButtonContentProperty = DependencyProperty.Register(
        nameof(AudioTracksSelectionButtonContent), typeof(object), typeof(VlcMediaTransportControls),
        new PropertyMetadata(new SymbolIcon(Symbol.ClosedCaption)));
    /// <summary>
    /// Gets or sets the audio tracks selection button content
    /// </summary>
    public object AudioTracksSelectionButtonContent
    {
        get => GetValue(AudioTracksSelectionButtonContentProperty);
        set => SetValue(AudioTracksSelectionButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsPlayPauseButtonVisible"/> dependency property
    /// </summary>
    public static readonly DependencyProperty IsPlayPauseButtonVisibleProperty = DependencyProperty.Register(nameof(IsPlayPauseButtonVisible),
        typeof(bool), typeof(VlcMediaTransportControls),
        new PropertyMetadata(true, (d, args) => ((VlcMediaTransportControls)d).PlayPauseAvailabilityCommand?.Update()));
    /// <summary>
    /// Gets or sets a value indicating whether the play/pause button is shown
    /// </summary>
    public bool IsPlayPauseButtonVisible
    {
        get => (bool)GetValue(IsPlayPauseButtonVisibleProperty);
        set => SetValue(IsPlayPauseButtonVisibleProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="PlayButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty PlayButtonContentProperty = DependencyProperty.Register(nameof(PlayButtonContent), typeof(object),
        typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Play)));
    /// <summary>
    /// Gets or sets the play button content
    /// </summary>
    public object PlayButtonContent
    {
        get => GetValue(PlayButtonContentProperty);
        set => SetValue(PlayButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="PlayButtonOnLeftContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty PlayButtonOnLeftContentProperty = DependencyProperty.Register(nameof(PlayButtonOnLeftContent),
        typeof(object), typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Play)));
    /// <summary>
    /// Gets or sets the play button on left content
    /// </summary>
    public object PlayButtonOnLeftContent
    {
        get => GetValue(PlayButtonOnLeftContentProperty);
        set => SetValue(PlayButtonOnLeftContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="PauseButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty PauseButtonContentProperty = DependencyProperty.Register(nameof(PauseButtonContent), typeof(object),
        typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Pause)));
    /// <summary>
    /// Gets or sets the pause button content
    /// </summary>
    public object PauseButtonContent
    {
        get => GetValue(PauseButtonContentProperty);
        set => SetValue(PauseButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="PauseButtonOnLeftContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty PauseButtonOnLeftContentProperty = DependencyProperty.Register(nameof(PauseButtonOnLeftContent),
        typeof(object), typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Pause)));
    /// <summary>
    /// Gets or sets the pause button on left content
    /// </summary>
    public object PauseButtonOnLeftContent
    {
        get => GetValue(PauseButtonOnLeftContentProperty);
        set => SetValue(PauseButtonOnLeftContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsStopButtonVisible"/> dependency property.
    /// </summary>
    public static DependencyProperty IsStopButtonVisibleProperty { get; } = DependencyProperty.Register(nameof(IsStopButtonVisible), typeof(bool),
        typeof(VlcMediaTransportControls), new PropertyMetadata(false, (d, e) => ((VlcMediaTransportControls)d).StopButtonAvailabilityCommand?.Update()));
    /// <summary>
    /// Gets or sets a value indicating whether the stop button is shown.
    /// </summary>
    public bool IsStopButtonVisible
    {
        get => (bool)GetValue(IsStopButtonVisibleProperty);
        set => SetValue(IsStopButtonVisibleProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsStopEnabled"/> dependency property.
    /// </summary>
    public static DependencyProperty IsStopEnabledProperty { get; } = DependencyProperty.Register(nameof(IsStopEnabled), typeof(bool),
        typeof(VlcMediaTransportControls), new PropertyMetadata(true, (d, e) => ((VlcMediaTransportControls)d).StopButtonAvailabilityCommand?.Update()));
    /// <summary>
    /// Gets or sets a value indicating whether a user can stop the media playback.
    /// </summary>
    public bool IsStopEnabled
    {
        get => (bool)GetValue(IsStopEnabledProperty);
        set => SetValue(IsStopEnabledProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="StopButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty StopButtonContentProperty = DependencyProperty.Register(nameof(StopButtonContent), typeof(object),
        typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Stop)));
    /// <summary>
    /// Gets or sets the font icon for stop
    /// </summary>
    public object StopButtonContent
    {
        get => (FontIcon)GetValue(StopButtonContentProperty);
        set => SetValue(StopButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsVolumeButtonVisible"/> dependency property.
    /// </summary>
    public static DependencyProperty IsVolumeButtonVisibleProperty { get; } = DependencyProperty.Register(nameof(IsVolumeButtonVisible),
        typeof(bool), typeof(VlcMediaTransportControls),
        new PropertyMetadata(true, (d, e) => ((VlcMediaTransportControls)d).VolumeMuteButtonAvailabilityCommand?.Update()));
    /// <summary>
    /// Gets or sets a value indicating whether the stop button is shown.
    /// </summary>
    public bool IsVolumeButtonVisible
    {
        get => (bool)GetValue(IsVolumeButtonVisibleProperty);
        set => SetValue(IsVolumeButtonVisibleProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsVolumeEnabled"/> dependency property.
    /// </summary>
    public static DependencyProperty IsVolumeEnabledProperty { get; } = DependencyProperty.Register(nameof(IsVolumeEnabled), typeof(bool),
        typeof(VlcMediaTransportControls),
        new PropertyMetadata(true, (d, e) => ((VlcMediaTransportControls)d).VolumeMuteButtonAvailabilityCommand?.Update()));
    /// <summary>
    /// Gets or sets a value indicating whether a user can stop the media playback.
    /// </summary>
    public bool IsVolumeEnabled
    {
        get => (bool)GetValue(IsVolumeEnabledProperty);
        set => SetValue(IsVolumeEnabledProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="VolumeButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty VolumeButtonContentProperty = DependencyProperty.Register(nameof(VolumeButtonContent),
        typeof(object), typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Volume)));
    /// <summary>
    /// Gets or sets the volume button content
    /// </summary>
    public object VolumeButtonContent
    {
        get => GetValue(VolumeButtonContentProperty);
        set => SetValue(VolumeButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="AudioButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty AudioButtonContentProperty = DependencyProperty.Register(nameof(AudioButtonContent), typeof(object),
        typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Volume)));
    /// <summary>
    /// Gets or sets the audio button content
    /// </summary>
    public object AudioButtonContent
    {
        get => GetValue(AudioButtonContentProperty);
        set => SetValue(AudioButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="VolumeMuteButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty VolumeMuteButtonContentProperty = DependencyProperty.Register(nameof(VolumeMuteButtonContent),
        typeof(object), typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Mute)));
    /// <summary>
    /// Gets or sets the volume mute button content
    /// </summary>
    public object VolumeMuteButtonContent
    {
        get => GetValue(VolumeMuteButtonContentProperty);
        set => SetValue(VolumeMuteButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="AudioMuteButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty AudioMuteButtonContentProperty = DependencyProperty.Register(nameof(AudioMuteButtonContent),
        typeof(object), typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Mute)));
    /// <summary>
    /// Gets or sets the audio mute button content
    /// </summary>
    public object AudioMuteButtonContent
    {
        get => GetValue(AudioMuteButtonContentProperty);
        set => SetValue(AudioMuteButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsZoomButtonVisible"/> dependency property
    /// </summary>
    public static DependencyProperty IsZoomButtonVisibleProperty { get; } = DependencyProperty.Register(nameof(IsZoomButtonVisible), typeof(bool),
        typeof(VlcMediaTransportControls), new PropertyMetadata(true, (d, e) => ((VlcMediaTransportControls)d).ZoomButtonAvailabilityCommand?.Update()));
    /// <summary>
    /// Gets or sets a value indicating whether the zoom button is shown
    /// </summary>
    public bool IsZoomButtonVisible
    {
        get => (bool)GetValue(IsZoomButtonVisibleProperty);
        set => SetValue(IsZoomButtonVisibleProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsZoomEnabled"/> dependency property
    /// </summary>
    public static DependencyProperty IsZoomEnabledProperty { get; } = DependencyProperty.Register(nameof(IsZoomEnabled), typeof(bool),
        typeof(VlcMediaTransportControls), new PropertyMetadata(true, (d, e) => ((VlcMediaTransportControls)d).ZoomButtonAvailabilityCommand?.Update()));
    /// <summary>
    /// Gets or sets a value indicating whether a user can zoom the media
    /// </summary>
    public bool IsZoomEnabled
    {
        get => (bool)GetValue(IsZoomEnabledProperty);
        set => SetValue(IsZoomEnabledProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ZoomButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty ZoomButtonContentProperty = DependencyProperty.Register(nameof(ZoomButtonContent), typeof(object),
        typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Zoom)));
    /// <summary>
    /// Gets or sets the aspect ratio button content
    /// </summary>
    public object ZoomButtonContent
    {
        get => GetValue(ZoomButtonContentProperty);
        set => SetValue(ZoomButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsCastButtonVisible"/> dependency property
    /// </summary>
    public static DependencyProperty IsCastButtonVisibleProperty { get; } = DependencyProperty.Register(nameof(IsCastButtonVisible), typeof(bool),
        typeof(VlcMediaTransportControls), new PropertyMetadata(false,
            (d, e) => ((VlcMediaTransportControls)d).Manager.Get<CastRenderersDiscoverer>().Enabled = (bool)e.NewValue));
    /// <summary>
    /// Gets or sets a value indicating whether the cast button is shown
    /// </summary>
    public bool IsCastButtonVisible
    {
        get => (bool)GetValue(IsCastButtonVisibleProperty);
        set => SetValue(IsCastButtonVisibleProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsCastEnabled"/> dependency property
    /// </summary>
    public static DependencyProperty IsCastEnabledProperty { get; } = DependencyProperty.Register(nameof(IsCastEnabledProperty), typeof(bool),
        typeof(VlcMediaTransportControls), new PropertyMetadata(true, (d, e) => ((VlcMediaTransportControls)d).CastButtonAvailabilityCommand?.Update()));
    /// <summary>
    /// Gets or sets a value indicating whether a user can cast
    /// </summary>
    public bool IsCastEnabled
    {
        get => (bool)GetValue(IsCastEnabledProperty);
        set => SetValue(IsCastEnabledProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="CastButtonContent"/> dependency property
    /// </summary>
    public static readonly DependencyProperty CastButtonContentProperty = DependencyProperty.Register(nameof(CastButtonContent), typeof(object),
        typeof(VlcMediaTransportControls), new PropertyMetadata(new SymbolIcon(Symbol.Placeholder)));
    /// <summary>
    /// Gets or sets the cast button content
    /// </summary>
    public object CastButtonContent
    {
        get => GetValue(CastButtonContentProperty);
        set => SetValue(CastButtonContentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsSeekBarVisible"/> dependency property
    /// </summary>
    public static DependencyProperty IsSeekBarVisibleProperty { get; } = DependencyProperty.Register(nameof(IsSeekBarVisible), typeof(bool),
        typeof(VlcMediaTransportControls), new PropertyMetadata(true, (d, e) => ((VlcMediaTransportControls)d).SeekBarAvailabilityCommand?.Update()));
    /// <summary>
    /// Gets or sets a value indicating whether the seek bar is shown
    /// </summary>
    public bool IsSeekBarVisible
    {
        get => (bool)GetValue(IsSeekBarVisibleProperty);
        set => SetValue(IsSeekBarVisibleProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsSeekBarEnabled"/> dependency property
    /// </summary>
    public static DependencyProperty IsSeekBarEnabledProperty { get; } = DependencyProperty.Register(nameof(IsSeekBarEnabled), typeof(bool),
        typeof(VlcMediaTransportControls), new PropertyMetadata(true, (d, e) => ((VlcMediaTransportControls)d).SeekBarAvailabilityCommand?.Update()));
    /// <summary>
    /// Gets or sets a value indicating whether a user can use the seek bar to find a location in the media
    /// </summary>
    public bool IsSeekBarEnabled
    {
        get => (bool)GetValue(IsSeekBarEnabledProperty);
        set => SetValue(IsSeekBarEnabledProperty, value);
    }

    public static readonly DependencyProperty IsCCSelectionVisibleProperty =
        DependencyProperty.Register("IsCCSelectionVisible", typeof(bool), typeof(VlcMediaTransportControls), new PropertyMetadata(false));

    public bool IsCCSelectionVisible
    {
        get { return (bool)GetValue(IsCCSelectionVisibleProperty); }
        set { SetValue(IsCCSelectionVisibleProperty, value); }
    }


    public bool IsInitialized { get; set; }

    public void Initialize(LibVLC vlc, MediaPlayer mediaPlayer, VideoView videoView)
    {
        Manager.LibVLC = LibVLC = vlc;
        Manager.MediaPlayer = MediaPlayer = mediaPlayer;
        Manager.VideoView = VideoView = videoView;

        IsInitialized = true;
    }

    public void SetDynamicSkipButton(ButtonBase button)
    {
        DynamicSkipButton = button;
        DynamicSkipButton.Click += (_, _) => _onDynamicSkipIntro.OnNext(Unit.Default);
    }

    public bool IsAddCCButtonVisibile
    {
        get { return (bool)GetValue(IsAddCCButtonVisibileProperty); }
        set { SetValue(IsAddCCButtonVisibileProperty, value); }
    }



    public static readonly DependencyProperty IsAddCCButtonVisibileProperty =
    DependencyProperty.Register("IsAddCCButtonVisibile", typeof(bool), typeof(VlcMediaTransportControls), new PropertyMetadata(false));

    /// <summary>
    /// Invoked whenever application code or internal processes (such as a rebuilding layout pass) call ApplyTemplate. 
    /// In simplest terms, this means the method is called just before a UI element displays in your app.
    /// Override this method to influence the default post-template logic of a class.
    /// </summary>
    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        ErrorTextBlock = GetTemplateChild(nameof(ErrorTextBlock)) as TextBlock;

        TimeElapsedElement = GetTemplateChild(nameof(TimeElapsedElement)) as TextBlock;
        TimeRemainingElement = GetTemplateChild(nameof(TimeRemainingElement)) as TextBlock;

        VolumeSlider = GetTemplateChild(nameof(VolumeSlider)) as Slider;
        if (VolumeSlider != null)
        {
            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;
        }
        if (GetTemplateChild("VolumeFlyout") is Flyout volumeFlyout)
        {
            volumeFlyout.Opening += (sender, e) => UpdateVolumeSlider();
            SubscribeFlyoutOpenedClosedEvents(volumeFlyout);
        }
        var audioTracksSelectionButton = Initialize("AudioTracksSelectionButton", "ShowAudioSelectionMenu") as Button;
        var ccSelectionButton = Initialize("CCSelectionButton", "ShowClosedCaptionMenu") as Button;
        AddTracksMenu(audioTracksSelectionButton, Manager.Get<AudioTracksManager>(), AudioSelectionAvailableState, AudioSelectionUnavailableState);
        AddTracksMenu(ccSelectionButton, Manager.Get<SubtitlesTracksManager>(), CCSelectionAvailableState, CCSelectionUnavailableState, true);

        PlayPauseAvailabilityCommand = Initialize(() => IsPlayPauseButtonVisible && Manager.Get<StateManager>().PlayPauseAvailable,
            PlayPauseAvailableState, PlayPauseUnavailableState, "PlayPauseButton", null, "Play", PlayPauseButton_Click);
        PlayPauseButton = PlayPauseAvailabilityCommand!.Control;
        PlayPauseButtonOnLeft = Initialize(nameof(PlayPauseButtonOnLeft), "Play", PlayPauseButton_Click);
        IsBufferingCommand = Initialize(() => Manager.Get<BufferingProgressNotifier>().IsBuffering, BufferingState, NormalState);
        IsCompactCommand = Initialize(() => IsCompact, CompactModeState, NormalModeState);
        CastButtonAvailabilityCommand = Initialize(() => Manager.Get<CastRenderersDiscoverer>().CastAvailable, CastAvailableState,
            CastUnavailableState, "CastButton", () => IsCastEnabled, "ShowCastMenu", CastButton_Click);
        StopButtonAvailabilityCommand = Initialize(() => IsStopButtonVisible, StopAvailableState, StopUnavailableState, "StopButton",
            () => IsStopEnabled && Manager.Get<StateManager>().IsPlayingOrPaused, "Stop", (sender, e) => Manager.Get<StateManager>().Stop());
        Initialize("AudioMuteButton", "Mute", (sender, e) => MediaPlayer?.ToggleMute());
        VolumeMuteButtonAvailabilityCommand = Initialize(() => IsVolumeButtonVisible, VolumeAvailableState, VolumeUnavailableState,
            "VolumeMuteButton", () => IsVolumeEnabled && Manager.Get<VolumeManager>().Enabled, "ShowVolumeMenu");
        MuteStateCommand = Initialize(() => Manager.Get<VolumeManager>().Mute, MuteState, VolumeState);
        ZoomButtonAvailabilityCommand = Initialize(() => IsZoomButtonVisible, ZoomAvailableState, ZoomUnavailableState, "ZoomButton",
            () => IsZoomEnabled, "AspectRatio");
        AddAspectRatioMenu(ZoomButtonAvailabilityCommand!.Control as Button);
        SeekBarAvailabilityCommand = Initialize(() => IsSeekBarVisible, SeekBarAvailableState, SeekBarUnavailableState, "ProgressSlider",
            () => IsSeekBarEnabled && Manager.Get<SeekBarManager>().Seekable);
        ProgressSlider = SeekBarAvailabilityCommand!.Control as Slider;
        if (ProgressSlider != null)
        {
            ProgressSlider.Minimum = 0;
            ProgressSlider.Maximum = 100;
            Manager.Get<SeekBarManager>().SeekBarMaximum = 100;
            ProgressSlider.ValueChanged += ProgressSlider_ValueChanged;
        }

        VisualStateManager.GoToState(this, Manager.Get<StateManager>().IsPlaying ? PauseState : PlayState, true);

        Initialize("SubmitTimeStampsButton", "Submit Timestamps", (_, _) => _onSubmitTimeStamp.OnNext(Unit.Default));
        PreviousTrackButton = Initialize("PreviousTrackButton", "Previous Episode", (_, _) => _onPrevTrack.OnNext(Unit.Default)) as ButtonBase;
        NextTrackButton = Initialize("NextTrackButton", "Next Episode", (_, _) => _onNextTrack.OnNext(Unit.Default)) as ButtonBase;
        Initialize("SkipForwardButton", "Skip forward", (_, _) => MediaPlayer.Time += 30000);
        Initialize("SkipBackwardButton", "skip backward", (_, _) => MediaPlayer.Time -= 10000);
        Initialize("SkipIntroButton", "Skip intro", (_, _) => _onSkipIntro.OnNext(Unit.Default));
        Initialize("FullWindowButton", "", (_, _) => App.GetService<IWindowService>().ToggleIsFullWindow());
        AddCCButton = Initialize("AddCCButton", "Add Subtitle", (_, _) => _onAddCc.OnNext(Unit.Default)) as ButtonBase;
        FullWindowSymbol = GetTemplateChild("FullWindowSymbol") as SymbolIcon;
        QualitiesButton = GetTemplateChild("QualitiesButton") as AppBarButton;
        QualitiesButton.Flyout = _qualitiesFlyout;

        var skipTime = App.GetService<ISettings>().SmallSkipAmount;
        Initialize("SmallSkipForward", "", (_, _) => MediaPlayer.Time += skipTime * 1000);
        Initialize("SmallSkipBackward", "", (_, _) => MediaPlayer.Time -= skipTime * 1000);

        UpdateVolumeSlider();
        UpdateTime();
    }

    private DependencyObject Initialize(string controlName, string toolTip = null, RoutedEventHandler clickEventHandler = null)
    {
        if (controlName == null)
        {
            return null;
        }
        var control = GetTemplateChild(controlName);
        if (clickEventHandler != null && control is ButtonBase button)
        {
            button.Click += clickEventHandler;
        }
        SetToolTip(control, toolTip);
        return control;
    }

    private AvailabilityCommand Initialize(Func<bool> isAvailable, string availableState, string unavailableState = null,
        string controlName = null, Func<bool> isEnabled = null, string toolTip = null, RoutedEventHandler clickEventHandler = null)
    {
        var command = new AvailabilityCommand(this, isAvailable, availableState, unavailableState,
            Initialize(controlName, toolTip, clickEventHandler) as Control, isEnabled);
        command.Update();
        return command;
    }

    /// <summary>
    /// Sets the value of the <see cref="ToolTipService.ToolTipProperty"/> for an object
    /// </summary>
    /// <param name="element">the object to which the attached property is written</param>
    /// <param name="resource">resource string</param>
    /// <param name="args">an object array that contains zero or more objects to format</param>
    protected void SetToolTip(DependencyObject element, string resource, params string[] args) { }

    private void SubscribeFlyoutOpenedClosedEvents(FlyoutBase flyout)
    {
        flyout.Opened += Flyout_Opened;
        flyout.Closed += Flyout_Closed;
    }

    private MenuFlyout CreateMenuFlyout()
    {
        var menuFlyout = new MenuFlyout();
        SubscribeFlyoutOpenedClosedEvents(menuFlyout);
        return menuFlyout;
    }

    private void AddAspectRatioMenu(Button zoomButton)
    {
        if (zoomButton != null)
        {
            var menuFlyout = CreateMenuFlyout();
            ZoomMenu = menuFlyout;
            zoomButton.Flyout = menuFlyout;
            var menuItems = menuFlyout.Items;
            var mediaPlayer = MediaPlayer;
            var currentAspectRatio = Manager.Get<AspectRatioManager>().AspectRatio;
            foreach (AspectRatio aspectRatio in Enum.GetValues(typeof(AspectRatio)))
            {
                var menuItem = new ToggleMenuFlyoutItem()
                {
                    Text = $"{nameof(AspectRatio)}{aspectRatio}",
                    IsChecked = aspectRatio == currentAspectRatio
                };
                menuItem.Command = new ActionCommand<AspectRatio>(AspectRatioMenuItemClick);
                menuItem.CommandParameter = aspectRatio;
                menuItems.Add(menuItem);
            }
        }
    }

    private void AspectRatioMenuItemClick(AspectRatio aspectRatio)
    {
        var aspectRatioManager = Manager.Get<AspectRatioManager>();
        var currentAspectRatio = aspectRatioManager.AspectRatio;
        aspectRatioManager.AspectRatio = aspectRatio;
        if (currentAspectRatio == aspectRatio)
        {
            // To prevent the menu item from being unchecked
            UpdateZoomMenu(aspectRatio);
        }
    }

    private void UpdateZoomMenu(AspectRatio aspectRatio)
    {
        if (ZoomMenu != null)
        {
            CheckMenuItem(ZoomMenu, ZoomMenu.Items.OfType<ToggleMenuFlyoutItem>().First(i => (AspectRatio)i.CommandParameter == aspectRatio));
        }
    }

    private void AspectRatioChanged(object sender, EventArgs e)
    {
        UpdateZoomMenu(((AspectRatioManager)sender).AspectRatio);
    }

    private void Flyout_Opened(object sender, object e)
    {
        Manager.Get<AutoHideNotifier>().Enabled = false;
    }

    private void Flyout_Closed(object sender, object e)
    {
        OnShowAndHideAutomaticallyPropertyChanged();
    }

    private void OnPointerMoved(object sender, RoutedEventArgs e)
    {
        Manager.Get<AutoHideNotifier>().Show(true);
    }

    private void OnShowAndHideAutomaticallyPropertyChanged()
    {
        Manager.Get<AutoHideNotifier>().Enabled = ShowAndHideAutomatically;
    }

    /// <summary>
    /// Shows the tranport controls if they're hidden
    /// </summary>
    public void Show()
    {
        Manager.Get<AutoHideNotifier>().Show();
    }

    /// <summary>
    /// Hides the playback controls if they're shown
    /// </summary>
    public void Hide()
    {
        Manager.Get<AutoHideNotifier>().Hide();
    }

    private void OnKeepDeviceAwakePropertyChanged()
    {
        Manager.Get<DeviceAwakeningManager>().KeepDeviceAwake = KeepDeviceAwake;
    }

    private void AddTracksMenu(Button trackButton, TracksManager manager, string availableStateName, string unavailableStateName,
        bool addNoneItem = false)
    {
        if (trackButton != null)
        {
            var menuFlyout = CreateMenuFlyout();
            trackButton.Flyout = menuFlyout;
            var tracksMenu = new TracksMenu(manager, availableStateName, unavailableStateName, addNoneItem);
            TracksMenus.Add(menuFlyout, tracksMenu);
            if (addNoneItem)
            {
                AddNoneItem(menuFlyout);
            }
        }
    }

    private void AddNoneItem(MenuFlyout menuflyout)
    {
        AddTrack(menuflyout, null, "None");
    }

    private void AddTrack(MenuFlyout menuflyout, int? trackId, string trackName)
    {
        if (menuflyout == null)
        {
            return;
        }

        var menuItems = menuflyout.Items;
        var toggleMenuFlyoutItems = menuItems.OfType<ToggleMenuFlyoutItem>();
        if (trackId == null && toggleMenuFlyoutItems.Any(i => i.CommandParameter == null) ||
            trackId != null && toggleMenuFlyoutItems.Any(i => trackId.Equals(i.CommandParameter)))
        {
            return;
        }

        var menuItem = new ToggleMenuFlyoutItem() { Text = trackName };
        menuItem.Command = new ActionCommand<int?>(TrackMenuItemClick);
        menuItem.CommandParameter = trackId;
        menuItems.Add(menuItem);

        if (menuItems.Count == 2)
        {
            toggleMenuFlyoutItems.FirstOrDefault(i => i.CommandParameter != null).IsChecked = true;
            VisualStateManager.GoToState(this, TracksMenus[menuflyout].AvailableStateName, true);
        }
    }

    private void CheckMenuItem(MenuFlyout menuFlyout, ToggleMenuFlyoutItem menuItem)
    {
        foreach (var item in menuFlyout.Items.OfType<ToggleMenuFlyoutItem>())
        {
            item.IsChecked = item == menuItem;
        }
    }

    private void TrackMenuItemClick(int? trackId)
    {
        var menu = TracksMenus.SelectMany(kvp => kvp.Key.Items.OfType<ToggleMenuFlyoutItem>().Where(i => (int?)i.CommandParameter == trackId)
            .Select(i => new { Menu = kvp, MenuItem = i })).FirstOrDefault();
        if (menu != null)
        {
            CheckMenuItem(menu.Menu.Key, menu.MenuItem);
            menu.Menu.Value.Manager.CurrentTrackId = trackId ?? -1;
        }
    }

    private KeyValuePair<MenuFlyout, TracksMenu> GetTracksMenu(object sender)
    {
        return TracksMenus.First(kvp => kvp.Value.Manager == sender);
    }

    private void OnTracksCleared(object sender, EventArgs e)
    {
        var manager = (TracksManager)sender;
        var tracksMenukeyValuePair = GetTracksMenu(manager);
        var menuFlyout = tracksMenukeyValuePair.Key;
        var tracksMenu = tracksMenukeyValuePair.Value;
        menuFlyout.Items.Clear();
        if (tracksMenu.HasNoneItem)
        {
            AddNoneItem(menuFlyout);
        }
        VisualStateManager.GoToState(this, tracksMenu.UnavailableStateName, true);

        var tracks = manager.Tracks;
        if (tracks != null)
        {
            foreach (var track in tracks)
            {
                AddTrack(menuFlyout, track);
            }
        }
    }

    private void AddTrack(MenuFlyout menuFlyout, TrackDescription? trackDescription)
    {
        if (trackDescription is TrackDescription td && !string.IsNullOrWhiteSpace(td.Name))
        {
            AddTrack(menuFlyout, trackDescription?.Id, td.Name!);
        }
    }

    private void OnTrackSelected(object sender, MediaPlayerESSelectedEventArgs e)
    {
        var menuFlyout = GetTracksMenu(sender).Key;
        var id = e.Id;
        CheckMenuItem(menuFlyout, menuFlyout.Items.OfType<ToggleMenuFlyoutItem>().FirstOrDefault(mi =>
        {
            var trackId = (int?)mi.CommandParameter;
            return id == -1 && trackId == null || id.Equals(trackId);
        }));
    }

    private void OnTrackAdded(object sender, MediaPlayerESAddedEventArgs e)
    {
        var manager = (TracksManager)sender;
        AddTrack(GetTracksMenu(manager).Key, manager.GetTrackDescription(e.Id));
    }

    private void OnTrackDeleted(object sender, MediaPlayerESDeletedEventArgs e)
    {
        var tracksMenu = GetTracksMenu(sender);
        var menuFlyout = tracksMenu.Key;
        if (menuFlyout == null)
        {
            return;
        }

        var menuItems = menuFlyout.Items;
        var menuItem = menuItems.OfType<ToggleMenuFlyoutItem>().FirstOrDefault(mi => e.Id.Equals(mi.CommandParameter));
        if (menuItem != null)
        {
            var isChecked = menuItem is ToggleMenuFlyoutItem toggleMenuFlyoutItem && toggleMenuFlyoutItem.IsChecked;
            menuItems.Remove(menuItem);
            if (isChecked && menuItems.FirstOrDefault() is ToggleMenuFlyoutItem firstMenuItem)
            {
                firstMenuItem.IsChecked = true;
            }
            if (menuItems.Count < 2)
            {
                VisualStateManager.GoToState(this, tracksMenu.Value.UnavailableStateName, true);
            }
        }
    }

    private void ProgressSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        Manager.Get<SeekBarManager>().SetSeekBarPosition(e.NewValue);
    }

    private void UpdateTime()
    {
        var position = Manager.Get<SeekBarManager>().Position;
        if (ProgressSlider != null)
        {
            ProgressSlider.ValueChanged -= ProgressSlider_ValueChanged;
            try
            {
                ProgressSlider.Value = position.SeekBarPosition;
            }
            finally
            {
                ProgressSlider.ValueChanged += ProgressSlider_ValueChanged;
            }
        }
        if (TimeRemainingElement != null)
        {
            TimeRemainingElement.Text = position.RemainingTimeText;
        }
        if (TimeElapsedElement != null)
        {
            TimeElapsedElement.Text = position.ElapsedTimeText;
        }
    }

    private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
    {
        Manager.Get<StateManager>().TogglePause();
    }

    private void AddCastMenuItem(ICollection<MenuFlyoutItemBase> items, string name, bool disconnectItem = false)
    {
        var menuItem = new MenuFlyoutItem() { Text = name };
        menuItem.Command = new ActionCommand<string>(CastMenuItemClick);
        menuItem.CommandParameter = disconnectItem ? null : name;
        items.Add(menuItem);
    }

    private void CastButton_Click(object sender, RoutedEventArgs e)
    {
        var castMenu = CreateMenuFlyout();
        var items = castMenu.Items;
        var castRenderersDiscoverer = Manager.Get<CastRenderersDiscoverer>();
        var mediaPlayer = MediaPlayer;
        foreach (var renderer in castRenderersDiscoverer.Renderers.OrderBy(r => r.Name))
        {
            AddCastMenuItem(items, renderer.Name);
        }
        AddCastMenuItem(items, "Disconnect", true);
        ((Button)sender).Flyout = castMenu;
    }

    private void CastMenuItemClick(string rendererName)
    {
        if (rendererName == null)
        {
            MediaPlayer?.SetRenderer(null);
        }
        else
        {
            var castRenderersDiscoverer = Manager.Get<CastRenderersDiscoverer>();
            var rendererItem = castRenderersDiscoverer.Renderers.FirstOrDefault(r => r.Name == rendererName);
            if (rendererItem != null)
            {
                MediaPlayer?.SetRenderer(rendererItem);
            }
        }
    }

    private void OnPlaying()
    {
        VisualStateManager.GoToState(this, NormalState, true);
        UpdatePlayPauseState("Pause", PauseState);
    }

    private void OnPaused()
    {
        VisualStateManager.GoToState(this, NormalState, true);
        GoToPauseState();
    }

    private void GoToPauseState()
    {
        UpdatePlayPauseState("Play", PlayState);
    }

    private void UpdatePlayPauseState(string tooltip, string state)
    {
        SetToolTip(PlayPauseButton, tooltip);
        SetToolTip(PlayPauseButtonOnLeft, tooltip);
        VisualStateManager.GoToState(this, state, true);
        StopButtonAvailabilityCommand?.Update();
    }

    private void OnStopped()
    {
        GoToPauseState();
    }

    private void UpdateVolumeSlider()
    {
        var volumeSlider = VolumeSlider;
        if (volumeSlider != null)
        {
            volumeSlider.Value = Manager.Get<VolumeManager>().Volume;
        }
    }

    private void ShowError()
    {
        var errorTextBlock = ErrorTextBlock;
        if (errorTextBlock != null)
        {
            errorTextBlock.Text = "Error";
            VisualStateManager.GoToState(this, ErrorState, true);
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        Manager.Get<VolumeManager>().Volume = (int)e.NewValue;
    }

    private static void OnResolutionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var mtc = d as VlcMediaTransportControls;
        var flyout = mtc._qualitiesFlyout;
        foreach (var item in flyout.Items.OfType<MenuFlyoutItem>())
        {
            item.Click -= mtc.FlyoutItem_Click;
        }
        flyout.Items.Clear();

        if (e.NewValue is IEnumerable<string> values && mtc.QualitiesButton is { } btn)
        {
            var qualities = values.ToList();
            if (qualities.Count == 1)
            {
                btn.Visibility = Visibility.Collapsed;
            }
            else if (qualities.Count > 1)
            {
                btn.Visibility = Visibility.Visible;
                foreach (var item in qualities)
                {
                    var flyoutItem = new ToggleMenuFlyoutItem { Text = item };
                    flyoutItem.Click += mtc.FlyoutItem_Click;
                    flyout.Items.Add(flyoutItem);
                }
            }
        }
    }

    private void FlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        _onQualityChanged.OnNext((sender as MenuFlyoutItem).Text);
    }

    private static void OnSelectedResolutionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not string s)
        {
            return;
        }

        if (string.IsNullOrEmpty(s))
        {
            return;
        }

        var mtc = d as VlcMediaTransportControls;
        foreach (var item in mtc._qualitiesFlyout.Items.OfType<ToggleMenuFlyoutItem>())
        {
            item.IsChecked = item.Text == s;
        }
    }
}

