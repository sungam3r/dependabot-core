# typed: false
# frozen_string_literal: true

require "dependabot/file_fetchers"
require "dependabot/file_fetchers/base"
require "set"
require "sorbet-runtime"

module Dependabot
  module Nuget
    class FileFetcher < Dependabot::FileFetchers::Base
      extend T::Sig
      extend T::Helpers

      require_relative "file_fetcher/import_paths_finder"
      require_relative "file_fetcher/sln_project_paths_finder"

      def self.required_files_in?(filenames)
        return true if filenames.any? { |f| f.match?(/^packages\.config$/i) }
        return true if filenames.any? { |f| f.end_with?(".sln") }
        return true if filenames.any? { |f| f.match?("^src$") }
        return true if filenames.any? { |f| f.end_with?(".proj") }

        filenames.any? { |name| name.match?(%r{^[^/]*\.[a-z]{2}proj$}) }
      end

      def self.required_files_message
        "Repo must contain a .proj file, .(cs|vb|fs)proj file, or a packages.config."
      end

      sig { override.returns(T::Array[DependencyFile]) }
      def fetch_files
        fetched_files = []
        fetched_files += project_files
        fetched_files += directory_build_files
        fetched_files += imported_property_files

        fetched_files += packages_config_files
        fetched_files += nuget_config_files
        fetched_files << global_json if global_json
        fetched_files << dotnet_tools_json if dotnet_tools_json
        fetched_files << packages_props if packages_props

        fetched_files = fetched_files.uniq

        if project_files.none? && packages_config_files.none?
          raise @missing_sln_project_file_errors.first if @missing_sln_project_file_errors&.any?

          raise(
            Dependabot::DependencyFileNotFound,
            File.join(directory, "<anything>.(cs|vb|fs)proj")
          )
        end

        fetched_files
      end

      private

      def project_files
        @project_files ||=
          begin
            project_files = []
            project_files << csproj_file if csproj_file
            project_files << vbproj_file if vbproj_file
            project_files << fsproj_file if fsproj_file
            project_files << directory_packages_props_file if directory_packages_props_file

            project_files += sln_project_files
            project_files
          end
      rescue Octokit::NotFound, Gitlab::Error::NotFound
        raise(
          Dependabot::DependencyFileNotFound,
          File.join(directory, "<anything>.(cs|vb|fs)proj")
        )
      end

      def packages_config_files
        return @packages_config_files if @packages_config_files

        candidate_paths =
          [*project_files.map { |f| File.dirname(f.name) }, "."].uniq

        @packages_config_files ||=
          candidate_paths.filter_map do |dir|
            file = repo_contents(dir: dir)
                   .find { |f| f.name.casecmp("packages.config").zero? }
            fetch_file_from_host(File.join(dir, file.name)) if file
          end
      end

      # rubocop:disable Metrics/PerceivedComplexity
      def sln_file_names
        sln_files = repo_contents.select { |f| f.name.end_with?(".sln") }
        src_dir = repo_contents.any? { |f| f.name == "src" && f.type == "dir" }

        # If there are no sln files but there is a src directory, check that dir
        if sln_files.none? && src_dir
          sln_files = repo_contents(dir: "src")
                      .select { |f| f.name.end_with?(".sln") }.map(&:dup)
                      .map { |file| file.tap { |f| f.name = "src/" + f.name } }
        end

        # Return `nil` if no sln files were found
        return if sln_files.none?

        sln_files.map(&:name)
      end
      # rubocop:enable Metrics/PerceivedComplexity

      def directory_build_files
        @directory_build_files ||= fetch_directory_build_files
      end

      def fetch_directory_build_files
        attempted_paths = []
        directory_build_files = []

        # Don't need to insert "." here, because Directory.Build.props files
        # can only be used by project files (not packages.config ones)
        project_files.map { |f| File.dirname(f.name) }.uniq.map do |dir|
          possible_paths = dir.split("/").flat_map.with_index do |_, i|
            base = dir.split("/").first(i + 1).join("/")
            possible_build_file_paths(base)
          end.reverse

          possible_paths += [
            "Directory.Build.props",
            "Directory.build.props",
            "Directory.Packages.props",
            "Directory.packages.props",
            "Directory.Build.targets",
            "Directory.build.targets"
          ]

          possible_paths.each do |path|
            break if attempted_paths.include?(path)

            attempted_paths << path
            file = fetch_file_if_present(path)
            directory_build_files << file if file
          end
        end

        directory_build_files
      end

      def possible_build_file_paths(base)
        [
          Pathname.new(base + "/Directory.Build.props").cleanpath.to_path,
          Pathname.new(base + "/Directory.build.props").cleanpath.to_path,
          Pathname.new(base + "/Directory.Packages.props").cleanpath.to_path,
          Pathname.new(base + "/Directory.packages.props").cleanpath.to_path,
          Pathname.new(base + "/Directory.Build.targets").cleanpath.to_path,
          Pathname.new(base + "/Directory.build.targets").cleanpath.to_path
        ]
      end

      def sln_project_files
        return [] unless sln_files

        @sln_project_files ||=
          begin
            paths = sln_files.flat_map do |sln_file|
              SlnProjectPathsFinder
                .new(sln_file: sln_file)
                .project_paths
                # track both the sln and the reported project path so that...
                .map { |proj_file_path| [sln_file, proj_file_path] }
            end

            # ...the paths can be deduped based on the fully resolved and normalzied path...
            paths = paths.uniq do |path_pair|
              Pathname.new(File.join(path_pair[0].directory, path_pair[1])).cleanpath.to_path
            end

            # ...then only return the relative path
            paths = paths.map { |pair| pair[1] }

            paths.filter_map do |path|
              fetch_file_from_host(path)
            rescue Dependabot::DependencyFileNotFound => e
              @missing_sln_project_file_errors ||= []
              @missing_sln_project_file_errors << e
              # Don't worry about missing files too much for now (at least
              # until we start resolving properties)
              nil
            end
          end
      end

      def sln_files
        return unless sln_file_names

        @sln_files ||=
          sln_file_names
          .map { |sln_file_name| fetch_file_from_host(sln_file_name) }
          .select { |file| file.content.valid_encoding? }
      end

      def csproj_file
        @csproj_file ||=
          begin
            file = repo_contents.find { |f| f.name.end_with?(".csproj") }
            fetch_file_from_host(file.name) if file
          end
      end

      def vbproj_file
        @vbproj_file ||=
          begin
            file = repo_contents.find { |f| f.name.end_with?(".vbproj") }
            fetch_file_from_host(file.name) if file
          end
      end

      def fsproj_file
        @fsproj_file ||=
          begin
            file = repo_contents.find { |f| f.name.end_with?(".fsproj") }
            fetch_file_from_host(file.name) if file
          end
      end

      def directory_packages_props_file
        @directory_packages_props_file ||=
          begin
            file = repo_contents.find { |f| f.name.casecmp?("directory.packages.props") }
            fetch_file_from_host(file.name) if file
          end
      end

      def nuget_config_files
        return @nuget_config_files if @nuget_config_files

        @nuget_config_files = []
        candidate_paths = [*project_files.map { |f| File.dirname(f.name) }, "."].uniq
        visited_directories = Set.new
        candidate_paths.each do |dir|
          search_in_directory_and_parents(dir, visited_directories)
        end
        @nuget_config_files
      end

      def search_in_directory_and_parents(dir, visited_directories)
        loop do
          break if visited_directories.include?(dir)

          visited_directories << dir
          file = repo_contents(dir: dir)
                 .find { |f| f.name.casecmp("nuget.config").zero? }
          if file
            file = fetch_file_from_host(File.join(dir, file.name))
            file&.tap { |f| f.support_file = true }
            @nuget_config_files << file
          end
          dir = File.dirname(dir)
        end
      end

      def global_json
        return @global_json if defined?(@global_json)

        @global_json = fetch_file_if_present("global.json")
      end

      def dotnet_tools_json
        return @dotnet_tools_json if defined?(@dotnet_tools_json)

        @dotnet_tools_json = fetch_file_if_present(".config/dotnet-tools.json")
      end

      def packages_props
        return @packages_props if defined?(@packages_props)

        @packages_props = fetch_file_if_present("Packages.props")
      end

      def imported_property_files
        imported_property_files = []

        files = [*project_files, *directory_build_files]

        files.each do |proj_file|
          previously_fetched_files = project_files + imported_property_files
          imported_property_files +=
            fetch_imported_property_files(
              file: proj_file,
              previously_fetched_files: previously_fetched_files
            )
        end

        imported_property_files
      end

      def fetch_imported_property_files(file:, previously_fetched_files:)
        paths =
          ImportPathsFinder.new(project_file: file).import_paths +
          ImportPathsFinder.new(project_file: file).project_reference_paths +
          ImportPathsFinder.new(project_file: file).project_file_paths

        paths.flat_map do |path|
          next if previously_fetched_files.map(&:name).include?(path)
          next if file.name == path
          next if path.include?("$(")

          fetched_file = fetch_file_from_host(path)
          grandchild_property_files = fetch_imported_property_files(
            file: fetched_file,
            previously_fetched_files: previously_fetched_files + [file]
          )
          [fetched_file, *grandchild_property_files]
        rescue Dependabot::DependencyFileNotFound
          # Don't worry about missing files too much for now (at least
          # until we start resolving properties)
          nil
        end.compact
      end
    end
  end
end

Dependabot::FileFetchers.register("nuget", Dependabot::Nuget::FileFetcher)
