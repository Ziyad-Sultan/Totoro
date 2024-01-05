﻿using System.ComponentModel;
using System.Reactive.Subjects;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Splat;
using Totoro.WinUI.Contracts;
using Totoro.WinUI.Helpers;

namespace Totoro.WinUI.Services;


public class NavigationService(IVolatileStateStorage stateStorage) : IWinUINavigationService, IEnableLogger
{
    private readonly IVolatileStateStorage _stateStorage = stateStorage;
    private object _lastParameterUsed;
    private Frame _frame;
    private readonly Subject<NavigationEventArgs> _subject = new();

    public Frame Frame
    {
        get
        {
            if (_frame == null)
            {
                _frame = App.MainWindow.Content as Frame;
                RegisterFrameEvents();
            }

            return _frame;
        }

        set
        {
            UnregisterFrameEvents();
            _frame = value;
            RegisterFrameEvents();
        }
    }

    public IObservable<NavigationEventArgs> Navigated => _subject;

    public bool CanGoBack => Frame.CanGoBack;

    private void RegisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated += OnNavigated;
        }
    }

    private void UnregisterFrameEvents()
    {
        if (_frame != null)
        {
            _frame.Navigated -= OnNavigated;
        }
    }

    public bool GoBack()
    {
        if (CanGoBack)
        {
            var vmBeforeNavigation = _frame.GetPageViewModel();

            if(vmBeforeNavigation is IHandleNavigation ihn && ihn.CanHandle())
            {
                ihn.GoBack();
            }
            else
            {
                _frame.GoBack();

                if (vmBeforeNavigation is INavigationAware navigationAware)
                {
                    navigationAware.OnNavigatedFrom();
                }
            }

            return true;
        }

        return false;
    }

    public bool NavigateTo(Type viewModelType,
                           object viewModel = null,
                           IReadOnlyDictionary<string, object> parameter = null,
                           bool clearNavigation = false)
    {
        return NavigateTo(viewModelType, viewModel, parameter, clearNavigation, null);
    }

    public bool NavigateTo<TViewModel>(TViewModel viewModel = null,
                                       IReadOnlyDictionary<string, object> parameter = null,
                                       bool clearNavigation = false)
        where TViewModel : class, INotifyPropertyChanged
    {
        return NavigateTo(typeof(TViewModel), viewModel, parameter, clearNavigation);
    }

    public bool NavigateTo(Type viewModelType, 
                           object viewModel, 
                           IReadOnlyDictionary<string, object> parameter = null, 
                           bool clearNavigation = false,
                           NavigationTransitionInfo transition = null)
    {
        var viewKey = typeof(ViewType<>).MakeGenericType(viewModelType);
        var pageType = ((ViewType)App.GetService(viewKey)).Type;

        if (viewModel is not null)
        {
            var @params = parameter is not null
                ? new Dictionary<string, object>(parameter)
                : [];

            @params.Add("ViewModel", viewModel);

            parameter = @params;
        }


        if ((_frame?.Content?.GetType()) == pageType && (parameter == null || parameter.Equals(_lastParameterUsed)))
        {
            return false;
        }

        _frame.Tag = clearNavigation;
        var vmBeforeNavigation = _frame.GetPageViewModel();
        var navigated = transition is null
            ? _frame.Navigate(pageType, parameter)
            : _frame.Navigate(pageType, parameter, transition);

        if (navigated)
        {
            _lastParameterUsed = parameter;
            if (vmBeforeNavigation is IHaveState stateAware)
            {
                var state = _stateStorage.GetState(stateAware.GetType());
                stateAware.StoreState(state);
            }
            if (vmBeforeNavigation is IDisposable disposable)
            {
                disposable.Dispose();
            }
            if (vmBeforeNavigation is INavigationAware navigationAware)
            {
                navigationAware.OnNavigatedFrom();
            }
        }

        return navigated;
    }

    private async void OnNavigated(object sender, NavigationEventArgs e)
    {
        if (sender is not Frame frame)
        {
            return;
        }

        var parameter = e.Parameter as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>();

        var clearNavigation = (bool)frame.Tag;
        if (clearNavigation)
        {
            frame.BackStack.Clear();
        }

        if (frame.Content is not IViewFor view)
        {
            _subject.OnNext(e);
            return;
        }



        view.ViewModel = !parameter.ContainsKey("ViewModel") || parameter is null
            ? App.GetService(view.GetType().GetProperty("ViewModel").PropertyType)
            : parameter["ViewModel"];


        this.Log().Debug($"Navigated to {view.ViewModel.GetType().Name}");

        if (view.ViewModel is IHaveState stateAware)
        {
            var state = _stateStorage.GetState(stateAware.GetType());

            if (state.IsEmpty)
            {
                await stateAware.SetInitialState();
            }
            else
            {
                stateAware.RestoreState(state);
            }
        }

        if (view.ViewModel is INavigationAware navigationAware)
        {
            try
            {
                await navigationAware.OnNavigatedTo(e.Parameter as IReadOnlyDictionary<string, object> ?? new Dictionary<string, object>());
            }
            catch (Exception ex)
            {
                this.Log().Fatal(ex);
            }
        }

        _subject.OnNext(e);
    }
}

public class ViewType<TViewModel> : ViewType
    where TViewModel : class, INotifyPropertyChanged
{
    public ViewType(Type type)
    {
        Type = type;
    }
}

public class ViewType
{
    public Type Type { get; set; }
}
