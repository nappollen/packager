using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Nappollen.Packager {
	public class NewPackagePopup : PopupWindowContent {
		public event Action<string> OnCreate;

		private TextField _idField;
		private Label     _displayNamePreview;
		private Label     _namespacePreview;

		public override Vector2 GetWindowSize() => new(300, 120);

		public override void OnGUI(Rect rect) { }

		public override void OnOpen() {
			var root = editorWindow.rootVisualElement;
			root.style.paddingTop    = 8;
			root.style.paddingBottom = 8;
			root.style.paddingLeft   = 8;
			root.style.paddingRight  = 8;

			_idField = new TextField("Package ID") {
				value = "com.company.package"
			};
			_idField.style.marginBottom = 4;
			_idField.RegisterValueChangedCallback(OnIdChanged);
			_idField.RegisterCallback<KeyDownEvent>(
				evt => {
					if (evt.keyCode == KeyCode.Return) {
						CreatePackage();
						evt.StopPropagation();
					}
				}
			);
			root.Add(_idField);

			// Preview labels
			_displayNamePreview = new Label();
			_displayNamePreview.style.fontSize = 10;
			_displayNamePreview.style.color = new Color(0.6f, 0.6f, 0.6f);
			_displayNamePreview.style.marginLeft = 4;
			root.Add(_displayNamePreview);

			_namespacePreview = new Label();
			_namespacePreview.style.fontSize = 10;
			_namespacePreview.style.color = new Color(0.6f, 0.6f, 0.6f);
			_namespacePreview.style.marginLeft = 4;
			_namespacePreview.style.marginBottom = 8;
			root.Add(_namespacePreview);

			UpdatePreview(_idField.value);

			var createButton = new Button(CreatePackage) { text = "Create" };
			createButton.style.height = 24;
			root.Add(createButton);

			_idField.Focus();
			_idField.SelectAll();
		}

		private void OnIdChanged(ChangeEvent<string> evt) {
			UpdatePreview(evt.newValue);
		}

		private void UpdatePreview(string packageId) {
			var parts = packageId.Split('.');
			
			// Ne sauter le premier élément que s'il a 3 caractères ou moins (com, org, net, io, dev...)
			if (parts.Length > 1 && parts[0].Length <= 3)
				parts = parts.Skip(1).ToArray();
			
			if (parts.Length == 0) {
				_displayNamePreview.text = "Display Name: -";
				_namespacePreview.text = "Namespace: -";
				return;
			}

			var displayName = string.Join(" ", parts.Select(s => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s));
			var rootNamespace = string.Join(".", parts.Select(s => s.Length > 0 ? char.ToUpper(s[0]) + s[1..] : s));

			_displayNamePreview.text = $"Display Name: {displayName}";
			_namespacePreview.text = $"Namespace: {rootNamespace}";
		}

		private void CreatePackage() {
			OnCreate?.Invoke(_idField.value);
			editorWindow.Close();
		}
	}
}

