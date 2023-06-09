using GeneLife.Core.Extensions;
using GeneLife.Genetic.Data;
using GeneLife.Genetic.Exceptions;

namespace GeneLife.Genetic.Mutators;

public static class MeiosisMutator
{
    public static string BuildGamete(Genome genome)
    {
        var sequence = GenomeSequencer.RemoveDelimiters(GenomeSequencer.ToSequence(genome));
        IEnumerable<ChromosomePair> gen = new List<ChromosomePair>();
        while (sequence != "") (sequence, gen) = GenomeSequencer.SequenceTransformStep(sequence, gen);
        var chromosomePairs = gen.ToList();
        if (gen == null || !chromosomePairs.Any()) throw new GenomeParsingError();
        gen = chromosomePairs.DistinctBy(x => x.Values).Where(x => x.Id != 10);
        var random = new Random();
        return new string(gen.Select(x => x.Values.Random(random)).ToArray());
    }
}