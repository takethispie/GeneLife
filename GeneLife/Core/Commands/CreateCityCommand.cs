namespace GeneLife.Core.Commands
{
    public class CreateCityCommand : ICommand
    {
        public TemplateCitySize Size;
    }

    public enum TemplateCitySize
    {
        Small,
        Medium,
        Big,
        Capital
    }
}