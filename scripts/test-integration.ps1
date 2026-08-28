param(
    [string]$ComposeFile = "deploy/docker/compose.yml",
    [string]$ApiBaseUrl = "http://localhost:3100",
    [string]$CallbackReceiverUrl = "http://localhost:3101",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$composeProject = "notification-integration-$PID"

try {
    if ($SkipBuild) {
        docker compose -p $composeProject -f $ComposeFile up --detach --wait
    }
    else {
        docker compose -p $composeProject -f $ComposeFile up --build --detach --wait
    }
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed to start." }

    $live = Invoke-WebRequest -Uri "$ApiBaseUrl/health/live" -UseBasicParsing
    if ($live.StatusCode -ne 200) { throw "Liveness returned $($live.StatusCode)." }

    $ready = Invoke-WebRequest -Uri "$ApiBaseUrl/health" -UseBasicParsing
    if ($ready.StatusCode -ne 200) { throw "Readiness returned $($ready.StatusCode)." }

    $adminWeb = Invoke-WebRequest -Uri "http://localhost:3200/" -UseBasicParsing
    if ($adminWeb.StatusCode -ne 200 -or $adminWeb.Content -notmatch '<div id="root"></div>') { throw "Admin web did not serve the SPA." }
    if ($adminWeb.Headers["Content-Security-Policy"] -notmatch "default-src 'self'") { throw "Admin web CSP header is missing." }

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

    $memberEmail = "member-$PID@local.test"
    $memberBody = @{ email = $memberEmail; password = "12345678"; displayName = "Integration Member" } | ConvertTo-Json
    $member = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/users" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $memberBody
    if ($member.role -ne "member" -or $member.status -ne "active" -or $member.deviceCount -ne 0) { throw "AUTH-004 create contract is invalid." }
    $users = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/users?status=active&limit=100" -Headers $adminHeaders
    if (@($users.items | Where-Object { $_.id -eq $member.id }).Count -ne 1) { throw "AUTH-004 list omitted the member." }

    $memberLoginBody = @{ email = $memberEmail; password = "12345678" } | ConvertTo-Json
    $memberLogin = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/auth/login" -Method Post -ContentType "application/json" -Body $memberLoginBody
    $memberHeaders = @{ Authorization = "Bearer $($memberLogin.accessToken)" }
    $me = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/users/me" -Headers $memberHeaders
    if ($me.id -ne $member.id -or $me.displayName -ne "Integration Member") { throw "AUTH-004 profile is invalid." }
    try { Invoke-WebRequest -Uri "$ApiBaseUrl/v1/users" -Headers $memberHeaders -UseBasicParsing | Out-Null; throw "Member accessed owner user list." }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 403) { throw } }

    $memberDevice = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices" -Method Post -ContentType "application/json" -Headers $memberHeaders -Body (@{ name = "Member Device $PID"; role = "source" } | ConvertTo-Json)
    $memberKey = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices/$($memberDevice.id)/api-keys" -Method Post -Headers $memberHeaders
    $ownerView = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices/$($memberDevice.id)" -Headers $adminHeaders
    if ($ownerView.ownerUserId -ne $member.id) { throw "Owner could not manage the member device." }
    Invoke-WebRequest -Uri "$ApiBaseUrl/v1/users/$($member.id)/disable" -Method Post -Headers $adminHeaders -UseBasicParsing | Out-Null
    Invoke-WebRequest -Uri "$ApiBaseUrl/v1/users/$($member.id)/disable" -Method Post -Headers $adminHeaders -UseBasicParsing | Out-Null
    try { Invoke-WebRequest -Uri "$ApiBaseUrl/v1/users/me" -Headers $memberHeaders -UseBasicParsing | Out-Null; throw "Disabled member JWT remained active." }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }
    try { Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/00000000-0000-0000-0000-000000000000" -Headers @{ Authorization = "Bearer $($memberKey.key)" } -UseBasicParsing | Out-Null; throw "Disabled member API key remained active." }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }

    $deviceBody = @{ name = "DRL Device $PID"; role = "source" } | ConvertTo-Json
    $device = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $deviceBody
    $duplicateDevice = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $deviceBody
    if ($device.id -eq $duplicateDevice.id -or $device.status -ne "active" -or $device.ownerUserId -eq $null) { throw "DEVICE-001 create contract is invalid." }
    $deviceList = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices?scope=mine&status=active&limit=1" -Headers $adminHeaders
    if ($deviceList.items.Count -ne 1 -or -not $deviceList.nextCursor) { throw "DEVICE-001 pagination is invalid." }
    $renamedDevice = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices/$($device.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body (@{ name = "DRL Renamed $PID" } | ConvertTo-Json)
    if ($renamedDevice.name -ne "DRL Renamed $PID" -or $renamedDevice.role -ne "source") { throw "DEVICE-001 rename changed immutable fields." }
    $deviceKey = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices/$($device.id)/api-keys" -Method Post -Headers $adminHeaders
    $secondDeviceKey = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices/$($device.id)/api-keys" -Method Post -Headers $adminHeaders
    $deviceKeysRaw = (Invoke-WebRequest -Uri "$ApiBaseUrl/v1/devices/$($device.id)/api-keys" -Headers $adminHeaders -UseBasicParsing).Content
    if ($deviceKeysRaw -match [regex]::Escape($deviceKey.key) -or $deviceKeysRaw -match 'keyHash') { throw "DEVICE-001 key list leaked a secret." }
    Invoke-WebRequest -Uri "$ApiBaseUrl/v1/devices/$($device.id)/api-keys/$($deviceKey.id)" -Method Delete -Headers $adminHeaders -UseBasicParsing | Out-Null
    try { Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/00000000-0000-0000-0000-000000000000" -Headers @{ Authorization = "Bearer $($secondDeviceKey.key)" } -UseBasicParsing | Out-Null }
    catch { if ($_.Exception.Response.StatusCode.value__ -eq 401) { throw "DEVICE-001 active key did not authenticate." } }
    Invoke-WebRequest -Uri "$ApiBaseUrl/v1/devices/$($device.id)/disable" -Method Post -Headers $adminHeaders -UseBasicParsing | Out-Null
    Invoke-WebRequest -Uri "$ApiBaseUrl/v1/devices/$($device.id)/disable" -Method Post -Headers $adminHeaders -UseBasicParsing | Out-Null
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/00000000-0000-0000-0000-000000000000" -Headers @{ Authorization = "Bearer $($secondDeviceKey.key)" } -UseBasicParsing | Out-Null
        throw "DEVICE-001 disabled device key still authenticates."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }
    $apiKeyBody = @{ producerName = "Integration Producer" } | ConvertTo-Json
    $issuedKey = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/api-keys" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $apiKeyBody
    if ($issuedKey.key -notmatch '^notify_[0-9a-f]{64}$') { throw "Issued API key has an invalid format." }
    if ($issuedKey.keyPrefix -ne $issuedKey.key.Substring(0, 19)) { throw "API key prefix does not match the raw key." }
    $keyListRaw = (Invoke-WebRequest -Uri "$ApiBaseUrl/v1/api-keys" -Headers $adminHeaders -UseBasicParsing).Content
    if ($keyListRaw -match [Regex]::Escape($issuedKey.key) -or $keyListRaw -match 'keyHash') { throw "API key list leaked secret material." }
    $secondKey = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/api-keys" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $apiKeyBody
    $callbackDeviceId = (docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT device_id FROM api_keys WHERE id = '$($secondKey.id)';").Trim()
    if (-not $callbackDeviceId) { throw "Legacy API key was not linked to a source device." }
    $callbackConfig = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices/$callbackDeviceId/callback" -Method Put -ContentType "application/json" -Headers $adminHeaders -Body (@{ url = "http://callback-receiver:8080/callback" } | ConvertTo-Json)
    if (-not $callbackConfig.secret -or $callbackConfig.url -ne "http://callback-receiver:8080/callback") { throw "Callback configuration contract is invalid." }
    Invoke-RestMethod -Uri "$CallbackReceiverUrl/configure" -Method Post -ContentType "application/json" -Body (@{ secret = $callbackConfig.secret } | ConvertTo-Json) | Out-Null
    $callbackDevice = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/devices/$callbackDeviceId" -Headers $adminHeaders
    if (-not $callbackDevice.callbackConfigured) { throw "Device did not report callbackConfigured." }
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

    $senderBody = @{ key = "integration-smtp"; host = "smtp.example.test"; port = 587; secure = $false; username = "mailer@example.test"; password = "smtp-secret-$PID"; fromEmail = "mailer@example.test"; fromName = "Integration Mailer" } | ConvertTo-Json
    $senderRaw = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $senderBody -UseBasicParsing
    if ($senderRaw.StatusCode -ne 201 -or $senderRaw.Content -match "smtp-secret-$PID|passwordEncrypted") { throw "Sender create failed or leaked its password." }
    $sender = $senderRaw.Content | ConvertFrom-Json
    $senderListRaw = (Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders" -Headers $adminHeaders -UseBasicParsing).Content
    if ($senderListRaw -match "smtp-secret-$PID|passwordEncrypted") { throw "Sender list leaked encrypted or raw password." }
    $senderPatch = @{ fromName = "Updated Mailer" } | ConvertTo-Json
    $updatedSender = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($sender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $senderPatch
    if ($updatedSender.fromName -ne "Updated Mailer") { throw "Sender patch did not update metadata." }
    $secondSenderBody = @{ key = "integration-smtp-2"; host = "smtp2.example.test"; port = 465; secure = $true; username = "mailer2@example.test"; password = "smtp-secret-2-$PID"; fromEmail = "mailer2@example.test"; fromName = "Second Mailer" } | ConvertTo-Json
    $secondSender = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $secondSenderBody
    $defaultBody = @{ isDefault = $true } | ConvertTo-Json
    $firstDefault = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($sender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $defaultBody
    if (-not $firstDefault.isDefault) { throw "Sender was not made default." }
    $secondDefault = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($secondSender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $defaultBody
    $senderList = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders" -Headers $adminHeaders
    $defaults = @($senderList.items | Where-Object { $_.isDefault })
    if ($defaults.Count -ne 1 -or $defaults[0].id -ne $secondSender.id) { throw "Replacing the default sender was not atomic." }
    Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($sender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $defaultBody | Out-Null
    Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($secondSender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $defaultBody | Out-Null
    $clearDefaultBody = @{ isDefault = $false } | ConvertTo-Json
    $cleared = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($secondSender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $clearDefaultBody
    $clearedAgain = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($secondSender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $clearDefaultBody
    if ($cleared.isDefault -or $clearedAgain.isDefault) { throw "Clearing the default sender was not idempotent." }
    Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($sender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $defaultBody | Out-Null
    $testSenderBody = @{ key = "greenmail-smtp"; host = "greenmail"; port = 3465; secure = $true; username = "mailer"; password = "secret"; fromEmail = "mailer@local.test"; fromName = "Integration SMTP" } | ConvertTo-Json
    $testSender = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $testSenderBody
    $testMailBody = @{ recipientEmail = "recipient@local.test" } | ConvertTo-Json
    $testMail = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($testSender.id)/test" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $testMailBody
    if (-not $testMail.sent -or -not $testMail.verifiedAt -or $testMail.recipientEmail -ne "recipient@local.test") { throw "SMTP test did not return verified success." }
    $firstVerifiedAt = $testMail.verifiedAt
    Start-Sleep -Milliseconds 20
    $testMailAgain = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($testSender.id)/test" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $testMailBody
    if ([DateTimeOffset]$testMailAgain.verifiedAt -le [DateTimeOffset]$firstVerifiedAt) { throw "Repeated SMTP test did not refresh verifiedAt." }
    $badAuthSenderBody = @{ key = "greenmail-bad-auth"; host = "greenmail"; port = 3465; secure = $true; username = "mailer"; password = "wrong"; fromEmail = "mailer@local.test"; fromName = "Bad SMTP" } | ConvertTo-Json
    $badAuthSender = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $badAuthSenderBody
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($badAuthSender.id)/test" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $testMailBody -UseBasicParsing | Out-Null
        throw "SMTP test accepted invalid credentials."
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 502) { throw }
        $smtpFailureJson = $_.ErrorDetails.Message
        if (-not $smtpFailureJson) { $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream()); $smtpFailureJson = $reader.ReadToEnd(); $reader.Dispose() }
        $smtpFailure = $smtpFailureJson | ConvertFrom-Json
        if ($smtpFailure.code -ne "SMTP_TEST_FAILED" -or $smtpFailure.reason -ne "SMTP_AUTHENTICATION") { throw "SMTP authentication failure mapping is invalid." }
    }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($testSender.id)/test" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer $($tenantLogin.accessToken)" } -Body $testMailBody -UseBasicParsing | Out-Null
        throw "A different tenant tested a sender."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw } }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($testSender.id)/test" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer $($issuedKey.key)" } -Body $testMailBody -UseBasicParsing | Out-Null
        throw "A machine API key tested a sender."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }
    Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($badAuthSender.id)" -Method Delete -Headers $adminHeaders -UseBasicParsing | Out-Null
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($badAuthSender.id)/test" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $testMailBody -UseBasicParsing | Out-Null
        throw "A disabled sender opened an SMTP test."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 409) { throw } }
    Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($testSender.id)/test" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $testMailBody | Out-Null
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($testSender.id)/test" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $testMailBody -UseBasicParsing | Out-Null
        throw "SMTP test rate limit did not reject the sixth request."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 429 -or -not $_.Exception.Response.Headers["Retry-After"]) { throw } }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($sender.id)" -Method Patch -ContentType "application/json" -Headers @{ Authorization = "Bearer $($tenantLogin.accessToken)" } -Body $senderPatch -UseBasicParsing | Out-Null
        throw "A different tenant updated a sender."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw } }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($sender.id)" -Method Patch -ContentType "application/json" -Headers @{ Authorization = "Bearer $($issuedKey.key)" } -Body $defaultBody -UseBasicParsing | Out-Null
        throw "A machine API key changed the default sender."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }
    $disabled = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($sender.id)" -Method Delete -Headers $adminHeaders -UseBasicParsing
    if ($disabled.StatusCode -ne 204) { throw "Sender disable returned $($disabled.StatusCode)." }
    $disabledAgain = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($sender.id)" -Method Delete -Headers $adminHeaders -UseBasicParsing
    if ($disabledAgain.StatusCode -ne 204) { throw "Idempotent sender disable returned $($disabledAgain.StatusCode)." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($sender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $senderPatch -UseBasicParsing | Out-Null
        throw "A disabled sender was updated."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 409) { throw } }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/senders/$($sender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $defaultBody -UseBasicParsing | Out-Null
        throw "A disabled sender was made default."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 409) { throw } }

    $templateBody = @{ key = "integration-template"; subject = "Hello {{name}}"; body = "Score: {{score}}"; variables = @("name", "score") } | ConvertTo-Json
    $template = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $templateBody
    if ($template.status -ne "draft" -or $template.variables.Count -ne 2) { throw "Template create contract is invalid." }
    $templateGet = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates/INTEGRATION-TEMPLATE" -Headers $adminHeaders
    if ($templateGet.id -ne $template.id) { throw "Template key normalization failed." }
    $templateList = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates?status=draft" -Headers $adminHeaders
    if (@($templateList.items | Where-Object { $_.id -eq $template.id }).Count -ne 1) { throw "Template list/filter failed." }
    $activeTemplate = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates/integration-template" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body (@{ status = "active" } | ConvertTo-Json)
    if ($activeTemplate.status -ne "active") { throw "Template activation failed." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/templates/integration-template" -Headers @{ Authorization = "Bearer $($tenantLogin.accessToken)" } -UseBasicParsing | Out-Null
        throw "A different tenant read a template."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw } }
    $retiredTemplate = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates/integration-template" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body (@{ status = "retired" } | ConvertTo-Json)
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/templates/integration-template" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body (@{ subject = "Changed" } | ConvertTo-Json) -UseBasicParsing | Out-Null
        throw "A retired template was changed."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 409) { throw } }

    $scopedTemplateBody = @{ templateCode = "score-result"; scope = "source"; sourceDeviceId = $callbackDeviceId; audience = "user"; subject = "Result for {{name}}"; textBody = "Score: {{score}}"; htmlBody = "<p>Score: <strong>{{score}}</strong></p>"; variables = @("name", "score") } | ConvertTo-Json
    $scopedTemplate = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $scopedTemplateBody
    if ($scopedTemplate.scope -ne "source" -or $scopedTemplate.sourceDeviceId -ne $callbackDeviceId -or $scopedTemplate.version -ne 1 -or $scopedTemplate.status -ne "draft" -or -not $scopedTemplate.htmlBody) { throw "TMPL-002 source template contract is invalid." }
    $publishedScoped = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates/$($scopedTemplate.id)/publish" -Method Post -Headers $adminHeaders
    if ($publishedScoped.status -ne "active" -or -not $publishedScoped.publishedAt) { throw "TMPL-002 publish failed." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/templates/$($scopedTemplate.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body (@{ subject = "Changed" } | ConvertTo-Json) -UseBasicParsing | Out-Null
        throw "TMPL-002 changed an immutable active version."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 409) { throw } }
    $versionTwo = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates/$($scopedTemplate.id)/versions" -Method Post -Headers $adminHeaders
    if ($versionTwo.version -ne 2 -or $versionTwo.status -ne "draft" -or $versionTwo.subject -ne $scopedTemplate.subject) { throw "TMPL-002 version clone failed." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/templates/$($scopedTemplate.id)/versions" -Method Post -Headers $adminHeaders -UseBasicParsing | Out-Null
        throw "TMPL-002 allowed two drafts in one family."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 409) { throw } }
    $publishedVersionTwo = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates/$($versionTwo.id)/publish" -Method Post -Headers $adminHeaders
    $previousVersion = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates/$($scopedTemplate.id)" -Headers $adminHeaders
    if ($publishedVersionTwo.status -ne "active" -or $previousVersion.status -ne "retired" -or $publishedVersionTwo.version -ne 2) { throw "TMPL-002 did not atomically replace the active version." }
    $tenantTemplateBody = @{ templateCode = "score-result"; scope = "tenant"; audience = "system"; subject = "System result"; textBody = "Ready"; variables = @() } | ConvertTo-Json
    $tenantScopedTemplate = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/templates" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $tenantTemplateBody
    if ($tenantScopedTemplate.scope -ne "tenant" -or $tenantScopedTemplate.sourceDeviceId -ne $null) { throw "TMPL-002 tenant fallback family failed." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/templates/$($scopedTemplate.id)" -Headers @{ Authorization = "Bearer $($tenantLogin.accessToken)" } -UseBasicParsing | Out-Null
        throw "A different tenant read a scoped template."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw } }

    $machineHeaders = @{ Authorization = "Bearer $($secondKey.key)" }
    $templateNotificationBody = @{ senderKey = "greenmail-smtp"; channels = @(@{ type = "email"; targets = @(@{ address = "template@example.test"; ref = "template-$PID" }) }); content = @{ mode = "template"; templateCode = "SCORE-RESULT"; data = @{ name = "An"; score = "<9>" } } } | ConvertTo-Json -Depth 8
    $templateAccepted = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $templateNotificationBody
    $templateDelivered = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $templateState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT n.status || '|' || d.status || '|' || (n.template_id = '$($publishedVersionTwo.id)') || '|' || (n.text_body_encrypted IS NOT NULL) || '|' || (n.html_body_encrypted IS NOT NULL) FROM notifications n JOIN deliveries d ON d.notification_id=n.id WHERE n.id='$($templateAccepted.id)';"
        if ($templateState.Trim() -eq "delivered|delivered|true|true|true") { $templateDelivered = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $templateDelivered) { throw "INTK-003 template snapshot delivery did not complete. Last state: $($templateState.Trim())" }
    $notificationCountBeforeInvalidTemplate = (docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT count(*) FROM notifications;").Trim()
    $invalidTemplateBody = @{ senderKey = "greenmail-smtp"; channels = @(@{ type = "email"; targets = @(@{ address = "template@example.test" }) }); content = @{ mode = "template"; templateCode = "score-result"; data = @{ name = "An"; score = "9"; extra = "secret" } } } | ConvertTo-Json -Depth 8
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $invalidTemplateBody -UseBasicParsing | Out-Null
        throw "INTK-003 accepted an unknown template variable."
    }
    catch {
        if ($_.Exception.Response.StatusCode.value__ -ne 400 -or $_.ErrorDetails.Message -match "secret") { throw }
    }
    $notificationCountAfterInvalidTemplate = (docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT count(*) FROM notifications;").Trim()
    if ($notificationCountAfterInvalidTemplate -ne $notificationCountBeforeInvalidTemplate) { throw "INTK-003 persisted a failed render." }
    $multiBody = @{ senderKey = "greenmail-smtp"; channels = @(@{ type = "email"; targets = @(@{ address = "MULTI@EXAMPLE.TEST"; ref = "multi-$PID" }) }); content = @{ mode = "plaintext"; subject = "Multi-channel contract"; body = "Delivery model $PID" } } | ConvertTo-Json -Depth 8
    $multiAccepted = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $multiBody
    if ($multiAccepted.status -ne "accepted" -or $multiAccepted.deliveries.Count -ne 1 -or $multiAccepted.deliveries[0].channel -ne "email" -or $multiAccepted.deliveries[0].status -ne "pending") { throw "CHAN-001 intake response is invalid." }
    $multiDelivered = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $multiState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT n.status || '|' || d.status FROM notifications n JOIN deliveries d ON d.notification_id=n.id WHERE n.id='$($multiAccepted.id)';"
        if ($multiState.Trim() -eq "delivered|delivered") { $multiDelivered = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $multiDelivered) { throw "CHAN-001 email delivery did not complete." }

    $notificationBody = @{ senderKey = "greenmail-smtp"; subject = "Integration notification"; body = "Notification body $PID"; recipients = @(@{ email = "STUDENT@EXAMPLE.TEST"; ref = "  student-$PID  " }) } | ConvertTo-Json -Depth 5
    $acceptedNotification = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $notificationBody
    if ($acceptedNotification.accepted -ne 1 -or $acceptedNotification.notifications.Count -ne 1 -or $acceptedNotification.notifications[0].email -ne "student@example.test" -or $acceptedNotification.notifications[0].ref -ne "student-$PID") { throw "Notification intake response is invalid." }
    $notificationId = $acceptedNotification.notifications[0].id
    $storedRecipient = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT target FROM deliveries WHERE notification_id = '$notificationId';"
    if ($storedRecipient.Trim() -ne "student@example.test") { throw "Notification was not persisted." }
    $batchTable = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT to_regclass('public.notification_batches') IS NULL;"
    if ($batchTable.Trim() -ne "t") { throw "INTK-001 unexpectedly created notification_batches." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $notificationBody -UseBasicParsing | Out-Null
        throw "JWT admin was accepted by machine intake."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }
    $invalidNotificationBody = @{ senderKey = "greenmail-smtp"; subject = "Invalid"; body = "Body"; recipients = @() } | ConvertTo-Json -Depth 5
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $invalidNotificationBody -UseBasicParsing | Out-Null
        throw "Empty recipients was accepted."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 400) { throw } }
    $delivered = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $deliveryState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count FROM deliveries WHERE notification_id = '$notificationId';"
        if ($deliveryState.Trim() -eq "delivered|1") { $delivered = $true; break }
        if ($deliveryState.Trim() -like "failed*") {
            $deliveryFailure = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT a.error_code || '|' || a.error_message FROM delivery_attempts a JOIN deliveries d ON d.id=a.delivery_id WHERE d.notification_id = '$notificationId';"
            throw "Worker marked integration notification failed: $deliveryState ($deliveryFailure)"
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $delivered) { throw "Worker did not deliver the accepted notification." }
    $deliveryAttempt = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT a.result || '|' || a.attempt_no FROM delivery_attempts a JOIN deliveries d ON d.id=a.delivery_id WHERE d.notification_id = '$notificationId';"
    if ($deliveryAttempt.Trim() -ne "success|1") { throw "Delivery attempt was not recorded as success." }
    $callbackDelivered = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        try {
            $callbackEvent = Invoke-RestMethod -Uri "$CallbackReceiverUrl/events?notificationId=$notificationId"
            if ($callbackEvent) { $callbackDelivered = $true; break }
        }
        catch { if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw } }
        Start-Sleep -Milliseconds 500
    }
    if (-not $callbackDelivered) { throw "notification.completed callback was not delivered." }
    if (-not $callbackEvent.signatureValid -or $callbackEvent.headerEventId -ne $callbackEvent.payload.eventId -or $callbackEvent.payload.schemaVersion -ne 1 -or $callbackEvent.payload.type -ne "notification.completed" -or $callbackEvent.payload.status -ne "delivered") { throw "Callback signature or payload contract is invalid: $($callbackEvent | ConvertTo-Json -Depth 8 -Compress)" }
    $callbackState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count FROM status_events WHERE notification_id = '$notificationId';"
    if ($callbackState.Trim() -ne "delivered|1") { throw "Callback event state is invalid: $callbackState" }

    docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -c "UPDATE senders SET host = 'missing-smtp' WHERE id = '$($testSender.id)';" | Out-Null
    $retryAccepted = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $notificationBody
    $retryNotificationId = $retryAccepted.notifications[0].id
    $transientRecorded = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $retryState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count FROM deliveries WHERE notification_id = '$retryNotificationId';"
        if ($retryState.Trim() -eq "pending|1") { $transientRecorded = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $transientRecorded) { throw "Transient SMTP failure was not scheduled for retry." }
    $firstRetryAttempt = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT a.result || '|' || a.error_code FROM delivery_attempts a JOIN deliveries d ON d.id=a.delivery_id WHERE d.notification_id = '$retryNotificationId' AND a.attempt_no = 1;"
    if ($firstRetryAttempt.Trim() -notin @("transient_failure|SMTP_CONNECTION", "transient_failure|SMTP_DNS")) { throw "Transient SMTP failure classification is invalid: $firstRetryAttempt" }
    docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -c "UPDATE senders SET host = 'greenmail' WHERE id = '$($testSender.id)';" | Out-Null
    docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -c "UPDATE deliveries SET next_attempt_at = now() WHERE notification_id = '$retryNotificationId';" | Out-Null
    $retryDelivered = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $retryState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count FROM deliveries WHERE notification_id = '$retryNotificationId';"
        if ($retryState.Trim() -eq "delivered|2") { $retryDelivered = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $retryDelivered) { throw "Transient notification did not succeed on retry." }

    $permanentSenderBody = @{ key = "greenmail-permanent"; host = "greenmail"; port = 3465; secure = $true; username = "mailer"; password = "wrong"; fromEmail = "mailer@local.test"; fromName = "Permanent Failure SMTP" } | ConvertTo-Json
    Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body $permanentSenderBody | Out-Null
    $permanentBody = @{ senderKey = "greenmail-permanent"; subject = "Permanent failure"; body = "Safe body"; recipients = @(@{ email = "student@example.test" }) } | ConvertTo-Json -Depth 5
    $permanentAccepted = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $permanentBody
    $permanentNotificationId = $permanentAccepted.notifications[0].id
    $permanentFailed = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $permanentState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count FROM deliveries WHERE notification_id = '$permanentNotificationId';"
        if ($permanentState.Trim() -eq "failed|1") { $permanentFailed = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $permanentFailed) { throw "Permanent SMTP failure was not terminal on first attempt." }
    $permanentAttempt = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT a.result || '|' || a.error_code FROM delivery_attempts a JOIN deliveries d ON d.id=a.delivery_id WHERE d.notification_id = '$permanentNotificationId';"
    if ($permanentAttempt.Trim() -ne "permanent_failure|SMTP_AUTHENTICATION") { throw "Permanent SMTP failure classification is invalid: $permanentAttempt" }

    docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -c "UPDATE senders SET host = 'missing-smtp' WHERE id = '$($testSender.id)';" | Out-Null
    $exhaustedAccepted = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $notificationBody
    $exhaustedNotificationId = $exhaustedAccepted.notifications[0].id
    for ($expectedAttempt = 1; $expectedAttempt -le 3; $expectedAttempt++) {
        $scheduled = $false
        for ($poll = 0; $poll -lt 30; $poll++) {
            $exhaustedState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count FROM deliveries WHERE notification_id = '$exhaustedNotificationId';"
            if ($exhaustedState.Trim() -eq "pending|$expectedAttempt") { $scheduled = $true; break }
            Start-Sleep -Milliseconds 500
        }
        if (-not $scheduled) { throw "Retry attempt $expectedAttempt was not scheduled." }
        docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -c "UPDATE deliveries SET next_attempt_at = now() WHERE notification_id = '$exhaustedNotificationId';" | Out-Null
    }
    $exhaustedFailed = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $exhaustedState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count FROM deliveries WHERE notification_id = '$exhaustedNotificationId';"
        if ($exhaustedState.Trim() -eq "failed|4") { $exhaustedFailed = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $exhaustedFailed) { throw "Transient delivery did not fail after four attempts." }
    $exhaustedAttempts = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT count(*) || '|' || min(a.result) || '|' || max(a.result) FROM delivery_attempts a JOIN deliveries d ON d.id=a.delivery_id WHERE d.notification_id = '$exhaustedNotificationId';"
    if ($exhaustedAttempts.Trim() -ne "4|transient_failure|transient_failure") { throw "Exhausted retry attempts are invalid: $exhaustedAttempts" }
    docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -c "UPDATE senders SET host = 'greenmail' WHERE id = '$($testSender.id)';" | Out-Null

    docker compose -p $composeProject -f $ComposeFile stop worker
    if ($LASTEXITCODE -ne 0) { throw "Failed to stop Worker for stuck delivery recovery test." }
    $recoverableAccepted = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $notificationBody
    $recoverableId = $recoverableAccepted.notifications[0].id
    docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -c "UPDATE deliveries SET status = 'sending', attempt_count = 1, updated_at = now() - interval '181 seconds' WHERE notification_id = '$recoverableId'; UPDATE notifications SET status = 'processing' WHERE id = '$recoverableId';" | Out-Null

    $terminalAccepted = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $notificationBody
    $terminalRecoveryId = $terminalAccepted.notifications[0].id
    docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -c "UPDATE deliveries SET status = 'sending', attempt_count = 4, updated_at = now() - interval '181 seconds' WHERE notification_id = '$terminalRecoveryId'; UPDATE notifications SET status = 'processing' WHERE id = '$terminalRecoveryId'; INSERT INTO delivery_attempts (id, tenant_id, delivery_id, sender_id, attempt_no, result, error_code, error_message, started_at, finished_at, created_at) SELECT gen_random_uuid(), tenant_id, id, sender_id, n, 'transient_failure', 'SMTP_CONNECTION', 'Email delivery failed temporarily.', now() - interval '190 seconds', now() - interval '189 seconds', now() - interval '189 seconds' FROM deliveries CROSS JOIN generate_series(1, 3) AS n WHERE notification_id = '$terminalRecoveryId';" | Out-Null
    $cancelAccepted = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $notificationBody
    $cancelId = $cancelAccepted.notifications[0].id
    $cancelResponse = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/$cancelId/cancel" -Method Post -Headers $adminHeaders -UseBasicParsing
    if ($cancelResponse.StatusCode -ne 204) { throw "HIST-003 cancel did not return 204." }
    $cancelAgain = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/$cancelId/cancel" -Method Post -Headers $adminHeaders -UseBasicParsing
    if ($cancelAgain.StatusCode -ne 204) { throw "HIST-003 repeated cancel was not idempotent." }
    $cancelState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT n.status || '|' || d.status || '|' || d.attempt_count || '|' || count(a.id) FROM notifications n JOIN deliveries d ON d.notification_id=n.id LEFT JOIN delivery_attempts a ON a.delivery_id=d.id WHERE n.id='$cancelId' GROUP BY n.status,d.status,d.attempt_count;"
    if ($cancelState.Trim() -ne "cancelled|cancelled|0|0") { throw "HIST-003 cancel state is invalid: $cancelState" }
    docker compose -p $composeProject -f $ComposeFile start --wait worker
    if ($LASTEXITCODE -ne 0) { throw "Failed to restart Worker for stuck delivery recovery test." }

    $recoverableSent = $false
    $terminalRecovered = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        $recoverableState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count FROM deliveries WHERE notification_id = '$recoverableId';"
        $terminalRecoveryState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count FROM deliveries WHERE notification_id = '$terminalRecoveryId';"
        if ($recoverableState.Trim() -eq "delivered|2") { $recoverableSent = $true }
        if ($terminalRecoveryState.Trim() -eq "failed|4") { $terminalRecovered = $true }
        if ($recoverableSent -and $terminalRecovered) { break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $recoverableSent) { throw "Stuck attempt 1 was not recovered and delivered as attempt 2." }
    if (-not $terminalRecovered) { throw "Stuck attempt 4 was not recovered as terminal failure." }
    $recoveryAttempts = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT string_agg(a.attempt_no || ':' || a.result || ':' || coalesce(a.error_code, ''), ',' ORDER BY a.attempt_no) FROM delivery_attempts a JOIN deliveries d ON d.id=a.delivery_id WHERE d.notification_id = '$recoverableId';"
    if ($recoveryAttempts.Trim() -ne "1:transient_failure:WORKER_INTERRUPTED,2:success:") { throw "Recoverable attempt history is invalid: $recoveryAttempts" }
    $terminalRecoveryAttempts = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT count(*) || '|' || max(a.attempt_no) || '|' || max(a.error_code) FILTER (WHERE a.attempt_no = 4) FROM delivery_attempts a JOIN deliveries d ON d.id=a.delivery_id WHERE d.notification_id = '$terminalRecoveryId';"
    if ($terminalRecoveryAttempts.Trim() -ne "4|4|WORKER_INTERRUPTED") { throw "Terminal recovery history is invalid: $terminalRecoveryAttempts" }
    $incidentState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT count(*) || '|' || sum(occurrence_count) || '|' || bool_and(length(sample_message) <= 300) FROM failure_incidents WHERE component='delivery';"
    if ($incidentState.Trim() -notmatch '^\d+\|[1-9]\d*\|(t|true)$') { throw "DLVR-004 incident aggregation is invalid: $incidentState" }
    Invoke-RestMethod -Uri "$ApiBaseUrl/v1/senders/$($testSender.id)" -Method Patch -ContentType "application/json" -Headers $adminHeaders -Body $defaultBody | Out-Null
    docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -c "UPDATE failure_alerts SET window_end=now()-interval '1 second' WHERE status='pending';" | Out-Null
    $alertDelivered = $false
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        $alertState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count || '|' || recipient_count || '|' || success_count FROM failure_alerts ORDER BY created_at DESC LIMIT 1;"
        if ($alertState.Trim() -match '^delivered\|1\|[1-9]\d*\|[1-9]\d*$') { $alertDelivered = $true; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $alertDelivered) { throw "DLVR-004 alert was not delivered once: $alertState" }
    $retryResponse = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/$terminalRecoveryId/retry" -Method Post -Headers $adminHeaders -UseBasicParsing
    if ($retryResponse.StatusCode -ne 201 -or -not $retryResponse.Headers.Location) { throw "HIST-003 retry did not create a notification." }
    $retry = $retryResponse.Content | ConvertFrom-Json
    $retryAgain = Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/$terminalRecoveryId/retry" -Method Post -Headers $adminHeaders -UseBasicParsing
    $retryAgainBody = $retryAgain.Content | ConvertFrom-Json
    if ($retryAgain.StatusCode -ne 200 -or $retryAgainBody.id -ne $retry.id) { throw "HIST-003 retry was not idempotent." }
    $manualActions = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT count(*) FROM notification_manual_actions WHERE source_notification_id IN ('$terminalRecoveryId','$cancelId');"
    if ($manualActions.Trim() -ne "2") { throw "HIST-003 audit rows are invalid: $manualActions" }
    try { Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/$terminalRecoveryId/retry" -Method Post -Headers $machineHeaders -UseBasicParsing | Out-Null; throw "HIST-003 allowed API-key retry." }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 401) { throw } }

    $adminDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications/$notificationId" -Headers $adminHeaders
    if ($adminDetail.status -ne "delivered" -or $adminDetail.subject -ne "Integration notification" -or $adminDetail.body -ne "Notification body $PID" -or $adminDetail.recipientRef -ne "student-$PID" -or $adminDetail.producerName -ne "Integration Producer" -or $adminDetail.deliveryAttempts[0].result -ne "success") { throw "Admin notification detail contract is invalid." }
    $machineDetailRaw = (Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/$notificationId" -Headers $machineHeaders -UseBasicParsing).Content
    $machineDetail = $machineDetailRaw | ConvertFrom-Json
    if ($machineDetail.status -ne "delivered" -or $machineDetail.deliveryAttempts[0].result -ne "success") { throw "API-key notification detail metadata is invalid." }
    if ($machineDetailRaw -match 'subject|body|recipientRef|senderKey|providerMessageId|subjectEncrypted|bodyEncrypted') { throw "API-key notification detail leaked private fields." }
    $adminListRaw = (Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications?channel=email&sourceDeviceId=$callbackDeviceId&apiKeyId=$($secondKey.id)&limit=1" -Headers $adminHeaders -UseBasicParsing).Content
    $adminList = $adminListRaw | ConvertFrom-Json
    if ($adminList.items.Count -ne 1 -or -not $adminList.nextCursor -or $adminList.items[0].sourceDeviceId -ne $callbackDeviceId -or $adminList.items[0].apiKeyId -ne $secondKey.id -or $adminList.items[0].deliveries.Count -lt 1) { throw "HIST-002 admin list/filter contract is invalid." }
    if ($adminListRaw -match 'subject|textBody|htmlBody|Encrypted|deliveryAttempts|providerMessageId') { throw "HIST-002 admin list loaded private content or attempts." }
    $listCursor = [Uri]::EscapeDataString($adminList.nextCursor)
    $adminSecondPage = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications?limit=1&cursor=$listCursor" -Headers $adminHeaders
    if ($adminSecondPage.items.Count -ne 1 -or $adminSecondPage.items[0].id -eq $adminList.items[0].id) { throw "HIST-002 cursor repeated or skipped the next item." }
    $machineListRaw = (Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications?status=delivered&channel=email&limit=100" -Headers $machineHeaders -UseBasicParsing).Content
    $machineList = $machineListRaw | ConvertFrom-Json
    if ($machineList.items.Count -lt 1 -or $machineListRaw -match 'apiKeyId|sourceDeviceId|targetRef|subject|textBody|htmlBody|Encrypted|deliveryAttempts|providerMessageId') { throw "HIST-002 API-key list scope/privacy contract is invalid." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications?apiKeyId=$($secondKey.id)" -Headers $machineHeaders -UseBasicParsing | Out-Null
        throw "HIST-002 allowed an API key to use an admin filter."
    }
    catch {
        $filterErrorJson = $_.ErrorDetails.Message
        if (-not $filterErrorJson) { $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream()); $filterErrorJson = $reader.ReadToEnd(); $reader.Dispose() }
        if ($_.Exception.Response.StatusCode.value__ -ne 400 -or $filterErrorJson -notmatch 'FILTER_NOT_ALLOWED') { throw }
    }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications?cursor=broken" -Headers $adminHeaders -UseBasicParsing | Out-Null
        throw "HIST-002 accepted an invalid cursor."
    }
    catch {
        $cursorErrorJson = $_.ErrorDetails.Message
        if (-not $cursorErrorJson) { $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream()); $cursorErrorJson = $reader.ReadToEnd(); $reader.Dispose() }
        if ($_.Exception.Response.StatusCode.value__ -ne 400 -or $cursorErrorJson -notmatch 'INVALID_CURSOR') { throw }
    }
    $otherKey = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/api-keys" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body (@{ producerName = "Other Producer" } | ConvertTo-Json)
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/$notificationId" -Headers @{ Authorization = "Bearer $($otherKey.key)" } -UseBasicParsing | Out-Null
        throw "A different API key read the notification."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw } }
    $crossTenantList = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications?limit=100" -Headers @{ Authorization = "Bearer $($tenantLogin.accessToken)" }
    if ($crossTenantList.items.Count -ne 0 -or $crossTenantList.nextCursor -ne $null) { throw "HIST-002 cross-tenant list exposed notifications." }
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/$notificationId" -Headers @{ Authorization = "Bearer $($tenantLogin.accessToken)" } -UseBasicParsing | Out-Null
        throw "A cross-tenant admin read the notification."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw } }

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
    docker compose -p $composeProject -f $ComposeFile run --rm migrate migrate 20260822085542_AddFailureAlerts
    if ($LASTEXITCODE -eq 0) { throw "AUTH-004 rollback unexpectedly accepted existing member data." }
    $truncateSql = "DO `$`$ DECLARE r record; BEGIN FOR r IN SELECT tablename FROM pg_tables WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory' LOOP EXECUTE format('TRUNCATE TABLE %I CASCADE', r.tablename); END LOOP; END `$`$;"
    docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -v ON_ERROR_STOP=1 -c $truncateSql
    if ($LASTEXITCODE -ne 0) { throw "Failed to clear integration fixtures before schema rollback." }
    docker compose -p $composeProject -f $ComposeFile run --rm migrate migrate 0
    if ($LASTEXITCODE -ne 0) { throw "InitialIdentity rollback failed." }
    docker compose -p $composeProject -f $ComposeFile run --rm migrate migrate latest
    if ($LASTEXITCODE -ne 0) { throw "InitialIdentity re-apply failed." }
}
finally {
    docker compose -p $composeProject -f $ComposeFile down --volumes --remove-orphans
}
