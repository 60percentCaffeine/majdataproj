param(
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$DotnetPath = "C:\Program Files\dotnet\dotnet.exe"
$IntegrationProject = Join-Path $ProjectRoot "mod-test-tools\integration\ModTest.Integration.Tests.csproj"
$ResultsDir = Join-Path $ProjectRoot ".scratch\mod-test-tools-artifacts\integration-test-results"

function Get-IntegrationTests {
    $arguments = @(
        "test",
        $IntegrationProject,
        "-c",
        "Release",
        "--nologo",
        "--list-tests"
    )

    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $arguments += @("--filter", $Filter)
    }

    $output = & $DotnetPath @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test --list-tests failed with exit code $LASTEXITCODE"
    }

    $tests = @()
    $inList = $false
    foreach ($line in $output) {
        if ($line -match "The following Tests are available:") {
            $inList = $true
            continue
        }

        if (-not $inList) {
            continue
        }

        $testName = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($testName)) {
            continue
        }

        $tests += $testName
    }

    return $tests
}

function Get-SafeResultName {
    param([string]$TestName)

    $safe = $TestName -replace "[^A-Za-z0-9_.-]", "_"
    if ($safe.Length -le 120) {
        return $safe
    }

    return $safe.Substring($safe.Length - 120)
}

function Format-Duration {
    param([TimeSpan]$Duration)

    if ($Duration.TotalMinutes -ge 1) {
        return $Duration.ToString("m\:ss\.fff")
    }

    return $Duration.ToString("s\.fff") + "s"
}

if (Test-Path $ResultsDir) {
    Remove-Item -Recurse -Force $ResultsDir
}
New-Item -ItemType Directory -Force -Path $ResultsDir | Out-Null

Get-Process MajdataPlay,ModTestReplClient -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

& (Join-Path $ProjectRoot "mod-test-tools\test-hook-mod\install.ps1")
& (Join-Path $ProjectRoot "mod-test-tools\repl\install.ps1")
& (Join-Path $ProjectRoot "mod-test-tools\harness\build.ps1")
& (Join-Path $PSScriptRoot "install.ps1")

$env:MODTEST_PROJECT_ROOT = $ProjectRoot.Path
try {
    $tests = @(Get-IntegrationTests)
    if ($tests.Count -eq 0) {
        if ([string]::IsNullOrWhiteSpace($Filter)) {
            throw "No integration tests were discovered."
        }

        throw "No integration tests matched filter '$Filter'."
    }

    Write-Host "Discovered $($tests.Count) integration test(s)."
    for ($index = 0; $index -lt $tests.Count; $index++) {
        $testName = $tests[$index]
        $remainingBeforeRun = $tests.Count - $index - 1
        $ordinal = $index + 1
        Write-Host "[$ordinal/$($tests.Count)] Starting: $testName ($remainingBeforeRun left after this)"

        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $resultName = Get-SafeResultName $testName
        & $DotnetPath test $IntegrationProject `
            -c Release `
            --nologo `
            --no-build `
            --results-directory $ResultsDir `
            --filter "FullyQualifiedName=$testName" `
            --logger "trx;LogFileName=$resultName.trx"
        $exitCode = $LASTEXITCODE
        $stopwatch.Stop()

        $elapsed = Format-Duration $stopwatch.Elapsed
        if ($exitCode -ne 0) {
            Write-Host "[$ordinal/$($tests.Count)] Failed: $testName in $elapsed ($remainingBeforeRun left unrun)"
            throw "dotnet test failed for '$testName' with exit code $exitCode"
        }

        Write-Host "[$ordinal/$($tests.Count)] Passed: $testName in $elapsed ($remainingBeforeRun left)"
    }
}
finally {
    Remove-Item Env:MODTEST_PROJECT_ROOT -ErrorAction SilentlyContinue
    Get-Process ModTestReplClient -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

Write-Host "Integration test artifacts written to $ResultsDir"
