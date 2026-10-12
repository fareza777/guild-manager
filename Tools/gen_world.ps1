# Generates Assets/Resources/Content/emberhall/world.json (enemies, regions, missions, exams, contract templates).
# Run:  powershell -ExecutionPolicy Bypass -File Tools\gen_world.ps1
$out = Join-Path $PSScriptRoot "..\Assets\Resources\Content\emberhall\world.json"
function StatV($id, $v) { [ordered]@{ id = $id; v = $v } }
function Res($id, $n) { [ordered]@{ id = $id; n = $n } }
function Req($s, $m) { [ordered]@{ stat = $s; min = $m } }

# ---------------- enemies ----------------
$enemies = New-Object System.Collections.ArrayList
function MkEnemy($id, $name, $sprite, $scale, $boss, $tags, $hp, $atk, $def, $spd, $skills, $crit = 3) {
    $skin = if ($sprite.StartsWith("bat:")) { "battler" } else { "sheet" }
    [void]$enemies.Add([ordered]@{ id = $id; name = $name; sprite = $sprite; skin = $skin; scale = $scale; boss = $boss; tags = $tags
        stats = @((StatV "hp" $hp), (StatV "atk" $atk), (StatV "def" $def), (StatV "spd" $spd), (StatV "crit" $crit)); skills = $skills })
}
# tier 1
MkEnemy "e_slime_g" "Green Slime" "mon:01_0" 1 $false @("slime","t1") 36 7 1 9 @("attack")
MkEnemy "e_slime_b" "Blue Slime" "mon:01_1" 1 $false @("slime","t1") 38 7 2 8 @("attack")
MkEnemy "e_slime_r" "Fire Slime" "mon:01_2" 1 $false @("slime","t1","t2") 36 9 1 10 @("attack")
MkEnemy "e_shroom" "Spore Cap" "mon:ADD_1" 1 $false @("plant","t1","t2") 44 7 3 7 @("attack","spore")
MkEnemy "e_slime_k" "King Slime" "mon:03_0" 1.9 $true @("slime","boss","boss_t1") 190 11 4 8 @("attack","gel_guard","charge")
# tier 2
MkEnemy "e_wasp" "Giant Wasp" "mon:ADD_3" 1 $false @("insect","t2","t3") 40 10 2 15 @("attack","sting")
MkEnemy "e_worm" "Cave Worm" "mon:ADD_2" 1 $false @("worm","t2") 62 9 4 8 @("bite","constrict")
MkEnemy "e_slime_s" "Sword Slime" "mon:03_2" 1 $false @("slime","t2") 52 11 3 11 @("attack","charge")
MkEnemy "e_slime_y" "Gold Slime" "mon:01_4" 1 $false @("slime","t2") 50 9 2 10 @("attack")
MkEnemy "e_lamia" "Lamia" "bat:LamiaB" 0.8 $false @("lamia","t2") 82 12 4 12 @("attack","constrict")
MkEnemy "e_lamia_q" "Lamia Queen" "bat:LamiaA" 1.5 $true @("lamia","boss","boss_t2") 270 15 6 11 @("bite","constrict","charm")
# tier 3
MkEnemy "e_scorpion" "Dune Scorpion" "mon:04_5" 1 $false @("scorpion","t3") 80 14 7 10 @("attack","tail_strike")
MkEnemy "e_scorpion_d" "Black Scorpion" "mon:04_4" 1 $false @("scorpion","t3") 92 15 9 9 @("attack","tail_strike")
MkEnemy "e_husk" "Desert Husk" "mon:02_0" 1 $false @("undead","t3","t4") 100 13 4 6 @("bite")
MkEnemy "e_ghoul" "Red Ghoul" "mon:02_2" 1 $false @("undead","t3") 90 16 3 9 @("bite")
MkEnemy "e_scorpion_k" "Sand Tyrant" "mon:04_6" 2.0 $true @("scorpion","boss","boss_t3") 440 20 12 10 @("tail_strike","charge","roar")
# tier 4
MkEnemy "e_skel" "Skeleton" "mon:05_0" 1 $false @("undead","skeleton","t4") 85 17 8 10 @("attack")
MkEnemy "e_skel_b" "Bone Soldier" "mon:05_2" 1 $false @("undead","skeleton","t4") 95 17 10 9 @("attack","cleave")
MkEnemy "e_skelw" "Skeleton Warrior" "mon:05_3" 1 $false @("undead","skeleton","t4") 130 21 12 9 @("attack","cleave")
MkEnemy "e_ghost" "Crypt Ghost" "mon:02_4" 1 $false @("undead","ghost","t4") 70 18 3 13 @("attack","wail")
MkEnemy "e_wraith" "Ember Wraith" "mon:02_7" 1 $false @("undead","ghost","t4","t5") 78 19 3 13 @("attack","wail")
MkEnemy "e_minotaur_k" "Minotaur Warlord" "bat:MinotaurA" 1.4 $true @("beast","boss","boss_t4") 640 28 16 9 @("attack","charge","roar")
# tier 5
MkEnemy "e_cultist" "Hooded Cultist" "mon:ADD_5" 1 $false @("cultist","t5") 105 24 6 11 @("dark_bolt","curse")
MkEnemy "e_imp" "Imp Seductress" "mon:ADD_4" 1 $false @("demon","t5") 115 22 6 14 @("dark_bolt","charm")
MkEnemy "e_genie" "Wishful Genie" "mon:04_0" 1.2 $false @("genie","t5") 190 26 12 10 @("attack","wish_blast")
MkEnemy "e_mino" "Minotaur" "mon:04_2" 1.2 $false @("beast","t5") 230 30 16 9 @("attack","charge")
MkEnemy "e_magus" "Black Magus" "bat:BlackMagusA" 1.0 $true @("cultist","boss","boss_t5") 920 38 18 11 @("shadow_bolt","curse","doom")
MkEnemy "e_succ_q" "Succubus Queen" "bat:SuccubusA" 1.0 $true @("demon","boss","boss_t5") 800 34 14 14 @("dark_bolt","charm","doom")
MkEnemy "e_genius" "Genius Overlord" "bat:GeniusA" 1.0 $true @("genie","boss","boss_t5") 980 36 20 10 @("attack","wish_blast","doom")

