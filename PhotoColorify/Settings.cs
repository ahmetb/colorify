using System.IO.IsolatedStorage;

namespace PhotoColorify
{
    public class SettingsProvider
    {
        static IsolatedStorageSettings isolatedStore = IsolatedStorageSettings.ApplicationSettings;

        public static string Get(string key)
        {
            string result=null;
            if (isolatedStore.Contains(key))
            {
                result = (string) isolatedStore[key];
            }
            return result;
        }

        public static int Get(string key, int defaultvalue)
        {
            if (isolatedStore.Contains(key))
            {
                string result = (string)isolatedStore[key];
                return int.Parse(result);
            }
            return defaultvalue;
        }

        public static void Set(string key, string value)
        {
            if (isolatedStore.Contains(key)) 
                isolatedStore[key] = value;
            else 
                isolatedStore.Add(key, value);

            isolatedStore.Save();
        }

        public static void Set(string key, int value)
        {
            Set(key, ""+value);
        }

        public static bool Delete(string key)
        {
            if (isolatedStore.Contains(key))
            {
                isolatedStore[key] = null;
            }

            bool del = isolatedStore.Remove(key);
            isolatedStore.Save();

            return del;
        }
    }
}
