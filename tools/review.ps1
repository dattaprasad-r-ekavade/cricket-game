param(
    [switch]$RunGameChecks,
    [switch]$CaptureVisuals,
    [switch]$SkipCaptures,
    [switch]$SkipGame)

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "tools/review.ps1 requires PowerShell 7 or newer; detected $($PSVersionTable.PSVersion). Run it with 'pwsh'."
}

$ErrorActionPreference = 'Stop'
$runGameChecks = $RunGameChecks -and !$SkipGame
$captureVisuals = $CaptureVisuals -and !$SkipCaptures
$repoPath = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$balanceBandsPath = Join-Path $PSScriptRoot 'balance-bands.json'
$balanceBands = Get-Content -LiteralPath $balanceBandsPath -Raw | ConvertFrom-Json
$planPath = Join-Path $repoPath 'docs/build/a5-build-plan.md'
$planText = Get-Content -LiteralPath $planPath -Raw
$planSectionSix = [regex]::Match($planText, '(?ms)^## 6\. Gate after wave 3.*?(?=^## |\z)').Value
if ([int]$balanceBands.planSection -ne 6 -or
    [string]::IsNullOrWhiteSpace($planSectionSix) -or
    $planSectionSix -notmatch 'tools/balance-bands\.json') {
    throw 'Balance bands must be defined in tools/balance-bands.json and referenced by section 6 of docs/build/a5-build-plan.md.'
}

function Assert-BalanceBand([string]$Name, $Band) {
    if ($null -eq $Band -or $null -eq $Band.min -or $null -eq $Band.max -or
        [double]$Band.min -gt [double]$Band.max) {
        throw "Balance band '$Name' is missing a valid min/max range in tools/balance-bands.json."
    }
}

$targetBands = $balanceBands.matchBalance
Assert-BalanceBand 'runsPerOver' $targetBands.runsPerOver
Assert-BalanceBand 'wicketsPerInnings.10' $targetBands.wicketsPerInnings.'10'
Assert-BalanceBand 'wicketsPerInnings.20' $targetBands.wicketsPerInnings.'20'
if ([int]$targetBands.maximumInningsScore.'10' -le 0) {
    throw "Balance band 'maximumInningsScore.10' must be a positive value in tools/balance-bands.json."
}
Assert-BalanceBand 'dismissalShares.lbw' $targetBands.dismissalShares.lbw
Assert-BalanceBand 'dismissalShares.caught' $targetBands.dismissalShares.caught
Assert-BalanceBand 'dismissalShares.bowled' $targetBands.dismissalShares.bowled
Assert-BalanceBand 'dismissalShares.runOut' $targetBands.dismissalShares.runOut
Assert-BalanceBand 'strokeSpeed.idealGroundMedianMetersPerSecond' $balanceBands.strokeSpeed.idealGroundMedianMetersPerSecond
Assert-BalanceBand 'strokeSpeed.idealLoftMedianMetersPerSecond' $balanceBands.strokeSpeed.idealLoftMedianMetersPerSecond

function Get-Median([double[]]$Values) {
    $sorted = @($Values | Sort-Object)
    if ($sorted.Count -eq 0) { throw 'A median requires at least one value.' }
    $middle = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) { return [double]$sorted[$middle] }
    return ([double]$sorted[$middle - 1] + [double]$sorted[$middle]) / 2
}

