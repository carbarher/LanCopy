using System;
using System.IO;
using System.Linq;
using SlskDown.Services;
using Xunit;

namespace SlskDown.Tests
{
    public sealed class TxtRunnerSmokeTests
    {
        [Fact]
        public void NormalizeFileNameForDedupe_ShouldNormalizeAndCollapseSpaces()
        {
            var input = "  Foo-Bar__Baz.PDF ";
            var actual = TitleSearchFromTxtService.NormalizeFileNameForDedupe(input);
            Assert.Equal("foo bar baz pdf", actual);
        }

        [Fact]
        public void QuarantineService_ShouldCreateExtendedLogHeaderAndAppendExtendedRow()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "SlskDownTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var service = new QuarantineService(tempDir);
                service.EnsureInitialized();

                var logPath = Path.Combine(tempDir, "quarantine_log.csv");
                Assert.True(File.Exists(logPath));

                var lines = File.ReadAllLines(logPath);
                Assert.True(lines.Length >= 1);
                Assert.Equal("timestamp,action,file_name,original_path,new_path,detector,reason,size_bytes,username,author_group", lines[0]);

                service.AppendLog(
                    action: "move",
                    fileName: "test.txt",
                    originalPath: "c:/in/test.txt",
                    newPath: "c:/out/test.txt",
                    detector: "unit",
                    reason: "corrupted",
                    sizeBytes: 123,
                    username: "user",
                    authorGroup: "author");

                var lastLine = File.ReadAllLines(logPath).Last();
                var columns = lastLine.Split(',');
                Assert.True(columns.Length >= 10);
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                }
            }
        }
    }
}
