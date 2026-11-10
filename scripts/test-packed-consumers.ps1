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

try {
	New-Item -ItemType Directory -Force -Path $feed, $PackOutput | Out-Null

	Write-Host "Packing Purview.Unions $version to $feed..." -ForegroundColor Cyan
	& dotnet pack $project -c $Configuration -o $feed --nologo
	if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE." }

	$package = Join-Path $feed "Purview.Unions.$version.nupkg"
	if (-not (Test-Path $package)) { throw "The packed package '$package' was not produced." }

	foreach ($tfm in @('net10.0', 'net11.0')) {
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
		<LangVersion>preview</LangVersion>
		<Nullable>enable</Nullable>
		<ImplicitUsings>enable</ImplicitUsings>
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

Console.WriteLine($"{Environment.Version}: {number} (ok)");
return 0;

[GenerateUnion(typeof(int), typeof(string))]
public readonly partial struct IntOrString;
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
