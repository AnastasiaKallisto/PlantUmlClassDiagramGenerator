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
        { "-->", 6 },
        { AssociationNode.Association.Node, 7 },
        { AssociationNode.Dependency.Node, 8 },
        {".[#green,thickness=3].>", 9},
        {".[#blue,thickness=3].>", 10 },
        {".[#red,thickness=4].>", 11}
    };

    public static int Compare(string a, string b)
    {
        return Order[a].CompareTo(Order[b]);
    }
}