# Generates Assets/Resources/Content/emberhall/items.json (items, recipes, loot tables).
# Run:  powershell -ExecutionPolicy Bypass -File Tools\gen_items.ps1
$out = Join-Path $PSScriptRoot "..\Assets\Resources\Content\emberhall\items.json"
$items = New-Object System.Collections.ArrayList
$recipes = New-Object System.Collections.ArrayList

function StatV($id, $v) { [ordered]@{ id = $id; v = $v } }
function Cost($id, $n) { [ordered]@{ id = $id; n = $n } }

$tiers = 1..5
$tint = @("#B08060", "#C8D0D8", "#5EC8FF", "#FF9A3C", "#E060FF")
$priceBase = @(20, 60, 160, 420, 1100)

# ---- weapons ----------------------------------------------------------------------------
$atk = @(6, 14, 26, 44, 70)
$weapons = @(
    @{ fam = "sword";  icon = "sword";  names = @("Rusty Sword", "Iron Sword", "Steel Blade", "Ember Brand", "Dragonfang"); extra = "def" },
    @{ fam = "spear";  icon = "spear";  names = @("Wooden Spear", "Iron Spear", "Halberd", "Ember Pike", "Dragon Lance"); extra = "def" },
    @{ fam = "bow";    icon = "bow";    names = @("Short Bow", "Hunter Bow", "Longbow", "Ember Recurve", "Skyfall"); extra = "crit" },
    @{ fam = "staff";  icon = "staff";  names = @("Oak Staff", "Mage Staff", "Crystal Staff", "Ember Scepter", "Archmage Staff"); extra = "spd" },
    @{ fam = "dagger"; icon = "dagger"; names = @("Rusty Dagger", "Steel Dagger", "Shadow Knife", "Ember Fang", "Nightblade"); extra = "crit" }
)
foreach ($w in $weapons) {
    foreach ($t in $tiers) {
        $i = $t - 1
        $stats = @((StatV "atk" $atk[$i]))
        switch ($w.extra) {
            "def"  { $stats += (StatV "def" ([math]::Round($t * 1.5))) }
            "crit" { $stats += (StatV "crit" ($t + 1)) }
            "spd"  { $stats += (StatV "spd" ([math]::Round($t * 1.2))) }
        }
        [void]$items.Add([ordered]@{
            id = "w_$($w.fam)_$t"; name = $w.names[$i]; type = "weapon"; slot = "weapon"; family = $w.fam; tier = $t
            price = $priceBase[$i]; icon = "icon:$($w.icon)"; tint = $tint[$i]; stats = $stats
            desc = "A tier $t $($w.fam)."
        })
    }
}

# ---- armor ------------------------------------------------------------------------------
$hp = @(30, 70, 130, 220, 350); $def = @(3, 7, 13, 22, 35)
$armors = @(
    @{ id = "plate"; icon = "plate"; names = @("Padded Vest", "Chain Mail", "Scale Armor", "Ember Plate", "Dragon Plate"); style = "heavy" },
    @{ id = "robe";  icon = "robe";  names = @("Cloth Tunic", "Leather Armor", "Ranger Cloak", "Ember Robe", "Archmage Robe"); style = "light" }
)
foreach ($a in $armors) {
    foreach ($t in $tiers) {
        $i = $t - 1
        if ($a.style -eq "heavy") { $stats = @((StatV "hp" $hp[$i]), (StatV "def" $def[$i])) }
        else { $stats = @((StatV "hp" ([math]::Round($hp[$i] * 0.75))), (StatV "def" ([math]::Round($def[$i] * 0.6))), (StatV "spd" ([math]::Round($t * 1.5)))) }
        [void]$items.Add([ordered]@{
            id = "a_$($a.id)_$t"; name = $a.names[$i]; type = "armor"; slot = "armor"; family = $a.style; tier = $t
            price = [int]($priceBase[$i] * 1.1); icon = "icon:$($a.icon)"; tint = $tint[$i]; stats = $stats; desc = "Tier $t body armor."
        })
    }
}

# ---- accessories ------------------------------------------------------------------------
$ringAtk = @(3, 7, 12, 20, 32); $amHp = @(20, 45, 85, 140, 220)
$ringN = @("Copper Ring", "Silver Ring", "Sapphire Ring", "Ember Ring", "Arcane Ring")
$amN = @("Wooden Charm", "Bone Amulet", "Jade Pendant", "Ember Amulet", "Dragon Heart")
foreach ($t in $tiers) {
    $i = $t - 1
    [void]$items.Add([ordered]@{ id = "x_ring_$t"; name = $ringN[$i]; type = "accessory"; slot = "accessory"; family = "ring"; tier = $t; price = [int]($priceBase[$i] * 0.9); icon = "icon:ring"; tint = $tint[$i]; stats = @((StatV "atk" $ringAtk[$i]), (StatV "crit" ($t + 1))); desc = "Tier $t ring." })
    [void]$items.Add([ordered]@{ id = "x_amulet_$t"; name = $amN[$i]; type = "accessory"; slot = "accessory"; family = "amulet"; tier = $t; price = [int]($priceBase[$i] * 0.9); icon = "icon:amulet"; tint = $tint[$i]; stats = @((StatV "hp" $amHp[$i]), (StatV "def" ([math]::Round($t * 1.8))), (StatV "spd" $t)); desc = "Tier $t amulet." })
}

