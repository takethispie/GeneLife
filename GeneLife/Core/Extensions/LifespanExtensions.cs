using GeneLife.Core.Components;
using GeneLife.Core.Data;

namespace GeneLife.Core.Extensions
{
    public static class LifespanExtensions
    {
        public static AgeState AgeState(Lifespan lifespan)
        {
            return lifespan switch
            {
                _ when lifespan.Age.Seconds < Constants.ChildToTeenagerTickCount => Core.AgeState.Child,
                _ when lifespan.Age.Seconds >= Constants.ChildToTeenagerTickCount
                       && lifespan.Age.Seconds < Constants.TeenagerToAdultTickCount => Core.AgeState.Teenager,
                _ when lifespan.Age.Seconds >= Constants.TeenagerToAdultTickCount
                       && lifespan.Age.Seconds < Constants.AdultToElderTickCOunt => Core.AgeState.Adult,
                _ when lifespan.Age.Seconds >= Constants.AdultToElderTickCOunt => Core.AgeState.Elder,
                _ => Core.AgeState.Unknown
            };
        }
    }
}