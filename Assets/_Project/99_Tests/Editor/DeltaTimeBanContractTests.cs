using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SweepNDodge.DotsBullets.Tests
{
    public class DeltaTimeBanContractTests
    {
        private const string SystemsRoot = "Assets/_Project/02_Scripts/ECS/Systems";
        private const string AllowToken = "DELTATIME_BAN_ALLOW";

        private static readonly Regex[] ForbiddenPatterns =
        {
            new Regex(@"\bSystemAPI\.Time\.DeltaTime\b", RegexOptions.Compiled),
            new Regex(@"\bTime\.deltaTime\b", RegexOptions.Compiled),
        };

        private static readonly HashSet<string> WhitelistedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 고정 Tick 시간원 공급자: 렌더 프레임 dt를 읽어 runtime에 반영하는 단일 진입점
            "FixedTickTimeSystems.cs",
            // 표현 계층(HUD): 표시용 프레임 타임 측정 허용
            "DebugHudAndStressSystems.cs",
        };

        [Test]
        public void DeltaTimeBan_NoUsageInLogicSystems()
        {
            string root = Path.Combine(GetProjectRoot(), SystemsRoot);
            Assert.That(Directory.Exists(root), Is.True, $"Missing directory: {root}");

            var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            var violations = new List<string>();

            foreach (var file in files)
            {
                if (IsWhitelisted(file))
                    continue;

                string[] lines = File.ReadAllLines(file, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line.Contains(AllowToken, StringComparison.Ordinal))
                        continue;

                    for (int p = 0; p < ForbiddenPatterns.Length; p++)
                    {
                        if (!ForbiddenPatterns[p].IsMatch(line))
                            continue;

                        string relative = Path.GetRelativePath(GetProjectRoot(), file).Replace('\\', '/');
                        violations.Add($"{relative}:{i + 1}: {line.Trim()}");
                        break;
                    }
                }
            }

            if (violations.Count <= 0)
                return;

            string detail = string.Join(Environment.NewLine, violations.OrderBy(v => v, StringComparer.Ordinal));
            Assert.Fail(
                "DeltaTime direct usage is forbidden in ECS logic systems. " +
                $"Use FixedTickTimeUtility/runtime instead. (allow token: {AllowToken}){Environment.NewLine}{detail}");
        }

        private static bool IsWhitelisted(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            if (WhitelistedFileNames.Contains(fileName))
                return true;

            string normalized = filePath.Replace('\\', '/');
            // 표현 계층 화이트리스트 확장점: Debug/Camera 관련 시스템은 필요 시 허용
            if (normalized.Contains("/Debug/", StringComparison.OrdinalIgnoreCase))
                return true;
            if (normalized.Contains("Camera", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string GetProjectRoot()
        {
            return Directory.GetCurrentDirectory();
        }
    }
}
