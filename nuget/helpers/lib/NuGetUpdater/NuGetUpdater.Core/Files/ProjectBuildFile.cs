using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Language.Xml;

namespace NuGetUpdater.Core;

internal sealed class ProjectBuildFile : XmlBuildFile
{
    public static ProjectBuildFile Open(string repoRootPath, string path)
        => Parse(repoRootPath, path, File.ReadAllText(path));
    public static ProjectBuildFile Parse(string repoRootPath, string path, string xml)
        => new(repoRootPath, path, Parser.ParseText(xml));

    public ProjectBuildFile(string repoRootPath, string path, XmlDocumentSyntax contents)
        : base(repoRootPath, path, contents)
    {
    }

    public IEnumerable<IXmlElementSyntax> PropertyNodes => CurrentContents.RootSyntax
        .GetElements("PropertyGroup", StringComparison.OrdinalIgnoreCase)
        .SelectMany(e => e.Elements);

    public IEnumerable<KeyValuePair<string, string>> GetProperties() => PropertyNodes
        .Select(e => new KeyValuePair<string, string>(e.Name, e.GetContentValue()));

    public IEnumerable<IXmlElementSyntax> ItemNodes => CurrentContents.RootSyntax
        .GetElements("ItemGroup", StringComparison.OrdinalIgnoreCase)
        .SelectMany(e => e.Elements);

    public IEnumerable<IXmlElementSyntax> PackageItemNodes => ItemNodes.Where(e =>
            e.Name.Equals("PackageReference", StringComparison.OrdinalIgnoreCase) ||
            e.Name.Equals("GlobalPackageReference", StringComparison.OrdinalIgnoreCase) ||
            e.Name.Equals("PackageVersion", StringComparison.OrdinalIgnoreCase));

    public IEnumerable<Dependency> GetDependencies() => PackageItemNodes
        .Select(GetDependency)
        .OfType<Dependency>();

    private static Dependency? GetDependency(IXmlElementSyntax element)
    {
        var name = element.GetAttributeOrSubElementValue("Include", StringComparison.OrdinalIgnoreCase)
            ?? element.GetAttributeOrSubElementValue("Update", StringComparison.OrdinalIgnoreCase);
        if (name is null || name.StartsWith("@("))
        {
            return null;
        }

        var isVersionOverride = false;
        var version = element.GetAttributeOrSubElementValue("Version", StringComparison.OrdinalIgnoreCase);
        if (version is null)
        {
            version = element.GetAttributeOrSubElementValue("VersionOverride", StringComparison.OrdinalIgnoreCase);
            isVersionOverride = version is not null;
        }

        return new Dependency(
            Name: name,
            Version: version?.Length == 0 ? null : version,
            Type: GetDependencyType(element.Name),
            IsOverride: isVersionOverride);
    }

    private static DependencyType GetDependencyType(string name)
    {
        return name.ToLower() switch
        {
            "packagereference" => DependencyType.PackageReference,
            "globalpackagereference" => DependencyType.GlobalPackageReference,
            "packageversion" => DependencyType.PackageVersion,
            _ => throw new InvalidOperationException($"Unknown dependency type: {name}")
        };
    }

    public IEnumerable<string> GetReferencedProjectPaths() => ItemNodes
        .Where(e =>
            e.Name.Equals("ProjectReference", StringComparison.OrdinalIgnoreCase) ||
            e.Name.Equals("ProjectFile", StringComparison.OrdinalIgnoreCase))
        .Select(e => PathHelper.GetFullPathFromRelative(System.IO.Path.GetDirectoryName(Path)!, e.GetAttribute("Include").Value));

    public void NormalizeDirectorySeparatorsInProject()
    {
        var hintPathNodes = CurrentContents.Descendants()
            .Where(e =>
                e.Name.Equals("HintPath", StringComparison.OrdinalIgnoreCase) &&
                e.Parent.Name.Equals("Reference", StringComparison.OrdinalIgnoreCase))
            .Select(e => (XmlElementSyntax)e.AsNode);
        var updatedXml = CurrentContents.ReplaceNodes(hintPathNodes,
            (_, n) => n.WithContent(n.GetContentValue().Replace("/", "\\")).AsNode);
        Update(updatedXml);
    }
}