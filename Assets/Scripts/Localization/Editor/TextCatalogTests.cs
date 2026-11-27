using System.IO;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace CurtainCall.Localization.Editor
{
    public sealed class TextCatalogTests
    {
        [Test]
        public void ShippingCatalogResolvesKeysAndFormattedWorldPrompts()
        {
            var table = GameLanguage.LoadCatalog();
            Assert.That(table.Display("@loc:room.prompt.shop-phone", false), Is.EqualTo("Open Shop"));
            Assert.That(table.Display("구조 장갑 줍기", false), Is.EqualTo("Pick up Rescue Gloves"));
            Assert.That(table.Display("소집 경보! 무대 입구로 모이세요 — 15초", false), Does.Contain("15s"));
            Assert.That(table.Display("설정", true), Is.EqualTo("설정"));
            Assert.That(table.Display("A1B2C3", false), Is.EqualTo("A1B2C3"));
        }

        [Test]
        public void CsvAcceptsQuotedNewlinesQuotesAndBom()
        {
            var t = TextCatalog.Read("\uFEFFkey,source,ko,en\r\nsample,표지,표지,\"Exit, \"\"left\"\"\nnow\"\r\n");
            Assert.That(t.Get("sample", "", false), Is.EqualTo("Exit, \"left\"\nnow"));
        }

        const string Original = "key,source,ko,en\nshop,상점,상점,Shop\nexit,종료,종료,Quit\n";

        [Test]
        public void SectionImportsOnlyWriteTheChangedFilesAndAcceptOlderCombinedCsv()
        {
            var local = new Dictionary<string, string> {
                ["상점"] = "key,source,ko,en\nshop,상점,상점,Shop\n",
                ["로비"] = "key,source,ko,en\nexit,종료,종료,Quit\n" };
            string baseline = Baseline(Original);
            var incoming = new Dictionary<string, string>(local) { ["상점"] = local["상점"].Replace("Shop","Store") };
            var changes = TextCatalog.MergeSections(local, incoming, baseline, out var next, out int changed);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(changes.Keys, Is.EquivalentTo(new[] { "상점" }));
            local["상점"] = changes["상점"];
            var older = new[] { new KeyValuePair<string,string>(null, Original.Replace("Shop","Store")) };
            Assert.That(TextCatalog.MergeSections(local, older, next, out var again, out changed), Is.Empty);
            Assert.That(again, Is.EqualTo(next));
            Assert.That(changed, Is.Zero);
            incoming["로비"] = incoming["상점"];
            Assert.Throws<InvalidDataException>(() => TextCatalog.MergeSections(local, incoming, next, out _, out _));
            Assert.Throws<InvalidDataException>(() => TextCatalog.ReadFiles(new[] { Original, Original }));
        }

        [Test]
        public void EveryShippingSectionMatchesItsPublishedBaseline()
        {
            var files = TranslationTableEditor.ReadLocal();
            Assert.That(files.Count, Is.GreaterThan(1));
            string baseline = File.ReadAllText("Scripts/Localization/sheet-baseline.tsv");
            var changes = TextCatalog.MergeSections(files, files, baseline, out var next, out int changed, GameLanguage.LoadKeys());
            Assert.That(changed, Is.Zero);
            Assert.That(changes, Is.Empty);
            Assert.That(next, Is.EqualTo(baseline));
            Assert.That(GameLanguage.LoadCatalog().Count, Is.EqualTo(files.Values.Sum(v => TextCatalog.Read(v).Count)));
        }

        [Test]
        public void AmbiguousLegacyPromptsKeepTheirPriorityAcrossFiles()
        {
            const string first = "key,source,ko,en\nitem,{item} 안내,{item} 안내,Item {item}";
            const string second = "key,source,ko,en\ntool,{tool} 안내,{tool} 안내,Tool {tool}";
            var order = new[] { "tool", "item" };
            foreach (var files in new[] { new[] { first, second }, new[] { second, first } })
                Assert.That(TextCatalog.ReadFiles(files, order).Display("상자 안내", false), Is.EqualTo("Tool 상자"));
        }

        [Test]
        public void SheetNotesAreIgnoredForBothImportAndChangeDetection()
        {
            const string sheet = "key,key description,source,ko,en\nshop,\"기획 메모,\n{번역 안 함}\",상점,상점,Shop\nexit,,종료,종료,Quit";
            string baseline = Baseline(Original);
            Assert.That(TextCatalog.MergeEdits(Original, sheet, baseline, out var next, out int changed), Is.EqualTo(Original));
            Assert.That(changed, Is.Zero);
            Assert.That(next, Is.EqualTo(baseline));
            Assert.That(TextCatalog.MergeEdits(Original, sheet.Replace("Quit", "Leave"), next, out _, out changed), Is.EqualTo(Original.Replace("Quit", "Leave")));
            Assert.That(changed, Is.EqualTo(1));
            Assert.Throws<InvalidDataException>(() => TextCatalog.Read(sheet));
            Assert.Throws<InvalidDataException>(() => TextCatalog.MergeEdits(Original, sheet.Replace(",상점,Shop", ",상점,{bad}"), out _));
        }

        [Test]
        public void LocalBindingsRenderInEitherLanguageAndRespectRebinding()
        {
            var table = GameLanguage.LoadCatalog();
            Assert.That(table.Get("SETTINGS_INPUT_TURN-AROUND", null, false), Is.EqualTo("Wheel click"));
            Assert.That(table.Get("REHEARSAL_OBJECTIVE_CROSS-LANE", null, true), Does.Contain("[좌클릭/우클릭]"));
            var guide = UI.SceneIntegration.PerformanceHudProfile.FormatKeys("[{side}]로 옆 줄 건너기", null);
            Assert.That(table.Display(guide, true), Does.Contain("좌클릭/우클릭"));
            Assert.That(table.Display(guide, false), Does.Contain("Left click/Right-click"));
            var input = WaitingRoom.WaitingRoomTutorialBook.Format("[{uv}]", "F", "Tab");
            Assert.That(table.Display(input, false), Is.EqualTo("[Tab]"));
            var legacy = UI.SceneIntegration.CommonInteractionUI.FormatControlLabels("F / Space", "Space", "우클릭");
            Assert.That(table.Display(legacy, false), Is.EqualTo("Space / Right-click"));
        }

        [Test]
        public void NestedTooltipTranslatesBeforeFillingMultilineCopyAndKeepsPlayerNames()
        {
            var table = GameLanguage.LoadCatalog();
            string tip = WaitingRoom.ItemCatalog.Tooltip("spring-shoes");
            string bound = WaitingRoom.WaitingRoomText.Fill(tip, new (string, object)[] { ("slot1", "R"), ("slot2", "휠 클릭") });
            string template = "{description}\n가격: {price}\n{reason}";
            string filled = WaitingRoom.WaitingRoomText.Fill(template, new (string, object)[] {
                ("reason", "공동 자금 부족"), ("price", 7), ("description", bound) });
            Assert.That(table.Display(filled, false), Does.StartWith("Jump once more in midair.\n").And.Contain("slot 1 R · slot 2 Wheel click").And.Contain("Price: 7"));
            Assert.That(table.Display(filled, true), Is.EqualTo(filled));
            var sameText = WaitingRoom.WaitingRoomText.Fill("{player}", new (string, object)[] { ("player", "구조 장갑") });
            Assert.That(table.Display(sameText, false), Is.EqualTo("구조 장갑"));
            var item = WaitingRoom.WaitingRoomText.Fill("{item:을} 줍기", new (string, object)[] { ("item", "구조 장갑") });
            Assert.That(table.Display(item, true), Is.EqualTo("구조 장갑을 줍기"));
        }

        static string Baseline(string csv)
        {
            TextCatalog.MergeEdits(csv, csv, "", out var state, out _);
            return state;
        }

        [Test]
        public void QualifiedReferencesReachTheEditedRowAndPreserveLiveValues()
        {
            const string header = "key,source,ko,en\n";
            const string side = "SETTINGS_INPUT_SIDE-STEP";
            const string target = "REHEARSAL_OBJECTIVE_CROSS-LANE";
            const string button = "SETTINGS_BUTTON_MOUSE_LEFT";
            string value = "{" + CatalogKeys.CurrentValue + "}";
            var schema = new CatalogKeys("old.quest\t" + target, "side\t" + side);
            var files = new Dictionary<string,string> {
                ["COMMON"] = header + CatalogKeys.CurrentValue + ",실제 값,실제 값,Value\n",
                ["SETTINGS"] = header + side + "," + value + "," + value + "," + value + "\n" +
                    button + ",좌클릭,좌클릭,Left click\n",
                ["REHEARSAL"] = header + target + ",건너기 {"+side+"},건너기 {"+side+"},Cross {"+side+"}\n"
            };
            var input = new Dictionary<string,string>(files) {
                ["SETTINGS"] = files["SETTINGS"].Replace("," + value + "\n", ",Button " + value + "\n")
            };
            var edit = TextCatalog.MergeSections(files, input, "", out _, out int changed, schema);
            Assert.That(changed, Is.EqualTo(1));
            files["SETTINGS"] = edit["SETTINGS"];
            var catalog = TextCatalog.ReadSections(files, null, schema, _ => "좌클릭");
            Assert.That(catalog.Get("old.quest", null, false), Is.EqualTo("Cross Button Left click"));
            var bound = WaitingRoom.WaitingRoomText.Fill("건너기 {side}", new (string,object)[] { ("side", "R") });
            Assert.That(catalog.Display(bound, false), Is.EqualTo("Cross Button R"));
            var authored = WaitingRoom.WaitingRoomText.Fill("건너기 {" + side + "}", new (string,object)[] { (side, "Z") });
            Assert.That(catalog.Display(authored, false), Is.EqualTo("Cross Button Z"));
            var old = new[] { new KeyValuePair<string,string>(null, header + "old.quest,건너기 {side},건너기 {side},Cross {side}\n") };
            Assert.That(TextCatalog.MergeSections(files, old, "", out _, out changed, schema), Is.Empty);
            foreach (string bad in new[] { "side", "없는탭.버튼.없음", target })
            {
                input = new Dictionary<string,string> { ["REHEARSAL"] = files["REHEARSAL"].Replace("Cross ", "{" + bad + "} ") };
                Assert.Throws<InvalidDataException>(() => TextCatalog.MergeSections(files, input, "", out _, out _, schema));
            }
            files["REHEARSAL"] = files["REHEARSAL"].Replace("Cross ", "{" + button + "} ");
            Assert.That(TextCatalog.ReadSections(files, null, schema, _ => "R").Get(target, null, false), Is.EqualTo("Left click Button R"));
            files["잘못된탭"] = files["REHEARSAL"];
            Assert.Throws<InvalidDataException>(() => TextCatalog.ReadSections(files, null, schema));
        }

        [Test]
        public void VariantNumbersAndDepthAreCheckedAcrossTheWholeTab()
        {
            const string header = "key,source,ko,en\n";
            var schema = new CatalogKeys("", "");
            Dictionary<string,string> Tab(params string[] names) => new Dictionary<string,string> {
                ["SHOP"] = header + string.Join("\n", names.Select(k => k + ",물건,물건,Item"))
            };
            var good = Tab("SHOP_TEXT_SALE_1", "SHOP_TEXT_SALE_0", "SHOP_BUTTON_MOUSE_LEFT");
            Assert.That(TextCatalog.ReadSections(good, null, schema).Count, Is.EqualTo(3));
            foreach (var bad in new[] { Tab("SHOP_TEXT_SALE_1"), Tab("SHOP_TEXT_SALE_0","SHOP_TEXT_SALE_2"),
                Tab("SHOP_TEXT_SALE","SHOP_TEXT_SALE_0","SHOP_TEXT_SALE_1"), Tab("SHOP_TEXT_SALE_00"),
                Tab("SHOP_A_B_C_D"), Tab("SHOP_TEXT_ITEM2"), Tab("SHOP_TEXT_BAD NAME"), Tab("Shop_TEXT_NAME"),
                Tab("SHOP_TEXT_품목"), Tab("STORE_TEXT_NAME") })
                Assert.Throws<InvalidDataException>(() => TextCatalog.MergeSections(bad, bad, "", out _, out _, schema));
        }

        [Test]
        public void PreviousKoreanSheetRowsAndParticlesResolveToTheSameCurrentRows()
        {
            const string item = "SHOP_VALUE_ITEM-NAME", pickup = "SHOP_PROMPT_PICK-UP", old = "상점·기물.값.기물이름";
            const string header = "key,source,ko,en\n";
            var schema = new CatalogKeys(old + "\t" + item + "\n상점·기물.안내.줍기\t" + pickup, "item\t" + item);
            var source = "{" + item + "}(을/를) 줍기";
            var current = "{" + CatalogKeys.CurrentValue + "}";
            var files = new Dictionary<string,string> {
                ["COMMON"] = header + CatalogKeys.CurrentValue + ",값,값,Value",
                ["SHOP"] = header + item + "," + current + "," + current + "," + current + "\n" +
                    pickup + "," + source + "," + source + ",Pick up {" + item + "}"
            };
            string previous = "{" + old + ":을} 줍기";
            var sheet = new Dictionary<string,string> { ["SHOP"] = header + "상점·기물.안내.줍기," + previous + "," + previous + ",Pick up {" + old + "}" };
            Assert.That(TextCatalog.MergeSections(files, sheet, "", out _, out int changed, schema), Is.Empty);
            Assert.That(changed, Is.Zero);
            var table = TextCatalog.ReadSections(files, null, schema);
            foreach (var pair in new[] { ("상자","상자를 줍기"), ("장갑","장갑을 줍기") })
            {
                var value = WaitingRoom.WaitingRoomText.Fill("{item:을} 줍기", new (string,object)[] { ("item",pair.Item1) });
                Assert.That(table.Display(value, true), Is.EqualTo(pair.Item2));
                Assert.That(table.Display(value, false), Is.EqualTo("Pick up " + pair.Item1));
            }
        }

        [Test]
        public void FullSheetSkipsUnchangedRowsAndPreservesLocalChangesAndFormatting()
        {
            const string start = "key,source,ko,en\n\"exit\",종료,종료,Quit\nshop,상점,상점,Shop";
            string local = start.Replace("Quit", "Leave");
            const string sheet = "key,source,ko,en\r\nshop,상점,상점,Store\r\nexit,종료,종료,Quit";
            string merged = TextCatalog.MergeEdits(local, sheet, Baseline(start), out var next, out int changed);
            Assert.That(merged, Is.EqualTo(local.Replace("Shop", "Store")));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(TextCatalog.MergeEdits(merged, sheet, next, out var again, out changed), Is.EqualTo(merged));
            Assert.That(changed, Is.Zero);
            Assert.That(again, Is.EqualTo(next));
        }

        [Test]
        public void OnlyChangedRowsUndergoTranslationChecks()
        {
            string legacy = Original.Replace("종료,Quit", "종료,#REF!");
            string sheet = legacy.Replace("Shop", "Store");
            Assert.That(TextCatalog.MergeEdits(legacy, sheet, Baseline(legacy), out _, out _), Is.EqualTo(sheet));
            Assert.Throws<InvalidDataException>(() =>
                TextCatalog.MergeEdits(legacy, sheet.Replace("Store", "{missing}"), Baseline(legacy), out _, out _));
        }

        [Test]
        public void RejectsCompetingEditsAndRecoversIfCsvWasAlreadyInstalled()
        {
            string baseline = Baseline(Original);
            string remote = Original.Replace("Shop", "Store");
            Assert.Throws<InvalidDataException>(() =>
                TextCatalog.MergeEdits(Original.Replace("Shop", "Market"), remote, baseline, out _, out _));
            Assert.That(TextCatalog.MergeEdits(remote, remote, baseline, out var next, out int changed), Is.EqualTo(remote));
            Assert.That(changed, Is.Zero);
            Assert.That(next, Is.EqualTo(Baseline(remote)));
        }

        [Test]
        public void CompleteGameSheetCanImportOneEditAndThenRepeatWithoutChanges()
        {
            string csv = File.ReadAllText(Path.Combine(TranslationTableEditor.Folder, "SETTINGS.csv"));
            string saved = File.ReadAllText("Scripts/Localization/sheet-baseline.tsv");
            Assert.That(TextCatalog.MergeEdits(csv, csv, saved, out var aligned, out int changed), Is.EqualTo(csv));
            Assert.That(changed, Is.Zero);
            string key = "SETTINGS_OPTION_LANGUAGE";
            string value = TextCatalog.Read(csv).Get(key, null, false);
            Assert.That(value, Is.Not.Null);
            string single = "key,source,ko,en\n" + key + ",언어,언어,\"" + value.Replace("\"", "\"\"") + "!\"";
            string full = TextCatalog.MergeEdits(csv, single, out changed);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(TextCatalog.MergeEdits(csv, full, aligned, out var installed, out changed), Is.EqualTo(full));
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(TextCatalog.MergeEdits(full, full, installed, out _, out changed), Is.EqualTo(full));
            Assert.That(changed, Is.Zero);
        }

        [Test]
        public void SheetEditsUpdateOnlyMatchingRowsAndCanBeImportedTwice()
        {
            const string edit = "key,source,ko,en\nshop,상점,상점,Store";
            string merged = TextCatalog.MergeEdits(Original, edit, out int changed);
            Assert.That(changed, Is.EqualTo(1));
            Assert.That(TextCatalog.Read(merged).Display("상점", false), Is.EqualTo("Store"));
            Assert.That(TextCatalog.Read(merged).Display("종료", false), Is.EqualTo("Quit"));
            Assert.That(TextCatalog.MergeEdits(merged, edit, out changed), Is.EqualTo(merged));
            Assert.That(changed, Is.Zero);
        }

        [TestCase("key,ko,en\nshop,상점,Store")]
        [TestCase("key,source,ko,en\nshops,상점,상점,Store")]
        [TestCase("key,source,ko,en\nshop,변경,상점,Store")]
        [TestCase("key,source,ko,en\nshop,상점,상점,<b>Store</b>")]
        public void BadSheetEditsAreRejectedBeforeSaving(string edit) =>
            Assert.Throws<InvalidDataException>(() => TextCatalog.MergeEdits(Original, edit, out _));

        [TestCase("x,소품,소품,Item\nx,문,문,Door")]
        [TestCase("x,소품 {n},소품 {n},Item")]
        [TestCase("x,소품,소품,")]
        [TestCase("x,소품,소품,\"Unfinished")]
        [TestCase("x,소품,소품,#REF!")]
        public void RejectsInvalidCatalog(string rows)
        {
            Assert.Throws<InvalidDataException>(() => TextCatalog.Read("key,source,ko,en\n" + rows));
        }

        [TestCase("ko-KR", SystemLanguage.English, "ko")]
        [TestCase("en_US", SystemLanguage.Korean, "en")]
        [TestCase("auto", SystemLanguage.Korean, "ko")]
        [TestCase("fr-FR", SystemLanguage.French, "en")]
        public void ResolvesLanguage(string code, SystemLanguage device, string expected) =>
            Assert.That(GameLanguage.Resolve(code, device), Is.EqualTo(expected));
    }
}

