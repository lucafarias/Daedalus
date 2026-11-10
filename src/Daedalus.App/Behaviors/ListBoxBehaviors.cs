using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Daedalus.App.Behaviors;

/// <summary>
/// Attached behaviors for <see cref="ListBox"/>.
/// </summary>
public static class ListBoxBehaviors
{
    /// <summary>
    /// Identifies the <c>AutoScroll</c> attached property.
    /// When set to <c>true</c>, the ListBox automatically scrolls to the last item
    /// when a new item is added to its ItemsSource.
    /// </summary>
    public static readonly DependencyProperty AutoScrollProperty =
        DependencyProperty.RegisterAttached(
            "AutoScroll",
            typeof(bool),
            typeof(ListBoxBehaviors),
            new PropertyMetadata(false, OnAutoScrollChanged));

    public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);

    public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);

    private static readonly Dictionary<INotifyCollectionChanged, ListBox> CollectionToListBoxMap = new();
    private static readonly object LockObj = new();

    private static readonly DependencyPropertyDescriptor ItemsSourceDescriptor =
        DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ItemsControl));

    private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBox listBox) return;

        if ((bool)e.NewValue)
        {
            listBox.Loaded += ListBox_Loaded;
            listBox.Unloaded += ListBox_Unloaded;
            if (listBox.IsLoaded) Attach(listBox);
        }
        else
        {
            listBox.Loaded -= ListBox_Loaded;
            listBox.Unloaded -= ListBox_Unloaded;
            Detach(listBox);
        }
    }

    private static void ListBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox) Attach(listBox);
    }

    private static void ListBox_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListBox listBox) Detach(listBox);
    }

    private static void Attach(ListBox listBox)
    {
        ItemsSourceDescriptor.AddValueChanged(listBox, OnItemsSourceChanged);
        RegisterCollection(listBox, listBox.ItemsSource);
    }

    private static void Detach(ListBox listBox)
    {
        ItemsSourceDescriptor.RemoveValueChanged(listBox, OnItemsSourceChanged);
        UnregisterCollection(listBox, listBox.ItemsSource);
    }

    private static void OnItemsSourceChanged(object? sender, EventArgs e)
    {
        if (sender is ListBox listBox)
        {
            RegisterCollection(listBox, listBox.ItemsSource);
        }
    }

    private static void RegisterCollection(ListBox listBox, System.Collections.IEnumerable? itemsSource)
    {
        if (itemsSource is INotifyCollectionChanged collection)
        {
            lock (LockObj)
            {
                if (!CollectionToListBoxMap.ContainsKey(collection))
                {
                    CollectionToListBoxMap[collection] = listBox;
                    collection.CollectionChanged += OnCollectionChanged;
                }
            }
        }
    }

    private static void UnregisterCollection(ListBox listBox, System.Collections.IEnumerable? itemsSource)
    {
        if (itemsSource is INotifyCollectionChanged collection)
        {
            lock (LockObj)
            {
                if (CollectionToListBoxMap.TryGetValue(collection, out var mappedListBox) && mappedListBox == listBox)
                {
                    collection.CollectionChanged -= OnCollectionChanged;
                    CollectionToListBoxMap.Remove(collection);
                }
            }
        }
    }

    private static void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add && e.Action != NotifyCollectionChangedAction.Reset)
            return;

        if (sender is not INotifyCollectionChanged collection) return;

        lock (LockObj)
        {
            if (CollectionToListBoxMap.TryGetValue(collection, out var listBox))
            {
                // Scroll asincrono per assicurarsi che il layout sia aggiornato
                listBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (listBox.Items.Count > 0)
                    {
                        listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
                    }
                }), DispatcherPriority.Background);
            }
        }
    }
}