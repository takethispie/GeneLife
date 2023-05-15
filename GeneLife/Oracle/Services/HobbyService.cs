using GeneLife.Oracle.Core;

namespace GeneLife.Oracle.Services;

public static class HobbyService
{
    public static (bool needsMoney, float moneyPerWeek) GetHobbyExpenses(HobbyType type) => type switch
    {
        HobbyType.Biking => (true, 15),
        HobbyType.Cars => (true, 200),
        HobbyType.Cinema => (true, 10),
        HobbyType.Cooking => (true, 20),
        HobbyType.Cosplay => (true, 20),
        HobbyType.Drawing => (false, 0),
        HobbyType.Fishing => (true, 10),
        HobbyType.Golf => (true, 100),
        HobbyType.Gym => (true, 5),
        HobbyType.Hiking => (false, 0),
        HobbyType.Motorcycles => (true, 150),
        HobbyType.Music => (false, 0),
        HobbyType.Painting => (true, 5),
        HobbyType.Sailing => (true, 300),
        HobbyType.Streaming => (true, 30),
        HobbyType.Swimming => (false, 0),
        HobbyType.CardGames => (true, 10),
        HobbyType.TabletopGames => (true, 15),
        HobbyType.VideoGames => (true, 15),
        _ => (false, 0)
    };
}