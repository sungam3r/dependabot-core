﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Xunit;

namespace NuGetUpdater.Core.Test
{
    public class EndToEndTestBase
    {
        protected static async Task TestUpdateForProject(
            string dependencyName,
            string oldVersion,
            string newVersion,
            string projectContents,
            string expectedProjectContents,
            (string Path, string Content)[]? additionalFiles = null,
            (string Path, string Content)[]? additionalFilesExpected = null)
        {
            var projectName = "test-project";
            var projectFileName = $"{projectName}.csproj";
            var slnName = "test-solution.sln";
            var slnContent = $$"""
                Microsoft Visual Studio Solution File, Format Version 12.00
                # Visual Studio 14
                VisualStudioVersion = 14.0.22705.0
                MinimumVisualStudioVersion = 10.0.40219.1
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "{{projectName}}", "{{projectFileName}}", "{782E0C0A-10D3-444D-9640-263D03D2B20C}"
                EndProject
                Global
                	GlobalSection(SolutionConfigurationPlatforms) = preSolution
                		Debug|Any CPU = Debug|Any CPU
                		Release|Any CPU = Release|Any CPU
                	EndGlobalSection
                	GlobalSection(ProjectConfigurationPlatforms) = postSolution
                		{782E0C0A-10D3-444D-9640-263D03D2B20C}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                		{782E0C0A-10D3-444D-9640-263D03D2B20C}.Debug|Any CPU.Build.0 = Debug|Any CPU
                		{782E0C0A-10D3-444D-9640-263D03D2B20C}.Release|Any CPU.ActiveCfg = Release|Any CPU
                		{782E0C0A-10D3-444D-9640-263D03D2B20C}.Release|Any CPU.Build.0 = Release|Any CPU
                	EndGlobalSection
                	GlobalSection(SolutionProperties) = preSolution
                		HideSolutionNode = FALSE
                	EndGlobalSection
                EndGlobal
                """;
            var testFiles = new List<(string Path, string Content)>()
            {
                (slnName, slnContent),
                (projectFileName, projectContents),
            };
            if (additionalFiles is not null)
            {
                testFiles.AddRange(additionalFiles);
            }

            var actualResult = await RunUpdate(testFiles.ToArray(), async (temporaryDirectory) =>
            {
                var slnPath = Path.Combine(temporaryDirectory, slnName);
                var worker = new NuGetUpdaterWorker(verbose: true);
                await worker.RunAsync(temporaryDirectory, slnPath, dependencyName, oldVersion, newVersion);
            });

            var expectedResult = new List<(string Path, string Content)>()
            {
                (projectFileName, expectedProjectContents)
            };
            if (additionalFilesExpected is not null)
            {
                expectedResult.AddRange(additionalFilesExpected);
            }

            AssertContainsFiles(expectedResult.ToArray(), actualResult);
        }

        protected static async Task<(string Path, string Content)[]> RunUpdate((string Path, string Content)[] files, Func<string, Task> action)
        {
            // write initial files
            using var tempDir = new TemporaryDirectory();
            foreach (var file in files)
            {
                var localPath = file.Path.StartsWith("/") ? file.Path[1..] : file.Path; // remove path rooting character
                var filePath = Path.Combine(tempDir.DirectoryPath, localPath);
                var directoryPath = Path.GetDirectoryName(filePath);
                Directory.CreateDirectory(directoryPath!);
                await File.WriteAllTextAsync(filePath, file.Content);
            }

            // run update
            await action(tempDir.DirectoryPath);

            // gather results
            var expectedFiles = new HashSet<string>(files.Select(f => f.Path));
            var result = new List<(string Path, string Content)>();
            foreach (var file in Directory.GetFiles(tempDir.DirectoryPath, "*.*", SearchOption.AllDirectories))
            {
                var localPath = file.StartsWith(tempDir.DirectoryPath)
                    ? file[tempDir.DirectoryPath.Length..]
                    : file; // how did this happen?
                localPath = localPath.Replace("\\", "/");
                if (localPath.StartsWith('/'))
                {
                    localPath = localPath[1..];
                }

                if (expectedFiles.Contains(localPath))
                {
                    var content = await File.ReadAllTextAsync(file);
                    result.Add((localPath, content));
                }
            }

            return result.ToArray();
        }

        protected static void AssertEqualFiles((string Path, string Content)[] expected, (string Path, string Content)[] actual)
        {
            Assert.Equal(expected.Length, actual.Length);
            AssertContainsFiles(expected, actual);
        }

        protected static void AssertContainsFiles((string Path, string Content)[] expected, (string Path, string Content)[] actual)
        {
            var actualContents = actual.ToDictionary(pair => pair.Path, pair => pair.Content);
            foreach (var expectedPair in expected)
            {
                var actualContent = actualContents[expectedPair.Path];
                var expectedContent = expectedPair.Content;
                Assert.Equal(expectedContent.Replace("\r", ""), actualContent.Replace("\r", "")); // normalize line endings
            }
        }
    }
}
