using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneLife.Entities.Person;

namespace GeneLife.Entities;

public record  Family(int Id, int CountryId, int NeighbourhoodId, string LastName, IEnumerable<int> Parents, IEnumerable<int> Children);