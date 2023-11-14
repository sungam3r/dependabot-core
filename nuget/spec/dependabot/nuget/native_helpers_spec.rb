# typed: false
# frozen_string_literal: true

require "spec_helper"
require "dependabot/nuget/native_helpers"
require "dependabot/shared_helpers"

RSpec.describe Dependabot::Nuget::NativeHelpers do
  let(:dependabot_home) { ENV.fetch("DEPENDABOT_HOME", nil) || Dir.home }

  describe "#native_csharp_tests" do
    let(:command) do
      [
        "dotnet",
        "test",
        "--configuration",
        "Release",
        project_path
      ].join(" ")
    end

    subject(:dotnet_test) do
      Dependabot::SharedHelpers.run_shell_command(command)
    end

    context "`dotnet test NuGetUpdater.Core.Test` output" do
      let(:project_path) do
        File.join(dependabot_home, "nuget", "helpers", "lib", "NuGetUpdater",
                  "NuGetUpdater.Core.Test", "NuGetUpdater.Core.Test.csproj")
      end

      it "contains the expected output" do
        expect(dotnet_test).to include("Passed!")
      end
    end

    context "`dotnet test NuGetUpdater.Cli.Test` output" do
      let(:project_path) do
        File.join(dependabot_home, "nuget", "helpers", "lib", "NuGetUpdater",
                  "NuGetUpdater.Cli.Test", "NuGetUpdater.Cli.Test.csproj")
      end

      it "contains the expected output" do
        expect(dotnet_test).to include("Passed!")
      end
    end
  end
end
