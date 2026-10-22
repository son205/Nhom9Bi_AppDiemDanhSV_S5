$ErrorActionPreference = "Stop"

try {
    # 1. Login Admin
    $loginBody = @{
        userNameOrEmail = "admin"
        password = "Admin@123456"
        otpMethod = "Email"
    } | ConvertTo-Json
    $res1 = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/login" -Method Post -Body $loginBody -ContentType "application/json"
    $res2 = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/verify-otp" -Method Post -Body (@{ tempToken = $res1.tempToken; otp = $res1.debugOtp } | ConvertTo-Json) -ContentType "application/json"
    $adminHeaders = @{ Authorization = "Bearer $($res2.token)" }

    # 2. Login Lecturer
    $lecLogin = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/login" -Method Post -Body (@{ userNameOrEmail = "lecturer"; password = "Lecturer@123456" } | ConvertTo-Json) -ContentType "application/json"
    $lecVerify = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/verify-otp" -Method Post -Body (@{ tempToken = $lecLogin.tempToken; otp = $lecLogin.debugOtp } | ConvertTo-Json) -ContentType "application/json"
    $lecHeaders = @{ Authorization = "Bearer $($lecVerify.token)" }
    Write-Host "Lecturer Login Success! Dashboard=$($lecVerify.targetDashboard), Role=$($lecVerify.user.roles[0])"

    # 3. Test Lecturer Sharing/Updating Session GPS Location
    $shareLocBody = @{
        latitude = 10.7780
        longitude = 106.7020
        allowedRadiusMeters = 200
    } | ConvertTo-Json
    $shareLocRes = Invoke-RestMethod -Uri "http://localhost:5003/api/ClassSessions/1/location" -Method Put -Body $shareLocBody -Headers $lecHeaders -ContentType "application/json"
    Write-Host "Lecturer Share Location Success! New Lat=$($shareLocRes.sessionLatitude), Long=$($shareLocRes.sessionLongitude), Radius=$($shareLocRes.allowedRadiusMeters)m"

    # 4. Test Lecturer Manual Attendance for another student
    # Create another student
    $newStdUser = Invoke-RestMethod -Uri "http://localhost:5003/api/Auth/register" -Method Post -Body (@{ userName = "student02"; email = "std02@school.edu.vn"; fullName = "Le Thi B"; password = "Student@123456" } | ConvertTo-Json) -ContentType "application/json"
    $std2List = Invoke-RestMethod -Uri "http://localhost:5003/api/Students" -Method Get -Headers $adminHeaders
    $std2 = $std2List | Where-Object { $_.userId -eq $newStdUser.userId }
    Write-Host "Created Student 2: Id=$($std2.id), Code=$($std2.studentCode)"

    # Lecturer marks manual attendance
    $manualBody = @{
        studentId = $std2.id
        classSessionId = 1
        status = "Present"
    } | ConvertTo-Json
    $manualRes = Invoke-RestMethod -Uri "http://localhost:5003/api/AttendanceRecords/manual" -Method Post -Body $manualBody -Headers $lecHeaders -ContentType "application/json"
    Write-Host "Manual Attendance by Lecturer: Status=$($manualRes.status), Method=$($manualRes.method)"

    Write-Host "`n>>> ALL ADVANCED WORKFLOWS PASSED! <<<"
} catch {
    Write-Host "ERROR:" $_.Exception.Message
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        Write-Host "Response Body:" ($reader.ReadToEnd())
    }
}
