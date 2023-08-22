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
        private static Dictionary<string, string> StateMap = new Dictionary<string, string>();
        private static Dictionary<string, string> StatusReasonMap = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            string zipPath = "C:\\Users\\EricL\\Downloads\\CRMBase_1_1_0_8.zip"; // Provide the path to your zip file here
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

        private static string ProcessStateOptionSets(XmlDocument doc)
        {
            StringBuilder statusEnumsClass = new StringBuilder("");

            XmlNode statusAttribute = doc.SelectSingleNode("//attribute[@PhysicalName='statecode']/optionset");
            if (statusAttribute != null)
            {
                string optionSetName = statusAttribute.Attributes["Name"].Value;
                string enumName = optionSetName.Replace(" ", "");
                statusEnumsClass.AppendLine($"public enum StateCode\n{{");

                XmlNodeList states = statusAttribute.SelectNodes("states/state");
                foreach (XmlNode state in states)
                {
                    string value = state.Attributes["value"].Value;
                    string label = state.Attributes["invariantname"].Value;
                    statusEnumsClass.AppendLine($"    {label.Replace(" ", "")} = {value},");
                    StateMap.Add(value, label);
                }

                statusEnumsClass.AppendLine("}");
            }

            return statusEnumsClass.ToString();
        }

        //private static string ProcessStatusOptionSets(XmlDocument doc)
        //{
        //    StringBuilder statusEnumsClass = new StringBuilder("");
        //
        //    XmlNodeList statusAttributes = doc.SelectNodes("//attribute[@PhysicalName='statuscode']/optionset");
        //    foreach (XmlNode statusAttribute in statusAttributes)
        //    {
        //        if (statusAttribute != null)
        //        {
        //            string optionSetName = statusAttribute.Attributes["Name"].Value;
        //            string enumName = optionSetName.Replace(" ", "");
        //            statusEnumsClass.AppendLine($"public enum {enumName}\n{{");
        //
        //            XmlNodeList statuses = statusAttribute.SelectNodes("statuses/status");
        //            foreach (XmlNode status in statuses)
        //            {
        //                string value = status.Attributes["value"].Value;
        //                string label = status.SelectSingleNode("labels/label").Attributes["description"].Value;
        //                statusEnumsClass.AppendLine($"    {label.Replace(" ", "")} = {value},");
        //            }
        //
        //            statusEnumsClass.AppendLine("}");
        //        }
        //    }
        //
        //    return statusEnumsClass.ToString();
        //}

        private static void ProcessStatusOptionSets(XmlDocument doc)
        {
            StringBuilder statusEnumsClass = new StringBuilder();

            XmlNodeList entities = doc.SelectNodes("//Entities/Entity");
            foreach (XmlNode entityNode in entities)
            {
                string entityName = entityNode.SelectSingleNode("Name").InnerText;
                string localizedName = entityNode.SelectSingleNode("Name").Attributes["LocalizedName"].Value;

                XmlNodeList statusAttributes = entityNode.SelectNodes(".//attribute[@PhysicalName='statuscode']/optionset");
                foreach (XmlNode statusAttribute in statusAttributes)
                {
                    if (statusAttribute != null)
                    {
                        // Group statuses by state attribute
                        var groupedStatuses = statusAttribute.SelectNodes("statuses/status")
                            .Cast<XmlNode>()
                            .GroupBy(s => s.Attributes["state"].Value);

                        foreach (var group in groupedStatuses)
                        {
                            string stateName = StateMap[group.Key];
                            statusEnumsClass.AppendLine($"    public enum {stateName}StatusReason\n    {{");

                            foreach (XmlNode status in group)
                            {
                                string value = status.Attributes["value"].Value;
                                string label = status.SelectSingleNode("labels/label").Attributes["description"].Value;
                                statusEnumsClass.AppendLine($"        {label.Replace(" ", "")} = {value},");
                            }

                            statusEnumsClass.AppendLine("    }");
                        }
                    }

                    StatusReasonMap.Add(entityName.ToLower(), statusEnumsClass.ToString());
                    statusEnumsClass.Clear();
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

            optionValueSetsClass.Append(ProcessStateOptionSets(doc));
            ProcessStatusOptionSets(doc);
            optionValueSetsClass.AppendLine("}");

            string outputPath = Path.Combine("C:\\Users\\EricL\\OneDrive\\Documents", "GeneratedClasses");
            Directory.CreateDirectory(outputPath); // Ensure the directory exists
            File.WriteAllText(Path.Combine(outputPath, "OptionValueSets.cs"), optionValueSetsClass.ToString());
        }

        private static string SanitizeString(string s)
        {
            if (s == null) return null;

            // Remove or replace any characters that are not valid in C# identifiers
            StringBuilder sanitizedString = new StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sanitizedString.Append(c);
                }
                else
                {
                }
            }

            return sanitizedString.ToString();
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
                string outputPath = Path.Combine("C:\\Users\\EricL\\OneDrive\\Documents", "GeneratedClasses");
                Directory.CreateDirectory(outputPath); // Ensure the directory exists
                File.WriteAllText(Path.Combine(outputPath, $"{entityName}.cs"), classDefinition);

            }
        }

        private static string GenerateClassDefinition(XmlNode entityNode, string classpostfix = "Item")
        {
            classpostfix = SanitizeString(classpostfix);
            ValidateEntityNode(entityNode);

            XmlNode entityInfoNode = entityNode.SelectSingleNode("EntityInfo");
            XmlNode innerEntityNode = entityInfoNode.SelectSingleNode("entity");
            XmlNode localizedNameNode = innerEntityNode.SelectSingleNode("LocalizedNames/LocalizedName");
            string entityName = SanitizeString(localizedNameNode?.Attributes["description"]?.Value);
            string entityLogicalName = innerEntityNode.Attributes["Name"]?.Value.ToLower();
            Console.WriteLine($"Processing {entityName}");
            if (entityName == null)
            {
                entityName = SanitizeString(innerEntityNode.Attributes["Name"]?.Value);
            }

            StringBuilder classDefinition = new StringBuilder();
            AppendUsingsAndClassDeclaration(classDefinition, entityName, classpostfix, entityLogicalName);
            string primaryKeyColumn = ProcessAttributes(classDefinition, entityNode);

            AppendInitializationMethods(classDefinition, entityName, classpostfix);
            AppendCustomAttribute(classDefinition);
            AppendMappingMethod(classDefinition, entityLogicalName);
            AppendRetrieveMethod(classDefinition, entityLogicalName, primaryKeyColumn);
            AppendCreateMethod(classDefinition, primaryKeyColumn);
            AppendUpdateMethod(classDefinition, primaryKeyColumn);
            AppendDeleteMethod(classDefinition, entityLogicalName, primaryKeyColumn);

            if (StatusReasonMap.ContainsKey(entityLogicalName))
            {
                classDefinition.Append(StatusReasonMap[entityLogicalName]);
            }

            classDefinition.AppendLine("}");

            return classDefinition.ToString();
        }

        private static void ValidateEntityNode(XmlNode entityNode)
        {
            if (entityNode == null)
            {
                throw new ArgumentNullException(nameof(entityNode));
            }

            if (entityNode.SelectSingleNode("EntityInfo") == null)
            {
                throw new InvalidOperationException("EntityInfo node not found.");
            }

            if (entityNode.SelectSingleNode("EntityInfo/entity") == null)
            {
                throw new InvalidOperationException("Inner entity node not found.");
            }
        }

        private static void AppendUsingsAndClassDeclaration(StringBuilder classDefinition, string entityName, string classpostfix, string entityLogicalName)
        {
            classDefinition.AppendLine("using static OptionValueSets;");
            classDefinition.AppendLine("using Microsoft.Xrm.Sdk;");
            classDefinition.AppendLine("using Microsoft.Xrm.Sdk.Query;");
            classDefinition.AppendLine("using System;");
            classDefinition.AppendLine("using System.Reflection;");
            classDefinition.AppendLine("");
            classDefinition.AppendLine("");
            classDefinition.AppendLine($"public class {entityName}{classpostfix}"); // Replaced className with entityName
            classDefinition.AppendLine("{");
            classDefinition.AppendLine("    private IOrganizationService _service;");
            classDefinition.AppendLine($"    private string EntityLogicalName = \"{entityLogicalName}\";");
            classDefinition.AppendLine("");
        }

        private static string ProcessAttributes(StringBuilder classDefinition, XmlNode entityNode)
        {
            string primaryKeyColumn = string.Empty;

            XmlNodeList attributes = entityNode.SelectNodes("EntityInfo/entity/attributes/attribute");
            foreach (XmlNode attribute in attributes)
            {
                string logicalName = attribute.SelectSingleNode("LogicalName")?.InnerText;
                string displayName = SanitizeString(attribute.SelectSingleNode("displaynames/displayname[@languagecode='1033']")?.Attributes["description"]?.Value);
                LocalizedNameToLogicalNameMap[displayName] = logicalName;
                string attributeType = attribute.SelectSingleNode("Type")?.InnerText;
                string propertyType = ConvertAttributeTypeToCSharpType(attribute); // Custom method to map attribute type to C# type

                if (displayName == "Status")
                {
                    displayName = "State";
                    propertyType = "StateCode";
                }

                if (displayName != null && propertyType != null)
                {
                    if (displayName == "StatusReason")
                    {
                        ProcessStatusReasonAttribute(classDefinition, logicalName, propertyType);
                    }
                    else
                    {
                        if (propertyType == "primarykey")
                        {
                            displayName = "Id";
                            primaryKeyColumn = displayName;
                            propertyType = "Guid";
                        }

                        classDefinition.AppendLine($"    [LogicalName(\"{logicalName}\")]");
                        classDefinition.AppendLine($"    public {propertyType} {displayName} {{ get; set; }}");
                        classDefinition.AppendLine("");
                    }
                }
            }

            return primaryKeyColumn;

        }

        private static void ProcessStatusReasonAttribute(StringBuilder classDefinition, string logicalName, string propertyType)
        {
            propertyType = "int?";
            classDefinition.AppendLine($"    public {propertyType} StatusReason {{ get; set; }}");
            classDefinition.AppendLine("");

            // Add the StatusReasonEnum property
            classDefinition.AppendLine($"    [LogicalName(\"{logicalName}\")]");
            classDefinition.AppendLine("    public object StatusReasonEnum");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        get");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            if (State == StateCode.Active && StatusReason.HasValue)");
            classDefinition.AppendLine("            {");
            classDefinition.AppendLine("                return (ActiveStatusReason)StatusReason.Value;");
            classDefinition.AppendLine("            }");
            classDefinition.AppendLine("            else if (State == StateCode.Inactive && StatusReason.HasValue)");
            classDefinition.AppendLine("            {");
            classDefinition.AppendLine("                return (InactiveStatusReason)StatusReason.Value;");
            classDefinition.AppendLine("            }");
            classDefinition.AppendLine("            return null;");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine("        set");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            if (State == StateCode.Active && value is ActiveStatusReason activeStatusReason)");
            classDefinition.AppendLine("            {");
            classDefinition.AppendLine("                StatusReason = (int)activeStatusReason;");
            classDefinition.AppendLine("            }");
            classDefinition.AppendLine("            else if (State == StateCode.Inactive && value is InactiveStatusReason inactiveStatusReason)");
            classDefinition.AppendLine("            {");
            classDefinition.AppendLine("                StatusReason = (int)inactiveStatusReason;");
            classDefinition.AppendLine("            }");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine("");
        }

        private static void AppendInitializationMethods(StringBuilder classDefinition, string entityName, string classpostfix)
        {
            //Initialization Method
            classDefinition.AppendLine("");
            classDefinition.AppendLine($"    public {entityName}{classpostfix}(IOrganizationService service)"); // Replaced className with entityName
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        this._service = service;");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine("");
        }

        private static void AppendCustomAttribute(StringBuilder classDefinition)
        {
            //Define Custom Attribute
            classDefinition.AppendLine("    [AttributeUsage(AttributeTargets.Property)]");
            classDefinition.AppendLine("    public class LogicalNameAttribute : Attribute");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        public string Name { get; }");
            classDefinition.AppendLine("        public LogicalNameAttribute(string name)");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            Name = name;");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine("");
        }

        private static void AppendMappingMethod(StringBuilder classDefinition, string entityLogicalName)
        {
            // Mapping method
            classDefinition.AppendLine("    private Entity MapPropertiesToEntity()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine($"        Entity entity = new Entity(EntityLogicalName);");
            classDefinition.AppendLine("        PropertyInfo[] properties = GetType().GetProperties();");
            classDefinition.AppendLine("        foreach (var property in properties)");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            var value = property.GetValue(this);");
            classDefinition.AppendLine("            if (value != null)");
            classDefinition.AppendLine("            {");
            classDefinition.AppendLine("                if (value is Guid guidValue && guidValue == Guid.Empty) continue;");
            classDefinition.AppendLine("                if (value is DateTime dateTimeValue && dateTimeValue == DateTime.MinValue) continue;");
            classDefinition.AppendLine("                if (value.GetType().IsEnum)");
            classDefinition.AppendLine("                {");
            classDefinition.AppendLine("                    value = new OptionSetValue((int)value);");
            classDefinition.AppendLine("                }");
            classDefinition.AppendLine("                var logicalNameAttribute = property.GetCustomAttribute<LogicalNameAttribute>();");
            classDefinition.AppendLine("                if (logicalNameAttribute != null)");
            classDefinition.AppendLine("                {");
            classDefinition.AppendLine("                    if (property.PropertyType == typeof(Party[]))");
            classDefinition.AppendLine("                    {");
            classDefinition.AppendLine("                        var parties = (Party[])value;");
            classDefinition.AppendLine("                        EntityCollection partyList = new EntityCollection();");
            classDefinition.AppendLine("                        foreach (var party in parties)");
            classDefinition.AppendLine("                        {");
            classDefinition.AppendLine("                            Entity partyEntity = new Entity(\"activityparty\");");
            classDefinition.AppendLine("                            partyEntity[\"partyid\"] = new EntityReference(party.EntityType, party.Id);");
            classDefinition.AppendLine("                            partyList.Entities.Add(partyEntity);");
            classDefinition.AppendLine("                        }");
            classDefinition.AppendLine($"                        entity[logicalNameAttribute.Name] = partyList;");
            classDefinition.AppendLine("                    }");
            classDefinition.AppendLine("                    else");
            classDefinition.AppendLine("                    {");
            classDefinition.AppendLine("                        entity[logicalNameAttribute.Name] = value;");
            classDefinition.AppendLine("                    }");
            classDefinition.AppendLine("                }");
            classDefinition.AppendLine("            }");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine("        return entity;");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine("");
        }

        private static void AppendRetrieveMethod(StringBuilder classDefinition, string entityLogicalName, string primaryKeyColumn)
        {
            //Retrieve Method
            classDefinition.AppendLine("    public void Retrieve(Guid id)");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine($"        Entity entity = _service.Retrieve(\"{entityLogicalName}\", id, new ColumnSet(true));"); // Retrieve all columns
            classDefinition.AppendLine("        PropertyInfo[] properties = GetType().GetProperties();");
            classDefinition.AppendLine("        foreach (var property in properties)");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            var logicalNameAttribute = property.GetCustomAttribute<LogicalNameAttribute>();");
            classDefinition.AppendLine("            if (logicalNameAttribute != null)");
            classDefinition.AppendLine("            {");
            classDefinition.AppendLine("                var attributeValue = entity[logicalNameAttribute.Name];");
            classDefinition.AppendLine("                if (attributeValue != null)");
            classDefinition.AppendLine("                {");
            classDefinition.AppendLine("                    property.SetValue(this, attributeValue);");
            classDefinition.AppendLine("                }");
            classDefinition.AppendLine("            }");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine($"        this.{primaryKeyColumn} = id;"); // Set the ID
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine("");
        }

        private static void AppendCreateMethod(StringBuilder classDefinition, string primaryKeyColumn)
        {
            // Create method
            classDefinition.AppendLine("    public void Create()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        Entity entity = MapPropertiesToEntity();");
            classDefinition.AppendLine($"        this.{primaryKeyColumn} = _service.Create(entity);");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine("");
        }

        private static void AppendUpdateMethod(StringBuilder classDefinition, string primaryKeyColumn)
        {
            // Update method
            classDefinition.AppendLine("    public void Update()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine($"        Entity entity = MapPropertiesToEntity();");
            classDefinition.AppendLine($"        entity.Id = this.{primaryKeyColumn};");
            classDefinition.AppendLine("        _service.Update(entity);");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine("");
        }

        private static void AppendDeleteMethod(StringBuilder classDefinition, string entityLogicalName, string primaryKeyColumn)
        {
            // Delete method (No changes needed)
            classDefinition.AppendLine("    public void Delete()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine($"        _service.Delete(\"{entityLogicalName}\", this.{primaryKeyColumn});");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine("");
        }


        private static string ConvertAttributeTypeToCSharpType(XmlNode attributeNode)
        {
            string type = attributeNode.SelectSingleNode("Type")?.InnerText;
            switch (type)
            {
                case "multiselectpicklist":
                case "customer":
                    return "object";
                case "partylist":
                    return "Party[]";
                case "status":
                    return "StatusReason?";
                case "state":
                    return "Status?";
                case "money":
                case "decimal":
                    return "decimal";
                case "int":
                    return "int";
                case "uniqueidentifier":
                    return "Guid";
                case "primarykey":
                    return "primarykey";
                case "owner":
                case "lookup":
                    return "Guid";
                case "datetime":
                    return "DateTime";
                case "bit":
                    return "bool";
                case "nvarchar":
                case "ntext":
                    return "string";
                case "bool":
                    return type;
                case "picklist":
                    string key = attributeNode.SelectSingleNode("OptionSetName")?.InnerText;
                    return LogicalNameToEnumMap.TryGetValue(key, out string optionSetName) ? optionSetName : "int";
            }
            return "object";
        }

    }
}
