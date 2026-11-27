using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts.Heroes;
using Assets.Scripts.IO;
using Assets.Scripts.MainMenu;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>The storage seam, the safe write, the save format version and export/import.</summary>
    public class SaveStorageTests
    {
        /// <summary>A save folder in memory, so format and transfer rules test without a disk.</summary>
        private sealed class MemoryStorage : ISaveStorage
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
            public int Flushes;

            public bool Exists(string path) { return Files.ContainsKey(path); }
            public string Read(string path) { return Files.TryGetValue(path, out var text) ? text : null; }
            public void Write(string path, string text) { Files[path] = text; if (_deferred > 0) { WritesWhileDeferred++; } }
            public void Delete(string path) { Files.Remove(path); }
            public void Flush() { Flushes++; }

            private int _deferred;
            public IDisposable DeferFlush()
            {
                _deferred++;
                return new Scope(() => { _deferred--; if (_deferred == 0) { Flush(); } });
            }

            /// <summary>Writes made while a flush was deferred.</summary>
            public int WritesWhileDeferred;

            private sealed class Scope : IDisposable
            {
                private Action _end;
                public Scope(Action end) { _end = end; }
                public void Dispose() { _end?.Invoke(); _end = null; }
            }

            public string[] List(string directory, string searchPattern)
            {
                var suffix = searchPattern.Substring(searchPattern.IndexOf('*') + 1);
                var prefix = searchPattern.Substring(0, searchPattern.IndexOf('*'));
                return Files.Keys
                    .Where(k => k.StartsWith(directory + "/") && k.IndexOf('/', directory.Length + 1) < 0)
                    .Where(k => Path.GetFileName(k).StartsWith(prefix) && k.EndsWith(suffix))
                    .ToArray();
            }
        }

        private const string Dir = "mem/savedata";

        [SetUp]
        public void SetUp()
        {
            SaveFormat.Forget();
        }

        [TearDown]
        public void TearDown()
        {
            SaveFormat.Forget();
        }

        // --- safe write --------------------------------------------------------

        [Test]
        public void LocalFileStorage_Write_ReplacesFileAndLeavesNoTemp()
        {
            var dir = TempDir();
            try
            {
                var storage = new LocalFileStorage();
                var path = $"{dir}/Meta.json";
                storage.Write(path, "one");
                storage.Write(path, "two");

                Assert.AreEqual("two", File.ReadAllText(path));
                Assert.IsFalse(File.Exists(path + LocalFileStorage.TempSuffix));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void LocalFileStorage_Read_RecoversFinishedTempWhenFileIsMissing()
        {
            var dir = TempDir();
            try
            {
                var path = $"{dir}/Meta.json";
                File.WriteAllText(path + LocalFileStorage.TempSuffix, "new");

                Assert.AreEqual("new", new LocalFileStorage().Read(path));
                Assert.IsTrue(File.Exists(path));
                Assert.IsFalse(File.Exists(path + LocalFileStorage.TempSuffix));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void LocalFileStorage_Read_PrefersRealFileOverLeftoverTemp()
        {
            var dir = TempDir();
            try
            {
                var path = $"{dir}/Meta.json";
                File.WriteAllText(path, "complete");
                File.WriteAllText(path + LocalFileStorage.TempSuffix, "half");

                Assert.AreEqual("complete", new LocalFileStorage().Read(path));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void LocalFileStorage_List_SkipsTempFiles()
        {
            var dir = TempDir();
            try
            {
                File.WriteAllText($"{dir}/Meta.json", "a");
                File.WriteAllText($"{dir}/Party.json.tmp", "b");

                var found = new LocalFileStorage().List(dir, "*.json");
                Assert.AreEqual(1, found.Length);
                StringAssert.EndsWith("Meta.json", found[0]);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        // --- format version -----------------------------------------------------

        [Test]
        public void FileHandler_Save_WritesCurrentFormatVersion()
        {
            var storage = new MemoryStorage();
            new FileHandler(Dir, storage).Save(new PartySaveData());

            var info = SaveFormat.ReadInfo(storage, Dir);
            Assert.IsNotNull(info);
            Assert.AreEqual(SaveFormat.CurrentVersion, info.FormatVersion);
        }

        [Test]
        public void FileHandler_Load_OnEmptyFolder_WritesNothing()
        {
            var storage = new MemoryStorage();
            new FileHandler(Dir, storage).Load<PartySaveData>();

            Assert.AreEqual(0, storage.Files.Count);
        }

        [Test]
        public void FileHandler_Load_OnOldFolder_RecordsCurrentVersion()
        {
            var storage = new MemoryStorage();
            storage.Write($"{Dir}/Party.json", JsonUtility.ToJson(new PartySaveData()));

            new FileHandler(Dir, storage).Load<PartySaveData>();

            Assert.AreEqual(SaveFormat.CurrentVersion, SaveFormat.ReadInfo(storage, Dir).FormatVersion);
        }

        [Test]
        public void FileHandler_Load_OnNewerFolder_LeavesVersionAlone()
        {
            var storage = new MemoryStorage();
            var newer = new SaveInfoData { FormatVersion = SaveFormat.CurrentVersion + 1 };
            storage.Write(SaveFormat.InfoPath(Dir), JsonUtility.ToJson(newer));
            storage.Write($"{Dir}/Party.json", JsonUtility.ToJson(new PartySaveData()));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer build"));
            new FileHandler(Dir, storage).Load<PartySaveData>();

            Assert.AreEqual(SaveFormat.CurrentVersion + 1, SaveFormat.ReadInfo(storage, Dir).FormatVersion);
        }

        [Test]
        public void FileHandler_Save_IntoNewerFolder_IsRefused()
        {
            var storage = new MemoryStorage();
            var newer = new SaveInfoData { FormatVersion = SaveFormat.CurrentVersion + 1 };
            storage.Write(SaveFormat.InfoPath(Dir), JsonUtility.ToJson(newer));
            var original = JsonUtility.ToJson(new PartySaveData { OwnedHeroKeys = { "FromTheFuture" } });
            storage.Write($"{Dir}/Party.json", original);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("newer build"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Not saving Party"));
            new FileHandler(Dir, storage).Save(new PartySaveData());

            Assert.AreEqual(original, storage.Read($"{Dir}/Party.json"));
        }

        [Test]
        public void SaveFormat_DamagedInfo_CountsAsVersionZeroAndIsRewritten()
        {
            var storage = new MemoryStorage();
            storage.Write(SaveFormat.InfoPath(Dir), "{\"FormatVersion\":");
            storage.Write($"{Dir}/Party.json", JsonUtility.ToJson(new PartySaveData()));

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("unreadable"));
            Assert.DoesNotThrow(() => new FileHandler(Dir, storage).Load<PartySaveData>());

            Assert.AreEqual(SaveFormat.CurrentVersion, SaveFormat.ReadInfo(storage, Dir).FormatVersion);
        }

        // --- export / import ---------------------------------------------------

        [Test]
        public void SaveTransfer_ExportThenImport_RestoresEveryFile()
        {
            var source = new MemoryStorage();
            var handler = new FileHandler(Dir, source);
            handler.Save(new PartySaveData { OwnedHeroKeys = { "Rogue" } });
            source.Write($"{Dir}/Dungeon_-42.json", "{\"Seed\":-42}");

            var text = SaveTransfer.Export(source, Dir);

            var target = new MemoryStorage();
            target.Write($"{Dir}/Meta.json", "{\"Gold\":999}");
            Assert.AreEqual(SaveImportCheck.Ok, SaveTransfer.Parse(text, out var bundle));
            SaveTransfer.Import(target, Dir, bundle);

            Assert.IsFalse(target.Exists($"{Dir}/Meta.json"), "an import replaces every save file");
            Assert.AreEqual(source.Read($"{Dir}/Party.json"), target.Read($"{Dir}/Party.json"));
            Assert.AreEqual("{\"Seed\":-42}", target.Read($"{Dir}/Dungeon_-42.json"));
            Assert.AreEqual(SaveFormat.CurrentVersion, SaveFormat.ReadInfo(target, Dir).FormatVersion);
            Assert.AreEqual(1, target.Flushes, "one sync for the whole import, at the end");
            Assert.Greater(target.WritesWhileDeferred, 0);
        }

        [Test]
        public void SaveTransfer_Import_BacksUpTheReplacedSave()
        {
            var target = new MemoryStorage();
            target.Write($"{Dir}/Meta.json", "{\"Gold\":999}");
            var bundle = Bundle(("Party", "{}"));

            SaveTransfer.Import(target, Dir, bundle);

            Assert.AreEqual(SaveImportCheck.Ok, SaveTransfer.Parse(target.Read(SaveTransfer.BackupPath(Dir)), out var backup));
            Assert.AreEqual("{\"Gold\":999}", backup.Files.Single(f => f.Name == "Meta").Json);
        }

        [Test]
        public void SaveTransfer_DeviceFiles_StayOnTheDevice()
        {
            var source = new MemoryStorage();
            source.Write($"{Dir}/Audio.json", "{\"Master\":0.1}");
            source.Write($"{Dir}/Display.json", "{\"InterfaceSize\":150}");
            source.Write($"{Dir}/Party.json", "{}");
            var exported = SaveTransfer.Collect(source, Dir);
            CollectionAssert.AreEqual(new[] { "Party" }, exported.Files.Select(f => f.Name).ToArray());

            var target = new MemoryStorage();
            target.Write($"{Dir}/Display.json", "{\"InterfaceSize\":100}");
            var bundle = Bundle(("Party", "{}"), ("Display", "{\"InterfaceSize\":150}"));
            Assert.AreEqual(SaveImportCheck.Ok, SaveTransfer.Parse(JsonUtility.ToJson(bundle), out var parsed));
            SaveTransfer.Import(target, Dir, parsed);

            Assert.AreEqual("{\"InterfaceSize\":100}", target.Read($"{Dir}/Display.json"));
        }

        [Test]
        public void SaveTransfer_Collect_UnversionedFolderTravelsAsVersionZero()
        {
            var storage = new MemoryStorage();
            storage.Write($"{Dir}/Party.json", "{}");

            Assert.AreEqual(0, SaveTransfer.Collect(storage, Dir).FormatVersion);
        }

        [Test]
        public void SaveTransfer_Parse_RejectsDamagedInnerFile()
        {
            var bundle = Bundle(("Party", "{}"), ("Meta", "{\"Gold\":"));

            Assert.AreEqual(SaveImportCheck.NotASave, SaveTransfer.Parse(JsonUtility.ToJson(bundle), out _));
        }

        [TestCase("CON")]
        [TestCase("com1")]
        [TestCase("Méta")]
        public void SaveTransfer_IsSafeName_RejectsReservedAndNonAscii(string name)
        {
            Assert.IsFalse(SaveTransfer.IsSafeName(name));
        }

        private static SaveBundle Bundle(params (string name, string json)[] files)
        {
            var bundle = new SaveBundle { Game = SaveBundle.GameTag, FormatVersion = SaveFormat.CurrentVersion };
            foreach (var (name, json) in files)
            {
                bundle.Files.Add(new SaveBundleFile { Name = name, Json = json });
            }
            return bundle;
        }

        [Test]
        public void SaveTransfer_Export_LeavesSaveInfoOutOfTheFiles()
        {
            var storage = new MemoryStorage();
            new FileHandler(Dir, storage).Save(new PartySaveData());

            var bundle = SaveTransfer.Collect(storage, Dir);
            CollectionAssert.AreEqual(new[] { "Party" }, bundle.Files.Select(f => f.Name).ToArray());
        }

        [TestCase("")]
        [TestCase("not json at all")]
        [TestCase("{\"Gold\":5}")]
        public void SaveTransfer_Parse_RejectsWhatIsNotABundle(string text)
        {
            Assert.AreEqual(SaveImportCheck.NotASave, SaveTransfer.Parse(text, out var bundle));
            Assert.IsNull(bundle);
        }

        [Test]
        public void SaveTransfer_Parse_RejectsNewerFormat()
        {
            var bundle = new SaveBundle { Game = SaveBundle.GameTag, FormatVersion = SaveFormat.CurrentVersion + 1 };
            bundle.Files.Add(new SaveBundleFile { Name = "Party", Json = "{}" });

            Assert.AreEqual(SaveImportCheck.TooNew, SaveTransfer.Parse(JsonUtility.ToJson(bundle), out _));
        }

        [Test]
        public void SaveTransfer_Parse_DropsUnsafeNamesAndReportsEmpty()
        {
            var bundle = new SaveBundle { Game = SaveBundle.GameTag, FormatVersion = 1 };
            bundle.Files.Add(new SaveBundleFile { Name = "../evil", Json = "{}" });
            bundle.Files.Add(new SaveBundleFile { Name = "a/b", Json = "{}" });

            Assert.AreEqual(SaveImportCheck.Empty, SaveTransfer.Parse(JsonUtility.ToJson(bundle), out _));
        }

        // --- interface size ----------------------------------------------------

        [Test]
        public void InterfaceScale_Clamp_UnknownSizeBecomesDeviceDefault()
        {
            Assert.AreEqual(InterfaceScale.DesktopDefault, InterfaceScale.Clamp(0, mobile: false));
            Assert.AreEqual(InterfaceScale.MobileDefault, InterfaceScale.Clamp(0, mobile: true));
            Assert.AreEqual(115, InterfaceScale.Clamp(115, mobile: true));
        }

        [Test]
        public void InterfaceScale_Stepped_MovesBothWaysAndStopsAtTheEnds()
        {
            Assert.AreEqual(115, InterfaceScale.Stepped(100, 1));
            Assert.AreEqual(115, InterfaceScale.Stepped(130, -1));
            Assert.AreEqual(150, InterfaceScale.Stepped(150, 1), "no wrap past the largest");
            Assert.AreEqual(100, InterfaceScale.Stepped(100, -1), "no wrap past the smallest");
            Assert.AreEqual(100, InterfaceScale.Stepped(7, 1));
            Assert.IsFalse(InterfaceScale.CanStep(150, 1));
            Assert.IsTrue(InterfaceScale.CanStep(150, -1));
        }

        [Test]
        public void InterfaceScale_Scaled_ShrinksReferenceResolution()
        {
            Assert.AreEqual(new Vector2Int(1600, 900), InterfaceScale.Scaled(new Vector2Int(1600, 900), 1f));
            Assert.AreEqual(new Vector2Int(1231, 692), InterfaceScale.Scaled(new Vector2Int(1600, 900), 1.3f));
        }

        [Test]
        public void FitToScreen_Factor_ShrinksOnlyWhatDoesNotFit()
        {
            Assert.AreEqual(1f, ImmoralityGaming.Menu.FitToScreen.Factor(new Vector2(600, 400), new Vector2(1600, 900), 1f));
            Assert.AreEqual(0.5f, ImmoralityGaming.Menu.FitToScreen.Factor(new Vector2(1200, 600), new Vector2(600, 900), 1f), 1e-4f);
            Assert.AreEqual(1f, ImmoralityGaming.Menu.FitToScreen.Factor(Vector2.zero, new Vector2(600, 900), 1f));
        }

        private static string TempDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "cd-save-" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
