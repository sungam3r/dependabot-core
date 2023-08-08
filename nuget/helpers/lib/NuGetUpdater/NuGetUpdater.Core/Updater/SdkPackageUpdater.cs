﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Language.Xml;

using NuGet.Versioning;

namespace NuGetUpdater.Core;

internal static partial class SdkPackageUpdater
{
    public static async Task UpdateDependencyAsync(string repoRootPath, string projectPath, string dependencyName, string previousDependencyVersion, string newDependencyVersion, bool isTransitive, Logger logger)
    {
        // SDK-style project, modify the XML directly
        logger.Log("  Running for SDK-style project");
        var buildFiles = LoadBuildFiles(repoRootPath, projectPath);

        // update all dependencies, including transitive
        var tfms = MSBuildHelper.GetTargetFrameworkMonikers(buildFiles);

        // Get the set of all top-level dependencies in the current project
        var topLevelDependencies = MSBuildHelper.GetTopLevelPackageDependenyInfos(buildFiles).ToArray();
        var packageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tfm in tfms)
        {
            var dependencies = await MSBuildHelper.GetAllPackageDependenciesAsync(repoRootPath, tfm, topLevelDependencies);
            foreach (var d in dependencies.Select(static d => d.PackageName))
            {
                packageNames.Add(d);
            }
        }

        // Skip updating the project if the dependency does not exist in the graph
        if (!packageNames.Contains(dependencyName))
        {
            logger.Log($"    Package [{dependencyName}] Does not exist as a dependency in [{projectPath}].");
            return;
        }

        var tfmsAndDependencies = new Dictionary<string, (string PackageName, string Version)[]>();
        foreach (var tfm in tfms)
        {
            var dependencies = await MSBuildHelper.GetAllPackageDependenciesAsync(repoRootPath, tfm, new[] { (dependencyName, newDependencyVersion) });
            tfmsAndDependencies[tfm] = dependencies;
        }