# ---------------- regions ----------------
$regions = @(
    [ordered]@{ id = "r1"; name = "Sunmeadow Plains"; desc = "Rolling grasslands plagued by slimes."; bg = "PlainA"; color = "#8FD16A"; order = 1; tier = 1; req = @() },
    [ordered]@{ id = "r2"; name = "Whisperwood"; desc = "A moody forest full of stingers and serpents."; bg = "ForestA"; color = "#4FA36B"; order = 2; tier = 2; req = @((Req "rank.index" 1)) },
    [ordered]@{ id = "r3"; name = "Sunscorch Dunes"; desc = "Scorching sands hide ancient horrors."; bg = "DesertA"; color = "#F0C060"; order = 3; tier = 3; req = @((Req "rank.index" 2)) },
    [ordered]@{ id = "r4"; name = "Gloomhold Crypt"; desc = "Undead roam these forgotten halls."; bg = "DungeonA"; color = "#9A8FD0"; order = 4; tier = 4; req = @((Req "rank.index" 3)) },
    [ordered]@{ id = "r5"; name = "Cinder Citadel"; desc = "The dark fortress of the Black Magus."; bg = "DungeonB"; color = "#FF6B6B"; order = 5; tier = 5; req = @((Req "rank.index" 4)) },
    [ordered]@{ id = "tower"; name = "Endless Spire"; desc = "How high can your guild climb?"; bg = "DungeonC"; color = "#6FE3FF"; order = 20; tier = 3; req = @((Req "rank.index" 2)) },
    [ordered]@{ id = "contracts"; name = "Contracts"; desc = ""; bg = "PlainA"; order = 90; tier = 1; req = @() },
    [ordered]@{ id = "exams"; name = "Rank Exams"; desc = ""; bg = "DungeonD"; order = 91; tier = 1; req = @() }
)

