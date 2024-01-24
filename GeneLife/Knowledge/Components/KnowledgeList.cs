namespace GeneLife.Knowledge.Components
{
    public struct KnowledgeList
    {
        public Knowledge[] KnownCategories;

        public KnowledgeList() => KnownCategories = Array.Empty<Knowledge>();

        public KnowledgeList(Knowledge[] knownCategories)
        {
            KnownCategories = knownCategories;
        }
    }
}