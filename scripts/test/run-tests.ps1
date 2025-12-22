# Run Tests Script for WinServiceManager
# This script runs all unit tests and generates coverage report

param(
    [switch]$Verbose,
    [switch]$Coverage,
    [switch]$Watch,
    [string]$Filter = ""
)

# Colors for output
$Colors = @{
    Success = "Green"
    Warning = "Yellow"
    Error = "Red"
    Info = "Cyan"
}

function Write-ColorOutput($Message, $Color = "White") {
    Write-Host $Message -ForegroundColor $Colors[$Color]
}

# Get the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionDir = Split-Path -Parent $ScriptDir
$TestProjectDir = Join-Path $SolutionDir "src\WinServiceManager.Tests"

Write-ColorOutput "======================================" "Info"
Write-ColorOutput "WinServiceManager Test Runner" "Info"
Write-ColorOutput "======================================" "Info"
Write-Host ""

# Check if test project exists
if (-not (Test-Path $TestProjectDir)) {
    Write-ColorOutput "Error: Test project not found at $TestProjectDir" "Error"
    exit 1
}

# Build the solution first
Write-ColorOutput "Building solution..." "Info"
dotnet build $SolutionDir

if ($LASTEXITCODE -ne 0) {
    Write-ColorOutput "Build failed!" "Error"
    exit 1
}

Write-Host ""
Write-ColorOutput "Build successful!" "Success"

# Prepare test command
$TestCommand = "dotnet test $TestProjectDir --logger ""console;verbosity=normal"""
if ($Verbose) {
    $TestCommand += " --logger ""console;verbosity=detailed"""
}
if ($Filter) {
    $TestCommand += " --filter $Filter"
}
if ($Coverage) {
    $TestCommand += " --collect:""XPlat Code Coverage"""
}
if ($Watch) {
    $TestCommand += " --watch"
}

Write-Host ""
Write-ColorOutput "Running tests..." "Info"
Write-Host "Command: $TestCommand" -ForegroundColor Gray
Write-Host ""

# Run tests
Invoke-Expression $TestCommand

# Generate coverage report if requested
if ($Coverage -and $LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-ColorOutput "Generating coverage report..." "Info"

    $CoverageTool = "dotnet tool run reportgenerator"
    $CoverageReportDir = Join-Path $TestProjectDir "TestResults"
    $CoverageXml = Join-Path $CoverageReportDir "coverage.cobertura.xml"
    $CoverageHtml = Join-Path $CoverageReportDir "coverage.html"

    if (Test-Path $CoverageXml) {
        try {
            # Install reportgenerator tool if not already installed
            dotnet tool install --global dotnet-reportgenerator-globaltool

            # Generate HTML report
            & $CoverageTool -reports:$CoverageXml -targetdir:$CoverageReportDir -reporttypes:Html

            Write-ColorOutput "Coverage report generated: $CoverageHtml" "Success"

            # Open the report in default browser
            Start-Process $CoverageHtml
        }
        catch {
            Write-ColorOutput "Failed to generate coverage report: $_" "Warning"
        }
    }
}

Write-Host ""
if ($LASTEXITCODE -eq 0) {
    Write-ColorOutput "All tests passed!" "Success"
}
else {
    Write-ColorOutput "Some tests failed!" "Error"
}

exit $LASTEXITCODE