﻿using System.Threading.Tasks;

using Xunit;

namespace NuGetUpdater.Core.Test.Update;

public partial class UpdateWorkerTests
{
    public class Sdk : UpdateWorkerTestBase
    {
        public Sdk()
        {
            MSBuildHelper.RegisterMSBuild();
        }

        [Theory]
        [InlineData("net472")]
        [InlineData("netstandard2.0")]
        [InlineData("net5.0")]
        public async Task UpdateVersionAttribute_InProjectFile_ForPackageReferenceInclude(string tfm)
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{tfm}</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="9.0.1" />
                  </ItemGroup>
                </Project>
                """,
                // expected
                expectedProjectContents: $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{tfm}</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdateExactMatchVersionAttribute_InProjectFile_ForPackageReferenceInclude()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: $"""
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <TargetFramework>net6.0</TargetFramework>
                    </PropertyGroup>

                    <ItemGroup>
                        <PackageReference Include="Newtonsoft.Json" Version="[9.0.1]" />
                    </ItemGroup>
                </Project>
                """,
                // expected
                expectedProjectContents: $"""
                <Project Sdk="Microsoft.NET.Sdk">
                    <PropertyGroup>
                        <TargetFramework>net6.0</TargetFramework>
                    </PropertyGroup>

                    <ItemGroup>
                        <PackageReference Include="Newtonsoft.Json" Version="[13.0.1]" />
                    </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task AddPackageReference_InProjectFile_ForTransientDependency()
        {
            // add transient System.Text.Json from 5.0.1 to 5.0.2
            await TestUpdateForProject("System.Text.Json", "5.0.1", "5.0.2", isTransitive: true,
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <TargetFramework>netcoreapp3.1</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Mongo2Go" Version="3.1.3" />
                  </ItemGroup>

                </Project>
                """,
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <TargetFramework>netcoreapp3.1</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Mongo2Go" Version="3.1.3" />
                    <PackageReference Include="System.Text.Json" Version="5.0.2" />
                  </ItemGroup>

                </Project>
                """);
        }

