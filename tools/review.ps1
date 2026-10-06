param([switch]$SkipCaptures, [switch]$SkipGame)

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
    $coastalRosterPath = 'assets/teams/coastal-xi.json'
    $highlandRosterPath = 'assets/teams/highland-xi.json'
    foreach ($teamPath in @($coastalRosterPath, $highlandRosterPath)) {
        Invoke-CheckedDotNet @($toolsDll, 'validate-team', $teamPath)
    }
    $invalidRosterPath = Join-Path ([System.IO.Path]::GetTempPath()) "super-cricket-invalid-roster-$PID.json"
    try {
        $invalidRoster = Get-Content -LiteralPath $coastalRosterPath -Raw | ConvertFrom-Json
        $invalidRoster.players[0].timing = 101
        $invalidRoster | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $invalidRosterPath -Encoding utf8
        $null = & dotnet $toolsDll validate-team $invalidRosterPath 2>&1
        if ($LASTEXITCODE -ne 1) { throw 'Team validator accepted an out-of-range player rating.' }
    }
    finally {
        Remove-Item -LiteralPath $invalidRosterPath -Force -ErrorAction SilentlyContinue
    }
    Write-Output 'PASS: invalid team batting rating rejected.'
    try {
        $invalidRoster = Get-Content -LiteralPath $coastalRosterPath -Raw | ConvertFrom-Json
        $invalidRoster.primaryKitColorHex = 'blue'
        $invalidRoster | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $invalidRosterPath -Encoding utf8
        $null = & dotnet $toolsDll validate-team $invalidRosterPath 2>&1
        if ($LASTEXITCODE -ne 1) { throw 'Team validator accepted an invalid kit color.' }
    }
    finally {
        Remove-Item -LiteralPath $invalidRosterPath -Force -ErrorAction SilentlyContinue
    }
    Write-Output 'PASS: invalid team kit color rejected.'
    try {
        $invalidRoster = Get-Content -LiteralPath $coastalRosterPath -Raw | ConvertFrom-Json
        $invalidRoster.accentKitColorHex = '#12G45Z'
        $invalidRoster | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $invalidRosterPath -Encoding utf8
        $null = & dotnet $toolsDll validate-team $invalidRosterPath 2>&1
        if ($LASTEXITCODE -ne 1) { throw 'Team validator accepted an invalid accent kit color.' }
    }
    finally {
        Remove-Item -LiteralPath $invalidRosterPath -Force -ErrorAction SilentlyContinue
    }
    Write-Output 'PASS: invalid team accent kit color rejected.'
    $invalidScalePath = Join-Path ([System.IO.Path]::GetTempPath()) "super-cricket-invalid-scale-$PID.json"
    try {
        $invalidScaleAsset = Get-Content -LiteralPath $batterPath -Raw | ConvertFrom-Json
        $invalidScaleAsset.bones[0].bindPose.scale.x = 10.0
        $invalidScaleAsset | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $invalidScalePath -Encoding utf8
        $null = & dotnet $toolsDll validate-player $invalidScalePath 2>&1
        if ($LASTEXITCODE -ne 1) { throw 'Player validator accepted an invalid rig scale.' }
    }
    finally {
        Remove-Item -LiteralPath $invalidScalePath -Force -ErrorAction SilentlyContinue
    }
    Write-Output 'PASS: invalid player rig scale rejected.'
    Invoke-CheckedDotNet @($toolsDll, 'validate-shots', $shotsPath)
    Invoke-CheckedDotNet @($toolsDll, 'validate-field', 'assets/fields/practice-attack.json')
    Invoke-CheckedDotNet @($toolsDll, 'verify-batting', $shotsPath)
    Invoke-CheckedDotNet @($toolsDll, 'verify-match')
    Invoke-CheckedDotNet @($toolsDll, 'verify-match-batch')
    Invoke-CheckedDotNet @($toolsDll, 'verify-fielding')
    $matchBatchPath = 'artifacts/review-match-batch.csv'
    Invoke-CheckedDotNet @($toolsDll, 'simulate-match-batch', $coastalRosterPath, $highlandRosterPath,
        '32', '2', '3026', $matchBatchPath)
    $matchBatchRows = @(Import-Csv -LiteralPath $matchBatchPath)
    if ($matchBatchRows.Count -ne 32) { throw "Automatic match batch wrote $($matchBatchRows.Count) rows instead of 32." }
    if (@($matchBatchRows | Where-Object { [string]::IsNullOrWhiteSpace($_.result) }).Count -gt 0) {
        throw 'Automatic match batch included a result without a match outcome.'
    }
    Write-Output 'PASS: match batch CSV contains 32 completed, scored matches.'
    foreach ($presetName in @('standard', 'wide', 'no-ball', 'yorker')) {
        $deliveryPath = "assets/deliveries/$presetName-pace.json"
        Invoke-CheckedDotNet @($toolsDll, 'validate', $deliveryPath)
        Invoke-CheckedDotNet @($toolsDll, 'simulate', $deliveryPath, "artifacts/review-$presetName-flight.csv")
        Invoke-CheckedDotNet @($toolsDll, 'analyze-batting-practice', $batterPath, $bowlerPath, $shotsPath,
            $deliveryPath, "artifacts/review-$presetName-batting.csv")
    }
    $yorkerPreset = Get-Content -LiteralPath 'assets/deliveries/yorker-pace.json' -Raw | ConvertFrom-Json
    $yorkerFlight = @(Import-Csv -LiteralPath 'artifacts/review-yorker-flight.csv')
    $yorkerBounce = $yorkerFlight | Where-Object { [int]$_.bounces -ge 1 } | Select-Object -First 1
    $yorkerWicketLineZ = -[double]$yorkerPreset.releasePosition.z
    $yorkerCrossing = $yorkerFlight | Where-Object { [double]$_.z_m -le $yorkerWicketLineZ } | Select-Object -First 1
    $pitchEndZ = -[double]$yorkerPreset.pitchLengthMeters / 2
    if ($null -eq $yorkerBounce -or [double]$yorkerBounce.z_m -le $pitchEndZ -or
        [double]$yorkerBounce.z_m -gt -9.0 -or $null -eq $yorkerCrossing -or
        [double]$yorkerCrossing.y_m -ge 0.3) {
        throw 'Yorker flight did not bounce on the pitch near the striker and stay low through the wicket line.'
    }
    Write-Output "PASS: yorker bounced at z=$([double]$yorkerBounce.z_m) m and crossed the wicket line at y=$([double]$yorkerCrossing.y_m) m."
    Invoke-CheckedDotNet @($toolsDll, 'verify-batting-practice', $batterPath, $bowlerPath, $shotsPath,
        'assets/deliveries/standard-pace.json')
    Invoke-CheckedDotNet @($toolsDll, 'verify-batting-practice', $batterPath, $bowlerPath, $shotsPath,
        'assets/deliveries/yorker-pace.json')
    Invoke-CheckedDotNet @($toolsDll, 'verify-cpu-batting', $batterPath, $bowlerPath, $shotsPath,
        'assets/deliveries/standard-pace.json', 'assets/deliveries/wide-pace.json',
        $highlandRosterPath, $coastalRosterPath, 'assets/fields/practice-attack.json')
    $physicsBatchPath = 'artifacts/review-physics-match-batch.csv'
    $physicsBatchRepeatPath = 'artifacts/review-physics-match-batch-repeat.csv'
    foreach ($physicsOutputPath in @($physicsBatchPath, $physicsBatchRepeatPath)) {
        Invoke-CheckedDotNet @($toolsDll, 'simulate-physics-match-batch', $coastalRosterPath, $highlandRosterPath,
            'assets/fields/practice-attack.json', $batterPath, $bowlerPath, $shotsPath,
            'assets/deliveries/standard-pace.json', 'assets/deliveries/wide-pace.json',
            'assets/deliveries/no-ball-pace.json', '3', '1', '3710', $physicsOutputPath)
    }
    $physicsRows = @(Import-Csv -LiteralPath $physicsBatchPath)
    if ($physicsRows.Count -ne 3) { throw "Physics match batch wrote $($physicsRows.Count) rows instead of 3." }
    if (@($physicsRows | Where-Object { [string]::IsNullOrWhiteSpace($_.result) }).Count -gt 0) {
        throw 'Physics match batch included a result without a match outcome.'
    }
    $physicsPlans = ($physicsRows | Measure-Object -Property shot_plans -Sum).Sum
    $physicsContacts = ($physicsRows | Measure-Object -Property contacts -Sum).Sum
    $physicsMisses = ($physicsRows | Measure-Object -Property misses -Sum).Sum
    $physicsLeaves = ($physicsRows | Measure-Object -Property leaves -Sum).Sum
    $physicsWides = ($physicsRows | Measure-Object -Property wide_deliveries -Sum).Sum
    $physicsDeliveries = ($physicsRows | Measure-Object -Property total_deliveries -Sum).Sum
    $physicsPickups = ($physicsRows | Measure-Object -Property ground_pickups -Sum).Sum
    $physicsBoundaries = ($physicsRows | Measure-Object -Property boundaries -Sum).Sum
    $physicsCompletedRuns = ($physicsRows | Measure-Object -Property completed_runs -Sum).Sum
    $physicsRunIntents = ($physicsRows | Measure-Object -Property run_intents -Sum).Sum
    $physicsSafeRuns = ($physicsRows | Measure-Object -Property safe_run_attempts -Sum).Sum
    if ($physicsPlans -ne ($physicsContacts + $physicsMisses) -or
        $physicsPlans + $physicsLeaves -ne $physicsDeliveries -or
        $physicsLeaves -gt $physicsWides -or
        $physicsSafeRuns -gt $physicsRunIntents * 2 -or
        $physicsPickups + $physicsBoundaries -le 0 -or
        $physicsCompletedRuns -le 0) {
        throw 'Physics match batch produced inconsistent contact or running metrics.'
    }
    $physicsBatchHash = (Get-FileHash -LiteralPath $physicsBatchPath).Hash
    $physicsBatchRepeatHash = (Get-FileHash -LiteralPath $physicsBatchRepeatPath).Hash
    if ($physicsBatchHash -ne $physicsBatchRepeatHash) { throw 'Repeated physics match-batch output differs.' }
    Write-Output "PASS: physics match batch replayed exactly with $physicsPlans shot plans, $physicsLeaves leaves, $physicsBoundaries boundaries, and $physicsCompletedRuns completed runs."
    $physicsBalancePath = 'artifacts/review-physics-balance-10-over.csv'
    Invoke-CheckedDotNet @($toolsDll, 'simulate-physics-match-batch', $coastalRosterPath, $highlandRosterPath,
        'assets/fields/practice-attack.json', $batterPath, $bowlerPath, $shotsPath,
        'assets/deliveries/standard-pace.json', 'assets/deliveries/wide-pace.json',
        'assets/deliveries/no-ball-pace.json', '6', '10', '7300', $physicsBalancePath)
    $physicsBalanceRows = @(Import-Csv -LiteralPath $physicsBalancePath)
    if ($physicsBalanceRows.Count -ne 6 -or
        @($physicsBalanceRows | Where-Object { [string]::IsNullOrWhiteSpace($_.result) }).Count -gt 0) {
        throw 'Physics balance batch did not complete all six seeded 10-over matches.'
    }
    $physicsBalancePlans = ($physicsBalanceRows | Measure-Object -Property shot_plans -Sum).Sum
    $physicsBalanceContacts = ($physicsBalanceRows | Measure-Object -Property contacts -Sum).Sum
    $physicsBalanceMisses = ($physicsBalanceRows | Measure-Object -Property misses -Sum).Sum
    $physicsBalanceLeaves = ($physicsBalanceRows | Measure-Object -Property leaves -Sum).Sum
    $physicsBalanceWides = ($physicsBalanceRows | Measure-Object -Property wide_deliveries -Sum).Sum
    $physicsBalanceDeliveries = ($physicsBalanceRows | Measure-Object -Property total_deliveries -Sum).Sum
    $physicsBalancePickups = ($physicsBalanceRows | Measure-Object -Property ground_pickups -Sum).Sum
    $physicsBalanceBoundaries = ($physicsBalanceRows | Measure-Object -Property boundaries -Sum).Sum
    $physicsBalanceCatches = ($physicsBalanceRows | Measure-Object -Property catches -Sum).Sum
    $physicsBalanceRunOuts = ($physicsBalanceRows | Measure-Object -Property run_outs -Sum).Sum
    $physicsBalanceTwoRunPlans = ($physicsBalanceRows | Measure-Object -Property two_run_plans -Sum).Sum
    $physicsBalanceTwoRunScores = ($physicsBalanceRows | Measure-Object -Property two_run_scores -Sum).Sum
    $physicsBalanceUnscoredDoublePlans = $physicsBalanceTwoRunPlans - $physicsBalanceTwoRunScores
    $physicsBalanceCombinedRuns =
        ($physicsBalanceRows | Measure-Object -Property first_runs -Sum).Sum +
        ($physicsBalanceRows | Measure-Object -Property second_runs -Sum).Sum
    $physicsAverageRunsPerInnings = $physicsBalanceCombinedRuns / ($physicsBalanceRows.Count * 2)
    $physicsInningsScores = @($physicsBalanceRows | ForEach-Object { [int]$_.first_runs; [int]$_.second_runs })
    if ($physicsBalancePlans -ne ($physicsBalanceContacts + $physicsBalanceMisses) -or
        $physicsBalancePlans + $physicsBalanceLeaves -ne $physicsBalanceDeliveries -or
        $physicsBalanceLeaves -gt $physicsBalanceWides -or
        $physicsBalancePickups -le 0 -or $physicsBalanceBoundaries -le 0 -or
        $physicsBalanceCatches -le 0 -or
        $physicsBalanceTwoRunPlans -le 0 -or
        $physicsBalanceUnscoredDoublePlans -gt $physicsBalanceCatches -or $physicsBalanceRunOuts -ne 0 -or
        $physicsAverageRunsPerInnings -lt 80 -or $physicsAverageRunsPerInnings -gt 120 -or
        ($physicsInningsScores | Measure-Object -Minimum).Minimum -lt 40 -or
        ($physicsInningsScores | Measure-Object -Maximum).Maximum -gt 150) {
        throw 'Seeded 10-over physics scores or event mix fell outside the calibrated review range.'
    }
    Write-Output "PASS: six physics-grounded 10-over matches averaged $([math]::Round($physicsAverageRunsPerInnings, 1)) runs per innings with $physicsBalanceLeaves leaves, boundaries, pickups, catches, and $physicsBalanceTwoRunScores/$physicsBalanceTwoRunPlans planned doubles scored; any unscored plan was resolved by a catch, with no run-outs."
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

    if (!$SkipGame) {
        $settingsPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'SuperCricket/settings.json'
        $settingsHashBefore = if (Test-Path -LiteralPath $settingsPath) { (Get-FileHash -LiteralPath $settingsPath).Hash } else { $null }
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--', '--verify-gameplay')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--verify-live-match', 'artifacts/review-live-match.csv')
        $settingsHashAfter = if (Test-Path -LiteralPath $settingsPath) { (Get-FileHash -LiteralPath $settingsPath).Hash } else { $null }
        if ($settingsHashBefore -ne $settingsHashAfter) { throw 'Game review modes modified saved user preferences.' }
        Write-Output 'PASS: game review modes preserve saved user preferences.'
    }
    if (!$SkipCaptures) {
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--', '--profile-frames', '90')
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
