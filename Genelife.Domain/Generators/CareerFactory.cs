using Genelife.Domain.Human;

namespace Genelife.Domain.Generators;

public static class CareerFactory
{
    public static Career CreateTechCareer()
    {
        return new(
            "Software Developer",
            25.0m,
            new TimeSpan(8, 0, 0), // 8 hours
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
        );
    }

    public static Career CreateWriterCareer()
    {
        return new(
            "Writer",
            20.0m,
            new TimeSpan(6, 0, 0), // 6 hours
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
        );
    }
}