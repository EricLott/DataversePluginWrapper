# Dataverse Wrapper Generator

A C# console application that generates strongly-typed entity classes and option set enums from a Dataverse solution zip (`customizations.xml`).
This tool helps developers working with Microsoft Dataverse (Dynamics 365) by creating easy-to-use wrapper classes instead of manually handling attributes and option sets.

## ✨ Features

* Parses `customizations.xml` inside a Dataverse solution zip.
* Generates:

  * **Option Sets** → strongly-typed enums in `OptionValueSets.cs`.
  * **State & StatusReason enums** (e.g., `ActiveStatusReason`, `InactiveStatusReason`).
  * **Entity classes** with CRUD wrappers (`Create`, `Retrieve`, `Update`, `Delete`).
* Smart sanitization for C# identifiers.
* Interactive overwrite prompts (with `--yes` to skip).
* Colored console output for better visibility.
* Entity filtering via `--filter`.

## ⚡ Getting Started

### 1. Clone & Build

```sh
git clone https://github.com/yourusername/DataverseWrapperGenerator.git
cd DataverseWrapperGenerator
dotnet build
```

### 2. Run

```sh
dotnet run -- -z path/to/your/solution.zip -o ./Generated
```

## 📖 Usage

```
DataverseWrapper -z <path_to_solution_zip> [options]

Options:
  -z, --zip <path>         Path to Dataverse solution zip (required)
  -o, --out <dir>          Output directory (default: ./GeneratedClasses_{timestamp})
  -f, --filter <text>      Only generate for entities whose display name contains text
  -v, --verbose            Verbose logging
  -y, --yes                Overwrite existing files without prompt
  -h, --help               Show help
```

### Example: Generate all

```sh
dotnet run -- -z ./MySolution.zip
```

### Example: Only generate for entities containing "Contact"

```sh
dotnet run -- -z ./MySolution.zip -f Contact
```

### Example: Overwrite without prompts

```sh
dotnet run -- -z ./MySolution.zip -o ./OutDir -y
```

## 📂 Output Structure

```
GeneratedClasses_20250919/
 ├── OptionSets/
 │   └── OptionValueSets.cs
 └── Entities/
     ├── Account.cs
     ├── Contact.cs
     └── ...
```

## ✅ Example Generated Code

### Option Set Enum

```csharp
public static class OptionValueSets
{
    public enum ContactType
    {
        Customer = 1,
        Vendor = 2,
        Partner = 3,
    }
}
```

### Entity Wrapper

```csharp
public class ContactItem
{
    private IOrganizationService _service;
    private string EntityLogicalName = "contact";

    [LogicalName("firstname")]
    public string FirstName { get; set; }

    [LogicalName("lastname")]
    public string LastName { get; set; }

    public ContactItem(IOrganizationService service)
    {
        _service = service;
    }

    public void Create() { ... }
    public void Retrieve(Guid id) { ... }
    public void Update() { ... }
    public void Delete() { ... }
}
```
