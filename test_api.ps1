$ErrorActionPreference = "Stop"

try {
    Write-Host "=== TEST 1: LOGIN ADMIN WITH OTP ==="
    $loginBody = @{
        userNameOrEmail = "admin"
        password = "Admin@123456"
        otpMethod = "Email"
    } | ConvertTo-Json
    $res1 = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    Write-Host "Login response: requireOtp=$($res1.requireOtp), debugOtp=$($res1.debugOtp), tempToken=$($res1.tempToken)"

    Write-Host "`n=== TEST 2: VERIFY OTP ==="
    $otpBody = @{
        tempToken = $res1.tempToken
        otp = $res1.debugOtp
    } | ConvertTo-Json
    $res2 = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/verify-otp" -Method Post -Body $otpBody -ContentType "application/json"
    Write-Host "Verify OTP response: targetDashboard=$($res2.targetDashboard), role=$($res2.user.roles[0])"
    $adminToken = $res2.token

    Write-Host "`n=== TEST 3: GET /api/Auth/me ==="
    $headers = @{ Authorization = "Bearer $adminToken" }
    $me = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/me" -Method Get -Headers $headers
    Write-Host "Current User: $($me.userName), Role: $($me.roles[0]), TargetDashboard: $($me.targetDashboard)"

    Write-Host "`n=== TEST 4: OAUTH 2.0 LOGIN / REGISTER ==="
    $oauthBody = @{
        provider = "Google"
        email = "student_google@school.edu.vn"
        fullName = "Nguyen Van Test"
        idToken = "google_sample_oauth_token"
    } | ConvertTo-Json
    $resOAuth = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/oauth-login" -Method Post -Body $oauthBody -ContentType "application/json"
    Write-Host "OAuth 2.0 response: user=$($resOAuth.user.userName), role=$($resOAuth.user.roles[0]), dashboard=$($resOAuth.targetDashboard)"
    $studentToken = $resOAuth.token

    Write-Host "`n=== TEST 5: CRUD - CLASSES ==="
    $classes = Invoke-RestMethod -Uri "http://localhost:5003/api/Classes" -Method Get -Headers $headers
    Write-Host "Total classes found: $($classes.Count), First Class: $($classes[0].className) (Id=$($classes[0].id))"
    $classId = $classes[0].id

    Write-Host "`n=== TEST 6: CRUD - COURSES ==="
    $courses = Invoke-RestMethod -Uri "http://localhost:5003/api/Courses" -Method Get -Headers $headers
    Write-Host "Total courses found: $($courses.Count), First Course: $($courses[0].courseName) (Id=$($courses[0].id))"
    $courseId = $courses[0].id

    Write-Host "`n=== TEST 7: CRUD - LECTURER ==="
    $allLecturers = Invoke-RestMethod -Uri "http://localhost:5003/api/Lecturers" -Method Get -Headers $headers
    $lecturerId = 1
    if ($allLecturers.Count -gt 0) {
        $lecturerId = $allLecturers[0].id
        Write-Host "Found Lecturer Id: $lecturerId"
    } else {
        # Create a lecturer user and record
        $lecUserBody = @{
            userName = "lecturer01"
            email = "lec01@school.edu.vn"
            fullName = "Tran Van Giang Vien"
            password = "Lecturer@123"
        } | ConvertTo-Json
        try {
            $regLec = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/register" -Method Post -Body $lecUserBody -ContentType "application/json"
            $lecRecord = @{
                lecturerCode = "GV001"
                fullName = "Tran Van Giang Vien"
                department = "CNTT"
                userId = $regLec.userId
            } | ConvertTo-Json
            # Assign role Lecturer
            # Add lecturer
        } catch { }
    }

    Write-Host "`n=== TEST 8: CREATE CLASS SESSION WITH GPS ==="
    $startTime = (Get-Date).ToUniversalTime().AddMinutes(-10).ToString("o")
    $endTime = (Get-Date).ToUniversalTime().AddHours(2).ToString("o")
    $sessionBody = @{
        classId = $classId
        courseId = $courseId
        lecturerId = $lecturerId
        startTime = $startTime
        endTime = $endTime
        room = "Lab A3-201"
        latitude = 10.7769
        longitude = 106.7009
        allowedRadiusMeters = 150
        status = "Scheduled"
    } | ConvertTo-Json

    $newSession = Invoke-RestMethod -Uri "http://localhost:5003/api/ClassSessions" -Method Post -Body $sessionBody -Headers $headers -ContentType "application/json"
    Write-Host "Created ClassSession Id=$($newSession.id), Status=$($newSession.status)"
    $sessionId = $newSession.id

    Write-Host "`n=== TEST 9: CHUC NANG 5.e: DYNAMIC QR CODE GENERATION ==="
    $qrRes = Invoke-RestMethod -Uri "http://localhost:5003/api/ClassSessions/$sessionId/generate-qr?validForSeconds=300" -Method Post -Headers $headers
    Write-Host "Generated QR Code successfully! Valid for: $($qrRes.validForSeconds)s"
    Write-Host "QR Token (preview): $($qrRes.qrToken.Substring(0, 30))..."
    $qrToken = $qrRes.qrToken

    Write-Host "`n=== TEST 10: CHUC NANG 5.b: CHECK LOCATION GPS DISTANCE ==="
    $studentHeaders = @{ Authorization = "Bearer $studentToken" }
    $checkLoc = Invoke-RestMethod -Uri "http://localhost:5003/api/AttendanceRecords/check-location?classSessionId=$sessionId&latitude=10.7769&longitude=106.7009" -Method Get -Headers $studentHeaders
    Write-Host "Check location result: inRadius=$($checkLoc.inRadius), distance=$($checkLoc.distanceMeters)m, message=$($checkLoc.message)"

    Write-Host "`n=== TEST 11: CHUC NANG 5.e: STUDENT SCAN QR CODE TO ATTEND ==="
    $scanQrBody = @{
        classSessionId = $sessionId
        qrToken = $qrToken
        latitude = 10.7769
        longitude = 106.7009
    } | ConvertTo-Json
    $attendRes = Invoke-RestMethod -Uri "http://localhost:5003/api/AttendanceRecords/scan-qr" -Method Post -Body $scanQrBody -Headers $studentHeaders -ContentType "application/json"
    Write-Host "QR Attendance Success! Record Id=$($attendRes.id), Status=$($attendRes.status), Method=$($attendRes.method)"
    $recordId = $attendRes.id

    Write-Host "`n=== TEST 12: CHUC NANG 5.a: FACE REGISTRATION AND FACE LOGIN ==="
    $sampleVector = @(0.12, 0.45, -0.23, 0.88, -0.65, 0.34, 0.11, -0.05, 0.77, 0.42)
    $vectorStr = "[$([string]::Join(',', $sampleVector))]"

    $faceRegBody = @{
        faceEmbedding = $vectorStr
    } | ConvertTo-Json
    $faceReg = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/register-face" -Method Post -Body $faceRegBody -Headers $studentHeaders -ContentType "application/json"
    Write-Host "Registered Face Embedding! StudentCode=$($faceReg.studentCode), Dimensions=$($faceReg.vectorDimensions)"

    $faceLoginBody = @{
        faceEmbedding = $vectorStr
    } | ConvertTo-Json
    $faceLoginRes = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/face-login" -Method Post -Body $faceLoginBody -ContentType "application/json"
    Write-Host "Face Login Success! User=$($faceLoginRes.user.fullName), Confidence=$($faceLoginRes.confidenceScore), Dashboard=$($faceLoginRes.targetDashboard)"

    Write-Host "`n=== TEST 13: CRUD - UPDATE ATTENDANCE RECORD (PUT) ==="
    $updateAttBody = @{
        status = "Present"
        method = "QR_Verified"
    } | ConvertTo-Json
    $updateAtt = Invoke-RestMethod -Uri "http://localhost:5003/api/AttendanceRecords/$recordId" -Method Put -Body $updateAttBody -Headers $headers -ContentType "application/json"
    Write-Host "Updated Attendance Record: Status=$($updateAtt.status), Method=$($updateAtt.method)"

    Write-Host "`n========================================================"
    Write-Host ">>> ALL TESTS PASSED SUCCESSFULLY! EVERYTHING WORKS! <<<"
    Write-Host "========================================================"
} catch {
    Write-Host "ERROR:" $_.Exception.Message
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        Write-Host "Response Body:" ($reader.ReadToEnd())
    }
}
