using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Reflection;
using Microsoft.Win32;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using DevExpress.Mvvm.DataAnnotations;

namespace TestGmailApp
{
    public enum SettingsType { Dashboard, Filter, Grid, LayoutControl, Ribbon, TreeList, None };

    public class SettingsFileBase
    {
        #region MEMBERS

        private string _name = "";

        public string Name
        {
            get
            {
                return _name;
            }

            set
            {
                _name = value;
            }
        }

        private string _prefix = "";

        public string Prefix
        {
            get
            {
                return _prefix;
            }

            set
            {
                _prefix = value;
            }
        }


        public bool xmlFileSaved = false;

        [XmlIgnore, Hidden]
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        public object this[string propertyName]
        {
            get
            {
                return this.GetNestedValue(propertyName);

            }

            set
            {

                this.SetNestedValue(propertyName, value);

            }

        }

        #endregion

        #region NESTED VALUE

        protected PropertyInfo GetProperty(string fieldname, Type type)
        {
            PropertyInfo resval = null;

            if (type == null ||
                String.IsNullOrEmpty(fieldname))
                return resval;

            resval = type.GetProperty(fieldname);

            return resval;
        }

        protected object GetNestedValue(string fieldNavigation)
        {
            object resval = null;

            if (String.IsNullOrEmpty(fieldNavigation))
                return resval;


            String[] path = fieldNavigation.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            PropertyInfo prevProp = null;
            PropertyInfo currrentProp = null;
            object currentItem = this;
            foreach (string p in path)
            {
                if (prevProp == null)
                {
                    currrentProp = GetProperty(p, currentItem.GetType());
                }
                else
                {
                    currrentProp = GetProperty(p, prevProp.PropertyType);
                }

                if (currrentProp == null ||
                    !currrentProp.CanRead)
                {
                    currentItem = null;
                    currrentProp = null;
                    break;
                }

                if (currentItem != null)
                    currentItem = currrentProp.GetValue(currentItem);

                prevProp = currrentProp;
            }

            resval = currentItem;

            return resval;
        }



        protected void SetNestedValue(string fieldNavigation, object value)
        {
            if (String.IsNullOrEmpty(fieldNavigation))
                return;


            String[] path = fieldNavigation.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);

            PropertyInfo prevProp = null;
            PropertyInfo currrentProp = null;
            object currentItem = this;
            object prevItem = null;
            foreach (string p in path)
            {
                prevProp = currrentProp;
                prevItem = currentItem;

                if (prevProp == null)
                {
                    currrentProp = GetProperty(p, currentItem.GetType());
                }
                else
                {
                    currrentProp = GetProperty(p, prevProp.PropertyType);
                }

                if (currrentProp == null ||
                    !currrentProp.CanRead)
                {
                    currentItem = null;
                    currrentProp = null;
                    break;
                }

                currentItem = currrentProp.GetValue(currentItem);

            }

            if (prevItem != null &&
                currrentProp != null &&
                currrentProp.CanWrite)
            {
                if (value != null &&
                    value.GetType() != currrentProp.PropertyType)
                {
                    value = Utilities.Parse(String.Format("{0}", value), currrentProp.PropertyType);
                }
                currrentProp.SetValue(prevItem, value);
            }

        }

        #endregion

