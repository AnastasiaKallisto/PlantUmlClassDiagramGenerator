using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PlantUmlClassDiagramGenerator.Attributes;

namespace PlantUmlClassDiagramGenerator.Library.ClassDiagramGenerator;

public partial class ClassDiagramGenerator
{
    public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (node.AttributeLists.HasIgnoreAttribute()) { return; }
        if (IsIgnoreMember(node.Modifiers)) { return; }

        var type = node.Type;

        var parentClass = (node.Parent as TypeDeclarationSyntax);
        var isTypeParameterProp = parentClass?.TypeParameterList?.Parameters
            .Any(t => t.Identifier.Text == type.ToString()) ?? false;

        var typeIgnoringNullable = type is NullableTypeSyntax nullableTypeSyntax ? nullableTypeSyntax.ElementType : type;

        var associationAttrSyntax = node.AttributeLists.GetAssociationAttributeSyntax();
        if (associationAttrSyntax is not null)
        {
            var associationAttr = CreateAssociationAttribute(associationAttrSyntax);
            relationships.AddAssociationFrom(node, associationAttr);
        }
        else if (!createAssociation
            || node.AttributeLists.HasIgnoreAssociationAttribute()
            || typeIgnoringNullable is PredefinedTypeSyntax
            || isTypeParameterProp)
        {
            FillAssociatedProperty(node, type);
        }
        else
        {
            if (type.GetType() == typeof(GenericNameSyntax))
            {
                if (this.removeSystemCollectionsAssociations)
                {
                    var t = node.Type.ToString().Split('<')[0];
                    if (!Enum.TryParse(t, out SystemCollectionsTypes _))
                        additionalTypeDeclarationNodes.Add(type);
                    else
                    {
                        FillAssociatedProperty(node, type);
                        var s = node.Type.ToString();
                        relationships.AddAssociationFrom(node, new PlantUmlAssociationAttribute()
                        {
                            Association = "o--",
                            Name = s.Substring(s.IndexOf('<') + 1,s.LastIndexOf('>') - s.IndexOf('<') - 1)
                        });
                    }
                }
                else
                {
                    additionalTypeDeclarationNodes.Add(type);
                    relationships.AddAssociationFrom(node, typeIgnoringNullable);
                }
            } else 
                relationships.AddAssociationFrom(node, typeIgnoringNullable);
        }
    }

    private void FillAssociatedProperty(PropertyDeclarationSyntax node, TypeSyntax type)
    {
        var modifiers = GetMemberModifiersText(node.Modifiers,
            isInterfaceMember: node.Parent.IsKind(SyntaxKind.InterfaceDeclaration));
        var name = node.Identifier.ToString();
        //Property does not have an accessor is an expression-bodied property. (get only)
        var accessorStr = "<<get>>";
        if (node.AccessorList != null)
        {
            var accessor = node.AccessorList.Accessors
                               .Where(x => !x.Modifiers.Select(y => y.Kind()).Contains(SyntaxKind.PrivateKeyword))
                               .Select(x => $"<<{(x.Modifiers.ToString() == "" ? "" : (x.Modifiers.ToString() + " "))}{x.Keyword}>>");
            accessorStr = string.Join(" ", accessor);
        }

        var useLiteralInit = node.Initializer?.Value?.Kind().ToString().EndsWith("LiteralExpression") ?? false;
        var initValue = useLiteralInit
            ? (" = " + escapeDictionary.Aggregate(node.Initializer.Value.ToString(),
                (n, e) => Regex.Replace(n, e.Key, e.Value)))
            : "";

        WriteLine($"{modifiers}{name} : {type} {accessorStr}{initValue}");
    }
}