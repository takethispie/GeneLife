namespace Genelife.Components;

public record struct Alive
{
    public int Age;

    public Alive(int age = 18)
    {
        Age = age;
    }
}