namespace Genelife.Main.Scheduler;

internal class Day {
    public DateOnly Date { get; set; }
    private IAction[] Actions { get; set; } = new Empty[24];

    public int GetFirstFreeIndex(int startHour) => Array.FindIndex(Actions, startHour, x => x is Empty);

    public void SetAtIndex(int idx, IAction action) {
        if(idx > Actions.Length) 
            throw new ArgumentOutOfRangeException($"trying to add element at {idx} in an array of {Actions.Length}");
        Actions[idx] = action;
    }
}