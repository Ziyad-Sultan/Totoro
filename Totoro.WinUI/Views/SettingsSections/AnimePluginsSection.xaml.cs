// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Views.SettingsSections;

public sealed partial class AnimePluginsSection : Page
{
    public SettingsViewModel ViewModel
    {
        get { return (SettingsViewModel)GetValue(ViewModelProperty); }
        set { SetValue(ViewModelProperty, value); }
    }

    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register("ViewModel", typeof(SettingsViewModel), typeof(AnimePluginsSection), new PropertyMetadata(null));


    public ICommand UpdateOfflineDb { get; }

    public AnimePluginsSection()
    {
        InitializeComponent();

        UpdateOfflineDb = ReactiveCommand.CreateFromTask(() => App.GetService<IOfflineAnimeIdService>().UpdateOfflineMappings());
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        ViewModel = e.Parameter as SettingsViewModel;
    }
}
