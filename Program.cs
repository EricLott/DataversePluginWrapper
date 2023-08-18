using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace DataverseWrapper
{
    class Program
    {
        private static Dictionary<string, string> LogicalNameToEnumMap = new Dictionary<string, string>();
        private static Dictionary<string, string> LocalizedNameToLogicalNameMap = new Dictionary<string, string>();


        static void Main(string[] args)
        {
            string zipPath = "C:\\Users\\Lenovo\\Downloads\\CRMBase_1_1_0_4.zip"; // Provide the path to your zip file here
            ReadCustomizationsXml(zipPath);
        }

        private static void ReadCustomizationsXml(string zipPath)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("customizations.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        using (StreamReader reader = new StreamReader(entry.Open()))
                        {
                            XmlDocument doc = new XmlDocument();
                            doc.Load(reader);

                            ProcessOptionSets(doc);
                            ProcessEntities(doc);
                        }
                    }
                }
            }
        }

        private static void ProcessOptionSets(XmlDocument doc)
        {
            StringBuilder optionValueSetsClass = new StringBuilder("public static class OptionValueSets\n{\n");

            XmlNodeList optionSets = doc.SelectNodes("//optionsets/optionset");
            foreach (XmlNode optionSet in optionSets)
            {
                string optionSetName = optionSet.Attributes["localizedName"].Value;
                string optionSetLogicalName = optionSet.Attributes["Name"].Value;
                string enumName = optionSetName.Replace(" ", "");
                LogicalNameToEnumMap.Add(optionSetLogicalName, enumName);


                string enumDefinition = GenerateEnumDefinition(optionSet, enumName);

                optionValueSetsClass.AppendLine(enumDefinition);
            }

            optionValueSetsClass.AppendLine("}");

            string outputPath = Path.Combine("C:\\Users\\Lenovo\\OneDrive\\Documents", "GeneratedClasses");
            Directory.CreateDirectory(outputPath); // Ensure the directory exists
            File.WriteAllText(Path.Combine(outputPath, "OptionValueSets.cs"), optionValueSetsClass.ToString());
        }

        private static string SanitizeString(string title)
        {
            if (title == null) return null;

            // Remove or replace any characters that are not valid in C# identifiers
            StringBuilder sanitizedTitle = new StringBuilder();
            foreach (char c in title)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sanitizedTitle.Append(c);
                }
                else
                {
                }
            }

            return sanitizedTitle.ToString();
        }


        private static string GenerateEnumDefinition(XmlNode optionSet, string enumName)
        {
            StringBuilder enumDefinition = new StringBuilder($"public enum {enumName} {{\n");

            XmlNodeList options = optionSet.SelectNodes("options/option");
            foreach (XmlNode option in options)
            {
                string label = option.SelectSingleNode("labels/label")?.Attributes["description"]?.Value;
                string value = option.Attributes["value"].Value;

                string sanitizedLabel = SanitizeString(label);

                enumDefinition.AppendLine($"   {sanitizedLabel} = {value},");
            }

            enumDefinition.AppendLine("}");

            return enumDefinition.ToString();
        }


        private static void ProcessEntities(XmlDocument doc)
        {
            XmlNodeList entities = doc.SelectNodes("//Entity");
            foreach (XmlNode entityNode in entities)
            {
                string classDefinition = GenerateClassDefinition(entityNode);

                // Get the entity name from the inner entity node
                XmlNode innerEntityNode = entityNode.SelectSingleNode("EntityInfo/entity");
                if (innerEntityNode == null)
                {
                    throw new InvalidOperationException("Inner entity node not found.");
                }

                XmlNode localizedNameNode = innerEntityNode.SelectSingleNode("LocalizedNames/LocalizedName");
                string entityName = localizedNameNode?.Attributes["description"]?.Value;
                string entityLogicalName = innerEntityNode.Attributes["Name"]?.Value.ToLower();
                Console.WriteLine($"Processing {entityName}");
                if (entityName == null)
                {
                    entityName = innerEntityNode.Attributes["Name"]?.Value;
                }

                // Write class to file or use as needed
                string outputPath = Path.Combine("C:\\Users\\Lenovo\\OneDrive\\Documents", "GeneratedClasses");
                Directory.CreateDirectory(outputPath); // Ensure the directory exists
                File.WriteAllText(Path.Combine(outputPath, $"{entityName}.cs"), classDefinition);

            }
        }


        private static string GenerateClassDefinition(XmlNode entityNode)
        {
            if (entityNode == null)
            {
                throw new ArgumentNullException(nameof(entityNode));
            }

            XmlNode entityInfoNode = entityNode.SelectSingleNode("EntityInfo");
            if (entityInfoNode == null)
            {
                throw new InvalidOperationException("EntityInfo node not found.");
            }

            XmlNode innerEntityNode = entityInfoNode.SelectSingleNode("entity");
            if (innerEntityNode == null)
            {
                throw new InvalidOperationException("Inner entity node not found.");
            }

            XmlNode localizedNameNode = innerEntityNode.SelectSingleNode("LocalizedNames/LocalizedName");
            string entityName = localizedNameNode?.Attributes["description"]?.Value;
            string entityLogicalName = innerEntityNode.Attributes["Name"]?.Value.ToLower();
            Console.WriteLine($"Processing {entityName}");
            if (entityName == null)
            {
                entityName = innerEntityNode.Attributes["Name"]?.Value;
            }

            StringBuilder classDefinition = new StringBuilder();
            classDefinition.AppendLine("using static OptionValueSets;");
            classDefinition.AppendLine("");
            classDefinition.AppendLine($"public class {entityName}"); // Replaced className with entityName
            classDefinition.AppendLine("{");
            classDefinition.AppendLine("    private IOrganizationService _service;");
            classDefinition.AppendLine("    public Guid Id { get; private set; }");

            XmlNodeList attributes = entityNode.SelectNodes("EntityInfo/entity/attributes/attribute");
            foreach (XmlNode attribute in attributes)
            {
                string logicalName = attribute.SelectSingleNode("LogicalName")?.InnerText;
                string displayName = SanitizeString(attribute.SelectSingleNode("displaynames/displayname[@languagecode='1033']")?.Attributes["description"]?.Value);
                LocalizedNameToLogicalNameMap[displayName] = logicalName;
                string attributeType = attribute.SelectSingleNode("Type")?.InnerText;
                string propertyType = ConvertAttributeTypeToCSharpType(attribute); // Custom method to map attribute type to C# type

                if (displayName != null && propertyType != null)
                {
                    classDefinition.AppendLine($"    public {propertyType} {displayName} {{ get; set; }}");
                }
            }

            classDefinition.AppendLine($"    public {entityName}(IOrganizationService service)"); // Replaced className with entityName
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        this._service = service;");
            classDefinition.AppendLine("    }");

            // Create method
            classDefinition.AppendLine("    public void Create()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine($"        Entity entity = new Entity({entityLogicalName});");
            foreach (XmlNode attribute in attributes)
            {
                string displayName = SanitizeString(attribute.SelectSingleNode("displaynames/displayname[@languagecode='1033']")?.Attributes["description"]?.Value);
                string logicalName = LocalizedNameToLogicalNameMap[displayName];
                classDefinition.AppendLine($"        entity[\"{logicalName}\"] = this.{displayName};");
            }
            classDefinition.AppendLine("        this.Id = _service.Create(entity);");
            classDefinition.AppendLine("    }");


            // Update method
            classDefinition.AppendLine("    public void Update()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine($"        Entity entity = new Entity({entityLogicalName})" + " { Id = this.Id };");
            foreach (XmlNode attribute in attributes)
            {
                string displayName = SanitizeString(attribute.SelectSingleNode("displaynames/displayname[@languagecode='1033']")?.Attributes["description"]?.Value);
                string logicalName = LocalizedNameToLogicalNameMap[displayName]; // Use the mapping
                classDefinition.AppendLine($"        if (this.{displayName} != null) entity[\"{logicalName}\"] = this.{displayName};");
            }
            classDefinition.AppendLine("        _service.Update(entity);");
            classDefinition.AppendLine("    }");


            // Delete method
            classDefinition.AppendLine("    public void Delete()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine($"        _service.Delete({entityLogicalName}, this.Id);");
            classDefinition.AppendLine("    }");


            classDefinition.AppendLine("}");

            return classDefinition.ToString();
        }


        private static string ConvertAttributeTypeToCSharpType(XmlNode attributeNode)
        {
            string type = attributeNode.SelectSingleNode("Type")?.InnerText;
            switch (type)
            {
                //todo
                case "partylist":
                    return "object";
                case "state":
                    return "object";
                case "status":
                    return "object";

                case "money":
                    return "decimal";
                case "int":
                    return "int";
                case "uniqueidentifier":
                    return "Guid";
                case "primarykey":
                    return "Guid";
                case "owner":
                    return "Guid";
                case "datetime":
                    return "DateTime";
                case "lookup":
                    return "Guid";
                case "decimal":
                    return "decimal";
                case "bit":
                    return "bool";
                case "nvarchar":
                    return "string";
                case "ntext":
                    return "string";
                case "bool":
                    return type;
                case "picklist":
                    string key = attributeNode.SelectSingleNode("OptionSetName")?.InnerText;
                    string optionSetName;

                    if (key == null)
                        return null;

                    if (LogicalNameToEnumMap.TryGetValue(key, out optionSetName))
                    {
                        // Value found for the key
                    }
                    else
                    {
                        // Value not found, so use the key itself
                        optionSetName = key;
                    };
                    return optionSetName; // Assuming the enum is named the same as the OptionSetName
                                          // Other type mappings
                                          // ...
            }
            return "object"; // Default type if no mapping found
        }

    }
}
