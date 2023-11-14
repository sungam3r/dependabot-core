# typed: true
# frozen_string_literal: true

require "nokogiri"

require "dependabot/dependency"
require "dependabot/nuget/file_parser"
require "dependabot/nuget/update_checker"

# For details on how dotnet handles version constraints, see:
# https://docs.microsoft.com/en-us/nuget/reference/package-versioning
module Dependabot
  module Nuget
    class FileParser
      class ProjectFileParser
        require "dependabot/file_parsers/base/dependency_set"
        require_relative "property_value_finder"

        DEPENDENCY_SELECTOR = "ItemGroup > PackageReference, " \
                              "ItemGroup > GlobalPackageReference, " \
                              "ItemGroup > PackageVersion, " \
                              "ItemGroup > Dependency, " \
                              "ItemGroup > DevelopmentDependency"

        PROJECT_REFERENCE_SELECTOR = "ItemGroup > ProjectReference"

        PROJECT_SDK_REGEX   = %r{^([^/]+)/(\d+(?:[.]\d+(?:[.]\d+)?)?(?:[+-].*)?)$}
        PROPERTY_REGEX      = /\$\((?<property>.*?)\)/
        ITEM_REGEX          = /\@\((?<property>.*?)\)/

        def initialize(dependency_files:, credentials:)
          @dependency_files       = dependency_files
          @credentials            = credentials
        end

        def dependency_set(project_file:)
          dependency_set = Dependabot::FileParsers::Base::DependencySet.new

          doc = Nokogiri::XML(project_file.content)
          doc.remove_namespaces!
          # Look for regular package references
          doc.css(DEPENDENCY_SELECTOR).each do |dependency_node|
            name = dependency_name(dependency_node, project_file)
            req = dependency_requirement(dependency_node, project_file)
            version = dependency_version(dependency_node, project_file)
            prop_name = req_property_name(dependency_node)
            is_dev = dependency_node.name == "DevelopmentDependency"

            dependency = build_dependency(name, req, version, prop_name, project_file, dev: is_dev)
            dependency_set << dependency if dependency
          end

          add_transitive_dependencies(project_file, doc, dependency_set)

          # Look for SDK references; see:
          # https://docs.microsoft.com/en-us/visualstudio/msbuild/how-to-use-project-sdk
          add_sdk_references(doc, dependency_set, project_file)

          dependency_set
        end

        def target_frameworks(project_file:)
          target_framework = details_for_property("TargetFramework", project_file)
          return [target_framework&.fetch(:value)] if target_framework

          target_frameworks = details_for_property("TargetFrameworks", project_file)
          return target_frameworks&.fetch(:value)&.split(";") if target_frameworks

          []
        end

        private

        attr_reader :dependency_files, :credentials

        def add_transitive_dependencies(project_file, doc, dependency_set)
          add_transitive_dependencies_from_packages(dependency_set)
          add_transitive_dependencies_from_project_references(project_file, doc, dependency_set)
        end

        def add_transitive_dependencies_from_project_references(project_file, doc, dependency_set)
          project_file_directory = File.dirname(project_file.name)
          is_rooted = project_file_directory.start_with?("/")
          # Root the directory path to avoid expand_path prepending the working directory
          project_file_directory = "/" + project_file_directory unless is_rooted

          # Look for regular project references
          doc.css(PROJECT_REFERENCE_SELECTOR).each do |reference_node|
            relative_path = dependency_name(reference_node, project_file)
            # normalize path separators
            relative_path = relative_path.tr("\\", "/")
            # path is relative to the project file directory
            relative_path = File.join(project_file_directory, relative_path)

            # get absolute path
            full_path = File.expand_path(relative_path)
            full_path = full_path[1..-1] unless is_rooted

            referenced_file = dependency_files.find { |f| f.name == full_path }
            next unless referenced_file

            dependency_set(project_file: referenced_file).dependencies.each do |dep|
              dependency = Dependency.new(
                name: dep.name,
                version: dep.version,
                package_manager: dep.package_manager,
                requirements: []
              )
              dependency_set << dependency
            end
          end
        end

        def add_transitive_dependencies_from_packages(dependency_set)
          transitive_dependencies = {}

          dependency_set.dependencies.each do |dependency|
            UpdateChecker::DependencyFinder.new(
              dependency: dependency,
              dependency_files: dependency_files,
              credentials: credentials
            ).transitive_dependencies.each do |transitive_dep|
              visited_dep = transitive_dependencies[transitive_dep.name.downcase]
              next if !visited_dep.nil? && visited_dep.numeric_version > transitive_dep.numeric_version

              transitive_dependencies[transitive_dep.name.downcase] = transitive_dep
            end
          end

          transitive_dependencies.each { |_, dep| dependency_set << dep }
        end

        def add_sdk_references(doc, dependency_set, project_file)
          # These come in 3 flavours:
          # - <Project Sdk="Name/Version">
          # - <Sdk Name="Name" Version="Version" />
          # - <Import Project="..." Sdk="Name" Version="Version" />
          # None of these support the use of properties, nor do they allow child
          # elements instead of attributes.
          add_sdk_refs_from_project(doc, dependency_set, project_file)
          add_sdk_refs_from_sdk_tags(doc, dependency_set, project_file)
          add_sdk_refs_from_import_tags(doc, dependency_set, project_file)
        end

        def add_sdk_ref_from_project(sdk_references, dependency_set, project_file)
          sdk_references.split(";")&.each do |sdk_reference|
            m = sdk_reference.match(PROJECT_SDK_REGEX)
            if m
              dependency = build_dependency(m[1], m[2], m[2], nil, project_file)
              dependency_set << dependency if dependency
            end
          end
        end

        def add_sdk_refs_from_import_tags(doc, dependency_set, project_file)
          doc.xpath("/Project/Import").each do |import_node|
            next unless import_node.attribute("Sdk") && import_node.attribute("Version")

            name = import_node.attribute("Sdk")&.value&.strip
            version = import_node.attribute("Version")&.value&.strip

            dependency = build_dependency(name, version, version, nil, project_file)
            dependency_set << dependency if dependency
          end
        end

        def add_sdk_refs_from_project(doc, dependency_set, project_file)
          doc.xpath("/Project").each do |project_node|
            sdk_references = project_node.attribute("Sdk")&.value&.strip
            next unless sdk_references

            add_sdk_ref_from_project(sdk_references, dependency_set, project_file)
          end
        end

        def add_sdk_refs_from_sdk_tags(doc, dependency_set, project_file)
          doc.xpath("/Project/Sdk").each do |sdk_node|
            next unless sdk_node.attribute("Version")

            name = sdk_node.attribute("Name")&.value&.strip
            version = sdk_node.attribute("Version")&.value&.strip

            dependency = build_dependency(name, version, version, nil, project_file)
            dependency_set << dependency if dependency
          end
        end

        def build_dependency(name, req, version, prop_name, project_file, dev: false)
          return unless name

          # Exclude any dependencies specified using interpolation
          return if [name, req, version].any? { |s| s&.include?("%(") }

          requirement = {
            requirement: req,
            file: project_file.name,
            groups: [dev ? "devDependencies" : "dependencies"],
            source: nil
          }

          if prop_name
            # Get the root property name unless no details could be found,
            # in which case use the top-level name to ease debugging
            root_prop_name = details_for_property(prop_name, project_file)
                             &.fetch(:root_property_name) || prop_name
            requirement[:metadata] = { property_name: root_prop_name }
          end

          Dependency.new(
            name: name,
            version: version,
            package_manager: "nuget",
            requirements: [requirement]
          )
        end

        # rubocop:disable Metrics/PerceivedComplexity
        def dependency_name(dependency_node, project_file)
          raw_name =
            dependency_node.attribute("Include")&.value&.strip ||
            dependency_node.at_xpath("./Include")&.content&.strip ||
            dependency_node.attribute("Update")&.value&.strip ||
            dependency_node.at_xpath("./Update")&.content&.strip
          return unless raw_name

          # If the item contains @(ItemGroup) then ignore as it
          # updates a set of ItemGroup elements
          return if raw_name.match?(ITEM_REGEX)

          evaluated_value(raw_name, project_file)
        end
        # rubocop:enable Metrics/PerceivedComplexity

        def dependency_requirement(dependency_node, project_file)
          raw_requirement = get_node_version_value(dependency_node)
          return unless raw_requirement

          evaluated_value(raw_requirement, project_file)
        end

        def dependency_version(dependency_node, project_file)
          requirement = dependency_requirement(dependency_node, project_file)
          return unless requirement

          # Remove brackets if present
          version = requirement.gsub(/[\(\)\[\]]/, "").strip

          # We don't know the version for range requirements or wildcard
          # requirements, so return `nil` for these.
          return if version.include?(",") || version.include?("*") ||
                    version == ""

          version
        end

        def req_property_name(dependency_node)
          raw_requirement = get_node_version_value(dependency_node)
          return unless raw_requirement

          return unless raw_requirement.match?(PROPERTY_REGEX)

          raw_requirement
            .match(PROPERTY_REGEX)
            .named_captures.fetch("property")
        end

        # rubocop:disable Metrics/PerceivedComplexity
        def get_node_version_value(node)
          attribute = "Version"
          value =
            node.attribute(attribute)&.value&.strip ||
            node.at_xpath("./#{attribute}")&.content&.strip ||
            node.attribute(attribute.downcase)&.value&.strip ||
            node.at_xpath("./#{attribute.downcase}")&.content&.strip

          value == "" ? nil : value
        end
        # rubocop:enable Metrics/PerceivedComplexity

        def evaluated_value(value, project_file)
          return value unless value.match?(PROPERTY_REGEX)

          property_name = value.match(PROPERTY_REGEX)
                               .named_captures.fetch("property")
          property_details = details_for_property(property_name, project_file)

          # Don't halt parsing for a missing property value until we're
          # confident we're fetching property values correctly
          return value unless property_details&.fetch(:value)

          value.gsub(PROPERTY_REGEX, property_details&.fetch(:value))
        end

        def details_for_property(property_name, project_file)
          property_value_finder
            .property_details(
              property_name: property_name,
              callsite_file: project_file
            )
        end

        def property_value_finder
          @property_value_finder ||=
            PropertyValueFinder.new(dependency_files: dependency_files)
        end
      end
    end
  end
end
