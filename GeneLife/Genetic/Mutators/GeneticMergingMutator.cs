using GeneLife.Genetic.Data;
using GeneLife.Utils;

namespace GeneLife.Genetic.Mutators;

public static class GeneticMergingMutator
{
    public static Genome ProduceZygote(string fatherGamete, string motherGamete)
    {
        fatherGamete = fatherGamete.Replace("X", "").Replace("x", "").Replace("Y", "").Replace("y", "");
        motherGamete = motherGamete.Replace("X", "").Replace("x", "");
        if (fatherGamete.Length != motherGamete.Length)
            throw new ArgumentException($"incompatible length: {fatherGamete.Length} vs {motherGamete.Length}");
        var sequence = "$$";
        foreach (var gene in fatherGamete)
        {
            var value = motherGamete.ToLower().IndexOf(gene.ToString().ToLower(), StringComparison.InvariantCulture);
            if(value < 0)
            {
                throw new ArgumentException();
            }
            sequence += $"{gene}{motherGamete[value]}";
        }
        string sex = new List<string> { "XX", "XY"}.Random(new Random());
        sequence += $"{sex}#00#$$";
        return GenomeSequencer.ToGenome(sequence);
    }
}