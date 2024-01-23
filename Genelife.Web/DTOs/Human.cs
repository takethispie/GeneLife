using GeneLife.Core.Components.Characters;

namespace Genelife.Web.DTOs
{
    public class Human
    {
        public string Identity { get; set; }
        public HumanStats Stats { get; set; }
        public string Wallet { get; set; }
        public string[] Objectives { get; set; }
        public string Position { get; set; }
    }
}