# ---- materials / consumables ------------------------------------------------------------
[void]$items.Add([ordered]@{ id = "iron_ore"; name = "Iron Ore"; type = "material"; price = 6; icon = "icon:ore"; desc = "Used to forge and enhance gear."; tier = 1 })
[void]$items.Add([ordered]@{ id = "essence"; name = "Arcane Essence"; type = "material"; price = 40; icon = "icon:essence"; desc = "Glowing dust used for promotions and powerful forging."; tier = 1 })
[void]$items.Add([ordered]@{ id = "soul_seal"; name = "Soul Seal"; type = "material"; price = 120; icon = "icon:seal"; desc = "A binding sigil. Needed to tame monsters."; tier = 1 })
[void]$items.Add([ordered]@{ id = "potion_s"; name = "Herbal Tonic"; type = "consumable"; price = 20; icon = "prop:pot_16"; use = "heal_pct"; useV = 0.35; desc = "Heals 35% HP of one hero."; tier = 1 })
[void]$items.Add([ordered]@{ id = "potion_m"; name = "Healing Draught"; type = "consumable"; price = 55; icon = "prop:pot_12"; use = "heal_pct"; useV = 0.7; desc = "Heals 70% HP of one hero."; tier = 1 })
[void]$items.Add([ordered]@{ id = "potion_l"; name = "Phoenix Elixir"; type = "consumable"; price = 140; icon = "prop:pot_20"; use = "heal_pct"; useV = 1.0; desc = "Fully heals one hero."; tier = 1 })
[void]$items.Add([ordered]@{ id = "tome_s"; name = "Training Manual"; type = "consumable"; price = 60; icon = "prop:book_01"; use = "xp"; useV = 60; desc = "Grants 60 XP to one hero."; tier = 1 })
[void]$items.Add([ordered]@{ id = "tome_l"; name = "Master's Tome"; type = "consumable"; price = 220; icon = "prop:book_04"; use = "xp"; useV = 260; desc = "Grants 260 XP to one hero."; tier = 1 })
[void]$items.Add([ordered]@{ id = "coin_bag"; name = "Coin Pouch"; type = "consumable"; price = 1; icon = "icon:coin"; use = "gold"; useV = 250; desc = "Contains 250 gold."; tier = 1 })
[void]$items.Add([ordered]@{ id = "fame_scroll"; name = "Fame Scroll"; type = "consumable"; price = 1; icon = "prop:book_06"; use = "fame"; useV = 25; desc = "Reading it spreads your guild's name. +25 Fame."; tier = 1 })

# ---- recipes ----------------------------------------------------------------------------
foreach ($it in $items) {
    if ($it.type -in @("weapon", "armor", "accessory")) {
        $t = $it.tier
        $cost = @((Cost "gold" ([int]($it.price * 1.3))), (Cost "iron_ore" ($t * 4 + 2)))
        if ($t -ge 4) { $cost += (Cost "essence" ($t - 2)) }
        [void]$recipes.Add([ordered]@{
            id = "rc_$($it.id)"; result = $it.id; name = $it.name; n = 1; cost = $cost
            time = [int](12 * [math]::Pow($t, 1.9)); req = @([ordered]@{ stat = "facility.blacksmith"; min = ($t * 2 - 1) })
        })
    }
}
[void]$recipes.Add([ordered]@{ id = "rc_potion_s"; result = "potion_s"; name = "Herbal Tonic"; n = 2; cost = @((Cost "gold" 25)); time = 10; req = @([ordered]@{ stat = "facility.blacksmith"; min = 1 }) })
[void]$recipes.Add([ordered]@{ id = "rc_potion_m"; result = "potion_m"; name = "Healing Draught"; n = 2; cost = @((Cost "gold" 80), (Cost "iron_ore" 2)); time = 30; req = @([ordered]@{ stat = "facility.blacksmith"; min = 3 }) })
[void]$recipes.Add([ordered]@{ id = "rc_potion_l"; result = "potion_l"; name = "Phoenix Elixir"; n = 1; cost = @((Cost "gold" 220), (Cost "essence" 2)); time = 60; req = @([ordered]@{ stat = "facility.blacksmith"; min = 6 }) })

