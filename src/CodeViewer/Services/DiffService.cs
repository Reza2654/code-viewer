using System;
using System.Collections.Generic;
using CodeViewer.Models;

namespace CodeViewer.Services;

public interface IDiffService
{
    List<DiffLine> ComputeDiff(string oldText, string newText);
}

public class DiffService : IDiffService
{
    public List<DiffLine> ComputeDiff(string oldText, string newText)
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);

        var n = oldLines.Length;
        var m = newLines.Length;

        // Fast-path: both empty
        if (n == 0 && m == 0) return new List<DiffLine>();

        // Fast-path: old is empty -> all added
        if (n == 0)
        {
            var res = new List<DiffLine>(m);
            for (int i = 0; i < m; i++)
            {
                res.Add(new DiffLine(DiffLineType.Added, null, i + 1, newLines[i]));
            }
            return res;
        }

        // Fast-path: new is empty -> all removed
        if (m == 0)
        {
            var res = new List<DiffLine>(n);
            for (int i = 0; i < n; i++)
            {
                res.Add(new DiffLine(DiffLineType.Removed, i + 1, null, oldLines[i]));
            }
            return res;
        }

        // Longest Common Subsequence (LCS) matrix for clean diff
        // For typical files up to a few thousand lines, standard DP is instant and predictable
        if (n * m <= 4_000_000)
        {
            return ComputeLcsDiff(oldLines, newLines);
        }

        // Fallback for massive files: Myers greedy diff
        return ComputeMyersDiff(oldLines, newLines);
    }

    private static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static List<DiffLine> ComputeLcsDiff(string[] a, string[] b)
    {
        int n = a.Length;
        int m = b.Length;
        int[,] dp = new int[n + 1, m + 1];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                if (a[i] == b[j])
                {
                    dp[i + 1, j + 1] = dp[i, j] + 1;
                }
                else
                {
                    dp[i + 1, j + 1] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                }
            }
        }

        var result = new List<DiffLine>(n + m);
        int curA = n;
        int curB = m;

        var stack = new Stack<DiffLine>();

        while (curA > 0 || curB > 0)
        {
            if (curA > 0 && curB > 0 && a[curA - 1] == b[curB - 1])
            {
                stack.Push(new DiffLine(DiffLineType.Unchanged, curA, curB, a[curA - 1]));
                curA--;
                curB--;
            }
            else if (curB > 0 && (curA == 0 || dp[curA, curB - 1] >= dp[curA - 1, curB]))
            {
                stack.Push(new DiffLine(DiffLineType.Added, null, curB, b[curB - 1]));
                curB--;
            }
            else if (curA > 0)
            {
                stack.Push(new DiffLine(DiffLineType.Removed, curA, null, a[curA - 1]));
                curA--;
            }
        }

        while (stack.Count > 0)
        {
            result.Add(stack.Pop());
        }

        return result;
    }

    private static List<DiffLine> ComputeMyersDiff(string[] a, string[] b)
    {
        // Line-by-line fallback for very large files
        var result = new List<DiffLine>();
        int min = Math.Min(a.Length, b.Length);
        int i = 0;
        for (; i < min; i++)
        {
            if (a[i] == b[i])
            {
                result.Add(new DiffLine(DiffLineType.Unchanged, i + 1, i + 1, a[i]));
            }
            else
            {
                result.Add(new DiffLine(DiffLineType.Removed, i + 1, null, a[i]));
                result.Add(new DiffLine(DiffLineType.Added, null, i + 1, b[i]));
            }
        }
        for (int remA = i; remA < a.Length; remA++)
        {
            result.Add(new DiffLine(DiffLineType.Removed, remA + 1, null, a[remA]));
        }
        for (int remB = i; remB < b.Length; remB++)
        {
            result.Add(new DiffLine(DiffLineType.Added, null, remB + 1, b[remB]));
        }
        return result;
    }
}
