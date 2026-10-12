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
                "A great storm left Emberhall in need of a little care. As sunlight warms its old stones, one small spark promises a fresh beginning.",
                "Badai besar membuat Emberhall perlu dirawat kembali. Mentari menghangatkan batu tuanya. Sebuah bara kecil menjanjikan awal baru.",
                "Every great adventure starts somewhere.",
                "Setiap petualangan besar punya awal."),
            new Beat("story_oath", 2, "A Hearth for Everyone", "Tungku untuk Semua",
                "Maren has kept the hearth glowing. Now, travelers gather around its warmth, ready to trade stories and build something wonderful together.",
                "Maren menjaga tungku tetap menyala. Para pengelana datang untuk berbagi cerita dan membangun sesuatu yang indah bersama.",
                "\"A warm fire, good friends. We have all we need to begin.\" — Maren",
                "\"Api hangat dan teman baik. Bekal awal kita sudah lengkap.\" — Maren"),
            new Beat("story_dawn", 3, "Adventure Awaits", "Petualangan Menanti",
                "The morning road leads to green valleys and a mysterious shining Spire. Gather your heroes, raise your banner, and make the next story yours.",
                "Jalan pagi menuju lembah hijau dan Menara berkilau yang penuh misteri. Himpun pahlawanmu, kibarkan panji, dan mulai kisahmu sendiri.",
                "A new guild. A bright horizon. Your adventure.",
                "Guild baru. Cakrawala cerah. Petualanganmu.")
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
