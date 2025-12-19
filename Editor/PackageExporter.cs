using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Nappollen.Packager {
	public static class PackageExporter {
		public const string OutputFolder = "PackageExports";

		private const string GpgEnabledPrefKey = "Nappollen.Packager.GpgSigningEnabled";

		public static bool GpgSigningEnabled {
			get => EditorPrefs.GetBool(GpgEnabledPrefKey, true);
			set => EditorPrefs.SetBool(GpgEnabledPrefKey, value);
		}

		[MenuItem("Tools/Nappollen Packager/Enable GPG Signing")]
		private static void ToggleGpgSigning() {
			GpgSigningEnabled = !GpgSigningEnabled;
			var status = GpgSigningEnabled ? "enabled" : "disabled";
			Debug.Log($"GPG signing {status}.");
		}

		[MenuItem("Tools/Nappollen Packager/Enable GPG Signing", true)]
		private static bool ToggleGpgSigningValidate() {
			Menu.SetChecked("Tools/Nappollen Packager/Enable GPG Signing", GpgSigningEnabled);
			return true;
		}

		public static void ExportPackage(PackageInfo package, string outputFolder = OutputFolder) {
			var baseName = $"{package.name}-{package.version}";

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

			// Créer le manifest avec les hashs
			var manifestPath = Path.Combine(outputPath, "MANIFEST.txt");
			CreateManifest(manifestPath, outputPath, package);

			// Ouvrir le dossier de sortie
			EditorUtility.RevealInFinder(outputPath);

			Debug.Log($"Package exported to: {outputPath}");
		}

		private static void CreateManifest(string manifestPath, string outputPath, PackageInfo package) {
			var sb = new System.Text.StringBuilder();

			sb.AppendLine($"Package: {package.name}");
			sb.AppendLine($"Version: {package.version}");
			sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss} UTC");
			sb.AppendLine($"Generator: Nappollen.Packager");
			sb.AppendLine();
			sb.AppendLine("Files:");

			foreach (var file in Directory.GetFiles(outputPath)) {
				if (Path.GetFileName(file) == "MANIFEST.txt") continue;
				if (Path.GetFileName(file) == "MANIFEST.txt.asc") continue;

				var fileName = Path.GetFileName(file);
				var sha256   = ComputeSha256(file);
				var sha512   = ComputeSha512(file);
				var fileSize = new FileInfo(file).Length;

				sb.AppendLine();
				sb.AppendLine($"  {fileName}");
				sb.AppendLine($"    Size: {fileSize} bytes");
				sb.AppendLine($"    SHA256: {sha256}");
				sb.AppendLine($"    SHA512: {sha512}");
			}

			File.WriteAllText(manifestPath, sb.ToString());

			// Signer avec GPG si activé et disponible
			if (GpgSigningEnabled && IsGpgAvailable()) {
				SignManifestWithGpg(manifestPath);
			}
		}

		private static string _gpgPath;

		private static bool IsGpgAvailable() {
			_gpgPath = FindGpgPath();
			return _gpgPath != null;
		}

		private static string FindGpgPath() {
			// Chemins courants pour GPG
			var possiblePaths = new[] {
				"gpg", // Dans le PATH
				@"C:\Program Files (x86)\GnuPG\bin\gpg.exe",
				@"C:\Program Files\GnuPG\bin\gpg.exe",
				@"C:\Program Files (x86)\gnupg\bin\gpg.exe",
				@"C:\Program Files\gnupg\bin\gpg.exe",
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GnuPG", "bin", "gpg.exe"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GnuPG", "bin", "gpg.exe"),
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GnuPG", "bin", "gpg.exe"),
			};

			return possiblePaths.FirstOrDefault(TryRunGpg);
		}

		private static bool TryRunGpg(string gpgPath) {
			try {
				var process = new System.Diagnostics.Process {
					StartInfo = new System.Diagnostics.ProcessStartInfo {
						FileName               = gpgPath,
						Arguments              = "--version",
						UseShellExecute        = false,
						RedirectStandardOutput = true,
						RedirectStandardError  = true,
						CreateNoWindow         = true
					}
				};
				process.Start();
				process.WaitForExit(2000);
				return process.ExitCode == 0;
			} catch {
				return false;
			}
		}

		private static void SignManifestWithGpg(string manifestPath) {
			if (string.IsNullOrEmpty(_gpgPath)) return;

			try {
				var process = new System.Diagnostics.Process {
					StartInfo = new System.Diagnostics.ProcessStartInfo {
						FileName               = _gpgPath,
						Arguments              = $"--armor --detach-sign \"{manifestPath}\"",
						UseShellExecute        = false,
						RedirectStandardOutput = true,
						RedirectStandardError  = true,
						CreateNoWindow         = true
					}
				};
				process.Start();
				process.WaitForExit(10000);

				if (process.ExitCode == 0) {
					Debug.Log("Manifest signed with GPG successfully.");
				} else {
					var error = process.StandardError.ReadToEnd();
					Debug.LogWarning($"GPG signing failed: {error}");
				}
			} catch (Exception e) {
				Debug.LogWarning($"Failed to sign manifest with GPG: {e.Message}");
			}
		}

		private static string ComputeSha256(string filePath) {
			using var sha256 = System.Security.Cryptography.SHA256.Create();
			using var stream = File.OpenRead(filePath);
			var       hash   = sha256.ComputeHash(stream);
			return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
		}

		private static string ComputeSha512(string filePath) {
			using var sha512 = System.Security.Cryptography.SHA512.Create();
			using var stream = File.OpenRead(filePath);
			var       hash   = sha512.ComputeHash(stream);
			return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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
				var dirName = Path.GetFileName(dirPath);

				// Exclure explicitement les dossiers .git
				if (dirName == ".git") continue;

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