        [Fact]
        public async Task UpdateVersionAttribute_InProjectFile_ForAnalyzerPackageReferenceInclude()
        {
            // update Microsoft.CodeAnalysis.Analyzers from 3.3.0 to 3.3.4
            await TestUpdateForProject("Microsoft.CodeAnalysis.Analyzers", "3.3.0", "3.3.4",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.0">
                      <PrivateAssets>all</PrivateAssets>
                      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                    </PackageReference>
                  </ItemGroup>
                </Project>
                """,
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4">
                      <PrivateAssets>all</PrivateAssets>
                      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
                    </PackageReference>
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdateVersionAttribute_InProjectFile_ForMultiplePackageReferences()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.JSON" Version="9.0.1" />
                    <PackageReference Update="Newtonsoft.Json" Version="9.0.1" />
                  </ItemGroup>
                </Project>
                """,
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.JSON" Version="13.0.1" />
                    <PackageReference Update="Newtonsoft.Json" Version="13.0.1" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdateVersionAttribute_InProjectFile_ForPackageReferenceUpdate()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                    <PackageReference Update="Newtonsoft.Json" Version="9.0.1" />
                  </ItemGroup>
                </Project>
                """,
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                    <PackageReference Update="Newtonsoft.Json" Version="13.0.1" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdateVersionAttribute_InDirectoryPackages_ForPackageVersion()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="9.0.1" />
                          </ItemGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="13.0.1" />
                          </ItemGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdateExactMatchVersionAttribute_InDirectoryPackages_ForPackageVersion()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="[9.0.1]" />
                          </ItemGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="[13.0.1]" />
                          </ItemGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdatePropertyValue_InProjectFile_ForPackageReferenceInclude()
        {
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <NewtonsoftJsonPackageVersion>9.0.1</NewtonsoftJsonPackageVersion>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """,
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <NewtonsoftJsonPackageVersion>13.0.1</NewtonsoftJsonPackageVersion>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdateExactMatchPropertyValue_InProjectFile_ForPackageReferenceInclude()
        {
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <NewtonsoftJsonPackageVersion>[9.0.1]</NewtonsoftJsonPackageVersion>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """,
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <NewtonsoftJsonPackageVersion>[13.0.1]</NewtonsoftJsonPackageVersion>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdateVersionAttributeAndPropertyValue_InProjectFile_ForMultiplePackageReferences()
        {
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <NewtonsoftJsonPackageVersion>9.0.1</NewtonsoftJsonPackageVersion>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="9.0.1" />
                    <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """,
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <NewtonsoftJsonPackageVersion>13.0.1</NewtonsoftJsonPackageVersion>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                    <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdatePropertyValue_InProjectFile_ForPackageReferenceUpdate()
        {
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <NewtonsoftJsonPackageVersion>9.0.1</NewtonsoftJsonPackageVersion>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                    <PackageReference Update="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """,
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <NewtonsoftJsonPackageVersion>13.0.1</NewtonsoftJsonPackageVersion>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                    <PackageReference Update="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdatePropertyValue_InDirectoryProps_ForPackageVersion()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>9.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>13.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdateExactMatchPropertyValue_InDirectoryProps_ForPackageVersion()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>[9.0.1]</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>[13.0.1]</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdateVersionOverrideAttributeAndPropertyValue_InProjectFileAndDirectoryProps_ForPackageVersion()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" VersionOverride="9.0.1" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>9.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" VersionOverride="13.0.1" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>13.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdateVersionAttribute_InDirectoryProps_ForGlobalPackageReference()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                          </PropertyGroup>

                          <ItemGroup>
                            <GlobalPackageReference Include="Newtonsoft.Json" Version="9.0.1" />
                          </ItemGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                          </PropertyGroup>

                          <ItemGroup>
                            <GlobalPackageReference Include="Newtonsoft.Json" Version="13.0.1" />
                          </ItemGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdatePropertyValue_InDirectoryProps_ForGlobalPackageReference()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>9.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <GlobalPackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>13.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <GlobalPackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdatePropertyValue_InDirectoryProps_ForPackageReferenceInclude()
        {
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial project
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    // initial props file
                    ("Directory.Build.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonPackageVersion>9.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                },
                // expected project
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    // expected props file
                    ("Directory.Build.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonPackageVersion>13.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdatePropertyValue_InProps_ForPackageReferenceInclude()
        {
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial project
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="my-properties.props" />

                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    // initial props file
                    ("my-properties.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonPackageVersion>9.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                },
                // expected project
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="my-properties.props" />

                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    // expected props file
                    ("my-properties.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonPackageVersion>13.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdatePropertyValue_InProps_ForPackageVersion()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    // initial props files
                    ("Directory.Packages.props", """
                        <Project>
                          <Import Project="Version.props" />
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """),
                    ("Version.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonPackageVersion>9.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    // expected props files
                    ("Directory.Packages.props", """
                        <Project>
                          <Import Project="Version.props" />
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """),
                    ("Version.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonPackageVersion>13.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdatePropertyValue_InProps_ThenSubstituted_ForPackageVersion()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    // initial props files
                    ("Directory.Packages.props", """
                        <Project>
                          <Import Project="Version.props" />
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>$(NewtonsoftJsonVersion)</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """),
                    ("Version.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonVersion>9.0.1</NewtonsoftJsonVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    // expected props files
                    ("Directory.Packages.props", """
                        <Project>
                          <Import Project="Version.props" />
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>$(NewtonsoftJsonVersion)</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """),
                    ("Version.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonVersion>13.0.1</NewtonsoftJsonVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdatePropertyValues_InProps_ThenRedefinedAndSubstituted_ForPackageVersion()
        {
            // update Newtonsoft.Json from 9.0.1 to 13.0.1
            await TestUpdateForProject("Newtonsoft.Json", "9.0.1", "13.0.1",
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    // initial props files
                    ("Directory.Packages.props", """
                        <Project>
                          <Import Project="Version.props" />
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>$(NewtonsoftJsonVersion)</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """),
                    ("Version.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJSONVersion>9.0.1</NewtonsoftJSONVersion>
                            <NewtonsoftJsonPackageVersion>9.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Newtonsoft.Json" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    // expected props files
                    ("Directory.Packages.props", """
                        <Project>
                          <Import Project="Version.props" />
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <NewtonsoftJsonPackageVersion>$(NewtonsoftJsonVersion)</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>

                          <ItemGroup>
                            <PackageVersion Include="Newtonsoft.Json" Version="$(NewtonsoftJsonPackageVersion)" />
                          </ItemGroup>
                        </Project>
                        """),
                    ("Version.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJSONVersion>13.0.1</NewtonsoftJSONVersion>
                            <NewtonsoftJsonPackageVersion>13.0.1</NewtonsoftJsonPackageVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdatePeerDependencyWithInlineVersion()
        {
            await TestUpdateForProject("Microsoft.Extensions.Http", "2.2.0", "7.0.0",
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Http" Version="2.2.0" />
                    <PackageReference Include="Microsoft.Extensions.Logging" Version="2.2.0" />
                  </ItemGroup>
                </Project>
                """,
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Http" Version="7.0.0" />
                    <PackageReference Include="Microsoft.Extensions.Logging" Version="7.0.0" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdatePeerDependencyFromPropertyInSameFile()
        {
            await TestUpdateForProject("Microsoft.Extensions.Http", "2.2.0", "7.0.0",
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <MicrosoftExtensionsHttpVersion>2.2.0</MicrosoftExtensionsHttpVersion>
                    <MicrosoftExtensionsLoggingVersion>2.2.0</MicrosoftExtensionsLoggingVersion>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Http" Version="$(MicrosoftExtensionsHttpVersion)" />
                    <PackageReference Include="Microsoft.Extensions.Logging" Version="$(MicrosoftExtensionsLoggingVersion)" />
                  </ItemGroup>
                </Project>
                """,
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                    <MicrosoftExtensionsHttpVersion>7.0.0</MicrosoftExtensionsHttpVersion>
                    <MicrosoftExtensionsLoggingVersion>7.0.0</MicrosoftExtensionsLoggingVersion>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Http" Version="$(MicrosoftExtensionsHttpVersion)" />
                    <PackageReference Include="Microsoft.Extensions.Logging" Version="$(MicrosoftExtensionsLoggingVersion)" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdatePeerDependencyFromPropertyInDifferentFile()
        {
            await TestUpdateForProject("Microsoft.Extensions.Http", "2.2.0", "7.0.0",
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="Versions.props" />
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Http" Version="$(MicrosoftExtensionsHttpVersion)" />
                    <PackageReference Include="Microsoft.Extensions.Logging" Version="$(MicrosoftExtensionsLoggingVersion)" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    ("Versions.props", """
                        <Project>
                          <PropertyGroup>
                            <MicrosoftExtensionsHttpVersion>2.2.0</MicrosoftExtensionsHttpVersion>
                            <MicrosoftExtensionsLoggingVersion>2.2.0</MicrosoftExtensionsLoggingVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                },
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="Versions.props" />
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Http" Version="$(MicrosoftExtensionsHttpVersion)" />
                    <PackageReference Include="Microsoft.Extensions.Logging" Version="$(MicrosoftExtensionsLoggingVersion)" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    ("Versions.props", """
                        <Project>
                          <PropertyGroup>
                            <MicrosoftExtensionsHttpVersion>7.0.0</MicrosoftExtensionsHttpVersion>
                            <MicrosoftExtensionsLoggingVersion>7.0.0</MicrosoftExtensionsLoggingVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdatePeerDependencyWithInlineVersionAndMultipleTfms()
        {
            await TestUpdateForProject("Microsoft.Extensions.Http", "2.2.0", "7.0.0",
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>netstandard2.0;netstandard2.1</TargetFrameworks>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Http" Version="2.2.0" />
                    <PackageReference Include="Microsoft.Extensions.Logging" Version="2.2.0" />
                  </ItemGroup>
                </Project>
                """,
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>netstandard2.0;netstandard2.1</TargetFrameworks>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Http" Version="7.0.0" />
                    <PackageReference Include="Microsoft.Extensions.Logging" Version="7.0.0" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task NoUpdateForPeerDependenciesWhichAreHigherVersion()
        {
            await TestUpdateForProject("Microsoft.Identity.Web", "2.13.0", "2.13.2",
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Azure.Identity" />
                    <PackageReference Include="Azure.Security.KeyVault.Keys" />
                    <PackageReference Include="Azure.Security.KeyVault.Secrets" />
                    <PackageReference Include="Microsoft.Identity.Web" />
                  </ItemGroup>
                </Project>
                """,
                additionalFiles: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageVersion Include="Azure.Identity" Version="1.9.0" />
                            <PackageVersion Include="Azure.Security.KeyVault.Keys" Version="4.5.0" />
                            <PackageVersion Include="Azure.Security.KeyVault.Secrets" Version="4.5.0" />
                            <PackageVersion Include="Microsoft.Identity.Web" Version="2.13.0" />
                          </ItemGroup>
                        </Project>
                        """)
                },
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Azure.Identity" />
                    <PackageReference Include="Azure.Security.KeyVault.Keys" />
                    <PackageReference Include="Azure.Security.KeyVault.Secrets" />
                    <PackageReference Include="Microsoft.Identity.Web" />
                  </ItemGroup>
                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageVersion Include="Azure.Identity" Version="1.9.0" />
                            <PackageVersion Include="Azure.Security.KeyVault.Keys" Version="4.5.0" />
                            <PackageVersion Include="Azure.Security.KeyVault.Secrets" Version="4.5.0" />
                            <PackageVersion Include="Microsoft.Identity.Web" Version="2.13.2" />
                          </ItemGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task UpdatingToNotCompatiblePackageDoesNothing()
        {
            await TestUpdateForProject("Microsoft.AspNetCore.Authentication.JwtBearer", "3.1.18", "7.0.5",
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netcoreapp3.1</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="3.1.18" />
                  </ItemGroup>
                </Project>
                """,
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netcoreapp3.1</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="3.1.18" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdatingToNotCompatiblePackageDoesNothingWithSingleOfMultileTfmNotSupported()
        {
            // the requested package upgrade is supported on net7.0, but not netcoreapp3.1, so we skip the whole thing
            await TestUpdateForProject("Microsoft.AspNetCore.Authentication.JwtBearer", "3.1.18", "7.0.5",
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>netcoreapp3.1;net7.0</TargetFrameworks>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="3.1.18" />
                  </ItemGroup>
                </Project>
                """,
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>netcoreapp3.1;net7.0</TargetFrameworks>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="3.1.18" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task UpdateOfNonExistantPackageDoesNothingEvenIfTransitiveDependencyIsPresent()
        {
            // package Microsoft.Extensions.Http isn't present, but one of its transitive dependencies is
            await TestUpdateForProject("Microsoft.Extensions.Http", "2.2.0", "7.0.0",
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="2.2.0" />
                  </ItemGroup>
                </Project>
                """,
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>netstandard2.0</TargetFramework>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="2.2.0" />
                  </ItemGroup>
                </Project>
                """);
        }

        [Fact]
        public async Task AvoidPackageDowngradeWhenUpdatingDependency()
        {
            await TestUpdateForProject("Microsoft.VisualStudio.Sdk.TestFramework.Xunit", "17.2.7", "17.6.16",
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <TargetFramework>$(PreferredTargetFramework)</TargetFramework>
                    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
                    <RootNamespace />
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Microsoft.NET.Test.Sdk" />
                    <PackageReference Include="Microsoft.VisualStudio.Sdk.TestFramework" />
                    <PackageReference Include="Microsoft.VisualStudio.Sdk.TestFramework.Xunit" />
                    <PackageReference Include="Moq" />
                    <PackageReference Include="xunit.runner.visualstudio" />
                    <PackageReference Include="xunit" />
                  </ItemGroup>

                </Project>
                """,
                additionalFiles: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.6.3" />
                            <PackageVersion Include="Microsoft.VisualStudio.Sdk.TestFramework" Version="17.2.7" />
                            <PackageVersion Include="Microsoft.VisualStudio.Sdk.TestFramework.Xunit" Version="17.2.7" />
                            <PackageVersion Include="Microsoft.VisualStudio.Shell.15.0" Version="17.6.36389" />
                            <PackageVersion Include="Microsoft.VisualStudio.Text.Data" Version="17.6.268" />
                            <PackageVersion Include="Moq" Version="4.18.2" />
                            <PackageVersion Include="xunit" Version="2.5.0" />
                            <PackageVersion Include="xunit.runner.visualstudio" Version="2.5.0" />
                          </ItemGroup>
                        </Project>
                        """),
                    ("Directory.Build.props", """
                        <Project>
                          <PropertyGroup>
                            <PreferredTargetFramework>net7.0</PreferredTargetFramework>
                          </PropertyGroup>
                        </Project>
                        """)
                },
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <TargetFramework>$(PreferredTargetFramework)</TargetFramework>
                    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
                    <RootNamespace />
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Microsoft.NET.Test.Sdk" />
                    <PackageReference Include="Microsoft.VisualStudio.Sdk.TestFramework" />
                    <PackageReference Include="Microsoft.VisualStudio.Sdk.TestFramework.Xunit" />
                    <PackageReference Include="Moq" />
                    <PackageReference Include="xunit.runner.visualstudio" />
                    <PackageReference Include="xunit" />
                  </ItemGroup>

                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.6.3" />
                            <PackageVersion Include="Microsoft.VisualStudio.Sdk.TestFramework" Version="17.6.16" />
                            <PackageVersion Include="Microsoft.VisualStudio.Sdk.TestFramework.Xunit" Version="17.6.16" />
                            <PackageVersion Include="Microsoft.VisualStudio.Shell.15.0" Version="17.6.36389" />
                            <PackageVersion Include="Microsoft.VisualStudio.Text.Data" Version="17.6.268" />
                            <PackageVersion Include="Moq" Version="4.18.4" />
                            <PackageVersion Include="xunit" Version="2.5.0" />
                            <PackageVersion Include="xunit.runner.visualstudio" Version="2.5.0" />
                          </ItemGroup>
                        </Project>
                        """),
                    ("Directory.Build.props", """
                        <Project>
                          <PropertyGroup>
                            <PreferredTargetFramework>net7.0</PreferredTargetFramework>
                          </PropertyGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task AddTransitiveDependencyByAddingPackageReferenceAndVersion()
        {
            await TestUpdateForProject("System.Text.Json", "5.0.0", "5.0.2", isTransitive: true,
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <TargetFramework>net5.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Mongo2Go" />
                  </ItemGroup>

                </Project>
                """,
                additionalFiles: new[]
                {
                    // initial props files
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageVersion Include="Mongo2Go" Version="3.1.3" />
                          </ItemGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <TargetFramework>net5.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Mongo2Go" />
                    <PackageReference Include="System.Text.Json" />
                  </ItemGroup>

                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    // expected props files
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageVersion Include="Mongo2Go" Version="3.1.3" />
                            <PackageVersion Include="System.Text.Json" Version="5.0.2" />
                          </ItemGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task PinTransitiveDependencyByAddingPackageVersion()
        {
            await TestUpdateForProject("System.Text.Json", "5.0.0", "5.0.2", isTransitive: true,
                // initial
                projectContents: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <TargetFramework>net5.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Mongo2Go" />
                  </ItemGroup>

                </Project>
                """,
                additionalFiles: new[]
                {
                    // initial props files
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageVersion Include="Mongo2Go" Version="3.1.3" />
                          </ItemGroup>
                        </Project>
                        """)
                },
                // expected
                expectedProjectContents: """
                <Project Sdk="Microsoft.NET.Sdk">

                  <PropertyGroup>
                    <TargetFramework>net5.0</TargetFramework>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="Mongo2Go" />
                  </ItemGroup>

                </Project>
                """,
                additionalFilesExpected: new[]
                {
                    // expected props files
                    ("Directory.Packages.props", """
                        <Project>
                          <PropertyGroup>
                            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                            <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageVersion Include="Mongo2Go" Version="3.1.3" />
                            <PackageVersion Include="System.Text.Json" Version="5.0.2" />
                          </ItemGroup>
                        </Project>
                        """)
                });
        }

        [Fact]
        public async Task PropsFileNameWithDifferentCasing()
        {
            await TestUpdateForProject("Newtonsoft.Json", "12.0.1", "13.0.1",
                projectContents: """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net7.0</TargetFramework>
                      </PropertyGroup>
                      <ItemGroup>
                        <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonVersion)" />
                      </ItemGroup>
                    </Project>
                    """,
                additionalFiles: new[]
                {
                    ("Directory.Build.props", """
                        <Project>
                          <Import Project="Versions.Props" />
                        </Project>
                        """),
                    // notice the uppercase 'P' in the file name
                    ("Versions.Props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonVersion>12.0.1</NewtonsoftJsonVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                },
                // no change
                expectedProjectContents: """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net7.0</TargetFramework>
                      </PropertyGroup>
                      <ItemGroup>
                        <PackageReference Include="Newtonsoft.Json" Version="$(NewtonsoftJsonVersion)" />
                      </ItemGroup>
                    </Project>
                    """,
                additionalFilesExpected: new[]
                {
                    // no change
                    ("Directory.Build.props", """
                        <Project>
                          <Import Project="Versions.Props" />
                        </Project>
                        """),
                    // version number was updated here
                    ("Versions.Props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonVersion>13.0.1</NewtonsoftJsonVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                }
            );
        }

        [Fact]
        public async Task VersionAttributeWithDifferentCasing_VersionNumberInline()
        {
            // the version attribute in the project has an all lowercase name
            await TestUpdateForProject("Newtonsoft.Json", "12.0.1", "13.0.1",
                projectContents: """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net7.0</TargetFramework>
                      </PropertyGroup>
                      <ItemGroup>
                        <PackageReference Include="Newtonsoft.Json" version="12.0.1" />
                      </ItemGroup>
                    </Project>
                    """,
                expectedProjectContents: """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net7.0</TargetFramework>
                      </PropertyGroup>
                      <ItemGroup>
                        <PackageReference Include="Newtonsoft.Json" version="13.0.1" />
                      </ItemGroup>
                    </Project>
                    """
            );
        }

        [Fact]
        public async Task VersionAttributeWithDifferentCasing_VersionNumberInProperty()
        {
            // the version attribute in the project has an all lowercase name
            await TestUpdateForProject("Newtonsoft.Json", "12.0.1", "13.0.1",
                projectContents: """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net7.0</TargetFramework>
                      </PropertyGroup>
                      <ItemGroup>
                        <PackageReference Include="Newtonsoft.Json" version="$(NewtonsoftJsonVersion)" />
                      </ItemGroup>
                    </Project>
                    """,
                additionalFiles: new[]
                {
                    ("Directory.Build.props", """
                        <Project>
                          <Import Project="Versions.props" />
                        </Project>
                        """),
                    ("Versions.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonVersion>12.0.1</NewtonsoftJsonVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                },
                // no change
                expectedProjectContents: """
                    <Project Sdk="Microsoft.NET.Sdk">
                      <PropertyGroup>
                        <TargetFramework>net7.0</TargetFramework>
                      </PropertyGroup>
                      <ItemGroup>
                        <PackageReference Include="Newtonsoft.Json" version="$(NewtonsoftJsonVersion)" />
                      </ItemGroup>
                    </Project>
                    """,
                additionalFilesExpected: new[]
                {
                    // no change
                    ("Directory.Build.props", """
                        <Project>
                          <Import Project="Versions.props" />
                        </Project>
                        """),
                    // version number was updated here
                    ("Versions.props", """
                        <Project>
                          <PropertyGroup>
                            <NewtonsoftJsonVersion>13.0.1</NewtonsoftJsonVersion>
                          </PropertyGroup>
                        </Project>
                        """)
                }
            );
        }
    }
}
