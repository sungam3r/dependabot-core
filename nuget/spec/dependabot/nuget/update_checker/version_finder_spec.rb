# typed: false
# frozen_string_literal: true

require "spec_helper"
require "dependabot/dependency"
require "dependabot/dependency_file"
require "dependabot/nuget/update_checker/version_finder"
require "dependabot/nuget/update_checker/tfm_comparer"

RSpec.describe Dependabot::Nuget::UpdateChecker::VersionFinder, :vcr do
  let(:finder) do
    described_class.new(
      dependency: dependency,
      dependency_files: dependency_files,
      credentials: credentials,
      ignored_versions: ignored_versions,
      raise_on_ignored: raise_on_ignored,
      security_advisories: security_advisories,
      tfm_comparer: tfm_comparer
    )
  end

  let(:tfm_comparer) do
    Dependabot::Nuget::UpdateChecker::TfmComparer.new(
      dependency_files: dependency_files,
      credentials: credentials
    )
  end

  let(:dependency) do
    Dependabot::Dependency.new(
      name: dependency_name,
      version: dependency_version,
      requirements: dependency_requirements,
      package_manager: "nuget"
    )
  end

  let(:dependency_requirements) do
    [{ file: "my.csproj", requirement: "1.1.1", groups: ["dependencies"], source: nil }]
  end
  let(:dependency_name) { "Microsoft.Extensions.DependencyModel" }
  let(:dependency_version) { "1.1.1" }

  let(:dependency_files) { [csproj] }
  let(:csproj) do
    Dependabot::DependencyFile.new(name: "my.csproj", content: csproj_body)
  end
  let(:csproj_body) { fixture("csproj", "basic.csproj") }

  let(:credentials) do
    [{
      "type" => "git_source",
      "host" => "github.com",
      "username" => "x-access-token",
      "password" => "token"
    }]
  end
  let(:ignored_versions) { [] }
  let(:raise_on_ignored) { false }
  let(:security_advisories) { [] }

  let(:nuget_versions_url) do
    "https://api.nuget.org/v3-flatcontainer/" \
      "microsoft.extensions.dependencymodel/index.json"
  end
  let(:nuget_search_url) do
    "https://azuresearch-usnc.nuget.org/query" \
      "?q=microsoft.extensions.dependencymodel&prerelease=true&semVerLevel=2.0.0"
  end
  let(:version_class) { Dependabot::Nuget::Version }
  let(:nuget_versions) { fixture("nuget_responses", "versions.json") }
  let(:nuget_search_results) do
    fixture("nuget_responses", "search_results.json")
  end

  before do
    stub_request(:get, nuget_versions_url)
      .to_return(status: 200, body: nuget_versions)
    stub_request(:get, nuget_search_url)
      .to_return(status: 200, body: nuget_search_results)
  end

  describe "#latest_version_details" do
    subject(:latest_version_details) { finder.latest_version_details }
    its([:version]) { is_expected.to eq(version_class.new("2.1.0")) }

    context "when the returned versions is prefixed with a zero-width char" do
      let(:nuget_search_results) do
        fixture("nuget_responses", "search_results_zero_width.json")
      end

      its([:version]) { is_expected.to eq(version_class.new("2.1.0")) }
    end

    context "when the user wants a pre-release" do
      let(:dependency_version) { "2.2.0-preview1-26216-03" }
      its([:version]) do
        is_expected.to eq(version_class.new("2.2.0-preview2-26406-04"))
      end

      context "for a previous version" do
        let(:dependency_version) { "2.1.0-preview1-26216-03" }
        its([:version]) do
          is_expected.to eq(version_class.new("2.1.0"))
        end
      end
    end

    context "when the user wants a pre-release with wildcard" do
      let(:dependency_version) { "*-*" }
      let(:dependency_requirements) do
        [{ file: "my.csproj", requirement: "*-*", groups: ["dependencies"], source: nil }]
      end
      its([:version]) do
        is_expected.to eq(version_class.new("2.2.0-preview2-26406-04"))
      end
    end

    context "when the user is using an unfound property" do
      let(:dependency_version) { "$PackageVersion_LibGit2SharpNativeBinaries" }
      its([:version]) { is_expected.to eq(version_class.new("2.1.0")) }
    end

    context "raise_on_ignored when later versions are allowed" do
      let(:raise_on_ignored) { true }
      it "doesn't raise an error" do
        expect { subject }.to_not raise_error
      end
    end

    context "when the user is on the latest version" do
      let(:dependency_version) { "2.1.0" }
      its([:version]) { is_expected.to eq(version_class.new("2.1.0")) }

      context "raise_on_ignored" do
        let(:raise_on_ignored) { true }
        it "doesn't raise an error" do
          expect { subject }.to_not raise_error
        end
      end
    end

    context "when the current version isn't known" do
      let(:dependency_version) { nil }

      context "raise_on_ignored" do
        let(:raise_on_ignored) { true }
        it "doesn't raise an error" do
          expect { subject }.to_not raise_error
        end
      end
    end

    context "when the dependency is a git dependency" do
      let(:dependency_version) { "a1b78a929dac93a52f08db4f2847d76d6cfe39bd" }

      context "raise_on_ignored" do
        let(:raise_on_ignored) { true }
        it "doesn't raise an error" do
          expect { subject }.to_not raise_error
        end
      end
    end

    context "when the user is ignoring all later versions" do
      let(:ignored_versions) { ["> 1.1.1"] }
      its([:version]) { is_expected.to eq(version_class.new("1.1.1")) }

      context "raise_on_ignored" do
        let(:raise_on_ignored) { true }
        it "raises an error" do
          expect { subject }.to raise_error(Dependabot::AllVersionsIgnored)
        end
      end
    end

    context "when the user is ignoring the latest version" do
      let(:ignored_versions) { ["[2.a,3.0.0)"] }
      its([:version]) { is_expected.to eq(version_class.new("1.1.2")) }
    end

    context "when a version range is specified using Ruby syntax" do
      let(:ignored_versions) { [">= 2.a, < 3.0.0"] }
      its([:version]) { is_expected.to eq(version_class.new("1.1.2")) }
    end

    context "when the user has ignored all versions" do
      let(:ignored_versions) { ["[0,)"] }
      it "returns nil" do
        expect(subject).to be_nil
      end

      context "raise_on_ignored" do
        let(:raise_on_ignored) { true }
        it "raises an error" do
          expect { subject }.to raise_error(Dependabot::AllVersionsIgnored)
        end
      end
    end

    context "when an open version range is specified using Ruby syntax" do
      let(:ignored_versions) { ["> 0"] }
      it "returns nil" do
        expect(subject).to be_nil
      end

      context "raise_on_ignored" do
        let(:raise_on_ignored) { true }
        it "raises an error" do
          expect { subject }.to raise_error(Dependabot::AllVersionsIgnored)
        end
      end
    end

    context "with a custom repo in a nuget.config file" do
      let(:config_file) do
        Dependabot::DependencyFile.new(
          name: "NuGet.Config",
          content: fixture("configs", "nuget.config")
        )
      end
      let(:dependency_files) { [csproj, config_file] }
      let(:custom_repo_url) do
        "https://www.myget.org/F/exceptionless/api/v3/index.json"
      end
      let(:custom_nuget_versions_url) do
        "https://www.myget.org/F/exceptionless/api/v3/flatcontainer/" \
          "microsoft.extensions.dependencymodel/index.json"
      end
      let(:custom_nuget_search_url) do
        "https://www.myget.org/F/exceptionless/api/v3/" \
          "query?q=microsoft.extensions.dependencymodel&prerelease=true&semVerLevel=2.0.0"
      end
      before do
        stub_request(:get, nuget_versions_url).to_return(status: 404)
        stub_request(:get, nuget_search_url).to_return(status: 404)

        stub_request(:get, custom_repo_url).to_return(status: 404)
        stub_request(:get, custom_repo_url)
          .with(basic_auth: %w(my passw0rd))
          .to_return(
            status: 200,
            body: fixture("nuget_responses", "myget_base.json")
          )
        stub_request(:get, custom_nuget_versions_url).to_return(status: 404)
        stub_request(:get, custom_nuget_versions_url)
          .with(basic_auth: %w(my passw0rd))
          .to_return(status: 200, body: nuget_versions)
        stub_request(:get, custom_nuget_search_url).to_return(status: 404)
        stub_request(:get, custom_nuget_search_url)
          .with(basic_auth: %w(my passw0rd))
          .to_return(status: 200, body: nuget_search_results)
      end

      # skipped
      # its([:version]) { is_expected.to eq(version_class.new("2.1.0")) }

      context "that uses the v2 API" do
        let(:config_file) do
          Dependabot::DependencyFile.new(
            name: "NuGet.Config",
            content: fixture("configs", "with_v2_endpoints.config")
          )
        end

        let(:custom_v3_nuget_versions_url) do
          "https://www.myget.org/F/exceptionless/api/v3/flatcontainer/" \
            "microsoft.extensions.dependencymodel/index.json"
        end

        before do
          v2_repo_urls = %w(
            https://www.nuget.org/api/v2/
            https://www.myget.org/F/azure-appservice/api/v2
            https://www.myget.org/F/azure-appservice-staging/api/v2
            https://www.myget.org/F/fusemandistfeed/api/v2
            https://www.myget.org/F/30de4ee06dd54956a82013fa17a3accb/
          )

          v2_repo_urls.each do |repo_url|
            stub_request(:get, repo_url)
              .to_return(
                status: 200,
                body: fixture("nuget_responses", "v2_base.xml")
              )
          end

          url = "https://dotnet.myget.org/F/aspnetcore-dev/api/v3/index.json"
          stub_request(:get, url)
            .to_return(
              status: 200,
              body: fixture("nuget_responses", "myget_base.json")
            )

          stub_request(:get, custom_v3_nuget_versions_url)
            .to_return(status: 404)

          custom_v2_nuget_versions_url =
            "https://www.nuget.org/api/v2/FindPackagesById()?id=" \
            "'Microsoft.Extensions.DependencyModel'"
          stub_request(:get, custom_v2_nuget_versions_url)
            .to_return(
              status: 200,
              body: fixture("nuget_responses", "v2_versions.xml")
            )
        end

        its([:version]) { is_expected.to eq(version_class.new("4.8.1")) }
      end
    end

    context "with a package that returns paginated api results when using the v2 nuget api", :vcr do
      let(:dependency_files) { project_dependency_files("paginated_package_v2_api") }
      let(:dependency_requirements) do
        [{ file: "my.csproj", requirement: "4.7.1", groups: ["dependencies"], source: nil }]
      end
      let(:dependency_name) { "FakeItEasy" }
      let(:dependency_version) { "4.7.1" }

      it "returns the expected version" do
        expect(subject[:version]).to eq(version_class.new("7.4.0"))
      end
    end

    context "with a custom repo in the credentials", :vcr do
      let(:credentials) do
        [{
          "type" => "git_source",
          "host" => "github.com",
          "username" => "x-access-token",
          "password" => "token"
        }, {
          "type" => "nuget_feed",
          "url" => custom_repo_url,
          "token" => "my:passw0rd"
        }]
      end

      let(:nuget_versions) { fixture("nuget_responses", "versions.json") }

      let(:nuget_search_results) do
        fixture("nuget_responses", "search_results.json")
      end

      let(:custom_repo_url) do
        "https://www.myget.org/F/exceptionless/api/v3/index.json"
      end
      let(:custom_nuget_search_url) do
        "https://www.myget.org/F/exceptionless/api/v3/" \
          "query?q=microsoft.extensions.dependencymodel&prerelease=true&semVerLevel=2.0.0"
      end
      let(:custom_nuget_versions_url) do
        "https://www.myget.org/F/exceptionless/api/v3/flatcontainer/" \
          "microsoft.extensions.dependencymodel/index.json"
      end

      before do
        stub_request(:get, nuget_versions_url).to_return(status: 404)
        stub_request(:get, nuget_search_url).to_return(status: 404)

        stub_request(:get, custom_repo_url).to_return(status: 404)
        stub_request(:get, custom_repo_url)
          .with(basic_auth: %w(my passw0rd))
          .to_return(
            status: 200,
            body: fixture("nuget_responses", "myget_base.json")
          )

        stub_request(:get, custom_nuget_versions_url).to_return(status: 404)
        stub_request(:get, custom_nuget_versions_url)
          .with(basic_auth: %w(my passw0rd))
          .to_return(status: 200, body: nuget_versions)

        stub_request(:get, custom_nuget_search_url).to_return(status: 404)
        stub_request(:get, custom_nuget_search_url)
          .with(basic_auth: %w(my passw0rd))
          .to_return(status: 200, body: nuget_search_results)
      end

      its([:version]) { is_expected.to eq(version_class.new("2.1.0")) }

      context "that does not return PackageBaseAddress" do
        let(:custom_repo_url) { "http://www.myget.org/artifactory/api/nuget/v3/dependabot-nuget-local" }
        before do
          stub_request(:get, custom_repo_url)
            .with(basic_auth: %w(admin password))
            .to_return(
              status: 200,
              body: fixture("nuget_responses", "artifactory_base.json")
            )
        end

        its([:version]) { is_expected.to eq(version_class.new("2.1.0")) }
      end
    end

    context "with a version range specified" do
      let(:dependency_files) { project_dependency_files("version_range") }
      let(:dependency_version) { "1.1.0" }
      let(:dependency_requirements) do
        [{ file: "my.csproj", requirement: "[1.1.0, 3.0.0)", groups: ["dependencies"], source: nil }]
      end

      its([:version]) { is_expected.to eq(version_class.new("2.1.0")) }
    end

    context "with a package that is implicitly referenced", :vcr do
      let(:dependency_files) { project_dependency_files("implicit_reference") }
      let(:dependency_requirements) do
        [{ file: "implicitReference.csproj", requirement: "1.1.2-beta1.22511.2", groups: ["dependencies"],
           source: nil }]
      end
      let(:dependency_name) { "NuGet.Protocol" }
      let(:dependency_version) { "6.3.0" }

      # skipped
      # it "returns the expected version" do
      #   expect(subject[:version]).to eq(version_class.new("6.5.0"))
      # end
    end
  end

  describe "#lowest_security_fix_version_details" do
    subject(:lowest_security_fix_version_details) do
      finder.lowest_security_fix_version_details
    end

    let(:dependency_version) { "1.1.1" }
    let(:security_advisories) do
      [
        Dependabot::SecurityAdvisory.new(
          dependency_name: "rails",
          package_manager: "nuget",
          vulnerable_versions: ["< 2.0.0"]
        )
      ]
    end

    its([:version]) { is_expected.to eq(version_class.new("2.0.0")) }

    context "when the user is ignoring the lowest version" do
      let(:ignored_versions) { [">= 2.a, <= 2.0.0"] }
      its([:version]) { is_expected.to eq(version_class.new("2.0.3")) }
    end
  end

  describe "#versions" do
    subject(:versions) { finder.versions }

    it "includes the correct versions" do
      expect(versions.count).to eq(21)
      expect(versions.first).to eq(
        nuspec_url: "https://api.nuget.org/v3-flatcontainer/" \
                    "microsoft.extensions.dependencymodel/1.0.0-rc2-002702/" \
                    "microsoft.extensions.dependencymodel.nuspec",
        repo_url: "https://api.nuget.org/v3/index.json",
        source_url: nil,
        version: Dependabot::Nuget::Version.new("1.0.0-rc2-002702")
      )
    end
  end
end
