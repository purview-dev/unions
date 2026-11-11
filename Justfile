set quiet

export TESTINGPLATFORM_EXITCODE_IGNORE := "8"
export DOTNET_CLI_TELEMETRY_OPTOUT := "1"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE := "1"
export DO_NOT_TRACK := "1"

solution := "src/Unions.slnx"
benchmarks_project := "src/src/Benchmarks/Benchmarks.csproj"
build_configuration := "Debug"

artifacts_folder := "./artifacts"
default_test_filter := "/*/*/*/*"

pipeline_tool := "dotnet purview-build"

current_version := `bun -p "require('./package.json').version"`

[private]
default:
    just --list

# Restore the repository's .NET local tools (.config/dotnet-tools.json)
[private]
ensure-pipeline-tool:
    pwsh -NoProfile -File scripts/restore-dotnet-tools.ps1

# Run the PR pipeline (restore, build, lint, tests)
[group('Pipeline')]
pipeline-pr *args:
    just ensure-pipeline-tool
    echo "Running PR pipeline..."
    {{ pipeline_tool }} {{ args }}

# Run the build pipeline (restore, build, lint)
[group('Pipeline')]
pipeline-build *args:
    just ensure-pipeline-tool
    echo "Running build pipeline..."
    {{ pipeline_tool }} --Build:RunTests=false --Release:Mode=None {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, publish, GitHub release)
[group('Pipeline')]
pipeline-release *args:
    just ensure-pipeline-tool
    just lint-fix
    echo "Running release pipeline..."
    {{ pipeline_tool }} --Release:Mode=NuGet {{ args }}

# Run the release pipeline (restore, build, lint, tests, pack, local nuget publish)
# Note: `just` runs recipes through the shell, which strips backslashes from unquoted arguments.
# Use the LOCAL_NUGET_FEED_PATH environment variable or forward slashes, e.g.
# just pipeline-local-release --PublishLocalNuGet:LocalFeedPath=p:/_sync-projects/.local-nuget/
[group('Pipeline')]
pipeline-local-release *args:
    just ensure-pipeline-tool
    just lint-fix
    echo "Running local release pipeline..."
    {{ pipeline_tool }} --Release:Mode=LocalNuGet {{ args }}

# Run the pipeline through pack + validate (restore, build, lint, tests, pack, validate pack contents) without publishing/releasing
[group('Pipeline')]
pipeline-pack-validate *args:
    just ensure-pipeline-tool
    echo "Running pack + validate pipeline..."
    {{ pipeline_tool }} --Build:RunPack=true --Build:ValidatePack=true --Release:Mode=None {{ args }}

# Displays the current version from package.json
[group('Build and Test')]
version:
    echo "Current version is {{ BLUE }}{{ current_version }}{{ NORMAL }}"

# Build the solution with the specified configuration, defaulting to "Debug"
[group('Build and Test')]
build *args:
    echo "Building {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}"
    dotnet build {{ solution }} -c {{ build_configuration }} {{ args }}

# Clean the solution with the specified configuration, defaulting to "Debug"
[group('Build and Test')]
clean *args:
    echo "Cleaning {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }}"
    dotnet clean {{ solution }} -c {{ build_configuration }} {{ args }}

# Run tests with the specified configuration, defaulting to "Debug"
[group('Build and Test')]
test filter=default_test_filter *args:
    echo "Running tests for {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }} and filter {{ GREEN }}{{ filter }}{{ NORMAL }}"
    dotnet test {{ solution }} -c {{ build_configuration }} --treenode-filter "{{ filter }}" -- {{ args }}

# Run the unit tests
[group('Build and Test')]
test-unit *args:
    just test "/*/*/*/*[Category=Unit]" {{ args }}

# Run the performance benchmarks on .NET 10
[group('Build and Test')]
perf-tests *args:
    echo "Running performance benchmarks for {{ BLUE }}{{ benchmarks_project }}{{ NORMAL }} on net10.0"
    dotnet run --project {{ benchmarks_project }} -c Release -f net10.0 {{ args }}

