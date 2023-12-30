using System.Numerics;
using Arch.Core;
using Arch.Core.Extensions;
using GeneLife.Core.Components;
using GeneLife.Core.Components.Buildings;
using GeneLife.Genetic.GeneticTraits;
using GeneLife.Oracle.Components;
using GeneLife.Oracle.Core;

namespace GeneLife.Core.Entities.Generators;

public static class TemplateCityGenerator
{
    public static void CreateSmallCity(World world)
    {
        ShopGenerator.GroceryStore(world, new Position(new Vector3(500, 500, 0)), new Adress());
        var man1 = PersonGenerator.CreatePure(world, Sex.Male, 20);
        var man2 = PersonGenerator.CreatePure(world, Sex.Male, 20);
        var woman1 = PersonGenerator.CreatePure(world, Sex.Female, 20);
        var woman2 = PersonGenerator.CreatePure(world, Sex.Female, 20);
        var childGirl1 = PersonGenerator.CreatePure(world, Sex.Female, 0, 14);
        var house1 = HouseHoldGenerator.House(world, new Position(new Vector3(0, 0, 0)), new Adress());
        var house2 = HouseHoldGenerator.House(world, new Position(new Vector3(100, 250, 0)), new Adress());
        var house3 = HouseHoldGenerator.House(world, new Position(new Vector3(50, 150, 0)), new Adress());

        man2.Add(new Relation(woman2.Id));
        woman2.Add(new Relation(man2.Id));

        house2.Set(new HouseHold(new [] { man2.Id, woman2.Id }));
        man2.Add(new Home { Position = new Vector3(100, 250, 0), Id = house2.Id });
        woman2.Add(new Home { Position = new Vector3(100, 250, 0), Id = house2.Id });
        
        var familyId = Guid.NewGuid();
        man1.Add(new FamilyMember { FamilyId = familyId, Generation = 0, MemberType = FamilyMemberType.Father });
        childGirl1.Add(new FamilyMember { FamilyId = familyId, Generation = 1, MemberType = FamilyMemberType.Child });

        house1.Set(new HouseHold(new []{ man1.Id, childGirl1.Id }));
        man1.Add(new Home { Position = new Vector3(0, 0, 0), Id = house1.Id });
        childGirl1.Add(new Home { Position = new Vector3(0, 0, 0), Id = house1.Id });

        house3.Set(new HouseHold(new []{ woman1.Id }));
        woman1.Add(new Home { Position = new Vector3(50, 150, 0), Id = house3.Id });
    }
}