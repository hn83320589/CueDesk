using CueDesk.Core.History;

namespace CueDesk.Core.Tests.History;

public class UndoStackTests
{
    [Fact]
    public void Undo_and_redo_walk_through_states()
    {
        var stack = new UndoStack<int>(0);
        stack.Push(1, "one");
        stack.Push(2, "two");

        Assert.Equal("two", stack.UndoDescription);
        Assert.Equal(1, stack.Undo());
        Assert.Equal(0, stack.Undo());
        Assert.False(stack.CanUndo);
        Assert.Equal("one", stack.RedoDescription);
        Assert.Equal(1, stack.Redo());
        Assert.Equal(2, stack.Redo());
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void Supports_at_least_fifty_levels()
    {
        var stack = new UndoStack<int>(0);
        for (var i = 1; i <= 60; i++)
        {
            stack.Push(i, $"step {i}");
        }
        for (var i = 59; i >= 0; i--)
        {
            Assert.Equal(i, stack.Undo());
        }
        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void Oldest_steps_are_dropped_beyond_capacity()
    {
        var stack = new UndoStack<int>(0, capacity: 50);
        for (var i = 1; i <= 55; i++)
        {
            stack.Push(i, "");
        }
        Assert.Equal(50, stack.UndoCount);
        for (var i = 0; i < 50; i++)
        {
            stack.Undo();
        }
        Assert.Equal(5, stack.Current);
        Assert.False(stack.CanUndo);
    }

    [Fact]
    public void New_push_clears_redo()
    {
        var stack = new UndoStack<int>(0);
        stack.Push(1, "");
        stack.Push(2, "");
        stack.Undo();
        Assert.True(stack.CanRedo);
        stack.Push(3, "");
        Assert.False(stack.CanRedo);
        Assert.Equal(1, stack.Undo());
    }

    [Fact]
    public void Undo_or_redo_when_empty_throws()
    {
        var stack = new UndoStack<int>(0);
        Assert.Throws<InvalidOperationException>(() => stack.Undo());
        Assert.Throws<InvalidOperationException>(() => stack.Redo());
    }

    [Fact]
    public void IsModified_tracks_save_point_through_undo()
    {
        var stack = new UndoStack<int>(0);
        Assert.False(stack.IsModified);
        stack.Push(1, "");
        Assert.True(stack.IsModified);
        stack.MarkSaved();
        Assert.False(stack.IsModified);
        stack.Undo();
        Assert.True(stack.IsModified);
        stack.Redo();
        Assert.False(stack.IsModified);
    }

    [Fact]
    public void Reset_clears_history_and_is_unmodified()
    {
        var stack = new UndoStack<int>(0);
        stack.Push(1, "");
        stack.Reset(10);
        Assert.Equal(10, stack.Current);
        Assert.False(stack.CanUndo);
        Assert.False(stack.IsModified);
    }
}