# Run the performance benchmarks on .NET 11
[group('Build and Test')]
perf-tests-net11 *args:
    echo "Running performance benchmarks for {{ BLUE }}{{ benchmarks_project }}{{ NORMAL }} on net11.0"
    dotnet run --project {{ benchmarks_project }} -c Release -f net11.0 {{ args }}

# Restore dependencies for the solution
[group('Build and Test')]
restore *args:
    echo "Restoring dependencies for {{ BLUE }}{{ solution }}{{ NORMAL }}"
    dotnet restore {{ solution }} {{ args }}

# Create the NuGet package
[group('Build and Test')]
pack publish_folder=artifacts_folder *args:
    echo "Packing {{ BLUE }}{{ solution }}{{ NORMAL }} with configuration {{ YELLOW }}{{ build_configuration }}{{ NORMAL }} to {{ GREEN }}{{ publish_folder }}{{ NORMAL }}"
    echo "  Current version is {{ BLUE }}{{ current_version }}{{ NORMAL }}"
    dotnet pack {{ solution }} -c {{ build_configuration }} -o {{ publish_folder }} {{ args }}

# Prove the packed package works for real consumers: packs Purview.Unions to an isolated feed, then publishes and
# runs a net10.0 and a net11.0 console against it, and leak-scans their output for compiler-host assemblies. Needs
# the .NET 10 and .NET 11 runtimes. See scripts/test-packed-consumers.ps1 for the options.
[group('Build and Test')]
smoke-packed-consumers *args:
    echo "Running the packed consumer gate..."
    pwsh -NoProfile -File scripts/test-packed-consumers.ps1 {{ args }}

# Prove Purview.Telemetry reads a Purview.Unions generated union as a telemetry parameter. Packs Purview.Unions,
# then builds and runs a net10.0 and a net11.0 consumer against it and Purview.Telemetry.SourceGenerator 6.0.0
# (read from LOCAL_NUGET_FEED_PATH / TELEMETRY_PRERELEASE_FEED), asserting the generated telemetry accessor uses
# TryGetValue and leak-scanning the output. See scripts/test-telemetry-integration.ps1 for the options.
[group('Build and Test')]
test-telemetry-integration *args:
    echo "Running the telemetry integration gate..."
    pwsh -NoProfile -File scripts/test-telemetry-integration.ps1 {{ args }}

# Prove every PackValidation ForbiddenContent rule in .config/purview-build.json rejects a package. Reads artifacts/,
# so pack first (`just pipeline-pack-validate`); `just pipeline-local-release` moves its packages to the local feed, so
# pass -PackagesFolder <feed> after it.
[group('Build and Test')]
verify-pack-rules *args:
    just ensure-pipeline-tool
    pwsh -NoProfile -File scripts/test-pack-validation-rules.ps1 {{ args }}

# Run the example (surfaces A, B, C and D)
[group('Examples')]
example *args:
    dotnet run --project src/src/Examples {{ args }}

# Install/refresh the skills recorded in skills-lock.json through Purview.BuildSdk's UpdatePurviewSkills target.
# Needs the skills CLI and network access; it is never run by a build or a test.
[group('Utilities')]
skills-update *args:
    dotnet msbuild src/src/Unions/Unions.csproj -t:UpdatePurviewSkills {{ args }}

# Report which installed skills are missing or drifted, without changing anything
[group('Utilities')]
skills-check *args:
    dotnet msbuild src/src/Unions/Unions.csproj -t:CheckPurviewSkills {{ args }}

# Fix code formatting issues using CSharpier
[group('Utilities')]
lint-fix *args:
    dotnet csharpier format . {{ args }}

# Check code formatting using CSharpier
[group('Utilities')]
lint-check *args:
    dotnet csharpier check . {{ args }}

# Open the solution in Visual Studio / the registered application
[group('Utilities')]
vs:
    open {{ solution }}

# Clean up the repository by removing build artifacts, bin/obj folders etc, and shutting down the build server
[group('Utilities')]
scrub:
    find . -type d \( -name bin -o -name obj \) -exec rm -rf {} +
    just clean
    just restore --force-evaluate
    dotnet build-server shutdown
