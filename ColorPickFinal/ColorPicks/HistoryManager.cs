using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ColorPicks
{
    /// <summary>
    /// Persists the color-pick history to a plain-text file.
    /// Mirrors Java HistoryManager (uses Path / Files).
    /// </summary>
    public static class HistoryManager
    {
        /// <summary>Writes every item in <paramref name="items"/> to <paramref name="filePath"/>, one per line.</summary>
        public static void SaveHistory(string filePath, ListBox.ObjectCollection items)
        {
            using var writer = new StreamWriter(filePath, append: false);
            foreach (var item in items)
                writer.WriteLine(item?.ToString() ?? string.Empty);
        }

        /// <summary>
        /// Reads <paramref name="filePath"/> and returns non-blank lines.
        /// Returns an empty list if the file does not exist.
        /// </summary>
        public static List<string> LoadHistory(string filePath)
        {
            if (!File.Exists(filePath))
                return new List<string>();

            return File.ReadAllLines(filePath)
                       .Where(l => !string.IsNullOrWhiteSpace(l))
                       .ToList();
        }
    }
}
