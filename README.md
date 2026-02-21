# Dataverse Wrapper Generator

A C# console application that generates strongly typed entity classes and option set enums from a Dataverse solution zip (`customizations.xml`).

## Features

- Parses `customizations.xml` from a Dataverse solution zip.
- Generates option set enums in `OptionSets/OptionValueSets.cs`.
- Generates entity wrapper classes with `Create`, `Retrieve`, `Update`, and `Delete` methods.
- Supports overwrite prompts (or `--yes` to skip prompts).
- Supports entity filtering with `--filter`.

## Getting Started

### 1. Clone and build

```sh
git clone https://github.com/yourusername/DataversePluginWrapper.git
cd DataversePluginWrapper
dotnet build
```

### 2. Run

```sh
dotnet run -- -z path/to/your/solution.zip -o ./Generated
```

## Usage

```text
DataverseWrapper -z <path_to_solution_zip> [options]
DataverseWrapper                         (interactive mode)

Options:
  -z, --zip <path>         Path to Dataverse solution zip (required)
  -o, --out <dir>          Output directory (default: ./GeneratedClasses_{timestamp})
  -f, --filter <text>      Only generate for entities whose display name contains text
  -v, --verbose            Verbose logging
  -y, --yes                Overwrite existing files without prompt
  -h, --help               Show help
```

If you run the executable with no arguments, it starts a guided interactive flow and prompts for zip path, output folder, optional filter, and overwrite behavior.

### Examples

Generate all:

```sh
dotnet run -- -z ./MySolution.zip
```

Generate entities containing "Contact":

```sh
dotnet run -- -z ./MySolution.zip -f Contact
```

Overwrite without prompts:

```sh
dotnet run -- -z ./MySolution.zip -o ./OutDir -y
```

## Output Structure

```text
GeneratedClasses_20250919/
|-- OptionSets/
|   `-- OptionValueSets.cs
`-- Entities/
    |-- Account.cs
    |-- Contact.cs
    `-- ...
```
