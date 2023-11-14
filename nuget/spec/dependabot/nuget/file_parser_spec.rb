# typed: false
# frozen_string_literal: true

require "spec_helper"
require "dependabot/dependency_file"
require "dependabot/source"
require "dependabot/nuget/file_parser"
require_common_spec "file_parsers/shared_examples_for_file_parsers"

RSpec.describe Dependabot::Nuget::FileParser, :vcr do
  it_behaves_like "a dependency file parser"

  let(:files) { [csproj_file] }
  let(:csproj_file) do
    Dependabot::DependencyFile.new(name: "my.csproj", content: csproj_body)
  end
  let(:csproj_body) { fixture("csproj", "basic.csproj") }
  let(:parser) { described_class.new(dependency_files: files, source: source) }
  let(:source) do
    Dependabot::Source.new(
      provider: "github",
      repo: "gocardless/bump",
      directory: "/"
    )
  end

  describe "parse" do
    let(:dependencies) { parser.parse }
    subject(:top_level_dependencies) { dependencies.select(&:top_level?) }

    its(:length) { is_expected.to eq(5) }

    describe "the first dependency" do
      subject(:dependency) { top_level_dependencies.first }

      it "has the right details" do
        expect(dependency).to be_a(Dependabot::Dependency)
        expect(dependency.name).to eq("Microsoft.Extensions.DependencyModel")
        expect(dependency.version).to eq("1.1.1")
        expect(dependency.requirements).to eq(
          [{
            requirement: "1.1.1",
            file: "my.csproj",
            groups: ["dependencies"],
            source: nil
          }]
        )
      end
    end

    describe "the last dependency" do
      subject(:dependency) { top_level_dependencies.last }

      it "has the right details" do
        expect(dependency).to be_a(Dependabot::Dependency)
        expect(dependency.name).to eq("System.Collections.Specialized")
        expect(dependency.version).to eq("4.3.0")
        expect(dependency.requirements).to eq(
          [{
            requirement: "4.3.0",
            file: "my.csproj",
            groups: ["dependencies"],
            source: nil
          }]
        )
      end
    end

    context "with a csproj and a vbproj" do
      let(:files) { [csproj_file, vbproj_file] }
      let(:vbproj_file) do
        Dependabot::DependencyFile.new(
          name: "my.vbproj",
          content: fixture("csproj", "basic2.csproj")
        )
      end

      its(:length) { is_expected.to eq(6) }

      describe "the first dependency" do
        subject(:dependency) { top_level_dependencies.first }

        it "has the right details" do
          expect(dependency).to be_a(Dependabot::Dependency)
          expect(dependency.name).to eq("Microsoft.Extensions.DependencyModel")
          expect(dependency.version).to eq("1.0.1")
          expect(dependency.requirements).to eq(
            [{
              requirement: "1.1.1",
              file: "my.csproj",
              groups: ["dependencies"],
              source: nil
            }, {
              requirement: "1.0.1",
              file: "my.vbproj",
              groups: ["dependencies"],
              source: nil
            }]
          )
        end
      end

      describe "the last dependency" do
        subject(:dependency) { top_level_dependencies.last }

        it "has the right details" do
          expect(dependency).to be_a(Dependabot::Dependency)
          expect(dependency.name).to eq("Serilog")
          expect(dependency.version).to eq("2.3.0")
          expect(dependency.requirements).to eq(
            [{
              requirement: "2.3.0",
              file: "my.vbproj",
              groups: ["dependencies"],
              source: nil
            }]
          )
        end
      end
    end

    context "with a packages.config" do
      let(:files) { [packages_config] }
      let(:packages_config) do
        Dependabot::DependencyFile.new(
          name: "packages.config",
          content: fixture("packages_configs", "packages.config")
        )
      end

      its(:length) { is_expected.to eq(9) }

      describe "the first dependency" do
        subject(:dependency) { top_level_dependencies.first }

        it "has the right details" do
          expect(dependency).to be_a(Dependabot::Dependency)
          expect(dependency.name)
            .to eq("Microsoft.CodeDom.Providers.DotNetCompilerPlatform")
          expect(dependency.version).to eq("1.0.0")
          expect(dependency.requirements).to eq(
            [{
              requirement: "1.0.0",
              file: "packages.config",
              groups: ["dependencies"],
              source: nil
            }]
          )
        end
      end

      describe "the second dependency" do
        subject(:dependency) { top_level_dependencies.at(1) }

        it "has the right details" do
          expect(dependency).to be_a(Dependabot::Dependency)
          expect(dependency.name)
            .to eq("Microsoft.Net.Compilers")
          expect(dependency.version).to eq("1.0.1")
          expect(dependency.requirements).to eq(
            [{
              requirement: "1.0.1",
              file: "packages.config",
              groups: ["devDependencies"],
              source: nil
            }]
          )
        end
      end

      context "that is nested" do
        its(:length) { is_expected.to eq(9) }
        let(:packages_config) do
          Dependabot::DependencyFile.new(
            name: "dir/packages.config",
            content: fixture("packages_configs", "packages.config")
          )
        end

        describe "the first dependency" do
          subject(:dependency) { top_level_dependencies.first }

          it "has the right details" do
            expect(dependency).to be_a(Dependabot::Dependency)
            expect(dependency.name)
              .to eq("Microsoft.CodeDom.Providers.DotNetCompilerPlatform")
            expect(dependency.version).to eq("1.0.0")
            expect(dependency.requirements).to eq(
              [{
                requirement: "1.0.0",
                file: "dir/packages.config",
                groups: ["dependencies"],
                source: nil
              }]
            )
          end
        end

        describe "the second dependency" do
          subject(:dependency) { top_level_dependencies.at(1) }

          it "has the right details" do
            expect(dependency).to be_a(Dependabot::Dependency)
            expect(dependency.name)
              .to eq("Microsoft.Net.Compilers")
            expect(dependency.version).to eq("1.0.1")
            expect(dependency.requirements).to eq(
              [{
                requirement: "1.0.1",
                file: "dir/packages.config",
                groups: ["devDependencies"],
                source: nil
              }]
            )
          end
        end
      end
    end

    context "with a global.json" do
      let(:files) { [packages_config, global_json] }
      let(:packages_config) do
        Dependabot::DependencyFile.new(
          name: "packages.config",
          content: fixture("packages_configs", "packages.config")
        )
      end
      let(:global_json) do
        Dependabot::DependencyFile.new(
          name: "global.json",
          content: fixture("global_jsons", "global.json")
        )
      end

      its(:length) { is_expected.to eq(10) }

      describe "the last dependency" do
        subject(:dependency) { top_level_dependencies.last }

        it "has the right details" do
          expect(dependency).to be_a(Dependabot::Dependency)
          expect(dependency.name).to eq("Microsoft.Build.Traversal")
          expect(dependency.version).to eq("1.0.45")
          expect(dependency.requirements).to eq(
            [{
              requirement: "1.0.45",
              file: "global.json",
              groups: ["dependencies"],
              source: nil
            }]
          )
        end
      end
    end

    context "with a dotnet-tools.json" do
      let(:files) { [packages_config, dotnet_tools_json] }
      let(:packages_config) do
        Dependabot::DependencyFile.new(
          name: "packages.config",
          content: fixture("packages_configs", "packages.config")
        )
      end
      let(:dotnet_tools_json) do
        Dependabot::DependencyFile.new(
          name: ".config/dotnet-tools.json",
          content: fixture("dotnet_tools_jsons", "dotnet-tools.json")
        )
      end

      its(:length) { is_expected.to eq(11) }

      describe "the last dependency" do
        subject(:dependency) { top_level_dependencies.last }

        it "has the right details" do
          expect(dependency).to be_a(Dependabot::Dependency)
          expect(dependency.name).to eq("dotnetsay")
          expect(dependency.version).to eq("1.0.0")
          expect(dependency.requirements).to eq(
            [{
              requirement: "1.0.0",
              file: ".config/dotnet-tools.json",
              groups: ["dependencies"],
              source: nil
            }]
          )
        end
      end
    end

    context "with an imported properties file" do
      let(:files) { [csproj_file, imported_file] }
      let(:imported_file) do
        Dependabot::DependencyFile.new(
          name: "commonprops.props",
          content: fixture("csproj", "commonprops.props")
        )
      end

      its(:length) { is_expected.to eq(6) }

      describe "the last dependency" do
        subject(:dependency) { top_level_dependencies.last }

        it "has the right details" do
          expect(dependency).to be_a(Dependabot::Dependency)
          expect(dependency.name).to eq("Serilog")
          expect(dependency.version).to eq("2.3.0")
          expect(dependency.requirements).to eq(
            [{
              requirement: "2.3.0",
              file: "commonprops.props",
              groups: ["dependencies"],
              source: nil
            }]
          )
        end
      end
    end

    context "with a packages.props file" do
      let(:files) { [csproj_file, packages_file] }
      let(:packages_file) do
        Dependabot::DependencyFile.new(
          name: "packages.props",
          content: fixture("csproj", "packages.props")
        )
      end

      its(:length) { is_expected.to eq(10) }

      describe "the last dependency" do
        subject(:dependency) { top_level_dependencies.last }

        it "has the right details" do
          expect(dependency).to be_a(Dependabot::Dependency)
          expect(dependency.name).to eq("System.WebCrawler")
          expect(dependency.version).to eq("1.1.1")
          expect(dependency.requirements).to eq(
            [{
              requirement: "1.1.1",
              file: "packages.props",
              groups: ["dependencies"],
              source: nil
            }]
          )
        end
      end
    end

    context "with a directory.packages.props file" do
      let(:files) { [csproj_file, packages_file] }
      let(:packages_file) do
        Dependabot::DependencyFile.new(
          name: "directory.packages.props",
          content: fixture("csproj", "directory.packages.props")
        )
      end

      its(:length) { is_expected.to eq(9) }

      describe "the last dependency" do
        subject(:dependency) { top_level_dependencies.last }

        it "has the right details" do
          expect(dependency).to be_a(Dependabot::Dependency)
          expect(dependency.name).to eq("System.WebCrawler")
          expect(dependency.version).to eq("1.1.1")
          expect(dependency.requirements).to eq(
            [{
              requirement: "1.1.1",
              file: "directory.packages.props",
              groups: ["dependencies"],
              source: nil
            }]
          )
        end
      end
    end

    context "with only directory.packages.props file" do
      let(:files) { [packages_file] }
      let(:packages_file) do
        Dependabot::DependencyFile.new(
          name: "directory.packages.props",
          content: fixture("csproj", "directory.packages.props")
        )
      end

      its(:length) { is_expected.to eq(4) }

      describe "the last dependency" do
        subject(:dependency) { top_level_dependencies.last }

        it "has the right details" do
          expect(dependency).to be_a(Dependabot::Dependency)
          expect(dependency.name).to eq("System.WebCrawler")
          expect(dependency.version).to eq("1.1.1")
          expect(dependency.requirements).to eq(
            [{
              requirement: "1.1.1",
              file: "directory.packages.props",
              groups: ["dependencies"],
              source: nil
            }]
          )
        end
      end
    end
  end
end
