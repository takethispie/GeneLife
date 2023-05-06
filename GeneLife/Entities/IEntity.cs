using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeneLife.Entities;
public interface IEntity
{
    int Id { get; init; }
    string Name { get; init; }
    EntityType EntityType { get; init; }
}
