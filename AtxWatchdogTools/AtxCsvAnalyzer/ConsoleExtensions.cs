using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AtxCsvAnalyzer
{
    internal class ConsoleExtensions
    {
        public static void EditClass(ref object cls, string message)
        {
            PropertyInfo[] properties;
            Type type = cls.GetType();

            properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);


            bool quit = false;
            string lastMessage = null;
            while (!quit)
            {
                Console.Clear();
                Console.WriteLine(message);
                Console.WriteLine("Currently editing: " + type.Name);
                Console.WriteLine("{0} properties.", properties.Length);
                Console.WriteLine();
                Console.WriteLine("Select an operation:");
                Console.WriteLine("\t[g]. Get field value");
                Console.WriteLine("\t[s]. Set field value");
                Console.WriteLine("\t[c]. Clear field value");
                Console.WriteLine("\t[d]. Dump all values for the class");
                Console.WriteLine("\t[x]. Quit and save changes");
                Console.WriteLine();

                if (!string.IsNullOrWhiteSpace(lastMessage))
                {
                    Console.WriteLine(lastMessage);
                    lastMessage = null;
                    Console.WriteLine();
                }

                ConsoleKeyInfo key = Console.ReadKey(true);
                string propertyName;
                PropertyInfo property = null;

                switch (key.Key)
                {
                    case ConsoleKey.G:
                        propertyName = GetPropertyNameFromConsole(properties);
                        if (!string.IsNullOrWhiteSpace(propertyName))
                        {
                            for (int i = 0; i < properties.Length; i++)
                            {
                                if (string.Equals(properties[i].Name, propertyName, StringComparison.OrdinalIgnoreCase))
                                    property = properties[i];
                            }

                            if (property == null)
                            {
                                lastMessage = "Invalid property name.";
                                break;
                            }

                            if (property.PropertyType.IsEnum)
                            {
                                string[] names = Enum.GetNames(property.PropertyType);
                                StringBuilder str = new StringBuilder();
                                foreach (string n in names)
                                {
                                    if (str.Length > 0)
                                        str.Append(", ");
                                    str.Append(n);
                                }

                                lastMessage = "Current value: " + (property.GetValue(cls) ?? "<empty>") + "\nPossible values: " + str.ToString();
                            }
                            else
                                lastMessage = "Current value: " + (property.GetValue(cls) ?? "<empty>") + "\nValue Type: " + property.PropertyType.Name;
                        }
                        break;
                    case ConsoleKey.S:
                        propertyName = GetPropertyNameFromConsole(properties);
                        if (!string.IsNullOrWhiteSpace(propertyName))
                        {
                            for (int i = 0; i < properties.Length; i++)
                            {
                                if (string.Equals(properties[i].Name, propertyName, StringComparison.OrdinalIgnoreCase))
                                    property = properties[i];
                            }

                            if (property == null)
                            {
                                lastMessage = "Invalid property name.";
                                break;
                            }

                            Console.WriteLine("Current value: " + property.GetValue(cls) ?? "<empty>");
                            Console.Write("Enter the new value for the <{0}> property <{1}>: ", property.PropertyType.Name, property.Name);
                            string strvalue = Console.ReadLine();
                            object value;

                            if (string.IsNullOrWhiteSpace(strvalue))
                                value = null;
                            else
                            {
                                if (!TryCastStringToType(strvalue, property.PropertyType, out value))
                                {
                                    lastMessage = "Invalid property value.";
                                    break;
                                }
                            }

                            try
                            {
                                property.SetValue(cls, Convert.ChangeType(value, property.PropertyType));
                            }
                            catch (Exception e)
                            {
                                lastMessage = e.Message;
                            }
                        }
                        break;
                    case ConsoleKey.C:
                        propertyName = GetPropertyNameFromConsole(properties);
                        if (!string.IsNullOrWhiteSpace(propertyName))
                        {
                            for (int i = 0; i < properties.Length; i++)
                            {
                                if (string.Equals(properties[i].Name, propertyName, StringComparison.OrdinalIgnoreCase))
                                    property = properties[i];
                            }

                            if (property == null)
                            {
                                lastMessage = "Invalid property name.";
                                break;
                            }

                            if (property.PropertyType == typeof(int) || property.PropertyType == typeof(long) || property.PropertyType == typeof(float) || property.PropertyType == typeof(double))
                                property.SetValue(cls, Convert.ChangeType(0, property.PropertyType));
                            else
                                property.SetValue(cls, null);
                        }
                        break;
                    case ConsoleKey.D:
                        SortedList<string, PropertyInfo> set = new SortedList<string, PropertyInfo>(properties.Length, StringComparer.OrdinalIgnoreCase);
                        foreach (PropertyInfo p in properties)
                            set.Add(p.Name, p);

                        Console.Clear();
                        Console.WriteLine("Current values for the edited class:");

                        for (int i = 0; i < set.Count; i++)
                            Console.WriteLine("\t{0} = {1}", set.ElementAt(i).Key, set.ElementAt(i).Value.GetValue(cls)?? "<empty>");

                        Console.WriteLine();
                        Console.WriteLine("-- Press any key to continue --");
                        Console.ReadKey(true);
                        break;
                    case ConsoleKey.X:
                        quit = true;
                        break;
                    default:
                        lastMessage = "Invalid operation specified.";
                        break;
                }
            }
        }

        public static bool TryCastStringToType(string value, Type type, out object result)
        {
            if (type == typeof(int))
            {
                bool ok = int.TryParse(value, out int v);
                result = v;
                return ok;
            }

            if (type == typeof(long))
            {
                bool ok = long.TryParse(value, out long v);
                result = v;
                return ok;
            }

            if (type == typeof(float))
            {
                bool ok = float.TryParse(value, out float v);
                result = v;
                return ok;
            }

            if (type == typeof(double))
            {
                bool ok = double.TryParse(value, out double v);
                result = v;
                return ok;
            }

            if (type == typeof(string))
            {
                result = value;
                return true;
            }

            if (type.IsEnum)
            {
                try
                {
                    var v = Activator.CreateInstance(type);
                    result = Enum.Parse(type, value, true);
                    return true;
                }
                catch (Exception)
                {
                    result = null;
                    return false;
                }
            }

            if (type == typeof(DateTime))
            {
                bool ok = DateTime.TryParse(value, out DateTime d);
                result = d;
                return ok;
            }

            if (type == typeof(bool))
            {
                string[] trueStrings = {"yes", "1", "on", "true", "y", "t"};

                result = false;
                foreach (string str in trueStrings)
                    if (string.Equals(value.Trim(), str, StringComparison.OrdinalIgnoreCase))
                        result = true;

                return true;
            }

            result = null;
            return false;
        }

        public static string GetPropertyNameFromConsole(PropertyInfo[] properties)
        {
            Console.Clear();
            Console.WriteLine("Available properties:");
            Console.WriteLine();

            SortedSet<string> names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PropertyInfo p in properties)
                names.Add(p.Name);

            for (int i = 0; i < names.Count; i++)
            {
                if (i % 2 > 0)
                    Console.Write("\t\t");
                Console.Write("[{0}] ", i);
                Console.Write(names.ElementAt(i));

                if (i % 2 > 0)
                    Console.WriteLine();
            }
            Console.WriteLine();
            Console.Write("Input the property name or its index: ");

            string val = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(val))
                return null;

            if (int.TryParse(val, out int index))
            {
                if (index < 0 || index >= properties.Length)
                    return null;

                foreach (PropertyInfo p in properties)
                    if (string.Equals(names.ElementAt(index), p.Name, StringComparison.OrdinalIgnoreCase))
                        return p.Name;
            }

            return val.Trim();
        }
    }
}
