using GeneLife.Data;
using GeneLife.Entities.Person;

namespace GeneLife.Mutators;

public static class AgeMutator
{
    public static IPerson Mutate(IPerson person) => person switch {
        Child child when person.Genome.Age >= Constant.ChildToTeenagerTickCount 
            => new Teenager(child.Id, child.Name, child.LastName, child.Genome, child.Traits),

        Teenager teenager when person.Genome.Age >= Constant.TeenagerToAdultTickCount 
            => new Adult(teenager.Id, teenager.Name, teenager.LastName, teenager.Genome, teenager.Traits),

        Adult adult when person.Genome.Age >= Constant.AdultToElderTickCOunt 
            => new Adult(adult.Id, adult.Name, adult.LastName, adult.Genome, adult.Traits),

        var p => p
    };
    
    public static IPerson MutateAbsolute(IPerson person) => person switch {
        { } p when person.Genome.Age >= Constant.ChildToTeenagerTickCount => new Teenager(p.Id, p.Name, p.LastName, p.Genome, p.Traits),
        { } p when person.Genome.Age >= Constant.TeenagerToAdultTickCount => new Adult(p.Id, p.Name, p.LastName, p.Genome, p.Traits),
        { } p when person.Genome.Age >= Constant.AdultToElderTickCOunt => new Adult(p.Id, p.Name, p.LastName, p.Genome, p.Traits),
        _ => person
    };
}