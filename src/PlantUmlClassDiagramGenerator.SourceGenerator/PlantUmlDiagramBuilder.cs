﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using PlantUmlClassDiagramGenerator.SourceGenerator.Associations;
using PlantUmlClassDiagramGenerator.SourceGenerator.Extensions;
using System.Collections.Immutable;
using System.Text;

namespace PlantUmlClassDiagramGenerator.SourceGenerator;

public class PlantUmlDiagramBuilder(
    INamedTypeSymbol symbol,
    string indent = "    ")
{
    private INamedTypeSymbol Symbol { get; } = symbol;
    private IList<string> MemberDeclarations { get; } = new List<string>();
    private ISet<Association> Associations { get; } = new HashSet<Association>();
    private ISet<string> IncludeItems { get; } = new HashSet<string>();

    public string Indent { get; set; } = indent;
    public string UmlString { get; private set; } = "";


    public string Build(IImmutableSet<INamedTypeSymbol> symbols)
    {
        Clear();
        SetInheritance(symbols);
        SetRealization(symbols);
        ProcessMembersSymbol(symbols);
        UmlString = MakeUmlString();
        return UmlString;
    }

    private void Clear()
    {
        MemberDeclarations.Clear();
        Associations.Clear();
        IncludeItems.Clear();
        UmlString = "";
    }

    private void ProcessMembersSymbol(IImmutableSet<INamedTypeSymbol> symbols)
    {
        foreach (var member in Symbol.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol fieldSymbol:
                    if (!fieldSymbol.Name.StartsWith("<")) //skip "<PropName>N_BackingField";
                    {
                        SetFieldDeclaration(fieldSymbol);
                        SetFieldAssociation(fieldSymbol, symbols);
                    }
                    break;
                case IPropertySymbol propertySymbol:
                    SetPropertyDeclaration(propertySymbol);
                    SetPropertyAssociation(propertySymbol, symbols);
                    break;
                case IMethodSymbol methodSymbol:
                    if (methodSymbol.MethodKind is not MethodKind.PropertyGet
                        and not MethodKind.PropertySet
                        and not MethodKind.EventAdd
                        and not MethodKind.EventRemove
                        && !methodSymbol.Name.StartsWith("<")) //skip property Getter/Setter & event Add/Remove & <Clone>
                    {
                        SetMethodDeclaration(methodSymbol);
                        SetMethodAssociation(methodSymbol, symbols);
                    }
                    break;
                case IEventSymbol eventSymbol:
                    SetEventDeclaration(eventSymbol);
                    break;
                case INamedTypeSymbol nestedType:
                    SetNest(nestedType);
                    break;
            }
        }
    }

    private string MakeUmlString()
    {
        var sb = new StringBuilder();
        //@startuml
        sb.AppendLine($"@startuml {Symbol.MetadataName}");
        //!include section
        if (IncludeItems.Count > 0)
        {
            sb.AppendLine(string.Join(Environment.NewLine, IncludeItems.Select(s => $"!include {s}.puml")));
        }
        //type declare
        sb.AppendLine($$"""
            {{GetTypeDeclaration()}} {
            {{string.Join(Environment.NewLine, MemberDeclarations.Select(s => Indent + s))}}
            }
            """);
        //associations
        if (Associations.Count > 0)
        {
            sb.AppendLine(string.Join(Environment.NewLine, Associations.Select(a => a.ToString())));
        }
        //@enduml
        sb.Append("@enduml");
        return sb.ToString();
    }

    private string GetTypeDeclaration()
    {
        var typeKind = Symbol.GetTypeKindString();
        var name = Symbol.MetadataName + Symbol.GetTypeParamtersString();
        var modifiers = Symbol.GetModifiersString();
        return $"{typeKind} {name} {modifiers}";
    }

    private void SetPropertyDeclaration(IPropertySymbol propertySymbol)
    {
        var accessibility = propertySymbol.DeclaredAccessibility.GetMemberAccessibilityString();
        var modifiers = propertySymbol.GetModifiersString();
        var typeName = propertySymbol.GetTypeString();
        var accessors = propertySymbol.GetAccessorString();
        MemberDeclarations.Add($"{accessibility}{modifiers}{propertySymbol.Name} : {typeName} {accessors}");
    }

    private void SetEventDeclaration(IEventSymbol eventSymbol) 
    {
        var accessibility = eventSymbol.DeclaredAccessibility.GetMemberAccessibilityString();
        var modifiers = eventSymbol.GetModifiersString();
        var typeName = eventSymbol.GetTypeString();
        MemberDeclarations.Add($"{accessibility}{modifiers}<<event>> {eventSymbol.Name} : {typeName}");
    }

    private void SetFieldDeclaration(IFieldSymbol fieldSymbol)
    {
        if (Symbol.TypeKind == TypeKind.Enum)
        {
            MemberDeclarations.Add(fieldSymbol
                .ToDisplayString(new SymbolDisplayFormat(
                    memberOptions: SymbolDisplayMemberOptions.IncludeConstantValue)));
            return;
        }
        var accessibility = fieldSymbol.DeclaredAccessibility.GetMemberAccessibilityString();
        var modifiers = fieldSymbol.GetModifiersString();
        var typeName = fieldSymbol.GetTypeString();
        var parts = fieldSymbol.ToDisplayParts(new SymbolDisplayFormat(memberOptions: SymbolDisplayMemberOptions.IncludeConstantValue));
        var value = parts.FirstOrDefault(p => p.Kind is SymbolDisplayPartKind.StringLiteral or SymbolDisplayPartKind.NumericLiteral);
        MemberDeclarations.Add($"{accessibility}{modifiers}{fieldSymbol.Name} : {typeName}");
    }

    private void SetMethodDeclaration(IMethodSymbol methodSymbol)
    {
        var accessibility = methodSymbol.DeclaredAccessibility.GetMemberAccessibilityString();
        var modifiers = methodSymbol.GetModifiersString();
        var returnType = methodSymbol.GetReturnTypeString();
        var parameters = methodSymbol.GetParametersString();
        var name = methodSymbol.GetName();
        MemberDeclarations.Add($"{accessibility}{modifiers}{name}({parameters}){returnType}");
    }

    private void SetPropertyAssociation(IPropertySymbol propertySymbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        var targetType = propertySymbol.Type;
        var leafLabel = "";

        var ie = propertySymbol.Type.AllInterfaces
            .FirstOrDefault(x => x.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
        if (ie != null)
        {
            targetType = ie.TypeArguments[0];
            leafLabel = "*";
        }
        else if (propertySymbol.Type is IArrayTypeSymbol arrayType)
        {
            targetType = arrayType.ElementType;
            leafLabel = "*";
        }

        if (targetType is INamedTypeSymbol typeSymbol
                && ContainsType(typeSymbol, symbols)
                && !typeSymbol.Equals(Symbol, SymbolEqualityComparer.Default))
        {
            if (propertySymbol.HasPropertyInitializer()
                || Symbol.ContainsObjectCreationInConstructor(propertySymbol.Type))
            {
                Associations.Add(AssociationKind.Composition.Create(
                    Symbol,
                    typeSymbol,
                    label: propertySymbol.Name,
                    leafLabel: leafLabel));
            }
            else
            {
                Associations.Add(AssociationKind.Aggregation.Create(
                    Symbol,
                    typeSymbol,
                    label: propertySymbol.Name,
                    leafLabel: leafLabel));
            }
            IncludeItems.Add(typeSymbol.MetadataName);
        }
    }

    private void SetFieldAssociation(IFieldSymbol fieldSymbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        if (fieldSymbol.Type is INamedTypeSymbol typeSymbol
            && ContainsType(typeSymbol, symbols)
            && !typeSymbol.Equals(Symbol, SymbolEqualityComparer.Default))
        {
            var leafLabel = typeSymbol.IsGenericType ? typeSymbol.GetTypeArgumentsString() : "";


            if (fieldSymbol.HasFieldInitializer()
                || Symbol.ContainsObjectCreationInConstructor(fieldSymbol.Type))
            {
                Associations.Add(AssociationKind.Composition.Create(
                    Symbol,
                    typeSymbol,
                    label: fieldSymbol.Name,
                    leafLabel: leafLabel));
            }
            else
            {
                Associations.Add(AssociationKind.Aggregation.Create(
                    Symbol,
                    typeSymbol,
                    label: fieldSymbol.Name,
                    leafLabel: leafLabel));
            }
            IncludeItems.Add(typeSymbol.MetadataName);
        }
    }

    private void SetMethodAssociation(IMethodSymbol methodSymbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        foreach (var parameter in methodSymbol.Parameters)
        {
            if (parameter.Type is INamedTypeSymbol typeSymbol
                && ContainsType(typeSymbol, symbols)
                && !typeSymbol.Equals(Symbol, SymbolEqualityComparer.Default))
            {
                var leafLabel = typeSymbol.IsGenericType ? typeSymbol.GetTypeArgumentsString() : "";
                Associations.Add(AssociationKind.Dependency.Create(Symbol, typeSymbol, leafLabel: leafLabel));
                IncludeItems.Add(typeSymbol.MetadataName);
            }
        }
    }

    private void SetInheritance(IImmutableSet<INamedTypeSymbol> symbols)
    {
        if (Symbol.BaseType is not null
            && Symbol.BaseType.SpecialType != SpecialType.System_Object
            && Symbol.BaseType.SpecialType != SpecialType.System_Enum
            && Symbol.BaseType.SpecialType != SpecialType.System_ValueType)
        {
            var rootLabel = Symbol.BaseType.IsGenericType ? Symbol.BaseType.GetTypeArgumentsString() : "";
            Associations.Add(AssociationKind.Inheritance.Create(Symbol.BaseType, Symbol, rootLabel: rootLabel));
            if (ContainsType(Symbol.BaseType, symbols))
            {
                IncludeItems.Add(Symbol.BaseType.MetadataName);
            }
        }
    }

    private void SetRealization(IImmutableSet<INamedTypeSymbol> symbols)
    {
        foreach (var type in Symbol.Interfaces)
        {
            var rootLabel = type.IsGenericType ? type.GetTypeArgumentsString() : "";
            Associations.Add(AssociationKind.Realization.Create(type, Symbol, rootLabel: rootLabel));
            if (ContainsType(type, symbols))
            {
                IncludeItems.Add(type.MetadataName);
            }
        }
    }

    private static bool ContainsType(INamedTypeSymbol symbol, IImmutableSet<INamedTypeSymbol> symbols)
    {
        var target = symbol.IsGenericType
                ? symbol.OriginalDefinition
                : symbol;
        return symbols.Contains(target);
    }

    private void SetNest(INamedTypeSymbol nestedTypeSymbol)
    {
        Associations.Add(AssociationKind.Nest.Create(Symbol, nestedTypeSymbol));
        IncludeItems.Add(nestedTypeSymbol.MetadataName);
    }
}



