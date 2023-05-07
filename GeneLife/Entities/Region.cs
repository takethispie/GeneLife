using GeneLife.Environment;

namespace GeneLife.Entities;

public record Region(Guid Id, string Name, int AverageSalary) : IEntity;
