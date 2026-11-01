using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ClassSchedule.Core;

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
    private FileStream OpenCurrent(StudentSnapshot expected)
    {
        var stream = new FileStream(FilePath(expected.Name), FileMode.Open, FileAccess.Read, FileShare.Delete);
        try
        {
            if (Convert.ToHexString(SHA256.HashData(stream)) != expected.Fingerprint)
                throw new InvalidDataException("文件已被外部修改，请更新学生信息后重试。");
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }
    public StudentSnapshot Rename(StudentSnapshot expected, string newName)
    {
        var source = FilePath(expected.Name); var target = FilePath(newName);
        using var current = OpenCurrent(expected);
        if (expected.Name == newName) return expected;
        var backup = Path.ChangeExtension(source, ".bak"); var targetBackup = Path.ChangeExtension(target, ".bak");
        var caseOnly = string.Equals(source, target, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && (File.Exists(target) || File.Exists(targetBackup)))
            throw new InvalidDataException("已有同名学生或备份，请使用其他姓名。");
        using var backupLock = File.Exists(backup) ? new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Delete) : null;
        // Two moves are needed even for a case-only rename on Windows.
        var temporary = Path.Combine(DirectoryPath, "." + Guid.NewGuid().ToString("N") + ".tmp");
        File.Move(source, temporary);
        try
        {
            File.Move(temporary, target);
            try { if (backupLock is not null) File.Move(backup, targetBackup); }
            catch { File.Move(target, source); throw; }
        }
        catch { if (File.Exists(temporary)) File.Move(temporary, source); throw; }
        return expected with { Name = newName };
    }
    public void Delete(StudentSnapshot expected)
    {
        var target = FilePath(expected.Name); var backup = Path.ChangeExtension(target, ".bak");
        using var current = OpenCurrent(expected);
        using var backupLock = File.Exists(backup) ? new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.Delete) : null;
        if (backupLock is not null && (File.GetAttributes(backup) & FileAttributes.ReadOnly) != 0)
            throw new UnauthorizedAccessException("学生备份为只读，未删除任何文件。");
        // Hold the backup under a temporary name until deleting the student succeeds.
        // A locked/readonly main file must not cost the user their backup.
        var heldBackup = Path.Combine(DirectoryPath, "." + Guid.NewGuid().ToString("N") + ".bak");
        if (backupLock is not null) File.Move(backup, heldBackup);
        try { File.Delete(target); }
        catch { if (backupLock is not null) File.Move(heldBackup, backup); throw; }
        if (backupLock is not null) File.Delete(heldBackup);
    }
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
