# Generates Assets/Resources/Content/emberhall/quests.json (main / side / daily quests + random events).
# Run:  powershell -ExecutionPolicy Bypass -File Tools\gen_quests.ps1
$out = Join-Path $PSScriptRoot "..\Assets\Resources\Content\emberhall\quests.json"
function Res($id, $n) { [ordered]@{ id = $id; n = $n } }
function Req($s, $m) { [ordered]@{ stat = $s; min = $m } }
function Goal($stat, $target, $mode = "delta", $text = $null) { $g = [ordered]@{ stat = $stat; target = $target; mode = $mode }; if ($text) { $g.text = $text }; $g }

$quests = New-Object System.Collections.ArrayList
$order = 0
function Q($id, $type, $title, $desc, $goals, $rewards, $req = @(), $story = $null, $hint = $null, $weight = 1) {
    $script:order++
    $q = [ordered]@{ id = $id; type = $type; title = $title; desc = $desc; order = $script:order; goals = @($goals); rewards = @($rewards); req = @($req); weight = $weight }
    if ($story) { $q.story = $story }
    if ($hint) { $q.hint = $hint }
    $q.giver = "Elder Maren"
    [void]$quests.Add($q)
}
function Done($id) { Req "quest.done.$id" 1 }

# ===================== MAIN STORY =====================
Q "q_m01" "main" "A Humble Beginning" "Emberhall is nearly empty. Visit the Tavern and recruit a second hero." @((Goal "hero.count" 2 "abs" "Have 2 heroes")) @((Res "gold" 100)) @() `
  "Elder Maren: 'Emberhall once rang with laughter and steel. Now it is just you and me, kid. Fill these halls again!'" "bld_tavern"
Q "q_m02" "main" "First Contract" "Open the Quest Board, pick a mission and win your first battle." @((Goal "missions.won" 1 "delta" "Win a battle")) @((Res "gold" 80), (Res "potion_s" 2)) @((Done "q_m01")) `
  "Elder Maren: 'Slimes. Always slimes. But every legend started with a slime or two.'" "tab_board"
Q "q_m03" "main" "Gear Up" "Open your Bag and equip an item on a hero." @((Goal "gear.equipped" 1 "delta" "Equip an item")) @((Res "gold" 100), (Res "iron_ore" 6)) @((Done "q_m02")) `
  "Elder Maren: 'A good blade is worth ten brave speeches.'" "tab_bag"
Q "q_m04" "main" "Rookie's Triumph" "Clear Mushroom Grove in Sunmeadow Plains." @((Goal "mission.clear.m_r1_3" 1 "abs" "Clear Mushroom Grove")) @((Res "gold" 150), (Res "fame" 8)) @((Done "q_m03")) `
  "Elder Maren: 'Word is spreading. Farmers are calling you the Ember Guild again.'" "tab_board"
Q "q_m05" "main" "Forge Ahead" "Build the Blacksmith in the Guild Hall." @((Goal "facility.blacksmith" 1 "abs" "Build the Blacksmith")) @((Res "iron_ore" 10), (Res "gems" 3)) @((Done "q_m04")) `
  "Elder Maren: 'Old Brannock's forge is still down there under the rubble. Light it up!'" "bld_blacksmith"
Q "q_m06" "main" "Strength Through Sweat" "Build the Training Hall." @((Goal "facility.training" 1 "abs" "Build the Training Hall")) @((Res "gold" 200), (Res "tome_s" 1)) @((Done "q_m05")) `
  "Elder Maren: 'Heroes are not born. They are trained. Preferably by me, shouting.'" "bld_training"
Q "q_m07" "main" "Train a Hero" "Send a hero to train in the Training Hall." @((Goal "train.done" 1 "delta" "Finish a training session")) @((Res "gold" 150), (Res "tome_s" 1)) @((Done "q_m06")) `
  "Elder Maren: 'Look at that form! Almost not embarrassing anymore.'" "bld_training"
Q "q_m08" "main" "The Slime Throne" "Defeat the King Slime at the end of Sunmeadow Plains." @((Goal "mission.clear.m_r1_5" 1 "abs" "Defeat King Slime")) @((Res "gold" 300), (Res "gems" 5), (Res "soul_seal" 1)) @((Done "q_m07")) `
  "Elder Maren: 'You toppled the King Slime! ...Can slimes be kings? Never mind. Well done.'" "tab_board"
