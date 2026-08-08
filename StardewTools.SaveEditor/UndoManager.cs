using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Avalonia.Threading;
using StardewTools.Core.Models;
using StardewTools.Core.Serialization;

namespace StardewTools.SaveEditor;

/// <summary>
/// Whole-app undo/redo over the loaded save's raw XML, not wired into any individual tab's edit
/// code. Every tab's Core editor (PlayerEditor, StorageEditor, FarmMapEditor, ...) is just a typed
/// view over sub-elements of the same shared SaveFile.Document (StardewTools.Core.Serialization.
/// SaveFile) - so instead of adding an explicit checkpoint call to every one of the ~100+ mutation
/// sites across every tab's ViewModels, this subscribes once to System.Xml.Linq's own
/// XObject.Changing/Changed events, which bubble from ANY descendant mutation up to a subscriber
/// on the document (confirmed via a standalone test before relying on it - see the
/// melodic-crafting-treasure plan). No tab needs to know undo exists.
///
/// A short debounce window coalesces a burst of rapid edits (typing in a field, a whole
/// brush-stroke placing many tiles) into one undo step, rather than one per individual XML
/// mutation - the alternative (checkpoint on every single Changing event) would make undo
/// unusably fine-grained for anything but a single discrete click.
/// </summary>
public sealed class UndoManager
{
    private const int MaxDepth = 50;
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(600);

    private readonly List<SaveFile> _undoStack = new();
    private readonly List<SaveFile> _redoStack = new();
    private readonly Action<SaveGameEditor> _onRestore;
    private readonly DispatcherTimer _debounceTimer;

    private SaveGameEditor? _current;
    private bool _dirty;

    public UndoManager(Action<SaveGameEditor> onRestore)
    {
        _onRestore = onRestore;
        _debounceTimer = new DispatcherTimer { Interval = DebounceInterval };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            _dirty = false;
        };
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>Fires after any state change (a checkpoint pushed, an undo/redo, a fresh
    /// attach) that could affect CanUndo/CanRedo - lets the UI keep its buttons in sync.</summary>
    public event Action? StackChanged;

    /// <summary>Call once per freshly-loaded save (StardewTools.SaveEditor.ViewModels.
    /// SaveEditorViewModel.Load) - resets both stacks (a newly opened file has no history) and
    /// starts listening for edits.</summary>
    public void Attach(SaveGameEditor save)
    {
        _current = save;
        _undoStack.Clear();
        _redoStack.Clear();
        _dirty = false;
        _debounceTimer.Stop();
        save.SaveFile.Document.Changing += OnDocumentChanging;
        StackChanged?.Invoke();
    }

    private void OnDocumentChanging(object? sender, XObjectChangeEventArgs e)
    {
        if (_current is null)
            return;

        if (!_dirty)
        {
            PushBounded(_undoStack, _current.SaveFile.Clone());
            _redoStack.Clear();
            _dirty = true;
            StackChanged?.Invoke();
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public void Undo()
    {
        if (_current is null || _undoStack.Count == 0)
            return;

        PushBounded(_redoStack, _current.SaveFile.Clone());
        var snapshot = Pop(_undoStack);
        RestoreFrom(snapshot);
    }

    public void Redo()
    {
        if (_current is null || _redoStack.Count == 0)
            return;

        PushBounded(_undoStack, _current.SaveFile.Clone());
        var snapshot = Pop(_redoStack);
        RestoreFrom(snapshot);
    }

    private void RestoreFrom(SaveFile snapshot)
    {
        var restored = new SaveGameEditor(snapshot);
        _current = restored;
        _dirty = false;
        _debounceTimer.Stop();
        // Rebind every tab BEFORE subscribing to Changing - a tab's Bind() can itself
        // self-heal/write a missing field on first read (an established pattern elsewhere in
        // this codebase), and that write must not look like a new user edit and push a spurious
        // checkpoint onto the very stack this restore just popped from.
        _onRestore(restored);
        restored.SaveFile.Document.Changing += OnDocumentChanging;
        StackChanged?.Invoke();
    }

    private static void PushBounded(List<SaveFile> stack, SaveFile snapshot)
    {
        stack.Add(snapshot);
        if (stack.Count > MaxDepth)
            stack.RemoveAt(0);
    }

    private static SaveFile Pop(List<SaveFile> stack)
    {
        var top = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return top;
    }
}
