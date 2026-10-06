param([switch]$SkipCaptures)

$ErrorActionPreference = 'Stop'
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location -LiteralPath $repoPath
try {
    function Invoke-CheckedDotNet([string[]]$CommandArguments) {
        & dotnet @CommandArguments
        if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $($CommandArguments -join ' ')" }
    }

    Invoke-CheckedDotNet @('build', 'SuperCricket.sln', '-c', 'Release')
    $toolsDll = 'src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll'
    $batterPath = 'assets/characters/practice-batter.scplayer.json'
    $bowlerPath = 'assets/characters/practice-bowler.scplayer.json'
    $shotsPath = 'assets/batting/shots.json'
    foreach ($playerPath in @($batterPath, $bowlerPath)) {
        Invoke-CheckedDotNet @($toolsDll, 'validate-player', $playerPath)
    }
    Invoke-CheckedDotNet @($toolsDll, 'validate-shots', $shotsPath)
    Invoke-CheckedDotNet @($toolsDll, 'validate-field', 'assets/fields/practice-attack.json')
    Invoke-CheckedDotNet @($toolsDll, 'verify-batting', $shotsPath)
    Invoke-CheckedDotNet @($toolsDll, 'verify-match')
    Invoke-CheckedDotNet @($toolsDll, 'verify-fielding')
    foreach ($presetName in @('standard', 'wide', 'no-ball')) {
        $deliveryPath = "assets/deliveries/$presetName-pace.json"
        Invoke-CheckedDotNet @($toolsDll, 'validate', $deliveryPath)
        Invoke-CheckedDotNet @($toolsDll, 'simulate', $deliveryPath, "artifacts/review-$presetName-flight.csv")
        Invoke-CheckedDotNet @($toolsDll, 'analyze-batting-practice', $batterPath, $bowlerPath, $shotsPath,
            $deliveryPath, "artifacts/review-$presetName-batting.csv")
    }
    Invoke-CheckedDotNet @($toolsDll, 'verify-batting-practice', $batterPath, $bowlerPath, $shotsPath,
        'assets/deliveries/standard-pace.json')
    Invoke-CheckedDotNet @($toolsDll, 'verify-footwork', $batterPath, $bowlerPath, $shotsPath,
        'assets/deliveries/wide-pace.json')
    Invoke-CheckedDotNet @($toolsDll, 'simulate-over', 'assets/scenarios/practice-over.json')
    Invoke-CheckedDotNet @($toolsDll, 'analyze-field', 'assets/fields/practice-attack.json', 'artifacts/review-field.csv')
    Invoke-CheckedDotNet @($toolsDll, 'analyze-batting-practice', $batterPath, $bowlerPath, $shotsPath,
        'assets/deliveries/standard-pace.json', 'artifacts/review-standard-batting-repeat.csv')
    $firstHash = (Get-FileHash -LiteralPath artifacts/review-standard-batting.csv).Hash
    $repeatHash = (Get-FileHash -LiteralPath artifacts/review-standard-batting-repeat.csv).Hash
    if ($firstHash -ne $repeatHash) { throw 'Repeated batting-practice output differs.' }
    $wideContacts = @(Import-Csv artifacts/review-wide-batting.csv | Where-Object { $_.contact_quality -ne '' })
    if ($wideContacts.Count -eq 0) { throw 'Wide preset produced no bat contact after searching footwork positions.' }
    $wideFootworkContacts = @($wideContacts | Where-Object { [math]::Abs([double]$_.footwork_offset_m) -gt 0.0001 })
    if ($wideFootworkContacts.Count -eq 0) { throw 'Wide contacts did not use a reported footwork step.' }
    & dotnet $toolsDll analyze-batting-practice $batterPath $bowlerPath $shotsPath `
        assets/deliveries/standard-pace.json artifacts/review-invalid.csv 0.001
    if ($LASTEXITCODE -ne 1) { throw 'Invalid timing step did not fail with exit code 1.' }
    Write-Output "PASS: repeated batting CSV hash $firstHash; wide-ball footwork contacts; invalid input rejected."

    Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--', '--verify-gameplay')
    if (!$SkipCaptures) {
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-start.png')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-debug-overlay.png', '--camera', 'bowler-end', '--show-debug-overlay')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-follow-through.png', '--camera', 'bowler-end', '--delivery-time', '1.0')
        foreach ($action in @('catch', 'pickup', 'throw')) {
            Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
                '--capture-frame', "artifacts/review-fielder-$action.png", '--fielder-action', "fielder-$action", '--action-time', '0.4')
        }
    }
    Write-Output 'Review checks passed. CSVs and optional renderer captures are in artifacts/.'
}
finally {
    Pop-Location
}
