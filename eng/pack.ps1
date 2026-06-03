#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pack the DotnetOpenEhr SDK with a single UTC build timestamp
    shared across every shipping csproj.

.DESCRIPTION
    Captures [DateTime]::UtcNow once at script start, formats it as
    'yyyy.MMdd.HHmm', and passes it to `dotnet pack` via
    -p:BuildTimestampUtc=<value>. Without this wrapper, a raw
    `dotnet pack dotnet-openehr-sdk.slnx` lets each csproj evaluate
    UtcNow independently; a pack that straddles an HHmm boundary
    can mint the umbrella `DotnetOpenEhr` metapackage referencing
    one of its `DotnetOpenEhr.*` siblings at a different minute
    than the sibling's own packed `<version>`. That failure mode
    would break consumer resolution from a feed that carries only
    the two packed versions.

    Use this wrapper for any pack intended for nuget.org publish.
    The publish workflow at `.github/workflows/nuget-tool.yml` does
    the equivalent capture inline. Raw `dotnet pack` remains fine
    for local exploration whose output is not pushed to a public
    feed.

.PARAMETER Configuration
    MSBuild configuration. Defaults to 'Release'.

.PARAMETER Output
    Output directory for the produced .nupkg / .snupkg files.
    Defaults to './nupkg' under the repo root.

.PARAMETER Solution
    Solution or project to pack. Defaults to
    'dotnet-openehr-sdk.slnx' at the repo root.

.PARAMETER NoBuild
    Pass '--no-build' to `dotnet pack`. You must have already run
    `dotnet build` with the same -p:BuildTimestampUtc value
    (otherwise the DLL's AssemblyVersion / FileVersion will not
    match the .nuspec's <version>).

.EXAMPLE
    ./eng/pack.ps1
    # Build + pack the whole SDK at one shared timestamp into
    # ./nupkg.

.EXAMPLE
    ./eng/pack.ps1 -Output ./out
    # Same, but write packages to ./out.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Output,
    [string]$Solution,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
if (-not $Output) {
    $Output = Join-Path $repoRoot 'nupkg'
}
if (-not $Solution) {
    $Solution = Join-Path $repoRoot 'dotnet-openehr-sdk.slnx'
}

$timestamp = [DateTime]::UtcNow.ToString('yyyy.MMdd.HHmm')
Write-Host "Packing with BuildTimestampUtc=$timestamp" -ForegroundColor Cyan
Write-Host "  Solution:      $Solution"
Write-Host "  Configuration: $Configuration"
Write-Host "  Output:        $Output"

$packArgs = @(
    'pack', $Solution,
    '-c', $Configuration,
    '-o', $Output,
    "-p:BuildTimestampUtc=$timestamp"
)
if ($NoBuild) {
    $packArgs += '--no-build'
}

& dotnet @packArgs
exit $LASTEXITCODE
