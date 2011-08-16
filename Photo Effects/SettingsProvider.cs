using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.IO.IsolatedStorage;

namespace Photo_Colorify
{
    public class SettingsProvider
    {
        private IsolatedStorageSettings isolatedStore;

        public SettingsProvider()
        {
             isolatedStore = IsolatedStorageSettings.ApplicationSettings;
        }

        public string Get(string key)
        {
            if (isolatedStore.Contains(key) && isolatedStore[key] != null)
            {
                return isolatedStore[key].ToString();
            }
            return null;
        }

        public void Set(string key, string value)
        {
            if (isolatedStore.Contains(key)) isolatedStore[key] = value;
            else isolatedStore.Add(key, value);
        }

        public bool Delete(string key)
        {
            return isolatedStore.Remove(key);
        }
    }
}
