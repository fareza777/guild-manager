using NUnit.Framework;
using UnityEngine;

namespace Emberhall.Tests
{
    public class LanguageTests
    {
        const string LanguageKey = "lang";
        const string MigrationKey = "english_default_v1";
        bool hadLanguage, hadMigration;
        string savedLanguage;
        int savedMigration;

        [SetUp]
        public void SavePreferences()
        {
            hadLanguage = PlayerPrefs.HasKey(LanguageKey);
            hadMigration = PlayerPrefs.HasKey(MigrationKey);
            savedLanguage = PlayerPrefs.GetString(LanguageKey);
            savedMigration = PlayerPrefs.GetInt(MigrationKey);
            PlayerPrefs.DeleteKey(LanguageKey);
            PlayerPrefs.DeleteKey(MigrationKey);
        }

        [TearDown]
        public void RestorePreferences()
        {
            if (hadLanguage) PlayerPrefs.SetString(LanguageKey, savedLanguage);
            else PlayerPrefs.DeleteKey(LanguageKey);
            if (hadMigration) PlayerPrefs.SetInt(MigrationKey, savedMigration);
            else PlayerPrefs.DeleteKey(MigrationKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void FreshInstallStartsInEnglish()
        {
            Assert.AreEqual("en", Prefs.InitializeLanguage());
            Assert.AreEqual("en", PlayerPrefs.GetString(LanguageKey));
            Assert.AreEqual(1, PlayerPrefs.GetInt(MigrationKey));
        }

        [Test]
        public void OlderInstallMovesToEnglishOnceAndKeepsLaterManualChoice()
        {
            PlayerPrefs.SetString(LanguageKey, "id");
            Assert.AreEqual("en", Prefs.InitializeLanguage());
            Prefs.Lang = "id";
            Assert.AreEqual("id", Prefs.InitializeLanguage());
            Assert.AreEqual("id", Prefs.InitializeLanguage());
        }

        [Test]
        public void ExplicitLanguageSelectionIsPreservedBeforeInitialization()
        {
            Prefs.Lang = "id";
            Assert.AreEqual("id", Prefs.InitializeLanguage());
            Prefs.Lang = "en";
            Assert.AreEqual("en", Prefs.InitializeLanguage());
        }

        [TestCase(null, "en")]
        [TestCase("", "en")]
        [TestCase("en", "en")]
        [TestCase("ID", "id")]
        [TestCase(" id ", "id")]
        [TestCase("fr", "en")]
        public void UnsupportedOrBlankLanguageAlwaysFallsBackToEnglish(string input, string expected)
        {
            Prefs.Lang = input;
            Assert.AreEqual(expected, Prefs.InitializeLanguage());
            Assert.AreEqual(expected, PlayerPrefs.GetString(LanguageKey));
        }
    }
}
