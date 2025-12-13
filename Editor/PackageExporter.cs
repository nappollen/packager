using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Nappollen.Packager {
	public static class PackageExporter {
		public const string OutputFolder = "PackageExports";


		public static void ExportPackage(PackageInfo package, string outputFolder = OutputFolder) {
			var packageId = package.name;
			var version   = package.version;
			var baseName  = $"{packageId}-{version}";

			if (!Directory.Exists(outputFolder))
				Directory.CreateDirectory(outputFolder);

			var outputPath = Path.Combine(outputFolder, baseName);

			if (Directory.Exists(outputPath))
				Directory.Delete(outputPath, true);

			Directory.CreateDirectory(outputPath);

			// Copier le package.json
			var packageJsonSource = Path.Combine(package.resolvedPath, "package.json");
			var packageJsonDest   = Path.Combine(outputPath, "package.json");
			if (File.Exists(packageJsonSource))
				File.Copy(packageJsonSource, packageJsonDest);

			// Créer le .zip
			var zipPath = Path.Combine(outputPath, $"{baseName}.zip");
			CreateZip(package.resolvedPath, zipPath);

			// Créer le .unitypackage
			var unityPackagePath = Path.Combine(outputPath, $"{baseName}.unitypackage");
			CreateUnityPackage(package, unityPackagePath);

			// Ouvrir le dossier de sortie
			EditorUtility.RevealInFinder(outputPath);

			Debug.Log($"Package exported to: {outputPath}");
		}

		private static void CreateZip(string sourcePath, string zipPath) {
			if (File.Exists(zipPath))
				File.Delete(zipPath);

			// Collecter les patterns gitignore
			var gitignoreRules = GitignoreParser.CollectRules(sourcePath);

			using var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
			AddDirectoryToZip(zipArchive, sourcePath, sourcePath, gitignoreRules);
		}

		private static void AddDirectoryToZip(ZipArchive archive, string rootPath, string currentPath, List<GitignoreRule> rules) {
			// Charger les règles .gitignore locales à ce dossier
			var localGitignore = Path.Combine(currentPath, ".gitignore");
			if (File.Exists(localGitignore)) {
				var localRules = GitignoreParser.ParseFile(localGitignore, currentPath, rootPath);
				rules = new List<GitignoreRule>(rules);
				rules.AddRange(localRules);
			}

			// Ajouter les fichiers
			foreach (var filePath in Directory.GetFiles(currentPath)) {
				var relativePath = GitignoreParser.GetRelativePath(rootPath, filePath);

				// Ignorer les fichiers .gitignore eux-mêmes
				if (Path.GetFileName(filePath) == ".gitignore") continue;

				if (!GitignoreParser.IsIgnored(relativePath, rules))
					archive.CreateEntryFromFile(filePath, relativePath.Replace('\\', '/'));
			}

			// Parcourir les sous-dossiers
			foreach (var dirPath in Directory.GetDirectories(currentPath)) {
				var relativePath = GitignoreParser.GetRelativePath(rootPath, dirPath);
				if (!GitignoreParser.IsIgnored(relativePath + "/", rules))
					AddDirectoryToZip(archive, rootPath, dirPath, rules);
			}
		}

		private static void CreateUnityPackage(PackageInfo package, string outputPath) {
			// Trouver le chemin relatif du package dans Assets ou Packages
			var packagePath = package.assetPath;

			if (string.IsNullOrEmpty(packagePath))
				// Pour les packages locaux, on utilise le chemin résolu
				packagePath = $"Packages/{package.name}";

			// Collecter tous les assets du package
			var assetPaths = GetAllAssetPaths(packagePath);

			if (assetPaths.Length > 0) {
				AssetDatabase.ExportPackage(assetPaths, outputPath, ExportPackageOptions.Recurse);
			} else Debug.LogWarning($"No assets found for package: {package.name}");
		}

		private static string[] GetAllAssetPaths(string packagePath) {
			var paths = new List<string>();

			// Ajouter le dossier du package lui-même
			if (!AssetDatabase.IsValidFolder(packagePath))
				return paths.ToArray();
			paths.Add(packagePath);

			// Trouver tous les assets dans ce dossier
			var guids = AssetDatabase.FindAssets("", new[] { packagePath });
			foreach (var guid in guids) {
				var path = AssetDatabase.GUIDToAssetPath(guid);
				if (!paths.Contains(path))
					paths.Add(path);
			}

			return paths.ToArray();
		}
	}
}