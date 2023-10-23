using GeneLife.Core.Components.Characters;

namespace Genelife.Web.DTOs;

public class Human
{
    public Identity Identity { get; set; }
    public Living Living { get; set; }
    public Wallet Wallet { get; set; }
}
