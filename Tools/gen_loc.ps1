# Builds Assets/Resources/Loc/id.json from Tools/loc_id.txt (English|Indonesian per line)
$map=[ordered]@{}; Get-Content (Join-Path $PSScriptRoot "loc_id.txt") -Encoding UTF8 | ForEach-Object { $i=$_.IndexOf("|"); if($i -gt 0){ $map[$_.Substring(0,$i)]=$_.Substring($i+1) } }
$entries=@($map.GetEnumerator() | ForEach-Object { [ordered]@{k=$_.Key;v=$_.Value} })
[IO.File]::WriteAllText((Join-Path $PSScriptRoot "..\Assets\Resources\Loc\id.json"),([ordered]@{entries=$entries} | ConvertTo-Json -Depth 4 -Compress),(New-Object Text.UTF8Encoding $false))
