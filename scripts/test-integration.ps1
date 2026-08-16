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
        if ($smtpFailure.code -ne "SMTP_TEST_FAILED" -or $smtpFailure.reason -ne "authentication") { throw "SMTP authentication failure mapping is invalid." }
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

    $notificationBody = @{ senderKey = "greenmail-smtp"; subject = "Integration notification"; body = "Notification body $PID"; recipients = @(@{ email = "STUDENT@EXAMPLE.TEST"; ref = "  student-$PID  " }) } | ConvertTo-Json -Depth 5
    $machineHeaders = @{ Authorization = "Bearer $($secondKey.key)" }
    $acceptedNotification = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications" -Method Post -ContentType "application/json" -Headers $machineHeaders -Body $notificationBody
    if ($acceptedNotification.accepted -ne 1 -or $acceptedNotification.notifications.Count -ne 1 -or $acceptedNotification.notifications[0].email -ne "student@example.test" -or $acceptedNotification.notifications[0].ref -ne "student-$PID") { throw "Notification intake response is invalid." }
    $notificationId = $acceptedNotification.notifications[0].id
    $storedRecipient = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT recipient_email FROM notifications WHERE id = '$notificationId';"
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
        $deliveryState = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT status || '|' || attempt_count FROM notifications WHERE id = '$notificationId';"
        if ($deliveryState.Trim() -eq "sent|1") { $delivered = $true; break }
        if ($deliveryState.Trim() -like "failed*") {
            $deliveryFailure = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT error_code || '|' || error_message FROM delivery_attempts WHERE notification_id = '$notificationId';"
            throw "Worker marked integration notification failed: $deliveryState ($deliveryFailure)"
        }
        Start-Sleep -Milliseconds 500
    }
    if (-not $delivered) { throw "Worker did not deliver the accepted notification." }
    $deliveryAttempt = docker compose -p $composeProject -f $ComposeFile exec -T postgres psql -U notify -d notification -tAc "SELECT result || '|' || attempt_no FROM delivery_attempts WHERE notification_id = '$notificationId';"
    if ($deliveryAttempt.Trim() -ne "success|1") { throw "Delivery attempt was not recorded as success." }

    $adminDetail = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/notifications/$notificationId" -Headers $adminHeaders
    if ($adminDetail.status -ne "sent" -or $adminDetail.subject -ne "Integration notification" -or $adminDetail.body -ne "Notification body $PID" -or $adminDetail.recipientRef -ne "student-$PID" -or $adminDetail.producerName -ne "Integration Producer" -or $adminDetail.deliveryAttempts[0].result -ne "success") { throw "Admin notification detail contract is invalid." }
    $machineDetailRaw = (Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/$notificationId" -Headers $machineHeaders -UseBasicParsing).Content
    $machineDetail = $machineDetailRaw | ConvertFrom-Json
    if ($machineDetail.status -ne "sent" -or $machineDetail.deliveryAttempts[0].result -ne "success") { throw "API-key notification detail metadata is invalid." }
    if ($machineDetailRaw -match 'subject|body|recipientRef|senderKey|providerMessageId|subjectEncrypted|bodyEncrypted') { throw "API-key notification detail leaked private fields." }
    $otherKey = Invoke-RestMethod -Uri "$ApiBaseUrl/v1/api-keys" -Method Post -ContentType "application/json" -Headers $adminHeaders -Body (@{ producerName = "Other Producer" } | ConvertTo-Json)
    try {
        Invoke-WebRequest -Uri "$ApiBaseUrl/v1/notifications/$notificationId" -Headers @{ Authorization = "Bearer $($otherKey.key)" } -UseBasicParsing | Out-Null
        throw "A different API key read the notification."
    }
    catch { if ($_.Exception.Response.StatusCode.value__ -ne 404) { throw } }
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
    docker compose -p $composeProject -f $ComposeFile run --rm migrate migrate 0
    if ($LASTEXITCODE -ne 0) { throw "InitialIdentity rollback failed." }
    docker compose -p $composeProject -f $ComposeFile run --rm migrate migrate latest
    if ($LASTEXITCODE -ne 0) { throw "InitialIdentity re-apply failed." }
}
finally {
    docker compose -p $composeProject -f $ComposeFile down --volumes --remove-orphans
}
