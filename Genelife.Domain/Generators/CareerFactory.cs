using Genelife.Domain.Human;

namespace Genelife.Domain.Generators;

public static class CareerFactory
{
    public static Career CreateTechCareer()
    {
        return new(
            "Software Developer",
            25,
            8,
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
        );
    }

    public static Career CreateWriterCareer()
    {
        return new(
            "Writer",
            20,
            6,
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
        );
    }
}