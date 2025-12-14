# Nappollen Packager

A Unity Editor tool to help create and manage packages.

![Preview](./preview.png)


## Features

- **Package Browser** - View and manage all local packages in your Unity project
- **Package Creator** - Quickly create new packages from a template with proper structure
- **Package Exporter** - Export packages as `.unitypackage`, `.zip`, and `package.json` files

## Installation

### Unity Package Manager (UPM)

Add the package using Unity Package Manager with the Git URL:
```
https://github.com/nappollen/packager.git
```
1. Open Unity and go to **Window > Package Manager**
2. Click the **+** button and select **Add package from git URL...**
3. Paste the URL and click **Add**

### VRChat Package Manager (VPM)

Check the VPM repository for the latest version:
[https://nappollen.github.io/vpm/](https://nappollen.github.io/vpm/)
1. On VPM listing, and `Add to VCC` to install the repo.
2. Open your VCC preject and (+) to add the Nappollen Packager package.

### Manual Installation

1. Download the latest release
2. Extract to your `Packages/` folder
3. Unity will automatically detect the package

## Usage

### Opening the Packager

Go to **Nappollen > Packager** in the Unity menu bar.

### Creating a New Package

1. Click the **New** button in the toolbar
2. Enter a package ID (e.g., `com.company.mypackage`)

### Exporting a Package

1. Click on a package in the list
2. The `.unitypackage`, `.zip` and package.json files will be created

## Requirements

- Unity 2022.3 or later

## License

MIT License - see [LICENSE](LICENSE) for details.

## Author

**Nappollen**
- GitHub: [@nappollen](https://github.com/nappollen)
