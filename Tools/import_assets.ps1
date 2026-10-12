# Copies the sprites used by the game from the shared pixel asset library into Assets/Resources/Art.
# Idempotent: re-run any time. Source library is never modified.
param([string]$Lib = "C:\Pixel Game Master\PixelAssetLibrary\Gif\Super_Retro_Collection\Resources")

$dst = Join-Path $PSScriptRoot "..\Assets\Resources\Art"
New-Item -ItemType Directory -Force $dst | Out-Null
$count = 0

function Copy-Set($srcRel, $dstRel, $filter = "*.png", [switch]$Recurse) {
    $src = Join-Path $Lib $srcRel
    $out = Join-Path $dst $dstRel
    New-Item -ItemType Directory -Force $out | Out-Null
    $files = if ($Recurse) { Get-ChildItem $src -Filter $filter -File -Recurse } else { Get-ChildItem $src -Filter $filter -File }
    foreach ($f in $files) {
        if ($f.Name -match ' ') { continue }
        Copy-Item $f.FullName (Join-Path $out $f.Name) -Force
        $script:count++
    }
}

Copy-Set "Characters\Characters" "chara" "chara_*.png"
Copy-Set "Characters\Monsters" "monster" "*.png"
Copy-Set "Battlers" "battler"
Copy-Set "Backgrounds" "bg"
foreach ($d in "Houses","Pots","Books","Statues","Fires","Torches","Barrels","Crates","Lamps","Trees","Rocks","Columns") {
    Copy-Set "Prefabs\$d\Sprites" "props" 
}
Copy-Set "Animations\Chest" "anim" "chest_*.png"
Copy-Set "Animations\Cristal" "anim" "cristal_*.png"
Copy-Set "Animations\Farm" "anim" "farm_icons_*.png"

# ARPG attack frames (per character skin): sword / spear / staff / slash, shield-less variant (_0)
$arpgOut = Join-Path $dst "arpg"
New-Item -ItemType Directory -Force $arpgOut | Out-Null
foreach ($n in 0..31) {
    foreach ($t in "sword01","spear01","staff01","slash01") {
        $s = Join-Path $Lib "ARPG\character_$n\srw_arpg_${t}_0.png"
        if (Test-Path $s) { Copy-Item $s (Join-Path $arpgOut "${t}_c$n.png") -Force; $count++ }
    }
}
# arrow projectile
Copy-Item (Join-Path $Lib "Hero\arrow\shot\arrow_shot_RIGHT.png") (Join-Path $dst "props\arrow_shot_RIGHT.png") -Force
Write-Host "Copied $count sprite files to $dst"
