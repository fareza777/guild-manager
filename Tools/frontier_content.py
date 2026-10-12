"""Author the Lantern Road chapters and readable rich-text labels."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parent.parent
chapters = [
 ("Maren sets a second cup by the hearth. The first companion has answered your banner; the Lantern Road begins with two people choosing to help.", "Maren menaruh cangkir kedua di tungku. Sahabat pertama menyambut panjimu; Jalan Lentera bermula dari dua orang yang memilih membantu."),
 ("The meadow courier reaches home at last. In the slime's trail, your heroes find a shard of amber glass: the first clue to the missing beacons.", "Kurir padang rumput akhirnya pulang. Dalam jejak slime, tim menemukan pecahan kaca kuning: petunjuk pertama tentang suar yang hilang."),
 ("The courier leaves a bundle of rescued gear at your door. Maren polishes the amber shard. It glows whenever the party gathers around the hearth.", "Kurir meninggalkan perlengkapan yang diselamatkan. Maren memoles pecahan kuning. Cahayanya menyala setiap tim berkumpul di tungku."),
 ("Beyond Mushroom Grove, a waystone hums again. The shard fits its hollow. For the first time since the storm, villagers can follow a light home.", "Di balik Hutan Jamur, batu penunjuk berdengung lagi. Pecahan itu pas di rongganya. Sejak badai, warga akhirnya punya cahaya penuntun pulang."),
 ("Your smith studies the broken lens mount. This was no ordinary lantern: five lenses once carried a message between the valleys and the Spire.", "Pandai besimu memeriksa dudukan lensa. Ini bukan lentera biasa: lima lensa dahulu membawa pesan antara lembah dan Menara."),
 ("An old training bell rings across the courtyard. Maren once learned here too. She offers her first lesson: a guild grows by helping each other improve.", "Lonceng latihan kembali berbunyi. Maren dahulu belajar di sini. Pelajaran pertamanya: guild tumbuh saat anggotanya saling membantu."),
 ("Practice turns a hesitant recruit into a dependable companion. Your heroes mark the next route together. No one will have to walk it alone.", "Latihan mengubah rekrut yang ragu menjadi sahabat andalan. Para pahlawan menandai rute berikutnya bersama. Tak seorang pun berjalan sendiri."),
 ("The King Slime releases an amber lens from its crown. Sunmeadow's beacon blooms gold. Across the valley, a distant tower answers with a single flash.", "Raja Slime melepaskan lensa kuning dari mahkotanya. Suar Sunmeadow bersinar emas. Menara jauh di lembah membalas dengan satu kilatan."),
 ("Farmers bring bread, letters, and news of your deeds. The council has noticed the reopened road. An invitation to the guild trials arrives at dawn.", "Petani membawa roti, surat, dan kabar jasa guildmu. Dewan melihat jalan yang terbuka. Undangan ujian guild tiba saat fajar."),
 ("Your new seal grants passage into Whisperwood. Maren recognizes the flash from the tower: the old keepers are asking Emberhall to finish their work.", "Segel baru membuka jalan ke Whisperwood. Maren mengenali kilatan Menara: penjaga lama meminta Emberhall menuntaskan pekerjaan mereka."),
 ("Under silver leaves, your party follows a trail of blue sparks. A forest keeper has hidden the second lens beyond the glade, safe from the storm.", "Di bawah daun perak, tim mengikuti percikan biru. Penjaga hutan menyimpan lensa kedua di balik lembah, terlindung dari badai."),
 ("The infirmary opens with fresh linen and flowers. Maren places a lamp by every bed. Bringing everyone home matters more than arriving first.", "Rumah sakit dibuka dengan seprai bersih dan bunga. Maren menaruh lampu di setiap ranjang. Pulang bersama lebih penting daripada tiba pertama."),
 ("Five companions share a table once built for one. Their maps overlap, their stories differ, and their laughter makes the old hall feel like home.", "Lima sahabat duduk di meja yang dahulu untuk seorang. Peta mereka bertemu, kisah mereka berbeda, dan tawa membuat aula terasa seperti rumah."),
 ("The Lamia Queen yields the forest lens to those who have earned her respect. Whisperwood glows blue; two restored beacons now trace a path south.", "Ratu Lamia menyerahkan lensa hutan kepada tim yang dihormatinya. Whisperwood berkilau biru; dua suar kini menunjukkan jalan ke selatan."),
 ("The council entrusts your guild with the caravan roads. Beyond the dunes lies a third beacon, buried beneath the route the storm erased.", "Dewan mempercayakan jalan karavan kepada guildmu. Di balik bukit pasir, suar ketiga terkubur di jalur yang dihapus badai."),
 ("A new creature curls up beside the hearth. It knows paths no map can show. Maren quietly adds another bowl to the supper table.", "Makhluk baru berbaring di sisi tungku. Ia mengenal jalan yang tak ada di peta. Maren menambah satu mangkuk di meja makan."),
 ("The Sand Tyrant falls and the buried beacon rises. Its lens shines rose-gold. Caravans sound their bells as three valleys reconnect at last.", "Tiran Pasir tumbang dan suar terkubur muncul. Lensanya bersinar emas merah. Lonceng karavan berbunyi saat tiga lembah akhirnya tersambung."),
 ("Your banner now flies at every caravan stop. An archivist sends a rubbing from Gloomhold: the five lenses form a map, not a weapon.", "Panjimu berkibar di tiap perhentian karavan. Arsiparis mengirim prasasti Gloomhold: lima lensa membentuk peta, bukan senjata."),
 ("The Warlord's vault holds a violet lens and a keeper's letter. Maren reads her mother's handwriting: 'Let every lost traveler find a welcome.'", "Brankas Panglima menyimpan lensa ungu dan surat penjaga. Maren mengenali tulisan ibunya: 'Sambut setiap pengelana yang tersesat.'"),
 ("On the Spire's tenth landing, your four lights reveal a hidden stair. The fifth lens waits in Cinder Citadel. The keepers' message is nearly whole.", "Di lantai sepuluh Menara, empat cahaya mengungkap tangga rahasia. Lensa kelima menanti di Benteng Cinder. Pesan penjaga hampir utuh."),
 ("The council names Emberhall a guardian of the roads. Maren finally tells the guild why she stayed: her mother believed someone would return to finish the lights.", "Dewan menamai Emberhall penjaga jalan. Maren akhirnya bercerita: ibunya percaya seseorang akan kembali menuntaskan cahaya-cahaya itu."),
 ("The Black Magus relinquishes the last lens. Five colors cross the sky above the Citadel. Far away, Emberhall's hearth answers with a golden flame.", "Penyihir Hitam menyerahkan lensa terakhir. Lima warna membentang di atas Benteng. Jauh di sana, tungku Emberhall membalas dengan nyala emas."),
 ("Letters arrive from every village you helped. The final ascent is ready. Your companions tuck the letters beside the five lenses and set out together.", "Surat tiba dari tiap desa yang dibantu. Pendakian terakhir siap. Para sahabat menyimpan surat bersama lima lensa, lalu berangkat bersama."),
 ("At the fortieth landing, the lenses reveal the keeper's map: every road leads to a hearth. The Spire was built to connect people, and yours has answered.", "Di lantai empat puluh, lensa mengungkap peta penjaga: tiap jalan menuju tungku. Menara dibangun untuk menyatukan orang, dan guildmu telah menjawab."),
 ("Five beacons shine above a world brought together. Maren hangs her mother's map by the door. Below it, a new traveler asks if there is room for one more.", "Lima suar menerangi dunia yang kembali menyatu. Maren menggantung peta ibunya di pintu. Seorang pengelana baru bertanya apakah masih ada tempat."),
]

quest_path = root / 'Assets/Resources/Content/emberhall/quests.json'
data = json.loads(quest_path.read_text(encoding='utf-8-sig'))
loc_path = root / 'Assets/Resources/Loc/id.json'
loc = json.loads(loc_path.read_text(encoding='utf-8-sig'))
entries = {entry['k']: entry['v'] for entry in loc['entries']}
short_copy = {
 'You already have one hero and starting gold. Open the Tavern and hire one more.': ('Open the Tavern. Hire one companion using your starting gold.', 'Buka Kedai. Rekrut satu sahabat dengan emas awalmu.'),
 'Scroll to the candidates. Tap HIRE + the gold price on an affordable hero.': ('Tap HIRE on an affordable candidate, then claim your guide reward.', 'Tekan REKRUT pada calon yang terjangkau, lalu ambil hadiah panduan.'),
 "Claim this chapter's reward to unlock your next step.": ('Claim this reward to unlock your next step.', 'Ambil hadiah ini untuk membuka langkah berikutnya.'),
 'Your roster is full. Upgrade the Barracks, then return to recruit.': ('Upgrade the Barracks to make room for another hero.', 'Tingkatkan Barak untuk menampung pahlawan baru.'),
 'No heroes are ready. Open a hero and use a healing potion, or visit the Infirmary.': ('Use a healing potion, or recover at the Infirmary.', 'Gunakan ramuan penyembuh, atau pulihkan diri di Rumah Sakit.'),
 'Open Campaign on the Board. Start Slime Meadow, keep the selected heroes, then tap FIGHT.': ('Open Campaign, start Slime Meadow, and tap FIGHT.', 'Buka Kampanye, mulai Padang Slime, lalu tekan BERTARUNG.'),
 'Open Bag → Gear. Tap AUTO-EQUIP to give each hero their best compatible items.': ('Open Bag → Gear and tap AUTO-EQUIP.', 'Buka Tas → Gear lalu tekan PAKAI OTOMATIS.'),
}
early_goals = [
 ('Tavern → Hire one companion. Then claim your reward.', 'Kedai → Rekrut satu sahabat. Lalu ambil hadiahmu.'),
 ('Board → Campaign → Slime Meadow. Select your party and fight.', 'Papan → Kampanye → Padang Slime. Pilih tim dan bertarung.'),
 ('Bag → Gear → Auto equip. Then claim your reward.', 'Tas → Gear → Pakai otomatis. Lalu ambil hadiahmu.'),
 ('Clear Gooey Hills, then Mushroom Grove. Heal between battles.', 'Selesaikan Bukit Lengket, lalu Hutan Jamur. Pulihkan tim sebelum bertarung.'),
]
for index, (english, indonesian) in enumerate(early_goals, 1):
    next(q for q in data['quests'] if q['id'] == f'q_m{index:02}')['desc'] = english
    entries[english] = indonesian
for english, indonesian in short_copy.values(): entries[english] = indonesian
for index, (english, indonesian) in enumerate(chapters, 1):
    quest = next(q for q in data['quests'] if q['id'] == f'q_m{index:02}')
    quest['story'] = english
    entries[english] = indonesian
entries.update({'Story':'Cerita', 'Side':'Sampingan', 'Journal':'Jurnal', 'Completed':'Selesai', 'Current chapter':'Bab saat ini', 'Pause':'Jeda', 'Resume':'Lanjut'})
loc['entries'] = [{'k':k, 'v':v} for k,v in sorted(entries.items())]
for path, content in ((quest_path,data), (loc_path,loc)):
    path.write_bytes((json.dumps(content, ensure_ascii=False, separators=(',',':'))+'\n').encode('utf-8'))

for path in (root/'Assets/Emberhall/UI').glob('*.cs'):
    was_crlf = b'\r\n' in path.read_bytes()
    text = path.read_text(encoding='utf-8-sig')
    for old, (new, _) in short_copy.items(): text = text.replace(old, new)
    # Explicit rich-text sizes bypass the shared typography helper.
    text = re.sub(r'<size=(\d+)>', lambda m: f'<size={max(36, int(m[1]))}>', text)
    path.write_bytes(text.replace('\n','\r\n').encode('utf-8') if was_crlf else text.encode('utf-8'))
