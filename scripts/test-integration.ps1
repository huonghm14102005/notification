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

    $loginBody = @{ email = "admin@local.test"; password = "12345678" } | ConvertTo-Json
    $login = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
    if (-not $login.accessToken -or -not $login.refreshToken) { throw "Login did not issue both tokens." }
    if ($login.accessToken -eq $login.refreshToken) { throw "Access and refresh tokens must differ." }

    $refreshBody = @{ refreshToken = $login.refreshToken } | ConvertTo-Json
    $refreshed = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/auth/refresh" -Method Post -ContentType "application/json" -Body $refreshBody
    if (-not $refreshed.refreshToken -or $refreshed.refreshToken -eq $login.refreshToken) { throw "Refresh token was not rotated." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/auth/refresh" -Method Post -ContentType "application/json" -Body $refreshBody -UseBasicParsing | Out-Null
        throw "Rotated refresh token was accepted twice."
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw }
    }

    $concurrentLogin = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
    $concurrentBody = @{ refreshToken = $concurrentLogin.refreshToken } | ConvertTo-Json
    $refreshJob = {
        param($url, $body)
        try { (Invoke-WebRequest -Uri $url -Method Post -ContentType "application/json" -Body $body -UseBasicParsing).StatusCode }
        catch { $_.Exception.Response.StatusCode.value__ }
    }
    $jobs = 1..2 | ForEach-Object { Start-Job -ScriptBlock $refreshJob -ArgumentList "$ApiBaseUrl/v1/auth/refresh", $concurrentBody }
    $codes = $jobs | Wait-Job | Receive-Job
    $jobs | Remove-Job
    if (($codes | Where-Object { $_ -eq 200 }).Count -ne 1 -or ($codes | Where-Object { $_ -eq 401 }).Count -ne 1) {
        throw "Concurrent refresh expected one 200 and one 401, got: $($codes -join ',')."
    }

    $logoutBody = @{ refreshToken = $refreshed.refreshToken } | ConvertTo-Json
    $logout = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/auth/logout" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer $($refreshed.accessToken)" } -Body $logoutBody -UseBasicParsing
    if ($logout.StatusCode -ne 204) { throw "Logout returned $($logout.StatusCode)." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/auth/refresh" -Method Post -ContentType "application/json" -Body $logoutBody -UseBasicParsing | Out-Null
        throw "Logged-out refresh token remained valid."
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw }
    }

    $adminHeaders = @{ Authorization = "Bearer $($refreshed.accessToken)" }
    $apiKeyBody = @{ producerName = "Integration Producer" } | ConvertTo-Json
    $issuedKey = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/api-keys" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $apiKeyBody
    if ($issuedKey.key -notmatch '^notify_[0-9a-f]{64}$') { throw "Issued API key has an invalid format." }
    if ($issuedKey.keyPrefix -ne $issuedKey.key.Substring(0, 19)) { throw "API key prefix does not match the raw key." }
    $keyListRaw = (Invoke-WebRequest -Uri "$ApiBaseUrl/v1/api-keys" -Headers $adminHeaders -UseBasicParsing).Content
    if ($keyListRaw -match [Regex]::Escape($issuedKey.key) -or $keyListRaw -match 'keyHash') { throw "API key list leaked secret material." }
    $secondKey = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/api-keys" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $apiKeyBody
    $firstPage = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/api-keys?limit=1" -Headers $adminHeaders
    if ($firstPage.items.Count -ne 1 -or -not $firstPage.nextCursor) { throw "API key cursor first page is invalid." }
    $encodedCursor = [Uri]::EscapeDataString($firstPage.nextCursor)
    $secondPage = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/api-keys?limit=1&cursor=$encodedCursor" -Headers $adminHeaders
    if ($secondPage.items.Count -ne 1 -or $secondPage.items[0].id -eq $firstPage.items[0].id) { throw "API key cursor pagination repeated or skipped an item." }

    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/api-keys" -Headers @{ Authorization = "Bearer $($issuedKey.key)" } -UseBasicParsing | Out-Null
        throw "Machine API key was accepted by an admin endpoint."
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw }
    }

    $tenantLoginBody = @{ email = "$slug@local.test"; password = "12345678" } | ConvertTo-Json
    $tenantLogin = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/auth/login" -Method Post -ContentType "application/json" -Body $tenantLoginBody
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/api-keys/$($issuedKey.id)" -Method Delete -Headers @{ Authorization = "Bearer $($tenantLogin.accessToken)" } -UseBasicParsing | Out-Null
        throw "A different tenant revoked an API key."
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw }
    }

    $revoked = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/api-keys/$($issuedKey.id)" -Method Delete -Headers $adminHeaders -UseBasicParsing
    if ($revoked.StatusCode -ne 204) { throw "API key revoke returned $($revoked.StatusCode)." }
    $revokedAgain = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/api-keys/$($issuedKey.id)" -Method Delete -Headers $adminHeaders -UseBasicParsing
    if ($revokedAgain.StatusCode -ne 204) { throw "Idempotent API key revoke returned $($revokedAgain.StatusCode)." }
    $keyList = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/api-keys" -Headers $adminHeaders
    if (($keyList.items | Where-Object { $_.id -eq $issuedKey.id }).status -ne "revoked") { throw "Revoked API key metadata was not retained." }

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
