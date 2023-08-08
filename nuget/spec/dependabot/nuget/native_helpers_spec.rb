# frozen_string_literal: true

require "spec_helper"
require "dependabot/nuget/native_helpers"
require "dependabot/shared_helpers"

RSpec.describe Dependabot::Nuget::NativeHelpers do

  dependabot_home = ENV.fetch("DEPENDABOT_HOME", nil)
  if dependabot_home.nil?
    dependabot_home = ENV.fetch("HOME", nil)
  end

  solution_path = File.join(dependabot_home, "nuget", "helpers", "lib", "NuGetUpdater", "NuGetUpdater.sln")

  describe "#native_csharp_tests" do

    let(:command) { [
      "dotnet",
      "test",
      "--configuration",
      "Release",
      solution_path
    ].join(" ")}

    subject(:dotnet_test) do
      Dependabot::SharedHelpers.run_shell_command(command)
    end

    context "dotnet test output" do
      it "contains the expected output" do
        expect(dotnet_test).to include("Passed!")
      end
    end
  end
end