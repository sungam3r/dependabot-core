# typed: true
# frozen_string_literal: true

require "nokogiri"

require "dependabot/dependency"
require "dependabot/nuget/file_parser"
require "dependabot/nuget/cache_manager"

# For details on packages.config files see:
# https://docs.microsoft.com/en-us/nuget/reference/packages-config
module Dependabot
  module Nuget
    class FileParser
      class PackagesConfigParser
        require "dependabot/file_parsers/base/dependency_set"

        DEPENDENCY_SELECTOR = "packages > package"

        def self.dependency_set_cache
          CacheManager.cache("packages_config_dependency_set")
        end

        def initialize(packages_config:)
          @packages_config = packages_config
        end

        def dependency_set
          return parse_dependencies if CacheManager.caching_disabled?

          key = "#{packages_config.name.downcase}::#{packages_config.content.hash}"
          cache = PackagesConfigParser.dependency_set_cache

          cache[key] ||= parse_dependencies

          dependency_set = Dependabot::FileParsers::Base::DependencySet.new
          dependency_set += cache[key]
          dependency_set
        end

        private

        attr_reader :packages_config

        def parse_dependencies
          dependency_set = Dependabot::FileParsers::Base::DependencySet.new

          doc = Nokogiri::XML(packages_config.content)
          doc.remove_namespaces!
          doc.css(DEPENDENCY_SELECTOR).each do |dependency_node|
            dependency_set <<
              Dependency.new(
                name: dependency_name(dependency_node),
                version: dependency_version(dependency_node),
                package_manager: "nuget",
                requirements: [{
                  requirement: dependency_version(dependency_node),
                  file: packages_config.name,
                  groups: [dependency_type(dependency_node)],
                  source: nil
                }]
              )
          end

          dependency_set
        end

        def dependency_name(dependency_node)
          dependency_node.attribute("id")&.value&.strip ||
            dependency_node.at_xpath("./id")&.content&.strip
        end

        def dependency_version(dependency_node)
          # Ranges and wildcards aren't allowed in a packages.config - the
          # specified requirement is always an exact version.
          dependency_node.attribute("version")&.value&.strip ||
            dependency_node.at_xpath("./version")&.content&.strip
        end

        def dependency_type(dependency_node)
          val = dependency_node.attribute("developmentDependency")&.value&.strip ||
                dependency_node.at_xpath("./developmentDependency")&.content&.strip
          val.to_s.casecmp("true").zero? ? "devDependencies" : "dependencies"
        end
      end
    end
  end
end
