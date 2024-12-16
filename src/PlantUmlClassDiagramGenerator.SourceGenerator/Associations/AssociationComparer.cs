namespace PlantUmlClassDiagramGenerator.SourceGenerator.Associations;

public static class AssociationComparer
{
    private static readonly Dictionary<string, int> Order = new()
    {
        { AssociationNode.Inheritance.Node, 1 },
        { AssociationNode.Realization.Node, 2 },
        { AssociationNode.Composition.Node, 3 },
        { AssociationNode.Aggregation.Node, 4 },
        { AssociationNode.Nest.Node, 5 },
        { AssociationNode.Association.Node, 6 },
        { AssociationNode.Dependency.Node, 7 },
        {".[#blue,thickness=3].>", 8 },
        {".[#red,thickness=4].>", 9}
    };

    public static int Compare(string a, string b)
    {
        return -1*Order[a].CompareTo(Order[b]);
    }
}