using System.Text.Json;
using DAQuester.Core;
using DAQuester.DarkAges.Diagnostics;
using Xunit;

namespace DAQuester.Tests;

public class ReplayExportMemoryTests
{
    [Fact]
    public void Large_replay_exports_without_allocating_a_complete_output_string()
    {
        var recorder = new ReplayRecorder();
        var snapshot = new CharacterSnapshot
        {
            Name = "Synthetic",
            Generation = 1,
            MapName = new string('m', 20000),
            Connected = true
        };
        for (int i = 1; i <= 160; i++)
            recorder.Record(0x04, [0, 1, 0, 1], snapshot with
            {
                Revision = i
            });
        string directory = Path.Combine(Path.GetTempPath(), "DAQuester-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "replay.json");
        try
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            recorder.Export(path);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            long size = new FileInfo(path).Length;
            Assert.True(allocated < size, $"Export allocated {allocated:N0} bytes for {size:N0} output bytes; it must stream the output.");
            using var stream = File.OpenRead(path);
            using var json = JsonDocument.Parse(stream);
            Assert.Equal(3, json.RootElement.GetProperty("SchemaVersion").GetInt32());
            Assert.True(json.RootElement.GetProperty("Sanitized").GetBoolean());
            Assert.Equal(160, json.RootElement.GetProperty("Events").GetArrayLength());
            Assert.Equal(snapshot.MapName, json.RootElement.GetProperty("Events")[159].GetProperty("Snapshot").GetProperty("MapName").GetString());
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }
}
