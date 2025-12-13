using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nappollen.Packager {
	public static class GitignoreParser {
		public static List<GitignoreRule> CollectRules(string rootPath) {
			var rules = new List<GitignoreRule>();

			// Charger le .gitignore racine
			var rootGitignore = Path.Combine(rootPath, ".gitignore");
			if (File.Exists(rootGitignore))
				rules.AddRange(ParseFile(rootGitignore, rootPath, rootPath));

			return rules;
		}

		public static List<GitignoreRule> ParseFile(string gitignorePath, string gitignoreDir, string rootPath) {
			var lines    = File.ReadAllLines(gitignorePath);
			var basePath = GetRelativePath(rootPath, gitignoreDir);

			return (from line in lines
				select line.Trim() into trimmedLine
				where !string.IsNullOrEmpty(trimmedLine) && !trimmedLine.StartsWith("#")
				let isNegation = trimmedLine.StartsWith("!")
				let pattern = isNegation ? trimmedLine[1..] : trimmedLine
				select new GitignoreRule {
					Pattern    = pattern,
					IsNegation = isNegation,
					BasePath   = basePath
				}).ToList();
		}

		public static bool IsIgnored(string relativePath, List<GitignoreRule> rules) {
			var ignored = false;
			relativePath = relativePath.Replace('\\', '/');

			foreach (var rule in rules.Where(rule => MatchesPattern(relativePath, rule)))
				ignored = !rule.IsNegation;

			return ignored;
		}

		private static bool MatchesPattern(string path, GitignoreRule rule) {
			var pattern  = rule.Pattern.Replace('\\', '/');
			var testPath = path;

			// Si le pattern commence par /, il est relatif au basePath
			if (pattern.StartsWith("/")) {
				pattern = pattern[1..];
				if (!string.IsNullOrEmpty(rule.BasePath))
					testPath = path.StartsWith(rule.BasePath + "/")
						? path[(rule.BasePath.Length + 1)..]
						: path;
			}

			// Convertir le pattern gitignore en regex
			var regexPattern = PatternToRegex(pattern);

			try {
				// Tester le chemin complet et le nom de fichier
				return Regex.IsMatch(testPath, regexPattern, RegexOptions.IgnoreCase)
					|| Regex.IsMatch(Path.GetFileName(path), regexPattern, RegexOptions.IgnoreCase);
			} catch {
				return false;
			}
		}

		private static string PatternToRegex(string pattern) {
			// Échapper les caractères spéciaux regex
			var escaped = Regex.Escape(pattern);

			// Convertir les wildcards gitignore
			escaped = escaped.Replace(@"\*\*", ".*");  // ** -> match tout
			escaped = escaped.Replace("\\*", "[^/]*"); // * -> match tout sauf /
			escaped = escaped.Replace("\\?", "[^/]");  // ? -> match un caractère sauf /

			// Si le pattern ne contient pas de /, il peut matcher n'importe où
			if (!pattern.Contains("/"))
				return $"(^|/){escaped}(/|$)";

			// Si le pattern se termine par /, c'est un dossier
			return pattern.EndsWith("/")
				? $"^{escaped.TrimEnd('/')}(/|$)"
				: $"^{escaped}$|/{escaped}$";
		}

		public static string GetRelativePath(string rootPath, string fullPath) {
			var rootUri = new System.Uri(rootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
			var fullUri = new System.Uri(fullPath);
			return System.Uri.UnescapeDataString(rootUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar));
		}
	}

	public class GitignoreRule {
		public string Pattern;
		public bool   IsNegation;
		public string BasePath;
	}
}

