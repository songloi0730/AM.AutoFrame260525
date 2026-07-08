# -------------------------------------------------------
# File:    am-daycode.ps1
# Purpose: Sinh mã dịch vụ theo ngày cho đăng nhập break-glass user 'service'
#          (design-notes/0012). Thuật toán PHẢI khớp UserService.ComputeDayCode:
#          HMAC-SHA256(secret, machineId + yyyyMMdd) -> 4 byte đầu (big-endian) mod 10^8.
# Usage:   .\am-daycode.ps1 -Secret "<secret-hãng>" -MachineId "AM-DEMO-01"
#          .\am-daycode.ps1 -Secret "..." -MachineId "..." -Date 20260710
# Note:    Secret KHÔNG commit vào repo — giữ trong két/password manager của hãng.
#          Máy chấp nhận mã của hôm nay ±1 ngày (lệch đồng hồ/ca đêm).
# -------------------------------------------------------
param(
    [Parameter(Mandatory = $true)][string]$Secret,
    [Parameter(Mandatory = $true)][string]$MachineId,
    [string]$Date = (Get-Date -Format 'yyyyMMdd')
)

$hmac = New-Object System.Security.Cryptography.HMACSHA256(, [System.Text.Encoding]::UTF8.GetBytes($Secret))
try {
    $mac = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($MachineId + $Date))
}
finally {
    $hmac.Dispose()
}

# 4 byte đầu big-endian -> uint32 -> 8 chữ số (khớp BinaryPrimitives.ReadUInt32BigEndian)
$value = ([uint64]$mac[0] * 16777216) + ([uint64]$mac[1] * 65536) + ([uint64]$mac[2] * 256) + [uint64]$mac[3]
'{0:D8}' -f ($value % 100000000)
