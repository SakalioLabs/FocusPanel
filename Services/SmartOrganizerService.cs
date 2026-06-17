using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FocusPanel.Services;

public class SmartOrganizerService
{
    private const int TimeGapThresholdHours = 4;

    public event Action<string> ProgressChanged;
    public event Action<int, int> ProgressUpdated;

    public async Task OrganizeByRelevance(string sourcePath)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(sourcePath)) return;

            var dirInfo = new DirectoryInfo(sourcePath);
            var files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly)
                               .Where(f => !f.Attributes.HasFlag(FileAttributes.Hidden))
                               .OrderBy(f => f.LastWriteTime)
                               .ToList();

            if (files.Count == 0) return;

            int total = files.Count;
            int processed = 0;

            // 1. Cluster by Time (Sessions)
            var sessions = new List<List<FileInfo>>();
            var currentSession = new List<FileInfo>();

            // Initial sort by Time ensures we can just iterate
            if (files.Count > 0) currentSession.Add(files[0]);

            for (int i = 1; i < files.Count; i++)
            {
                var file = files[i];
                var lastFile = currentSession.Last();
                var timeDiff = file.LastWriteTime - lastFile.LastWriteTime;

                // Heuristic: If gap > 4 hours OR date changed significantly, break session
                if (timeDiff.TotalHours > TimeGapThresholdHours)
                {
                    sessions.Add(new List<FileInfo>(currentSession));
                    currentSession.Clear();
                }
                currentSession.Add(file);

                if (i % 100 == 0)
                {
                    ProgressChanged?.Invoke($"Analyzing timeline... {i}/{total}");
                }
            }
            if (currentSession.Count > 0) sessions.Add(currentSession);

            // 2. Process each session
            // Within each session, we might have multiple "projects" mixed together if user worked on multiple things.
            // Let's refine the sessions by Name Similarity.
            
            var finalGroups = new List<(string Name, List<FileInfo> Files)>();

            foreach (var session in sessions)
            {
                // Sub-cluster by name similarity
                // Simple approach: Group by common prefix (first 5 chars?) or Extension?
                // Better: Use extension as primary grouper if no common name pattern found.
                
                // For now, let's keep it simple: One folder per "Work Session".
                // But try to find a meaningful name.
                
                string sessionName = GenerateSessionName(session);
                
                // Create folder
                string targetDir = Path.Combine(sourcePath, sessionName);
                if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                foreach (var file in session)
                {
                    processed++;
                    try
                    {
                        string targetPath = Path.Combine(targetDir, file.Name);
                        // Rename if exists
                        if (File.Exists(targetPath))
                        {
                            string name = Path.GetFileNameWithoutExtension(file.Name);
                            string ext = file.Extension;
                            int count = 1;
                            while (File.Exists(targetPath))
                            {
                                targetPath = Path.Combine(targetDir, $"{name} ({count++}){ext}");
                            }
                        }
                        file.MoveTo(targetPath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error moving {file.Name}: {ex.Message}");
                    }
                    
                    if (processed % 10 == 0)
                    {
                        ProgressUpdated?.Invoke(processed, total);
                        ProgressChanged?.Invoke($"Organizing: {processed}/{total}");
                    }
                }
            }
            
            ProgressChanged?.Invoke("Done!");
        });
    }

    private string GenerateSessionName(List<FileInfo> session)
    {
        if (session == null || session.Count == 0) return "Empty_Session";

        var firstFile = session.First();
        string dateStr = firstFile.LastWriteTime.ToString("yyyy-MM-dd");
        string timeStr = firstFile.LastWriteTime.ToString("HHmm");

        // Try to find a dominant file prefix
        // 1. Get all names without extension
        var names = session.Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToList();
        
        // 2. Find longest common substring that appears in > 50% of files
        // This is expensive for many files. Let's try simpler: Common Prefix.
        
        string commonPrefix = GetCommonPrefix(names);
        if (commonPrefix.Length > 3)
        {
            return $"{dateStr}_{commonPrefix.Trim(new[] { '_', '-', ' ', '.' })}";
        }

        // Fallback: Dominant Extension
        var dominantExt = session.GroupBy(f => f.Extension)
                                 .OrderByDescending(g => g.Count())
                                 .First().Key.TrimStart('.').ToUpper();
                                 
        return $"{dateStr}_Work_{timeStr}_{dominantExt}";
    }

    private string GetCommonPrefix(List<string> names)
    {
        if (names.Count == 0) return "";
        string prefix = names[0];
        
        // Only check first 5 files to be fast? Or check all?
        // Let's check all but stop early if prefix becomes too short.
        foreach (var name in names.Skip(1))
        {
            int len = 0;
            while (len < prefix.Length && len < name.Length && prefix[len] == name[len])
            {
                len++;
            }
            prefix = prefix.Substring(0, len);
            if (prefix.Length == 0) return "";
        }
        return prefix;
    }
}