using System.Text.Json;
using System.Text.Json.Serialization;
using GeneLife.Data.DTOs;
using GeneLife.Data.Exceptions;
using GeneLife.Entities.Person;

namespace GeneLife.Data;

public static class PersonLoader
{

    private enum PersonType
    {
        Child,
        Teenager,
        Adult,
        Elder
    }
    
    public static IEnumerable<IPerson> Load(string path)
    {
        string file = File.ReadAllText(path);
        var save = JsonSerializer.Deserialize<PersonSave>(file);
        if (save == null) throw new ParsingPersonSaveException(); 
        return save.Persons.Select(ToIPerson);
    }
    
    private static IPerson ToIPerson(Person x) => x switch
    {
        not null when x.Age < Constant.ChildToTeenagerTickCount => BuildPerson(x, PersonType.Child),
        not null when x.Age >= Constant.ChildToTeenagerTickCount && x.Age < Constant.TeenagerToAdultTickCount
            => BuildPerson(x, PersonType.Teenager),
        not null when x.Age >= Constant.TeenagerToAdultTickCount && x.Age < Constant.AdultToElderTickCOunt
            => BuildPerson(x, PersonType.Adult),
        not null when x.Age >= Constant.AdultToElderTickCOunt => BuildPerson(x, PersonType.Elder),
        _ => throw new ParsingPersonSaveException()
    };

    private static IPerson BuildPerson(Person x, PersonType type) => type switch
    {
        PersonType.Child => new Child(x.Id, x.Name, x.LastName, GenomeSequencer.ToGenome(x.Genome), x.Traits),
        PersonType.Teenager => new Teenager(x.Id, x.Name, x.LastName, GenomeSequencer.ToGenome(x.Genome), x.Traits),
        PersonType.Adult => new Adult(x.Id, x.Name, x.LastName, GenomeSequencer.ToGenome(x.Genome), x.Traits),
        PersonType.Elder => new Elder(x.Id, x.Name, x.LastName, GenomeSequencer.ToGenome(x.Genome), x.Traits),
        _ => throw new BuildingPersonException()
    };
}