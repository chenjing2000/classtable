using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ClassTimetable.Core;

public sealed record StudentSnapshot(string Name, StudentDocument Document, string Fingerprint);
public sealed record StudentScan(List<StudentSnapshot> Students, List<string> Errors);

public sealed class StudentFileStore(string directory)
{
    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    private string FilePath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.EndsWith('.') || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            name.Contains('/') || name.Contains('\\') || name.Length > 80 ||
            Regex.IsMatch(name, @"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)", RegexOptions.IgnoreCase))
            throw new InvalidDataException("姓名不能包含路径、特殊文件名字符、尾随空格/点或Windows保留名，长度最多80字。");
        return Path.Combine(DirectoryPath, name + ".json");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public StudentSnapshot Load(string name)
    {
        var bytes = File.ReadAllBytes(FilePath(name));
        var document = DocumentJson.Read(bytes);
        TimetableValidator.Validate(document);
        return new(name, document, Hash(bytes));
    }
    public StudentScan Scan()
    {
        var result = new StudentScan([], []);
        if (!Directory.Exists(DirectoryPath)) return result;
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*.json").Order(StringComparer.CurrentCultureIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            try { result.Students.Add(Load(name)); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or ArgumentException)
            { result.Errors.Add($"{Path.GetFileName(path)}：{ex.Message}"); }
        }
        return result;
    }
    public StudentSnapshot Save(string name, StudentDocument document, string? expectedFingerprint)
    {
        var target = FilePath(name);
        TimetableValidator.Validate(document);
        Directory.CreateDirectory(DirectoryPath);
        void CheckCurrent()
        {
            if (expectedFingerprint is null)
            {
                if (Directory.EnumerateFiles(DirectoryPath, "*.json").Any(p => string.Equals(p, target, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidDataException("已有同名学生，请使用其他姓名。");
            }
            else
            {
                if (!File.Exists(target) || Hash(File.ReadAllBytes(target)) != expectedFingerprint)
                    throw new InvalidDataException("文件已被外部修改或删除，请保留编辑内容并先刷新后重新编辑。");
                var current = Load(name).Document;
                if (current.Courses.Count > 0 && current.Settings.ClassesUnit != document.Settings.ClassesUnit)
                    throw new InvalidDataException("已有课程时不能更改课程模式，请先删除全部课程并保存。");
            }
        }
        CheckCurrent();
        var bytes = DocumentJson.Write(document);
        var temp = Path.Combine(DirectoryPath, "." + Guid.NewGuid().ToString("N") + ".tmp");
        var created = false;
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { created = true; stream.Write(bytes); stream.Flush(true); }
            CheckCurrent();
            if (expectedFingerprint is null) File.Move(temp, target, false);
            else File.Replace(temp, target, Path.ChangeExtension(target, ".bak"), true);
        }
        finally { if (created && File.Exists(temp)) File.Delete(temp); }
        return new(name, document.Clone(), Hash(bytes));
    }
}