function Get-PhysicsBalanceMetrics([object[]]$Rows, [int]$Overs) {
    $runs = 0
    $legalBalls = 0
    $wickets = 0
    $caught = 0
    $lbw = 0
    $bowled = 0
    $runOuts = 0
    $boundaries = 0
    $powerplayRuns = 0
    $middleRuns = 0
    $deathRuns = 0
    $powerplayBalls = 0
    $middleBalls = 0
    $deathBalls = 0
    $maximumInningsScore = 0
    $powerplayCapacity = [int][math]::Ceiling($Overs * 6 * 0.30)
    $middleCapacity = [int][math]::Ceiling($Overs * 6 * 0.45)

    foreach ($row in $Rows) {
        $firstBalls = [int]$row.first_legal_balls
        $secondBalls = [int]$row.second_legal_balls
        $runs += [int]$row.first_runs + [int]$row.second_runs
        $legalBalls += $firstBalls + $secondBalls
        $wickets += [int]$row.first_wickets + [int]$row.second_wickets
        $caught += [int]$row.caught_dismissals
        $lbw += [int]$row.lbw_dismissals
        $bowled += [int]$row.bowled_dismissals
        $runOuts += [int]$row.run_outs
        $boundaries += [int]$row.boundaries
        $powerplayRuns += [int]$row.powerplay_runs
        $middleRuns += [int]$row.middle_runs
        $deathRuns += [int]$row.death_runs
        $powerplayBalls += [math]::Min($powerplayCapacity, $firstBalls) + [math]::Min($powerplayCapacity, $secondBalls)
        $middleBalls += [math]::Min($middleCapacity, [math]::Max(0, $firstBalls - $powerplayCapacity)) +
            [math]::Min($middleCapacity, [math]::Max(0, $secondBalls - $powerplayCapacity))
        $deathBalls += [math]::Max(0, $firstBalls - $powerplayCapacity - $middleCapacity) +
            [math]::Max(0, $secondBalls - $powerplayCapacity - $middleCapacity)
        $maximumInningsScore = [math]::Max($maximumInningsScore, [math]::Max([int]$row.first_runs, [int]$row.second_runs))
    }

    if ($wickets -le 0 -or $legalBalls -le 0 -or $powerplayBalls -le 0 -or $middleBalls -le 0 -or $deathBalls -le 0) {
        throw "The $Overs-over physics sample is missing dismissals, legal balls, or phase balls."
    }
    return [pscustomobject]@{
        RunsPerOver = $runs * 6 / $legalBalls
        WicketsPerInnings = $wickets / ($Rows.Count * 2)
        MaximumInningsScore = $maximumInningsScore
        BoundariesPerInnings = $boundaries / ($Rows.Count * 2)
        LbwShare = $lbw / $wickets
        CaughtShare = $caught / $wickets
        BowledShare = $bowled / $wickets
        RunOutShare = $runOuts / $wickets
        PowerplayRunsPerOver = $powerplayRuns * 6 / $powerplayBalls
        MiddleRunsPerOver = $middleRuns * 6 / $middleBalls
        DeathRunsPerOver = $deathRuns * 6 / $deathBalls
    }
}

function Assert-TargetBand([string]$Name, [double]$Value, $Band) {
    if ($Value -lt [double]$Band.min -or $Value -gt [double]$Band.max) {
        throw "$Name $([math]::Round($Value, 4)) is outside target [$($Band.min), $($Band.max)]."
    }
}

function Record-TargetBandFailure(
    [string]$Name,
    [double]$Value,
    $Band,
    [System.Collections.Generic.List[string]]$Failures) {
    if ($Value -lt [double]$Band.min -or $Value -gt [double]$Band.max) {
        [void]$Failures.Add("$Name $([math]::Round($Value, 4)) is outside target [$($Band.min), $($Band.max)].")
    }
}