# ---- loot tables ------------------------------------------------------------------------
function E($id, $w, $min, $max, $tier = 1, $chance = 1) { [ordered]@{ id = $id; w = $w; min = $min; max = $max; tier = $tier; chance = $chance } }
$loot = New-Object System.Collections.ArrayList
function LT($id, $rolls, $entries) { [void]$loot.Add([ordered]@{ id = $id; rolls = $rolls; entries = $entries }) }

LT "loot_r1" 2 @((E "iron_ore" 4 1 3), (E "@gear" 3 1 1 1), (E "potion_s" 2 1 1), (E "coin_bag" 0.5 1 1))
LT "loot_r2" 2 @((E "iron_ore" 4 2 4), (E "@gear" 2.5 1 1 1), (E "@gear" 1.5 1 1 2), (E "potion_s" 2 1 2), (E "potion_m" 0.6 1 1), (E "tome_s" 0.5 1 1))
LT "loot_r3" 2 @((E "iron_ore" 4 3 5), (E "@gear" 2 1 1 2), (E "@gear" 1 1 1 3), (E "potion_m" 1.5 1 2), (E "tome_s" 1 1 1), (E "essence" 0.6 1 1))
LT "loot_r4" 3 @((E "iron_ore" 4 4 7), (E "@gear" 2.5 1 1 3), (E "@gear" 0.8 1 1 4), (E "potion_m" 1.5 1 2), (E "potion_l" 0.5 1 1), (E "tome_s" 1 1 2), (E "essence" 1 1 2))
LT "loot_r5" 3 @((E "iron_ore" 4 5 9), (E "@gear" 2.5 1 1 4), (E "@gear" 0.8 1 1 5), (E "potion_l" 1 1 1), (E "tome_l" 0.8 1 1), (E "essence" 1.2 1 3))
LT "loot_boss_r1" 3 @((E "iron_ore" 3 3 5), (E "@gear" 4 1 1 1), (E "@gear" 2 1 1 2), (E "soul_seal" 1.5 1 1), (E "tome_s" 1 1 1))
LT "loot_boss_r2" 3 @((E "iron_ore" 3 4 6), (E "@gear" 3 1 1 2), (E "@gear" 2 1 1 3), (E "soul_seal" 1.5 1 1), (E "essence" 1 1 2))
LT "loot_boss_r3" 3 @((E "iron_ore" 3 5 8), (E "@gear" 3 1 1 3), (E "@gear" 1 1 1 4), (E "soul_seal" 1.5 1 1), (E "essence" 1.5 1 3))
LT "loot_boss_r4" 4 @((E "iron_ore" 3 6 10), (E "@gear" 3 1 1 4), (E "@gear" 1 1 1 5), (E "soul_seal" 1.5 1 2), (E "essence" 2 2 4), (E "tome_l" 1 1 1))
LT "loot_boss_r5" 4 @((E "iron_ore" 3 8 12), (E "@gear" 3 1 1 4), (E "@gear" 2 1 1 5), (E "soul_seal" 2 1 2), (E "essence" 2 3 5), (E "tome_l" 1.5 1 2))
LT "loot_tower" 2 @((E "iron_ore" 4 3 8), (E "essence" 2 1 3), (E "@gear" 2 1 1 3), (E "@gear" 1.5 1 1 4), (E "@gear" 0.6 1 1 5), (E "potion_l" 1 1 1), (E "tome_l" 0.8 1 1), (E "soul_seal" 0.4 1 1))
LT "loot_c1" 1 @((E "iron_ore" 4 1 3), (E "potion_s" 2 1 2), (E "@gear" 1 1 1 1), (E "coin_bag" 1 1 1))
LT "loot_c2" 2 @((E "iron_ore" 4 2 5), (E "potion_s" 1 1 2), (E "potion_m" 1.5 1 1), (E "@gear" 1.5 1 1 2), (E "coin_bag" 1 1 1), (E "tome_s" 0.7 1 1))
LT "loot_c3" 2 @((E "iron_ore" 4 3 7), (E "potion_m" 1.5 1 2), (E "@gear" 1.5 1 1 3), (E "essence" 1 1 2), (E "tome_s" 1 1 2))
LT "loot_c4" 3 @((E "iron_ore" 4 5 9), (E "potion_l" 1 1 1), (E "@gear" 1.6 1 1 4), (E "essence" 1.4 1 3), (E "tome_l" 1 1 1), (E "soul_seal" 0.5 1 1))
LT "loot_exam" 3 @((E "essence" 2 2 4), (E "@gear" 3 1 1 3), (E "@gear" 1.5 1 1 4), (E "soul_seal" 1 1 1), (E "tome_l" 1 1 1))

$pack = [ordered]@{ items = $items; recipes = $recipes; loot = $loot }
$json = $pack | ConvertTo-Json -Depth 12
Set-Content -Path $out -Value $json -Encoding UTF8
Write-Host "items: $($items.Count)  recipes: $($recipes.Count)  loot tables: $($loot.Count)"