        // stop update process if we find conflicting package versions
        var conflictingPackageVersionsFound = false;
        var packagesAndVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tfm, dependencies) in tfmsAndDependencies)
        {
            foreach (var (packageName, packageVersion) in dependencies)
            {
                if (packagesAndVersions.TryGetValue(packageName, out var existingVersion) &&
                    existingVersion != packageVersion)
                {
                    logger.Log($"    Package [{packageName}] tried to update to version [{packageVersion}], but found conflicting package version of [{existingVersion}].");
                    conflictingPackageVersionsFound = true;
                }
                else
                {
                    packagesAndVersions[packageName] = packageVersion;
                }
            }
        }

        if (conflictingPackageVersionsFound)
        {
            return;
        }

        var unupgradableTfms = tfmsAndDependencies.Where(kvp => !kvp.Value.Any()).Select(kvp => kvp.Key);
        if (unupgradableTfms.Any())
        {
            logger.Log($"    The following target frameworks could not find packages to upgrade: {string.Join(", ", unupgradableTfms)}");
            return;
        }

        if (isTransitive)
        {
            await AddTransitiveDependencyAsync(projectPath, dependencyName, previousDependencyVersion, newDependencyVersion, logger);
        }
        else
        {
            await UpdateTopLevelDepdendencyAsync(buildFiles, dependencyName, previousDependencyVersion, newDependencyVersion, packagesAndVersions, logger);
        }
    }

    private static async Task AddTransitiveDependencyAsync(string projectPath, string dependencyName, string previousDependencyVersion, string newDependencyVersion, Logger logger)
    {
        logger.Log($"    Adding [{dependencyName}/{previousDependencyVersion}] as a top-level package reference.");

        // see https://learn.microsoft.com/nuget/consume-packages/install-use-packages-dotnet-cli
        var (exitCode, stdout, stderr) = await ProcessEx.RunAsync("dotnet", $"add {projectPath} package {dependencyName} --version {newDependencyVersion}");
        if (exitCode != 0)
        {
            logger.Log($"    Transient dependency [{dependencyName}/{previousDependencyVersion}] was not added.");
        }
    }

    private static async Task UpdateTopLevelDepdendencyAsync(ImmutableArray<BuildFile> buildFiles, string dependencyName, string previousDependencyVersion, string newDependencyVersion, Dictionary<string, string> packagesAndVersions, Logger logger)
    {
        var result = TryUpdateDependencyVersion(buildFiles, dependencyName, previousDependencyVersion, newDependencyVersion, logger);
        if (result == UpdateResult.NotFound)
        {
            logger.Log($"    Root package [{dependencyName}/{previousDependencyVersion}] was not updated; skipping dependencies.");
            return;
        }

        foreach (var (packageName, packageVersion) in packagesAndVersions.Where(kvp => string.Compare(kvp.Key, dependencyName, StringComparison.OrdinalIgnoreCase) != 0))
        {
            TryUpdateDependencyVersion(buildFiles, packageName, previousDependencyVersion: null, newDependencyVersion: packageVersion, logger);
        }

        foreach (var buildFile in buildFiles)
        {
            if (await buildFile.SaveAsync())
            {
                logger.Log($"    Saved [{buildFile.RepoRelativePath}].");
            }
        }
    }

    private static ImmutableArray<BuildFile> LoadBuildFiles(string repoRootPath, string projectPath)
    {
        return new string[] { projectPath }
            .Concat(Directory.EnumerateFiles(repoRootPath, "*.props", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(repoRootPath, "*.targets", SearchOption.AllDirectories))
            .Select(path => new BuildFile(repoRootPath, path, Parser.ParseText(File.ReadAllText(path))))
            .ToImmutableArray();
    }

    private static UpdateResult TryUpdateDependencyVersion(ImmutableArray<BuildFile> buildFiles, string dependencyName, string? previousDependencyVersion, string newDependencyVersion, Logger logger)
    {
        var foundCorrect = false;
        var updateWasPerformed = false;
        var propertyNames = new List<string>();

        // First we locate all the PackageReference, GlobalPackageReference, or PackageVersion which set the Version
        // or VersionOverride attribute. In the simplest case we can update the version attribute directly then move
        // on. When property substitution is used we have to additionally search for the property containing the version.

        foreach (var buildFile in buildFiles)
        {
            var updateAttributes = new List<XmlAttributeSyntax>();
            var packageNodes = FindPackageNode(buildFile.Xml, dependencyName);

            foreach (var packageNode in packageNodes)
            {
                var versionAttribute = packageNode.GetAttribute("Version") ?? packageNode.GetAttribute("VersionOverride");

                // Is this the case where version is specified with property substitution?
                if (versionAttribute.Value.StartsWith("$(") && versionAttribute.Value.EndsWith(")"))
                {
                    propertyNames.Add(versionAttribute.Value.Substring(2, versionAttribute.Value.Length - 3));
                }
                // Is this the case that the version is specified directly in the package node?
                else if (versionAttribute.Value == previousDependencyVersion)
                {
                    logger.Log($"    Found incorrect [{packageNode.Name}] version attribute in [{buildFile.RepoRelativePath}].");
                    updateAttributes.Add(versionAttribute);
                }
                else if (previousDependencyVersion == null && SemanticVersion.TryParse(versionAttribute.Value, out var previousVersion))
                {
                    var newVersion = SemanticVersion.Parse(newDependencyVersion);
                    if (previousVersion < newVersion)
                    {
                        logger.Log($"    Found incorrect peer [{packageNode.Name}] version attribute in [{buildFile.RepoRelativePath}].");
                        updateAttributes.Add(versionAttribute);
                    }
                }
                else if (versionAttribute.Value == newDependencyVersion)
                {
                    logger.Log($"    Found correct [{packageNode.Name}] version attribute in [{buildFile.RepoRelativePath}].");
                    foundCorrect = true;
                }
            }

            if (updateAttributes.Count > 0)
            {
                var updatedXml = buildFile.Xml
                    .ReplaceNodes(updateAttributes, (o, n) => n.WithValue(newDependencyVersion));
                buildFile.Update(updatedXml);
                updateWasPerformed = true;
            }
        }

        // If property substitution was used to set the Version, we must search for the property containing
        // the version string. Since it could also be populated by property substitution this search repeats
        // with the each new property name until the version string is located.

        var processedPropertyNames = new HashSet<string>();

        for (int propertyNameIndex = 0; propertyNameIndex < propertyNames.Count; propertyNameIndex++)
        {
            var propertyName = propertyNames[propertyNameIndex];
            if (processedPropertyNames.Contains(propertyName))
            {
                continue;
            }

            processedPropertyNames.Add(propertyName);

            foreach (var buildFile in buildFiles)
            {
                var updateProperties = new List<XmlElementSyntax>();
                var propertyElements = buildFile.Xml
                    .Descendants()
                    .Where(e => e.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

                foreach (var propertyElement in propertyElements)
                {
                    var propertyContents = propertyElement.GetContentValue();

                    // Is this the case where this property contains another property substitution?
                    if (propertyContents.StartsWith("$(") && propertyContents.EndsWith(")"))
                    {
                        propertyNames.Add(propertyContents.Substring(2, propertyContents.Length - 3));
                    }
                    // Is this the case that the property contains the version?
                    else if (propertyContents == previousDependencyVersion)
                    {
                        logger.Log($"    Found incorrect version property [{propertyElement.Name}] in [{buildFile.RepoRelativePath}].");
                        updateProperties.Add((XmlElementSyntax)propertyElement.AsNode);
                    }
                    else if (previousDependencyVersion is null && SemanticVersion.TryParse(propertyContents, out var previousVersion))
                    {
                        var newVersion = SemanticVersion.Parse(newDependencyVersion);
                        if (previousVersion < newVersion)
                        {
                            logger.Log($"    Found incorrect peer version property [{propertyElement.Name}] in [{buildFile.RepoRelativePath}].");
                            updateProperties.Add((XmlElementSyntax)propertyElement.AsNode);
                        }
                    }
                    else if (propertyContents == newDependencyVersion)
                    {
                        logger.Log($"    Found correct version property [{propertyElement.Name}] in [{buildFile.RepoRelativePath}].");
                        foundCorrect = true;
                    }
                }

                if (updateProperties.Count > 0)
                {
                    var updatedXml = buildFile.Xml
                        .ReplaceNodes(updateProperties, (o, n) => n.WithContent(newDependencyVersion));
                    buildFile.Update(updatedXml);
                    updateWasPerformed = true;
                }
            }
        }

        return updateWasPerformed
            ? UpdateResult.Updated
            : foundCorrect
                ? UpdateResult.Correct
                : UpdateResult.NotFound;
    }

    private static IEnumerable<IXmlElementSyntax> FindPackageNode(XmlDocumentSyntax xml, string packageName)
    {
        return xml.Descendants().Where(e =>
            (e.Name == "PackageReference" || e.Name == "GlobalPackageReference" || e.Name == "PackageVersion") &&
            string.Equals(e.GetAttributeValue("Include") ?? e.GetAttributeValue("Update"), packageName, StringComparison.OrdinalIgnoreCase) &&
            (e.GetAttribute("Version") ?? e.GetAttribute("VersionOverride")) is not null);
    }
}