Push-Location -LiteralPath $repoPath
try {
    function Invoke-CheckedDotNet([string[]]$CommandArguments) {
        & dotnet @CommandArguments
        if ($LASTEXITCODE -ne 0) { throw "dotnet failed ($LASTEXITCODE): $($CommandArguments -join ' ')" }
    }

    if (!$runGameChecks -and !$captureVisuals) {
        Write-Output 'Mode: headless review; game checks and visual captures are opt-in.'
    }
    else {
        Write-Output "Mode: headless review plus$(if ($runGameChecks) { ' game checks' })$(if ($captureVisuals) { ' visual captures' })."
    }

    Invoke-CheckedDotNet @('build', 'SuperCricket.sln', '-c', 'Release')
    Invoke-CheckedDotNet @('build', 'trials/godot/SuperCricket.GodotTrial.csproj', '-c', 'Release')
    $toolsDll = 'src/SuperCricket.Tools/bin/Release/net9.0/SuperCricket.Tools.dll'
    $batterContractPath = 'assets/characters/practice-batter.animation-contract.json'
    $bowlerContractPath = 'assets/characters/practice-bowler.animation-contract.json'
    $batterPath = 'assets/characters/practice-batter-humanoid.glb'
    $bowlerPath = 'assets/characters/practice-bowler-humanoid.glb'
    & python -m unittest discover -s 'tests/tools' -p 'test_*.py'
    if ($LASTEXITCODE -ne 0) { throw 'Player animation-contract tests failed.' }
    & python 'tools/blender/validate_humanoid_glb.py' $batterPath --role batter --animation-contract $batterContractPath
    if ($LASTEXITCODE -ne 0) { throw 'The humanoid batter GLB validation failed.' }
    & python 'tools/blender/validate_humanoid_glb.py' $bowlerPath --role bowler --animation-contract $bowlerContractPath
    if ($LASTEXITCODE -ne 0) { throw 'The humanoid bowler GLB validation failed.' }
    $shotsPath = 'assets/batting/shots.json'
    $strokesPath = 'assets/batting/strokes.json'
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
    Invoke-CheckedDotNet @($toolsDll, 'validate-shots', $shotsPath)
    Invoke-CheckedDotNet @($toolsDll, 'validate-strokes', $strokesPath)
    Invoke-CheckedDotNet @($toolsDll, 'validate-deliveries', 'assets/bowling/deliveries.json')
    Invoke-CheckedDotNet @($toolsDll, 'verify-delivery-envelopes')
    Invoke-CheckedDotNet @($toolsDll, 'validate-field', 'assets/fields/practice-attack.json')
    Invoke-CheckedDotNet @($toolsDll, 'validate-fields', 'assets/fields/positions.json', 'assets/fields/presets')
    Invoke-CheckedDotNet @($toolsDll, 'validate-stadium-layout', 'assets/stadium/stadium-layout.json')
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
    $timingCalibration = Get-Content -LiteralPath 'assets/batting/timing-calibration.json' -Raw | ConvertFrom-Json
    foreach ($calibrationCase in @(
        @{ Preset = 'standard'; Delivery = 'Standard pace' },
        @{ Preset = 'wide'; Delivery = 'Wide pace' },
        @{ Preset = 'no-ball'; Delivery = 'No-ball pace' },
        @{ Preset = 'yorker'; Delivery = 'Yorker pace' }
    )) {
        $profiles = @($timingCalibration.deliveryProfiles | Where-Object {
            $_.deliveryName -eq $calibrationCase.Delivery
        })
        if ($profiles.Count -ne 1) { throw "Timing calibration needs one profile for $($calibrationCase.Delivery)." }
        $timingRows = @(Import-Csv -LiteralPath "artifacts/review-$($calibrationCase.Preset)-batting.csv")
        foreach ($shotCalibration in $profiles[0].shots) {
            $bestTiming = $timingRows | Where-Object {
                $_.shot -eq $shotCalibration.shotName -and $_.contact_quality -ne ''
            } | Sort-Object { [double]$_.contact_quality } -Descending | Select-Object -First 1
            if ($null -eq $bestTiming -or
                [math]::Abs([double]$bestTiming.input_delay_s - [double]$shotCalibration.idealInputDelaySeconds) -gt 0.013) {
                throw "Timing calibration drifted for $($calibrationCase.Delivery)/$($shotCalibration.shotName); rerun the analyzer and update the calibration asset."
            }
        }
    }
    Write-Output 'PASS: early/perfect/late timing targets match the highest-quality analyzer samples for all four delivery presets.'
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
    $physicsBalanceRunOuts = ($physicsBalanceRows | Measure-Object -Property run_outs -Sum).Sum
    $physicsBalanceDismissals =
        ($physicsBalanceRows | Measure-Object -Property first_wickets -Sum).Sum +
        ($physicsBalanceRows | Measure-Object -Property second_wickets -Sum).Sum
    $physicsBalanceTwoRunPlans = ($physicsBalanceRows | Measure-Object -Property two_run_plans -Sum).Sum
    $physicsBalanceTwoRunScores = ($physicsBalanceRows | Measure-Object -Property two_run_scores -Sum).Sum
    $physicsBalanceCombinedRuns =
        ($physicsBalanceRows | Measure-Object -Property first_runs -Sum).Sum +
        ($physicsBalanceRows | Measure-Object -Property second_runs -Sum).Sum
    $physicsAverageRunsPerInnings = $physicsBalanceCombinedRuns / ($physicsBalanceRows.Count * 2)
    $physicsBalanceRunOutShare = if ($physicsBalanceDismissals -gt 0) {
        $physicsBalanceRunOuts / $physicsBalanceDismissals
    } else { 0 }
    $physicsInningsScores = @($physicsBalanceRows | ForEach-Object { [int]$_.first_runs; [int]$_.second_runs })
    if ($physicsBalancePlans -ne ($physicsBalanceContacts + $physicsBalanceMisses) -or
        $physicsBalancePlans + $physicsBalanceLeaves -ne $physicsBalanceDeliveries -or
        $physicsBalanceLeaves -gt $physicsBalanceWides -or
        $physicsBalancePickups -le 0 -or $physicsBalanceBoundaries -le 0 -or
        $physicsBalanceTwoRunPlans -le 0 -or
        $physicsBalanceTwoRunPlans -gt $physicsBalanceContacts -or
        $physicsBalanceTwoRunScores -gt ($physicsBalanceTwoRunPlans + $physicsBalanceBoundaries) -or
        $physicsBalanceDismissals -le 0) {
        throw 'Seeded 10-over physics batch produced inconsistent scores or event counts.'
    }
    Write-Output "PASS: six physics-grounded 10-over smoke matches wrote consistent innings and event metrics, including $physicsBalanceLeaves leaves, $physicsBalanceBoundaries boundaries, and $physicsBalanceTwoRunScores doubles."

    foreach ($overs in @(10, 20)) {
        $balancePath = "artifacts/review-physics-balance-$overs-over-standard.csv"
        Invoke-CheckedDotNet @($toolsDll, 'simulate-physics-match-batch', $coastalRosterPath, $highlandRosterPath,
            'assets/fields/practice-attack.json', $batterPath, $bowlerPath, $shotsPath,
            'assets/deliveries/standard-pace.json', 'assets/deliveries/wide-pace.json',
            'assets/deliveries/no-ball-pace.json', '50', [string]$overs, '9100', $balancePath, 'Standard')
        $balanceRows = @(Import-Csv -LiteralPath $balancePath)
        if ($balanceRows.Count -ne 50 -or
            @($balanceRows | Where-Object { [string]::IsNullOrWhiteSpace($_.result) }).Count -gt 0) {
            throw "The $overs-over Standard acceptance batch did not complete all 50 seeded matches."
        }
        $metrics = Get-PhysicsBalanceMetrics $balanceRows $overs
        Assert-TargetBand "$overs-over runs per over" $metrics.RunsPerOver $targetBands.runsPerOver
        Assert-TargetBand "$overs-over wickets per innings" $metrics.WicketsPerInnings $targetBands.wicketsPerInnings.($overs.ToString())
        Assert-TargetBand "$overs-over LBW share" $metrics.LbwShare $targetBands.dismissalShares.lbw
        Assert-TargetBand "$overs-over caught share" $metrics.CaughtShare $targetBands.dismissalShares.caught
        Assert-TargetBand "$overs-over bowled share" $metrics.BowledShare $targetBands.dismissalShares.bowled
        Assert-TargetBand "$overs-over run-out share" $metrics.RunOutShare $targetBands.dismissalShares.runOut
        if ($overs -eq 10 -and $metrics.MaximumInningsScore -gt [int]$targetBands.maximumInningsScore.'10') {
            throw "10-over maximum innings score $($metrics.MaximumInningsScore) exceeds the $($targetBands.maximumInningsScore.'10') target."
        }
        Write-Output ("PASS: 50 Standard matches at {0} overs; {1:0.00} RPO, {2:0.00} wickets/innings, LBW {3:P1}, caught {4:P1}, bowled {5:P1}, run-out {6:P1}, max score {7}." -f
            $overs, $metrics.RunsPerOver, $metrics.WicketsPerInnings, $metrics.LbwShare,
            $metrics.CaughtShare, $metrics.BowledShare, $metrics.RunOutShare, $metrics.MaximumInningsScore)
        if ($overs -eq 20 -and $metrics.DeathRunsPerOver -le $metrics.MiddleRunsPerOver) {
            throw 'T20 death-over scoring did not exceed middle-over scoring.'
        }
    }

    $humanAverageScores = @{}
    $humanGateFailures = [System.Collections.Generic.List[string]]::new()
    foreach ($difficulty in @('Rookie', 'Amateur', 'Standard', 'Pro', 'Legend')) {
        $humanBalancePath = "artifacts/review-physics-balance-20-over-human-$difficulty.csv"
        Invoke-CheckedDotNet @($toolsDll, 'simulate-physics-match-batch', $coastalRosterPath, $highlandRosterPath,
            'assets/fields/practice-attack.json', $batterPath, $bowlerPath, $shotsPath,
            'assets/deliveries/standard-pace.json', 'assets/deliveries/wide-pace.json',
            'assets/deliveries/no-ball-pace.json', '50', '20', '9100', $humanBalancePath, $difficulty, 'Human')
        $humanBalanceRows = @(Import-Csv -LiteralPath $humanBalancePath)
        if ($humanBalanceRows.Count -ne 50 -or
            @($humanBalanceRows | Where-Object { [string]::IsNullOrWhiteSpace($_.result) }).Count -gt 0) {
            throw "The 20-over $difficulty Human-profile acceptance batch did not complete all 50 seeded matches."
        }
        $humanMetrics = Get-PhysicsBalanceMetrics $humanBalanceRows 20
        Record-TargetBandFailure "$difficulty Human runs per over" $humanMetrics.RunsPerOver $targetBands.runsPerOver $humanGateFailures
        Record-TargetBandFailure "$difficulty Human wickets per innings" $humanMetrics.WicketsPerInnings $targetBands.wicketsPerInnings.'20' $humanGateFailures
        Record-TargetBandFailure "$difficulty Human LBW share" $humanMetrics.LbwShare $targetBands.dismissalShares.lbw $humanGateFailures
        Record-TargetBandFailure "$difficulty Human caught share" $humanMetrics.CaughtShare $targetBands.dismissalShares.caught $humanGateFailures
        Record-TargetBandFailure "$difficulty Human bowled share" $humanMetrics.BowledShare $targetBands.dismissalShares.bowled $humanGateFailures
        Record-TargetBandFailure "$difficulty Human run-out share" $humanMetrics.RunOutShare $targetBands.dismissalShares.runOut $humanGateFailures
        $combinedRuns = 0
        foreach ($row in $humanBalanceRows) {
            $combinedRuns += [int]$row.first_runs + [int]$row.second_runs
        }
        $humanAverageScores[$difficulty] = $combinedRuns / $humanBalanceRows.Count
        Write-Output ("RESULT: 50 {0} Human-profile matches; {1:0.00} RPO, {2:0.00} wickets/innings, LBW {3:P1}, caught {4:P1}, bowled {5:P1}, run-out {6:P1}, average combined score {7:0.0}." -f
            $difficulty, $humanMetrics.RunsPerOver, $humanMetrics.WicketsPerInnings, $humanMetrics.LbwShare,
            $humanMetrics.CaughtShare, $humanMetrics.BowledShare, $humanMetrics.RunOutShare, $humanAverageScores[$difficulty])
    }
    if ($humanAverageScores.Rookie -le $humanAverageScores.Legend) {
        [void]$humanGateFailures.Add("Rookie Human-profile average score $($humanAverageScores.Rookie) must exceed Legend $($humanAverageScores.Legend).")
    }
    Write-Output ("RESULT: Rookie Human-profile average score {0:0.0}; Legend {1:0.0}." -f
        $humanAverageScores.Rookie, $humanAverageScores.Legend)

    $strokeAsset = Get-Content -LiteralPath $strokesPath -Raw | ConvertFrom-Json
    $groundIdealMidpoints = @($strokeAsset.strokes | Where-Object { !$_.loft -and $_.intent -eq 'attack' } | ForEach-Object {
        ([double]$_.zones.ideal.minSpeedMetersPerSecond + [double]$_.zones.ideal.maxSpeedMetersPerSecond) / 2
    })
    $loftIdealMidpoints = @($strokeAsset.strokes | Where-Object { $_.loft } | ForEach-Object {
        ([double]$_.zones.ideal.minSpeedMetersPerSecond + [double]$_.zones.ideal.maxSpeedMetersPerSecond) / 2
    })
    $groundIdealMedian = Get-Median $groundIdealMidpoints
    $loftIdealMedian = Get-Median $loftIdealMidpoints
    Assert-TargetBand 'ground Ideal median speed' $groundIdealMedian $balanceBands.strokeSpeed.idealGroundMedianMetersPerSecond
    Assert-TargetBand 'loft Ideal median speed' $loftIdealMedian $balanceBands.strokeSpeed.idealLoftMedianMetersPerSecond
    Write-Output "PASS: Attack Ideal ground median $([math]::Round($groundIdealMedian, 2)) m/s and loft Ideal median $([math]::Round($loftIdealMedian, 2)) m/s."
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

    if ($runGameChecks) {
        $settingsPath = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'SuperCricket/settings.json'
        $settingsHashBefore = if (Test-Path -LiteralPath $settingsPath) { (Get-FileHash -LiteralPath $settingsPath).Hash } else { $null }
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--', '--verify-gameplay')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--verify-live-match', 'artifacts/review-live-match.csv')
        $settingsHashAfter = if (Test-Path -LiteralPath $settingsPath) { (Get-FileHash -LiteralPath $settingsPath).Hash } else { $null }
        if ($settingsHashBefore -ne $settingsHashAfter) { throw 'Game review modes modified saved user preferences.' }
        Write-Output 'PASS: game review modes preserve saved user preferences.'
    }
    if ($captureVisuals) {
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--', '--profile-frames', '90')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-start.png')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-practice-setup.png', '--practice-setup-preview')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-field-overview.png', '--camera', 'field-overview')
        Invoke-CheckedDotNet @($toolsDll, 'capture-spin-paths', 'assets/bowling/deliveries.json',
            'assets/deliveries/standard-pace.json', 'artifacts/wp23-spin-path-comparison.svg')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-behind-striker.png', '--camera', 'behind-striker')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-batting-field-map.png', '--camera', 'behind-striker')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-bowling-target.png', '--bowling-target')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-bowling-meter.png', '--bowling-meter-preview')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-lbw-replay.png', '--lbw-replay-preview')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-batting-feedback.png', '--feedback-preview')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-batting-field-pip.png', '--camera', 'behind-striker', '--live-field-preview')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-bowling-feedback.png', '--bowling-target', '--feedback-preview')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-batting-timing-wait.png', '--camera', 'behind-striker', '--ball-flight-time', '0.10')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-batting-timing-swing.png', '--camera', 'behind-striker', '--ball-flight-time', '0.20')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-batting-timing-late.png', '--camera', 'behind-striker', '--ball-flight-time', '0.40')
        foreach ($difficulty in @('Rookie', 'Standard', 'Pro')) {
            Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
                '--capture-frame', "artifacts/review-contact-zone-$difficulty.png", '--camera', 'behind-striker',
                '--ball-flight-time', '0.10', '--contact-zone-preview', $difficulty)
        }
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-debug-overlay.png', '--camera', 'bowler-end', '--show-debug-overlay')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-ball-follow.png', '--camera', 'ball-follow', '--ball-flight-time', '0.45')
        Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
            '--capture-frame', 'artifacts/review-follow-through.png', '--camera', 'bowler-end', '--delivery-time', '1.0')
        foreach ($action in @('catch', 'pickup', 'throw')) {
            Invoke-CheckedDotNet @('run', '--project', 'src/SuperCricket.Game', '-c', 'Release', '--no-build', '--',
                '--capture-frame', "artifacts/review-fielder-$action.png", '--fielder-action', "fielder-$action", '--action-time', '0.4')
        }
    }
    if ($humanGateFailures.Count -gt 0) {
        throw "Section 6 Human-profile batch gate failed: $($humanGateFailures -join '; ')"
    }
    Write-Output 'Review checks passed. CSVs and any requested renderer captures are in artifacts/.'
}
finally {
    Pop-Location
}