# ---------------- missions ----------------
$missions = New-Object System.Collections.ArrayList
function Wave($ids) { [ordered]@{ enemies = @($ids | Group-Object | ForEach-Object { Res $_.Name $_.Count }) } }
function MkMission($id, $region, $order, $name, $lvl, $tier, $waves, $gold, $fame, $xp, $loot, $recommend, $boss = $false, $first = @(), $par = 10, $bg = $null) {
    $rw = @((Res "gold" $gold)); if ($fame -gt 0) { $rw += (Res "fame" $fame) }
    $m = [ordered]@{ id = $id; kind = "battle"; region = $region; name = $name; desc = ""; order = $order; level = $lvl; tier = $tier
        waves = @($waves | ForEach-Object { Wave $_ }); rewards = $rw; firstClear = $first; xp = $xp; loot = $loot; req = @(); recommend = $recommend; boss = $boss; par3 = $par; maxTurns = 60 }
    if ($bg) { $m.bg = $bg }
    [void]$missions.Add($m)
}
function Chain($ms) { for ($i = 1; $i -lt $ms.Count; $i++) { } }
$fc = @((Res "gems" 3))
$fcb = @((Res "gems" 8), (Res "gold" 150))

# R1
MkMission "m_r1_1" "r1" 11 "Slime Meadow" 1 1 @(@("e_slime_g","e_slime_g"), @("e_slime_g","e_slime_b")) 22 3 14 "loot_r1" @("warrior","mage") $false $fc 8
MkMission "m_r1_2" "r1" 12 "Gooey Hills" 2 1 @(@("e_slime_g","e_slime_b","e_slime_r"), @("e_slime_b","e_slime_r")) 30 3 17 "loot_r1" @("archer","warrior") $false $fc 9
MkMission "m_r1_3" "r1" 13 "Mushroom Grove" 3 1 @(@("e_shroom","e_slime_g"), @("e_slime_r","e_slime_r","e_slime_b")) 38 4 20 "loot_r1" @("cleric","mage") $false $fc 10
MkMission "m_r1_4" "r1" 14 "Old Farm Road" 4 1 @(@("e_shroom","e_slime_b","e_slime_g"), @("e_shroom","e_slime_r","e_slime_r")) 50 4 24 "loot_r1" @("knight","archer") $false $fc 11
MkMission "m_r1_5" "r1" 15 "The Slime Throne" 5 1 @(@("e_slime_g","e_slime_b","e_slime_r"), @("e_slime_k","e_slime_g","e_slime_b")) 75 10 40 "loot_boss_r1" @("knight","cleric","mage") $true $fcb 14
# R2
MkMission "m_r2_1" "r2" 21 "Forest Edge" 6 2 @(@("e_wasp","e_wasp"), @("e_worm","e_shroom")) 80 8 50 "loot_r2" @("archer","rogue") $false $fc 10
MkMission "m_r2_2" "r2" 22 "Hollow Trail" 7 2 @(@("e_slime_s","e_slime_s","e_wasp"), @("e_worm","e_worm")) 95 9 58 "loot_r2" @("warrior","knight") $false $fc 10
MkMission "m_r2_3" "r2" 23 "Moonlit Glade" 8 2 @(@("e_lamia","e_wasp","e_wasp"), @("e_shroom","e_shroom","e_slime_y")) 110 10 66 "loot_r2" @("mage","cleric") $false $fc 11
MkMission "m_r2_4" "r2" 24 "Serpent Den" 9 2 @(@("e_slime_s","e_wasp","e_lamia"), @("e_worm","e_lamia","e_shroom")) 130 12 76 "loot_r2" @("rogue","knight") $false $fc 12
MkMission "m_r2_5" "r2" 25 "Lair of the Lamia Queen" 10 2 @(@("e_wasp","e_wasp","e_lamia"), @("e_lamia_q","e_lamia","e_wasp")) 190 25 110 "loot_boss_r2" @("knight","cleric","mage") $true $fcb 15 "ForestB"
# R3
MkMission "m_r3_1" "r3" 31 "Dune Road" 11 3 @(@("e_scorpion","e_scorpion","e_wasp"), @("e_ghoul","e_husk")) 170 16 120 "loot_r3" @("archer","mage") $false $fc 10
MkMission "m_r3_2" "r3" 32 "Bleached Bones" 12 3 @(@("e_scorpion","e_scorpion_d","e_ghoul"), @("e_husk","e_husk","e_husk")) 200 18 135 "loot_r3" @("warrior","cleric") $false $fc 11
MkMission "m_r3_3" "r3" 33 "Oasis Ambush" 13 3 @(@("e_wasp","e_wasp","e_scorpion_d"), @("e_ghoul","e_ghoul","e_scorpion")) 235 20 150 "loot_r3" @("rogue","mage") $false $fc 11
MkMission "m_r3_4" "r3" 34 "Mirage Ruins" 14 3 @(@("e_scorpion_d","e_husk","e_ghoul"), @("e_scorpion_d","e_scorpion_d","e_husk")) 275 22 166 "loot_r3" @("knight","cleric") $false $fc 12
MkMission "m_r3_5" "r3" 35 "The Sand Tyrant" 16 3 @(@("e_scorpion","e_scorpion_d","e_ghoul"), @("e_scorpion_k","e_scorpion","e_scorpion")) 400 50 240 "loot_boss_r3" @("knight","cleric","archer") $true $fcb 16 "DesertB"
# R4
MkMission "m_r4_1" "r4" 41 "Crypt Gates" 17 4 @(@("e_skel","e_skel","e_ghost"), @("e_skel_b","e_skel")) 340 30 260 "loot_r4" @("cleric","mage") $false $fc 10
MkMission "m_r4_2" "r4" 42 "Hall of Whispers" 19 4 @(@("e_ghost","e_ghost","e_skel_b"), @("e_skelw","e_skel","e_skel")) 400 33 290 "loot_r4" @("mage","archer") $false $fc 11
MkMission "m_r4_3" "r4" 43 "Bone Gallery" 21 4 @(@("e_skelw","e_skel_b","e_ghost"), @("e_wraith","e_wraith","e_skelw")) 470 37 320 "loot_r4" @("warrior","cleric") $false $fc 11
MkMission "m_r4_4" "r4" 44 "Sunken Vault" 22 4 @(@("e_skelw","e_skelw","e_wraith"), @("e_ghost","e_wraith","e_skel_b","e_skel")) 540 41 350 "loot_r4" @("knight","mage") $false $fc 12
MkMission "m_r4_5" "r4" 45 "Warlord's Throne" 24 4 @(@("e_skelw","e_wraith","e_skel_b"), @("e_minotaur_k","e_skelw","e_skelw")) 780 90 520 "loot_boss_r4" @("knight","cleric","archer") $true $fcb 16 "DungeonD"
# R5
MkMission "m_r5_1" "r5" 51 "Cinder Gate" 26 5 @(@("e_cultist","e_imp","e_cultist"), @("e_mino","e_imp")) 700 55 560 "loot_r5" @("knight","cleric") $false $fc 10
MkMission "m_r5_2" "r5" 52 "Ash Corridor" 28 5 @(@("e_genie","e_cultist","e_imp"), @("e_mino","e_mino")) 820 60 620 "loot_r5" @("mage","archer") $false $fc 11
MkMission "m_r5_3" "r5" 53 "Hall of Embers" 30 5 @(@("e_imp","e_imp","e_cultist","e_wraith"), @("e_genie","e_mino","e_genie")) 950 66 690 "loot_r5" @("warrior","cleric") $false $fc 11
MkMission "m_r5_4" "r5" 54 "Obsidian Stairs" 32 5 @(@("e_mino","e_genie","e_cultist"), @("e_imp","e_imp","e_mino","e_genie")) 1080 72 760 "loot_r5" @("knight","mage") $false $fc 12
MkMission "m_r5_5" "r5" 55 "Throne of the Black Magus" 34 5 @(@("e_genie","e_mino","e_cultist","e_imp"), @("e_magus","e_cultist","e_cultist")) 1600 160 1100 "loot_boss_r5" @("knight","cleric","mage") $true @((Res "gems" 15), (Res "gold" 600), (Res "essence" 5)) 18 "DungeonB"

