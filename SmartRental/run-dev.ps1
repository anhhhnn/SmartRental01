$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiPath = Join-Path $projectRoot "AI\api"
$apiMain = Join-Path $apiPath "main.py"
$apiUrl = "http://127.0.0.1:8000"
$apiHealthUrl = "$apiUrl/health"
$apiOpenApiUrl = "$apiUrl/openapi.json"
$apiStartupTimeout = [TimeSpan]::FromSeconds(90)

if (-not (Get-Command py -ErrorAction SilentlyContinue)) {
    Write-Error "Python launcher 'py' was not found. Install Python or add the launcher to PATH."
    exit 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "'dotnet' was not found. Install the .NET SDK or add it to PATH."
    exit 1
}

if (-not (Test-Path -LiteralPath $apiMain -PathType Leaf)) {
    Write-Error "FastAPI entry point was not found: $apiMain"
    exit 1
}

function Test-SmartRentalAiApi {
    try {
        $health = Invoke-RestMethod -Uri $apiHealthUrl -TimeoutSec 3
        $openApi = Invoke-RestMethod -Uri $apiOpenApiUrl -TimeoutSec 3
        $routeNames = @($openApi.paths.PSObject.Properties.Name)
        return (
            $openApi.info.title -eq "SmartRental AI Search API" -and
            $routeNames -contains "/health" -and
            $routeNames -contains "/parse-search" -and
            $routeNames -contains "/recommend/{user_id}" -and
            $health.search -eq "ready" -and
            $health.status -in @("ok", "degraded")
        )
    }
    catch {
        return $false
    }
}

function Wait-SmartRentalAiApi {
    param([TimeSpan]$Timeout)

    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.Elapsed -lt $Timeout) {
        if (Test-SmartRentalAiApi) {
            return $true
        }

        # Polling drives readiness; this interval only avoids a busy loop.
        Start-Sleep -Milliseconds 500
    }

    return $false
}

function Get-Port8000Listeners {
    return @(Get-NetTCPConnection -LocalPort 8000 -State Listen -ErrorAction SilentlyContinue)
}

function Get-ListenerDescription {
    param($Listeners)

    $descriptions = foreach ($listener in @($Listeners)) {
        $ownerProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($listener.OwningProcess)" -ErrorAction SilentlyContinue
        if ($ownerProcess) {
            "PID $($listener.OwningProcess) ($($ownerProcess.Name)): $($ownerProcess.CommandLine)"
        }
        else {
            "PID $($listener.OwningProcess)"
        }
    }

    return ($descriptions -join "; ")
}

# Serialize the port check and process start so concurrent script launches cannot
# both observe a free port and create duplicate FastAPI instances.
$startupMutex = [Threading.Mutex]::new($false, "Local\SmartRental.Dev.FastApi.Startup")
$ownsStartupMutex = $false
$startupError = $null

try {
    try {
        $ownsStartupMutex = $startupMutex.WaitOne([TimeSpan]::FromSeconds(120))
    }
    catch [Threading.AbandonedMutexException] {
        $ownsStartupMutex = $true
    }

    if (-not $ownsStartupMutex) {
        $startupError = "Timed out waiting for another SmartRental startup check to finish."
    }
    else {
        $listeners = @(Get-Port8000Listeners)
        if (@($listeners).Count -gt 0) {
            if (Test-SmartRentalAiApi) {
                Write-Host "SmartRental FastAPI is already ready at $apiUrl; reusing it."
            }
            else {
                $owner = Get-ListenerDescription $listeners
                $startupError = "Port 8000 is already in use by a process that is not a ready SmartRental FastAPI. It was not stopped. Owner: $owner"
            }
        }
        else {
            # Run FastAPI as a hidden helper; ASP.NET remains in this terminal.
            $escapedApiPath = $apiPath.Replace("'", "''")
            $apiCommand = "`$Host.UI.RawUI.WindowTitle = 'SmartRental FastAPI'; Set-Location -LiteralPath '$escapedApiPath'; py -m uvicorn main:app"
            try {
                Start-Process -FilePath "powershell.exe" -ArgumentList @("-NoProfile", "-Command", $apiCommand) -WindowStyle Hidden | Out-Null
                Write-Host "Started FastAPI; polling /health for readiness..."

                if (Wait-SmartRentalAiApi -Timeout $apiStartupTimeout) {
                    Write-Host "SmartRental FastAPI is ready at $apiUrl."
                }
                else {
                    $listeners = @(Get-Port8000Listeners)
                    if (@($listeners).Count -gt 0) {
                        $owner = Get-ListenerDescription $listeners
                        $startupError = "FastAPI did not become ready within $($apiStartupTimeout.TotalSeconds) seconds. Port 8000 owner: $owner."
                    }
                    else {
                        $startupError = "FastAPI did not become ready within $($apiStartupTimeout.TotalSeconds) seconds, and no process is listening on port 8000."
                    }
                }
            }
            catch {
                $startupError = "Could not start FastAPI: $($_.Exception.Message)"
            }
        }
    }
}
finally {
    if ($ownsStartupMutex) {
        $startupMutex.ReleaseMutex()
    }
    $startupMutex.Dispose()
}

if ($startupError) {
    Write-Error $startupError
    exit 1
}

Write-Host "Starting SmartRental ASP.NET Core in this terminal from $projectRoot ..."
Set-Location -LiteralPath $projectRoot
& dotnet run
exit $LASTEXITCODE
