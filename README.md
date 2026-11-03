# rg-gui: A Simple RipGrep GUI for Windows

![screenshot](screenshot.png)
![screenshot2](screenshot2.png)

[![License: MIT](https://img.shields.io/github/license/kcowolf/rg-gui)](https://opensource.org/licenses/MIT)

## Installation

Download the latest release of rg-gui from the [Releases page](https://github.com/kcowolf/rg-gui/releases).  Unzip it to a convenient location such as `C:\rg-gui`.

rg-gui first looks for it in your `PATH`; if it is not found there, it looks in the same folder as `rg-gui.exe`. You can [install ripgrep](<https://github.com/BurntSushi/ripgrep#installation>] or simply copy `rg.exe` next to `rg-gui.exe`. If neither is found, an error is shown.

The **.NET Desktop Runtime 8** must also be installed.  You can download it from here: <https://dotnet.microsoft.com/en-us/download/dotnet/8.0>

## Usage

Select a folder using the Browse button or by typing the path into the "In Folder" box.

Include Files and Exclude Files can be path or file names and can include wildcards.  Multiple names can be specified, separated by commas, semicolons, or spaces.

Folders can be added to the include/exclude list.  For example, excluding `\.git;\bin;\obj;*.exe;*.dll` will ignore any files in a subfolder named .git, bin, or obj, as well as any files with a .exe or .dll extension.

Finally, type the text you would like to search for in the "Containing Text" box.  Press the Start button to start your search.

The **Split into terms** option controls how the text is interpreted. When checked, words separated by space are searched as separate terms (use double quotes to keep a phrase together); a file is listed only if every term matches somewhere in it, and each term is highlighted in its own color. When unchecked, the entire text is searched as one phrase and no quoting is needed.

### File results

The left box displays a list of files containing matches.  If you click one of these results, the matching lines within that file will displayed in the right box.  You can right-click the filename to open the Windows context menu options (Open with, Edit, etc.)

### Line results

The right box displays a list of matching lines from the selected file.  You can select text from the "content" column by clicking/dragging over it; you can then use CTRL+C to copy it to the Windows clipboard.

If you have configured the file viewer path and arguments in the Settings window, you can right-click a line to open the file at that line in the viewer; you can also press the F3 key after clicking a line.

## Command Line Arguments

When you run rg-gui.exe, you can optionally use any of the following arguments to set a value:

| Long form       | Short form | Sets the following value |
| --------------- | ---------- | ------------------------ |
| --folder        | -f         | "In Folder"              |
| --include-files | -i         | "Include Files"          |
| --exclude-files | -e         | "Exclude Files"          |
| --text          | -t         | "Containing Text"        |

Examples in long and short form:

 `rg-gui.exe --folder "C:\git-projects\SQLiteCpp" --include-files "*.h" --exclude-files "\.git;\node_modules;\googletest" --text "tryExec"`

 `rg-gui.exe -f "C:\git-projects\SQLiteCpp" -i "*.h" -e "\.git;\node_modules;\googletest" -t "tryExec"`

## Settings

### Theme

Color theme for the app.  Light and Dark themes are included.

### Maximum Search Terms

Maximum number of terms (a word or quoted string) which can be typed in the "Containing Text" box.  This number determines the maximum number of processes rg-gui will run while searching.

### Multi-color Highlighting

If a search contains multiple terms, the results for each term can be highlighted in a separate color (up to 4 colors).  If this is disabled, results for all terms will be highlighted using the same color.

### Max highlights per line

Maximum number of terms (a word or quoted string) which can be highlighted in the line results box.  This prevents possible slowdowns when viewing results in a large file containing only one line.

### File Viewer

This can be used to allow a file to be opened directly to a line containing a result.  Both the "Executable path" and "Arguments" options must be set, or this will be disabled.

#### Executable Path

Full path to the program which will be used to view the file.  For example, `C:\Program Files\Notepad++\Notepad++.exe`

#### Arguments

Command-line arguments which should be used when invoking the file viewer.  The specific values needed here depend on which viewer program you are using.

The arguements must contain `$FILE` as a placeholder for the full path to the file to be opened.  `$LINE` may also be used as a placeholder for the line number.

When filling in the `$FILE` placeholder, the file path will automatically be surrounded with double-quotes.

For Notepad++, set the arguments to: `-n$LINE $FILE`

## Credits

rg-gui was written by Benjamin Stauffer (kcowolf).

Icon based on Line Hero Unlimited v2.4.1 - 02032020, file_find_search.png
<https://wishforge.itch.io/3000-free-icons>

Special thanks to Andrew Gallant (BurntSushi) for the [RipGrep](https://github.com/BurntSushi/ripgrep) tool.

Theme engine based on [WPFDarkTheme](https://github.com/AngryCarrot789/WPFDarkTheme).  Theme colors originally based on [ThemeWPF](https://github.com/Verta-IT/ThemeWPF/tree/main/Source/VertaIT.WPF.Theme).

## License

All files are distributed under the [MIT License](LICENSE) unless otherwise specified.  ShellContextMenu.cs is licensed under The Code Project Open License (CPOL) 1.02, <https://www.codeproject.com/info/cpol10.aspx>.  SHOpenFolderAndSelectItems.cs is licensed under the BSD 2-Clause License, BSD 2-Clause License, <https://opensource.org/license/bsd-2-clause>.
