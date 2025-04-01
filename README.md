```markdown
# DataverseWrapper

A simple console application to extract and process a `customizations.xml` file from a Dynamics 365 / Dataverse unmanaged solution. It generates C# classes—including enums and CRUD operations—for your custom tables based on all metadata included in the solution.

## Features

- **Extraction:** Reads a zip file to locate `customizations.xml`.
- **Enum Generation:** Processes optionsets (including state and status) into enums.
- **Class Generation:** Creates C# classes for custom tables with mapped properties and CRUD methods.
- **Metadata Coverage:** Ensure your unmanaged solution contains all metadata for your custom tables.

## Requirements

- [.NET 5.0+](https://dotnet.microsoft.com/download) or a compatible .NET Framework version
- A valid unmanaged solution (downloaded from your Dynamics 365/Dataverse environment) that includes custom tables with full metadata
- Visual Studio or your preferred C# IDE

## Getting Started

1. **Download the Unmanaged Solution:**

   Make sure you export an unmanaged solution from your Dynamics 365/Dataverse environment. This solution should include all the custom tables you want to generate classes for, with complete metadata.

2. **Clone the Repository:**

   ```bash
   git clone https://github.com/yourusername/DataverseWrapper.git
   cd DataverseWrapper
   ```

3. **Build the Project:**

   Open the solution in Visual Studio and build it, or run:

   ```bash
   dotnet build
   ```

4. **Configure the Zip File Path:**

   In `Program.cs`, update the `zipPath` variable with the path to your zip file containing the unmanaged solution:

   ```csharp
   string zipPath = "path/to/your/customizations.zip";
   ```

5. **Run the Application:**

   Execute the project via Visual Studio or the command line:

   ```bash
   dotnet run
   ```

   The application will process the zip file and generate:
   - `OptionValueSets.cs`
   - A separate C# class file for each custom table in the `GeneratedClasses` folder

## Usage

- **Solution Export:** Ensure your unmanaged solution export includes all the required metadata for the custom tables.
- **Processing:** The app scans the provided zip file for `customizations.xml`, then reads and processes optionsets and entity definitions.
- **Generation:** C# classes with proper mappings and CRUD methods are output into the `GeneratedClasses` folder.

## Contributing

Contributions are welcome!  
Feel free to open issues or submit pull requests for improvements or bug fixes.
