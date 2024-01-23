using System.Numerics;

namespace GeneLife.Core.Components.Buildings
{
    public struct Adress
    {
        public string Number;
        public string Street;
        public string City;
        public string ZipCode;

        public readonly string Full() => $"{Number} {Street} {ZipCode} {City}";
    }
}