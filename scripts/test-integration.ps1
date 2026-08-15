param(
    [string]$ComposeFile = "deploy/docker/compose.yml",
    [string]$ApiBaseUrl = "http://localhost:3100"
)

$ErrorActionPreference = "Stop"
$composeProject = "notification-integration-$PID"

try {
    docker compose -p $composeProject -f $ComposeFile up --build --detach --wait
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed to start." }

    $live = Invoke-WebRequest -Uri "$ApiBaseUrl/health/live" -UseBasicParsing
    if ($live.StatusCode -ne 200) { throw "Liveness returned $($live.StatusCode)." }

    $ready = Invoke-WebRequest -Uri "$ApiBaseUrl/health" -UseBasicParsing
    if ($ready.StatusCode -ne 200) { throw "Readiness returned $($ready.StatusCode)." }

    $correlationId = "integration-$PID"
    $response = Invoke-WebRequest -Uri "$ApiBaseUrl/health/live" -Headers @{ "X-Correlation-ID" = $correlationId } -UseBasicParsing
    if ($response.Headers["X-Correlation-ID"] -ne $correlationId) { throw "Correlation ID was not preserved." }

    $seedConflict = @{ tenantName = "Other"; tenantSlug = "test-organization"; adminEmail = "other@local.test"; adminPassword = "12345678" } | ConvertTo-Json
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/tenants/register" -Method Post -ContentType "application/json" -Body $seedConflict -UseBasicParsing | Out-Null
        throw "Seed tenant was not created before API became ready."
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 409) { throw }
    }

    $slug = "integration-$PID"
    $registration = @{ tenantName = "Integration Tenant"; tenantSlug = $slug; adminEmail = "$slug@local.test"; adminPassword = "12345678" } | ConvertTo-Json
    $created = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/tenants/register" -Method Post -ContentType "application/json" -Body $registration -UseBasicParsing
    if ($created.StatusCode -ne 201) { throw "Registration returned $($created.StatusCode)." }
    if ($created.Content -match "12345678|passwordHash") { throw "Registration response leaked password data." }

    docker compose -p $composeProject -f $ComposeFile stop redis
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed to stop Redis." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/health" -UseBasicParsing | Out-Null
        throw "Readiness stayed healthy after Redis stopped."
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 503) { throw }
    }

    $live = Invoke-WebRequest -Uri "$ApiBaseUrl/health/live" -UseBasicParsing
    if ($live.StatusCode -ne 200) { throw "Liveness failed with Redis stopped." }

    $workerId = docker compose -p $composeProject -f $ComposeFile ps --quiet worker
    $workerUnhealthy = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        $workerHealth = docker inspect --format "{{.State.Health.Status}}" $workerId
        if ($workerHealth -eq "unhealthy") {
            $workerUnhealthy = $true
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $workerUnhealthy) { throw "Worker did not report unhealthy after Redis stopped." }

    docker compose -p $composeProject -f $ComposeFile stop api worker
    if ($LASTEXITCODE -ne 0) { throw "Failed to stop API/Worker before migration rollback." }
    docker compose -p $composeProject -f $ComposeFile run --rm migrate migrate 0
    if ($LASTEXITCODE -ne 0) { throw "InitialIdentity rollback failed." }
    docker compose -p $composeProject -f $ComposeFile run --rm migrate migrate latest
    if ($LASTEXITCODE -ne 0) { throw "InitialIdentity re-apply failed." }
}
finally {
    docker compose -p $composeProject -f $ComposeFile down --volumes --remove-orphans
}
