namespace GeneLife.Entities;
public record Planet(int Id, string Name, string Description, EntityType EntityType = EntityType.AstralBody) : IEntity;
