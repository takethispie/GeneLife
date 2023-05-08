using GeneLife.Genetic.Data;

namespace GeneLife.Genetic.Mutators;

public static class GeneticMergingMutator
{
    public static Genome ProduceZygote(string fatherGamete, string motherGamete)
    {
        var sequence = "$$";
        foreach (var gene in fatherGamete)
        {
            var value = motherGamete.ToLower().IndexOf(gene.ToString().ToLower(), StringComparison.InvariantCulture);
            sequence += $"{gene}{motherGamete[value]}";
        }
        sequence += "#00#$$";
        return GenomeSequencer.ToGenome(sequence);
    }
}