﻿using Microsoft.UI.Xaml.Controls;
using Totoro.Core.ViewModels;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Services;

public class NavigationViewService : INavigationViewService
{
    private readonly INavigationService _navigationService;
    private NavigationView _navigationView;

    public IList<object> MenuItems => _navigationView.MenuItems;

    public object SettingsItem => _navigationView.SettingsItem;

    public NavigationViewService(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public void Initialize(NavigationView navigationView)
    {
        _navigationView = navigationView;
        _navigationView.BackRequested += OnBackRequested;
        _navigationView.ItemInvoked += OnItemInvoked;
    }

    public void UnregisterEvents()
    {
        _navigationView.BackRequested -= OnBackRequested;
        _navigationView.ItemInvoked -= OnItemInvoked;
    }

    public NavigationViewItem GetSelectedItem(Type pageType) => GetSelectedItem(_navigationView.MenuItems, pageType);

    private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args) => _navigationService.GoBack();

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            _navigationService.NavigateTo<SettingsViewModel>();
        }
        else
        {
            var selectedItem = args.InvokedItemContainer as NavigationViewItem;

            if (selectedItem.GetValue(NavigationHelper.NavigateToProperty) is string typeKey)
            {
                _navigationService.NavigateTo(Type.GetType($"{typeKey},Totoro.Core") ?? Type.GetType($"{typeKey},Totoro.WinUI"));
            }
        }
    }

    private static NavigationViewItem GetSelectedItem(IEnumerable<object> menuItems, Type vmType)
    {
        foreach (var item in menuItems.OfType<NavigationViewItem>())
        {
            if (IsMenuItemForPageType(item, vmType))
            {
                return item;
            }

            var selectedChild = GetSelectedItem(item.MenuItems, vmType);
            if (selectedChild != null)
            {
                return selectedChild;
            }
        }

        return null;
    }

    private static bool IsMenuItemForPageType(NavigationViewItem menuItem, Type vmType)
    {
        if (menuItem.GetValue(NavigationHelper.NavigateToProperty) is string typeKey)
        {
            return Type.GetType($"{typeKey},Totoro.Core") == vmType || Type.GetType($"{typeKey},Totoro.WinUI") == vmType;
        }

        return false;
    }
}