Q "q_m09" "main" "Prove Your Worth" "Earn 100 Fame to become eligible for the Rank E exam." @((Goal "rank.fame" 100 "abs" "Reach 100 Fame")) @((Res "gems" 3), (Res "gold" 200)) @((Done "q_m08")) `
  "Elder Maren: 'The Adventurers' Association is watching. Reach 100 fame and they will test you.'" "tab_board"
Q "q_m10" "main" "Rank E Trial" "Pass the Rank E exam from the Quest Board." @((Goal "rank.index" 1 "abs" "Reach Guild Rank E")) @((Res "gold" 500), (Res "gems" 10), (Res "x_amulet_2" 1)) @((Done "q_m09")) `
  "Elder Maren: 'RANK E! Whisperwood is open to us now. Stay on your toes in there.'" "tab_board"
Q "q_m11" "main" "Into the Whisperwood" "Clear Moonlit Glade in Whisperwood." @((Goal "mission.clear.m_r2_3" 1 "abs" "Clear Moonlit Glade")) @((Res "gold" 350), (Res "fame" 20)) @((Done "q_m10")) `
  "Elder Maren: 'Strange lights between the trees... and that hissing. Lamias, I bet.'" "tab_board"
Q "q_m12" "main" "Mend and Make Do" "Build the Infirmary so wounded heroes can recover." @((Goal "facility.infirmary" 1 "abs" "Build the Infirmary")) @((Res "gold" 250), (Res "potion_m" 2)) @((Done "q_m11")) `
  "Elder Maren: 'Even heroes bleed. The Infirmary will keep them in one piece.'" "bld_infirmary"
Q "q_m13" "main" "Strength in Numbers" "Grow your guild to 5 heroes." @((Goal "hero.count" 5 "abs" "Have 5 heroes")) @((Res "gems" 5), (Res "gold" 400)) @((Done "q_m12")) `
  "Elder Maren: 'Now it feels like a guild. Look at that mess in the dining hall. Beautiful.'" "bld_tavern"
Q "q_m14" "main" "Queen of Coils" "Defeat the Lamia Queen in her lair." @((Goal "mission.clear.m_r2_5" 1 "abs" "Defeat the Lamia Queen")) @((Res "gold" 600), (Res "gems" 8), (Res "soul_seal" 2)) @((Done "q_m13")) `
  "Elder Maren: 'The Lamia Queen is down! Some of her kin may even be willing to talk... for a price.'" "tab_board"
Q "q_m15" "main" "Rank D Trial" "Reach Guild Rank D." @((Goal "rank.index" 2 "abs" "Reach Guild Rank D")) @((Res "gold" 1000), (Res "gems" 15), (Res "x_ring_3" 1)) @((Done "q_m14")) `
  "Elder Maren: 'Rank D! The sands of Sunscorch Dunes await. Pack water. Lots of water.'" "tab_board"
Q "q_m16" "main" "The Beast Within" "Tame a monster at the Tavern's Beast Den." @((Goal "monster.tamed" 1 "delta" "Tame a monster")) @((Res "gems" 10), (Res "essence" 5)) @((Done "q_m15")) `
  "Elder Maren: 'A skeleton on the payroll. In my day, we paid them in nothing but bad jokes.'" "bld_tavern"
Q "q_m17" "main" "Tyrant of the Dunes" "Defeat the Sand Tyrant." @((Goal "mission.clear.m_r3_5" 1 "abs" "Defeat the Sand Tyrant")) @((Res "gold" 1500), (Res "gems" 10), (Res "soul_seal" 2)) @((Done "q_m16")) `
  "Elder Maren: 'The caravans can pass safely again. Their gratitude is... mostly gold.'" "tab_board"
Q "q_m18" "main" "Rank C Trial" "Reach Guild Rank C." @((Goal "rank.index" 3 "abs" "Reach Guild Rank C")) @((Res "gold" 2500), (Res "gems" 20), (Res "essence" 8)) @((Done "q_m17")) `
  "Elder Maren: 'Rank C. Even the king's court knows your banner now.'" "tab_board"