        /// <summary>
        /// Loads settings from the registry
        /// </summary>
        /// <param name="registryPath">the full registry key for the settings</param>
        /// 
        public virtual void LoadFromRegistry(string registryPath)
        {

            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath);
            if (key == null)
                return;
            using (key)
            {
                try
                {
                    PropertyInfo[] properties = this.GetType().GetProperties();

                    foreach (PropertyInfo property in properties)
                    {
                        if (property.CanWrite)
                        {
                            string propName = property.Name;
                            object value = key.GetValue(propName);

                            if (value == null)
                                value = Utilities.GetDefaultValue(property.PropertyType);
                            if (value == null)
                                return;

                            property.SetValue(this, value);
                        }
                    }
                }
                finally
                {
                    key.Close();
                }
            }
        }

        /// <summary>
        /// Saves the settings to the registry
        /// </summary>
        /// <param name="registryPath">the full registry key for the settings</param>
        public virtual void SaveToRegistry(string registryPath)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(registryPath);
            if (key == null)
                return;
            using (key)
            {
                try
                {
                    PropertyInfo[] properties = this.GetType().GetProperties();

                    foreach (PropertyInfo property in properties)
                    {
                        if (property.CanRead)
                        {
                            string propName = property.Name;
                            object value = property.GetValue(this);

                            if (value == null)
                                return;

                            key.SetValue(propName, value);
                        }
                    }
                }
                finally
                {
                    key.Close();
                }
            }
        }

        /// <summary>
        /// Returns the Folder that contains ta settings files
        /// </summary>
        /// <param name="local">if true, root folder is AppData\Local else AppData\Roaming.
        /// The default value is false.</param>
        /// <returns></returns>
        public virtual string GetFilePath(bool local = false)
        {
            string resval = (local ?
                      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                     : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

            if (!String.IsNullOrEmpty(this.Prefix))
            {
                resval += String.Format(@"\{0}", this.Prefix);

                // if (!Directory.Exists(resval))
                //    Directory.CreateDirectory(resval);
            }

            return resval;
        }

        /// <summary>
        /// Saves settings to file
        /// </summary>
        public void SaveToXmlFile()
        {
            SaveToXmlFile(false);
        }

        /// <summary>
        /// Loads settings from file
        /// </summary>
        public void LoadFromXmlFile()
        {
            LoadFromXmlFile(false);
        }

        /// <summary>
        /// Saves settings to file
        /// </summary>
        /// <param name="local">if true, root folder is AppData\Local else AppData\Roaming.
        /// The default value is false.</param>
        public void SaveToXmlFile(bool local)
        {
            xmlFileSaved = false;

            if (String.IsNullOrEmpty(this.Name))
                return;

            SaveToXmlFile(String.Format(@"{0}.xml", this.Name), local);
        }

        /// <summary>
        /// Loads settings from file
        /// </summary>
        /// <param name="local"></param>
        public void LoadFromXmlFile(bool local)
        {
            if (String.IsNullOrEmpty(this.Name))
                return;

            LoadFromXmlFile(String.Format(@"{0}.xml", this.Name), local);
        }

        /// <summary>
        /// Saves settings to file
        /// </summary>
        /// <param name="filename">the name of the file to save</param>
        /// <param name="local">if true, root folder is AppData\Local else AppData\Roaming.
        /// The default value is false.</param>
        public void SaveToXmlFile(string filename, bool local)
        {
            string path = GetFilePath(local);
            string fullfilename = String.Format(@"{0}\{1}", path, filename);

            this.SaveToXmlFile(fullfilename);
        }

        /// <summary>
        /// Loads settings from file
        /// </summary>
        /// <param name="filename">the name of the file to save</param>
        /// <param name="local">if true, root folder is AppData\Local else AppData\Roaming.
        /// The default value is false.</param>
        public void LoadFromXmlFile(string filename, bool local)
        {
            string path = GetFilePath(local);
            string fullfilename = String.Format(@"{0}\{1}", path, filename);

            this.LoadFromXmlFile(fullfilename);
        }

        /// <summary>
        /// Loads settings from file
        /// </summary>
        /// <param name="fullfilename">the full name (path + name) of the file to load</param>
        public virtual void LoadFromXmlFile(string fullfilename)
        {

            if (!File.Exists(fullfilename))
                return;

            using (TextReader reader = new StreamReader(fullfilename))
            {
                try
                {

                    XmlSerializer serializer = new XmlSerializer(this.GetType());
                    object settings = serializer.Deserialize(reader);

                    this.LoadFrom(settings);


                }
                catch (Exception ex)
                {
                   
                }
                finally
                {
                    reader.Close();
                }
            }


        }

        /// <summary>
        /// Saves settings to file
        /// </summary>
        /// <param name="fullfilename">the full name (path + name) of the file to save</param>
        public virtual void SaveToXmlFile(string fullfilename)
        {
            if (String.IsNullOrEmpty(fullfilename))
                return;

            Utilities.CheckCreateFilePath(fullfilename);

            using (XmlTextWriter writer = new XmlTextWriter(fullfilename, Encoding.UTF8))
            {
                writer.Formatting = Formatting.Indented;
                writer.Indentation = 4;

                try
                {
                    XmlSerializer ser = new XmlSerializer(this.GetType());

                    ser.Serialize(writer, this);

                    xmlFileSaved = true;
                }
                catch
                {
                    xmlFileSaved = false;
                }
                finally
                {
                    writer.Close();
                }
            }

        }

        public void FromXmlString(string s, Encoding encoding = null)
        {
            if (string.IsNullOrEmpty(s))
                return;

            if (encoding == null)
                encoding = Encoding.UTF8;

            using (MemoryStream ms = new MemoryStream(encoding.GetBytes(s)))
            {
                try
                {

                    XmlSerializer serializer = new XmlSerializer(this.GetType());
                    object settings = serializer.Deserialize(ms);

                    this.LoadFrom(settings);


                }
                catch (Exception ex)
                {                   
                  
                }
                finally
                {
                    ms.Close();
                }
            }
        }

        public string ToXmlString(Encoding encoding = null)
        {
            string resval = "";

            if (encoding == null)
                encoding = Encoding.UTF8;

            using (MemoryStream ms = new MemoryStream())
            {
                try
                {
                    XmlSerializer ser = new XmlSerializer(this.GetType());

                    ser.Serialize(ms, this);
                    resval = encoding.GetString(ms.GetBuffer());
                }
                catch (Exception ex)
                {
                }
                finally
                {
                    ms.Close();
                }
            }

            return resval;
        }

        protected virtual void LoadFrom(object source)
        {
            if (source == null)
                return;

            var properties = this.GetType().GetProperties().ToList().FindAll(t => t.GetCustomAttribute(typeof(XmlIgnoreAttribute)) == null);


            foreach (PropertyInfo property in properties)
            {
                if (property.CanWrite)
                {
                    string propName = property.Name;
                    PropertyInfo sourceProp = source.GetType().GetProperty(propName);
                    if (sourceProp == null ||
                        !sourceProp.CanRead)
                        continue;

                    object value = sourceProp.GetValue(source);

                    if (value == null)
                        value = Utilities.GetDefaultValue(property.PropertyType);
                    if (value == null)
                        return;

                    property.SetValue(this, value);

                }
            }
        }

        #region Settings Items

        private List<SettingItem> FindItemList()
        {
            List<SettingItem> resval = null;

            PropertyInfo[] properties = this.GetType().GetProperties();

            foreach (PropertyInfo p in properties)
            {
                if (p.PropertyType.Equals(typeof(List<SettingItem>)))
                {
                    resval = p.GetValue(this) as List<SettingItem>;
                    break;
                }
            }


            return resval;
        }

        public SettingItem GetItem(string itemName)
        {
            List<SettingItem> itemList = FindItemList();

            SettingItem resval = null;

            if (itemList == null ||
                String.IsNullOrEmpty(itemName))
                return resval;

            resval = itemList.Find(t => (t.Name.Equals(itemName, StringComparison.InvariantCultureIgnoreCase)));

            return resval;
        }

        public object GetItemValue(string itemName)
        {
            object resval = null;

            SettingItem item = GetItem(itemName);

            if (item == null)
                return resval;

            resval = item.Value;

            return resval;
        }

        public void SetItemValue(string itemName, object value)
        {
            SettingItem item = GetItem(itemName);

            if (item == null)
                return;

            item.Value = value;

        }

        public void AddItem(SettingItem item)
        {
            List<SettingItem> itemList = FindItemList();

            if (item == null ||
                itemList == null ||
                this.GetItem(item.Name) != null)
                return;

            itemList.Add(item);
        }

        #endregion
    }

    public class SettingItem
    {
        private string _name = "";
        private string _type = null;
        private object _value = null;

        public string Name
        {
            get
            {
                return _name;
            }

            set
            {
                _name = value;
            }
        }

        public string Type
        {
            get
            {
                return _type;
            }

            set
            {
                _type = value;
            }
        }

        public object Value
        {
            get
            {
                return _value;
            }

            set
            {
                _value = value;
            }
        }

        public SettingItem()
        {

        }

        public SettingItem(string name, Type type, object value)
            : this()
        {
            this.Name = name;
            this.Type = type.FullName;
            this.Value = value;
        }
    }

    public class Utilities
    {
        public static DateTime MIN_DATE = new DateTime(1905, 3, 14, 0, 0, 0);

        public static void CheckCreateFilePath(string fileFullName)
        {
            if (string.IsNullOrEmpty(fileFullName))
                return;

            string path = Path.GetDirectoryName(fileFullName);

            if (String.IsNullOrEmpty(path))
                return;

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        public static object GetDefaultValue(Type type)
        {
            object resval = null;

            if (type.Equals(typeof(String)))
                resval = "";
            else if ((type.Equals(typeof(Int16))) ||
                     (type.Equals(typeof(Int32))) ||
                     (type.Equals(typeof(Int64))) ||
                     (type.Equals(typeof(UInt16))) ||
                     (type.Equals(typeof(UInt32))) ||
                     (type.Equals(typeof(UInt64))))
                resval = 0;
            else if (type.Equals(typeof(Double)) ||
                     type.Equals(typeof(float)))
                resval = Convert.ToDouble(0);
            else if (type.Equals(typeof(Decimal)))
                resval = Convert.ToDecimal(0);
            else if (type.Equals(typeof(DateTime)))
                resval = MIN_DATE;
            else if (type.Equals(typeof(Boolean)))
                resval = false;

            return resval;
        }

        public static object Parse(string value, Type targetType)
        {
            object resval = null;

            if (String.IsNullOrEmpty(value))
                return resval;

            Type type = targetType;
            if (type.Name.StartsWith("Nullable", StringComparison.InvariantCultureIgnoreCase))
                type = type.GetGenericArguments()[0];

            if ((type.Equals(typeof(Int16))) ||
                     (type.Equals(typeof(Int32))) ||
                     (type.Equals(typeof(Int64))) ||
                     (type.Equals(typeof(UInt16))) ||
                     (type.Equals(typeof(UInt32))) ||
                     (type.Equals(typeof(UInt64))))
                resval = int.Parse(value);
            else if (type.Equals(typeof(Double)))
                resval = double.Parse(value);
            else if (type.Equals(typeof(Decimal)))
                resval = decimal.Parse(value);
            else if (type.Equals(typeof(DateTime)))
                resval = DateTime.Parse(value);
            else if (type.Equals(typeof(Boolean)))
                resval = bool.Parse(value);

            return resval;
        }

    }
}
