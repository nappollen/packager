using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Nappollen.Packager {
	public static class PackageCreator {
		public static string ToDisplayName(string packageId) {
			var parts = packageId.Replace("-", ".").Split('.');
			if (parts.Length > 1 && parts[0].Length < 4)
				parts = parts.Skip(1).ToArray();
			return string.Join(" ", parts.Select(s => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s));
		}

		public static string ToRootNamespace(string packageId) {
			var parts = packageId.Split('.');
			if (parts.Length > 1 && parts[0].Length < 4)
				parts = parts.Skip(1).ToArray();
			var rootNamespace = string.Join(".", parts.Select(s => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s));
			parts         = rootNamespace.Split('-');
			rootNamespace = string.Join("", parts.Select(s => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s));
			return rootNamespace;
		}


		public static void CreateNewPackage(string packageId) {
			if (string.IsNullOrWhiteSpace(packageId)) {
				Debug.LogError("Package ID cannot be empty.");
				return;
			}

			// Valider le format de l'ID (com.example.package)
			if (!Regex.IsMatch(packageId, @"^[a-z][a-z0-9-_]*(\.[a-z][a-z0-9-_]*)+$")) {
				Debug.LogError("Invalid package ID format. Use lowercase with dots (e.g., com.company.package)");
				return;
			}

			var projectRoot = Path.GetDirectoryName(Application.dataPath);
			if (projectRoot == null) return;

			var packagePath = Path.Combine(projectRoot, "Packages", packageId);

			if (Directory.Exists(packagePath)) {
				Debug.LogError($"Package '{packageId}' already exists.");
				return;
			}

			// Trouver le dossier template
			var templatePath = Path.GetFullPath("Packages/nappollen.packager/PackageTemplate");
			if (!Directory.Exists(templatePath)) {
				Debug.LogError($"Package template not found at: {templatePath}");
				return;
			}

			// Copier le template
			CopyDirectory(templatePath, packagePath);

			// Préparer les remplacements
			var parts = packageId.Split('.');

			// Ne sauter le premier élément que s'il s'agit d'un préfixe standard
			if (parts.Length > 1 && parts[0].Length < 4)
				parts = parts.Skip(1).ToArray();

			var displayName   = ToDisplayName(packageId);
			var unityVersion  = $"{Application.unityVersion.Split('.')[0]}.{Application.unityVersion.Split('.')[1]}";
			var rootNamespace = ToRootNamespace(packageId);

			var repository = parts.Length > 0 && parts[^1].Length > 0
				? string.Join(".", parts.Skip(1).Select(s => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s))
				: "";
			
			var organization = parts.Length > 0 && parts[0].Length > 0
				? char.ToUpper(parts[0][0]) + parts[0][1..]
				: "";
			
			parts = packageId.Replace("-", ".").Split('.');
			if (parts.Length > 1 && parts[0].Length < 4)
				parts = parts.Skip(1).ToArray();
			
			var name = parts.Length > 0 && parts[^1].Length > 0
				? string.Join("", parts.Skip(1).Select(s => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s))
				: "";
			
			var replacements = new Dictionary<string, string> {
				{ "PACKAGE_ID", packageId },
				{ "DISPLAY_NAME", displayName },
				{ "UNITY_VERSION", unityVersion },
				{ "ROOT_NAMESPACE", rootNamespace },
				{ "REPOSITORY", repository },
				{ "ORGANIZATION", organization },
				{ "NAME", name },
				{ "DATE", System.DateTime.Now.ToString("yyyy-MM-dd") },
				{ "YEAR", System.DateTime.Now.Year.ToString() },
				{ "VERSION", "1.0.0" },
			};

			// Remplacer les placeholders dans tous les fichiers et renommer si nécessaire
			ProcessTemplateFiles(packagePath, replacements);

			AssetDatabase.Refresh();

			// Recharger les packages Unity
			Client.Resolve();
		}

		private static void ProcessTemplateFiles(string directory, Dictionary<string, string> replacements) {
			// Traiter les fichiers
			foreach (var filePath in Directory.GetFiles(directory)) {
				var fileName = Path.GetFileName(filePath);

				// Lire le fichier en bytes pour vérifier s'il contient des placeholders
				var bytes   = File.ReadAllBytes(filePath);
				var content = System.Text.Encoding.UTF8.GetString(bytes);

				// Vérifier si le fichier contient des placeholders
				var hasPlaceholders = replacements.Keys.Any(key => content.Contains(key));

				if (hasPlaceholders) {
					// Remplacer le contenu
					content = ReplacePlaceholders(content, replacements);
					File.WriteAllText(filePath, content);
				}

				// Renommer le fichier si nécessaire
				var newFileName = ReplacePlaceholders(fileName, replacements);

				// Retirer l'extension .template
				if (newFileName.EndsWith(".template"))
					newFileName = newFileName[..^".template".Length];

				if (newFileName != fileName) {
					var newFilePath = Path.Combine(directory, newFileName);
					File.Move(filePath, newFilePath);
				}
			}

			// Parcourir les sous-dossiers
			foreach (var dir in Directory.GetDirectories(directory))
				ProcessTemplateFiles(dir, replacements);
		}

		private static string ReplacePlaceholders(string content, Dictionary<string, string> replacements) {
			foreach (var (key, value) in replacements) {
				// {KEY} -> valeur normale
				content = content.Replace($"{{{key}}}", value);
				// {-KEY} -> valeur en minuscules
				content = content.Replace($"{{-{key}}}", value.ToLower());
				// {+KEY} -> valeur en majuscules
				content = content.Replace($"{{+{key}}}", value.ToUpper());
			}

			return content;
		}

		private static void CopyDirectory(string sourceDir, string destDir) {
			Directory.CreateDirectory(destDir);

			foreach (var file in Directory.GetFiles(sourceDir)) {
				var fileName = Path.GetFileName(file);
				// Ignorer les fichiers .meta et .gitkeep
				if (fileName.EndsWith(".meta") || fileName == ".gitkeep") continue;
				File.Copy(file, Path.Combine(destDir, fileName));
			}

			foreach (var dir in Directory.GetDirectories(sourceDir)) {
				var dirName = Path.GetFileName(dir);
				CopyDirectory(dir, Path.Combine(destDir, dirName));
			}
		}
	}
}