Q "q_m19" "main" "Into the Crypt" "Defeat the Minotaur Warlord in Gloomhold Crypt." @((Goal "mission.clear.m_r4_5" 1 "abs" "Defeat the Warlord")) @((Res "gold" 3000), (Res "gems" 15), (Res "soul_seal" 3)) @((Done "q_m18")) `
  "Elder Maren: 'The Warlord shattered, the crypt quiet at last. I... heard something laughing beyond the gate.'" "tab_board"
Q "q_m20" "main" "The Endless Spire" "Climb to floor 10 of the Endless Spire." @((Goal "mission.tower" 10 "abs" "Reach Spire floor 10")) @((Res "gems" 15), (Res "essence" 10)) @((Done "q_m19")) `
  "Elder Maren: 'They say the Spire has no top. I say challenge accepted.'" "tab_board"
Q "q_m21" "main" "Rank B Trial" "Reach Guild Rank B." @((Goal "rank.index" 4 "abs" "Reach Guild Rank B")) @((Res "gold" 5000), (Res "gems" 25), (Res "x_amulet_4" 1)) @((Done "q_m20")) `
  "Elder Maren: 'Rank B! The Citadel's gates are opening. Whatever lives there knows we are coming.'" "tab_board"
Q "q_m22" "main" "Cinders and Shadows" "Defeat the Black Magus in the Cinder Citadel." @((Goal "mission.clear.m_r5_5" 1 "abs" "Defeat the Black Magus")) @((Res "gold" 8000), (Res "gems" 30), (Res "soul_seal" 5)) @((Done "q_m21")) `
  "Elder Maren: 'The Magus falls... but his last words were about a figure beyond even him. The Rank A trial will tell more.'" "tab_board"
Q "q_m23" "main" "Rank A Trial" "Reach Guild Rank A." @((Goal "rank.index" 5 "abs" "Reach Guild Rank A")) @((Res "gold" 12000), (Res "gems" 40), (Res "essence" 20)) @((Done "q_m22")) `
  "Elder Maren: 'A legendary guild. I never thought I would live to see Emberhall like this.'" "tab_board"
Q "q_m24" "main" "Master of the Spire" "Conquer floor 40 of the Endless Spire." @((Goal "mission.tower" 40 "abs" "Reach Spire floor 40")) @((Res "gems" 40), (Res "essence" 25)) @((Done "q_m23")) `
  "Elder Maren: 'Floor forty. My knees ache just hearing it.'" "tab_board"
