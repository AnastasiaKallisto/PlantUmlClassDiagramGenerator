using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PlantUmlClassDiagramGenerator.Attributes;
using PlantUmlClassDiagramGenerator.Library.Enums;

namespace PlantUmlClassDiagramGenerator.Library;

public class RelationshipCollection : IEnumerable<Relationship>
{
    private readonly IList<Relationship> items = new List<Relationship>();

    public void AddAll(RelationshipCollection collection)
    {
        foreach (var c in collection)
        {
            items.Add(c);
        }
    }

    public void AddInheritanceFrom(TypeDeclarationSyntax syntax)
    {
        if (syntax.BaseList == null) return;

        var subTypeName = TypeNameText.From(syntax);

        foreach (var typeStntax in syntax.BaseList.Types)
        {
            if (typeStntax.Type is not SimpleNameSyntax typeNameSyntax) continue;
            var baseTypeName = TypeNameText.From(typeNameSyntax);
            items.Add(new Relationship(baseTypeName, subTypeName, "<|--", baseTypeName.TypeArguments));
        }
    }
    
    public void AddInheritanceFromWithoutSystemTypes(TypeDeclarationSyntax syntax)
    {
        if (syntax.BaseList == null) return;

        var subTypeName = TypeNameText.From(syntax);

        foreach (var typeStntax in syntax.BaseList.Types)
        {
            if (typeStntax.Type is not SimpleNameSyntax typeNameSyntax) continue;
            var baseTypeName = TypeNameText.From(typeNameSyntax);
            if (!Enum.TryParse(baseTypeName.Identifier, out SystemCollectionsTypes _) && !Enum.TryParse(baseTypeName.Identifier, out IgnoredTypes _))
                items.Add(new Relationship(baseTypeName, subTypeName, "<|--", baseTypeName.TypeArguments));
        }
    }

    public void AddInnerclassRelationFrom(SyntaxNode node)
    {
        if (node.Parent is not BaseTypeDeclarationSyntax outerTypeNode 
            || node is not BaseTypeDeclarationSyntax innerTypeNode) return;

        var outerTypeName = TypeNameText.From(outerTypeNode);
        var innerTypeName = TypeNameText.From(innerTypeNode);
        items.Add(new Relationship(outerTypeName, innerTypeName, "+--"));
    }

    public void AddAssociationFrom(FieldDeclarationSyntax node, VariableDeclaratorSyntax field)
    {
        var symbol = "o--";
        var fieldIdentifier = field.Identifier.ToString();
        if (node.Declaration.Type is ArrayTypeSyntax leafNodeArray
            && node.Parent is BaseTypeDeclarationSyntax rootNodeForArray)
        {
            AddAssociationForArray(leafNodeArray.ToString(), TypeNameText.From(rootNodeForArray), symbol, "");
            return;
        }

        if (node.Declaration.Type is not SimpleNameSyntax leafNode
            || node.Parent is not BaseTypeDeclarationSyntax rootNode)
            return;
        
        var leafName = TypeNameText.From(leafNode);
        var rootName = TypeNameText.From(rootNode);
        AddRelationship(leafName, rootName, symbol, fieldIdentifier);
    }

    public void AddAssociationFromWithNoLabel(FieldDeclarationSyntax node, VariableDeclaratorSyntax field)
    {
        var symbol = "o--";
        if (node.Declaration.Type is ArrayTypeSyntax leafNodeArray
            && node.Parent is BaseTypeDeclarationSyntax rootNodeForArray)
        {
            AddAssociationForArray(leafNodeArray.ToString(), TypeNameText.From(rootNodeForArray), symbol, "");
            return;
        }

        if (node.Declaration.Type is not SimpleNameSyntax leafNode
            || node.Parent is not BaseTypeDeclarationSyntax rootNode)
            return;
        var leafName = TypeNameText.From(leafNode);
        var rootName = TypeNameText.From(rootNode);
        AddRelationship(leafName, rootName, symbol, "");
    }

    private void AddAssociationForArray(string type, TypeNameText rootName, string symbol, string nodeIdentifier)
    {
        var s = type.Split('[')[0];
        if (!Enum.TryParse(CapitalizeFirstLetter(s), out BaseTypes _))
            AddRelationship(new TypeNameText{Identifier = s, TypeArguments = ""}, rootName, symbol, "");
    }