# exams
function MkExam($id, $order, $name, $lvl, $waves, $rec) {
    $m = [ordered]@{ id = $id; kind = "exam"; region = "exams"; name = $name; desc = "Defeat the examiner's champions to earn the next guild rank."; order = $order; level = $lvl; tier = 1
        waves = @($waves | ForEach-Object { Wave $_ }); rewards = @((Res "gold" ($lvl * 40))); firstClear = @((Res "gems" 10)); xp = ($lvl * 10); loot = "loot_exam"; req = @(); recommend = $rec; boss = $true; par3 = 15; maxTurns = 80 }
    [void]$missions.Add($m)
}
MkExam "exam_e" 910 "Rank E Trial" 7 @(@("e_shroom","e_slime_s","e_slime_s"), @("e_slime_k","e_wasp","e_wasp")) @("warrior","cleric")
MkExam "exam_d" 911 "Rank D Trial" 12 @(@("e_lamia","e_lamia","e_wasp"), @("e_lamia_q","e_scorpion","e_wasp")) @("knight","mage")
MkExam "exam_c" 912 "Rank C Trial" 19 @(@("e_scorpion_d","e_scorpion_d","e_ghoul"), @("e_scorpion_k","e_skel","e_skel_b")) @("knight","cleric")
MkExam "exam_b" 913 "Rank B Trial" 27 @(@("e_skelw","e_skelw","e_wraith"), @("e_minotaur_k","e_skelw","e_ghost")) @("knight","archer")
MkExam "exam_a" 914 "Rank A Trial" 35 @(@("e_cultist","e_imp","e_genie"), @("e_magus","e_cultist","e_imp")) @("cleric","mage")
MkExam "exam_s" 915 "Rank S Trial: The Final Test" 45 @(@("e_genie","e_mino","e_imp"), @("e_genius","e_succ_q","e_cultist"), @("e_magus","e_succ_q","e_genius")) @("knight","cleric","mage")

