param(
    [string]$ComposeFile = "deploy/docker/compose.yml",
    [string]$ApiBaseUrl = "http://localhost:3100",
    [string]$RecipientEmail = "recipient@local.test",
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"

function Invoke-Api {
    param([string]$Uri, [string]$Method = "Get", [hashtable]$Headers = @{}, [object]$Body = $null)
    $arguments = @{ Uri = $Uri; Method = $Method; Headers = $Headers }
    if ($null -ne $Body) {
        $arguments.ContentType = "application/json"
        $arguments.Body = $Body | ConvertTo-Json -Depth 6
    }
    Invoke-RestMethod @arguments
}

Write-Host "[1/6] Starting notification-server with Docker Compose..."
docker compose -f $ComposeFile up --build --detach --wait
if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed to start." }

Write-Host "[2/6] Logging in with the local test admin..."
$login = Invoke-Api -Uri "$ApiBaseUrl/v1/auth/login" -Method Post -Body @{
    email = "admin@local.test"
    password = "12345678"
}
$adminHeaders = @{ Authorization = "Bearer $($login.accessToken)" }

$suffix = [Guid]::NewGuid().ToString("N").Substring(0, 10)
Write-Host "[3/6] Creating a temporary API key and GreenMail sender..."
$apiKey = Invoke-Api -Uri "$ApiBaseUrl/v1/api-keys" -Method Post -Headers $adminHeaders -Body @{
    producerName = "Demo source $suffix"
}
$sender = Invoke-Api -Uri "$ApiBaseUrl/v1/senders" -Method Post -Headers $adminHeaders -Body @{
    key = "demo-$suffix"
    host = "greenmail"
    port = 3465
    secure = $true
    username = "mailer"
    password = "secret"
    fromEmail = "mailer@local.test"
    fromName = "Notification Demo"
}

Write-Host "[4/6] Submitting one notification as a simulated source system..."
$machineHeaders = @{ Authorization = "Bearer $($apiKey.key)" }
$accepted = Invoke-Api -Uri "$ApiBaseUrl/v1/notifications" -Method Post -Headers $machineHeaders -Body @{
    senderKey = $sender.key
    subject = "Notification server demo"
    body = "This message proves the API-to-PostgreSQL-to-Worker-to-SMTP flow. Run: $suffix"
    recipients = @(@{ email = $RecipientEmail; ref = "demo-$suffix" })
}
$notificationId = $accepted.notifications[0].id
if (-not $notificationId -or $accepted.accepted -ne 1) { throw "The intake response was invalid." }

Write-Host "[5/6] Waiting for the Worker to send notification $notificationId..."
$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
$state = ""
$detail = $null
while ([DateTimeOffset]::UtcNow -lt $deadline) {
    $detail = Invoke-Api -Uri "$ApiBaseUrl/v1/notifications/$notificationId" -Headers $machineHeaders
    $state = $detail.status
    if ($state -eq "sent" -or $state -eq "failed") { break }
    Start-Sleep -Milliseconds 500
}

$attempt = if ($detail.deliveryAttempts.Count -gt 0) { "$($detail.deliveryAttempts[0].result)|$($detail.deliveryAttempts[0].attemptNo)|$($detail.deliveryAttempts[0].errorCode)" } else { "" }
if ($state -ne "sent" -or $attempt -ne "success|1|") {
    throw "Demo failed. notification=$notificationId status=$state attempt=$attempt. Run 'docker compose -f $ComposeFile logs worker' for safe diagnostics."
}

Write-Host "[6/6] Demo passed." -ForegroundColor Green
[pscustomobject]@{
    notificationId = $notificationId
    recipientEmail = $RecipientEmail.ToLowerInvariant()
    status = $state
    deliveryAttempt = $attempt
    senderKey = $sender.key
    apiBaseUrl = $ApiBaseUrl
} | Format-List

Write-Host "Containers remain running for inspection. Stop them with: docker compose -f $ComposeFile down"
