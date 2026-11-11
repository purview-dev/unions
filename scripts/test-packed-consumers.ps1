#!/usr/bin/env pwsh
<#
.SYNOPSIS
	Proves the packed Purview.Unions package works for real consumers.

.DESCRIPTION
	Packs Purview.Unions to an isolated feed, then creates a scratch console project for each supported
	target framework that references only the package, declares a union with [GenerateUnion], executes it,
	and exits non-zero on any mismatch. Every consumer output is leak-scanned for compiler-host assemblies.

	The scratch feed is the only package source, so the run never reaches the network. The consumers target
	net10.0 (the generator's internal polyfills) and net11.0 (the BCL union types), which is what proves
	the package's two personalities.

.PARAMETER PackOutput
	Folder that receives the packed .nupkg. Defaults to artifacts/packed.

.PARAMETER Configuration
	The pack configuration. Defaults to Release so the RequiredCompilerFlags check is representative.

.PARAMETER KeepArtifacts
	Leave the scratch consumers and feed in place for inspection.

.EXAMPLE
	./scripts/test-packed-consumers.ps1

.EXAMPLE
	./scripts/test-packed-consumers.ps1 -KeepArtifacts
#>
[CmdletBinding()]
param (
	[string] $PackOutput = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/packed'),
	[ValidateSet('Debug', 'Release')]
	[string] $Configuration = 'Release',
	[switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $repositoryRoot 'package.json') -Raw | ConvertFrom-Json).version
$project = Join-Path $repositoryRoot 'src/src/Unions/Unions.csproj'

$scratch = Join-Path ([System.IO.Path]::GetTempPath()) "unions-consumers-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
$feed = Join-Path $scratch 'feed'
$failures = [System.Collections.Generic.List[string]]::new()
$originalNuGetPackages = $env:NUGET_PACKAGES

try {
	New-Item -ItemType Directory -Force -Path $feed, $PackOutput | Out-Null

	# Purview.Unions deliberately has no ProjectReference to the generator, so packing it restores only
	# itself. The pack target invokes `GetPurviewMergedAnalyzerFile` on the generator project and
	# `Build` on the code-fix project through MSBuild, and a project that was never restored has no
	# package build assets: the framework's targets are not imported and the target does not exist
	# (MSB4057). Restore both before packing so the gate works on a clean checkout, not only on a
	# developer machine where an earlier build already restored them.
	$generator = Join-Path $repositoryRoot 'src/src/SourceGenerator/SourceGenerator.csproj'
	$codeFixes = Join-Path $repositoryRoot 'src/src/SourceGenerator.CodeFixes/SourceGenerator.CodeFixes.csproj'

	Write-Host 'Restoring the generator and code-fix projects...' -ForegroundColor Cyan
	& dotnet restore $generator --nologo
	if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed for $generator with exit code $LASTEXITCODE." }

	& dotnet restore $codeFixes --nologo
	if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed for $codeFixes with exit code $LASTEXITCODE." }

	Write-Host "Packing Purview.Unions $version to $feed..." -ForegroundColor Cyan
	& dotnet pack $project -c $Configuration -o $feed --nologo
	if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE." }

	$package = Join-Path $feed "Purview.Unions.$version.nupkg"
	if (-not (Test-Path $package)) { throw "The packed package '$package' was not produced." }

	# The scratch feed is temporary and removed below, so mirror what was packed into PackOutput as well.
	# The workflow validates these same files with test-pack-validation-rules.ps1 after this script, and a
	# PackOutput that received nothing made that gate fail on a clean runner even though packing succeeded.
	Copy-Item $package $PackOutput -Force
	$symbolPackage = [System.IO.Path]::ChangeExtension($package, '.snupkg')
	if (Test-Path $symbolPackage) { Copy-Item $symbolPackage $PackOutput -Force }

	# NuGet reuses an extracted package for the same id and version, so an isolated global-packages
	# folder is what guarantees each consumer tests the package just packed rather than a stale extract
	# left by an earlier run with the same version.
	$env:NUGET_PACKAGES = Join-Path $scratch 'packages'

	foreach ($tfm in @('net10.0', 'net11.0')) {
		# net11.0 gets C# 15 by default in the .NET 11 SDK; net10.0 defaults to C# 14, so its
		# consumer sets latest to prove the union syntax with the generator's polyfills. Neither
		# consumer sets preview.
		$langVersionProperty = if ($tfm -eq 'net10.0') { '<LangVersion>latest</LangVersion>' } else { '' }
		$consumer = Join-Path $scratch "Consumer.$tfm"
		New-Item -ItemType Directory -Force -Path $consumer | Out-Null

		@"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
	<packageSources>
		<clear />
		<add key="local" value="$feed" />
	</packageSources>
</configuration>
"@ | Set-Content (Join-Path $consumer 'nuget.config') -Encoding utf8

		@"
<Project Sdk="Microsoft.NET.Sdk">
	<PropertyGroup>
		<OutputType>Exe</OutputType>
		<TargetFramework>$tfm</TargetFramework>
		$langVersionProperty
		<Nullable>enable</Nullable>
		<ImplicitUsings>enable</ImplicitUsings>
		<!-- Proves the shipped UNI0000/UNI9001 suppressors handle CA1034 and CA1515 on a surface-C union. -->
		<AnalysisMode>All</AnalysisMode>
		<WarningsAsErrors>CA1034;CA1515</WarningsAsErrors>
		<!-- The other all-rules warnings are irrelevant to this gate; CA1034/CA1515 are under test. -->
		<NoWarn>CA1000;CA1050;CA1815;CA1852</NoWarn>
	</PropertyGroup>
	<ItemGroup>
		<PackageReference Include="Purview.Unions" Version="$version" />
	</ItemGroup>
</Project>
"@ | Set-Content (Join-Path $consumer 'Consumer.csproj') -Encoding utf8

		@'
using Purview.Unions;

IntOrString value = 42;

if (!value.TryGetValue(out int number) || number != 42)
{
	Console.Error.WriteLine($"{Environment.Version}: expected int 42, got {value.Value}");
	return 1;
}

if (value is not int)
{
	Console.Error.WriteLine($"{Environment.Version}: the compiler did not treat the generated type as a union.");
	return 1;
}

if (value.Value is not int boxed || boxed != 42)
{
	Console.Error.WriteLine($"{Environment.Version}: Value did not return the case.");
	return 1;
}

IntOrString empty = default;
if (empty.HasValue || empty.TryGetValue(out string _))
{
	Console.Error.WriteLine($"{Environment.Version}: default was not empty.");
	return 1;
}

Outcome<int> outcome = 9;
if (!outcome.TryGetValue(out int result) || result != 9)
{
	Console.Error.WriteLine($"{Environment.Version}: surface C did not hold the value.");
	return 1;
}

Outcome<int> failure = new InvalidOperationException("boom");
if (!failure.TryGetValue(out Exception? error) || error?.Message != "boom")
{
	Console.Error.WriteLine($"{Environment.Version}: surface C did not hold the error.");
	return 1;
}

PackedMaybe<int> packedSome = new PackedSome<int>(7);
if (!packedSome.TryGetValue(out PackedSome<int> packedValue) || packedValue.Value != 7)
{
	Console.Error.WriteLine($"{Environment.Version}: internal surface B did not hold the value.");
	return 1;
}

PackedMaybe<int> packedNone = new PackedNone();
if (!packedNone.TryGetValue(out PackedNone _))
{
	Console.Error.WriteLine($"{Environment.Version}: internal surface B did not hold None.");
	return 1;
}

Console.WriteLine($"{Environment.Version}: {number} (ok)");
return 0;

[GenerateUnion(typeof(int), typeof(string))]
readonly partial struct IntOrString;

readonly record struct PackedSome<T>(T Value);

readonly record struct PackedNone;

[GenerateUnion]
readonly partial struct PackedMaybe<T>
{
	internal static partial PackedMaybe<T> Some(PackedSome<T> value);

	internal static partial PackedMaybe<T> None(PackedNone value);
}

[GenerateUnion]
readonly partial struct Outcome<T> : Outcome<T>.IUnionMembers
{
	public interface IUnionMembers
	{
		static Outcome<T> Create(T? value) => Outcome<T>.CreateCase0(value);

		static Outcome<T> Create(Exception? value) => Outcome<T>.CreateCase1(value);

		object? Value { get; }

		bool HasValue { get; }

		bool TryGetValue(out T value);

		bool TryGetValue(out Exception value);
	}
}
'@ | Set-Content (Join-Path $consumer 'Program.cs') -Encoding utf8

		Push-Location $consumer
		try {
			Write-Host "Running the $tfm consumer..." -ForegroundColor Cyan
			& dotnet run -c $Configuration --nologo
			if ($LASTEXITCODE -ne 0) {
				$failures.Add("The $tfm consumer exited with $LASTEXITCODE.")
			}
			else {
				Write-Host "  $tfm passed" -ForegroundColor Green
			}
		}
		finally {
			Pop-Location
		}

		$outputPath = Join-Path $consumer "bin/$Configuration/$tfm"
		if (Test-Path $outputPath) {
			& (Join-Path $PSScriptRoot 'assert-no-compiler-host-assemblies.ps1') -Path $outputPath
			if ($LASTEXITCODE -ne 0) {
				$failures.Add("The $tfm consumer output carries a compiler-host assembly.")
			}
		}
	}
}
catch {
	$failures.Add($_.Exception.Message)
}
finally {
	if ($null -ne $originalNuGetPackages) {
		$env:NUGET_PACKAGES = $originalNuGetPackages
	}
	else {
		Remove-Item Env:NUGET_PACKAGES -ErrorAction SilentlyContinue
	}

	if ($KeepArtifacts) {
		Write-Host "Scratch consumers kept at $scratch" -ForegroundColor Yellow
	}
	else {
		Remove-Item $scratch -Recurse -Force -ErrorAction SilentlyContinue
	}
}

if ($failures.Count -gt 0) {
	foreach ($failure in $failures) {
		if ($env:GITHUB_ACTIONS) { Write-Host "::error::$failure" } else { Write-Host "ERROR: $failure" -ForegroundColor Red }
	}

	exit 1
}

Write-Host 'Every packed consumer passed.' -ForegroundColor Green
exit 0
