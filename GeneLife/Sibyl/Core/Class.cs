namespace GeneLife.Sibyl.Core;

public struct Class
{
    public string Name;
    public KnowledgeCategory Category;
    public KnowledgeLevel Level;
    public KnowledgeLevel MinRequiredLevel;
    //multiplayer should be high and decrease with increasing level of knowledge
    public float LearningRate;
    public float TargetLearningLevel;
}