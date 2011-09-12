using System.Windows;
using Microsoft.Phone.Marketplace;

namespace Colorify
{
    public class ApplicationLicense
    {
        public static string AD_KEY = "COLORIFY_AD_KEYX";
        public static string AD_MESSAGE = "Trial mode has all the funtionally of full version with ads though.";

        public static bool IsInTrialMode
        {
            get
            {
                #if TRIAL                 
                    return true; 
                #else                 
                    LicenseInformation license = new LicenseInformation();                 
                    return license.IsTrial(); 
                #endif
            }
        }

        public static void CheckTrialMode(UIElement adControl)
        {
            if (ApplicationLicense.IsInTrialMode)
            {
                if (SettingsProvider.Get(ApplicationLicense.AD_KEY) == null)
                {
                    MessageBox.Show(ApplicationLicense.AD_MESSAGE);
                    SettingsProvider.Set(ApplicationLicense.AD_KEY, "1");
                }
                adControl.Visibility = Visibility.Visible;
            }
            else
            {
                adControl.Visibility = Visibility.Collapsed;
            }
        }
    }
}
