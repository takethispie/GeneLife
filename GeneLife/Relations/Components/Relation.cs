namespace GeneLife.Relations.Components
{
    public struct Relation
    {
        public int PartnerId;

        public Relation()
        {
            PartnerId = -1;
        }

        public Relation(int id)
        {
            PartnerId = id;
        }
    }
}