    public void AddAssociationFrom(PropertyDeclarationSyntax node, TypeSyntax typeIgnoringNullable)
    {
        var symbol = "o--";
        var nodeIdentifier = node.Identifier.ToString();
        if (typeIgnoringNullable is ArrayTypeSyntax leafNodeArray
            && node.Parent is BaseTypeDeclarationSyntax rootNodeForArray)
        {
            AddAssociationForArray(leafNodeArray.ToString(), TypeNameText.From(rootNodeForArray), symbol, "");
            return;
        }

        if (typeIgnoringNullable is not SimpleNameSyntax leafNode
            || node.Parent is not BaseTypeDeclarationSyntax rootNode)
            return;

        var leafName = TypeNameText.From(leafNode);
        var rootName = TypeNameText.From(rootNode);
        AddRelationship(leafName, rootName, symbol, nodeIdentifier);
    }
    
    public void AddAssociationFromWithNoLabel(PropertyDeclarationSyntax node, TypeSyntax typeIgnoringNullable)
    {
        var symbol = "o--";
        if (typeIgnoringNullable is ArrayTypeSyntax leafNodeArray
            && node.Parent is BaseTypeDeclarationSyntax rootNodeForArray)
        {
            AddAssociationForArray(leafNodeArray.ToString(), TypeNameText.From(rootNodeForArray), symbol, "");
            return;
        }

        if (typeIgnoringNullable is not SimpleNameSyntax leafNode
            || node.Parent is not BaseTypeDeclarationSyntax rootNode)
            return;
        var leafName = TypeNameText.From(leafNode);
        var rootName = TypeNameText.From(rootNode);
        AddRelationship(leafName, rootName, symbol, "");
    }

    public void AddAssociationFrom(ParameterSyntax node, RecordDeclarationSyntax parent)
    {
        if (node.Type is not SimpleNameSyntax leafNode 
            || parent is not BaseTypeDeclarationSyntax rootNode) return;

        var symbol = node.Default == null ? "-->" : "o--";
        var nodeIdentifier = node.Identifier.ToString();
        var leafName = TypeNameText.From(leafNode);
        var rootName = TypeNameText.From(rootNode);
        AddRelationship(leafName, rootName, symbol, nodeIdentifier);
    }

    public void AddAssociationFrom(PropertyDeclarationSyntax node, PlantUmlAssociationAttribute attribute)
    {
        if (node.Parent is not BaseTypeDeclarationSyntax rootNode) return;
        var leafName = GetLeafName(attribute.Name, node.Type);
        if (leafName is null) { return; }
        var rootName = TypeNameText.From(rootNode);
        AddRelationship(attribute, leafName, rootName);

    }

    public void AddAssociationFrom(MethodDeclarationSyntax node, ParameterSyntax parameter, PlantUmlAssociationAttribute attribute)
    {
        if (node.Parent is not BaseTypeDeclarationSyntax rootNode) return;
        var leafName = GetLeafName(attribute.Name, parameter.Type);
        if (leafName is null) { return; }
        var rootName = TypeNameText.From(rootNode);
        AddRelationship(attribute, leafName, rootName);
    }

    public void AddAssociationFrom(MethodDeclarationSyntax node, ParameterSyntax parameter)
    {
        if (node.Parent is not BaseTypeDeclarationSyntax rootNode) return;
        var symbol = ".[#blue,thickness=3].>";
        TypeNameText leafName;
        var leafIdentifier = parameter.Type.ToString();
        if (Enum.TryParse(leafIdentifier, out IgnoredTypes _)
            || Enum.TryParse(CapitalizeFirstLetter(leafIdentifier), out BaseTypes _))
            return;
        if (Enum.TryParse(leafIdentifier.Split('<')[0], out SystemCollectionsTypes _))
        {
            var s = leafIdentifier.Split('<')[1];
            s = s.Remove(s.Length - 1);
            if (!Enum.TryParse(CapitalizeFirstLetter(s), out BaseTypes _)
                && !s.Contains(",") && !s.Contains("(") && !s.Contains(")"))
                leafName = new TypeNameText
                {
                    Identifier = s,
                    TypeArguments = ""
                };
        }
        else
        {
            leafName = new TypeNameText
            {
                Identifier = leafIdentifier,
                TypeArguments = ""
            };
            var rootName = TypeNameText.From(rootNode);
            AddRelationship(leafName, rootName, symbol, "");
        }
            
    }

    public void AddAssociationFrom(RecordDeclarationSyntax node, ParameterSyntax parameter, PlantUmlAssociationAttribute attribute)
    {
        if (node is not BaseTypeDeclarationSyntax rootNode) { return; }
        var leafName = GetLeafName(attribute.Name, parameter.Type);
        if (leafName is null) { return; }
        var rootName = TypeNameText.From(rootNode);
        AddRelationship(attribute, leafName, rootName);
    }

