using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;

namespace Daedalus.App.Behaviors;

public static class AvalonEditBehaviors
{
    // ---------------------------------------------------------------------
    // BoundText
    // ---------------------------------------------------------------------
    public static readonly DependencyProperty BoundTextProperty =
        DependencyProperty.RegisterAttached(
            "BoundText",
            typeof(string),
            typeof(AvalonEditBehaviors),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnBoundTextChanged));

    public static string GetBoundText(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string)element.GetValue(BoundTextProperty);
    }

    public static void SetBoundText(DependencyObject element, string value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(BoundTextProperty, value);
    }

    private static void OnBoundTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor) return;

        editor.TextChanged -= OnEditorTextChanged;
        string newText = e.NewValue as string ?? string.Empty;
        if (!string.Equals(editor.Text, newText, StringComparison.Ordinal))
        {
            editor.Text = newText;
        }
        editor.TextChanged += OnEditorTextChanged;
    }

    private static void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (sender is TextEditor editor)
        {
            SetBoundText(editor, editor.Text);
        }
    }

    // ---------------------------------------------------------------------
    // IsReadOnly
    // ---------------------------------------------------------------------
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.RegisterAttached(
            "IsReadOnly",
            typeof(bool),
            typeof(AvalonEditBehaviors),
            new PropertyMetadata(false, OnIsReadOnlyChanged));

    public static bool GetIsReadOnly(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsReadOnlyProperty);
    }

    public static void SetIsReadOnly(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsReadOnlyProperty, value);
    }

    private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextEditor editor)
        {
            editor.IsReadOnly = (bool)e.NewValue;
        }
    }

    // ---------------------------------------------------------------------
    // SyntaxHighlighting
    // ---------------------------------------------------------------------
    public static readonly DependencyProperty SyntaxHighlightingProperty =
        DependencyProperty.RegisterAttached(
            "SyntaxHighlighting",
            typeof(string),
            typeof(AvalonEditBehaviors),
            new PropertyMetadata(string.Empty, OnSyntaxHighlightingChanged));

    public static string GetSyntaxHighlighting(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string)element.GetValue(SyntaxHighlightingProperty);
    }

    public static void SetSyntaxHighlighting(DependencyObject element, string value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(SyntaxHighlightingProperty, value);
    }

    private static void OnSyntaxHighlightingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor) return;

        string language = e.NewValue as string ?? string.Empty;
        if (string.IsNullOrWhiteSpace(language))
        {
            editor.SyntaxHighlighting = null;
            return;
        }

        var highlighting = HighlightingManager.Instance.GetDefinition(language);
        if (highlighting != null)
        {
            editor.SyntaxHighlighting = highlighting;
        }
    }

    // ---------------------------------------------------------------------
    // SynchronizedScrollGroup
    // ---------------------------------------------------------------------
    public static readonly DependencyProperty SynchronizedScrollGroupProperty =
        DependencyProperty.RegisterAttached(
            "SynchronizedScrollGroup",
            typeof(string),
            typeof(AvalonEditBehaviors),
            new PropertyMetadata(string.Empty, OnSynchronizedScrollGroupChanged));

    public static string GetSynchronizedScrollGroup(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (string)element.GetValue(SynchronizedScrollGroupProperty);
    }

    public static void SetSynchronizedScrollGroup(DependencyObject element, string value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(SynchronizedScrollGroupProperty, value);
    }

    private static readonly Dictionary<string, List<WeakReference<TextEditor>>> ScrollGroups = new();
    private static readonly object ScrollGroupsLock = new();
    private static readonly ConditionalWeakTable<TextView, TextEditor> ViewToEditor = new();

    private static bool _isPropagatingScroll;

    private static void OnSynchronizedScrollGroupChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextEditor editor) return;

        string oldGroup = e.OldValue as string ?? string.Empty;
        string newGroup = e.NewValue as string ?? string.Empty;

        if (!string.IsNullOrEmpty(oldGroup))
        {
            UnregisterFromGroup(editor, oldGroup);
        }

        editor.TextArea.TextView.ScrollOffsetChanged -= OnScrollOffsetChanged;

        if (!string.IsNullOrEmpty(newGroup))
        {
            RegisterInGroup(editor, newGroup);
            ViewToEditor.AddOrUpdate(editor.TextArea.TextView, editor);
            editor.TextArea.TextView.ScrollOffsetChanged += OnScrollOffsetChanged;
        }
        else
        {
            ViewToEditor.Remove(editor.TextArea.TextView);
        }
    }

    private static void RegisterInGroup(TextEditor editor, string group)
    {
        lock (ScrollGroupsLock)
        {
            if (!ScrollGroups.TryGetValue(group, out var members))
            {
                members = new List<WeakReference<TextEditor>>();
                ScrollGroups[group] = members;
            }
            members.RemoveAll(static wr => !wr.TryGetTarget(out _));
            members.Add(new WeakReference<TextEditor>(editor));
        }
    }

    private static void UnregisterFromGroup(TextEditor editor, string group)
    {
        lock (ScrollGroupsLock)
        {
            if (!ScrollGroups.TryGetValue(group, out var members)) return;
            members.RemoveAll(wr => !wr.TryGetTarget(out var target) || ReferenceEquals(target, editor));
            if (members.Count == 0)
            {
                ScrollGroups.Remove(group);
            }
        }
    }

    private static void OnScrollOffsetChanged(object? sender, EventArgs e)
    {
        if (_isPropagatingScroll) return;

        if (sender is not TextView sourceView || !ViewToEditor.TryGetValue(sourceView, out var sourceEditor))
            return;

        string group = GetSynchronizedScrollGroup(sourceEditor);
        if (string.IsNullOrEmpty(group)) return;

        List<TextEditor> targets = new();
        lock (ScrollGroupsLock)
        {
            if (ScrollGroups.TryGetValue(group, out var members))
            {
                foreach (var wr in members)
                {
                    if (wr.TryGetTarget(out var target) && !ReferenceEquals(target, sourceEditor))
                    {
                        targets.Add(target);
                    }
                }
            }
        }

        if (targets.Count == 0) return;

        double vOffset = sourceEditor.VerticalOffset;
        double hOffset = sourceEditor.HorizontalOffset;

        _isPropagatingScroll = true;

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var target in targets)
            {
                if (Math.Abs(target.VerticalOffset - vOffset) > 0.5)
                {
                    target.ScrollToVerticalOffset(vOffset);
                }
                if (Math.Abs(target.HorizontalOffset - hOffset) > 0.5)
                {
                    target.ScrollToHorizontalOffset(hOffset);
                }
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _isPropagatingScroll = false;
            }), DispatcherPriority.Background);
        }), DispatcherPriority.Render);
    }
}