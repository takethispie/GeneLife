using GeneLife.Core.Data;
using GeneLife.Knowledge;
using GeneLife.Knowledge.Components;

namespace GeneLife.Knowledge.Services
{
    public static class KnowledgeService
    {

        public static KnowledgeLevel GetKnowledgeLevel(KnowledgeCategory category, IEnumerable<Knowledge> knownCategories)
        {
            var cat = knownCategories.FirstOrDefault(x => x.Category == category);
            return cat?.Level ?? KnowledgeLevel.None;
        }

        internal static (Learning learning, KnowledgeList knowledgeList) LearningLoop(Learning learning, KnowledgeList knowledgeList, float delta)
        {
            var learningCategory = learning.Class.Category;
            var minLevel = (int)learning.Class.MinRequiredLevel;
            var knowledgeLevel =
                (int)GetKnowledgeLevel(learningCategory, knowledgeList.KnownCategories);
            if (minLevel == 0 && knowledgeLevel == 0 && learning is { Finished: false, CurrentLearningLevel: 0 })
                knowledgeList.KnownCategories =
                    knowledgeList.KnownCategories.Append(new Knowledge(learningCategory, KnowledgeLevel.None)).ToArray();

            if (minLevel <= knowledgeLevel && (int)learning.Class.Level > knowledgeLevel && !learning.Finished)
            {
                learning.CurrentLearningLevel += delta * learning.Class.LearningRate * Constants.LearningMultiplier;
                learning.CanLearn = true;
            }

            if (!(learning.CurrentLearningLevel >= learning.Class.TargetLearningLevel)) return (learning, knowledgeList);
            learning.Finished = true;
            var idx = knowledgeList.KnownCategories.ToList().FindIndex(x => x.Category == learningCategory);
            if (idx != -1)
                knowledgeList.KnownCategories =
                    knowledgeList.KnownCategories
                        .Where(x => x.Category != learningCategory)
                        .Append(new Knowledge(learning.Class.Category, learning.Class.Level)).ToArray();

            return (learning, knowledgeList);
        }

        public static bool DoesSchoolTeach(School school, Knowledge knowledge) =>
            school.Classes.Any(cl => cl.Category == knowledge.Category && cl.Level == knowledge.Level);
    }
}