    public void AddAssociationFrom(ConstructorDeclarationSyntax node, ParameterSyntax parameter, PlantUmlAssociationAttribute attribute)
    {
        if (node.Parent is not BaseTypeDeclarationSyntax rootNode) { return; }
        var leafName = GetLeafName(attribute.Name, parameter.Type);
        if (leafName is null) { return; }
        var rootName = TypeNameText.From(rootNode);
        AddRelationship(attribute, leafName, rootName);
    }
    
    
    public void AddAssociationFrom(ConstructorDeclarationSyntax node, ParameterSyntax parameter)
    {
        if (node.Parent is not BaseTypeDeclarationSyntax rootNode) return;
        var symbol = ".[#green,thickness=3].>";
        TypeNameText leafName;
        TypeNameText rootName;
        var leafIdentifier = parameter.Type.ToString();
        if (Enum.TryParse(leafIdentifier, out IgnoredTypes _)
            || Enum.TryParse(CapitalizeFirstLetter(leafIdentifier), out BaseTypes _))
            return;
        var s0 = leafIdentifier.Split('<')[0];
        if (Enum.TryParse(s0, out SystemCollectionsTypes _))
        {
            if (leafIdentifier.Contains("<"))
            {
                var s = leafIdentifier.Split('<')[1];
                s = s.Remove(s.Length - 1);
                if (!Enum.TryParse(CapitalizeFirstLetter(s), out BaseTypes _)
                    && !s.Contains(",") && !s.Contains("(") && !s.Contains(")"))
                {
                    rootName = TypeNameText.From(rootNode);
                    if (rootName.Identifier.Equals(s) && (s0.Equals("ILogger") || s0.Equals("Logger") || (s0.Equals("IOptions") && s.Equals(rootName))))
                        return;
                    leafName = new TypeNameText
                    {
                        Identifier = s,
                        TypeArguments = ""
                    };
                    AddRelationship(leafName, rootName, symbol, "");
                }
            }
        }
        else
        {
            leafName = new TypeNameText
            {
                Identifier = leafIdentifier,
                TypeArguments = ""
            };
            rootName = TypeNameText.From(rootNode);
            AddRelationship(leafName, rootName, symbol, "");
        }
            
    }

    public void AddAssociationFrom(FieldDeclarationSyntax node, PlantUmlAssociationAttribute attribute)
    {
        if (node.Parent is not BaseTypeDeclarationSyntax rootNode) { return; }
        var leafName = GetLeafName(attribute.Name, node.Declaration.Type);
        if(leafName is null) { return; }
        var rootName = TypeNameText.From(rootNode);
        AddRelationship(attribute, leafName, rootName);
    }

    private static TypeNameText GetLeafName(string attributeName, TypeSyntax typeSyntax)
    {
        if (!string.IsNullOrWhiteSpace(attributeName))
        {
            return new TypeNameText() { Identifier = attributeName, TypeArguments = ""};
        }
        else if (typeSyntax is SimpleNameSyntax simpleNode)
        {
            return TypeNameText.From(simpleNode);
        }
        return null;
        
    }

    private void AddRelationship(PlantUmlAssociationAttribute attribute, TypeNameText leafName, TypeNameText rootName)
    {
        var symbol = string.IsNullOrEmpty(attribute.Association) ? "--" : attribute.Association;
        var relationship = new Relationship(rootName, leafName, symbol, attribute.RootLabel, attribute.LeafLabel, attribute.Label);
        if (!items.Contains(relationship))
            items.Add(relationship);
        else if (items[items.IndexOf(relationship)].CompareTo(relationship) > 0)
        {
            items.Remove(relationship);
            items.Add(relationship);
        }
    }

    private void AddRelationship(TypeNameText leafName, TypeNameText rootName, string symbol, string nodeIdentifier)
    {
        var relationship = new Relationship(rootName, leafName, symbol, "", nodeIdentifier + leafName.TypeArguments);
        if (!items.Contains(relationship))
            items.Add(relationship);
        else if (items[items.IndexOf(relationship)].CompareTo(relationship) > 0)
        {
            items.Remove(relationship);
            items.Add(relationship);
        }
    }

    public IEnumerator<Relationship> GetEnumerator()
    {
        return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void RemoveAll(IEnumerable<Relationship> relationships)
    {
        foreach (var r in relationships)
        {
            items.Remove(r);
        }
    }
    
    private static string CapitalizeFirstLetter(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        if (input.Length == 1)
            return char.ToUpper(input[0]) + "";

        return char.ToUpper(input[0]) + input.Substring(1).Replace("?", "");
    }
}