# contract templates (expeditions)
function MkContract($id, $name, $desc, $dur, $power, $gold, $fame, $xp, $loot, $rec, $req = @()) {
    $rw = @((Res "gold" $gold)); if ($fame -gt 0) { $rw += (Res "fame" $fame) }
    [void]$missions.Add([ordered]@{ id = $id; kind = "contract"; region = "contracts"; name = $name; desc = $desc; order = 900; level = 1; tier = 1; waves = @(); rewards = $rw; firstClear = @(); xp = $xp; loot = $loot; req = $req; duration = $dur; power = $power; recommend = $rec })
}
MkContract "ct_herbs" "Herb Gathering" "Collect rare herbs from the nearby fields." 180 55 40 1 10 "loot_c1" @("cleric","rogue")
MkContract "ct_rats" "Cellar Cleanup" "Clear a merchant's cellar of giant rats." 300 75 70 1 14 "loot_c1" @("warrior","knight")
MkContract "ct_escort" "Caravan Escort" "Protect a trade caravan on the road." 900 115 190 1 30 "loot_c1" @("knight","archer")
MkContract "ct_bandits" "Bandit Camp Raid" "Smash a bandit hideout." 1800 160 380 2 55 "loot_c2" @("warrior","rogue") @((Req "rank.index" 1))
MkContract "ct_ruins" "Ancient Ruins Survey" "Map out a half-collapsed ruin." 3600 210 800 3 100 "loot_c2" @("mage","rogue") @((Req "rank.index" 1))
MkContract "ct_nest" "Monster Nest" "Burn out a nest before it spreads." 7200 270 1700 5 180 "loot_c3" @("warrior","mage","cleric") @((Req "rank.index" 2))
MkContract "ct_dive" "Deep Dive Expedition" "A long delve into uncharted tunnels." 14400 340 3600 8 340 "loot_c3" @("knight","archer","cleric") @((Req "rank.index" 2))
MkContract "ct_hoard" "Dragon's Hoard Rumor" "Chase a legend across the mountains." 28800 430 7500 14 650 "loot_c4" @("knight","mage","cleric","rogue") @((Req "rank.index" 3))
MkContract "ct_siege" "Border Siege Support" "Reinforce a besieged outpost." 5400 300 1500 5 160 "loot_c3" @("warrior","knight","cleric") @((Req "rank.index" 2))


foreach ($m in $missions) {
    if ($m.id -match '^m_(r\d)_(\d)$' -and [int]$Matches[2] -gt 1) {
        $m.req = @((Req "mission.clear.m_$($Matches[1])_$([int]$Matches[2] - 1)" 1))
    }
}
$pack = [ordered]@{ enemies = $enemies; regions = $regions; missions = $missions }
Set-Content -Path $out -Value ($pack | ConvertTo-Json -Depth 12 -Compress) -Encoding UTF8
Write-Host "enemies: $($enemies.Count) regions: $($regions.Count) missions: $($missions.Count)"
