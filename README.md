# Dataverse Plugin Wrapper Generator

Generate strongly typed C# wrappers and option set enums from a Dataverse solution zip (`customizations.xml`).

## Download

Download the latest prebuilt Windows executable from GitHub Releases:

- Latest release page: `https://github.com/EricLott/DataversePluginWrapper/releases/latest`
- Direct download (win-x64 zip): `https://github.com/EricLott/DataversePluginWrapper/releases/latest/download/DataversePluginWrapper-win-x64.zip`

Unzip and run `DataversePluginWrapper.exe`.

This tool reads Dataverse metadata and writes:
- `OptionSets/OptionValueSets.cs` with global option set enums and state enums
- `Entities/*.cs` wrapper classes with CRUD helpers (`Create`, `Retrieve`, `Update`, `Delete`)

## What This Project Is

- A .NET console app (`DataversePluginWrapper`) that parses Dataverse solution metadata.
- A code generator only. It does not deploy to Dataverse.
- Supports both:
  - CLI mode (arguments)
  - Interactive mode (no arguments)

## Prerequisites

- .NET 6 SDK or later installed
- A Dataverse solution zip containing `customizations.xml`

Check SDK:

```sh
dotnet --version
```

## Build

```sh
dotnet restore
dotnet build
```

## Run

### Interactive mode (recommended for first-time use)

Run with no arguments:

```sh
dotnet run --
```

You will be prompted for:
- solution zip path
- output directory
- optional entity-name filter
- verbose logging toggle
- overwrite behavior
- final confirmation

### CLI mode

```sh
dotnet run -- -z <path_to_solution_zip> [options]
```

Options:

- `-z, --zip <path>`: path to Dataverse solution zip (required)
- `-o, --out <dir>`: output directory (default: `./GeneratedClasses_{timestamp}`)
- `-f, --filter <text>`: generate only entities whose display name contains text
- `-v, --verbose`: verbose logging
- `-y, --yes`: overwrite existing files without prompt
- `-h, --help`: show help

### CLI examples

Generate everything:

```sh
dotnet run -- -z ./MySolution.zip
```

Generate only entities containing `Contact`:

```sh
dotnet run -- -z ./MySolution.zip -f Contact
```

Write to a fixed folder and overwrite existing files:

```sh
dotnet run -- -z ./MySolution.zip -o ./Generated -y
```

## Publish an EXE

Windows x64 example:

```sh
dotnet publish -c Release -r win-x64 --self-contained false
```

Output executable will be under:

`bin/Release/net6.0/win-x64/publish/`

Run the EXE directly:
- no args -> interactive mode
- with args -> CLI mode

## Output Layout

```text
GeneratedClasses_20260221_153000/
|-- OptionSets/
|   `-- OptionValueSets.cs
`-- Entities/
    |-- Account.cs
    |-- Contact.cs
    `-- ...
```

## Generated Code Notes

- Generated entity wrappers use Dataverse SDK types (`IOrganizationService`, `Entity`, `EntityReference`, `OptionSetValue`, etc.).
- Option set/state values are mapped to enums where possible.
- Status reason is generated as numeric backing (`int?`) with enum helper behavior.
- Lookups/owners/customers are generated as `EntityReference`.

## Consuming Generated Files

In the project where you use generated files, ensure Dataverse SDK references are available (for example, `Microsoft.Xrm.Sdk`).

If your consuming project does not reference SDK assemblies, generated files will not compile.

## Exit Codes

- `0`: success
- `1`: unexpected error
- `2`: argument/path error
- `3`: XML parsing error
- `4`: invalid data shape
- `130`: user canceled (Ctrl+C or interactive cancel)

## Troubleshooting

### "customizations.xml not found in zip"

- Ensure the zip is a Dataverse solution export.
- Verify the archive includes `customizations.xml`.

### No entities generated

- Check whether `--filter` excluded all entities.
- Run without filter to validate metadata extraction.

### Existing files are skipped

- Use `-y`/`--yes`, or answer `y` at overwrite prompts.

### Build succeeds but generated code fails in another project

- Add Dataverse SDK references to the consuming project.
- Validate that generated namespace/import placement matches your project conventions.

## Development

Local project files:
- `Program.cs`: generator implementation
- `DataversePluginWrapper.csproj`: project configuration

Build locally before committing:

```sh
dotnet build
```
