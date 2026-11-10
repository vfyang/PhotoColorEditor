namespace PhotoColorEditor;

internal sealed class EditHistory
{
    private const int MaxSteps = 21;
    private readonly List<EditSettings> states = [EditSettings.Default];
    private int index;

    public EditSettings Current => states[index];
    public int UndoCount => index;
    public int RedoCount => states.Count - 1 - index;
    public bool CanRedo => RedoCount > 0;

    public bool Commit(EditSettings settings)
    {
        if (settings == Current) return false;
        states.RemoveRange(index + 1, states.Count - index - 1);
        states.Add(settings);
        if (states.Count > MaxSteps + 1) states.RemoveAt(0);
        index = states.Count - 1;
        return true;
    }

    public bool Undo()
    {
        if (index == 0) return false;
        index--;
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo) return false;
        index++;
        return true;
    }
}
