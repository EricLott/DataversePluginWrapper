using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;

namespace DataverseWrapper
{
    class Program
    {
        // Maps and counters
        private static readonly Dictionary<string, string> LogicalNameToEnumMap = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> StateMap = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> StatusReasonMap = new Dictionary<string, string>();
        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const","continue",
            "decimal","default","delegate","do","double","else","enum","event","explicit","extern","false","finally",
            "fixed","float","for","foreach","goto","if","implicit","in","int","interface","internal","is","lock",
            "long","namespace","new","null","object","operator","out","override","params","private","protected",
            "public","readonly","ref","return","sbyte","sealed","short","sizeof","stackalloc","static","string",
            "struct","switch","this","throw","true","try","typeof","uint","ulong","unchecked","unsafe","ushort",
            "using","virtual","void","volatile","while"
        };

        private static int _entitiesGenerated = 0;
        private static int _enumsGenerated = 0;
        private static int _optionSetsProcessed = 0;
        private static bool _verbose = false;
        private static bool _overwrite = false;
        private static string _filterContains = null;

        static int Main(string[] args)
        {
            Console.Title = "Dataverse Wrapper Generator";
            PrintBanner();

            // Basic CLI parsing with friendly defaults
            if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
            {
                PrintHelp();
                return 0;
            }

            string zipPath = GetArg(args, "-z", "--zip");
            string outDir = GetArg(args, "-o", "--out");
            _verbose = args.Contains("-v") || args.Contains("--verbose");
            _overwrite = args.Contains("-y") || args.Contains("--yes");
            _filterContains = GetArg(args, "-f", "--filter"); // case-insensitive contains on entity display name

            if (string.IsNullOrWhiteSpace(zipPath))
            {
                LogError("Missing required -z|--zip argument to point at a Dataverse solution zip.");
                PrintHelp();
                return 2;
            }

            if (!File.Exists(zipPath))
            {
                LogError($"Zip not found: {zipPath}");
                return 2;
            }

            // Default output folder to ./GeneratedClasses_YYYYMMdd_HHmmss
            if (string.IsNullOrWhiteSpace(outDir))
            {
                outDir = Path.Combine(Environment.CurrentDirectory, $"GeneratedClasses_{DateTime.Now:yyyyMMdd_HHmmss}");
            }

            try
            {
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    LogWarn("Canceled by user.");
                    Environment.Exit(130);
                };

                EnsureDirectory(outDir);

                var sw = Stopwatch.StartNew();
                LogInfo($"Reading customizations from: {zipPath}");
                ReadCustomizationsXml(zipPath, outDir);

                sw.Stop();
                Console.WriteLine();
                WriteHr();
                LogSuccess("Done");
                Console.WriteLine($"Time: {sw.Elapsed}");
                Console.WriteLine($"Entities generated:  {_entitiesGenerated}");
                Console.WriteLine($"OptionSets processed: {_optionSetsProcessed}");
                Console.WriteLine($"Enums generated:      {_enumsGenerated}");
                Console.WriteLine($"Output directory:     {outDir}");
                WriteHr();

                return 0;
            }
            catch (XmlException xe)
            {
                LogError($"XML parsing error: {xe.Message}");
                return 3;
            }
            catch (InvalidOperationException ioe)
            {
                LogError($"Invalid data: {ioe.Message}");
                return 4;
            }
            catch (Exception ex)
            {
                LogError($"Unexpected error: {ex.Message}");
                return 1;
            }
        }

        private static void ReadCustomizationsXml(string zipPath, string outputRoot)
        {
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                var customizations = archive.Entries
                    .FirstOrDefault(e => e.FullName.EndsWith("customizations.xml", StringComparison.OrdinalIgnoreCase));

                if (customizations == null)
                    throw new FileNotFoundException("customizations.xml not found in zip.");

                using (StreamReader reader = new StreamReader(customizations.Open()))
                {
                    XmlDocument doc = new XmlDocument();
                    doc.PreserveWhitespace = false;
                    doc.Load(reader);

                    // Generate OptionValueSets first so entity classes can "using static OptionValueSets;"
                    ProcessOptionSets(doc, outputRoot);

                    // Then entities
                    ProcessEntities(doc, outputRoot);
                }
            }
        }

        private static string ProcessStateOptionSets(XmlDocument doc)
        {
            var statusEnumsClass = new StringBuilder();
            var usedMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // State enum (Active/Inactive)
            XmlNode statusAttribute = doc.SelectSingleNode("//attribute[@PhysicalName='statecode']/optionset");
            if (statusAttribute != null)
            {
                string enumName = "StateCode";
                statusEnumsClass.AppendLine($"public enum {enumName}");
                statusEnumsClass.AppendLine("{");

                XmlNodeList states = statusAttribute.SelectNodes("states/state");
                foreach (XmlNode state in states)
                {
                    string value = state.Attributes["value"].Value;
                    string label = state.Attributes["invariantname"].Value;
                    string memberName = MakeUniqueIdentifier(SanitizeString(label), usedMembers);
                    statusEnumsClass.AppendLine($"    {memberName} = {value},");
                    // Add or update state value text
                    StateMap[value] = label;
                }

                statusEnumsClass.AppendLine("}");
                _enumsGenerated++;
            }

            return statusEnumsClass.ToString();
        }

        private static void ProcessStatusOptionSets(XmlDocument doc)
        {
            // Build per-entity status reason enums grouped by state
            XmlNodeList entities = doc.SelectNodes("//Entities/Entity");
            if (entities == null) return;

            foreach (XmlNode entityNode in entities.Cast<XmlNode>())
            {
                string entityName = entityNode.SelectSingleNode("Name")?.InnerText;
                if (string.IsNullOrWhiteSpace(entityName)) continue;

                var statusEnumsClass = new StringBuilder();
                var usedEnumNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                XmlNodeList statusAttributes = entityNode.SelectNodes(".//attribute[@PhysicalName='statuscode']/optionset");
                foreach (XmlNode statusAttribute in statusAttributes.Cast<XmlNode>())
                {
                    var statuses = statusAttribute.SelectNodes("statuses/status").Cast<XmlNode>();
                    var grouped = statuses
                        .Where(s => s.Attributes?["state"] != null)
                        .GroupBy(s => s.Attributes["state"].Value);

                    foreach (var group in grouped)
                    {
                        // StateMap should be ready from ProcessStateOptionSets
                        if (!StateMap.TryGetValue(group.Key, out var stateName))
                        {
                            stateName = "Unknown";
                        }

                        string enumName = MakeUniqueIdentifier($"{SanitizeString(stateName)}StatusReason", usedEnumNames);
                        statusEnumsClass.AppendLine($"    public enum {enumName}");
                        statusEnumsClass.AppendLine("    {");
                        var usedMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var status in group)
                        {
                            string value = status.Attributes["value"].Value;
                            string label = status.SelectSingleNode("labels/label")?.Attributes?["description"]?.Value ?? $"Value_{value}";
                            string memberName = MakeUniqueIdentifier(SanitizeString(label), usedMembers);
                            statusEnumsClass.AppendLine($"        {memberName} = {value},");
                        }
                        statusEnumsClass.AppendLine("    }");
                        _enumsGenerated++;
                    }
                }

                if (statusEnumsClass.Length > 0)
                {
                    StatusReasonMap[entityName.ToLowerInvariant()] = statusEnumsClass.ToString();
                }
            }
        }

        private static void ProcessOptionSets(XmlDocument doc, string outputRoot)
        {
            var sb = new StringBuilder();
            sb.AppendLine("public static class OptionValueSets");
            sb.AppendLine("{");

            XmlNodeList optionSets = doc.SelectNodes("//optionsets/optionset");
            foreach (XmlNode optionSet in optionSets.Cast<XmlNode>())
            {
                string optionSetName = optionSet.Attributes?["localizedName"]?.Value
                                       ?? optionSet.Attributes?["Name"]?.Value
                                       ?? "UnnamedOptions";
                string optionSetLogicalName = optionSet.Attributes?["Name"]?.Value ?? optionSetName;
                string enumName = SanitizeString(optionSetName);

                LogicalNameToEnumMap[optionSetLogicalName] = enumName;
                string enumDefinition = GenerateEnumDefinition(optionSet, enumName);
                sb.AppendLine(enumDefinition);

                _optionSetsProcessed++;
                _enumsGenerated++;
            }

            // State + Status reason enums
            sb.Append(ProcessStateOptionSets(doc));
            ProcessStatusOptionSets(doc);

            sb.AppendLine("}");

            string outDir = Path.Combine(outputRoot, "OptionSets");
            EnsureDirectory(outDir);
            var outfile = Path.Combine(outDir, "OptionValueSets.cs");
            SafeWriteAllText(outfile, sb.ToString());
            LogInfo($"Wrote: {outfile}");
        }

        private static string SanitizeString(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "Unnamed";

            var sanitized = new StringBuilder();
            // If first char is digit, prefix with underscore for valid identifier
            if (char.IsDigit(s[0])) sanitized.Append('_');

            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sanitized.Append(c);
                else if (c == '_' || c == '-') sanitized.Append('_'); // normalize hyphens to underscores
                // skip other chars
            }

            var text = sanitized.ToString();
            if (string.IsNullOrWhiteSpace(text)) return "Unnamed";
            if (CSharpKeywords.Contains(text)) return $"_{text}";
            return text;
        }

        private static string GenerateEnumDefinition(XmlNode optionSet, string enumName)
        {
            var enumDefinition = new StringBuilder($"    public enum {enumName}\n    {{\n");
            var usedMembers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            XmlNodeList options = optionSet.SelectNodes("options/option");
            foreach (XmlNode option in options.Cast<XmlNode>())
            {
                string label = option.SelectSingleNode("labels/label")?.Attributes?["description"]?.Value ?? "Unnamed";
                string value = option.Attributes?["value"]?.Value ?? "0";
                string memberName = MakeUniqueIdentifier(SanitizeString(label), usedMembers);
                enumDefinition.AppendLine($"        {memberName} = {value},");
            }

            enumDefinition.AppendLine("    }");
            return enumDefinition.ToString();
        }

        private static void ProcessEntities(XmlDocument doc, string outputRoot)
        {
            // Entity list
            XmlNodeList entities = doc.SelectNodes("//Entity");
            if (entities == null || entities.Count == 0)
            {
                LogWarn("No <Entity> nodes found.");
                return;
            }

            // Optional filter by display name contains
            foreach (XmlNode entityNode in entities.Cast<XmlNode>())
            {
                // EntityInfo/entity/LocalizedNames/LocalizedName @description is the friendly name
                string displayName = entityNode.SelectSingleNode("EntityInfo/entity/LocalizedNames/LocalizedName")
                    ?.Attributes?["description"]?.Value;

                if (!string.IsNullOrEmpty(_filterContains) &&
                    (displayName == null || displayName.IndexOf(_filterContains, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    // skip if filter provided and doesn't match
                    continue;
                }

                string classDefinition = GenerateClassDefinition(entityNode);

                // Resolve entity name for filename
                XmlNode innerEntityNode = entityNode.SelectSingleNode("EntityInfo/entity")
                                          ?? throw new InvalidOperationException("Inner entity node not found.");
                string entityName = displayName ?? innerEntityNode.Attributes?["Name"]?.Value ?? "UnnamedEntity";
                string fileSafeName = SanitizeString(entityName);

                string outDir = Path.Combine(outputRoot, "Entities");
                EnsureDirectory(outDir);

                string outfile = Path.Combine(outDir, $"{fileSafeName}.cs");
                SafeWriteAllText(outfile, classDefinition);
                LogInfo($"Wrote: {outfile}");
                _entitiesGenerated++;
            }

            if (_entitiesGenerated == 0 && !string.IsNullOrEmpty(_filterContains))
            {
                LogWarn($"No entities matched filter: \"{_filterContains}\"");
            }
        }

        private static string GenerateClassDefinition(XmlNode entityNode, string classpostfix = "Item")
        {
            classpostfix = SanitizeString(classpostfix);
            ValidateEntityNode(entityNode);

            XmlNode entityInfoNode = entityNode.SelectSingleNode("EntityInfo");
            XmlNode innerEntityNode = entityInfoNode.SelectSingleNode("entity");
            string entityName = SanitizeString(innerEntityNode.SelectSingleNode("LocalizedNames/LocalizedName")
                ?.Attributes?["description"]?.Value)
                ?? SanitizeString(innerEntityNode.Attributes?["Name"]?.Value) ?? "UnnamedEntity";
            string entityLogicalName = innerEntityNode.Attributes?["Name"]?.Value?.ToLowerInvariant() ?? "unnamed";

            var classDefinition = new StringBuilder();
            AppendUsingsAndClassDeclaration(classDefinition, entityName, classpostfix, entityLogicalName);
            string primaryKeyColumn = ProcessAttributes(classDefinition, entityNode);

            AppendInitializationMethods(classDefinition, entityName, classpostfix);
            AppendCustomAttribute(classDefinition);
            AppendMappingMethod(classDefinition);
            AppendValueConversionMethods(classDefinition);
            AppendRetrieveMethod(classDefinition, entityLogicalName, primaryKeyColumn);
            AppendCreateMethod(classDefinition, primaryKeyColumn);
            AppendUpdateMethod(classDefinition, primaryKeyColumn);
            AppendDeleteMethod(classDefinition, entityLogicalName, primaryKeyColumn);

            if (StatusReasonMap.TryGetValue(entityLogicalName, out var statusEnums))
            {
                classDefinition.AppendLine(statusEnums);
            }

            classDefinition.AppendLine("}");
            return classDefinition.ToString();
        }

        private static void ValidateEntityNode(XmlNode entityNode)
        {
            if (entityNode == null) throw new ArgumentNullException(nameof(entityNode));
            if (entityNode.SelectSingleNode("EntityInfo") == null)
                throw new InvalidOperationException("EntityInfo node not found.");
            if (entityNode.SelectSingleNode("EntityInfo/entity") == null)
                throw new InvalidOperationException("Inner entity node not found.");
        }

        private static void AppendUsingsAndClassDeclaration(StringBuilder classDefinition, string entityName, string classpostfix, string entityLogicalName)
        {
            classDefinition.AppendLine("using static OptionValueSets;");
            classDefinition.AppendLine("using Microsoft.Xrm.Sdk;");
            classDefinition.AppendLine("using Microsoft.Xrm.Sdk.Query;");
            classDefinition.AppendLine("using System;");
            classDefinition.AppendLine("using System.Reflection;");
            classDefinition.AppendLine();
            classDefinition.AppendLine($"public class {entityName}{classpostfix}");
            classDefinition.AppendLine("{");
            classDefinition.AppendLine("    private IOrganizationService _service;");
            classDefinition.AppendLine($"    private string EntityLogicalName = \"{entityLogicalName}\";");
            classDefinition.AppendLine();
        }

        private static string ProcessAttributes(StringBuilder classDefinition, XmlNode entityNode)
        {
            string primaryKeyColumn = "Id";
            var usedPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            XmlNodeList attributes = entityNode.SelectNodes("EntityInfo/entity/attributes/attribute");
            foreach (XmlNode attribute in attributes.Cast<XmlNode>())
            {
                string logicalName = attribute.SelectSingleNode("LogicalName")?.InnerText;
                string displayName = SanitizeString(attribute.SelectSingleNode("displaynames/displayname[@languagecode='1033']")?.Attributes?["description"]?.Value);
                string rawType = attribute.SelectSingleNode("Type")?.InnerText?.ToLowerInvariant() ?? string.Empty;
                string propertyType = ConvertAttributeTypeToCSharpType(attribute);
                bool isStateCode = string.Equals(logicalName, "statecode", StringComparison.OrdinalIgnoreCase) || rawType == "state";
                bool isStatusCode = string.Equals(logicalName, "statuscode", StringComparison.OrdinalIgnoreCase) || rawType == "status";

                if (isStateCode)
                {
                    displayName = "State";
                    propertyType = "StateCode";
                }

                if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(propertyType))
                {
                    string propertyName = MakeUniqueIdentifier(displayName, usedPropertyNames);
                    if (isStatusCode)
                    {
                        ProcessStatusReasonAttribute(classDefinition, logicalName, rawType);
                    }
                    else
                    {
                        if (propertyType == "primarykey")
                        {
                            propertyName = MakeUniqueIdentifier("Id", usedPropertyNames);
                            primaryKeyColumn = propertyName;
                            propertyType = "Guid";
                        }

                        classDefinition.AppendLine($"    [LogicalName(\"{logicalName}\", \"{rawType}\")]");
                        classDefinition.AppendLine($"    public {propertyType} {propertyName} {{ get; set; }}");
                        classDefinition.AppendLine();
                    }
                }
            }

            return primaryKeyColumn;
        }

        private static void ProcessStatusReasonAttribute(StringBuilder classDefinition, string logicalName, string rawType)
        {
            // Backing int? + smart enum proxy
            classDefinition.AppendLine($"    [LogicalName(\"{logicalName}\", \"{rawType}\")]");
            classDefinition.AppendLine("    public int? StatusReason { get; set; }");
            classDefinition.AppendLine();
            classDefinition.AppendLine("    public object StatusReasonEnum");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        get");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            if (!StatusReason.HasValue) return null;");
            classDefinition.AppendLine("            var stateProperty = GetType().GetProperty(\"State\");");
            classDefinition.AppendLine("            var stateValue = stateProperty?.GetValue(this);");
            classDefinition.AppendLine("            if (stateValue == null) return StatusReason.Value;");
            classDefinition.AppendLine("            var enumType = GetType().GetNestedType($\"{stateValue}StatusReason\", BindingFlags.Public);");
            classDefinition.AppendLine("            if (enumType == null) return StatusReason.Value;");
            classDefinition.AppendLine("            return Enum.ToObject(enumType, StatusReason.Value);");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine("        set");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            if (value == null) { StatusReason = null; return; }");
            classDefinition.AppendLine("            if (value is int i) { StatusReason = i; return; }");
            classDefinition.AppendLine("            if (value.GetType().IsEnum) { StatusReason = (int)value; }");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine();
        }

        private static void AppendInitializationMethods(StringBuilder classDefinition, string entityName, string classpostfix)
        {
            classDefinition.AppendLine($"    public {entityName}{classpostfix}(IOrganizationService service)");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        _service = service;");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine();
        }

        private static void AppendCustomAttribute(StringBuilder classDefinition)
        {
            classDefinition.AppendLine("    [AttributeUsage(AttributeTargets.Property)]");
            classDefinition.AppendLine("    public class LogicalNameAttribute : Attribute");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        public string Name { get; }");
            classDefinition.AppendLine("        public string AttributeType { get; }");
            classDefinition.AppendLine("        public string TargetEntity { get; }");
            classDefinition.AppendLine("        public LogicalNameAttribute(string name, string attributeType = null, string targetEntity = null)");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            Name = name;");
            classDefinition.AppendLine("            AttributeType = attributeType;");
            classDefinition.AppendLine("            TargetEntity = targetEntity;");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine();
        }

        private static void AppendMappingMethod(StringBuilder classDefinition)
        {
            classDefinition.AppendLine("    private Entity MapPropertiesToEntity()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        Entity entity = new Entity(EntityLogicalName);");
            classDefinition.AppendLine("        PropertyInfo[] properties = GetType().GetProperties();");
            classDefinition.AppendLine("        foreach (var property in properties)");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            var logical = property.GetCustomAttribute<LogicalNameAttribute>();");
            classDefinition.AppendLine("            if (logical == null) continue;");
            classDefinition.AppendLine("            var value = property.GetValue(this);");
            classDefinition.AppendLine("            if (value == null) continue;");
            classDefinition.AppendLine("            if (property.PropertyType == typeof(Party[]))");
            classDefinition.AppendLine("            {");
                classDefinition.AppendLine("                var parties = (Party[])value;");
                classDefinition.AppendLine("                EntityCollection partyList = new EntityCollection();");
            classDefinition.AppendLine("                foreach (var party in parties)");
            classDefinition.AppendLine("                {");
            classDefinition.AppendLine("                    Entity p = new Entity(\"activityparty\");");
            classDefinition.AppendLine("                    p[\"partyid\"] = new EntityReference(party.EntityType, party.Id);");
            classDefinition.AppendLine("                    partyList.Entities.Add(p);");
                classDefinition.AppendLine("                }");
                classDefinition.AppendLine("                entity[logical.Name] = partyList;");
            classDefinition.AppendLine("            }");
            classDefinition.AppendLine("            else");
            classDefinition.AppendLine("            {");
            classDefinition.AppendLine("                var mapped = ConvertToDataverseValue(value, property.PropertyType, logical);");
            classDefinition.AppendLine("                if (mapped != null) entity[logical.Name] = mapped;");
            classDefinition.AppendLine("            }");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine("        return entity;");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine();
        }

        private static void AppendValueConversionMethods(StringBuilder classDefinition)
        {
            classDefinition.AppendLine("    private object ConvertToDataverseValue(object value, Type propertyType, LogicalNameAttribute logical)");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        if (value == null) return null;");
            classDefinition.AppendLine("        if (value is Guid g && g == Guid.Empty) return null;");
            classDefinition.AppendLine("        if (value is DateTime dt && dt == DateTime.MinValue) return null;");
            classDefinition.AppendLine("        if (value.GetType().IsEnum) return new OptionSetValue((int)value);");
            classDefinition.AppendLine("        if ((logical.AttributeType == \"state\" || logical.AttributeType == \"status\" || logical.AttributeType == \"picklist\") && value is int i)");
            classDefinition.AppendLine("            return new OptionSetValue(i);");
            classDefinition.AppendLine("        if (logical.AttributeType == \"money\" && value is decimal d)");
            classDefinition.AppendLine("            return new Money(d);");
            classDefinition.AppendLine("        return value;");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine();
            classDefinition.AppendLine("    private object ConvertFromDataverseValue(object value, Type targetType)");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        if (value is AliasedValue alias) value = alias.Value;");
            classDefinition.AppendLine("        if (value == null) return null;");
            classDefinition.AppendLine("        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;");
            classDefinition.AppendLine("        if (value is OptionSetValue osv)");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            if (underlying.IsEnum) return Enum.ToObject(underlying, osv.Value);");
            classDefinition.AppendLine("            if (underlying == typeof(int)) return osv.Value;");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine("        if (value is Money money && underlying == typeof(decimal)) return money.Value;");
            classDefinition.AppendLine("        if (value is EntityReference er)");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            if (underlying == typeof(Guid)) return er.Id;");
            classDefinition.AppendLine("            if (underlying == typeof(string)) return er.Name;");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine("        if (underlying.IsEnum && value is int intValue) return Enum.ToObject(underlying, intValue);");
            classDefinition.AppendLine("        return value;");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine();
        }

        private static void AppendRetrieveMethod(StringBuilder classDefinition, string entityLogicalName, string primaryKeyColumn)
        {
            classDefinition.AppendLine("    public void Retrieve(Guid id)");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine($"        Entity entity = _service.Retrieve(\"{entityLogicalName}\", id, new ColumnSet(true));");
            classDefinition.AppendLine("        PropertyInfo[] properties = GetType().GetProperties();");
            classDefinition.AppendLine("        foreach (var property in properties)");
            classDefinition.AppendLine("        {");
            classDefinition.AppendLine("            var logical = property.GetCustomAttribute<LogicalNameAttribute>();");
            classDefinition.AppendLine("            if (logical == null) continue;");
            classDefinition.AppendLine("            if (!entity.Attributes.ContainsKey(logical.Name)) continue;");
            classDefinition.AppendLine("            var attributeValue = entity[logical.Name];");
            classDefinition.AppendLine("            var convertedValue = ConvertFromDataverseValue(attributeValue, property.PropertyType);");
            classDefinition.AppendLine("            if (convertedValue == null && property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) == null) continue;");
            classDefinition.AppendLine("            property.SetValue(this, convertedValue);");
            classDefinition.AppendLine("        }");
            classDefinition.AppendLine($"        this.{primaryKeyColumn} = id;");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine();
        }

        private static void AppendCreateMethod(StringBuilder classDefinition, string primaryKeyColumn)
        {
            classDefinition.AppendLine("    public void Create()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        Entity entity = MapPropertiesToEntity();");
            classDefinition.AppendLine($"        this.{primaryKeyColumn} = _service.Create(entity);");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine();
        }

        private static void AppendUpdateMethod(StringBuilder classDefinition, string primaryKeyColumn)
        {
            classDefinition.AppendLine("    public void Update()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine("        Entity entity = MapPropertiesToEntity();");
            classDefinition.AppendLine($"        entity.Id = this.{primaryKeyColumn};");
            classDefinition.AppendLine("        _service.Update(entity);");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine();
        }

        private static void AppendDeleteMethod(StringBuilder classDefinition, string entityLogicalName, string primaryKeyColumn)
        {
            classDefinition.AppendLine("    public void Delete()");
            classDefinition.AppendLine("    {");
            classDefinition.AppendLine($"        _service.Delete(\"{entityLogicalName}\", this.{primaryKeyColumn});");
            classDefinition.AppendLine("    }");
            classDefinition.AppendLine();
        }

        private static string ConvertAttributeTypeToCSharpType(XmlNode attributeNode)
        {
            string type = attributeNode.SelectSingleNode("Type")?.InnerText;
            switch (type)
            {
                case "multiselectpicklist":
                    return "object";
                case "partylist":
                    return "Party[]";
                case "status":
                    return "int?";
                case "state":
                    return "StateCode";
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
                case "customer":
                    return "EntityReference";
                case "datetime":
                    return "DateTime";
                case "bit":
                    return "bool";
                case "nvarchar":
                case "ntext":
                    return "string";
                case "bool":
                    return "bool";
                case "picklist":
                    string key = attributeNode.SelectSingleNode("OptionSetName")?.InnerText;
                    return LogicalNameToEnumMap.TryGetValue(key ?? string.Empty, out string optionSetName) ? optionSetName : "int";
                default:
                    return "object";
            }
        }

        // --------------- Utilities: Logging, IO, CLI ---------------

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("===============================================");
            Console.WriteLine(" Dataverse Wrapper Generator");
            Console.WriteLine(" Generate enums and entity wrappers from a zip");
            Console.WriteLine("===============================================");
            Console.ResetColor();
        }

        private static void PrintHelp()
        {
            WriteHr();
            Console.WriteLine("Usage:");
            Console.WriteLine("  DataverseWrapper -z <path_to_solution_zip> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -z, --zip <path>         Path to Dataverse solution zip containing customizations.xml (required)");
            Console.WriteLine("  -o, --out <dir>          Output directory (default: ./GeneratedClasses_{timestamp})");
            Console.WriteLine("  -f, --filter <text>      Only generate for entities whose display name contains text");
            Console.WriteLine("  -v, --verbose            Verbose logging");
            Console.WriteLine("  -y, --yes                Overwrite existing files without prompt");
            Console.WriteLine("  -h, --help               Show help");
            WriteHr();
        }

        private static void WriteHr()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(new string('-', 46));
            Console.ResetColor();
        }

        private static void LogInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[info] {message}");
            Console.ResetColor();
        }

        private static void LogWarn(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[warn] {message}");
            Console.ResetColor();
        }

        private static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[error] {message}");
            Console.ResetColor();
        }

        private static void LogSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ok] {message}");
            Console.ResetColor();
        }

        private static void EnsureDirectory(string dir)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                if (_verbose) LogInfo($"Created directory: {dir}");
            }
        }

        private static void SafeWriteAllText(string path, string contents)
        {
            if (File.Exists(path) && !_overwrite)
            {
                // Ask user before overwrite
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"File exists: {path}. Overwrite? (y/N): ");
                Console.ResetColor();
                var key = Console.ReadKey();
                Console.WriteLine();
                if (key.KeyChar != 'y' && key.KeyChar != 'Y')
                {
                    LogWarn("Skipped overwrite.");
                    return;
                }
            }

            File.WriteAllText(path, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static string GetArg(string[] args, string shortName, string longName)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(shortName, StringComparison.OrdinalIgnoreCase) ||
                    args[i].Equals(longName, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length) return args[i + 1];
                }
                // Also support --key=value
                if (args[i].StartsWith(longName + "=", StringComparison.OrdinalIgnoreCase))
                {
                    var split = args[i].Split(new[] { '=' }, 2);
                    if (split.Length == 2) return split[1];
                }
            }
            return null;
        }

        private static string MakeUniqueIdentifier(string candidate, HashSet<string> used)
        {
            string baseName = SanitizeString(candidate);
            string unique = baseName;
            int suffix = 2;
            while (!used.Add(unique))
            {
                unique = $"{baseName}_{suffix}";
                suffix++;
            }
            return unique;
        }
    }

    // Placeholder Party type to compile if not present in your project
    public class Party
    {
        public Guid Id { get; set; }
        public string EntityType { get; set; }
    }
}
