
namespace Genelife.Web.DTOs;

public class HumanNPC
{
    public string Identity { get; set; }
    public HumanStats Stats { get; set; }
    public string Wallet { get; set; }
    public string Position { get; set; }
    public string[] Objectives { get; set; }
}