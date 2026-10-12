using ManagerKit.UI;

namespace Emberhall
{
    /// <summary>Short, authored copy for the cinematic opening and the first steps of a new guild.</summary>
    public static class StoryScript
    {
        public sealed class Beat
        {
            public readonly string ArtKey;
            readonly string titleEn, titleId, bodyEn, bodyId, quoteEn, quoteId;
            readonly int chapter;

            public Beat(string artKey, int chapter, string titleEn, string titleId,
                string bodyEn, string bodyId, string quoteEn, string quoteId)
            {
                ArtKey = artKey; this.chapter = chapter;
                this.titleEn = titleEn; this.titleId = titleId;
                this.bodyEn = bodyEn; this.bodyId = bodyId;
                this.quoteEn = quoteEn; this.quoteId = quoteId;
            }

            public string Kicker => Pick("PROLOGUE", "PROLOG") + "  /  " + chapter.ToString("00");
            public string Title => Pick(titleEn, titleId);
            public string Body => Pick(bodyEn, bodyId);
            public string Quote => Pick(quoteEn, quoteId);
        }

        public static string Pick(string english, string indonesian) => L.Lang == "id" ? indonesian : english;

        public static readonly Beat[] Prologue =
        {
            new Beat("story_fall", 1, "A Spark at Sunrise", "Bara di Pagi Hari",
                "Five beacon lenses vanished in the storm. Emberhall's last keeper still tends the hearth, waiting for you.",
                "Lima lensa suar hilang dalam badai. Penjaga terakhir Emberhall tetap merawat tungku, menunggumu.",
                "One ember can light a thousand roads.",
                "Satu bara dapat menerangi seribu jalan."),
            new Beat("story_oath", 2, "A Hearth for Everyone", "Tungku untuk Semua",
                "Maren's map marks five lost lights and one shining Spire. Rebuild the guild. Help the villages find their way home.",
                "Peta Maren menandai lima cahaya hilang dan satu Menara. Bangun guild dan bantu penduduk menemukan jalan pulang.",
                "\"We mend the road one kindness at a time.\" — Maren",
                "\"Kita pulihkan jalan, satu kebaikan setiap saat.\" — Maren"),
            new Beat("story_frontier", 3, "The Lantern Road", "Jalan Lentera",
                "Recruit a companion in Sunmeadow. Reopen the meadow road, recover its beacon lens, and uncover the Spire's secret.",
                "Rekrut sahabat di Sunmeadow. Buka jalan padang rumput, temukan lensa suar, dan ungkap rahasia Menara.",
                "A brighter world begins with your guild.",
                "Dunia yang lebih cerah bermula dari guildmu.")
        };

        public static string Tagline => Pick("EVERY LEGEND BEGINS WITH AN EMBER", "SETIAP LEGENDA BERMULA DARI BARA");
        public static string Begin => Pick("LIGHT THE HEARTH", "NYALAKAN TUNGKU");
        public static string CharterTitle => Pick("Raise Your Banner", "Kibarkan Panjimu");
        public static string CharterBody => Pick(
            "Give your guild a name, choose a banner, and welcome the first hero of your new chapter.",
            "Beri nama guildmu, pilih panji, lalu sambut pahlawan pertama dalam kisah barumu.");
        public static string StarterHint => Pick(
            "Your first hero sets your opening style. Every class can join your guild later.",
            "Pahlawan pertama menentukan gaya awalmu. Semua kelas bisa bergabung nanti.");

        public static string[] OnboardingHeading => new[]
        {
            Prologue[0].Title, Prologue[1].Title, Prologue[2].Title, CharterTitle
        };
        public static string[] OnboardingBody => new[]
        {
            Prologue[0].Body, Prologue[1].Body, Prologue[2].Body, CharterBody
        };
        public static string[] LoopTitle => new[]
        {
            Pick("Gather your heroes", "Himpun pahlawan"),
            Pick("Choose your battles", "Pilih pertempuran"),
            Pick("Build a lasting guild", "Bangun guild yang kuat")
        };
        public static string[] LoopDescription => new[]
        {
            Pick("Recruit at the Tavern. Mix a defender, damage dealer, and healer for a stronger party.",
                "Rekrut di Kedai. Gabungkan pelindung, penyerang, dan penyembuh untuk tim yang kuat."),
            Pick("Read the Quest Board. Choose your party, prepare their gear, and face the frontier.",
                "Baca Papan Misi. Pilih tim, siapkan perlengkapan, lalu hadapi wilayah liar."),
            Pick("Bring rewards home. Equip better gear, train your heroes, and restore the hall.",
                "Bawa hadiah pulang. Pasang gear lebih baik, latih pahlawan, dan pulihkan aula.")
        };
    }
}
