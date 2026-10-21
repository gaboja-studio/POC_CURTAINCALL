using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace CurtainCall.Telemetry.Pipeline.EditorTests
{
    public sealed class SurveyPolicyTests
    {
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void LogAndSurveyTagsAreIndependentAndSurveyRequiresExactVersion(bool logs, bool surveys)
        {
            var tags = new List<string>(); if (logs) tags.Add(PlaytestBuildTags.Logs); if (surveys) tags.Add(PlaytestBuildTags.Surveys);
            var config = new PlaytestBuildTags { gameVersion = "0.1.0", tags = tags.ToArray() };
            Assert.That(config.AllowsLogs, Is.EqualTo(logs)); Assert.That(config.AllowsSurveys("0.1.0"), Is.EqualTo(surveys));
            Assert.That(config.AllowsSurveys("0.1.1"), Is.False); Assert.That(new PlaytestBuildTags().AllowsLogs, Is.False);
        }
        [Test] public void FormsKeepDifferentEntryIdsAndIncludeSharedRunAndPlayer()
        {
            string run = "7a9e2d31-8f5b-4cde-9201-1bfa8294a732";
            string code = SurveyLaunchPolicy.Code(SurveyLaunchPolicy.Night, "TEST", run, 1, new string('a', 32), new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc));
            Assert.That(code, Does.Contain(".TEST.NIGHT1." + run.Replace("-", "") + ".1."));
            Assert.That(SurveyLaunchPolicy.Url(SurveyLaunchPolicy.Night, SurveyLaunchPolicy.NightEntry, code), Does.Contain("entry.280071335="));
            Assert.That(SurveyLaunchPolicy.Url(SurveyLaunchPolicy.Exit, SurveyLaunchPolicy.ExitEntry, code), Does.Contain("entry.809038892="));
        }
        [Test] public void RestartAndNextRunCannotReopenButDifferentFormAndAccountCan()
        {
            string root = Path.Combine(Path.GetTempPath(), "curtaincall-survey-" + Guid.NewGuid().ToString("N"));
            try
            {
                int opened = 0; Action<string> launch = _ => opened++; var cache = new SurveyLaunchCache(root);
                Assert.That(cache.TryOpen("user", SurveyLaunchPolicy.Night, SurveyLaunchPolicy.NightEntry, "first-run", launch), Is.True);
                cache = new SurveyLaunchCache(root);
                Assert.That(cache.TryOpen("user", SurveyLaunchPolicy.Night, SurveyLaunchPolicy.NightEntry, "next-run", launch), Is.False);
                Assert.That(cache.TryOpen("user", SurveyLaunchPolicy.Exit, SurveyLaunchPolicy.ExitEntry, "next-run", launch), Is.True);
                Assert.That(cache.TryOpen("other", SurveyLaunchPolicy.Night, SurveyLaunchPolicy.NightEntry, "next-run", launch), Is.True);
                Assert.That(opened, Is.EqualTo(3));
            }
            finally { foreach (string file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root); }
        }
        [Test] public void BadConfigurationOrLaunchFailureNeverConsumesFirstOpen()
        {
            string root = Path.Combine(Path.GetTempPath(), "curtaincall-survey-" + Guid.NewGuid().ToString("N")); var cache = new SurveyLaunchCache(root);
            Assert.Throws<ArgumentException>(() => cache.TryOpen("user", SurveyLaunchPolicy.Night, "", "code", _ => {}));
            Assert.That(cache.WasOpened("user", SurveyLaunchPolicy.Night), Is.False);
            try
            {
                Assert.Throws<IOException>(() => cache.TryOpen("user", SurveyLaunchPolicy.Night, SurveyLaunchPolicy.NightEntry, "code", _ => throw new IOException()));
                Assert.That(cache.WasOpened("user", SurveyLaunchPolicy.Night), Is.False);
            }
            finally { if (Directory.Exists(root)) { foreach (string file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root); } }
        }
        [Test] public void InstallationKeepsLegacyClaimsAcrossBuildsAndProcessRestarts()
        {
            string root = Path.Combine(Path.GetTempPath(), "curtaincall-survey-" + Guid.NewGuid().ToString("N"));
            try
            {
                var cache = new SurveyLaunchCache(root); int opened = 0;
                Assert.That(cache.TryOpen("account:old-anonymous-id", SurveyLaunchPolicy.Night, SurveyLaunchPolicy.NightEntry, "old-build", _ => opened++), Is.True);
                cache = new SurveyLaunchCache(root);
                Assert.That(cache.TryOpen(SurveyLaunchCache.LocalIdentity, SurveyLaunchPolicy.Night, SurveyLaunchPolicy.NightEntry, "new-build", _ => opened++), Is.False);
                Assert.That(cache.TryOpen(SurveyLaunchCache.LocalIdentity, SurveyLaunchPolicy.Exit, SurveyLaunchPolicy.ExitEntry, "exit", _ => opened++), Is.True);
                cache = new SurveyLaunchCache(root);
                Assert.That(cache.TryOpen(SurveyLaunchCache.LocalIdentity, SurveyLaunchPolicy.Exit, SurveyLaunchPolicy.ExitEntry, "restart", _ => opened++), Is.False);
                Assert.That(opened, Is.EqualTo(2));
            }
            finally { foreach (string file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root); }
        }
    }
}
