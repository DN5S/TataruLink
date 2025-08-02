# TataruLink

A Dalamud plugin for Final Fantasy XIV that provides real-time chat translation with multiple engines and advanced features.

## Features

- **Translation Engines**: Google Translate (free), DeepL, Gemini AI, and Ollama (local LLM)
- **Real-time Translation**: Automatic chat translation with smart filtering
- **Outgoing Translation**: Translate your messages before sending (`/tl`)
- **User Glossary**: Custom dictionary for consistent translations (`/tg`)
- **Display**: In-game chat, overlay window, or both
- **Caching**: Reduces API calls and improves performance

## Commands

- `/tataruconfig` - Configuration window
- `/tatarulink` - Translation history window
- `/tataruoverlay` - Toggle the overlay window
- `/tatarutest <text>` - Test translation
- `/tl <text>` - Translate text and copy to clipboard
- `/tg <original> <translation>` - Add glossary entry

## Quick Setup

1. Install manually (official repository coming soon)
2. Open `/tataruconfig`:
   - **General**: Enable translation, select engine, set languages
   - **Chat Types**: Choose channels to translate
   - **Glossary**: Add custom translations
3. **API Keys** (optional):
   - **DeepL**: Get key from [deepl.com/pro-api](https://www.deepl.com/pro-api)
   - **Gemini**: Get key from `Google AI Studio`
   - **Ollama**: Set local endpoint (default: `http://localhost:11434`)

## Development

### Building
```bash
git clone https://github.com/DN5S/TataruLink.git
cd TataruLink
# Open TataruLink.sln and build
# Output: TataruLink/bin/x64/[Debug|Release]/TataruLink.dll
```


## Contributing

We welcome contributions! Here's how to get started:

### Bug Reports
- Use the [GitHub Issues](https://github.com/DN5S/TataruLink/issues) page
- Include steps to reproduce, expected vs. actual behavior
- Attach logs from `%AppData%\XIVLauncher\pluginConfigs\TataruLink\`

### Feature Requests
- Open an issue with the `enhancement` label
- Describe the use case and expected behavior
- Consider if it fits the plugin's scope

### Code Contributions

#### Getting Started
1. **Fork** the repository
2. **Create** a feature branch: `git checkout -b feature/amazing-feature`
3. **Follow** the coding standards below
4. **Test** your changes thoroughly
5. **Submit** a Pull Request

## License

**GNU AFFERO GENERAL PUBLIC LICENSE v3.0** - See [LICENSE.md](LICENSE.md)
