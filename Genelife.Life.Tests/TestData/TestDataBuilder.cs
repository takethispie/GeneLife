using System.Numerics;
using Bogus;
using Genelife.Life.Messages.DTOs;

namespace Genelife.Life.Tests.TestData;

public static class TestDataBuilder
{
    private static readonly Faker Faker = new();

    public static Human CreateHuman(
        string? firstName = null,
        string? lastName = null,
        DateTime? birthday = null,
        Sex? sex = null,
        float? money = null,
        float? hunger = null,
        float? energy = null,
        float? hygiene = null)
    {
        return new Human(
            firstName ?? Faker.Name.FirstName(),
            lastName ?? Faker.Name.LastName(),
            birthday ?? Faker.Date.Past(50, DateTime.Now.AddYears(-18)),
            sex ?? Faker.PickRandom<Sex>(),
             new SkillSet(),
            new Position(Vector3.Zero, ""),
            money ?? Faker.Random.Float(0, 10000),
            hunger ?? Faker.Random.Float(0, 100),
            energy ?? Faker.Random.Float(0, 100),
            hygiene ?? Faker.Random.Float(0, 100)
        );
    }
}