Q "q_m25" "main" "Legend of Emberhall" "Pass the final trial and reach Guild Rank S." @((Goal "rank.index" 6 "abs" "Reach Guild Rank S")) @((Res "gold" 30000), (Res "gems" 100), (Res "essence" 50)) @((Done "q_m24")) `
  "Elder Maren: 'Rank S... Emberhall burns bright again, brighter than ever. Whatever comes next, we face it together. Thank you, Guildmaster.'" "tab_board"

# ===================== SIDE QUESTS =====================
$sideReq = @((Req "missions.won" 1))
Q "q_s_slime1" "side" "Slime Squasher I" "Defeat 20 slimes." @((Goal "kills.tag.slime" 20)) @((Res "gold" 120)) $sideReq
Q "q_s_slime2" "side" "Slime Squasher II" "Defeat 80 slimes." @((Goal "kills.tag.slime" 80)) @((Res "gold" 400), (Res "gems" 3)) @((Done "q_s_slime1"))
Q "q_s_slime3" "side" "Slime Squasher III" "Defeat 250 slimes." @((Goal "kills.tag.slime" 250)) @((Res "gold" 1200), (Res "gems" 8)) @((Done "q_s_slime2"))
Q "q_s_undead1" "side" "Grave Digger I" "Defeat 30 undead." @((Goal "kills.tag.undead" 30)) @((Res "gold" 500), (Res "potion_m" 2)) @((Req "rank.index" 2))
Q "q_s_undead2" "side" "Grave Digger II" "Defeat 120 undead." @((Goal "kills.tag.undead" 120)) @((Res "gold" 2000), (Res "gems" 8)) @((Done "q_s_undead1"))
Q "q_s_boss1" "side" "Giant Slayer I" "Defeat 3 bosses." @((Goal "boss.killed" 3)) @((Res "gold" 300), (Res "gems" 3)) @((Req "missions.won" 5))
Q "q_s_boss2" "side" "Giant Slayer II" "Defeat 15 bosses." @((Goal "boss.killed" 15)) @((Res "gold" 2500), (Res "gems" 12)) @((Done "q_s_boss1"))
Q "q_s_earn1" "side" "Pocket Change" "Earn 1,000 gold." @((Goal "earn.gold" 1000)) @((Res "gems" 2), (Res "iron_ore" 5)) $sideReq
Q "q_s_earn2" "side" "Gold Hoarder" "Earn 20,000 gold." @((Goal "earn.gold" 20000)) @((Res "gems" 6), (Res "essence" 3)) @((Done "q_s_earn1"))
Q "q_s_earn3" "side" "Dragon's Wallet" "Earn 250,000 gold." @((Goal "earn.gold" 250000)) @((Res "gems" 20), (Res "essence" 10)) @((Done "q_s_earn2"))
Q "q_s_smith1" "side" "Smith's Apprentice" "Successfully enhance gear 3 times." @((Goal "enhance.success" 3)) @((Res "gold" 200), (Res "iron_ore" 8)) @((Done "q_m05"))
Q "q_s_smith2" "side" "Master Enhancer" "Enhance an item to +5." @((Goal "enhance.best" 5 "abs")) @((Res "gems" 6), (Res "essence" 3)) @((Done "q_s_smith1"))
Q "q_s_smith3" "side" "Legendary Smith" "Craft 15 items at the Blacksmith." @((Goal "crafted" 15)) @((Res "gems" 8), (Res "essence" 5)) @((Done "q_s_smith1"))
Q "q_s_family" "side" "Growing Family" "Recruit 6 heroes in total." @((Goal "hero.hired" 6 "abs")) @((Res "gold" 300), (Res "gems" 3)) $sideReq
Q "q_s_train1" "side" "Dedicated Trainer" "Finish 5 training sessions." @((Goal "train.done" 5)) @((Res "gold" 400), (Res "tome_s" 2)) @((Done "q_m07"))
Q "q_s_heal1" "side" "Field Medic" "Finish 3 healing sessions." @((Goal "heal.done" 3)) @((Res "gold" 250), (Res "potion_m" 2)) @((Done "q_m12"))
Q "q_s_loot1" "side" "Treasure Hunter" "Collect 20 pieces of gear." @((Goal "gear.gained" 20)) @((Res "gold" 300), (Res "iron_ore" 10)) $sideReq
Q "q_s_epic" "side" "Shiny Things" "Own a gear piece of Epic quality or better." @((Goal "gear.best_rarity" 3 "abs")) @((Res "gems" 8), (Res "essence" 4)) @((Req "missions.won" 8))
Q "q_s_exp1" "side" "Caravan Master" "Win 5 expeditions." @((Goal "expeditions.won" 5)) @((Res "gold" 500), (Res "gems" 4)) @((Req "missions.won" 3))
Q "q_s_exp2" "side" "Trade Baron" "Win 30 expeditions." @((Goal "expeditions.won" 30)) @((Res "gold" 4000), (Res "gems" 12)) @((Done "q_s_exp1"))
Q "q_s_perfect" "side" "Flawless Victory" "Win 10 battles without losing a hero." @((Goal "perfect.wins" 10)) @((Res "gold" 600), (Res "gems" 5)) @((Req "missions.won" 4))
Q "q_s_events" "side" "Guild Life" "Resolve 5 guild events." @((Goal "events.done" 5)) @((Res "gold" 350), (Res "gems" 4)) @((Req "events.done" 1))
Q "q_s_market" "side" "Shopaholic" "Buy 5 offers from the Market." @((Goal "market.bought" 5)) @((Res "gold" 300), (Res "gems" 3)) @((Req "rank.index" 1))
Q "q_s_level1" "side" "Veteran" "Raise a hero to level 10." @((Goal "hero.maxlevel" 10 "abs")) @((Res "gold" 400), (Res "tome_s" 2)) $sideReq
Q "q_s_level2" "side" "Grand Veteran" "Raise a hero to level 25." @((Goal "hero.maxlevel" 25 "abs")) @((Res "gems" 10), (Res "tome_l" 2)) @((Done "q_s_level1"))
Q "q_s_stars" "side" "Star Collector" "Earn 30 mission stars." @((Goal "mission.totalstars" 30 "abs")) @((Res "gems" 8), (Res "gold" 800)) @((Req "missions.won" 5))
Q "q_s_vault" "side" "Tax Collector" "Collect the Treasury 10 times." @((Goal "vault.collected" 10)) @((Res "gold" 500), (Res "gems" 3)) @((Req "facility.treasury" 1))
Q "q_s_promote" "side" "Ascension" "Promote a hero." @((Goal "hero.promoted" 1)) @((Res "gems" 8), (Res "essence" 5)) @((Req "hero.maxlevel" 8))
Q "q_s_beasts" "side" "Beastmaster" "Have 3 monsters in your guild." @((Goal "hero.monsters" 3 "abs")) @((Res "gems" 12), (Res "essence" 6)) @((Done "q_m16"))
Q "q_s_tower1" "side" "Spire Climber" "Reach floor 25 of the Endless Spire." @((Goal "mission.tower" 25 "abs")) @((Res "gems" 15), (Res "essence" 10)) @((Done "q_m20"))
Q "q_s_research" "side" "Scholar" "Finish 3 research projects." @((Goal "research.done" 3)) @((Res "gems" 6), (Res "essence" 4)) @((Req "facility.library" 1))

# ===================== DAILY QUESTS =====================
$dr = @((Res "gold" 120), (Res "gems" 1))
Q "d_win3" "daily" "Monster Hunter" "Win 3 battles." @((Goal "missions.won" 3)) @((Res "gold" 150), (Res "gems" 1)) @() $null $null 3
Q "d_exped" "daily" "Send a Party" "Start an expedition." @((Goal "expeditions.started" 1)) @((Res "gold" 120), (Res "gems" 1)) @() $null $null 2
Q "d_vault" "daily" "Fill the Coffers" "Collect gold from the Treasury." @((Goal "vault.collected" 1)) @((Res "gold" 100), (Res "gems" 1)) @((Req "facility.treasury" 1)) $null $null 2
Q "d_train" "daily" "Hit the Yard" "Start a training session." @((Goal "train.started" 1)) @((Res "gold" 100), (Res "iron_ore" 3)) @((Req "facility.training" 1)) $null $null 2
Q "d_spend" "daily" "Big Spender" "Spend 500 gold." @((Goal "spend.gold" 500)) @((Res "gold" 150), (Res "gems" 1)) @() $null $null 2
Q "d_forge" "daily" "Tinkering" "Try to enhance gear." @((Goal "enhance.tries" 1)) @((Res "gold" 120), (Res "iron_ore" 4)) @((Req "facility.blacksmith" 1)) $null $null 2
Q "d_kill" "daily" "Pest Control" "Defeat 15 enemies." @((Goal "enemies.killed" 15)) @((Res "gold" 140), (Res "gems" 1)) @() $null $null 3
Q "d_heal" "daily" "Bedside Manner" "Start a healing session." @((Goal "heal.started" 1)) @((Res "gold" 100), (Res "potion_s" 1)) @((Req "facility.infirmary" 1)) $null $null 1
Q "d_loot" "daily" "Magpie" "Collect 3 pieces of gear." @((Goal "gear.gained" 3)) @((Res "gold" 130), (Res "iron_ore" 3)) @() $null $null 2
Q "d_recruit" "daily" "Open Doors" "Resolve a guild event or recruit a hero." @((Goal "hero.hired" 1)) @((Res "gold" 150), (Res "gems" 1)) @() $null $null 1

# ===================== EVENTS =====================
$events = New-Object System.Collections.ArrayList
function Out($w, $text, $gain = @(), $loss = @(), $hurt = 0, $item = $null) { $o = [ordered]@{ w = $w; text = $text; gain = @($gain); loss = @($loss); hurt = $hurt }; if ($item) { $o.item = $item }; $o }
function Ch($text, $outs, $cost = @(), $req = @()) { [ordered]@{ text = $text; cost = @($cost); req = @($req); outcomes = @($outs) } }
function Ev($id, $title, $text, $sprite, $w, $choices, $req = @()) { [void]$events.Add([ordered]@{ id = $id; title = $title; text = $text; sprite = $sprite; w = $w; req = @($req); choices = @($choices) }) }

Ev "ev_merchant" "Wandering Merchant" "A cheerful merchant parks his cart in front of the guild and spreads out some wares." "chara_9" 3 @(
    (Ch "Buy supplies (100 gold)" @((Out 1 "He sells you a handy bundle of potions." @((Res "potion_m" 2), (Res "tome_s" 1)))) @((Res "gold" 100))),
    (Ch "Haggle hard" @((Out 55 "He grumbles, but gives you a decent discount package." @((Res "potion_m" 2), (Res "iron_ore" 5))), (Out 45 "He storms off. No deal."))),
    (Ch "Send him away" @((Out 1 "The merchant shrugs and moves on."))))
Ev "ev_brawl" "Tavern Brawl" "A fight erupts at the Tavern! Chairs are flying." "chara_5" 3 @(
    (Ch "Break it up" @((Out 70 "The room calms down. Patrons toss you a few coins." @((Res "fame" 3), (Res "gold" 40))), (Out 30 "You take a stool to the shoulder, but order is restored." @((Res "fame" 2)) @() 0.15))),
    (Ch "Join the fun" @((Out 1 "What a night! You win a few bets on the side." @((Res "gold" 90)) @() 0.2))),
    (Ch "Ignore it" @((Out 1 "It burns itself out. The tavern keeper is not amused."))))
Ev "ev_kitten" "Lost Kitten" "A little girl begs your help to find her lost kitten." "chara_7" 2 @(
    (Ch "Help her search" @((Out 70 "You find the kitten! Her father pays you handsomely." @((Res "gold" 70), (Res "fame" 2))), (Out 30 "The kitten is gone, but she hands you her piggy bank." @((Res "gold" 25))))),
    (Ch "Too busy" @((Out 1 "She walks away, sniffling."))))
Ev "ev_map" "Tattered Treasure Map" "A drunk adventurer sells you a very suspicious map." "chara_16" 2 @(
    (Ch "Follow the map" @((Out 40 "X marks the spot! A chest of coins!" @((Res "gold" 250), (Res "iron_ore" 4))), (Out 35 "You find a glittering crystal." @((Res "essence" 2))), (Out 25 "It was a trap! A hero is hurt." @() @() 0.3))),
    (Ch "Sell the map (150 gold)" @((Out 1 "A collector happily pays." @((Res "gold" 150))))))
Ev "ev_noble" "Noble Patron" "A noble in a velvet cloak offers sponsorship." "chara_14" 2 @(
    (Ch "Accept a donation" @((Out 1 "He leaves a heavy purse and a cold smile." @((Res "gold" 220))))),
    (Ch "Take a contract in his name" @((Out 65 "The job goes smoothly and your name travels far." @((Res "fame" 12), (Res "gold" 60))), (Out 35 "Nothing but trouble, but you learned something." @((Res "fame" 4)))))) @((Req "rank.index" 1))
Ev "ev_rats" "Rat Infestation" "The pantry is overrun by giant rats!" "chara_11" 2 @(
    (Ch "Hire exterminators (80 gold)" @((Out 1 "Clean and quiet. The cook praises your name." @((Res "fame" 2))) ) @((Res "gold" 80))),
    (Ch "Let the heroes handle it" @((Out 1 "They crush the rats, but one hero is nibbled." @((Res "fame" 4)) @() 0.15))))
Ev "ev_bard" "Traveling Bard" "A bard offers to sing of your guild's deeds." "chara_22" 2 @(
    (Ch "Tip the bard (50 gold)" @((Out 1 "He composes a catchy ballad. Fame spreads!" @((Res "fame" 8)))) @((Res "gold" 50))),
    (Ch "Share your own tale" @((Out 60 "The tavern roars with applause." @((Res "fame" 4))), (Out 40 "You get heckled mercilessly." ))))
Ev "ev_stranger" "Mysterious Stranger" "A hooded figure drops a sealed box on the counter and vanishes." "chara_1" 1.5 @(
    (Ch "Open the box" @((Out 50 "A Soul Seal gleams inside!" @((Res "soul_seal" 1))), (Out 30 "Arcane dust pours out." @((Res "essence" 3))), (Out 20 "A puff of smoke stings your eyes." @() @() 0.2))),
    (Ch "Leave it alone" @((Out 1 "The box crumbles to dust overnight.")))) @((Req "rank.index" 1))
Ev "ev_festival" "Harvest Festival" "The town is celebrating and wants your guild to join." "chara_18" 1.5 @(
    (Ch "Join the festivities" @((Out 1 "Good food, good company! Your heroes feel refreshed." @((Res "fame" 6), (Res "gems" 2))))),
    (Ch "Run a gear stall (60 gold)" @((Out 70 "Business is brisk!" @((Res "gold" 200))), (Out 30 "Slow day. You break even." @((Res "gold" 60)))) @((Res "gold" 60))))

$pack = [ordered]@{ quests = $quests; events = $events }
Set-Content -Path $out -Value ($pack | ConvertTo-Json -Depth 14 -Compress) -Encoding UTF8
Write-Host "quests: $($quests.Count) events: $($events.Count)"
