namespace Genelife.Domain.Scheduler;

public class Scheduler
{
    private List<HumanTask> tasks;

    public Scheduler()
    {
        tasks = [];
    }

    public void AddTask(string name, TimeOnly startTime, TimeOnly endTime)
    {
        var newTask = new HumanTask(name, startTime, endTime);
        
        // Check for time conflicts
        if (HasTimeConflict(newTask))
        {
            throw new InvalidOperationException("Task time conflicts with existing tasks.");
        }

        tasks.Add(newTask);
        tasks = tasks.OrderBy(t => t.Start).ToList();
    }

    public void RemoveTask(string name)
    {
        tasks.RemoveAll(t => t.Name == name);
    }

    public ISlot GetTaskByDateTime(TimeOnly targetTime)
    {
        var containingTask = tasks.FirstOrDefault(t => 
            targetTime >= t.Start && targetTime <= t.End);

        if (containingTask != null)
            return containingTask;

        // Find the next task after target time
        var nextTask = tasks.Where(t => t.Start > targetTime)
                            .OrderBy(t => t.Start)
                            .FirstOrDefault();

        // Create an empty task from target time to next task's start (or end of day)
        return new HumanTask("Empty", targetTime, nextTask.Start);
    }

    public List<HumanTask> GetTasksForTime(TimeOnly time)
    {
        return tasks.Where(t => t.Start.Hour == time.Hour).ToList();
    }

    private bool HasTimeConflict(HumanTask newTask)
    {
        return tasks.Any(existingTask => 
            (newTask.Start < existingTask.End && newTask.End > existingTask.Start));
    }

    public void DisplaySchedule()
    {
        foreach (var task in tasks)
        {
            Console.WriteLine($"{task.Name}: {task.Start} - {task.End}");
        }
    }
}