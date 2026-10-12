# Runs Unity in batch mode against this project.
#   .\Tools\unity.ps1                      -> import + compile only
#   .\Tools\unity.ps1 -Method Class.Method -> execute a static editor method
#   .\Tools\unity.ps1 -Tests EditMode      -> run NUnit tests
#   .\Tools\unity.ps1 -Method Emberhall.EditorTools.BuildTools.BuildWindows
# Visual verification: launch Builds\Win\Emberhall.exe with -gm-tour (or -gm-tour-id).
# Add -gm-tour-dir "path" to choose a screenshot directory. Each shot includes a layout audit.
param(
    [string]$Method = "",
    [string]$Tests = "",
    [string]$Filter = "",
    [switch]$Graphics,
    [string]$Log = "Logs\unity_last.log",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.6.1f1\Editor\Unity.exe",
    [string[]]$Extra = @()
)
$proj = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
New-Item -ItemType Directory -Force (Join-Path $proj "Logs") | Out-Null
$logPath = Join-Path $proj $Log
$a = @("-batchmode", "-projectPath", "`"$proj`"", "-logFile", "`"$logPath`"")
if (-not $Graphics) { $a += "-nographics" }
if ($Tests) {
    $a += @("-runTests", "-testPlatform", $Tests, "-testResults", "`"$(Join-Path $proj "Logs\results_$Tests.xml")`"")
    if ($Filter) { $a += @("-testFilter", "`"$Filter`"") }
} else {
    $a += "-quit"
    if ($Method) { $a += @("-executeMethod", $Method) }
}
$a += $Extra
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process $Unity -ArgumentList $a -PassThru -WindowStyle Hidden
# Wait only for the editor. Unity's licensing helper can outlive the build.
$p.WaitForExit()
Write-Host ("Unity exit code {0} in {1:n0}s" -f $p.ExitCode, $sw.Elapsed.TotalSeconds)
$errs = Select-String -Path $logPath -Pattern "error CS\d+|Exception:|Build Finished|Build succeeded|BUILD|Tests? (passed|failed)|Failed|\[GM\]" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Line -Unique | Select-Object -First 60
$errs | ForEach-Object { Write-Host $_ }
exit $p.ExitCode
