using Microsoft.Phone.Marketplace;

namespace Colorify
{
    public class ApplicationLicense
    {
        public static string AD_KEY = "COLORIFY_AD_KEY1";
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
    }
}
