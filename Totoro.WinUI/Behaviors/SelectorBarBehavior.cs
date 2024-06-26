﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.Xaml.Interactivity;
using ReactiveMarbles.ObservableEvents;
using Totoro.Core.ViewModels;

namespace Totoro.WinUI.Behaviors;

public class SelectorBarBehavior : Behavior<SelectorBar>
{
    public IEnumerable<PivotItemModel> ItemSource
    {
        get { return (IEnumerable<PivotItemModel>)GetValue(ItemSourceProperty); }
        set { SetValue(ItemSourceProperty, value); }
    }

    public PivotItemModel SelectedItem
    {
        get { return (PivotItemModel)GetValue(SelectedItemProperty); }
        set { SetValue(SelectedItemProperty, value); }
    }

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register("SelectedItem", typeof(PivotItemModel), typeof(SelectorBarBehavior), new PropertyMetadata(null));

    public static readonly DependencyProperty ItemSourceProperty =
        DependencyProperty.Register("ItemSource", typeof(IEnumerable<PivotItemModel>), typeof(SelectorBarBehavior), new PropertyMetadata(Enumerable.Empty<PivotItemModel>()));


    protected override void OnAttached()
    {
        this.WhenAnyValue(x => x.ItemSource)
            .Where(x => x is not null && x.Any() && AssociatedObject is not null)
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(values =>
            {
                AssociatedObject.Items.Clear();
                foreach (var item in values)
                {
                    var barItem = new SelectorBarItem
                    {
                        Text = item.Header,
                        FontSize = 20,
                    };

                    barItem.SetBinding(UIElement.VisibilityProperty, new Binding
                    {
                        Source = item,
                        Path = new PropertyPath(nameof(item.Visible)),
                        Mode = BindingMode.OneWay
                    });

                    AssociatedObject.Items.Add(barItem);
                }

                if (SelectedItem is null)
                {
                    AssociatedObject.SelectedItem = AssociatedObject.Items.FirstOrDefault();
                }
            });

        this.WhenAnyValue(x => x.SelectedItem)
            .Where(_ => AssociatedObject is not null)
            .WhereNotNull()
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(item =>
            {
                if (AssociatedObject.Items.FirstOrDefault(x => x.Text == item.Header) is not { } uiItem)
                {
                    return;
                }

                AssociatedObject.SelectedItem = uiItem;
            });

        AssociatedObject
            .Events()
            .SelectionChanged
            .Select(x => x.sender.SelectedItem?.Text)
            .Where(text => !string.IsNullOrEmpty(text))
            .Subscribe(header => SelectedItem = ItemSource.FirstOrDefault(x => x.Header == header));

    }
}
