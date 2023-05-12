using GeneLife.Data;
using GeneLife.Sibyl.Components;
using GeneLife.Sibyl.Core;

namespace GeneLife.Sibyl.Services;

public static class KnowledgeService
{
    
    public static KnowledgeLevel GetKnowledgeLevel(KnowledgeCategory category, IEnumerable<Knowledge> knownCategories)
    {
        var cat = knownCategories.FirstOrDefault(x => x.Category == category);
        return cat?.Level ?? KnowledgeLevel.None;
    }

    public static (Learning learning, KnowledgeList knowledgeList) LearningLoop(Learning learning, KnowledgeList knowledgeList, float delta)
    {
        var learningCategory = learning.Class.Category;
        var minLevel = (int)learning.Class.MinRequiredLevel;
        var knowledgeLevel =
            (int)GetKnowledgeLevel(learningCategory, knowledgeList.KnownCategories);
        if (minLevel == 0 && knowledgeLevel == 0 && !learning.Finished && learning.CurrentLearningLevel == 0)
        {
            knowledgeList.KnownCategories = 
                knowledgeList.KnownCategories.Append(new Knowledge(learningCategory, KnowledgeLevel.None)).ToArray();
        }
        
        if(minLevel <= knowledgeLevel && (int)learning.Class.Level > knowledgeLevel && !learning.Finished)
        {
            learning.CurrentLearningLevel += (delta * learning.Class.LearningRate) * Constants.LearningMultiplier;
            learning.CanLearn = true;
        }

        if (!(learning.CurrentLearningLevel >= learning.Class.TargetLearningLevel)) return (learning, knowledgeList);
        learning.Finished = true;
        var idx = knowledgeList.KnownCategories.ToList().FindIndex(x => x.Category == learningCategory);
        if (idx != -1)
        {
            knowledgeList.KnownCategories =  
                knowledgeList.KnownCategories
                    .Where(x => x.Category != learningCategory)
                    .Append(new Knowledge(learning.Class.Category, learning.Class.Level)).ToArray();
        }

        return (learning, knowledgeList);
    }
}