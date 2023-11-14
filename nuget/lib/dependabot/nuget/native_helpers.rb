# typed: false
# frozen_string_literal: true

module Dependabot
  module Nuget
    module NativeHelpers
      def self.native_helpers_root
        helpers_root = ENV.fetch("DEPENDABOT_NATIVE_HELPERS_PATH", nil)
        return helpers_root unless helpers_root.nil?

        File.join(__dir__, "../../../helpers")
      end

      def self.run_nuget_framework_check(project_tfms, package_tfms)
        exe_path = File.join(native_helpers_root, "NuGetUpdater", "NuGetUpdater.Cli")
        command = [
          exe_path,
          "framework-check",
          "--project-tfms",
          *project_tfms,
          "--package-tfms",
          *package_tfms,
          "--verbose"
        ].join(" ")

        fingerprint = [
          exe_path,
          "framework-check",
          "--project-tfms",
          "<project-tfms>",
          "--package-tfms",
          "<package-tfms>",
          "--verbose"
        ].join(" ")

        puts "running NuGet updater:\n" + command

        output = SharedHelpers.run_shell_command(command, fingerprint: fingerprint)
        puts output

        # Exit code == 0 means that all project frameworks are compatible
        true
      rescue Dependabot::SharedHelpers::HelperSubprocessFailed
        # Exit code != 0 means that not all project frameworks are compatible
        false
      end

      # rubocop:disable Metrics/MethodLength
      def self.run_nuget_updater_tool(repo_root, proj_path, dependency, is_transitive)
        exe_path = File.join(native_helpers_root, "NuGetUpdater", "NuGetUpdater.Cli")
        command = [
          exe_path,
          "update",
          "--repo-root",
          repo_root,
          "--solution-or-project",
          proj_path,
          "--dependency",
          dependency.name,
          "--new-version",
          dependency.version,
          "--previous-version",
          dependency.previous_version,
          is_transitive ? "--transitive" : "",
          "--verbose"
        ].join(" ")

        fingerprint = [
          exe_path,
          "update",
          "--repo-root",
          "<repo-root>",
          "--solution-or-project",
          "<path-to-solution-or-project>",
          "--dependency",
          "<dependency-name>",
          "--new-version",
          "<new-version>",
          "--previous-version",
          "<previous-version>",
          is_transitive ? "--transitive" : "",
          "--verbose"
        ].join(" ")

        puts "running NuGet updater:\n" + command

        output = SharedHelpers.run_shell_command(command, fingerprint: fingerprint)

        puts output
      end
      # rubocop:enable Metrics/MethodLength
    end
  end
end
