using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Nappollen.Packager {
	public class Packager : EditorWindow {
		private          ListRequest                   _listRequest;
		private readonly List<PackageInfo>             _packages     = new();
		private readonly Dictionary<string, Texture2D> _packageIcons = new();
		private          bool                          _isLoading    = true;
		private          string                        _filterText   = "";

		// UI Elements
		private VisualElement      _loadingContainer;
		private VisualElement      _emptyContainer;
		private VisualElement      _noMatchContainer;
		private ScrollView         _packageList;
		private ToolbarSearchField _searchField;
		private VisualTreeAsset    _packageItemTemplate;

		[MenuItem("Nappollen/Packager")]
		public static void ShowWindow() {
			var window = GetWindow<Packager>("Nappollen's Packager");
			window.minSize = new Vector2(400, 700);
			window.Show();
		}

		private void OnEnable() {
			// Charger le UXML
			var visualTree = Resources.Load<VisualTreeAsset>("Packager");
			if (visualTree != null)
				visualTree.CloneTree(rootVisualElement);

			// Charger le template pour les items
			_packageItemTemplate = Resources.Load<VisualTreeAsset>("PackageItem");

			// Récupérer les références
			_loadingContainer = rootVisualElement.Q("loading-container");
			_emptyContainer   = rootVisualElement.Q("empty-container");
			_noMatchContainer = rootVisualElement.Q("no-match-container");
			_packageList      = rootVisualElement.Q<ScrollView>("package-list");
			_searchField      = rootVisualElement.Q<ToolbarSearchField>("search-field");

			// Configurer les événements
			_searchField?.RegisterValueChangedCallback(OnSearchChanged);
			rootVisualElement.Q<ToolbarButton>("new-button")?.RegisterCallback<ClickEvent>(_ => ShowNewPackagePopup());
			rootVisualElement.Q<ToolbarButton>("refresh-button")?.RegisterCallback<ClickEvent>(_ => RefreshPackageList());

			RefreshPackageList();
		}

		private void OnSearchChanged(ChangeEvent<string> evt) {
			_filterText = evt.newValue;
			UpdatePackageListUI();
		}

		private void ShowNewPackagePopup() {
			var popup = new NewPackagePopup();
			popup.OnCreate += OnCreateNewPackage;
			UnityEditor.PopupWindow.Show(rootVisualElement.Q<ToolbarButton>("new-button").worldBound, popup);
		}

		private void OnCreateNewPackage(string packageId) {
			PackageCreator.CreateNewPackage(packageId);
			RefreshPackageList();
		}

		private void RefreshPackageList() {
			_isLoading = true;
			_packages.Clear();
			_packageIcons.Clear();
			_listRequest             =  Client.List(true);
			EditorApplication.update += OnPackageListProgress;
		}

		private void OnPackageListProgress() {
			if (!_listRequest.IsCompleted) return;

			EditorApplication.update -= OnPackageListProgress;

			if (_listRequest.Status == StatusCode.Success) {
				foreach (var package in _listRequest.Result) {
					_packages.Add(package);
					LoadPackageIcon(package);
				}

				_packages.Sort(
					(a, b) => {
						var displayCompare = string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
						return displayCompare != 0 ? displayCompare : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
					}
				);
			} else Debug.LogError($"Failed to list packages: {_listRequest.Error.message}");

			_isLoading = false;
			UpdatePackageListUI();
		}

		private void LoadPackageIcon(PackageInfo package) {
			var iconPath = Path.Combine(package.resolvedPath, "icon.png");
			if (!File.Exists(iconPath)) return;
			var iconData = File.ReadAllBytes(iconPath);
			var texture  = new Texture2D(2, 2);
			if (texture.LoadImage(iconData))
				_packageIcons[package.name] = texture;
		}

		private void UpdatePackageListUI() {
			if (_loadingContainer == null) return;

			// Afficher/masquer les conteneurs selon l'état
			_loadingContainer.EnableInClassList("hidden", !_isLoading);

			if (_isLoading) {
				_emptyContainer.AddToClassList("hidden");
				_noMatchContainer.AddToClassList("hidden");
				_packageList.AddToClassList("hidden");
				return;
			}

			var hasPackages = _packages.Count > 0;
			_emptyContainer.EnableInClassList("hidden", hasPackages);

			if (!hasPackages) {
				_noMatchContainer.AddToClassList("hidden");
				_packageList.AddToClassList("hidden");
				return;
			}

			// Filtrer les packages
			var filteredPackages = string.IsNullOrEmpty(_filterText)
				? _packages
				: _packages.Where(
						p =>
							p.displayName.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0 || p.name.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0
					)
					.ToList();

			var hasMatches = filteredPackages.Count > 0;
			_noMatchContainer.EnableInClassList("hidden", hasMatches);
			_packageList.EnableInClassList("hidden", !hasMatches);

			if (!hasMatches) return;

			// Reconstruire la liste
			_packageList.Clear();
			foreach (var package in filteredPackages) {
				var item = CreatePackageItem(package);
				_packageList.Add(item);
			}
		}

		private VisualElement CreatePackageItem(PackageInfo package) {
			VisualElement item;

			if (_packageItemTemplate != null) {
				item = _packageItemTemplate.CloneTree();
				item = item.Q(className: "package-item") ?? item;
			} else {
				// Fallback si le template n'est pas chargé
				item = new VisualElement();
				item.AddToClassList("package-item");

				var iconFallback = new VisualElement();
				iconFallback.AddToClassList("package-icon");
				item.Add(iconFallback);

				var info = new VisualElement();
				info.AddToClassList("package-info");

				var displayName = new Label { name = "display-name" };
				displayName.AddToClassList("package-display-name");
				info.Add(displayName);

				var packageId = new Label { name = "package-id" };
				packageId.AddToClassList("package-id");
				info.Add(packageId);

				item.Add(info);
			}

			// Configurer l'icône
			var iconElement = item.Q(className: "package-icon");
			if (iconElement != null && _packageIcons.TryGetValue(package.name, out var icon)) {
				iconElement.style.backgroundImage = icon;
			}

			// Configurer les labels
			var displayNameLabel = item.Q<Label>("display-name");
			if (displayNameLabel != null)
				displayNameLabel.text = package.displayName;

			var packageIdLabel = item.Q<Label>("package-id");
			if (packageIdLabel != null)
				packageIdLabel.text = $"{package.name} • v{package.version}";

			// Ajouter le clic
			item.RegisterCallback<ClickEvent>(_ => PackageExporter.ExportPackage(package));

			return item;
		}

		private void OnDisable() {
			foreach (var icon in _packageIcons.Values.Where(icon => icon))
				DestroyImmediate(icon);
			_packageIcons.Clear();
		}
	}
}