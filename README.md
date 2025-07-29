# TataruLink

A Dalamud plugin for `FINAL FANTASY XIV` that provides real-time chat translation directly in-game, supporting multiple translation engines.

## Features

* **Real-time Chat Translation**: Automatically translates incoming chat messages seamlessly.
* **Multiple Translation Engines**: Natively supports Google Translate (free) and DeepL (API key required).
* **Flexible Display Options**: Choose to display translations in the standard in-game chat, a dedicated overlay window, or both.
* **Smart Filtering**: A powerful filtering system to precisely control which chat types, senders, or message types are translated.
* **Performance-Oriented Caching**: An intelligent caching system reduces redundant API calls, saving bandwidth and improving response time.
* **Comprehensive UI Suite**:
    * **Main Window (`/tatarulink`):** A detailed history viewer to browse and search your recent translations.
    * **Overlay Window (`/tataruoverlay`):** A customizable, movable window for a dedicated translation feed.
    * **Configuration Window (`/tataruconfig`):** A central hub to manage all plugin settings.

## Installation

### From the official repository (Not Supported)

*Coming soon. Once approved, TataruLink will be available through the official Dalamud plugin repository via the `/xlplugins` command.*

### Manual Installation (Building from Source)

For developers and testers. See the `Development` section below for instructions on how to build the plugin from source.

-----

## Usage

### Initial Setup

1.  Use the `/tataruconfig` command to open the configuration window.
2.  Navigate through the tabs to configure your preferred settings:
    * **General**: Enable translations, choose your primary engine, and set your source/target languages.
    * **Chat Types**: Select the specific chat channels (e.g., Say, Party, FC) you want to translate.
    * **Display**: Configure the output mode (in-game, overlay, or both) and customize the message format.

### Available Commands

* `/tatarulink` - Toggles the main history/cache window.
* `/tataruoverlay` - Toggles the real-time translation overlay window.
* `/tataruconfig` - Toggles the configuration window.
* `/tatarutest <text>` - Performs a test translation with the provided text using your current settings.

## Development

### Prerequisites

* [**Visual Studio 2022**](https://visualstudio.microsoft.com) or [**JetBrains Rider**](https://www.jetbrains.com/rider/).
* Dalamud plugin development environment correctly set up. The project is configured to work with the official Dalamud template.

### Building from Source

1.  Clone the repository:
    ```bash
    git clone https://github.com/your-username/TataruLink.git
    cd TataruLink
    ```
2.  Open `TataruLink.sln` in your IDE.
3.  Build the solution. The dependencies, including Dalamud references, will be handled by NuGet.
4.  The compiled plugin DLL will be located at:
    `TataruLink/bin/x64/[Debug or Release]/TataruLink.dll`

-----

## API Keys

### DeepL API (Optional)

To use DeepL translation (recommended for better accuracy):

1. Sign up for a DeepL API account at [https://www.deepl.com/pro-api](https://www.deepl.com/pro-api)
2. Get your API key from the account dashboard
3. Enter the API key in the plugin configuration (`/tataruconfig`)

Note: DeepL offers a free tier with 500,000 characters per month.

-----

## Contributing

Contributions are welcome. Please feel free to submit pull requests or open issues for bugs and feature requests. All contributions will be reviewed based on the architectural principles established in the project.

## License

This project is licensed under the `GNU AFFERO GENERAL PUBLIC LICENSE`. See the [LICENSE.md](LICENSE.md) file for details.

